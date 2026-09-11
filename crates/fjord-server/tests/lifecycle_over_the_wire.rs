//! **9d's last piece, as a test**: a database is created, written, sealed and removed
//! *against a running server*, rather than by stopping it first.
//!
//! That was the whole of what was left. `ops-I1` gives one process the store root, so
//! before this the honest interim was for the CLI to refuse every lifecycle command
//! while a server held it — which made "usable" and "serving" mutually exclusive.
//!
//! `list` and `describe` are not here, and their absence is the design rather than a
//! gap: `ops-I7` reads sidecars and never opens fjall, so both already worked while a
//! server held every database. Only the three that *mutate* needed a way in.
//!
//! The client is hand-rolled from `fjord-wire` and `protocol` alone, as everywhere
//! else on this seam: if a lifecycle client needs something those two do not expose,
//! so does every non-Rust one.

use std::{
    io::{Read, Write},
    os::unix::net::UnixStream,
    path::PathBuf,
    sync::Arc,
    thread,
    time::{Duration, Instant},
};

use fjord_schema::schema::{Predicate, PredicateId, PredicateTy, Schema};
use fjord_server::{Registry, registry::Schemas, server::Listener};
use fjord_store_fjall::{
    catalog::{Catalog, Intent, Selector},
    store::FjallDb,
};
use fjord_wire::{
    Control, ControlOp, ControlReply, ErrorCode, FrameHeader, FrameKind, Mode, Startup, StreamId,
    WireFact, WireValue, encode_block, encode_frame, frame,
    protocol::{self, kinds},
};
use lasso::Rodeo;

const FILE: PredicateId = PredicateId(0);

/// One predicate, so a fact count is a fact count: `src.File : string`.
///
/// Nesting is exercised to death in `over_a_socket.rs`; what is being counted here is
/// *when* a write is allowed, and a key that interned two facts would make every
/// assertion below a subtraction.
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

fn file(path: &str) -> WireFact {
    WireFact {
        predicate: FILE,
        key: WireValue::Str(path.to_owned()),
        value: None,
    }
}

fn block(paths: &[&str]) -> Vec<u8> {
    let facts: Vec<WireFact> = paths.iter().map(|path| file(path)).collect();
    let mut out = vec![];
    encode_block(&mut out, &schema(), FILE, &facts).expect("a block");
    out
}

/// A running server over an **empty** store root, since the point is what it can be
/// told to put in one.
struct Serving {
    _dir: tempfile::TempDir,
    socket: PathBuf,
    root: PathBuf,
    fingerprint: u64,
}

impl Serving {
    /// A catalog over the same root, for checking what the server did to the disk.
    ///
    /// Takes no lock, and that is `ops-I7`: reading the catalog while a server owns
    /// every database under it is the one thing that must always work.
    fn catalog(&self) -> Catalog {
        Catalog::open(&self.root).expect("a store root")
    }
}

fn start() -> Serving {
    start_with(Schemas::new(""))
}

/// The same, with the catalogue the shipped server serves.
///
/// [`start`] passes `""` — a server answering no virtual predicate at all, which is
/// what most of this file wants because it is testing the lifecycle rather than the
/// catalogue. A battery about what a session bound to *no database* can ask needs the
/// real thing, because that schema is the whole of what such a session sees.
fn start_with(schemas: Schemas) -> Serving {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fjord.sock");
    let root = dir.path().join("store");

    let schema = schema();
    let fingerprint = fjord_schema::fingerprint::of(&schema);

    let catalog = Catalog::open(&root).expect("a store root");
    let (registry, _listing) = Registry::open(catalog, schemas).expect("a registry");

    let listener = Listener::bind(&socket).expect("a socket");
    thread::spawn(move || {
        let _ = listener.run_blocking(Arc::new(registry));
    });

    Serving {
        _dir: dir,
        socket,
        root,
        fingerprint,
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

    /// Open a session bound to `database`, or — for the empty string — to none at all.
    ///
    /// **A claim belongs to a database.** A session naming one asserts the fingerprint it
    /// expects, which is the whole point of that field; a session naming *none* has
    /// nothing to assert about, since a `create` is about to name a database that does not
    /// exist yet. So a control session sends `0` — "do not check" — and what it agrees
    /// with the server about is the catalogue, which is all a server holds of its own.
    fn hello(serving: &Serving, database: &str, mode: Mode) -> (Client, FrameHeader, Vec<u8>) {
        let mut client = Client::connect(serving);

        let claim = if database.is_empty() {
            0
        } else {
            serving.fingerprint
        };

        let startup = protocol::encode_startup(&Startup {
            version: protocol::VERSION,
            database: database.to_owned(),
            mode,
            schema_fingerprint: claim,
            predicates: vec![],
        });

        client.send(kinds::STARTUP, StreamId(0), &startup);
        let (header, payload) = client.recv();
        (client, header, payload)
    }

    /// A **control session**: bound to no database, which is the only session a
    /// `create` could be sent on.
    fn control_session(serving: &Serving, mode: Mode) -> Client {
        let (client, header, _) = Client::hello(serving, "", mode);
        assert_eq!(header.kind, kinds::READY, "a control session establishes");
        client
    }

    fn control(&mut self, op: ControlOp, database: &str, allow_zero_facts: bool) -> ControlReply {
        let (header, payload) = self.control_raw(op, database, allow_zero_facts);
        assert_eq!(
            header.kind,
            kinds::CONTROL_REPLY,
            "expected a reply, got {:?}",
            protocol::decode_error(&payload)
        );
        protocol::decode_control_reply(&payload).expect("a control reply")
    }

    fn control_raw(
        &mut self,
        op: ControlOp,
        database: &str,
        allow_zero_facts: bool,
    ) -> (FrameHeader, Vec<u8>) {
        let request = protocol::encode_control(&Control {
            op,
            database: database.to_owned(),
            allow_zero_facts,
            // **`create` requires a schema; every other op ignores the field.** Printed
            // from this file's own `schema()` rather than restated as text, so there is
            // one statement of it — and the trip out through source and back through
            // `lower` is exercised on the way.
            schema: match op {
                ControlOp::Create => fjord_schema::syntax::print::print(&schema()),
                _ => String::new(),
            },
        });

        self.send(kinds::CONTROL, StreamId(1), &request);
        self.recv()
    }

    /// Write one block on a fresh write stream, and report what came back.
    fn write_block(&mut self, stream: StreamId, paths: &[&str]) -> (FrameHeader, Vec<u8>) {
        self.send(kinds::OPEN_WRITE, stream, &[]);
        let (header, payload) = self.recv();

        if header.kind != FrameKind::COPY_IN_RESPONSE {
            return (header, payload);
        }

        self.send(FrameKind::COPY_DATA, stream, &block(paths));
        self.send(FrameKind::COPY_DONE, stream, &[]);
        self.recv()
    }

    /// Run a query and count the rows it answered with.
    fn count(&mut self, stream: StreamId, source: &str) -> u64 {
        self.send(kinds::QUERY, stream, source.as_bytes());

        let (header, payload) = self.recv();
        assert_eq!(
            header.kind,
            FrameKind::ROW_DESCRIPTION,
            "{:?}",
            protocol::decode_error(&payload)
        );

        let mut rows = 0;
        loop {
            let (header, payload) = self.recv();
            match header.kind {
                FrameKind::DATA_ROW => rows += 1,
                kinds::COMPLETE => {
                    let (sent, _) = protocol::decode_complete(&payload).expect("a complete");
                    assert_eq!(sent, rows, "the count and the rows agree");
                    return rows;
                }
                other => panic!("unexpected frame `{other}` during a query"),
            }
        }
    }
}

fn error_of(payload: &[u8]) -> (ErrorCode, String) {
    protocol::decode_error(payload).expect("an error frame")
}

/// Poll until `attempt` succeeds, or give up loudly.
///
/// Needed exactly once, for a condition that is genuinely asynchronous: a session
/// releases its database when its task ends, and a client closing a socket does not
/// get to say when that happens.
fn eventually(what: &str, mut attempt: impl FnMut() -> bool) {
    let deadline = Instant::now() + Duration::from_secs(10);

    while Instant::now() < deadline {
        if attempt() {
            return;
        }
        thread::sleep(Duration::from_millis(20));
    }

    panic!("{what} never happened");
}

/// **The criterion.** A database's whole life, over one socket, against a server that
/// is running throughout: create it, write to it, query it, seal it, and delete it.
#[test]
fn a_database_lives_and_dies_against_a_running_server() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);

    // ---- create
    let ControlReply::Created { instance } = control.control(ControlOp::Create, "code", false)
    else {
        panic!("expected a created reply");
    };
    assert!(!instance.is_empty(), "it was given a provisional instance");

    // On the disk, and visible to a reader that never opens fjall (`ops-I7`) — which
    // is how `list` and `describe` see a database this server just made.
    let entry = serving
        .catalog()
        .resolve(&Selector::of("code"), Intent::Read)
        .expect("it is on the disk");
    assert_eq!(entry.meta.instance, instance);
    assert!(entry.status().is_writable());

    // ---- and immediately usable, without restarting anything
    let (mut writer, header, _) = Client::hello(&serving, "code", Mode::ReadWrite);
    assert_eq!(header.kind, kinds::READY, "the new database is served");

    let (header, payload) = writer.write_block(StreamId(1), &["a.py", "b.py", "c.py"]);
    assert_eq!(header.kind, kinds::COMPLETE);
    assert_eq!(
        protocol::decode_complete(&payload).expect("a complete"),
        (3, 0)
    );

    assert_eq!(writer.count(StreamId(2), "X where src.File X"), 3);

    // ---- seal
    let ControlReply::Finished {
        fingerprint,
        facts,
        bytes,
        already_complete,
    } = control.control(ControlOp::Finish, "code", false)
    else {
        panic!("expected a finished reply");
    };

    assert_eq!(facts, 3);
    assert!(fingerprint != 0, "an identity was computed, not stubbed");
    assert!(bytes > 0);
    assert!(!already_complete);

    let entry = serving
        .catalog()
        .resolve(&Selector::of("code"), Intent::Read)
        .expect("it is on the disk");
    assert_eq!(entry.meta.content_fingerprint, Some(fingerprint));
    assert!(!entry.status().is_writable(), "the sidecar flipped");

    // ---- `ops-I2`: no writable session exists for it, ever again
    let (_refused, header, payload) = Client::hello(&serving, "code", Mode::ReadWrite);
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::ModeRefused);
    assert!(message.contains("code"), "{message}");

    // ...while reading it goes on working, which is what sealing is *for*.
    let (mut reader, header, _) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(header.kind, kinds::READY);
    assert_eq!(reader.count(StreamId(1), "X where src.File X"), 3);
    drop(reader);
    drop(writer);

    // ---- remove
    eventually("the sessions let go of `code`", || {
        matches!(
            control.control_raw(ControlOp::Remove, "code", false).0.kind,
            kinds::CONTROL_REPLY
        )
    });

    assert!(
        serving
            .catalog()
            .find("code")
            .expect("the root reads")
            .is_none(),
        "it is gone from the disk"
    );

    let (_gone, header, payload) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(header.kind, FrameKind::ERROR);
    assert_eq!(error_of(&payload).0, ErrorCode::UnknownDatabase);
}

/// **`ops-I2` reaches a session that was already open.** A write session established
/// while the database was Writable does not get to keep writing across a seal — which
/// is the case the establishment check alone cannot answer, and the reason the seal
/// happens inside the per-database writer lock.
#[test]
fn a_seal_stops_a_write_session_that_was_already_established() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);
    control.control(ControlOp::Create, "code", false);

    // Established *before* the seal, and kept open across it.
    let (mut writer, header, _) = Client::hello(&serving, "code", Mode::ReadWrite);
    assert_eq!(header.kind, kinds::READY);

    let (header, _) = writer.write_block(StreamId(1), &["a.py", "b.py"]);
    assert_eq!(header.kind, kinds::COMPLETE, "the first block lands");

    let reply = control.control(ControlOp::Finish, "code", false);
    assert!(
        matches!(reply, ControlReply::Finished { facts: 2, .. }),
        "{reply:?}"
    );

    // The same connection, the same session, a second write stream — refused.
    let (header, payload) = writer.write_block(StreamId(2), &["c.py"]);
    assert_eq!(header.kind, FrameKind::ERROR);
    assert_eq!(error_of(&payload).0, ErrorCode::ModeRefused);

    // ...and the refusal was a refusal, not a partial write: the sealed database holds
    // what it held when it was sealed, and its recorded count still describes it.
    let (mut reader, _, _) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(reader.count(StreamId(1), "X where src.File X"), 2);
}

/// **`ops-I6` is about the whole session, not about facts.** A read-only session does
/// not get to create, seal or delete a database by asking on a different frame kind.
#[test]
fn a_read_only_session_cannot_change_the_lifecycle() {
    let serving = start();

    // Made by a session that may, so there is something to try to destroy.
    let mut allowed = Client::control_session(&serving, Mode::ReadWrite);
    allowed.control(ControlOp::Create, "code", false);

    let mut reader = Client::control_session(&serving, Mode::ReadOnly);

    for op in [ControlOp::Create, ControlOp::Finish, ControlOp::Remove] {
        let name = if op == ControlOp::Create {
            "other"
        } else {
            "code"
        };
        let (header, payload) = reader.control_raw(op, name, true);

        assert_eq!(header.kind, FrameKind::ERROR, "{op:?} should be refused");
        assert_eq!(error_of(&payload).0, ErrorCode::ModeRefused);
    }

    // Nothing happened: no second database, and the first is untouched.
    assert!(
        serving
            .catalog()
            .find("other")
            .expect("the root reads")
            .is_none()
    );
    assert!(
        serving
            .catalog()
            .resolve(&Selector::of("code"), Intent::Read)
            .expect("still there")
            .status()
            .is_writable()
    );
}

/// A database a session still holds is **refused by name**, not pulled out from under
/// it. `remove` closes the store, and a query running against a closed store is a
/// fault the client did not cause.
#[test]
fn removing_a_database_a_session_holds_is_refused() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);
    control.control(ControlOp::Create, "code", false);

    let (mut holder, header, _) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(header.kind, kinds::READY);

    let (header, payload) = control.control_raw(ControlOp::Remove, "code", false);
    assert_eq!(header.kind, FrameKind::ERROR);

    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::InUse);
    assert!(message.contains("code"), "{message}");

    // Refused, and *nothing else*: the holder's session is still serving.
    assert_eq!(holder.count(StreamId(1), "X where src.File X"), 0);
    assert!(
        serving
            .catalog()
            .find("code")
            .expect("the root reads")
            .is_some()
    );

    drop(holder);

    // The refusal was contention, not a state: it ends when the session does.
    eventually("the session lets go of `code`", || {
        matches!(
            control.control_raw(ControlOp::Remove, "code", false).0.kind,
            kinds::CONTROL_REPLY
        )
    });

    assert!(
        serving
            .catalog()
            .find("code")
            .expect("the root reads")
            .is_none()
    );
}

/// **A control session can ask the one question it is for.**
///
/// It is bound to no database, and the first thing anyone wants of a server is the
/// list of them — which used to be the one question a session could not be opened to
/// ask. It handshakes against the catalogue schema, so answering a query over that
/// schema is the server agreeing with what it already told the client.
#[test]
fn a_control_session_can_query_the_catalogue() {
    let serving = start_with(Schemas::default());
    let mut control = Client::control_session(&serving, Mode::ReadWrite);
    control.control(ControlOp::Create, "code", false);
    control.control(ControlOp::Create, "other", false);

    control.send(kinds::QUERY, StreamId(2), b"X where fjord.db.List X");

    // A row description, then a row per database, then the end of the stream.
    let (header, payload) = control.recv();
    assert_eq!(
        header.kind,
        FrameKind::ROW_DESCRIPTION,
        "{:?}",
        error_of(&payload)
    );

    let mut rows = 0;
    loop {
        let (header, _) = control.recv();
        match header.kind {
            FrameKind::DATA_ROW => rows += 1,
            // The listing's digest, which a query reading `fjord.db.List` is sent so a
            // resume can be refused when the catalogue moves under it.
            kinds::LISTING_DIGEST => {}
            kinds::COMPLETE => break,
            other => panic!("unexpected {other:?}"),
        }
    }

    assert_eq!(rows, 2, "one row per database");
}

/// **A stored predicate is refused for what it is**, not for the session's shape.
///
/// A control session's schema is the catalogue and nothing else, so `src.File` is not
/// a predicate it has — which is a compile error naming the schema, and a better
/// answer than "name a database at startup" was. The refusal is still a refusal; what
/// changed is that it says the true thing.
#[test]
fn a_control_session_has_no_stored_predicate_to_read() {
    let serving = start_with(Schemas::default());
    let mut control = Client::control_session(&serving, Mode::ReadWrite);
    control.control(ControlOp::Create, "code", false);

    control.send(kinds::QUERY, StreamId(2), b"X where src.File X");
    let (header, payload) = control.recv();

    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::BadQuery, "{message}");
    assert!(message.contains("src.File"), "{message}");

    // Naming a database that does not exist is a different rule and still holds: a
    // session binds a database or it binds none, and never something almost right.
    let (_client, header, payload) = Client::hello(&serving, "nope", Mode::ReadOnly);
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::UnknownDatabase);
    assert!(message.contains("nope"), "{message}");
}

/// **A write still needs somewhere to put a fact.** Reading the catalogue is a
/// question about the server; writing is a change to a database, and there is none.
#[test]
fn a_control_session_still_cannot_write() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);

    control.send(kinds::OPEN_WRITE, StreamId(2), b"");
    let (header, payload) = control.recv();

    assert_eq!(header.kind, FrameKind::ERROR);
    assert_eq!(error_of(&payload).0, ErrorCode::UnknownDatabase);
}

/// A lifecycle request the store declines comes back as a **refusal**, with the reason
/// in it — not as `Internal`, which would send someone to the server's logs to read a
/// message already in their hand.
#[test]
fn a_declined_request_says_why() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);
    control.control(ControlOp::Create, "code", false);

    // A second `create` under the same name is not a refusal any more — it is a second
    // instance — so the refusal exercised here is the one that replaced it: naming a
    // database that holds two, for an operation that must not guess which. Done under
    // its own name, so that `code` stays a single instance for everything below.
    control.control(ControlOp::Create, "twin", false);
    control.control(ControlOp::Create, "twin", false);

    let (header, payload) = control.control_raw(ControlOp::Remove, "twin", false);
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::Refused);
    assert!(message.contains("2 instances"), "{message}");

    // An empty database will not seal without being told to, over the wire exactly as
    // it will not offline — a silently-empty sealed artifact is the same CI failure
    // whichever door it came through.
    let (header, payload) = control.control_raw(ControlOp::Finish, "code", false);
    assert_eq!(header.kind, FrameKind::ERROR);
    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::Refused);
    assert!(message.contains("--allow-zero-facts"), "{message}");

    // ...and does when it is.
    let reply = control.control(ControlOp::Finish, "code", true);
    assert!(
        matches!(reply, ControlReply::Finished { facts: 0, .. }),
        "{reply:?}"
    );

    // Sealing again is the same no-op it is offline, with the notice a client needs to
    // tell "I sealed it" from "it was already sealed".
    let reply = control.control(ControlOp::Finish, "code", true);
    assert!(
        matches!(
            reply,
            ControlReply::Finished {
                already_complete: true,
                ..
            }
        ),
        "{reply:?}"
    );

    let (header, payload) = control.control_raw(ControlOp::Remove, "nope", false);
    assert_eq!(header.kind, FrameKind::ERROR);
    assert_eq!(error_of(&payload).0, ErrorCode::UnknownDatabase);
}

/// **A database published into a live root binds over the wire**, with nothing having
/// told the server it arrived.
///
/// That is the deployment rather than a curiosity: CI seals `<name>/<ULID>/` and a
/// sidecar syncs it under the root a server owns, and `ops-I7` means the create needs
/// no ownership of the root to do it. A handshake that refused what it resolved would
/// leave this name advertised by `fjord.db.List` and unbindable until the process was
/// restarted.
#[test]
fn a_database_published_under_a_running_server_binds_without_a_restart() {
    let serving = start();

    serving
        .catalog()
        .create("published", &schema())
        .expect("a database");

    let (_client, header, payload) = Client::hello(&serving, "published", Mode::ReadOnly);

    assert_eq!(
        header.kind,
        kinds::READY,
        "it must bind: {:?}",
        protocol::decode_error(&payload)
    );
}

/// **And one that will not open says so over the wire**, naming the instance and what
/// went wrong with it.
///
/// The whole point of telling the two apart is what an operator reads, and an operator
/// reads it through a socket. "No database named `broken`" for a directory that is
/// plainly under the root is the answer that sends somebody looking in the wrong place;
/// a name that is genuinely absent still gets exactly that, which is the contrast being
/// asserted here.
#[test]
fn a_database_that_will_not_open_says_which_instance_and_why() {
    let serving = start();

    let published = serving
        .catalog()
        .create("broken", &schema())
        .expect("a database");

    // What a half-finished sync leaves: the sidecar has landed, the schema copy the
    // database must be served through has not.
    std::fs::remove_dir_all(published.path.join("schema")).expect("it goes");

    let (_client, header, payload) = Client::hello(&serving, "broken", Mode::ReadOnly);
    assert_eq!(header.kind, FrameKind::ERROR);

    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::UnknownDatabase);
    assert!(
        message.contains(&published.meta.instance),
        "the client is told which instance: {message}"
    );
    assert!(
        message.contains("no schema copy"),
        "and what could not be read: {message}"
    );

    let (_client, header, payload) = Client::hello(&serving, "absent", Mode::ReadOnly);
    assert_eq!(header.kind, FrameKind::ERROR);

    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::UnknownDatabase, "the same code");
    assert!(
        message.contains("no database named"),
        "and a different sentence: {message}"
    );
}

/// **`create` needs a schema, and an empty one is refused rather than substituted.**
///
/// Until 0.0.1 an empty `schema` field meant "this server's own", and a server carried a
/// built-in one to hand over. That made the artifact a property of whichever binary
/// happened to be listening: the same command against two builds produced two databases
/// with two different embedded schemas, and nothing said so. `create` now requires the
/// text, which is what [operations §5](../../../website/content/operations.md) always
/// specified.
///
/// **What is refused is the schema, not the field.** Stated as a check on the request
/// — an empty `schema` string, answered with "pass one with `--schema`" — the rule
/// fails an empty schema *file* over the wire while the identical `create` goes through
/// against the directory. The empty source lowers to an empty schema and
/// [`Catalog::create`] refuses that at both doors, so the two answers are one answer.
///
/// Sent as a raw control frame because no client should make this easy to do by accident
/// — `fjord create` has a required flag, and `Connection::create` takes the source.
#[test]
fn creating_a_database_with_no_schema_is_refused() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);

    let request = protocol::encode_control(&Control {
        op: ControlOp::Create,
        database: "schemaless".to_owned(),
        allow_zero_facts: false,
        schema: String::new(),
    });

    control.send(kinds::CONTROL, StreamId(1), &request);
    let (header, payload) = control.recv();

    assert_eq!(header.kind, FrameKind::ERROR, "an empty schema is refused");
    let (_, message) = error_of(&payload);
    assert!(
        message.contains("declares no predicates"),
        "the refusal should say what is wrong with it: {message}"
    );

    // And nothing was left behind by the attempt.
    let catalog = Catalog::open(&serving.root).expect("a store root");
    assert!(
        catalog
            .resolve(
                &Selector::parse("schemaless").expect("a selector"),
                Intent::Read
            )
            .is_err(),
        "a refused create leaves no database"
    );
}

/// **A copy that has not finished delivering is refused over the wire**, and the same
/// name answers its facts once it has.
///
/// The contract layer is the point. An operator reads this through a socket, and a
/// client branches on the code: a copy that has not delivered the store yet is a
/// database that will be there shortly, so it answers `InUse` — the retryable one —
/// rather than `UnknownDatabase` about a directory that is plainly under the root. What
/// it must never do is what it did before: answer `READY` and serve zero rows out of an
/// empty store this server stamped into the copy's own target, permanently.
///
/// **`InUse` is the code for this part of the copy, not for the whole of it.** The
/// shape here is before fjall's marker file lands. Past that point the open goes ahead,
/// its recovery deletes the keyspaces the copy has not finished, and the refusal that
/// comes back is a fact count that will not agree however long anybody waits —
/// `UnknownDatabase`, a database that is there and will not open.
/// `a_store_one_keyspace_manifest_short_is_never_served_the_facts_it_records` is that
/// window; publishing under one rename is what keeps a bind out of it.
///
/// Built, sealed and removed through the server so that the artifact being delivered is
/// a real one with a real fact count, then delivered back under the name and instance id
/// it was built at — which is what a copy into a store root does.
#[test]
fn a_copy_that_has_not_finished_is_refused_over_the_wire() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);

    let ControlReply::Created { instance } = control.control(ControlOp::Create, "code", false)
    else {
        panic!("expected a created reply");
    };

    let built = serving
        .catalog()
        .resolve(&Selector::of("code"), Intent::Read)
        .expect("it is on the disk")
        .path;

    {
        let (mut writer, header, _) = Client::hello(&serving, "code", Mode::ReadWrite);
        assert_eq!(header.kind, kinds::READY);
        let (header, _) = writer.write_block(StreamId(1), &["a.py", "b.py", "c.py"]);
        assert_eq!(header.kind, kinds::COMPLETE);
    }

    let reply = control.control(ControlOp::Finish, "code", false);
    assert!(
        matches!(reply, ControlReply::Finished { facts: 3, .. }),
        "{reply:?}"
    );

    // The sealed artifact CI would publish, kept outside the root, and the root put
    // back the way it was before it was there.
    let stash = tempfile::tempdir().expect("a scratch directory");
    let source = stash.path().join("sealed");
    copy_tree(&built, &source);

    eventually("the sessions let go of `code`", || {
        matches!(
            control.control_raw(ControlOp::Remove, "code", false).0.kind,
            kinds::CONTROL_REPLY
        )
    });
    assert!(!built.exists(), "the root holds nothing under that name");

    // A copy in flight: the sidecar and the schema copy have landed, the tables have
    // not. `fjord.db.List` reports three facts for it from here on, because `ops-I7`
    // reads the sidecar.
    for part in ["FJORD_META", "schema"] {
        copy_tree(&source.join(part), &built.join(part));
    }

    let (_client, header, payload) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(
        header.kind,
        FrameKind::ERROR,
        "a database whose store has not arrived must not be served"
    );

    let (code, message) = error_of(&payload);
    assert_eq!(
        code,
        ErrorCode::InUse,
        "the copy ends by itself, so this is the retryable code: {message}"
    );
    assert!(
        message.contains(&instance),
        "the client is told which instance: {message}"
    );
    assert!(
        message.contains("has a sidecar but no store"),
        "and what is missing: {message}"
    );

    // The rest of it arrives, and the same handshake is served the database — with the
    // rows the sidecar has been claiming all along, which is the assertion the defect
    // failed: it answered `READY` and zero.
    copy_tree(&source, &built);

    let (mut reader, header, payload) = Client::hello(&serving, "code", Mode::ReadOnly);
    assert_eq!(
        header.kind,
        kinds::READY,
        "the finished copy is served: {:?}",
        protocol::decode_error(&payload)
    );
    assert_eq!(reader.count(StreamId(1), "X where src.File X"), 3);
}

/// **A store another process is holding answers `InUse` over the wire**, not
/// `UnknownDatabase`.
///
/// The mapping has an in-process guard; this is the layer it exists for. A client that
/// reads code 2 for a held instance stops, because "no such database" is the one answer
/// worth no retry — and the condition ends the moment the holder lets go, which is the
/// second half here.
#[test]
fn a_held_instance_answers_in_use_over_the_wire() {
    let serving = start();

    // Published behind the server's back, so this server has never opened it and the
    // handle below is the only one on it.
    let published = serving
        .catalog()
        .create("held", &schema())
        .expect("a database");
    let holder = FjallDb::open(&published.path).expect("something else has it");

    let (_client, header, payload) = Client::hello(&serving, "held", Mode::ReadOnly);
    assert_eq!(header.kind, FrameKind::ERROR);

    let (code, message) = error_of(&payload);
    assert_eq!(code, ErrorCode::InUse, "{message}");
    assert!(
        message.contains(&published.meta.instance),
        "the client is told which instance: {message}"
    );

    drop(holder);

    let (_client, header, payload) = Client::hello(&serving, "held", Mode::ReadOnly);
    assert_eq!(
        header.kind,
        kinds::READY,
        "and it binds once the holder lets go: {:?}",
        protocol::decode_error(&payload)
    );
}

/// Copy a file, or a directory and everything under it, to `to`.
fn copy_tree(from: &std::path::Path, to: &std::path::Path) {
    if from.is_dir() {
        std::fs::create_dir_all(to).expect("a directory");
        for entry in std::fs::read_dir(from).expect("a directory") {
            let entry = entry.expect("an entry");
            copy_tree(&entry.path(), &to.join(entry.file_name()));
        }
    } else {
        if let Some(parent) = to.parent() {
            std::fs::create_dir_all(parent).expect("a parent directory");
        }
        std::fs::copy(from, to).expect("a copy");
    }
}

/// **A schema declaring into the reserved namespace is refused, and publishes nothing.**
///
/// Serving a database composes its own schema with the server's catalogue and marks every
/// reserved predicate virtual, so a database that already declares `fjord.db.List`
/// composes to two of them and cannot be opened at all. `create` used to accept such a
/// schema, publish the instance, and *then* fail to open it — answering
/// [`ServerError::Internal`] about an artifact sitting under the root that no listing
/// could explain and nothing could repair.
///
/// **`Catalog::create` now refuses it before anything is written**, which is what this
/// asserts: the refusal names the predicate, and the root is left as it was found.
///
/// The claim that replaced it — an open that fails after a publish answers `Internal`
/// rather than "no such database" — is
/// [`a_database_that_will_not_open_says_which_instance_and_why`], which provokes it with a
/// genuinely broken artifact rather than through `create`. That is the better provocation
/// anyway: it does not depend on a wart to reach the state it is about.
#[test]
fn a_schema_in_the_reserved_namespace_is_refused_and_publishes_nothing() {
    let serving = start();
    let mut control = Client::control_session(&serving, Mode::ReadWrite);

    let request = protocol::encode_control(&Control {
        op: ControlOp::Create,
        database: "collides".to_owned(),
        allow_zero_facts: false,
        schema: "schema fjord.db {\n  predicate List : string\n}\n".to_owned(),
    });

    control.send(kinds::CONTROL, StreamId(1), &request);
    let (header, payload) = control.recv();
    assert_eq!(header.kind, FrameKind::ERROR);

    let (_, message) = error_of(&payload);
    assert!(
        message.contains("fjord.db.List"),
        "the refusal names the predicate that cannot be declared: {message}"
    );

    // **Nothing published is the half that used to fail.** A refusal that still left an
    // instance under the root would leave a database no server can open and no listing
    // can explain, which is the state this check exists to prevent.
    assert!(
        serving
            .catalog()
            .find("collides")
            .expect("the root reads")
            .is_none(),
        "a refused create must publish nothing"
    );
}
