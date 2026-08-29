//! **What the server does when it runs out of something.**
//!
//! Two mechanisms, one subject. A file descriptor is the resource every connection
//! costs and the resource fjall spends on data, so the two compete — and the failure
//! that follows is not the one it looks like:
//!
//! - **An `accept` that fails is not the loop's death.** `EMFILE` is a statement about
//!   the *process*, not about the listener, and the connections already being served
//!   are the descriptors it is complaining about. A loop that propagated it ended the
//!   server and dropped every live connection to refuse one new one — which is why
//!   [`after_accept_error`] has no fatal outcome to return.
//! - **A cap below the descriptor limit is what keeps a fresh query answerable.**
//!   Without one, a flood consumes every descriptor and the server is *alive and
//!   unreachable*: a health probe, an admin query and a search all fail alike, because
//!   the last descriptor went to whoever asked most recently rather than to whoever
//!   mattered. [`Admission`] refuses past the cap so the rest stays spendable.
//!
//! # Politeness is bounded too
//!
//! A refusal is itself a descriptor: it lives from the accept until the peer closes,
//! and a peer that will not read what it was sent holds one for the whole linger. Under
//! a burst that is hundreds of descriptors spent saying *no* — out of the reserve the
//! cap just made. So refusals in flight are capped at a share of the connection cap,
//! and past that a connection is closed without a word, which is what the kernel would
//! have done with it. Measured: 1,500 connections against a cap of 512 under a
//! 1,024-descriptor limit reached `EMFILE` through the refusal path alone.
//!
//! # The permit is held, not counted
//!
//! [`Admitted`] releases on `Drop`, for the reason [`stats`](crate::stats) states about
//! gauges: a connection ends by returning, by failing, by being cancelled and by
//! panicking, and a decrement written at the exit somebody had in mind is wrong the
//! first time a `?` is added above it. A leaked permit is permanent — the cap ratchets
//! down until the server admits nobody — so this is the one place the pattern is not
//! optional.
//!
//! # The default is a share of the limit, not a number
//!
//! Half the soft `RLIMIT_NOFILE`, and the other half is not spare: it is fjall's files,
//! the listeners, the readiness file, and the descriptors an admin query needs to
//! answer while the flood is happening. A tuned deployment says what it wants
//! (`--max-connections`); an untuned one gets a rule that moves with `ulimit -n`
//! instead of a constant that was right on one machine.

use std::{io, sync::Arc, time::Duration};

use tokio::sync::{OwnedSemaphorePermit, Semaphore, TryAcquireError};

/// How long the accept loop pauses after a descriptor-exhaustion failure.
///
/// Long enough that a closing connection's descriptor can be reused before the next
/// attempt, short enough to be invisible when the pressure passes. The alternative is
/// a loop that retries at full speed against a condition only somebody else can end,
/// which spends the core that would have run the queries already accepted.
pub const ACCEPT_BACKOFF: Duration = Duration::from_millis(50);

/// One in this many places under the cap may be spent on a refusal at a time.
///
/// Small on purpose: a refusal is a frame and a close, so a few in flight are enough to
/// answer a burst, and every one of them is a descriptor not available to a connection
/// being served.
const REFUSAL_SHARE: usize = 16;

/// The floor under refusals in flight, so a small cap can still say `busy` at all.
const MIN_REFUSALS: usize = 8;

/// The floor under the derived cap.
///
/// Not headroom — a refusal to derive a server that admits nobody from a limit so low
/// that nothing else would work either. Under it, staying alive is
/// [`after_accept_error`]'s job rather than this one's.
const MIN_CONNECTIONS: usize = 16;

/// What the accept loop does about an `accept` that failed.
///
/// **There is deliberately no third variant.** The type is the statement: whatever
/// `accept` reports, the loop carries on, because every error it can report is about
/// one connection or about a pressure that ends without the server's help.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum AcceptOutcome {
    /// The process is out of descriptors (`EMFILE`/`ENFILE`) or of memory for a
    /// socket. Retrying immediately would spin against a condition the loop cannot
    /// change: pause for [`ACCEPT_BACKOFF`] first.
    Backoff,
    /// One connection failed on the way in — the peer went away between the SYN and
    /// the accept, or the socket was in a state that connection alone is in. The next
    /// one is unaffected.
    Retry,
}

/// Classify an `accept` failure. **Never fatal** — see [`AcceptOutcome`].
#[must_use]
pub fn after_accept_error(error: &io::Error) -> AcceptOutcome {
    match error.raw_os_error() {
        // Out of descriptors per-process, out of them system-wide, out of buffer
        // space, out of memory: four spellings of "the machine has none right now",
        // and all four end without this loop doing anything except waiting.
        Some(libc::EMFILE | libc::ENFILE | libc::ENOBUFS | libc::ENOMEM) => AcceptOutcome::Backoff,
        _ => AcceptOutcome::Retry,
    }
}

/// The connection cap, and the permits that hold it up.
#[derive(Debug)]
pub struct Admission {
    permits: Arc<Semaphore>,
    refusals: Arc<Semaphore>,
    max: usize,
    max_refusals: usize,
}

/// One admitted connection's place under the cap, held for as long as this lives.
///
/// Owns its permit rather than borrowing one: a connection is a spawned task that
/// outlives the accept loop's stack frame.
#[derive(Debug)]
pub struct Admitted {
    _permit: OwnedSemaphorePermit,
}

/// One refusal's place in the much smaller budget for saying so.
#[derive(Debug)]
pub struct Refusing {
    _permit: OwnedSemaphorePermit,
}

impl Admission {
    /// A cap of exactly `max` concurrent connections, clamped into what a semaphore
    /// can hold.
    #[must_use]
    pub fn with_max(max: usize) -> Admission {
        let max = max.clamp(1, Semaphore::MAX_PERMITS);
        let max_refusals = (max / REFUSAL_SHARE).max(MIN_REFUSALS);

        Admission {
            permits: Arc::new(Semaphore::new(max)),
            refusals: Arc::new(Semaphore::new(max_refusals)),
            max,
            max_refusals,
        }
    }

    /// The default cap: half this process's soft descriptor limit.
    #[must_use]
    pub fn from_fd_limit() -> Admission {
        Admission::with_max(cap_for_fd_limit(fd_soft_limit()))
    }

    /// How many connections may be served at once.
    #[must_use]
    pub fn max(&self) -> usize {
        self.max
    }

    /// How many refusals may be in flight at once.
    #[must_use]
    pub fn max_refusals(&self) -> usize {
        self.max_refusals
    }

    /// How many are being served right now.
    #[must_use]
    pub fn live(&self) -> usize {
        self.max - self.permits.available_permits()
    }

    /// Take a place under the cap, or `None` if there is none to take.
    ///
    /// Never waits. A queue in front of the cap would be the thing the cap exists to
    /// prevent — descriptors held by connections nobody is serving — so the answer to
    /// a full server is a refusal the client can act on, not a wait it cannot see.
    #[must_use]
    pub fn try_admit(self: &Arc<Self>) -> Option<Admitted> {
        match Arc::clone(&self.permits).try_acquire_owned() {
            Ok(permit) => Some(Admitted { _permit: permit }),
            // `Closed` cannot happen — nothing closes this semaphore — and treating it
            // as "full" rather than unwrapping keeps a refusal from becoming a panic
            // in the one loop that must not have one.
            Err(TryAcquireError::NoPermits | TryAcquireError::Closed) => None,
        }
    }

    /// Take a place in the refusal budget, or `None` if there is none.
    ///
    /// `None` is not an error: it means the answer to this connection is a close
    /// rather than a sentence — which is what the kernel would have done, and what an
    /// unbounded politeness budget would have spent the reserve to avoid saying.
    #[must_use]
    pub fn try_refuse(self: &Arc<Self>) -> Option<Refusing> {
        match Arc::clone(&self.refusals).try_acquire_owned() {
            Ok(permit) => Some(Refusing { _permit: permit }),
            Err(TryAcquireError::NoPermits | TryAcquireError::Closed) => None,
        }
    }
}

/// The soft `RLIMIT_NOFILE`, or `u64::MAX` if the limit cannot be read.
///
/// Unreadable means uncapped by this rule, which is the honest default: the cap is a
/// share of a number, and inventing the number would be inventing the policy.
fn fd_soft_limit() -> u64 {
    let mut limit = libc::rlimit {
        rlim_cur: 0,
        rlim_max: 0,
    };

    // SAFETY: `getrlimit` writes a `rlimit` through the pointer and reads nothing
    // else; the resource is a constant of the platform.
    let read = unsafe { libc::getrlimit(libc::RLIMIT_NOFILE, &raw mut limit) };

    if read == 0 { limit.rlim_cur } else { u64::MAX }
}

/// The cap a descriptor limit implies. Pure, so the policy is testable without a
/// process to set a limit on.
fn cap_for_fd_limit(soft: u64) -> usize {
    let half = usize::try_from(soft / 2).unwrap_or(usize::MAX);
    half.clamp(MIN_CONNECTIONS, Semaphore::MAX_PERMITS)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn descriptor_exhaustion_backs_off_and_everything_else_retries() {
        for code in [libc::EMFILE, libc::ENFILE, libc::ENOBUFS, libc::ENOMEM] {
            assert_eq!(
                after_accept_error(&io::Error::from_raw_os_error(code)),
                AcceptOutcome::Backoff,
                "errno {code} is the machine being out of something"
            );
        }

        for code in [libc::ECONNABORTED, libc::EPROTO, libc::EINTR, libc::EINVAL] {
            assert_eq!(
                after_accept_error(&io::Error::from_raw_os_error(code)),
                AcceptOutcome::Retry,
                "errno {code} is about one connection"
            );
        }

        // An error with no errno at all — what a wrapper or a mock produces — still
        // has to be answered, and the answer is still not "stop serving".
        assert_eq!(
            after_accept_error(&io::Error::other("no errno here")),
            AcceptOutcome::Retry
        );
    }

    #[test]
    fn the_cap_is_half_the_descriptor_limit_with_a_floor() {
        assert_eq!(cap_for_fd_limit(1024), 512);
        assert_eq!(cap_for_fd_limit(65_536), 32_768);

        // A limit so small the share is meaningless: the floor answers, and the
        // accept loop's backoff is what actually holds the process up there.
        assert_eq!(cap_for_fd_limit(0), MIN_CONNECTIONS);
        assert_eq!(cap_for_fd_limit(8), MIN_CONNECTIONS);

        // `RLIM_INFINITY` is not a number to take half of — there are no descriptors
        // to run out of, so the rule that reserves them caps nothing.
        assert_eq!(cap_for_fd_limit(u64::MAX), Semaphore::MAX_PERMITS);
    }

    #[test]
    fn nothing_is_admitted_past_the_cap() {
        let admission = Arc::new(Admission::with_max(2));

        let first = admission.try_admit().expect("a place under the cap");
        let second = admission.try_admit().expect("a place under the cap");

        assert!(admission.try_admit().is_none(), "the cap must be a cap");
        assert_eq!(admission.live(), 2);

        drop(first);
        drop(second);
        assert_eq!(admission.live(), 0);
    }

    /// The property the `Drop` release exists for: a permit returned on a path nobody
    /// wrote an exit for. A leak here is permanent, so this is the guard that matters.
    #[test]
    fn a_place_is_returned_however_its_connection_ends() {
        let admission = Arc::new(Admission::with_max(1));
        let admitted = admission.try_admit().expect("a place under the cap");

        let unwound = std::panic::catch_unwind(std::panic::AssertUnwindSafe(move || {
            let _admitted = admitted;
            panic!("a connection's task failing is an ordinary outcome");
        }));

        assert!(unwound.is_err());
        assert_eq!(admission.live(), 0, "an unwind must return the permit");
        assert!(
            admission.try_admit().is_some(),
            "the next connection must be admissible"
        );
    }

    #[test]
    fn saying_no_is_bounded_and_the_budget_comes_back() {
        let admission = Arc::new(Admission::with_max(320));
        assert_eq!(
            admission.max_refusals(),
            20,
            "a share of the cap, not all of it"
        );

        let mut refusing: Vec<_> = (0..20)
            .map(|_| admission.try_refuse().expect("a place to say no from"))
            .collect();

        assert!(
            admission.try_refuse().is_none(),
            "an unbounded refusal path spends the descriptors the cap reserved"
        );

        refusing.pop();
        assert!(
            admission.try_refuse().is_some(),
            "a finished refusal frees its place"
        );
    }

    #[test]
    fn a_small_cap_can_still_say_no() {
        // The share of a cap this size rounds to nothing, and a server that cannot
        // refuse is one that closes connections without a word.
        assert_eq!(Admission::with_max(16).max_refusals(), MIN_REFUSALS);
    }

    #[test]
    fn a_cap_of_zero_still_serves_somebody() {
        let admission = Arc::new(Admission::with_max(0));
        assert_eq!(admission.max(), 1);
        assert!(admission.try_admit().is_some());
    }
}
