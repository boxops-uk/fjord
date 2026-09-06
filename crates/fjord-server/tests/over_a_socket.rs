//! **The wire-ingestion criterion, as a test**: facts are writable over a socket and
//! queried back on the same connection.
//!
//! Over a real `UnixListener` and a real `FjallDb`, because the criterion says
//! *socket* — an in-process call proves the frame handling and not the thing that was
//! promised. The client here is deliberately hand-rolled from `fjord-wire` alone,
//! which is the same position the .NET client is in: if this needs something the wire
//! crate does not expose, so does every other client.
//!
//! # One battery, every door
//!
//! Every claim below runs once per transport the server opens — the Unix socket and an
//! opted-in TCP port, which is what [`Over`] enumerates. A transport whose only coverage
//! is *it answers the same as the other one* is covered by a differential, and two doors
//! that mangled a frame the same way would satisfy that and both be wrong. A listener
//! added later joins by growing [`Over`] and [`Client::connect`]; a second copy of the
//! battery would be two statements of one protocol.
//!
//! Names below still say *socket* where the criterion they quote does.

use std::{
    io::{Read, Write},
    os::unix::net::UnixStream,
    sync::Arc,
    thread,
};

use fjord_schema::schema::{Predicate, PredicateId, PredicateTy, Schema};
use fjord_server::{Registry, registry::Schemas};
use fjord_store_fjall::catalog::Catalog;
use fjord_wire::{
    Desc, ErrorCode, FrameHeader, FrameKind, Mode, Startup, StreamId, WireFact, WireRef, WireValue,
    decode_desc, encode_block, encode_frame, frame,
    protocol::{self, kinds},
    value::decode_value,
};
use lasso::Rodeo;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);
/// A predicate **with a value side**, so two facts can share a key and disagree —
/// which is the only way to provoke a conflict, and so the only way to check that a
/// conflict reaches a client under its own code.
const BLOB: PredicateId = PredicateId(2);

fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let (file, decl, blob) = (
        rodeo.get_or_intern("src.File"),
        rodeo.get_or_intern("src.Decl"),
        rodeo.get_or_intern("src.Blob"),
    );
    let (f_file, f_line, f_name) = (
        rodeo.get_or_intern("file"),
        rodeo.get_or_intern("line"),
        rodeo.get_or_intern("name"),
    );

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![
            Predicate {
                name: file,
                key: PredicateTy::Str,
                value: None,
            },
            Predicate {
                name: decl,
                key: PredicateTy::Record(
                    vec![
                        (f_file, PredicateTy::Fact(FILE)),
                        (f_line, PredicateTy::Int),
                        (f_name, PredicateTy::Str),
                    ]
                    .into(),
                ),
                value: None,
            },
            Predicate {
                name: blob,
                key: PredicateTy::Str,
                value: Some(PredicateTy::Int),
            },
        ]),
    )
}

fn blob(path: &str, contents: i64) -> WireFact {
    WireFact {
        predicate: BLOB,
        key: WireValue::Str(path.to_owned()),
        value: Some(WireValue::Int(contents)),
    }
}

/// A door onto a server, and the whole of what a claim needs to know about one.
///
/// Growing this and [`Client::connect`] is how a new listener joins the battery.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum Over {
    UnixSocket,
    Tcp,
}

/// Every door the server below opens.
const EVERY: [Over; 2] = [Over::UnixSocket, Over::Tcp];

/// Run one claim over every door, and report every door it failed over.
///
/// The door is **printed rather than asserted**: libtest shows a failing test's output,
/// and without this an assertion firing inside a claim would not say which transport it
/// fired on — the first thing anybody wants to know.
///
/// **A door whose claim fails does not stop the next one.** Returning at the first
/// panic would report one door and say nothing about the other, and a reader could not
/// tell a transport-specific defect from one both doors share — which is the single
/// question this battery exists to answer. Each panic is caught so the run continues,
/// the hook prints it where it happened, and the doors that failed are named together at
/// the end. A claim is a `fn` pointer that starts its own server, so no state crosses
/// from one door to the next for the unwind to have damaged, and [`PORT`] is taken
/// through `PoisonError::into_inner` — so a claim that panicked while holding it does
/// not take the next door down with it.
fn over_every_door(claim: fn(Over)) {
    let mut failed = Vec::new();

    for over in EVERY {
        println!("--- over {over:?}");

        if std::panic::catch_unwind(|| claim(over)).is_err() {
            failed.push(over);
        }
    }

    assert!(
        failed.is_empty(),
        "the claim failed over {failed:?}, of {} door(s) tried",
        EVERY.len()
    );
}

/// **A claim that fails over one door is still tried over the other.**
///
/// The battery's own guard, and it is about the report rather than about a protocol: a
/// helper that stopped at the first failing door would leave a reader unable to say
/// whether the second door broke too, and a claim's whole purpose here is to be answered
/// once per transport. Driven with a claim planted to fail over the first door, so what
/// is asserted is which doors ran and which the report names.
#[test]
fn a_claim_that_fails_over_one_door_is_still_tried_over_the_other() {
    static TRIED: std::sync::Mutex<Vec<Over>> = std::sync::Mutex::new(Vec::new());

    fn refuses_the_first_door(over: Over) {
        TRIED
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .push(over);

        assert!(
            !matches!(over, Over::UnixSocket),
            "planted: this claim refuses {over:?}"
        );
    }

    let report = std::panic::catch_unwind(|| over_every_door(refuses_the_first_door))
        .expect_err("a claim that fails over a door has to fail its test");

    assert_eq!(
        *TRIED
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner),
        EVERY.to_vec(),
        "a door the battery never tried is a door its report cannot speak for"
    );

    let message = report
        .downcast_ref::<String>()
        .map(String::as_str)
        .expect("the battery's own failure is a formatted message");

    // Both halves: the door that failed is named, and the door that passed is not
    // implicated with it.
    assert!(message.contains("UnixSocket"), "{message}");
    assert!(!message.contains("Tcp"), "{message}");
}

/// **A claim that fails over every door names every door.**
///
/// The other half of the report, and the reason the doors are collected rather than
/// counted: one door failing and both failing are different defects — a transport's own
/// and the protocol's — and a report that cannot tell them apart sends a reader to the
/// wrong half of the server.
#[test]
fn a_claim_that_fails_over_every_door_names_every_door() {
    static TRIED: std::sync::Mutex<Vec<Over>> = std::sync::Mutex::new(Vec::new());

    fn refuses_every_door(over: Over) {
        TRIED
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .push(over);

        panic!("planted: this claim refuses {over:?}");
    }

    let report = std::panic::catch_unwind(|| over_every_door(refuses_every_door))
        .expect_err("a claim that fails over every door has to fail its test");

    assert_eq!(
        *TRIED
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner),
        EVERY.to_vec(),
        "every door is tried whatever the one before it did"
    );

    let message = report
        .downcast_ref::<String>()
        .map(String::as_str)
        .expect("the battery's own failure is a formatted message");

    for over in EVERY {
        assert!(message.contains(&format!("{over:?}")), "{message}");
    }
}

/// Serialises the window between probing for a free port and the server binding it.
///
/// `serve_on` takes an address rather than a bound listener, so a free port can only be
/// learned by taking one and letting it go — and two claims doing that at once in this
/// binary would be handed the same port, leaving one of them with a door that never
/// opens. Held across the bind, not just the probe.
static PORT: std::sync::Mutex<()> = std::sync::Mutex::new(());

/// A running server, and how to reach it through either door.
struct Serving {
    _dir: tempfile::TempDir,
    socket: std::path::PathBuf,
    address: String,
    fingerprint: u64,
}

fn start() -> Serving {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fjord.sock");

    let schema = schema();
    let fingerprint = fjord_schema::fingerprint::of(&schema);

    // Through the catalog rather than by opening a directory: the server owns a store
    // root and serves what is under it, so a test that handed it a bare `FjallDb`
    // would be testing a shape the server no longer has.
    let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
    catalog.create("code", &schema).expect("a database");

    let (registry, _listing) = Registry::open(catalog, Schemas::new("")).expect("a registry");

    let claim = PORT
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    let address = {
        let probe = std::net::TcpListener::bind("127.0.0.1:0").expect("a free port");
        probe.local_addr().expect("its address").to_string()
    };

    // `serve_on` rather than `Listener::run_blocking`, because the TCP door is
    // `ops-I10`'s opt-in and that function is where the opt-in lives — a test that bound
    // its own port would be exercising a shape no operator can ask for. It blocks, so it
    // gets a thread of its own; the client below is deliberately runtime-free, which is
    // the position every non-Rust client is in.
    {
        let socket = socket.clone();
        let address = address.clone();
        thread::spawn(move || {
            let _ = fjord_server::server::serve_on(
                &socket,
                Some(&address),
                None,
                None,
                Arc::new(registry),
            );
        });
    }

    // **Waiting on the port waits for both doors.** `serve_on` binds the socket before
    // it binds the port, so a TCP connection that succeeds proves the socket is already
    // accepting — the reverse is not true, and the readiness file lands between the two.
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(30);
    while std::net::TcpStream::connect(&address).is_err() {
        assert!(
            std::time::Instant::now() < deadline,
            "the server never opened its doors"
        );
        thread::sleep(std::time::Duration::from_millis(10));
    }
    drop(claim);

    Serving {
        _dir: dir,
        socket,
        address,
        fingerprint,
    }
}

/// The whole of what this client needs of a transport: blocking, byte-oriented I/O.
trait Duplex: Read + Write {}
impl<T: Read + Write> Duplex for T {}

/// A minimal client: frames in, frames out.
struct Client {
    /// Boxed rather than a type parameter, so a claim's signature names a door at
    /// runtime instead of at compile time — which is what lets one function body be the
    /// claim for every transport.
    stream: Box<dyn Duplex>,
}

impl Client {
    fn connect(serving: &Serving, over: Over) -> Client {
        Client {
            stream: match over {
                Over::UnixSocket => Box::new(
                    UnixStream::connect(&serving.socket).expect("a connection over the socket"),
                ),
                Over::Tcp => {
                    let stream = std::net::TcpStream::connect(&serving.address)
                        .expect("a connection over TCP");

                    // What every client does, and for the reason `fjord_client` states:
                    // frames are small and answered one at a time, so Nagle holds a
                    // handshake back waiting for company that is not coming.
                    stream.set_nodelay(true).expect("nodelay");
                    Box::new(stream)
                }
            },
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

    fn hello(&mut self, fingerprint: u64, mode: Mode) -> (FrameHeader, Vec<u8>) {
        let startup = protocol::encode_startup(&Startup {
            version: protocol::VERSION,
            database: "code".to_owned(),
            mode,
            schema_fingerprint: fingerprint,
            predicates: vec![],
        });

        self.send(kinds::STARTUP, StreamId(0), &startup);
        self.recv()
    }
}

fn file(path: &str) -> WireFact {
    WireFact {
        predicate: FILE,
        key: WireValue::Str(path.to_owned()),
        value: None,
    }
}

fn decl(path: &str, line: i64, name: &str) -> WireFact {
    WireFact {
        predicate: DECL,
        key: WireValue::Record(
            vec![
                // Nested: the client holds no ids at all, which is the point.
                WireValue::Ref(WireRef::Nested(Box::new(file(path)))),
                WireValue::Int(line),
                WireValue::Str(name.to_owned()),
            ]
            .into(),
        ),
        value: None,
    }
}

/// **The criterion.** Handshake, write a block of facts holding no ids, query them
/// back — one connection, start to finish.
#[test]
fn facts_are_writable_over_a_socket_and_queried_back_on_the_same_connection() {
    over_every_door(writable_and_queried_back);
}

fn writable_and_queried_back(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);

    // ---- handshake
    let (header, payload) = client.hello(serving.fingerprint, Mode::ReadWrite);
    assert_eq!(header.kind, kinds::READY);
    let ready = protocol::decode_ready(&payload).expect("a ready frame");
    assert_eq!(ready.version, protocol::VERSION);
    assert_eq!(ready.schema_fingerprint, serving.fingerprint);
    assert_eq!(ready.predicates, 3);

    // ---- write stream
    let write = StreamId(1);
    client.send(kinds::OPEN_WRITE, write, &[]);
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::COPY_IN_RESPONSE);
    assert_eq!(header.stream, write);

    let facts = vec![
        decl("store/keys.py", 12, "key_of"),
        decl("store/keys.py", 48, "key_prefix"),
        decl("store/codec.py", 7, "encode_key"),
    ];

    let mut block = vec![];
    encode_block(&mut block, &schema(), DECL, &facts).expect("a block");
    client.send(FrameKind::COPY_DATA, write, &block);
    client.send(FrameKind::COPY_DONE, write, &[]);

    let (header, payload) = client.recv();
    assert_eq!(header.kind, kinds::COMPLETE);
    let (created, deduped) = protocol::decode_complete(&payload).expect("counts");

    // Three declarations and two files: `store/keys.py` is named twice and written
    // once. Interning, over a socket.
    assert_eq!((created, deduped), (5, 1));

    // ---- query stream, on the *same* connection
    let read = StreamId(2);
    client.send(kinds::QUERY, read, b"F where src.File F");

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
    assert_eq!(header.stream, read);
    let (desc, _) = decode_desc(&payload).expect("a descriptor");
    assert_eq!(desc, Desc::Str, "the head is a bare string");

    let mut paths = vec![];
    loop {
        let (header, payload) = client.recv();

        if header.kind == kinds::COMPLETE {
            let (rows, _) = protocol::decode_complete(&payload).expect("counts");
            assert_eq!(rows as usize, paths.len());
            break;
        }

        assert_eq!(header.kind, FrameKind::DATA_ROW);
        let (value, _) =
            decode_value(&payload, &schema(), &PredicateTy::Str).expect("a row decodes");

        match value {
            WireValue::Str(path) => paths.push(path),
            other => panic!("expected a string row, got {other:?}"),
        }
    }

    paths.sort();
    assert_eq!(paths, vec!["store/codec.py", "store/keys.py"]);
}

/// A record head comes back as a record descriptor, and the rows follow it
/// positionally — the case that makes a descriptor necessary at all, since no
/// predicate declares this shape.
#[test]
fn a_record_head_describes_itself_and_its_rows_follow() {
    over_every_door(a_record_head_describes_itself);
}

fn a_record_head_describes_itself(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    let write = StreamId(1);
    client.send(kinds::OPEN_WRITE, write, &[]);
    client.recv();

    let mut block = vec![];
    encode_block(&mut block, &schema(), DECL, &[decl("a.py", 3, "f")]).expect("a block");
    client.send(FrameKind::COPY_DATA, write, &block);
    client.send(FrameKind::COPY_DONE, write, &[]);
    client.recv();

    client.send(
        kinds::QUERY,
        StreamId(2),
        b"{at = D.line, what = D.name} where D = src.Decl _",
    );

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
    let (desc, _) = decode_desc(&payload).expect("a descriptor");

    let Desc::Record(fields) = &desc else {
        panic!("expected a record descriptor, got {desc:?}");
    };
    assert_eq!(fields.len(), 2);
    assert_eq!(fields[0].0, "at");
    assert_eq!(fields[1].0, "what");

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::DATA_ROW);

    // A client decodes rows with the same value decoder it encodes facts with — the
    // descriptor converts to a type and everything downstream is the ordinary codec.
    let mut interner = fjord_schema::schema::LocalInterner::new(schema().interner().clone());
    let ty = desc.to_ty(&mut interner);
    let (row, _) = decode_value(&payload, &schema(), &ty).expect("a row decodes");

    assert_eq!(
        row,
        WireValue::Record(vec![WireValue::Int(3), WireValue::Str("f".to_owned())].into())
    );
}

/// **A record head whose fields the schema does not declare, holding a reference.**
///
/// This is the case the Rust tests missed and the .NET demo caught: `{decl = …, file
/// = …}` names fields no predicate has, so there is no schema symbol for them, and one
/// of them is a *reference* rather than a scalar. Matching a row's fields to its type
/// by name cannot work here — a `PredicateTy::Record` holds a bare `Spur` and cannot
/// say which tier of the interner it came from — and the wrong guess resolves to a
/// different string rather than failing.
#[test]
fn a_record_head_of_undeclared_names_holding_a_reference() {
    over_every_door(undeclared_names_and_a_reference);
}

fn undeclared_names_and_a_reference(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    let write = StreamId(1);
    client.send(kinds::OPEN_WRITE, write, &[]);
    client.recv();

    let mut block = vec![];
    encode_block(&mut block, &schema(), DECL, &[decl("a.py", 3, "f")]).expect("a block");
    client.send(FrameKind::COPY_DATA, write, &block);
    client.send(FrameKind::COPY_DONE, write, &[]);
    client.recv();

    // `what` and `where_` are not schema names; `where_.file` is a reference.
    client.send(
        kinds::QUERY,
        StreamId(2),
        b"{what = D.name, whose = D.file} where D = src.Decl _",
    );

    let (header, payload) = client.recv();
    assert_eq!(
        header.kind,
        FrameKind::ROW_DESCRIPTION,
        "{:?}",
        protocol::decode_error(&payload)
    );

    let (desc, _) = decode_desc(&payload).expect("a descriptor");
    let Desc::Record(fields) = &desc else {
        panic!("expected a record descriptor, got {desc:?}");
    };
    assert_eq!(fields[0].0, "what");
    assert_eq!(fields[1].0, "whose");
    assert_eq!(fields[1].1, Desc::Fact(FILE), "a reference field");

    let (header, payload) = client.recv();
    assert_eq!(
        header.kind,
        FrameKind::DATA_ROW,
        "{:?}",
        protocol::decode_error(&payload)
    );

    let mut interner = fjord_schema::schema::LocalInterner::new(schema().interner().clone());
    let ty = desc.to_ty(&mut interner);
    let (row, _) = decode_value(&payload, &schema(), &ty).expect("a row decodes");

    let WireValue::Record(values) = &row else {
        panic!("expected a record row, got {row:?}");
    };
    assert_eq!(values[0], WireValue::Str("f".to_owned()));
    assert!(
        matches!(values[1], WireValue::Ref(WireRef::Id(_))),
        "the reference field came back as an id: {:?}",
        values[1]
    );
}

/// **A wrong schema fingerprint is refused before any data flows** — the cheap early
/// mismatch detection §6 is after, and the reason the handshake carries one.
///
/// This is also **the flag day's own failure**, and the reason it is a flag day. A
/// client carrying one whole-schema constant and claiming no predicates — which is what
/// the .NET client sends, deliberately — takes this branch the moment
/// a shipped schema moves, and stays refused until it is rebuilt. So the message has
/// to carry **both numbers**: an operator seeing only "schema mismatch" cannot tell a
/// stale client from a client pointed at the wrong database, and those have different
/// fixes.
#[test]
fn a_schema_mismatch_is_refused_at_the_handshake() {
    over_every_door(a_schema_mismatch_is_refused);
}

fn a_schema_mismatch_is_refused(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);

    let stale = serving.fingerprint ^ 0xFF;
    let (header, payload) = client.hello(stale, Mode::ReadWrite);

    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::SchemaMismatch);
    assert!(message.contains("schema mismatch"), "{message}");

    assert!(
        message.contains(&format!("{stale:x}")),
        "the refusal must name what the client claimed, or a stale client and a client \
         on the wrong database read the same: {message}"
    );
    assert!(
        message.contains(&format!("{:x}", serving.fingerprint)),
        "the refusal must name what the database has: {message}"
    );
}

/// Zero means "do not check", which is what a reader or a client written against
/// whatever the server has will send.
#[test]
fn a_zero_fingerprint_skips_the_check() {
    over_every_door(a_zero_fingerprint_skips);
}

fn a_zero_fingerprint_skips(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);

    let (header, _) = client.hello(0, Mode::ReadOnly);
    assert_eq!(header.kind, kinds::READY);
}

/// A read-only session cannot open a write stream, and the refusal is `ops-I6`'s:
/// the mode is declared once at startup and resolved there, not argued about per
/// frame.
#[test]
fn a_read_only_session_cannot_write() {
    over_every_door(a_read_only_session_refuses_a_write);
}

fn a_read_only_session_refuses_a_write(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadOnly);

    client.send(kinds::OPEN_WRITE, StreamId(1), &[]);
    let (header, payload) = client.recv();

    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, _) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::ModeRefused);
}

/// **A stream-level fault fails its stream and the connection survives** — which is
/// what makes multiplexing worth having at all, and is checked by using the
/// connection afterwards.
#[test]
fn a_failed_stream_leaves_the_connection_usable() {
    over_every_door(a_failed_stream_leaves_the_connection);
}

fn a_failed_stream_leaves_the_connection(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    // A query that does not compile.
    client.send(kinds::QUERY, StreamId(1), b"this is not sigla");
    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);
    assert_eq!(header.stream, StreamId(1), "the error names its own stream");
    let (code, _) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::BadQuery);

    // The connection still works, on a different stream.
    client.send(kinds::QUERY, StreamId(2), b"F where src.File F");
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
    assert_eq!(header.stream, StreamId(2));
}

/// Fact blocks on a stream that was never opened for writing are a protocol fault
/// rather than an implicit open — see [`session`](fjord_server::session) for why
/// an implicit open would be worse than an error.
#[test]
fn copy_data_on_an_unopened_stream_is_refused() {
    over_every_door(copy_data_unopened_is_refused);
}

fn copy_data_unopened_is_refused(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    let mut block = vec![];
    encode_block(&mut block, &schema(), FILE, &[file("a.py")]).expect("a block");
    client.send(FrameKind::COPY_DATA, StreamId(9), &block);

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::Protocol);
    assert!(message.contains("never opened"), "{message}");
}

/// **A frame kind this server does not know is `Protocol`, on the stream that sent it.**
///
/// The framing layer hands an unrecognised kind up intact rather than failing the decode,
/// so that a peer speaking a newer protocol can be *told* rather than left to read
/// "malformed". This is the other half of that promise, and it is load-bearing at a
/// distance: [`Connection::fetch`](fjord_client::Connection::fetch) reads exactly this
/// code to turn a server that predates the `F` frame into a sentence naming the remedy —
/// "restart it with a current build" — instead of showing somebody
/// `no handler for frame kind`. A future change that answered an unknown kind with some
/// other code would leave that translation silently unreachable, and the person back where
/// they started.
///
/// `Z` is deliberately not a kind anything assigns, and this is the test that would fail
/// if it became one.
#[test]
fn an_unknown_frame_kind_is_refused_by_code_and_the_connection_lives() {
    over_every_door(an_unknown_frame_kind_is_refused);
}

fn an_unknown_frame_kind_is_refused(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadOnly);

    client.send(FrameKind(b'Z'), StreamId(1), b"");

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);

    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(
        code,
        ErrorCode::Protocol,
        "an unhandled kind is a protocol fault: {message}"
    );
    assert!(message.contains('Z'), "and it names the kind: {message}");

    // The connection survives it, which is what makes "I do not know that message" a
    // conversation rather than a disconnection.
    client.send(kinds::QUERY, StreamId(2), b"F where src.File F");
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
}

/// **A block whose bytes do not decode fails its stream, and the connection lives.**
///
/// Worth telling apart from a frame-level fault, and the codes do: the *frame* was
/// well-formed — its length delimited the payload correctly — so nothing about the
/// connection is in doubt. Only the payload was rubbish, which is `BadFacts` and
/// survivable, where a frame that would not parse is `Protocol` and is not.
#[test]
fn a_malformed_block_fails_its_stream_and_the_connection_lives() {
    over_every_door(a_malformed_block_fails_its_stream);
}

fn a_malformed_block_fails_its_stream(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    let write = StreamId(1);
    client.send(kinds::OPEN_WRITE, write, &[]);
    client.recv();

    client.send(FrameKind::COPY_DATA, write, b"not a block");
    let (header, payload) = client.recv();

    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, _) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::BadFacts);

    // Still usable afterwards.
    client.send(kinds::QUERY, StreamId(2), b"F where src.File F");
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
}

/// **A conflict reaches the client under its own code**, so a producer can tell "you
/// contradicted yourself" from "your bytes were malformed" without reading English.
#[test]
fn a_conflict_has_its_own_code() {
    over_every_door(a_conflict_under_its_own_code);
}

fn a_conflict_under_its_own_code(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    let mut send_blob = |stream: StreamId, contents: i64| {
        client.send(kinds::OPEN_WRITE, stream, &[]);
        client.recv();

        let mut block = vec![];
        encode_block(&mut block, &schema(), BLOB, &[blob("same.py", contents)]).expect("a block");
        client.send(FrameKind::COPY_DATA, stream, &block);
        client.send(FrameKind::COPY_DONE, stream, &[]);
        client.recv()
    };

    let (header, _) = send_blob(StreamId(1), 1);
    assert_eq!(header.kind, kinds::COMPLETE, "the first blob lands");

    // The same key, a different value side: ops-I5's same-key-different-value, over a
    // socket.
    let (header, payload) = send_blob(StreamId(2), 2);
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, _) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::Conflict);
}

// ---- what the runtime was for -----------------------------------------------

/// Write `count` files, so a query over them takes long enough to interleave with.
fn seed_files(client: &mut Client, count: usize) {
    let facts: Vec<WireFact> = (0..count)
        .map(|index| file(&format!("src/file{index:06}.py")))
        .collect();

    let write = StreamId(90);
    client.send(kinds::OPEN_WRITE, write, &[]);
    client.recv();

    // Blocks rather than one giant one, since a block is capped and this is also the
    // shape a real producer sends.
    for chunk in facts.chunks(500) {
        let mut block = vec![];
        encode_block(&mut block, &schema(), FILE, chunk).expect("a block");
        client.send(FrameKind::COPY_DATA, write, &block);
    }

    client.send(FrameKind::COPY_DONE, write, &[]);
    let (header, _) = client.recv();
    assert_eq!(header.kind, kinds::COMPLETE);
}

/// **A long query does not delay a short one on the same connection.**
///
/// This was false before the runtime landed — the server awaited each frame's work
/// before reading the next, so a query scanning thousands of rows held the connection
/// for its whole duration. It is the property §5 asks for and the reason the reader
/// loop never does a stream's work.
///
/// Checked by *which stream completes first*, not by a clock: both queries are issued
/// back to back with the long one first, and the short one's `COMPLETE` has to arrive
/// while the long one is still sending rows.
#[test]
fn a_long_query_does_not_delay_a_short_one() {
    over_every_door(the_streams_interleave);
}

fn the_streams_interleave(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    seed_files(&mut client, 4_000);

    let long = StreamId(1);
    let short = StreamId(2);

    // The long one first, so that a server processing frames in order would finish it
    // before even reading the short one.
    client.send(kinds::QUERY, long, b"F where src.File F");
    client.send(
        kinds::QUERY,
        short,
        b"F where F = src.File \"src/file000001.py\"",
    );

    let mut long_rows = 0;
    let mut short_completed_after = None;

    loop {
        let (header, payload) = client.recv();

        if header.kind == FrameKind::ERROR {
            let (code, message) = protocol::decode_error(&payload).expect("an error frame");
            panic!("stream {} failed: {code:?}: {message}", header.stream.0);
        }

        if header.kind == FrameKind::DATA_ROW && header.stream == long {
            long_rows += 1;
        }

        if header.kind == kinds::COMPLETE {
            if header.stream == short {
                short_completed_after = Some(long_rows);
            } else {
                let (rows, _) = protocol::decode_complete(&payload).expect("counts");
                assert_eq!(rows, 4_000, "the long query answered in full");
                break;
            }
        }
    }

    // Measured at 0 — the short query completes before the long one emits a single
    // row. The bound is loose against a loaded machine and still an order of
    // magnitude inside "they did not interleave at all".
    let interleaved = short_completed_after.expect("the short query completed");
    assert!(
        interleaved < 1_000,
        "the short query waited for {interleaved} of the long query's 4000 rows — \
         the streams did not interleave"
    );
}

/// **Resume across chunks equals an uninterrupted run.**
///
/// A thousand rows is four chunks at [`CHUNK_ROWS`](fjord_server::session), so the
/// executor is entered once and *resumed* three times — through the same bytes-only
/// cursor [chapter 5](../../../website/content/executor.md) is about. Until now that machinery
/// was exercised only by its own batteries; this is the first thing that uses it for
/// what it is for.
///
/// Checked as a **set**, not a count: a resume that dropped a row, repeated one, or
/// restarted a level would still produce a thousand frames.
#[test]
fn a_chunked_result_is_the_same_rows_an_uninterrupted_one_would_give() {
    over_every_door(a_chunked_result_is_every_row);
}

fn a_chunked_result_is_every_row(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    seed_files(&mut client, 1_000);

    client.send(kinds::QUERY, StreamId(1), b"F where src.File F");

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
    let (desc, _) = decode_desc(&payload).expect("a descriptor");
    assert_eq!(desc, Desc::Str);

    let mut paths = std::collections::BTreeSet::new();
    let mut frames = 0;

    loop {
        let (header, payload) = client.recv();

        match header.kind {
            FrameKind::DATA_ROW => {
                frames += 1;
                let (value, _) =
                    decode_value(&payload, &schema(), &PredicateTy::Str).expect("a row decodes");
                match value {
                    WireValue::Str(path) => {
                        paths.insert(path);
                    }
                    other => panic!("expected a string row, got {other:?}"),
                }
            }
            kinds::COMPLETE => {
                let (count, _) = protocol::decode_complete(&payload).expect("counts");
                assert_eq!(count, 1_000);
                break;
            }
            other => panic!("unexpected frame `{other}`"),
        }
    }

    assert_eq!(frames, 1_000, "one frame per row");
    assert_eq!(
        paths.len(),
        1_000,
        "a thousand *distinct* paths — a resume that repeated or dropped a row would \
         still have sent a thousand frames"
    );
    assert!(paths.contains("src/file000000.py"));
    assert!(paths.contains("src/file000999.py"));
}

/// **Cancel stops a stream, not the connection.**
///
/// In band and on the stream it cancels, which is the whole reason frames carry a
/// stream id: a second connection could not do this, because the first one's state is
/// not there. The stream completes with what it had sent — a cancel is an early end
/// rather than a failure, and a client that asked for one is not owed an error.
#[test]
fn cancelling_a_stream_ends_it_and_leaves_the_connection() {
    over_every_door(a_cancel_ends_one_stream);
}

fn a_cancel_ends_one_stream(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadWrite);

    seed_files(&mut client, 8_000);

    client.send(kinds::QUERY, StreamId(1), b"F where src.File F");

    // Read the descriptor and a few rows, then ask it to stop.
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);

    for _ in 0..10 {
        let (header, _) = client.recv();
        assert_eq!(header.kind, FrameKind::DATA_ROW);
    }

    client.send(kinds::CANCEL, StreamId(1), &[]);

    let mut rows = 10;
    let cancelled_at = loop {
        let (header, payload) = client.recv();

        match header.kind {
            FrameKind::DATA_ROW => rows += 1,
            kinds::COMPLETE => {
                let (count, _) = protocol::decode_complete(&payload).expect("counts");
                break count;
            }
            other => panic!("unexpected frame `{other}`"),
        }
    };

    assert_eq!(cancelled_at, rows, "the count is what was actually sent");
    assert!(
        cancelled_at < 8_000,
        "the query ran to completion despite being cancelled"
    );

    // And the connection is unharmed: a different stream answers normally.
    client.send(
        kinds::QUERY,
        StreamId(2),
        b"F where F = src.File \"src/file000002.py\"",
    );
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
    assert_eq!(header.stream, StreamId(2));
}

/// A frame that needs a database, on a session bound to none, is refused as
/// [`ErrorCode::UnknownDatabase`] with a message saying what to do — the one kind of
/// session `create` is sent on is also one a query must not silently default on.
#[test]
fn a_query_on_a_session_naming_no_database_is_refused() {
    over_every_door(a_query_with_no_database_is_refused);
}

fn a_query_with_no_database_is_refused(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);

    let startup = protocol::encode_startup(&Startup {
        version: protocol::VERSION,
        database: String::new(),
        mode: Mode::ReadOnly,
        schema_fingerprint: 0,
        predicates: vec![],
    });
    client.send(kinds::STARTUP, StreamId(0), &startup);
    let (header, _) = client.recv();
    assert_eq!(header.kind, kinds::READY, "a control session is legitimate");

    client.send(kinds::QUERY, StreamId(1), b"F where src.File F");
    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::UnknownDatabase);
    assert!(
        message.contains("names no database"),
        "the message says what to do: {message}"
    );
}

/// A per-predicate claim the database does not hold draws the schema-mismatch code
/// **with the predicate named in the message** — two whole-schema numbers that differ
/// say nothing about which predicate they differ over, and a producer writing a
/// subset needs exactly that.
#[test]
fn a_predicate_claim_the_database_does_not_hold_is_named() {
    over_every_door(an_unheld_predicate_claim_is_named);
}

fn an_unheld_predicate_claim_is_named(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);

    let startup = protocol::encode_startup(&Startup {
        version: protocol::VERSION,
        database: "code".to_owned(),
        mode: Mode::ReadOnly,
        // A nonzero whole-schema number is what makes the per-predicate claims
        // the fallback that gets checked; zero-and-empty means "no opinion".
        schema_fingerprint: 1,
        predicates: vec![("src.File".to_owned(), 0xDEAD_BEEF)],
    });
    client.send(kinds::STARTUP, StreamId(0), &startup);

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::SchemaMismatch);
    assert!(
        message.contains("src.File"),
        "the broken predicate is named: {message}"
    );
}

/// A fault the *server* cannot attribute to the request — here, a resume token whose
/// bytes are garbage — is [`ErrorCode::Internal`], and the stream fails without
/// taking the connection.
#[test]
fn a_fault_of_the_servers_own_is_internal_and_survivable() {
    over_every_door(an_internal_fault_is_survivable);
}

fn an_internal_fault_is_survivable(over: Over) {
    let serving = start();
    let mut client = Client::connect(&serving, over);
    client.hello(0, Mode::ReadOnly);

    let page = protocol::encode_page(&protocol::Page {
        limit: 1,
        cursor: vec![0xFF, 0x00, 0xFF],
        query: "F where src.File F".to_owned(),
    });
    client.send(kinds::QUERY_PAGE, StreamId(1), &page);

    let (header, payload) = client.recv();
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, _) = protocol::decode_error(&payload).expect("an error frame");
    assert_eq!(code, ErrorCode::Internal);

    // The connection is still usable afterwards.
    client.send(kinds::QUERY, StreamId(2), b"F where src.File F");
    let (header, _) = client.recv();
    assert_eq!(header.kind, FrameKind::ROW_DESCRIPTION);
}
