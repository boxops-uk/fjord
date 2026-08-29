//! The client, against a real server over a real socket.
//!
//! Not a mock, and the reason is the same one the .NET demo keeps proving: a client
//! tested against our idea of the server tests the idea. What is being checked here is
//! the conversation — that a page holds its place, that two results can be open at
//! once, and that a cancel ends one stream and leaves the connection working.

use std::{
    io::{Read, Write},
    os::unix::net::UnixStream as RawUnixStream,
    path::PathBuf,
    sync::Arc,
    thread,
};

use fjord_client::{
    ClientError, Connection, ErrorCode, Expander, FULL_DEPTH, Mode, WireFact, WireRef, WireValue,
};
use fjord_schema::fingerprint;
use fjord_schema::id::FactId;
use fjord_schema::schema::{Alternative, Predicate, PredicateId, PredicateTy, Schema};
use fjord_server::{Registry, registry::Schemas, server::Listener};
use fjord_store_fjall::catalog::Catalog;
use fjord_wire::protocol::Found;
use lasso::Rodeo;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);
const DOC: PredicateId = PredicateId(2);
const TAGGED: PredicateId = PredicateId(3);

/// The alternatives of `src.Tagged`'s union: tags that are neither positions nor in
/// declaration order, so a peer numbering them by position is caught by the rows.
const NUM: u32 = 3;
const TEXT: u32 = 0;
const OF: u32 = 40_000;

fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let (file, decl, doc) = (
        rodeo.get_or_intern("src.File"),
        rodeo.get_or_intern("src.Decl"),
        rodeo.get_or_intern("src.Doc"),
    );
    let tagged = rodeo.get_or_intern("src.Tagged");
    let (f_what, f_id) = (rodeo.get_or_intern("what"), rodeo.get_or_intern("id"));
    let (a_num, a_text, a_of) = (
        rodeo.get_or_intern("num"),
        rodeo.get_or_intern("text"),
        rodeo.get_or_intern("of"),
    );
    let (f_file, f_line, f_name, f_decl) = (
        rodeo.get_or_intern("file"),
        rodeo.get_or_intern("line"),
        rodeo.get_or_intern("name"),
        rodeo.get_or_intern("decl"),
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
            // **A key of one field, and that field a reference.** The shape the built-in
            // schema uses for an attribute *of* something — a declaration has at most
            // one doc comment and at most one type, so the declaration alone is the
            // identity and the answer is the value. It encodes as the bare reference
            // does, which is exactly why it is worth a test of its own: nothing else
            // here would notice if a record of one started framing itself.
            Predicate {
                name: doc,
                key: PredicateTy::Record(vec![(f_decl, PredicateTy::Fact(DECL))].into()),
                value: Some(PredicateTy::Str),
            },
            // **A union in the leading key field**, one of whose alternatives is a
            // *reference* — so a fact written down this socket has to be interned
            // through a payload, and a row read back off it has to be described by a
            // descriptor that carries the alternatives' names.
            Predicate {
                name: tagged,
                key: PredicateTy::Record(
                    vec![
                        (
                            f_what,
                            PredicateTy::Union(
                                vec![
                                    Alternative {
                                        name: a_num,
                                        disc: NUM,
                                        ty: PredicateTy::Int,
                                    },
                                    Alternative {
                                        name: a_text,
                                        disc: TEXT,
                                        ty: PredicateTy::Str,
                                    },
                                    Alternative {
                                        name: a_of,
                                        disc: OF,
                                        ty: PredicateTy::Fact(FILE),
                                    },
                                ]
                                .into(),
                            ),
                        ),
                        (f_id, PredicateTy::Int),
                    ]
                    .into(),
                ),
                value: None,
            },
        ]),
    )
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
                // Nested: this client holds no ids at all, which is the point.
                WireValue::Ref(WireRef::Nested(Box::new(file(path)))),
                WireValue::Int(line),
                WireValue::Str(name.to_owned()),
            ]
            .into(),
        ),
        value: None,
    }
}

/// A doc comment for a declaration, nested three deep: doc → declaration → file.
fn doc(path: &str, line: i64, name: &str, text: &str) -> WireFact {
    WireFact {
        predicate: DOC,
        key: WireValue::Record(
            vec![WireValue::Ref(WireRef::Nested(Box::new(decl(
                path, line, name,
            ))))]
            .into(),
        ),
        value: Some(WireValue::Str(text.to_owned())),
    }
}

struct Serving {
    _dir: tempfile::TempDir,
    socket: PathBuf,
    /// The registry the server is running, kept so a test can read its counters.
    registry: Arc<Registry>,
}

impl Serving {
    fn open(&self, mode: Mode) -> Connection {
        Connection::connect(&self.socket, "code", Arc::new(schema()), mode, true)
            .expect("a connection")
    }

    fn control(&self) -> Connection {
        Connection::control(&self.socket).expect("a control session")
    }
}

fn start() -> Serving {
    start_with_examined_ceiling(None)
}

fn start_with_examined_ceiling(examined_ceiling: Option<u64>) -> Serving {
    start_configured(examined_ceiling, "")
}

fn start_with_catalogue() -> Serving {
    start_configured(None, fjord_server::catalogue::SOURCE)
}

fn start_configured(examined_ceiling: Option<u64>, virtual_source: &str) -> Serving {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fjord.sock");

    let schema = schema();
    let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
    catalog.create("code", &schema).expect("a database");

    let (registry, _listing) =
        Registry::open(catalog, Schemas::new(virtual_source)).expect("a registry");
    let registry = Arc::new(registry);
    let mut listener = Listener::bind(&socket).expect("a socket");
    if let Some(ceiling) = examined_ceiling {
        listener = listener.with_examined_ceiling(ceiling);
    }

    let serving = Arc::clone(&registry);
    thread::spawn(move || {
        let _ = listener.run_blocking(serving);
    });

    Serving {
        _dir: dir,
        socket,
        registry,
    }
}

fn send_frame(
    stream: &mut RawUnixStream,
    kind: fjord_wire::FrameKind,
    id: fjord_wire::StreamId,
    payload: &[u8],
) {
    let mut out = vec![];
    fjord_wire::frame::encode_frame(&mut out, kind, id, payload).expect("a frame encodes");
    stream.write_all(&out).expect("the frame is sent");
}

/// Read exactly one frame without consuming any bytes belonging to the next one.
fn read_frame(stream: &mut RawUnixStream) -> (fjord_wire::FrameHeader, Vec<u8>) {
    let mut head = [0u8; fjord_wire::frame::HEADER_LEN];
    stream
        .read_exact(&mut head)
        .expect("a frame header arrives");
    let header = fjord_wire::frame::decode_header(&head).expect("a frame header");
    let mut payload = vec![0u8; header.length as usize];
    stream
        .read_exact(&mut payload)
        .expect("the frame payload arrives");
    (header, payload)
}

fn ready_fake_server(stream: &mut RawUnixStream) {
    let (header, _) = read_frame(stream);
    assert_eq!(header.kind, fjord_wire::protocol::kinds::STARTUP);
    let ready = fjord_wire::protocol::encode_ready(&fjord_wire::protocol::Ready {
        version: fjord_wire::protocol::VERSION,
        schema_fingerprint: 0,
        predicates: 0,
    });
    send_frame(
        stream,
        fjord_wire::protocol::kinds::READY,
        header.stream,
        &ready,
    );
}

fn string_row(text: &str) -> Vec<u8> {
    let mut out = vec![];
    fjord_wire::value::encode_value(
        &mut out,
        &schema(),
        &PredicateTy::Str,
        &WireValue::Str(text.to_owned()),
    )
    .expect("a string encodes");
    out
}

/// Write `count` files, so a result can be made as long as a test needs.
fn seed(connection: &mut Connection, count: usize) {
    let facts: Vec<WireFact> = (0..count).map(|n| file(&format!("f{n:05}.py"))).collect();
    let written = connection.write(FILE, &facts).expect("they are written");
    assert_eq!(written.created, count as u64);
}

fn strings(rows: &[WireValue]) -> Vec<String> {
    rows.iter()
        .map(|row| match row {
            WireValue::Str(text) => text.clone(),
            other => panic!("expected a string row, got {other:?}"),
        })
        .collect()
}

/// Handshake, write facts holding no ids, read them back — one connection, and every
/// step through the client rather than around it.
#[test]
fn facts_written_by_this_client_are_queried_back_by_it() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    assert_eq!(connection.hello().version, fjord_wire::protocol::VERSION);
    assert_eq!(connection.hello().predicates, 4);
    assert_eq!(
        connection.hello().schema_fingerprint,
        fingerprint::of(&schema()),
        "the handshake asserted our schema and the server agreed"
    );

    let written = connection
        .write(
            DECL,
            &[
                decl("store/keys.py", 12, "key_of"),
                decl("store/keys.py", 48, "key_prefix"),
                decl("store/codec.py", 7, "encode_key"),
            ],
        )
        .expect("the facts are written");

    // Three declarations and two files: `store/keys.py` is named twice and written
    // once. Interning, and the client never learned what anything was called.
    assert_eq!((written.created, written.deduped), (5, 1));
    assert_eq!(written.seen(), 6);

    let mut rows = connection.query("F where src.File F").expect("it compiles");
    assert_eq!(rows.desc(), &fjord_client::Desc::Str);

    let mut paths = strings(&connection.drain(&mut rows).expect("the rows arrive"));
    paths.sort();

    assert_eq!(paths, ["store/codec.py", "store/keys.py"]);
    assert!(rows.finished());
    assert_eq!(rows.sent(), 2);
}

/// **A key of one field, holding a reference, behind a value.**
///
/// Three things at once, and each is a place a shape can be got wrong on its own: the
/// key is a record of one — which encodes as its single field and must not start
/// framing itself — the field is a reference nested two levels deep, so interning has
/// to reach the file through the declaration before the doc's key has any bytes, and
/// the fact has a value side that the query reads without matching on
/// ([I6](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i6)).
///
/// It is written here rather than only in the encoder's golden because encoding a shape
/// correctly and *storing* one are different claims.
#[test]
fn a_key_of_one_field_holds_a_reference_and_a_value() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    let written = connection
        .write(
            DOC,
            &[
                doc(
                    "store/keys.py",
                    12,
                    "key_of",
                    "The key a row is filed under.",
                ),
                doc("store/codec.py", 7, "encode_key", "Order-preserving."),
            ],
        )
        .expect("the facts are written");

    // Two docs, two declarations, two files: nothing was here before, and every one of
    // the six was named by nesting rather than by an id.
    assert_eq!((written.created, written.deduped), (6, 0));

    // The value is read, the reference is followed, and neither is matched on.
    let mut rows = connection
        .query("{name = D.name, text = T.value} where T = src.Doc {decl = D}")
        .expect("it compiles");

    let mut answers: Vec<String> = connection
        .drain(&mut rows)
        .expect("the rows arrive")
        .iter()
        .map(|row| match row {
            WireValue::Record(fields) => match (&fields[0], &fields[1]) {
                (WireValue::Str(name), WireValue::Str(text)) => format!("{name}: {text}"),
                other => panic!("expected two strings, got {other:?}"),
            },
            other => panic!("expected a record row, got {other:?}"),
        })
        .collect();

    answers.sort();

    assert_eq!(
        answers,
        [
            "encode_key: Order-preserving.",
            "key_of: The key a row is filed under.",
        ]
    );
}

/// **A reference expanded into the fact it names, all the way down.**
///
/// The read-path counterpart of the write path this file already proves: a producer
/// nests a target rather than holding an id, ingest interns it, and a query then answers
/// with the id — which is a number naming a fact the reader cannot see. Expanding it
/// recovers the same nested shape the producer sent, which is the claim worth having,
/// since it means one logical form rather than a display invention.
///
/// `src.Doc` is keyed by a reference to a declaration, whose key holds a reference to a
/// file: three predicates, two hops, and the deepest chain this schema has.
#[test]
fn a_reference_expands_into_the_fact_it_names() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    connection
        .write(
            DOC,
            &[
                doc(
                    "store/keys.py",
                    12,
                    "key_of",
                    "The key a row is filed under.",
                ),
                doc(
                    "store/keys.py",
                    48,
                    "key_prefix",
                    "Everything under a prefix.",
                ),
            ],
        )
        .expect("the facts are written");

    let schema = Arc::new(schema());
    let mut rows = connection
        .query("D where src.Doc D")
        .expect("a whole-fact bind compiles");
    let unexpanded = connection.drain(&mut rows).expect("the rows arrive");

    // What a row holds before expansion: one field, and it is an id.
    let WireValue::Record(fields) = &unexpanded[0] else {
        panic!("src.Doc's key is a record of one: {:?}", unexpanded[0]);
    };
    assert!(
        matches!(fields[0], WireValue::Ref(WireRef::Id(_))),
        "stored, a reference is an id: {fields:?}"
    );

    let mut expander = Expander::new(Arc::clone(&schema));
    let expanded = expander
        .expand(&mut connection, &unexpanded[0], FULL_DEPTH, &[])
        .expect("the ids resolve");

    // doc → declaration → file, and the file's key is the path the producer nested.
    let decl = nested(field(&expanded, 0));
    assert_eq!(decl.predicate, DECL);

    let WireValue::Record(decl_fields) = &decl.key else {
        panic!("a declaration's key is a record: {decl:?}");
    };
    assert_eq!(decl_fields[1], WireValue::Int(12));
    assert_eq!(decl_fields[2], WireValue::Str("key_of".to_owned()));

    let file = nested(&decl_fields[0]);
    assert_eq!(file.predicate, FILE);
    assert_eq!(file.key, WireValue::Str("store/keys.py".to_owned()));

    // Two point reads for two hops, and **the value side is not one of them**: a
    // reference names an identity, and the identity is the key.
    assert_eq!(expander.fetched(), 2);
    assert_eq!(expander.unresolved(), 0);
    assert!(
        decl.value.is_none() && file.value.is_none(),
        "expansion answers keys"
    );

    // **The cache is what makes this affordable.** The second row names a different
    // declaration in the *same* file, so it costs one read rather than two — and
    // re-expanding the first costs none at all.
    expander
        .expand(&mut connection, &unexpanded[1], FULL_DEPTH, &[])
        .expect("the ids resolve");
    assert_eq!(expander.fetched(), 3, "the file was already known");

    expander
        .expand(&mut connection, &unexpanded[0], FULL_DEPTH, &[])
        .expect("the ids resolve");
    assert_eq!(expander.fetched(), 3, "nothing was read twice");

    // Depth is hops: one reaches the declaration and leaves its file an id.
    let shallow = expander
        .expand(&mut connection, &unexpanded[0], 1, &[])
        .expect("the ids resolve");
    let WireValue::Record(shallow_decl) = &nested(field(&shallow, 0)).key else {
        panic!("a declaration's key is a record");
    };
    assert!(
        matches!(shallow_decl[0], WireValue::Ref(WireRef::Id(_))),
        "the second hop is not taken at depth 1: {shallow_decl:?}"
    );
}

/// **An id naming no fact is an absence, and one naming no predicate is a refusal.**
///
/// The first cannot happen for an id out of a row — both column families are written
/// together ([I12](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i12)) and ids are never reused
/// ([I11](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i11)) — so the server answers "nothing" and lets
/// the client decide what that means about where the id came from. The second is a
/// question about a schema both ends share, so it is refused on the stream that asked,
/// and the session goes on.
#[test]
fn an_id_that_names_nothing_is_answered_and_one_that_cannot_exist_is_refused() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);
    let schema = Arc::new(schema());

    connection
        .write(FILE, &[file("a.py")])
        .expect("one file is written");

    let absent = FactId::new(FILE, 9_999).expect("a well-formed id");
    assert_eq!(
        connection
            .fetch(&schema, &[absent], None)
            .expect("it is answered"),
        vec![Found::Missing],
        "a well-formed id for a fact nobody wrote"
    );

    let nowhere = FactId::new(PredicateId(99), 1).expect("a well-formed id");
    let refused = connection
        .fetch(&schema, &[nowhere], None)
        .expect_err("no such predicate");
    assert!(
        matches!(refused, ClientError::Server { .. }),
        "refused on the stream, not by closing the connection: {refused:?}"
    );

    // And the session still answers.
    let mut rows = connection.query("F where src.File F").expect("it compiles");
    assert_eq!(
        strings(&connection.drain(&mut rows).expect("the rows arrive")),
        ["a.py"]
    );
}

/// **Only a *protocol* refusal means "this server cannot do that".**
///
/// A fetch is translated into [`ClientError::Unsupported`] when the server answers with
/// `ErrorCode::Protocol`, because on that stream it can only mean the `F` frame was not
/// understood — a server older than expansion. The translation has to be that narrow: a
/// session bound to no database is refused for an ordinary reason, with an ordinary code,
/// and reporting *that* as "restart your server" would send somebody after the wrong
/// thing entirely.
#[test]
fn a_refusal_that_is_not_about_the_frame_is_not_reported_as_an_old_server() {
    let serving = start();
    let mut control = serving.control();
    let schema = Arc::new(schema());

    let id = FactId::new(FILE, 1).expect("a well-formed id");
    let refused = control
        .fetch(&schema, &[id], None)
        .expect_err("a control session names no database");

    assert!(
        matches!(
            refused,
            ClientError::Server {
                code: ErrorCode::UnknownDatabase,
                ..
            }
        ),
        "an ordinary refusal, under its own code: {refused:?}"
    );
}

/// The fact behind a reference, or a panic naming what was there instead.
fn nested(value: &WireValue) -> &WireFact {
    match value {
        WireValue::Ref(WireRef::Nested(fact)) => fact,
        other => panic!("expected an expanded reference, got {other:?}"),
    }
}

/// One field of a record row.
fn field(value: &WireValue, index: usize) -> &WireValue {
    match value {
        WireValue::Record(fields) => &fields[index],
        other => panic!("expected a record, got {other:?}"),
    }
}

/// **The page holds its place, and the pages concatenate.**
///
/// The property `\more` is built on, checked here before there is a shell to check it
/// in. The result is long enough to cross the server's chunk boundary several times —
/// so between pages the server is parked mid-result holding a bytes-only cursor, with
/// its snapshot already released ([I8](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i8)) — and the
/// concatenation of the pages must equal an uninterrupted run of the same query, which
/// is [I4](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i4) seen from a client.
#[test]
fn a_paged_read_equals_an_uninterrupted_one() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    // Well past the server's 256-row chunk, and not a multiple of it or of the page
    // size below: a paging bug that only shows up on an unaligned tail is exactly the
    // kind this is for.
    seed(&mut connection, 1000);

    let query = "F where src.File F";

    let mut whole = connection.query(query).expect("it compiles");
    let uninterrupted = strings(&connection.drain(&mut whole).expect("every row"));
    assert_eq!(uninterrupted.len(), 1000);

    let mut paged = connection.query(query).expect("it compiles");
    let mut pages = vec![];

    loop {
        let page = connection.take(&mut paged, 37).expect("a page");
        if page.is_empty() {
            break;
        }
        assert!(page.len() <= 37);
        pages.push(strings(&page));
    }

    // 1000 = 27 pages of 37 and one of 1, so the last page is short — which is the
    // case a `take` that read a fixed count would hang on.
    assert_eq!(pages.len(), 28);
    assert_eq!(pages.last().map(Vec::len), Some(1));

    let concatenated: Vec<String> = pages.concat();
    assert_eq!(
        concatenated, uninterrupted,
        "the pages are the uninterrupted run, in order and without repeats"
    );

    assert!(paged.finished());
    assert_eq!(paged.sent(), 1000);
}

/// **Two results open at once.** The second query is issued while the first is parked
/// mid-result, and neither loses a row — which is only true because frames for a
/// stream nobody is reading are *parked* rather than dropped.
#[test]
fn two_results_are_open_at_once() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 400);

    let mut first = connection.query("F where src.File F").expect("it compiles");
    let opening = strings(&connection.take(&mut first, 10).expect("a page"));
    assert_eq!(opening.len(), 10);

    // A second query, started while the first is still open and its rows are still
    // arriving on the socket.
    // A prefix constraint, which the level binding `F` applies as a seek rather than
    // as a filter — so this is a short answer by construction, not a long one filtered.
    let mut second = connection
        .query("F where src.File F; F = \"f00042\"..")
        .expect("it compiles");
    let narrow = strings(&connection.drain(&mut second).expect("its rows"));
    assert_eq!(narrow, ["f00042.py"]);

    // ...and the first carries on exactly where it stopped.
    let rest = strings(&connection.drain(&mut first).expect("the rest"));
    assert_eq!(rest.len(), 390);
    assert_eq!(first.sent(), 400);

    let mut all = opening;
    all.extend(rest);
    all.sort();
    all.dedup();
    assert_eq!(all.len(), 400, "no row was lost or duplicated");
}

/// A cancel is an **early end, not a failure**: the stream completes with what it
/// sent, the client is not owed an error, and the connection keeps answering.
#[test]
fn a_cancel_ends_one_result_and_leaves_the_connection_working() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 1000);

    let mut rows = connection.query("F where src.File F").expect("it compiles");
    let page = connection.take(&mut rows, 5).expect("a page");
    assert_eq!(page.len(), 5);

    let sent = connection.cancel(&mut rows).expect("it cancels");
    assert!(sent >= 5, "the server sent at least what we read: {sent}");
    assert!(rows.finished());

    // The connection is untouched: a stream ended, not a session.
    let mut again = connection
        .query("F where src.File F; F = \"f00007\"..")
        .expect("it compiles");
    assert_eq!(
        strings(&connection.drain(&mut again).expect("its rows")),
        ["f00007.py"]
    );

    // Cancelling a finished result is a no-op rather than a second cancel on a stream
    // the server has already closed.
    assert_eq!(connection.cancel(&mut rows).expect("a no-op"), sent);
}

/// A virtual query announces its listing immediately after the row description.
/// Cancelling before the first row must consume that transparent frame just as
/// ordinary row pulling does, rather than mistake it for the query's answer.
#[test]
fn cancelling_a_virtual_query_before_its_first_row_consumes_the_listing_digest() {
    let serving = start_with_catalogue();
    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query("X where X = fjord.db.List _")
        .expect("the virtual query opens");

    let sent = connection.cancel(&mut rows).expect("it cancels");
    assert!(
        sent <= 1,
        "the one-row listing sent at most its row: {sent}"
    );
    assert!(rows.finished());

    let mut again = connection
        .query("F where src.File F")
        .expect("the connection still works");
    assert!(connection.drain(&mut again).expect("it answers").is_empty());
}

/// **A cached virtual id does not outlive the listing it was a position in.** The fetch
/// digest cannot catch this on its own: `prefetch` asks only for ids it does not already
/// hold, so a cached catalogue row is never re-fetched and its digest never re-checked.
/// Left unrepaired, the second expansion below names the database the *first* listing had
/// at that position.
#[test]
fn a_cached_virtual_id_does_not_survive_a_relisting() {
    let serving = start_with_catalogue();
    let mut control = serving.control();
    let source = fjord_schema::syntax::print::print(&schema());
    let mut connection = serving.open(Mode::ReadOnly);
    // The *served* schema, which is what declares the catalogue and marks it virtual.
    // The handshake schema deliberately excludes it, so an expander built from that one
    // would neither decode a listing row nor know it held a virtual id.
    let served = Arc::new(connection.served_schema().expect("the served schema"));
    let mut expander = Expander::new(served);

    let name_of = |value: &WireValue| -> String {
        let WireValue::Record(fields) = &nested(value).key else {
            panic!("a listing row's key is a record: {value:?}");
        };
        let WireValue::Str(name) = &fields[0] else {
            panic!("a listing row's first field is its name: {fields:?}");
        };
        name.clone()
    };

    let mut rows = connection
        .query("X where X = fjord.db.List _")
        .expect("the virtual query opens");
    let listed = connection.drain(&mut rows).expect("the rows arrive");
    assert_eq!(listed.len(), 1);

    let first = expander
        .expand(
            &mut connection,
            &listed[0],
            FULL_DEPTH,
            rows.listing_digests(),
        )
        .expect("the virtual id resolves");
    assert_eq!(name_of(&first), "code");

    // A database that sorts *before* the one already there, so the row this id names
    // changes rather than merely moving down the page.
    control.create("aaa", &source).expect("a second database");

    let mut relisted = connection
        .query("X where X = fjord.db.List _")
        .expect("the virtual query opens again");
    let after = connection.drain(&mut relisted).expect("the rows arrive");
    assert_eq!(after.len(), 2);

    let second = expander
        .expand(
            &mut connection,
            &after[0],
            FULL_DEPTH,
            relisted.listing_digests(),
        )
        .expect("the virtual id resolves");
    assert_eq!(
        name_of(&second),
        "aaa",
        "a cached virtual id answered for the listing it was minted in"
    );

    // The digests really did move, so the assertion above is not passing because
    // nothing changed.
    assert_ne!(
        rows.listing_digests(),
        relisted.listing_digests(),
        "the listing digest did not move; this test proves nothing"
    );
}

/// A digest scopes caching, not whether the fetch result is useful for the current
/// expansion. The server deliberately resolves a digestless virtual id typed by hand.
#[test]
fn a_digestless_virtual_fetch_expands_for_the_current_row() {
    let serving = start_with_catalogue();
    let mut control = serving.control();
    let source = fjord_schema::syntax::print::print(&schema());
    let mut connection = serving.open(Mode::ReadOnly);
    let served = Arc::new(connection.served_schema().expect("the served schema"));
    let mut expander = Expander::new(served);

    let mut rows = connection
        .query("X where X = fjord.db.List _")
        .expect("the virtual query opens");
    let listed = connection.drain(&mut rows).expect("the row arrives");
    assert_eq!(listed.len(), 1);

    let expanded = expander
        .expand(&mut connection, &listed[0], FULL_DEPTH, &[])
        .expect("a digestless fetch still resolves now");

    let listing_name = |value: &WireValue| -> String {
        let WireValue::Ref(WireRef::Nested(fact)) = value else {
            panic!("the listing id was not expanded: {value:?}");
        };
        let WireValue::Record(fields) = &fact.key else {
            panic!("a listing key is a record: {:?}", fact.key);
        };
        let WireValue::Str(name) = &fields[0] else {
            panic!("a listing name is a string: {fields:?}");
        };
        name.clone()
    };
    assert_eq!(listing_name(&expanded), "code");

    control.create("aaa", &source).expect("a second database");
    let current = expander
        .expand(&mut connection, &listed[0], FULL_DEPTH, &[])
        .expect("the same id resolves without using the previous row");
    assert_eq!(listing_name(&current), "aaa");
}

/// The repair above rests on the recovered schema knowing which predicates are virtual,
/// and the printed form carries no marker for it. A `served_schema` that stopped marking
/// them would leave every guard above green while the cache went stale again.
#[test]
fn a_served_schema_marks_the_catalogue_virtual() {
    let serving = start_with_catalogue();
    let mut connection = serving.open(Mode::ReadOnly);
    let served = connection.served_schema().expect("the served schema");

    let (listing, _) = served
        .find_position("fjord.db.List")
        .expect("the catalogue is served");
    assert!(served.is_virtual(listing));

    let (stored, _) = served
        .find_position("src.File")
        .expect("the database's own predicates are served too");
    assert!(
        !served.is_virtual(stored),
        "a stored predicate was marked virtual"
    );
}

/// A discard is **not** a cancel: the query runs to its end and the server's own count
/// is what comes back, with no row ever decoded on this side.
#[test]
fn a_discard_runs_the_result_out_and_leaves_the_connection_working() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 1000);

    let mut rows = connection.query("F where src.File F").expect("it compiles");
    let sent = connection.discard(&mut rows).expect("it runs out");

    assert_eq!(sent, 1000, "the whole result, not the first chunk");
    assert_eq!(rows.seen(), 1000, "every row arrived here and was dropped");
    assert!(rows.finished());

    // A stream ended, not a session — and the id it held is free again, which is the
    // half a load generator depends on: a connection it reuses for hours.
    let mut again = connection
        .query("F where src.File F; F = \"f00007\"..")
        .expect("it compiles");
    assert_eq!(
        strings(&connection.drain(&mut again).expect("its rows")),
        ["f00007.py"]
    );

    assert_eq!(connection.discard(&mut rows).expect("a no-op"), sent);
}

/// Peak-live allocation stays flat if every row is decoded and immediately dropped.
/// A payload that cannot decode is the witness that `discard` never takes that path.
#[test]
fn a_discard_does_not_decode_data_rows() {
    use fjord_wire::{
        desc::{Desc, encode_desc},
        frame::FrameKind,
        protocol,
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("discard.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");
        ready_fake_server(&mut stream);

        let (query, _) = read_frame(&mut stream);
        assert_eq!(query.kind, protocol::kinds::QUERY);

        let mut desc = vec![];
        encode_desc(&mut desc, &Desc::Str);
        send_frame(&mut stream, FrameKind::ROW_DESCRIPTION, query.stream, &desc);

        // Empty is not an encoded string, but it is a valid frame payload. A discard
        // counts the frame without interpreting the value inside it.
        send_frame(&mut stream, FrameKind::DATA_ROW, query.stream, &[]);
        send_frame(
            &mut stream,
            protocol::kinds::COMPLETE,
            query.stream,
            &protocol::encode_complete(1, 0),
        );
    });

    let mut connection =
        Connection::connect(&socket, "code", Arc::new(schema()), Mode::ReadOnly, false)
            .expect("the handshake completes");
    let mut rows = connection
        .query("F where src.File F")
        .expect("the result opens");

    assert_eq!(connection.discard(&mut rows).expect("no row is decoded"), 1);
    assert_eq!(rows.seen(), 1);
    assert!(rows.finished());

    server.join().expect("the fake server exits cleanly");
}

/// **The claim `discard` is for.** A result held in memory costs the client its length;
/// one run out costs a frame at a time, and a load generator that paid the first price
/// would be measuring itself rather than the server.
///
/// Peak live bytes rather than the total: the total is dominated by the frames read off
/// the socket either way, and it is the *retention* the two calls disagree about.
#[test]
fn discarding_is_flat_in_the_length_of_the_result() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 4000);

    let peak = |connection: &mut Connection, sigla: &str, drain: bool| -> u64 {
        let mut rows = connection.query(sigla).expect("it compiles");
        allocation_counter::measure(|| {
            if drain {
                connection.drain(&mut rows).expect("its rows");
            } else {
                connection.discard(&mut rows).expect("it runs out");
            }
        })
        .bytes_max
    };

    const ONE: &str = "F where src.File F; F = \"f00007.py\"";
    const ALL: &str = "F where src.File F";

    let (drained_one, drained_all) = (
        peak(&mut connection, ONE, true),
        peak(&mut connection, ALL, true),
    );
    let (discarded_one, discarded_all) = (
        peak(&mut connection, ONE, false),
        peak(&mut connection, ALL, false),
    );

    assert!(
        drained_all > drained_one * 10,
        "a drained result is held, so four thousand rows cost proportionally more: \
         {drained_one} → {drained_all}"
    );
    assert!(
        discarded_all < discarded_one * 2,
        "a discarded result is not held, so its peak does not follow the row count: \
         {discarded_one} → {discarded_all}"
    );
}

/// A query that does not compile fails its **stream**, carrying the compiler's own
/// diagnostics, and the connection is usable afterwards.
#[test]
fn a_bad_query_fails_its_stream_by_code() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadOnly);

    let error = connection.query("this is not sigla").expect_err("it fails");

    assert_eq!(error.code(), Some(ErrorCode::BadQuery));
    assert!(
        error.to_string().contains("invalid syntax"),
        "the compiler's own words: {error}"
    );

    let mut rows = connection.query("F where src.File F").expect("it compiles");
    assert!(connection.drain(&mut rows).expect("no rows").is_empty());
    assert_eq!(
        connection.stream_ids_issued(),
        1,
        "the query after the terminal refusal must reuse its stream id"
    );
}

/// The lifecycle, through the client: create a database, seal it, and find that
/// `ops-I2` refuses a write session to it afterwards.
#[test]
fn the_lifecycle_runs_through_the_client() {
    let serving = start();
    let mut control = serving.control();

    // The source, because `create` requires one: a database embeds the schema it was
    // built against, and there is no longer a server-side default standing in for a
    // caller who did not name one.
    let source = fjord_schema::syntax::print::print(&schema());
    let instance = control.create("fresh", &source).expect("it is created");
    assert!(!instance.is_empty());

    // Immediately usable, without the server being restarted.
    let mut writer = Connection::connect(
        &serving.socket,
        "fresh",
        Arc::new(schema()),
        Mode::ReadWrite,
        true,
    )
    .expect("a session on the new database");

    seed(&mut writer, 3);
    drop(writer);

    let sealed = control.finish("fresh", false).expect("it seals");
    assert_eq!(sealed.facts, 3);
    assert!(sealed.fingerprint != 0);
    assert!(!sealed.already_complete);

    // `ops-I2`, from a client's side: the refusal is at establishment.
    let refused = Connection::connect(
        &serving.socket,
        "fresh",
        Arc::new(schema()),
        Mode::ReadWrite,
        true,
    )
    .expect_err("a sealed database takes no writer");

    assert_eq!(refused.code(), Some(ErrorCode::ModeRefused));

    // ...and reading it still works.
    let mut reader = Connection::connect(
        &serving.socket,
        "fresh",
        Arc::new(schema()),
        Mode::ReadOnly,
        true,
    )
    .expect("a reader");

    let mut rows = reader.query("F where src.File F").expect("it compiles");
    assert_eq!(reader.drain(&mut rows).expect("its rows").len(), 3);
    drop(rows);
    drop(reader);

    // **Dropping a connection and the server noticing are not the same event.** The
    // socket closes here; the server learns when its read loop sees EOF, tears the
    // session down and releases the database — which is asynchronous with this line
    // by construction. `remove` refuses a database an open session holds
    // (`ops-I1`), so a `remove` issued in the same breath as a `drop` can lose the
    // race and be told the database is in use.
    //
    // That is the product behaving correctly and a caller retrying, so the test does
    // what a caller does rather than asserting a timing it cannot control. It was a
    // flake before it was a comment: it fired once in a run that added four tests.
    remove_when_released(&mut control, "fresh");

    let gone = Connection::connect(
        &serving.socket,
        "fresh",
        Arc::new(schema()),
        Mode::ReadOnly,
        false,
    )
    .expect_err("it is gone");

    assert_eq!(gone.code(), Some(ErrorCode::UnknownDatabase));
}

/// **A schema that disagrees is refused at the handshake**, before a byte of data
/// flows — which is the whole reason the fingerprint is sent as a claim rather than
/// asked for as a question.
///
/// *Disagrees*, not *differs*: a client declaring fewer predicates than the server has
/// is checked by containment and let in ([I13](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i13), and
/// `i13_embedded_schema.rs` for the whole rule). What is refused here is a client whose
/// `src.File` is a different `src.File`.
#[test]
fn a_schema_that_disagrees_is_refused_before_any_data() {
    let serving = start();

    /// One predicate, `src.File`, keyed as `key` says. The server's is a string.
    fn one(key: PredicateTy) -> Schema {
        let mut rodeo = Rodeo::new();
        let file = rodeo.get_or_intern("src.File");

        Schema::new(
            rodeo.into_reader(),
            Arc::from(vec![Predicate {
                name: file,
                key,
                value: None,
            }]),
        )
    }

    let refused = Connection::connect(
        &serving.socket,
        "code",
        Arc::new(one(PredicateTy::Int)),
        Mode::ReadWrite,
        true,
    )
    .expect_err("a string key is not an int key");

    assert_eq!(refused.code(), Some(ErrorCode::SchemaMismatch));
    assert!(
        refused.to_string().contains("src.File"),
        "the refusal names the predicate that disagrees: {refused}"
    );

    // ...and `false` is the reader's answer: nothing is claimed, so nothing is checked,
    // even by a client whose idea of `src.File` is wrong.
    Connection::connect(
        &serving.socket,
        "code",
        Arc::new(one(PredicateTy::Int)),
        Mode::ReadOnly,
        false,
    )
    .expect("a reader that asserts nothing is let in");
}

/// A bookmark from another connection is refused rather than read from, which would
/// be a wait for a frame nobody is going to send.
#[test]
fn a_bookmark_belongs_to_its_connection() {
    let serving = start();
    let mut one = serving.open(Mode::ReadWrite);
    let mut two = serving.open(Mode::ReadOnly);

    seed(&mut one, 10);

    let mut rows = one.query("F where src.File F").expect("it compiles");
    assert_eq!(one.take(&mut rows, 2).expect("a page").len(), 2);

    let wrong = two.next_row(&mut rows).expect_err("not this connection's");
    assert!(matches!(wrong, ClientError::Protocol(_)), "{wrong}");

    // The bookmark still works where it belongs.
    assert_eq!(one.drain(&mut rows).expect("the rest").len(), 8);
}

/// **A profile is the outcome to a plan's intent.** What makes it worth carrying is
/// the gap between examined and produced: a residual that rejects almost everything
/// is invisible in a row count and obvious here.
#[test]
fn a_profile_reports_what_the_query_examined() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 500);

    // A constant bind, which folds — so `src.File F` becomes an exact key seek.
    let mut scan = connection
        .query_profiled("F where src.File F; F = \"f00042.py\"")
        .expect("it compiles");

    let rows = connection.drain(&mut scan).expect("its rows");
    let profile = scan.profile().expect("a profile arrived");

    assert_eq!(rows.len(), 1);
    assert_eq!(profile.steps.len(), 1, "one level, one step");
    assert_eq!(profile.steps[0].label, "src.File");

    // A constant bind **folds**, so this is a seek rather than a scan with a filter —
    // and the number is how you can tell without reading the plan.
    assert_eq!(profile.examined(), 1, "the index answered it");
    assert!(!profile.steps[0].full_scan);

    // ...against a scan of the same predicate, which reads all five hundred.
    let mut whole = connection
        .query_profiled("F where src.File F")
        .expect("it compiles");
    let rows = connection.drain(&mut whole).expect("its rows");
    let profile = whole.profile().expect("a profile arrived");

    assert_eq!(rows.len(), 500);
    assert_eq!(profile.examined(), 500);
    assert!(profile.steps[0].full_scan, "it read the predicate whole");
}

/// A profile survives **chunking**: the result is long enough to cross the server's
/// 256-row boundary several times, so the tally has to accumulate across real resumes
/// rather than describing the last page.
#[test]
fn a_profile_accumulates_across_the_chunks_it_took() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 1000);

    let mut rows = connection
        .query_profiled("F where src.File F")
        .expect("it compiles");

    // Read in pages, so the server genuinely parks and resumes between them.
    let mut seen = 0;
    loop {
        let page = connection.take(&mut rows, 37).expect("a page");
        if page.is_empty() {
            break;
        }
        seen += page.len();
    }

    assert_eq!(seen, 1000);
    assert_eq!(
        rows.profile().expect("a profile arrived").examined(),
        1000,
        "the whole run's work, not the last page's, and not the replay's"
    );
}

/// A query that did not ask for a profile does not get one — which is what makes the
/// frame additive, and is the property the .NET client depends on without knowing it.
#[test]
fn an_unprofiled_query_gets_no_profile_frame() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 10);

    let mut rows = connection.query("F where src.File F").expect("it compiles");
    assert_eq!(connection.drain(&mut rows).expect("its rows").len(), 10);
    assert!(rows.profile().is_none());
}

/// **A connection that has answered a thousand queries holds no more than one that has
/// answered one.**
///
/// The regression guard for `bench/FINDINGS.md` §7: a stream's task waiting forever on
/// a channel whose only `Sender` lives in a map with no removal path leaves a parked
/// task per query — ~3.5 kB retained, for the life of the connection. A pooled
/// connection is exactly the shape that reaches it, and a web tier is a pool by
/// construction.
///
/// Two claims, and they are different halves of the same fix. The server's is that the
/// task **ends** — `streams_live` is the gauge that was already counting them and that
/// nothing was allowed to decrement. The client's is that it stops **inventing** ids, or
/// the server's map grows with the query count even once every task in it is dead.
///
/// Polled rather than slept on: the task ends immediately once it may, so a fixed sleep
/// would be either flaky or slow.
#[test]
fn a_long_lived_connection_does_not_accumulate_streams() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 4);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    const QUERIES: usize = 200;
    for _ in 0..QUERIES {
        let mut rows = connection.query("F where src.File F").expect("a query");
        let all = connection.drain(&mut rows).expect("the rows");
        assert_eq!(all.len(), 4, "the query is the same every time");
    }

    // The client's half: four concurrent streams were never open, so four ids were never
    // needed. One is enough, and the writer above used one of its own before it closed.
    assert!(
        connection.stream_ids_issued() <= 2,
        "the client invented {} stream ids for {QUERIES} sequential queries — it is not \
         recycling them",
        connection.stream_ids_issued()
    );

    // The server's half. The connection is still open, which is the whole point: this is
    // not "they go when you hang up", it is "they go when the work is done".
    let stats = Arc::clone(serving.registry.stats());
    let settled = within(std::time::Duration::from_secs(5), || {
        stats.streams_live() == 0
    });

    assert!(
        settled,
        "{} stream tasks are still live after {QUERIES} finished queries on an open \
         connection",
        stats.streams_live()
    );

    // The control: the gauge is capable of being non-zero, so a zero above is the tasks
    // ending rather than the counter never having counted.
    assert!(
        stats.queries_completed() >= QUERIES as u64,
        "the queries did not run"
    );

    // And the connection still works, which is what says the streams ended rather than
    // broke.
    let mut rows = connection.query("F where src.File F").expect("a query");
    assert_eq!(connection.drain(&mut rows).expect("the rows").len(), 4);
}

/// A **write** stream spans frames by definition, and must not be ended between them.
///
/// The rule that ends a finished stream is "`handle` returned and this is not a write in
/// progress". Getting that wrong in the other direction would end the stream at
/// `OPEN_WRITE` and lose every block after it — which no other test here would notice,
/// because they all write in one call.
#[test]
fn a_write_stream_survives_between_its_frames() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    // Two blocks on one stream, so the second arrives after `copy_data` has already
    // returned once.
    let first: Vec<WireFact> = (0..3).map(|n| file(&format!("a{n}.py"))).collect();
    let second: Vec<WireFact> = (0..3).map(|n| file(&format!("b{n}.py"))).collect();

    let written = connection
        .write_blocks(&[(FILE, &first), (FILE, &second)])
        .expect("both blocks are written");

    assert_eq!(written.created, 6, "both blocks landed");

    let mut rows = connection.query("F where src.File F").expect("a query");
    assert_eq!(connection.drain(&mut rows).expect("the rows").len(), 6);
}

/// Wait for `f` to hold, or give up.
fn within(limit: std::time::Duration, mut f: impl FnMut() -> bool) -> bool {
    let deadline = std::time::Instant::now() + limit;
    while std::time::Instant::now() < deadline {
        if f() {
            return true;
        }
        thread::sleep(std::time::Duration::from_millis(20));
    }
    f()
}

/// **Paging across separate connections answers the whole result, in order.**
///
/// The claim `Rows::resume_token` exists for. An ordinary query streams its result on
/// one stream, so page two needs the connection that asked for page one — and there is
/// no workaround in the language, because "everything after key K" cannot be written.
///
/// So this asks for every page on a **new connection**, closing the last one first,
/// which is the shape a stateless web tier has and the shape nothing here could take
/// before. The concatenated pages must equal the uninterrupted result exactly: same
/// rows, same order, no gap and no repeat — [I4](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i4)
/// carried through a token rather than through a session.
#[test]
fn pages_taken_on_separate_connections_equal_one_result() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 25);
    drop(writer);

    const QUERY: &str = "F where src.File F";

    let whole = {
        let mut connection = serving.open(Mode::ReadOnly);
        let mut rows = connection.query(QUERY).expect("a query");
        strings(&connection.drain(&mut rows).expect("the rows"))
    };
    assert_eq!(whole.len(), 25, "the corpus is what the test thinks it is");

    let mut paged: Vec<String> = vec![];
    let mut token: Option<Vec<u8>> = None;
    let mut pages = 0usize;

    loop {
        // A fresh connection every page, and the previous one is gone — the point is
        // that nothing about the result lives in a session.
        let mut connection = serving.open(Mode::ReadOnly);

        let mut rows = connection
            .query_page(QUERY, 7, token.as_deref())
            .expect("a page");

        paged.extend(strings(&connection.drain(&mut rows).expect("the page")));
        pages += 1;

        token = rows.resume_token().map(<[u8]>::to_vec);
        drop(connection);

        if token.is_none() {
            break;
        }

        assert!(pages < 10, "paging did not terminate");
    }

    assert_eq!(paged, whole, "the pages are the result, in order");
    assert_eq!(
        pages, 4,
        "25 rows at 7 a page is four pages, the last one short"
    );
}

/// A page smaller than a chunk does not overshoot it.
///
/// The server computes rows a chunk at a time, and a page limit under `CHUNK_ROWS`
/// has to cut the chunk rather than the frames: rows past the limit would otherwise be
/// computed, encoded and dropped, and the token would name a position the caller was
/// never told about — a silent gap in the result.
#[test]
fn a_page_smaller_than_a_chunk_stops_at_the_limit() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 400);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("F where src.File F", 3, None)
        .expect("a page");

    let first = strings(&connection.drain(&mut rows).expect("the page"));
    assert_eq!(first.len(), 3, "the page is the size asked for");

    let token = rows.resume_token().expect("there is more").to_vec();

    let mut rows = connection
        .query_page("F where src.File F", 3, Some(&token))
        .expect("the next page");

    let second = strings(&connection.drain(&mut rows).expect("the page"));
    assert_eq!(second.len(), 3);

    // The join has to be seamless: no row dropped between the pages, and none repeated.
    let mut whole = connection.query("F where src.File F").expect("a query");
    let all = strings(&connection.drain(&mut whole).expect("the rows"));

    assert_eq!(&all[..6], &[first, second].concat()[..]);
}

/// A token from **another query** is refused rather than answered.
///
/// A cursor is checked against the plan that built it — entries are paired with levels
/// by order, so two same-shaped plans over different predicates would otherwise accept
/// each other's tokens and answer from the wrong rows. Over the wire that check is the
/// only thing standing between a caller and a plausible wrong answer, because the
/// token is bytes the caller could have kept from anything.
#[test]
fn a_resume_token_belongs_to_the_query_that_made_it() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 20);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("F where src.File F", 2, None)
        .expect("a page");
    let _ = connection.drain(&mut rows).expect("the page");
    let token = rows.resume_token().expect("there is more").to_vec();

    // The same shape over a different predicate: one level, one projection.
    let refused = connection.query_page("D where src.Decl D", 2, Some(&token));

    assert!(
        refused.is_err(),
        "a token from another plan was accepted: {:?}",
        refused.map(|_| ())
    );

    // And the connection still works, which says the refusal ended the stream rather
    // than the session.
    let mut rows = connection
        .query_page("F where src.File F", 2, Some(&token))
        .expect("the right query still resumes");
    assert_eq!(connection.drain(&mut rows).expect("the page").len(), 2);
}

/// **A write that lands between two pages of a Writable database is refused,
/// rather than answered as a hybrid of the two states the read passed through.**
///
/// A cursor names a plan, a layout version and — since the world stamp landed —
/// which base it was read against. On a Complete database that base can never
/// move, so nothing here would fire; a Writable one is read through a fresh
/// snapshot every chunk, so an ingest between two `query_page` calls is exactly
/// the case [I4](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i4)
/// names: "a database still being written to... the cursor carries nothing that
/// would detect [it]". This is the server-level arm that closes it, and it has to
/// be server-level — a generated `(plan, store)` pair in the engine's own battery
/// holds one store for the whole property and cannot express a store that changes
/// mid-resume.
#[test]
fn a_write_between_two_pages_of_a_writable_database_is_refused() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 20);

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("F where src.File F", 5, None)
        .expect("the first page");
    let first = strings(&connection.drain(&mut rows).expect("the page"));
    assert_eq!(first.len(), 5);
    let token = rows.resume_token().expect("there is more").to_vec();

    // The database is still Writable — nothing here finishes it — so the write
    // below lands squarely inside the case this test exists to cover. A new name,
    // not `seed`'s own `f00000.py`: seeding again would dedup against the fact
    // already written and move nothing.
    let written = writer
        .write(FILE, &[file("between-the-pages.py")])
        .expect("the extra fact is written");
    assert_eq!(written.created, 1, "the write must actually create a fact");

    // `query_page` itself only sends the request and reads the row description,
    // which is sent before the resumed chunk ever runs — so the refusal, like any
    // other mid-stream error, surfaces on the read that follows rather than here.
    let mut second_page = connection
        .query_page("F where src.File F", 5, Some(&token))
        .expect("the row description arrives before the resumed chunk runs");
    let refused = connection.drain(&mut second_page);
    assert!(
        refused.is_err(),
        "a write between two pages of a Writable database was answered rather than refused: {:?}",
        refused.map(|rows| rows.len())
    );

    // The refusal ended the stream, not the connection: a fresh page still works,
    // and — since it is unpaged from here — sees all 21 rows, never a duplicate or
    // a gap from the page that was refused.
    let mut whole = connection
        .query("F where src.File F")
        .expect("the connection still works");
    assert_eq!(connection.drain(&mut whole).expect("the rows").len(), 21);
}

/// **The negative control for the test above**: paging a Writable database with no
/// intervening write behaves exactly as it always has. Without this, a bug that
/// made the world stamp refuse *every* Writable resume — not only one a write
/// crossed — would still pass every other test in this file, since none of them
/// distinguish "always refused" from "refused when it should be".
#[test]
fn paging_a_writable_database_with_no_intervening_write_still_works() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 20);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("F where src.File F", 5, None)
        .expect("the first page");
    let first = strings(&connection.drain(&mut rows).expect("the page"));
    assert_eq!(first.len(), 5);
    let token = rows.resume_token().expect("there is more").to_vec();

    let mut rows = connection
        .query_page("F where src.File F", 5, Some(&token))
        .expect("the second page, over an unchanged Writable database");
    let second = strings(&connection.drain(&mut rows).expect("the page"));
    assert_eq!(second.len(), 5);

    let mut whole = connection.query("F where src.File F").expect("a query");
    let all = strings(&connection.drain(&mut whole).expect("the rows"));
    assert_eq!(&all[..10], &[first, second].concat()[..]);
}

/// **A database created between two pages of `fjord.db.List` renumbers the listing
/// under a cursor that is a *position* in it — item 12's defect, closed the same way
/// item 13 closes the base-database one above.** `fjord.db.List`'s rows are a view
/// materialised per request, not a keyspace: `query_page` re-prepares on every call,
/// so a `create` between two calls changes what the listing *is* while the cursor
/// still names the same plan and the same (Writable, in this test unwritten-to) base.
/// Nothing but the listing's own digest, folded into the world stamp beside the base
/// identity, can catch that — which is exactly what this proves happens.
#[test]
fn a_database_created_between_two_pages_of_a_listing_is_refused() {
    let serving = start_with_catalogue();
    let mut control = serving.control();
    let source = fjord_schema::syntax::print::print(&schema());

    // Two databases already, so a page of one leaves a second row for the resumed
    // page to find — otherwise the first page would already exhaust the listing and
    // there would be nothing left to disagree about.
    control
        .create("second", &source)
        .expect("a second database");

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("X where X = fjord.db.List _", 1, None)
        .expect("the first page");
    let first = connection.drain(&mut rows).expect("the page");
    assert_eq!(first.len(), 1);
    let token = rows.resume_token().expect("there is more").to_vec();

    control.create("third", &source).expect("a third database");

    // `query_page` itself only sends the request and reads the row description and
    // the listing digest — both sent before the resumed chunk ever runs — so, exactly
    // as the Writable-write case above, the refusal surfaces on the read that follows
    // rather than here.
    let mut second_page = connection
        .query_page("X where X = fjord.db.List _", 1, Some(&token))
        .expect("the row description arrives before the resumed chunk runs");
    let refused = connection.drain(&mut second_page);
    assert!(
        refused.is_err(),
        "a database created between two pages of a listing was answered rather than \
         refused: {:?}",
        refused.map(|rows| rows.len())
    );

    // The refusal ended the stream, not the connection: a fresh, unpaged query still
    // works and sees all three databases.
    let mut whole = connection
        .query("X where X = fjord.db.List _")
        .expect("the connection still works");
    assert_eq!(connection.drain(&mut whole).expect("the rows").len(), 3);
}

/// The other direction of the case above: a `rm` between two pages moves the listing
/// exactly as a `create` does, and must be caught the same way.
#[test]
fn a_database_removed_between_two_pages_of_a_listing_is_refused() {
    let serving = start_with_catalogue();
    let mut control = serving.control();
    let source = fjord_schema::syntax::print::print(&schema());

    control
        .create("second", &source)
        .expect("a second database");
    control.create("third", &source).expect("a third database");

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("X where X = fjord.db.List _", 1, None)
        .expect("the first page");
    let first = connection.drain(&mut rows).expect("the page");
    assert_eq!(first.len(), 1);
    let token = rows.resume_token().expect("there is more").to_vec();

    remove_when_released(&mut control, "third");

    let mut second_page = connection
        .query_page("X where X = fjord.db.List _", 1, Some(&token))
        .expect("the row description arrives before the resumed chunk runs");
    let refused = connection.drain(&mut second_page);
    assert!(
        refused.is_err(),
        "a database removed between two pages of a listing was answered rather than \
         refused: {:?}",
        refused.map(|rows| rows.len())
    );

    let mut whole = connection
        .query("X where X = fjord.db.List _")
        .expect("the connection still works");
    assert_eq!(connection.drain(&mut whole).expect("the rows").len(), 2);
}

/// **The negative control for the pair above**: paging a listing with no intervening
/// `create` or `rm` behaves exactly as it always has. Without this, a bug that made
/// the listing's digest refuse *every* resume over a virtual predicate — not only one
/// a mutation crossed — would still pass both tests above, since neither distinguishes
/// "always refused" from "refused when it should be".
#[test]
fn paging_a_listing_with_no_intervening_change_still_works() {
    let serving = start_with_catalogue();
    let mut control = serving.control();
    let source = fjord_schema::syntax::print::print(&schema());

    control
        .create("second", &source)
        .expect("a second database");

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("X where X = fjord.db.List _", 1, None)
        .expect("the first page");
    let first = connection.drain(&mut rows).expect("the page");
    assert_eq!(first.len(), 1);
    let token = rows.resume_token().expect("there is more").to_vec();

    let mut rows = connection
        .query_page("X where X = fjord.db.List _", 1, Some(&token))
        .expect("the second page, over an unchanged listing");
    let second = connection.drain(&mut rows).expect("the page");
    assert_eq!(second.len(), 1);

    let mut whole = connection
        .query("X where X = fjord.db.List _")
        .expect("a query");
    assert_eq!(connection.drain(&mut whole).expect("the rows").len(), 2);
}

/// **`fjord.db.Interning` has no snapshot to number, so a resume that crosses
/// requests is refused by name rather than validated against a digest that would
/// always disagree.** The counters are read by taking every interning stripe's lock
/// in turn — not a point-in-time capture even as it happens, and thrashing on every
/// write — so unlike `fjord.db.List` there is no stable value a generation could
/// name. This is not a race to provoke: the refusal fires on any attempt to resume
/// such a query across two requests, whether or not the counters actually moved.
#[test]
fn resuming_a_query_over_the_interning_counters_is_refused_by_name() {
    let serving = start_with_catalogue();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 1);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    let mut rows = connection
        .query_page("X where X = fjord.db.Interning _", 1, None)
        .expect("the first page");
    let _ = connection.drain(&mut rows).expect("the page");
    let token = rows
        .resume_token()
        .expect("a token, whether or not more rows remain")
        .to_vec();

    let refused = connection.query_page("X where X = fjord.db.Interning _", 1, Some(&token));
    assert!(
        refused.is_err(),
        "a resume across requests over fjord.db.Interning was accepted: {:?}",
        refused.map(|_| ())
    );
    let error = refused.expect_err("checked above");
    assert_eq!(error.code(), Some(ErrorCode::Refused));
    assert!(
        error.to_string().contains("fjord.db.Interning"),
        "the refusal names the predicate: {error}"
    );

    // The refusal ended the stream, not the connection.
    let mut whole = connection
        .query("F where src.File F")
        .expect("the connection still works");
    assert_eq!(connection.drain(&mut whole).expect("its rows").len(), 1);
}

/// Garbage is refused rather than half-read.
#[test]
fn a_malformed_resume_token_is_refused() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 5);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    let refused = connection.query_page("F where src.File F", 2, Some(&[1, 2, 3]));
    assert!(
        refused.is_err(),
        "three arbitrary bytes were read as a cursor"
    );

    let mut rows = connection
        .query_page("F where src.File F", 2, None)
        .expect("the connection still works");
    assert_eq!(connection.drain(&mut rows).expect("the page").len(), 2);
}

/// **A count equals the number of rows, and does not receive them.**
///
/// The same plan and the same executor; what differs is the accumulator. So the
/// claim worth pinning is the boring one — that it agrees with counting the rows by
/// hand — over results on both sides of the server's chunk boundary, since counting
/// is chunked the same way answering is and an off-by-one at the seam is exactly the
/// mistake available here.
#[test]
fn a_count_agrees_with_the_rows() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 600);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    for (query, want) in [
        ("F where src.File F", 600u64),
        // A prefix that matches a tenth of them, so the count is not the whole
        // predicate and a plan that ignored the seek would be visible.
        ("F where src.File F; F = \"f0000\"..", 10),
        ("F where src.File F; F = \"nothing\"..", 0),
    ] {
        let counted = connection.count(query).expect("a count");

        let mut rows = connection.query(query).expect("a query");
        let received = connection.drain(&mut rows).expect("the rows").len() as u64;

        assert_eq!(counted, want, "{query}");
        assert_eq!(
            counted, received,
            "{query}: the count and the rows disagree"
        );
    }

    // 600 crosses the server's 256-row chunk boundary twice, which is the seam this
    // is here for: counting is chunked exactly as answering is, and an off-by-one at
    // a resume would show as a count short or long by a chunk. Said out loud so a
    // future edit to the corpus size cannot quietly remove the coverage.
    const ROWS: usize = 600;
    const CHUNK: usize = 256;
    const { assert!(ROWS > 2 * CHUNK, "the corpus spans more than two chunks") };
}

/// A count leaves the connection usable and recycles its stream id.
#[test]
fn a_count_ends_its_stream() {
    let serving = start();

    let mut writer = serving.open(Mode::ReadWrite);
    seed(&mut writer, 3);
    drop(writer);

    let mut connection = serving.open(Mode::ReadOnly);

    for _ in 0..50 {
        assert_eq!(connection.count("F where src.File F").expect("a count"), 3);
    }

    assert!(
        connection.stream_ids_issued() <= 2,
        "fifty counts invented {} stream ids",
        connection.stream_ids_issued()
    );
}

/// The server's examined ceiling reaches both wire paths as a terminal error, and
/// each path releases its stream id before the connection carries on.
#[test]
fn the_server_ceiling_stops_queries_and_counts_without_leaking_their_streams() {
    let serving = start_with_examined_ceiling(Some(3));
    let mut connection = serving.open(Mode::ReadWrite);
    seed(&mut connection, 5);

    let mut rows = connection
        .query("F where src.File F")
        .expect("the descriptor arrives before execution");
    let query_error = connection
        .next_row(&mut rows)
        .expect_err("the fourth examined row exceeds the ceiling");
    assert_eq!(query_error.code(), Some(ErrorCode::Internal));
    assert!(
        query_error
            .to_string()
            .contains("examined 4 rows, over this run's ceiling of 3"),
        "the executor's named refusal reaches the client: {query_error}"
    );
    assert!(rows.finished(), "the query error is terminal");

    let count_error = connection
        .count("F where src.File F")
        .expect_err("count uses the same ceiling");
    assert_eq!(count_error.code(), Some(ErrorCode::Internal));
    assert!(
        count_error
            .to_string()
            .contains("examined 4 rows, over this run's ceiling of 3"),
        "the count path carries the same refusal: {count_error}"
    );

    assert_eq!(
        connection
            .count("F where src.File F; F = \"f00000.py\"")
            .expect("a seek inside the ceiling still works"),
        1
    );
    assert_eq!(
        connection.stream_ids_issued(),
        1,
        "the write, failed query, failed count, and successful count are sequential and reuse one id"
    );
}

/// Remove a database once the server has let go of it.
///
/// See the call site: a client's `drop` and the server's session teardown are
/// separate events, so a `remove` in the same breath can be refused for a session
/// that is on its way out. Polled rather than slept on — the release is immediate
/// once it happens, and a fixed sleep would be either flaky or slow.
fn remove_when_released(control: &mut Connection, database: &str) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);

    loop {
        match control.remove(database) {
            Ok(()) => return,
            Err(error) if std::time::Instant::now() < deadline => {
                // Only the one refusal is worth waiting out; anything else is a
                // real failure and saying so now beats timing out on it.
                assert_eq!(
                    error.code(),
                    Some(ErrorCode::InUse),
                    "removing {database} failed for a reason waiting cannot fix"
                );
                thread::sleep(std::time::Duration::from_millis(10));
            }
            Err(error) => panic!("{database} was still in use after five seconds: {error}"),
        }
    }
}

/// **A cancel that lands *inside* a chunk is still a cancel**, not a failure.
///
/// The loop already says so where a cancel lands *between* chunks: "a cancel is an
/// early end, not a failure, and a client that asked for one is not owed an error".
/// Inside one it went the other way — the executor reports cancellation as an error,
/// because to the executor it is one, and that error was sent on the stream instead of
/// the `COMPLETE` the client was waiting for. Two things then go wrong at once: the
/// caller is handed an error for something it asked for, and the stream is left
/// un-drained with its id never returned.
///
/// It needs a *slow* chunk to be observable at all, which is why this query examines a
/// million rows to produce none: a cancel arriving between chunks takes the clean path
/// and proves nothing. On a small corpus every chunk is microseconds, which is why this
/// was invisible until a 25M-fact index made an ordinary shell session hit it every
/// time.
#[test]
fn a_cancel_inside_a_chunk_completes_rather_than_fails() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    seed(&mut connection, 1000);

    // A thousand files joined against a thousand files, with every inner row denied:
    // a million rows examined, none produced, and all of it inside the first chunk.
    // **A denial rather than a constraint**, and that is the whole of why this is slow
    // — `G = "zzzz".."` would be *captured* by the level that binds `G` and become a
    // seek that finds nothing immediately, where `!=` is never a seek and filters
    // however it is written (chapter 7). Every file the seed writes begins `f`, so this
    // rejects all thousand of them, a thousand times over.
    //
    // The descriptor arrives before any of that work, so the cancel below lands while
    // the executor is in the middle of it.
    let mut rows = connection
        .query("{a = F} where src.File F; src.File G; G != \"f\"..")
        .expect("a result");

    let sent = connection
        .cancel(&mut rows)
        .expect("a cancel is not a failure");

    assert_eq!(sent, 0, "it produced nothing before it was stopped");

    // And the connection is still usable — which it would not be if the stream had
    // been left waiting for a completion that was replaced by an error.
    let mut after = connection.query("F where src.File F").expect("a result");
    assert_eq!(
        connection.take(&mut after, 3).expect("a page").len(),
        3,
        "the session goes on"
    );
}

/// **Two connections write one database at the same time.**
///
/// A per-database mutex across every block would mean that however many clients are
/// writing, one is writing — a mutex there has the right to exactly one job, keeping a
/// block out of a database that has been sealed. Keeping writers out of *each other's*
/// way is the store's job, done per **key** by the striped merge frontier.
///
/// **The peak gauge rather than a stopwatch.** "They ran at the same time" is exactly the
/// kind of claim a timing test argues for and never settles — a slow CI box makes two
/// serialised writers look concurrent and a fast one makes two concurrent writers look
/// serialised. `intern_concurrency` counts threads inside interning, so a peak above one
/// is not evidence of parallelism, it *is* parallelism.
#[test]
fn two_connections_write_one_database_at_the_same_time() {
    use std::sync::Barrier;

    const PER_CONNECTION: usize = 4_000;

    let serving = start();
    let database = serving.registry.bind("code").expect("the database");

    // Both halves overlap in the middle, so the frontier has to decide contended keys
    // rather than two disjoint sets that would never meet.
    let facts = |from: usize| -> Vec<WireFact> {
        (from..from + PER_CONNECTION)
            .map(|n| file(&format!("shared{n:06}.py")))
            .collect()
    };

    let start_together = Barrier::new(2);
    thread::scope(|scope| {
        for from in [0, PER_CONNECTION / 2] {
            let (serving, barrier) = (&serving, &start_together);
            scope.spawn(move || {
                let mut connection = serving.open(Mode::ReadWrite);
                let batch = facts(from);
                barrier.wait();
                connection
                    .write(FILE, &batch)
                    .expect("the block is written");
            });
        }
    });

    let (_, peak) = database.db.intern_concurrency();
    assert!(
        peak >= 2,
        "the write path is still serialised: never more than {peak} writer interning at once"
    );

    // And it is correct, not merely concurrent: the overlap was written once.
    let expected = PER_CONNECTION + PER_CONNECTION / 2;
    let mut reader = serving.open(Mode::ReadOnly);
    let mut rows = reader.query("F where src.File F").expect("a result");
    assert_eq!(
        reader.take(&mut rows, expected * 2).expect("a page").len(),
        expected,
        "two writers overlapping by half must write the overlap once"
    );
}

/// **A conflict is still refused when the two writers are concurrent**, and refused to
/// exactly one of them.
///
/// `ops-I5`'s reject is the rule that survives parallelism, and it is the rule
/// [`ops-I4`](https://github.com/boxops-uk/fjord/blob/main/website/content/operations.md) actually needs: *which* producer is
/// told no may vary with the interleaving, but that one of them is told no may not. A
/// pick-one rule would make the database depend on a race; a reject makes the failure
/// depend on it, which is a different and acceptable thing.
#[test]
fn a_conflict_between_concurrent_writers_fails_exactly_one_of_them() {
    use std::sync::Barrier;

    let serving = start();
    let start_together = Barrier::new(2);

    // Same key, different value side — the one shape `ops-I5` refuses.
    let refused = thread::scope(|scope| {
        let handles: Vec<_> = ["one", "two"]
            .into_iter()
            .map(|text| {
                let (serving, barrier) = (&serving, &start_together);
                scope.spawn(move || {
                    let mut connection = serving.open(Mode::ReadWrite);
                    let batch = vec![doc("same.py", 1, "f", text)];
                    barrier.wait();
                    connection.write(DOC, &batch).is_err()
                })
            })
            .collect();
        handles
            .into_iter()
            .map(|handle| handle.join().expect("a writer"))
            .filter(|refused| *refused)
            .count()
    });

    assert_eq!(
        refused, 1,
        "exactly one of two contradictory writers must be refused — never both, which \
         would lose a fact nobody disagreed about, and never neither, which would mean \
         one silently won"
    );
}

/// **A union, all the way down the socket and back.**
///
/// Three claims at once, and each of them is somewhere a union could work in isolation
/// and still not work here:
///
/// - a schema declaring one survives `create`, which prints it to the database's
///   embedded copy and reads it back before anything exists on disk;
/// - a fact whose payload is a **reference** is interned through the payload, so the
///   file it names is written first and the tagged fact's key has no bytes until then;
/// - a row that *is* a union comes back with its alternative's **name**, which only the
///   row descriptor can supply — the row itself carries the tag.
#[test]
fn a_union_is_written_and_read_back_over_the_wire() {
    let serving = start();
    let mut connection = serving.open(Mode::ReadWrite);

    let tagged = |what: WireValue, id: i64| WireFact {
        predicate: TAGGED,
        key: WireValue::Record(Box::from([what, WireValue::Int(id)])),
        value: None,
    };
    let alt = |disc: u32, value: WireValue| WireValue::Union {
        disc,
        value: Box::new(value),
    };

    let written = connection
        .write(
            TAGGED,
            &[
                tagged(alt(NUM, WireValue::Int(5)), 10),
                tagged(alt(TEXT, WireValue::Str("a".to_owned())), 20),
                // The payload is a nested fact: two facts written for one, and the
                // reference resolved bottom-up.
                tagged(
                    alt(OF, WireValue::Ref(WireRef::Nested(Box::new(file("f.py"))))),
                    30,
                ),
            ],
        )
        .expect("the facts are written");

    assert_eq!(
        (written.created, written.deduped),
        (4, 0),
        "three tagged facts and the file one of their payloads names"
    );

    // Matching an alternative: the tag is a prefix of the key order here, so this is a
    // seek — and the rows say which alternative was matched, whatever the plan did.
    let mut rows = connection
        .query("X where src.Tagged {what = {num = X}, id = _}")
        .expect("it compiles");
    assert_eq!(
        connection.drain(&mut rows).expect("the rows arrive"),
        [WireValue::Int(5)]
    );

    // And the whole union, projected: the descriptor names the alternatives, so a
    // client can tell `num` from `text` without holding the schema's tags itself.
    let mut rows = connection
        .query("W where src.Tagged {what = W, id = 20}")
        .expect("it compiles");

    let fjord_client::Desc::Union(alternatives) = rows.desc() else {
        panic!("expected a union descriptor, got {:?}", rows.desc());
    };
    assert_eq!(
        alternatives
            .iter()
            .map(|(name, disc, _)| (name.as_str(), *disc))
            .collect::<Vec<_>>(),
        [("num", NUM), ("text", TEXT), ("of", OF)],
        "the descriptor carries every alternative's name and tag, in declaration order"
    );

    assert_eq!(
        connection.drain(&mut rows).expect("the rows arrive"),
        [alt(TEXT, WireValue::Str("a".to_owned()))]
    );
}

/// **The positive half of the old-server translation**: a fetch answered with
/// `ErrorCode::Protocol` — which on that stream can only mean the `F` frame itself
/// was not understood — comes back as [`ClientError::Unsupported`], with the remedy
/// in the message. The fake server here *is* the old server: it completes the
/// handshake and then refuses the frame kind by code.
#[test]
fn a_server_that_predates_expansion_is_reported_as_unsupported() {
    use fjord_wire::{
        frame::FrameKind,
        protocol::{self, ErrorCode},
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("old.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");
        ready_fake_server(&mut stream);

        // The next frame is the fetch this server has never heard of.
        let (header, _) = read_frame(&mut stream);

        let refusal = protocol::encode_error(
            ErrorCode::Protocol,
            &format!("no handler for frame kind {:?}", header.kind),
        );
        send_frame(&mut stream, FrameKind::ERROR, header.stream, &refusal);
    });

    let schema = Arc::new(schema());
    let mut connection =
        Connection::connect(&socket, "code", Arc::clone(&schema), Mode::ReadOnly, false)
            .expect("the handshake completes");

    let id = FactId::new(FILE, 1).expect("a well-formed id");
    let refused = connection
        .fetch(&schema, &[id], None)
        .expect_err("the fake server refuses the frame kind");

    assert!(
        matches!(&refused, ClientError::Unsupported(message)
            if message.contains("before expansion existed")),
        "an old server is reported as unsupported, with the remedy: {refused:?}"
    );

    server.join().expect("the fake server exits cleanly");
}

/// **A server-reported error mid-stream is terminal, exactly as `COMPLETE` is.**
///
/// The real-server ceiling guard fixes an error before a chunk has emitted anything;
/// this peer fixes the other important position by sending one `DATA_ROW` first. The
/// second query proves release by reusing the terminal stream's id.
#[test]
fn a_mid_stream_error_ends_the_stream_the_way_complete_does() {
    use fjord_wire::{
        desc::{Desc, encode_desc},
        frame::FrameKind,
        protocol::{self, ErrorCode},
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fake.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");

        ready_fake_server(&mut stream);

        // First query: one row, then an error instead of `COMPLETE`.
        let (first, _) = read_frame(&mut stream);
        assert_eq!(first.kind, protocol::kinds::QUERY);

        let mut desc = vec![];
        encode_desc(&mut desc, &Desc::Str);
        send_frame(&mut stream, FrameKind::ROW_DESCRIPTION, first.stream, &desc);
        send_frame(
            &mut stream,
            FrameKind::DATA_ROW,
            first.stream,
            &string_row("row-one"),
        );
        send_frame(
            &mut stream,
            FrameKind::ERROR,
            first.stream,
            &protocol::encode_error(ErrorCode::Internal, "examined rows past this run's ceiling"),
        );

        // Second query, on the *same connection*: the stream id it carries is the
        // proof. Reused only if the errored stream was actually released.
        let (second, _) = read_frame(&mut stream);
        assert_eq!(second.kind, protocol::kinds::QUERY);
        assert_eq!(
            second.stream, first.stream,
            "the errored stream's id was never recycled"
        );

        send_frame(
            &mut stream,
            FrameKind::ROW_DESCRIPTION,
            second.stream,
            &desc,
        );
        send_frame(
            &mut stream,
            FrameKind::DATA_ROW,
            second.stream,
            &string_row("row-two"),
        );
        send_frame(
            &mut stream,
            protocol::kinds::COMPLETE,
            second.stream,
            &protocol::encode_complete(1, 0),
        );
    });

    let schema = Arc::new(schema());
    let mut connection =
        Connection::connect(&socket, "code", Arc::clone(&schema), Mode::ReadOnly, false)
            .expect("the handshake completes");

    let mut rows = connection
        .query("F where src.File F")
        .expect("the stream opens");

    assert_eq!(
        connection
            .next_row(&mut rows)
            .expect("the first row arrives"),
        Some(WireValue::Str("row-one".to_owned()))
    );

    let error = connection
        .next_row(&mut rows)
        .expect_err("the server's error reaches the caller");
    assert!(
        matches!(&error, ClientError::Server { code, .. } if *code == ErrorCode::Internal),
        "the server's own error, not a protocol complaint about the frame: {error:?}"
    );

    assert!(
        rows.finished(),
        "an error ends the result exactly as COMPLETE does"
    );

    // Safe to call again only because `finished()` is already true above: this
    // returns `Ok(None)` without touching the socket rather than waiting on a stream
    // whose server-side task has already returned.
    assert_eq!(
        connection
            .next_row(&mut rows)
            .expect("finished is idempotent"),
        None
    );

    assert_eq!(
        connection.stream_ids_issued(),
        1,
        "one stream was ever needed before the second query"
    );

    let mut second_rows = connection
        .query("F where src.File F")
        .expect("it opens again");
    assert_eq!(
        connection.drain(&mut second_rows).expect("its row arrives"),
        vec![WireValue::Str("row-two".to_owned())]
    );

    assert_eq!(
        connection.stream_ids_issued(),
        1,
        "the second query reused the errored stream's id rather than minting a new one"
    );

    server.join().expect("the fake server exits cleanly");
}

/// A session-level error may surface while a query is open, but it does not prove
/// that query's task ended and must not make its stream id available for reuse.
#[test]
fn a_session_error_does_not_recycle_a_query_stream_that_is_still_running() {
    use fjord_wire::{
        StreamId,
        desc::{Desc, encode_desc},
        frame::FrameKind,
        protocol::{self, ErrorCode},
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("session-error.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");
        ready_fake_server(&mut stream);

        let (first, _) = read_frame(&mut stream);
        assert_eq!(first.kind, protocol::kinds::QUERY);

        let mut desc = vec![];
        encode_desc(&mut desc, &Desc::Str);
        send_frame(&mut stream, FrameKind::ROW_DESCRIPTION, first.stream, &desc);
        send_frame(
            &mut stream,
            FrameKind::ERROR,
            StreamId(0),
            &protocol::encode_error(ErrorCode::Internal, "the session reported a fault"),
        );

        // The first query is still live, so concurrent work must claim another id.
        let (second, _) = read_frame(&mut stream);
        assert_eq!(second.kind, protocol::kinds::QUERY);
        assert_ne!(second.stream, first.stream);
        send_frame(
            &mut stream,
            FrameKind::ROW_DESCRIPTION,
            second.stream,
            &desc,
        );
        send_frame(
            &mut stream,
            protocol::kinds::COMPLETE,
            second.stream,
            &protocol::encode_complete(0, 0),
        );

        send_frame(
            &mut stream,
            FrameKind::DATA_ROW,
            first.stream,
            &string_row("first-still-live"),
        );
        send_frame(
            &mut stream,
            protocol::kinds::COMPLETE,
            first.stream,
            &protocol::encode_complete(1, 0),
        );
    });

    let schema = Arc::new(schema());
    let mut connection = Connection::connect(&socket, "code", schema, Mode::ReadOnly, false)
        .expect("the handshake completes");

    let mut first = connection.query("F where src.File F").expect("a result");
    let error = connection
        .next_row(&mut first)
        .expect_err("the session error surfaces immediately");
    assert_eq!(error.code(), Some(ErrorCode::Internal));
    assert!(!first.finished(), "the query itself has not ended");

    let mut second = connection
        .query("F where src.File F")
        .expect("another stream can still be opened");
    assert!(
        connection
            .drain(&mut second)
            .expect("it completes")
            .is_empty()
    );
    assert_eq!(connection.stream_ids_issued(), 2);

    assert_eq!(
        connection
            .next_row(&mut first)
            .expect("the query carries on"),
        Some(WireValue::Str("first-still-live".to_owned()))
    );
    assert_eq!(connection.next_row(&mut first).expect("it completes"), None);

    server.join().expect("the fake server exits cleanly");
}

/// A fetch has no `Rows` bookmark, but its stream is still live when a session-level
/// error interrupts the receive. Reusing that id lets its late positional reply answer
/// a different fetch, silently returning the wrong fact.
#[test]
fn a_session_error_does_not_recycle_a_fetch_stream_that_is_still_running() {
    use fjord_wire::{
        StreamId,
        frame::FrameKind,
        protocol::{self, ErrorCode, Fetched},
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fetch-session-error.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");
    let first_id = FactId::new(FILE, 1).expect("an id");
    let second_id = FactId::new(FILE, 2).expect("an id");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");
        ready_fake_server(&mut stream);

        let (first, _) = read_frame(&mut stream);
        assert_eq!(first.kind, protocol::kinds::FETCH);
        send_frame(
            &mut stream,
            FrameKind::ERROR,
            StreamId(0),
            &protocol::encode_error(ErrorCode::Internal, "the session reported a fault"),
        );

        let (second, _) = read_frame(&mut stream);
        assert_eq!(second.kind, protocol::kinds::FETCH);

        // The first reply arrives only after the second request. Since FETCHED is
        // positional and carries no ids, reusing the stream would make this look like
        // the answer to `second_id` and return the wrong key without a decode error.
        let first_reply = protocol::encode_fetched(
            &schema(),
            &[Fetched {
                id: first_id,
                found: Found::Key(WireValue::Str("first.py".to_owned())),
            }],
        )
        .expect("a reply");
        send_frame(
            &mut stream,
            protocol::kinds::FETCHED,
            first.stream,
            &first_reply,
        );

        let second_reply = protocol::encode_fetched(
            &schema(),
            &[Fetched {
                id: second_id,
                found: Found::Key(WireValue::Str("second.py".to_owned())),
            }],
        )
        .expect("a reply");
        send_frame(
            &mut stream,
            protocol::kinds::FETCHED,
            second.stream,
            &second_reply,
        );
    });

    let schema = Arc::new(schema());
    let mut connection =
        Connection::connect(&socket, "code", Arc::clone(&schema), Mode::ReadOnly, false)
            .expect("the handshake completes");

    let error = connection
        .fetch(&schema, &[first_id], None)
        .expect_err("the session error surfaces");
    assert_eq!(error.code(), Some(ErrorCode::Internal));

    assert_eq!(
        connection
            .fetch(&schema, &[second_id], None)
            .expect("the second fetch"),
        vec![Found::Key(WireValue::Str("second.py".to_owned()))]
    );
    assert_eq!(
        connection.stream_ids_issued(),
        2,
        "the still-live first fetch keeps its stream id"
    );

    server.join().expect("the fake server exits cleanly");
}

/// **A cancel that races a terminal error must not hang, and the connection must
/// still work afterwards.**
///
/// [`Connection::cancel`] reads frames on the same query stream `next_row` does, and
/// shares its fix: a server can answer `CANCEL` with an error instead of `COMPLETE`
/// — the query the cancel raced against had already failed — and that stream must be
/// released exactly as an error mid-`next_row` releases it.
#[test]
fn a_cancel_racing_a_terminal_error_leaves_the_connection_working() {
    use fjord_wire::{
        desc::{Desc, encode_desc},
        frame::FrameKind,
        protocol::{self, ErrorCode},
    };

    let dir = tempfile::tempdir().expect("a scratch directory");
    let socket = dir.path().join("fake.sock");
    let listener = std::os::unix::net::UnixListener::bind(&socket).expect("a socket");

    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().expect("a connection");

        ready_fake_server(&mut stream);

        let (query, _) = read_frame(&mut stream);
        assert_eq!(query.kind, protocol::kinds::QUERY);

        let mut desc = vec![];
        encode_desc(&mut desc, &Desc::Str);
        send_frame(&mut stream, FrameKind::ROW_DESCRIPTION, query.stream, &desc);

        // The client cancels before any row arrives — the fake server answers the
        // `CANCEL` with an error rather than `COMPLETE`.
        let (cancel, _) = read_frame(&mut stream);
        assert_eq!(cancel.kind, protocol::kinds::CANCEL);
        assert_eq!(cancel.stream, query.stream);
        send_frame(
            &mut stream,
            FrameKind::ERROR,
            query.stream,
            &protocol::encode_error(ErrorCode::Internal, "failed before the cancel landed"),
        );

        // A second query proves the first stream's id was recycled.
        let (second, _) = read_frame(&mut stream);
        assert_eq!(second.kind, protocol::kinds::QUERY);
        assert_eq!(
            second.stream, query.stream,
            "the errored-during-cancel stream's id was never recycled"
        );

        send_frame(
            &mut stream,
            FrameKind::ROW_DESCRIPTION,
            second.stream,
            &desc,
        );
        let row = string_row("still-here");
        send_frame(&mut stream, FrameKind::DATA_ROW, second.stream, &row);
        send_frame(
            &mut stream,
            protocol::kinds::COMPLETE,
            second.stream,
            &protocol::encode_complete(1, 0),
        );
    });

    let schema = Arc::new(schema());
    let mut connection =
        Connection::connect(&socket, "code", Arc::clone(&schema), Mode::ReadOnly, false)
            .expect("the handshake completes");

    let mut rows = connection
        .query("F where src.File F")
        .expect("the stream opens");

    let error = connection
        .cancel(&mut rows)
        .expect_err("the race's error reaches the caller rather than being swallowed");
    assert!(
        matches!(&error, ClientError::Server { code, .. } if *code == ErrorCode::Internal),
        "wrong error: {error:?}"
    );
    assert!(rows.finished(), "the race still ends the result");

    let mut second_rows = connection
        .query("F where src.File F")
        .expect("it opens again");
    assert_eq!(
        connection.drain(&mut second_rows).expect("its row arrives"),
        vec![WireValue::Str("still-here".to_owned())]
    );

    assert_eq!(
        connection.stream_ids_issued(),
        1,
        "the connection is still usable and did not need a second stream id"
    );

    server.join().expect("the fake server exits cleanly");
}
