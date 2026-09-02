//! **The two-client criterion**: the Rust and C# clients produce byte-identical blocks
//! for the same facts.
//!
//! Interoperating today does not prove that, and the difference matters. Two encoders
//! can disagree about something the server happens to tolerate, or about a case neither
//! demo exercises, and a fact file written by one would then not be the file the other
//! writes — which is a problem that surfaces at 7b, in a fact file nobody can split,
//! long after the two implementations parted company.
//!
//! So the C# client writes its answer for a fixed corpus to
//! `clients/dotnet/golden/blocks.txt` (`./clients/dotnet/emit-golden.sh`), and this
//! encodes the same facts and compares. **The schema and the corpus are stated
//! independently on each side** — three times over, counting `fjord::code_index` —
//! and that is deliberate rather than duplication to be tidied away: a shared statement
//! would make the two encoders agree by construction, which is precisely the agreement
//! this is trying to test.
//!
//! The test needs no `dotnet` to run. Regenerating the golden does.

use std::sync::Arc;

use fjord_schema::fingerprint;
use fjord_schema::schema::{Alternative, Predicate, PredicateId, PredicateTy, Schema};
use fjord_wire::{WireFact, WireRef, WireValue, encode_block};
use lasso::Rodeo;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);
const REFERENCE: PredicateId = PredicateId(2);
const SPAN: PredicateId = PredicateId(3);
const EXTENT: PredicateId = PredicateId(4);
const KIND: PredicateId = PredicateId(5);
const KIND_OF: PredicateId = PredicateId(6);
const RESOLVES: PredicateId = PredicateId(7);
const DIGEST: PredicateId = PredicateId(8);
const EXTENDS: PredicateId = PredicateId(9);
const NOTE: PredicateId = PredicateId(10);

/// **`schemas/demo.sigla`, stated here rather than parsed.**
///
/// The point of the file is that two implementations agree, so this side writes the
/// schema down. Parsing the `.sigla` and encoding against it would test the parser, not
/// the codec, and would make the C# statement's agreement automatic.
///
/// The order is the C# client's order, because a predicate's id is its position in each
/// client's own list and the golden names the ids it used. Nothing positional crosses
/// the wire — a block header carries the predicate's *name* — so the two lists need only
/// agree with themselves.
fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let mut sym = |name: &str| rodeo.get_or_intern(name);

    let (file, decl, reference, span) = (
        sym("code.File"),
        sym("code.Decl"),
        sym("code.Ref"),
        sym("code.Span"),
    );
    let (extent, kind, kind_of) = (sym("code.Extent"), sym("code.Kind"), sym("code.KindOf"));
    let (resolves, digest, extends, note) = (
        sym("code.Resolves"),
        sym("code.Digest"),
        sym("code.Extends"),
        sym("code.Note"),
    );

    let (f_file, f_name, f_line, f_col) = (sym("file"), sym("name"), sym("line"), sym("col"));
    let (f_from, f_to, f_decl, f_at) = (sym("from"), sym("to"), sym("decl"), sym("at"));
    let (f_what, f_sha256, f_type, f_base, f_text) = (
        sym("what"),
        sym("sha256"),
        sym("type"),
        sym("base"),
        sym("text"),
    );
    let f_assembly = sym("assembly");

    let alt = |name: lasso::Spur, disc: u32, ty: PredicateTy| Alternative { name, disc, ty };

    /// **An alternative declared with no type is the empty record**, which is what
    /// `unresolved = 0` lowers to — verified against `fjord schema check`, not assumed.
    fn unit() -> PredicateTy {
        PredicateTy::Record(Arc::from([]))
    }

    let position = PredicateTy::Record(Arc::from([
        (f_line, PredicateTy::Int),
        (f_col, PredicateTy::Int),
    ]));

    // **The tags are 5 and 2, not 0 and 1.** A client numbering alternatives by
    // position writes `data` where the schema says `func`, and nothing on the wire
    // objects.
    let what = PredicateTy::Union(Arc::from([
        alt(sym("data"), 5, PredicateTy::Str),
        alt(sym("func"), 2, PredicateTy::Int),
    ]));

    // A payload of every kind: none, a reference, a reference to a *different*
    // predicate, and a record.
    let target = PredicateTy::Union(Arc::from([
        alt(sym("unresolved"), 0, unit()),
        alt(sym("decl"), 1, PredicateTy::Fact(DECL)),
        alt(sym("file"), 2, PredicateTy::Fact(FILE)),
        alt(
            sym("external"),
            3,
            PredicateTy::Record(Arc::from([
                (f_name, PredicateTy::Str),
                (f_assembly, PredicateTy::Str),
            ])),
        ),
    ]));

    let rendered = PredicateTy::Union(Arc::from([alt(sym("html"), 0, PredicateTy::Str)]));

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![
            Predicate {
                name: file,
                key: PredicateTy::Str,
                value: None,
            },
            // A reference leading the key, and a value side.
            Predicate {
                name: decl,
                key: PredicateTy::Record(Arc::from([
                    (f_file, PredicateTy::Fact(FILE)),
                    (f_name, PredicateTy::Str),
                    (f_line, PredicateTy::Int),
                ])),
                value: Some(PredicateTy::Str),
            },
            // Two references to one predicate, each of which names its file — two
            // levels of nesting.
            Predicate {
                name: reference,
                key: PredicateTy::Record(Arc::from([
                    (f_from, PredicateTy::Fact(DECL)),
                    (f_to, PredicateTy::Fact(DECL)),
                ])),
                value: None,
            },
            // A reference then a **nested record**, spliced into the key rather than
            // framed.
            Predicate {
                name: span,
                key: PredicateTy::Record(Arc::from([
                    (f_decl, PredicateTy::Fact(DECL)),
                    (f_at, position.clone()),
                ])),
                value: None,
            },
            // A **record value side**, which a scalar one does not reach.
            Predicate {
                name: extent,
                key: PredicateTy::Record(Arc::from([(f_decl, PredicateTy::Fact(DECL))])),
                value: Some(PredicateTy::Record(Arc::from([
                    (f_from, position.clone()),
                    (f_to, position),
                ]))),
            },
            Predicate {
                name: kind,
                key: PredicateTy::Record(Arc::from([
                    (f_decl, PredicateTy::Fact(DECL)),
                    (f_what, what.clone()),
                ])),
                value: None,
            },
            // A **union leading a key**.
            Predicate {
                name: kind_of,
                key: PredicateTy::Record(Arc::from([
                    (f_what, what),
                    (f_decl, PredicateTy::Fact(DECL)),
                ])),
                value: None,
            },
            Predicate {
                name: resolves,
                key: PredicateTy::Record(Arc::from([(f_at, PredicateTy::Int), (f_to, target)])),
                value: None,
            },
            // `bytes`, in a value side.
            Predicate {
                name: digest,
                key: PredicateTy::Record(Arc::from([(f_file, PredicateTy::Fact(FILE))])),
                value: Some(PredicateTy::Record(Arc::from([(
                    f_sha256,
                    PredicateTy::Bytes,
                )]))),
            },
            // `type` as a field name, which the grammar allows.
            Predicate {
                name: extends,
                key: PredicateTy::Record(Arc::from([
                    (f_type, PredicateTy::Fact(DECL)),
                    (f_base, PredicateTy::Fact(DECL)),
                ])),
                value: None,
            },
            Predicate {
                name: note,
                key: PredicateTy::Record(Arc::from([
                    (f_decl, PredicateTy::Fact(DECL)),
                    (f_text, rendered),
                ])),
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

/// A declaration naming its file, with its signature on the value side.
fn decl(path: &str, name: &str, line: i64, signature: &str) -> WireFact {
    WireFact {
        predicate: DECL,
        key: WireValue::Record(Box::from([
            WireValue::Ref(WireRef::Nested(Box::new(file(path)))),
            WireValue::Str(name.to_owned()),
            WireValue::Int(line),
        ])),
        value: Some(WireValue::Str(signature.to_owned())),
    }
}

fn nested(fact: WireFact) -> WireValue {
    WireValue::Ref(WireRef::Nested(Box::new(fact)))
}

fn union(disc: u32, value: WireValue) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(value),
    }
}

/// The same facts the C# side writes, restated here rather than shared.
///
/// Chosen for what it **reaches** rather than for what it means: scalars, a value side,
/// two levels of nesting, a record spliced into a key, a union leading a key with a tag
/// that is neither 0 nor 1, an empty union payload, a union payload that is a record,
/// `bytes` behind a `->`, a record value side, and integers on both sides of the
/// varint's one-byte boundary in both signs.
fn corpus() -> Vec<(&'static str, PredicateId, Vec<WireFact>)> {
    let key_of = || decl("store/keys.py", "key_of", 12, "def key_of(row)");
    let plan = || decl("query/plan.py", "Plan", 5, "class Plan");

    vec![
        (
            "code.File",
            FILE,
            vec![file("store/keys.py"), file("query/plan.py")],
        ),
        (
            "code.Decl",
            DECL,
            vec![
                key_of(),
                decl("store/keys.py", "zero", 0, "def zero()"),
                decl("query/plan.py", "Plan", 2_147_483_648, "class Plan"),
                decl("store/keys.py", "before", -1, "def before()"),
            ],
        ),
        (
            "code.Ref",
            REFERENCE,
            vec![WireFact {
                predicate: REFERENCE,
                key: WireValue::Record(Box::from([nested(key_of()), nested(plan())])),
                value: None,
            }],
        ),
        (
            "code.Span",
            SPAN,
            vec![WireFact {
                predicate: SPAN,
                key: WireValue::Record(Box::from([
                    nested(key_of()),
                    WireValue::Record(Box::from([WireValue::Int(12), WireValue::Int(4)])),
                ])),
                value: None,
            }],
        ),
        (
            "code.KindOf",
            KIND_OF,
            vec![
                WireFact {
                    predicate: KIND_OF,
                    key: WireValue::Record(Box::from([
                        union(2, WireValue::Int(1)),
                        nested(key_of()),
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: KIND_OF,
                    key: WireValue::Record(Box::from([
                        union(5, WireValue::Str("class".to_owned())),
                        nested(plan()),
                    ])),
                    value: None,
                },
            ],
        ),
        (
            "code.Resolves",
            RESOLVES,
            vec![
                WireFact {
                    predicate: RESOLVES,
                    key: WireValue::Record(Box::from([
                        WireValue::Int(7),
                        union(0, WireValue::Record(Box::from([]))),
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: RESOLVES,
                    key: WireValue::Record(Box::from([
                        WireValue::Int(19),
                        union(
                            3,
                            WireValue::Record(Box::from([
                                WireValue::Str("Deserialize".to_owned()),
                                WireValue::Str("serde".to_owned()),
                            ])),
                        ),
                    ])),
                    value: None,
                },
            ],
        ),
        (
            "code.Digest",
            DIGEST,
            vec![WireFact {
                predicate: DIGEST,
                key: WireValue::Record(Box::from([nested(file("store/keys.py"))])),
                value: Some(WireValue::Record(Box::from([WireValue::Bytes(vec![
                    0x00, 0x53, 0x80, 0xBF, 0xFF,
                ])]))),
            }],
        ),
        (
            "code.Extent",
            EXTENT,
            vec![WireFact {
                predicate: EXTENT,
                key: WireValue::Record(Box::from([nested(plan())])),
                value: Some(WireValue::Record(Box::from([
                    WireValue::Record(Box::from([WireValue::Int(5), WireValue::Int(1)])),
                    WireValue::Record(Box::from([WireValue::Int(41), WireValue::Int(2)])),
                ]))),
            }],
        ),
        // The same union *after* a reference rather than before it: a tag has to encode
        // the same wherever it sits in a key.
        (
            "code.Kind",
            KIND,
            vec![WireFact {
                predicate: KIND,
                key: WireValue::Record(Box::from([nested(key_of()), union(2, WireValue::Int(1))])),
                value: None,
            }],
        ),
        // `type` as a field name, which the grammar allows.
        (
            "code.Extends",
            EXTENDS,
            vec![WireFact {
                predicate: EXTENDS,
                key: WireValue::Record(Box::from([
                    nested(decl("store/codec.py", "CodecError", 31, "class CodecError")),
                    nested(plan()),
                ])),
                value: None,
            }],
        ),
        // A single-alternative union, whose tag is still written rather than elided.
        (
            "code.Note",
            NOTE,
            vec![WireFact {
                predicate: NOTE,
                key: WireValue::Record(Box::from([
                    nested(plan()),
                    union(
                        0,
                        WireValue::Str("<p>An ordered list of steps.</p>".to_owned()),
                    ),
                ])),
                value: None,
            }],
        ),
    ]
}

/// The golden as parsed: the fingerprint it names, and one entry per block.
struct Golden {
    fingerprint: u64,
    blocks: Vec<(String, u32, Vec<u8>)>,
}

fn golden() -> Golden {
    golden_at(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../clients/dotnet/golden/blocks.txt"
    ))
}

fn golden_at(path: &str) -> Golden {
    let text = std::fs::read_to_string(path).unwrap_or_else(|error| {
        panic!("cannot read {path}: {error}\nregenerate with ./clients/dotnet/emit-golden.sh")
    });

    let mut fingerprint = None;
    let mut blocks = vec![];

    for line in text.lines() {
        let line = line.trim();
        if line.is_empty() || line.starts_with('#') {
            continue;
        }

        let mut parts = line.split_whitespace();

        match parts.next() {
            Some("schema-fingerprint") => {
                let hex = parts.next().expect("a fingerprint");
                fingerprint = Some(u64::from_str_radix(hex, 16).expect("hex"));
            }
            Some("block") => {
                let name = parts.next().expect("a predicate name").to_owned();
                let predicate: u32 = parts
                    .next()
                    .expect("a predicate id")
                    .parse()
                    .expect("a u32");
                let bytes = unhex(parts.next().expect("the block's bytes"));
                blocks.push((name, predicate, bytes));
            }
            other => panic!("a golden line this test does not understand: {other:?}"),
        }
    }

    Golden {
        fingerprint: fingerprint.expect("the golden names a schema fingerprint"),
        blocks,
    }
}

fn unhex(text: &str) -> Vec<u8> {
    assert!(text.len() % 2 == 0, "hex comes in pairs");

    (0..text.len())
        .step_by(2)
        .map(|at| u8::from_str_radix(&text[at..at + 2], 16).expect("hex"))
        .collect()
}

fn hex(bytes: &[u8]) -> String {
    bytes.iter().map(|byte| format!("{byte:02x}")).collect()
}

/// **The criterion.** Same facts, same schema, same bytes.
#[test]
fn byte_identical_with_the_dotnet_client() {
    let golden = golden();
    let schema = schema();

    // First, because it explains every failure below it. Two clients that disagree
    // about the schema are not two clients that disagree about the codec, and being
    // told which one it is saves reading a hex diff to find out.
    assert_eq!(
        fingerprint::of(&schema),
        golden.fingerprint,
        "the two clients' schemas disagree, so their blocks were never going to match"
    );

    let corpus = corpus();
    assert_eq!(
        corpus.len(),
        golden.blocks.len(),
        "the corpora have drifted: {} blocks here, {} in the golden",
        corpus.len(),
        golden.blocks.len()
    );

    for ((name, predicate, facts), (golden_name, golden_predicate, expected)) in
        corpus.iter().zip(&golden.blocks)
    {
        assert_eq!(name, golden_name, "the corpora are in different orders");
        assert_eq!(predicate.0, *golden_predicate, "{name}");

        let mut block = vec![];
        encode_block(&mut block, &schema, *predicate, facts).expect("it encodes");

        assert_eq!(
            hex(&block),
            hex(expected),
            "`{name}` differs between the Rust and C# clients"
        );
    }
}

/// The golden is bytes on the wire, so it is also bytes this build can *read* — which
/// is worth checking separately, because an encoder and a decoder can agree with each
/// other while both disagree with everyone else.
#[test]
fn the_dotnet_clients_blocks_decode_here() {
    let schema = schema();

    for ((name, predicate, facts), (_, _, bytes)) in corpus().iter().zip(&golden().blocks) {
        let header = fjord_wire::block::decode_header(bytes)
            .unwrap_or_else(|error| panic!("`{name}`'s header does not decode: {error}"));
        let (decoded, _) = fjord_wire::decode_block(bytes, &schema)
            .unwrap_or_else(|error| panic!("`{name}` does not decode: {error}"));

        // The header names its predicate now rather than numbering it, so this
        // asserts the *name* — and that the reader resolves it to the id it expects.
        assert_eq!(header.predicate, *name, "{name}");
        assert_eq!(
            schema.find_position(name).map(|(id, _)| id),
            Some(*predicate),
            "`{name}` does not resolve to the id the corpus declares"
        );
        assert_eq!(header.count as usize, facts.len(), "{name}");
        assert_eq!(&decoded, facts, "`{name}` decodes to different facts");
    }
}

// ---- unions (8.6) ---------------------------------------------------------
//
// A **second** golden, over a schema of its own, and the separation is deliberate: a
// union in `schemas/code.sigla` would move that schema's fingerprint and with it two
// constants in the .NET clients and every block in the golden above — a flag day, and
// one that has nothing to do with whether the two codecs agree about a tag. So the
// union corpus gets three predicates of its own, stated independently on each side
// exactly as the corpus above is.

const THING: PredicateId = PredicateId(0);
const TAGGED: PredicateId = PredicateId(1);
const LABELLED: PredicateId = PredicateId(2);

/// The tags, which are **not** positions: 3, 0, 40000 and 7, declared in that order.
///
/// 40000 is past a single varint byte, and 0 is the tag a reader defaulting to "the
/// first alternative" would produce — so a client numbering by position answers `num`
/// where this says `text`, and one truncating a varint answers nothing at all.
const NUM: u32 = 3;
const TEXT: u32 = 0;
const THING_ALT: u32 = 40_000;
const NONE: u32 = 7;

/// `uni.Thing`, `uni.Tagged` and `uni.Labelled`, restated in Rust.
fn union_schema() -> Schema {
    use fjord_schema::schema::Alternative;

    let mut rodeo = Rodeo::new();
    let mut sym = |name: &str| rodeo.get_or_intern(name);

    let (thing, tagged, labelled) = (sym("uni.Thing"), sym("uni.Tagged"), sym("uni.Labelled"));
    let (f_id, f_what) = (sym("id"), sym("what"));
    let (a_num, a_text, a_thing, a_none) = (sym("num"), sym("text"), sym("thing"), sym("none"));

    // One union, used in a key field *and* on a value side — so the same alternatives
    // are encoded through both paths, and a client that special-cased one of them is
    // caught.
    let alternatives = || {
        PredicateTy::Union(Arc::from([
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
                name: a_thing,
                disc: THING_ALT,
                ty: PredicateTy::Fact(THING),
            },
            Alternative {
                name: a_none,
                disc: NONE,
                ty: PredicateTy::Record(Arc::from([])),
            },
        ]))
    };

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![
            Predicate {
                name: thing,
                key: PredicateTy::Record(Arc::from([(f_id, PredicateTy::Int)])),
                value: None,
            },
            // The union **leads**, which on the wire changes nothing and in storage
            // changes everything — stated the same way on both sides so the two
            // schemas match field for field.
            Predicate {
                name: tagged,
                key: PredicateTy::Record(Arc::from([
                    (f_what, alternatives()),
                    (f_id, PredicateTy::Int),
                ])),
                value: None,
            },
            Predicate {
                name: labelled,
                key: PredicateTy::Record(Arc::from([(f_id, PredicateTy::Int)])),
                value: Some(alternatives()),
            },
        ]),
    )
}

fn thing(id: i64) -> WireFact {
    WireFact {
        predicate: THING,
        key: WireValue::Record(Box::from([WireValue::Int(id)])),
        value: None,
    }
}

fn tagged(what: WireValue, id: i64) -> WireFact {
    WireFact {
        predicate: TAGGED,
        key: WireValue::Record(Box::from([what, WireValue::Int(id)])),
        value: None,
    }
}

fn alt(disc: u32, value: WireValue) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(value),
    }
}

/// One block per predicate, one fact per alternative.
fn union_corpus() -> Vec<(&'static str, PredicateId, Vec<WireFact>)> {
    vec![
        ("uni.Thing", THING, vec![thing(1), thing(2)]),
        (
            "uni.Tagged",
            TAGGED,
            vec![
                tagged(alt(NUM, WireValue::Int(5)), 10),
                tagged(alt(TEXT, WireValue::Str("a".to_owned())), 20),
                // **A nested reference inside a payload** — the case a walk that stops
                // at a union misses, and the one that would leave a fact uninterned.
                tagged(
                    alt(
                        THING_ALT,
                        WireValue::Ref(WireRef::Nested(Box::new(thing(1)))),
                    ),
                    30,
                ),
                // An alternative whose payload is the empty record, which is what an
                // alternative declared with no type at all comes to: zero bytes after
                // the tag, so a reader expecting any is caught.
                tagged(alt(NONE, WireValue::Record(Box::from([]))), 40),
            ],
        ),
        (
            "uni.Labelled",
            LABELLED,
            vec![
                WireFact {
                    predicate: LABELLED,
                    key: WireValue::Record(Box::from([WireValue::Int(1)])),
                    value: Some(alt(NUM, WireValue::Int(7))),
                },
                WireFact {
                    predicate: LABELLED,
                    key: WireValue::Record(Box::from([WireValue::Int(2)])),
                    value: Some(alt(TEXT, WireValue::Str("b".to_owned()))),
                },
            ],
        ),
    ]
}

/// **The same criterion, for a tag.** Same facts, same schema, same bytes — over the
/// one construct the transport codec had to grow a marker for.
#[test]
fn unions_are_byte_identical_with_the_dotnet_client() {
    let golden = golden_at(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../clients/dotnet/golden/unions.txt"
    ));
    let schema = union_schema();

    assert_eq!(
        fingerprint::of(&schema),
        golden.fingerprint,
        "the two clients' union schemas disagree, so their blocks were never going to \
         match"
    );

    let corpus = union_corpus();
    assert_eq!(
        corpus.len(),
        golden.blocks.len(),
        "the corpora have drifted: {} blocks here, {} in the golden",
        corpus.len(),
        golden.blocks.len()
    );

    for ((name, predicate, facts), (golden_name, golden_predicate, expected)) in
        corpus.iter().zip(&golden.blocks)
    {
        assert_eq!(name, golden_name, "the corpora are in different orders");
        assert_eq!(predicate.0, *golden_predicate, "{name}");

        let mut block = vec![];
        encode_block(&mut block, &schema, *predicate, facts).expect("it encodes");

        assert_eq!(
            hex(&block),
            hex(expected),
            "`{name}` differs between the Rust and C# clients"
        );
    }
}

/// The `bytes` corpus's schema, stated independently of the C# side's.
///
/// Its own schema for the reason the union corpus has one: a `bytes` field in
/// `schemas/code.sigla` would move that schema's fingerprint and every block in
/// `blocks.txt` with it.
fn bytes_schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let mut sym = |name: &str| rodeo.get_or_intern(name);

    let (digest_p, blob_p) = (sym("blob.Digest"), sym("blob.Blob"));
    let (f_digest, f_path, f_id) = (sym("digest"), sym("path"), sym("id"));

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![
            Predicate {
                name: digest_p,
                key: PredicateTy::Record(Arc::from([
                    (f_digest, PredicateTy::Bytes),
                    (f_path, PredicateTy::Str),
                ])),
                value: None,
            },
            // A `bytes` **value side** as well as a key field, so the same run goes
            // through both paths.
            Predicate {
                name: blob_p,
                key: PredicateTy::Record(Arc::from([(f_id, PredicateTy::Int)])),
                value: Some(PredicateTy::Bytes),
            },
        ]),
    )
}

/// The same facts the C# side writes, restated here rather than shared.
fn bytes_corpus() -> Vec<(&'static str, PredicateId, Vec<WireFact>)> {
    let digest = |payload: &[u8], path: &str| WireFact {
        predicate: PredicateId(0),
        key: WireValue::Record(Box::from([
            WireValue::Bytes(payload.to_vec()),
            WireValue::Str(path.to_owned()),
        ])),
        value: None,
    };

    let blob = |id: i64, payload: &[u8]| WireFact {
        predicate: PredicateId(1),
        key: WireValue::Record(Box::from([WireValue::Int(id)])),
        value: Some(WireValue::Bytes(payload.to_vec())),
    };

    vec![
        (
            "blob.Digest",
            PredicateId(0),
            vec![
                digest(b"", "empty"),
                digest(&[0x00], "nul"),
                digest(
                    &[0x00, 0xFF, 0xFF, 0x00, 0x80, 0xC0],
                    "everything a string cannot",
                ),
                digest(&[0xFF, 0xFF, 0xFF], "escape bytes"),
            ],
        ),
        (
            "blob.Blob",
            PredicateId(1),
            vec![blob(1, &[0xED, 0xA0, 0x80]), blob(2, b"")],
        ),
    ]
}

/// **The two clients produce the same bytes for a `bytes` field**, on the key side and
/// the value side, for runs no `String` could hold.
///
/// What this pins that the union golden could not: that neither client validates the
/// run, and that both spend a length prefix and the payload and nothing else. A client
/// that reused its string path would produce the same bytes *here* — the escaping is
/// storage's business, not this wire's — and the wrong ones on disk, which is why the
/// storage side has `bytes_ordering_edges` of its own.
#[test]
fn bytes_are_byte_identical_with_the_dotnet_client() {
    let golden = golden_at(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../clients/dotnet/golden/bytes.txt"
    ));
    let schema = bytes_schema();

    assert_eq!(
        fingerprint::of(&schema),
        golden.fingerprint,
        "the two clients' `bytes` schemas disagree, so their blocks were never going \
         to match"
    );

    let corpus = bytes_corpus();
    assert_eq!(
        corpus.len(),
        golden.blocks.len(),
        "the corpora have drifted: {} blocks here, {} in the golden",
        corpus.len(),
        golden.blocks.len()
    );

    for ((name, predicate, facts), (golden_name, golden_predicate, expected)) in
        corpus.iter().zip(&golden.blocks)
    {
        assert_eq!(name, golden_name, "the corpora are in different orders");
        assert_eq!(predicate.0, *golden_predicate, "{name}");

        let mut block = vec![];
        encode_block(&mut block, &schema, *predicate, facts).expect("it encodes");

        assert_eq!(
            hex(&block),
            hex(expected),
            "`{name}` differs between the Rust and C# clients"
        );
    }
}

/// The `bytes` corpus's fingerprint, for the C# side to carry.
#[test]
#[ignore = "not a guard: prints the bytes corpus's schema fingerprint, for the C# client to carry"]
fn print_the_bytes_schema_fingerprint() {
    println!(
        "bytes schema fingerprint {:016x}",
        fingerprint::of(&bytes_schema())
    );
}

/// The fingerprint the C# side has to **carry**, printed rather than asserted.
///
/// A client carries the number instead of computing it (chapter 6's D2), so somebody
/// has to read it off. `fjord schema fingerprint` is how a real client's author gets it;
/// this corpus has no `.sigla` file of its own, so this is that command for it.
#[test]
#[ignore = "not a guard: prints the union corpus's schema fingerprint, for the C# client to carry"]
fn print_the_union_schema_fingerprint() {
    println!(
        "union schema fingerprint {:016x}",
        fingerprint::of(&union_schema())
    );
}
