//! **The connection cap, over a socket.**
//!
//! The gap this closes is not "the server crashes" — that is
//! `an_accept_failure_is_not_fatal`'s subject. It is what a *saturated* server owes
//! the next caller: a refusal it can act
//! on, and descriptors left over for the connections already being served. A server
//! with no cap answers a flood by admitting all of it and then being alive and
//! unreachable, which from outside is indistinguishable from having died.
//!
//! The client is hand-rolled from `fjord-wire` alone, as `over_a_socket` is: a refusal
//! a client can only see by linking the server would not be a refusal at all.

use std::{
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
    ErrorCode, FrameHeader, FrameKind, Mode, Startup, StreamId, encode_frame, frame,
    protocol::{self, kinds},
};
use lasso::Rodeo;

/// The cap the server under test admits under — small, so it is reachable without a
/// flood, and greater than one, so "the ones under it keep working" has something to
/// say.
const CAP: usize = 2;

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

struct Serving {
    _dir: tempfile::TempDir,
    socket: std::path::PathBuf,
    fingerprint: u64,
    registry: Arc<Registry>,
}

fn start(max_connections: usize) -> Serving {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fjord.sock");

    let schema = schema();
    let fingerprint = fjord_schema::fingerprint::of(&schema);

    let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
    catalog.create("code", &schema).expect("a database");

    let (registry, _listing) = Registry::open(catalog, Schemas::new("")).expect("a registry");
    let registry = Arc::new(registry);

    let listener = Listener::bind(&socket)
        .expect("a socket")
        .with_max_connections(max_connections);

    {
        let registry = Arc::clone(&registry);
        thread::spawn(move || {
            let _ = listener.run_blocking(registry);
        });
    }

    Serving {
        _dir: dir,
        socket,
        fingerprint,
        registry,
    }
}

/// A minimal client: frames in, frames out.
struct Client {
    stream: UnixStream,
}

impl Client {
    fn connect(serving: &Serving) -> Client {
        Client {
            stream: UnixStream::connect(&serving.socket).expect("a connection"),
        }
    }

    fn send(&mut self, kind: FrameKind, stream: StreamId, payload: &[u8]) {
        let mut out = vec![];
        encode_frame(&mut out, kind, stream, payload).expect("a frame");
        self.stream.write_all(&out).expect("a write");
    }

    fn recv(&mut self) -> (FrameHeader, Vec<u8>) {
        let mut head = [0u8; frame::HEADER_LEN];
        self.stream.read_exact(&mut head).expect("a frame header");
        let header = frame::decode_header(&head).expect("a header");

        let mut payload = vec![0u8; header.length as usize];
        self.stream.read_exact(&mut payload).expect("a payload");

        (header, payload)
    }

    fn hello(&mut self, serving: &Serving) -> (FrameHeader, Vec<u8>) {
        let startup = protocol::encode_startup(&Startup {
            version: protocol::VERSION,
            database: "code".to_owned(),
            mode: Mode::ReadOnly,
            schema_fingerprint: serving.fingerprint,
            predicates: vec![],
        });

        self.send(kinds::STARTUP, StreamId(0), &startup);
        self.recv()
    }

    /// Handshake, expecting to be let in.
    fn admitted(&mut self, serving: &Serving) {
        let (header, _) = self.hello(serving);
        assert_eq!(
            header.kind,
            kinds::READY,
            "this connection should be under the cap"
        );
    }

    /// Ask something the server has to reach the store to answer.
    fn queries(&mut self, stream: StreamId) {
        self.send(kinds::QUERY, stream, b"F where src.File F");

        let (header, _) = self.recv();
        assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);

        let (header, payload) = self.recv();
        assert_eq!(header.kind, kinds::COMPLETE);
        let (rows, _) = protocol::decode_complete(&payload).expect("counts");
        assert_eq!(
            rows, 0,
            "the database is empty; the point is that it answered"
        );
    }
}

/// **The criterion.** At the cap, a new connection is told so by code and closed, the
/// connections already admitted go on answering, and the place a closed connection
/// held is given to the next caller.
#[test]
fn past_the_cap_a_connection_is_refused_by_name_and_the_admitted_ones_keep_answering() {
    let serving = start(CAP);

    let mut first = Client::connect(&serving);
    first.admitted(&serving);
    let mut second = Client::connect(&serving);
    second.admitted(&serving);

    // ---- the refusal
    //
    // The kernel takes this into the backlog whatever the server thinks, which is the
    // whole reason a refusal has to be spoken: connecting succeeding says nothing.
    let mut third = Client::connect(&serving);
    let (header, payload) = third.hello(&serving);

    assert_eq!(
        header.kind,
        FrameKind::ERROR,
        "past the cap, no ready frame"
    );
    assert_eq!(header.stream, StreamId(0));

    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(
        code,
        ErrorCode::Busy,
        "a full server is worth retrying, and only its own code says so"
    );
    assert!(
        message.contains("connection limit"),
        "the message is what a person reads: {message}"
    );

    // And then it is closed rather than left half-open holding a descriptor.
    let mut after = [0u8; 1];
    assert_eq!(
        third
            .stream
            .read(&mut after)
            .expect("a read on a closed peer"),
        0,
        "a refused connection is closed, not parked"
    );

    assert_eq!(
        serving.registry.stats().connections_refused(),
        1,
        "the refusal is counted, so `busy` can be told from `broken` after the fact"
    );

    // ---- the connections under the cap are unharmed
    //
    // This is the property the cap exists for: the descriptors it kept back are the
    // ones these are using, and a refusal must not have cost them anything.
    first.queries(StreamId(1));
    second.queries(StreamId(1));

    // ---- a place is returned when a connection ends
    drop(second);
    drop(third);

    let deadline = Instant::now() + Duration::from_secs(5);
    let mut fourth = loop {
        let mut candidate = Client::connect(&serving);
        let (header, _) = candidate.hello(&serving);

        if header.kind == kinds::READY {
            break candidate;
        }

        assert!(
            Instant::now() < deadline,
            "the place a closed connection held was never returned — the cap ratchets"
        );
        // The session's task has to notice the peer is gone before it drops its
        // permit, so this races the runtime rather than the server's logic.
        thread::sleep(Duration::from_millis(20));
    };

    fourth.queries(StreamId(1));
    first.queries(StreamId(2));
}

/// A cap the operator did not set is derived rather than absent — and the derivation
/// is a share of the descriptor limit, so a machine that raises `ulimit -n` gets a
/// server that uses it.
#[test]
fn the_default_cap_is_derived_from_the_descriptor_limit() {
    let listener = {
        let dir = tempfile::tempdir().expect("a scratch directory");
        Listener::bind(dir.path().join("fjord.sock")).expect("a socket")
    };

    let mut limit = libc::rlimit {
        rlim_cur: 0,
        rlim_max: 0,
    };
    // SAFETY: `getrlimit` writes through the pointer and reads nothing else.
    assert_eq!(
        unsafe { libc::getrlimit(libc::RLIMIT_NOFILE, &raw mut limit) },
        0
    );

    let soft = limit.rlim_cur;
    let max = u64::try_from(listener.admission().max()).expect("a cap that fits a u64");

    assert!(
        max >= 16,
        "a derived cap still has to serve somebody: {max}"
    );
    assert_eq!(listener.admission().live(), 0);

    // The *property*, not the formula — `admission`'s own tests pin the arithmetic.
    // What matters here is that a real descriptor limit leaves at least half of itself
    // unspent, because the store's files come out of the same table as the sockets.
    if (64..u64::MAX / 4).contains(&soft) {
        assert!(
            max * 2 <= soft,
            "a cap of {max} against a limit of {soft} reserves nothing"
        );
    }
}
