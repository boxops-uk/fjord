//! **`EMFILE` on the accept loop ends one connection, never the server.**
//!
//! The failure this pins is the one that was in the tree: `accept().await?` propagated
//! out of the loop, so the *first* time the process ran out of descriptors the server
//! exited and every live connection went with it — a thousand idle sockets, needing no
//! valid handshake, to take down a database. The fix is that the loop has no fatal
//! outcome; this is the proof, and it is a proof only if the failure is real, so this
//! provokes an actual `EMFILE` rather than a mocked one.
//!
//! # Why this file holds exactly one test
//!
//! It lowers `RLIMIT_NOFILE` **for the whole process** and then exhausts it. Cargo runs
//! the tests in one binary as threads of one process, so a second test here — or a
//! test in a file that also ran in this binary — would be running with no descriptors
//! left through no fault of its own. An integration test is its own process, which is
//! what makes that containable.
//!
//! # How the exhaustion is made deterministic
//!
//! Flooding until something breaks is a race: the loop only reports `EMFILE` if a
//! connection is *pending* when the descriptors run out, and a fast accept loop can
//! drain the backlog first. So the descriptors are taken deliberately, with one spare
//! held back:
//!
//! 1. take every descriptor the process may have, keeping one in hand;
//! 2. release it and spend it on a `connect` — now something is pending and nothing is
//!    available, so the server's `accept` **must** fail;
//! 3. give the descriptors back, and the same connection gets served.
//!
//! What that separates, which a flood cannot, is *survived* from *lucky*: step 3
//! answering proves the loop went round again rather than that it was never tested. It
//! takes all of them back rather than one, because a handshake reads the catalog — the
//! filesystem *is* the catalog (`ops-I7`) — and this is a test about the accept loop,
//! not about how few descriptors a session can be served with.

use std::{
    fs::File,
    io::{Read, Write},
    os::unix::net::UnixStream,
    sync::Arc,
    thread,
    time::{Duration, Instant},
};

use fjord_schema::schema::{Predicate, PredicateTy, Schema};
use fjord_server::{Registry, registry::Schemas, server::Listener};
use fjord_store_fjall::catalog::Catalog;
use fjord_wire::{
    FrameHeader, FrameKind, Mode, Startup, StreamId, encode_frame, frame,
    protocol::{self, kinds},
};
use lasso::Rodeo;

/// The soft descriptor limit this test runs under.
///
/// Low enough that taking every descriptor is a few dozen `open` calls rather than a
/// million, and high enough that the server, the store and the runtime have what they
/// already opened. Nothing here opens a file after the limit drops except the
/// deliberate exhaustion.
const LOWERED: u64 = 256;

/// The soft limit, restored when this is dropped.
///
/// A test that left the process at 256 descriptors would be a test that broke whatever
/// ran next — and on the panic path, which is exactly when it is hardest to notice.
struct FdLimit {
    original: libc::rlimit,
}

impl FdLimit {
    fn lower_to(soft: u64) -> FdLimit {
        let mut original = libc::rlimit {
            rlim_cur: 0,
            rlim_max: 0,
        };

        // SAFETY: `getrlimit` writes a `rlimit` through the pointer and reads nothing
        // else; the resource is a constant of the platform.
        assert_eq!(
            unsafe { libc::getrlimit(libc::RLIMIT_NOFILE, &raw mut original) },
            0,
            "the descriptor limit must be readable for this test to mean anything"
        );

        let lowered = libc::rlimit {
            rlim_cur: soft.min(original.rlim_cur),
            rlim_max: original.rlim_max,
        };

        // SAFETY: as above; lowering the soft limit is always permitted, and the hard
        // limit is carried over unchanged.
        assert_eq!(
            unsafe { libc::setrlimit(libc::RLIMIT_NOFILE, &raw const lowered) },
            0
        );

        FdLimit { original }
    }
}

impl Drop for FdLimit {
    fn drop(&mut self) {
        // SAFETY: as above. Raising the soft limit back to what it was is permitted
        // because the hard limit has not moved.
        unsafe { libc::setrlimit(libc::RLIMIT_NOFILE, &raw const self.original) };
    }
}

fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let file = rodeo.get_or_intern("src.File");

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![Predicate {
            name: file,
            key: PredicateTy::Str,
            value: None,
        }]),
    )
}

fn handshake(stream: &mut UnixStream, fingerprint: u64) -> (FrameHeader, Vec<u8>) {
    let startup = protocol::encode_startup(&Startup {
        version: protocol::VERSION,
        database: "code".to_owned(),
        mode: Mode::ReadOnly,
        schema_fingerprint: fingerprint,
        predicates: vec![],
    });

    let mut out = vec![];
    encode_frame(&mut out, kinds::STARTUP, StreamId(0), &startup).expect("a frame");
    stream.write_all(&out).expect("a write");

    let mut head = [0u8; frame::HEADER_LEN];
    stream.read_exact(&mut head).expect("a frame header");
    let header = frame::decode_header(&head).expect("a header");

    let mut payload = vec![0u8; header.length as usize];
    stream.read_exact(&mut payload).expect("a payload");

    (header, payload)
}

fn queries(stream: &mut UnixStream, on: StreamId) {
    let mut out = vec![];
    encode_frame(&mut out, kinds::QUERY, on, b"F where src.File F").expect("a frame");
    stream.write_all(&out).expect("a write");

    for expected in [FrameKind::ROW_DESCRIPTION, kinds::COMPLETE] {
        let mut head = [0u8; frame::HEADER_LEN];
        stream.read_exact(&mut head).expect("a frame header");
        let header = frame::decode_header(&head).expect("a header");

        let mut payload = vec![0u8; header.length as usize];
        stream.read_exact(&mut payload).expect("a payload");

        assert_eq!(header.kind, expected);
    }
}

#[test]
fn the_server_survives_running_out_of_descriptors_and_serves_again_when_they_free() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fjord.sock");

    let schema = schema();
    let fingerprint = fjord_schema::fingerprint::of(&schema);

    let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
    catalog.create("code", &schema).expect("a database");

    let (registry, _listing) = Registry::open(catalog, Schemas::new("")).expect("a registry");
    let registry = Arc::new(registry);

    // A cap far above anything this test opens: the subject here is the *descriptor*
    // limit, and a refusal at the cap would be a different mechanism answering.
    let listener = Listener::bind(&socket)
        .expect("a socket")
        .with_max_connections(4096);

    {
        let registry = Arc::clone(&registry);
        thread::spawn(move || {
            let _ = listener.run_blocking(registry);
        });
    }

    // A connection established *before* the exhaustion, and one the server must still
    // be serving after it: the old failure dropped exactly these.
    let mut established = UnixStream::connect(&socket).expect("a connection");
    assert_eq!(
        handshake(&mut established, fingerprint).0.kind,
        kinds::READY
    );

    // Held before the limit drops, so there is something to release afterwards.
    let spare_for_the_connect = File::open("/dev/null").expect("a spare descriptor");

    let _limit = FdLimit::lower_to(LOWERED);

    // ---- take everything else
    let mut taken = vec![];
    while let Ok(file) = File::open("/dev/null") {
        taken.push(file);

        assert!(
            taken.len() < 100_000,
            "the limit was not lowered: this would exhaust the machine, not the test"
        );
    }

    let before = registry.stats().accept_failures();

    // ---- one pending connection, and no descriptor to accept it with
    drop(spare_for_the_connect);

    let mut pending = loop {
        match UnixStream::connect(&socket) {
            Ok(stream) => break stream,
            // Something else in this process took the released descriptor first. Free
            // another and try again: a test about the server must not fail on a race
            // inside the test.
            Err(_) => assert!(
                taken.pop().is_some(),
                "no descriptor could be spared for the connection this test is about"
            ),
        }
    };

    let deadline = Instant::now() + Duration::from_secs(5);
    while registry.stats().accept_failures() == before {
        assert!(
            Instant::now() < deadline,
            "no descriptor failure was ever reported: either the loop never hit one, \
             and this test proved nothing, or it died on one — which is the failure \
             being guarded"
        );
        thread::sleep(Duration::from_millis(10));
    }

    // ---- and now it can be served
    //
    // The loop is asleep on its backoff at this point, so the connection is answered
    // one backoff later at the latest — which is the whole claim: it went round again.
    drop(taken);
    let (header, payload) = handshake(&mut pending, fingerprint);
    assert_eq!(
        header.kind,
        kinds::READY,
        "a server that survived the failure must serve the connection that caused it: {:?}",
        protocol::decode_error(&payload)
    );

    // ---- and the connections it already had are untouched
    queries(&mut established, StreamId(1));
    queries(&mut pending, StreamId(1));
}
