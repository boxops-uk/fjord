//! **The fused path writes what the ordinary one writes** — byte for byte.
//!
//! The experiment's whole claim is that three representations can be one, and the only
//! way to believe it is to run both against the same block and compare the rows that
//! come out. Not the ids — those are allocation order and would agree trivially — but
//! the stored bytes: the key a row is found by, and the value it holds.

use fjord_ingest::{
    fused::{Scratch, fuse_block},
    intern_block,
};
use fjord_schema::{
    id::FactId,
    schema::{Predicate, PredicateId, PredicateTy, Schema},
};
use fjord_store_fjall::store::FjallDb;
use fjord_wire::{WireFact, WireRef, WireValue, encode_block};
use lasso::Rodeo;
use std::sync::Arc;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);
const DOC: PredicateId = PredicateId(2);

/// Every shape the walk has an arm for: a scalar key, a record key holding a
/// reference, an int and a string, a value side, a nested record and a union.
fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let (file, decl, doc) = (
        rodeo.get_or_intern("src.File"),
        rodeo.get_or_intern("src.Decl"),
        rodeo.get_or_intern("src.Doc"),
    );
    let (f_file, f_line, f_name, f_decl, f_span, f_start, f_len, f_kind, f_text) = (
        rodeo.get_or_intern("file"),
        rodeo.get_or_intern("line"),
        rodeo.get_or_intern("name"),
        rodeo.get_or_intern("decl"),
        rodeo.get_or_intern("span"),
        rodeo.get_or_intern("start"),
        rodeo.get_or_intern("length"),
        rodeo.get_or_intern("kind"),
        rodeo.get_or_intern("text"),
    );
    let (a_none, a_some) = (rodeo.get_or_intern("none"), rodeo.get_or_intern("some"));

    use fjord_schema::schema::Alternative;

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
                        // A nested record, which is framed where the top level is not.
                        (
                            f_span,
                            PredicateTy::Record(
                                vec![(f_start, PredicateTy::Int), (f_len, PredicateTy::Int)].into(),
                            ),
                        ),
                        // A union, with an empty arm and one carrying a scalar.
                        (
                            f_kind,
                            PredicateTy::Union(
                                vec![
                                    Alternative {
                                        name: a_none,
                                        disc: 0,
                                        ty: PredicateTy::Record(vec![].into()),
                                    },
                                    Alternative {
                                        name: a_some,
                                        disc: 7,
                                        ty: PredicateTy::Int,
                                    },
                                ]
                                .into(),
                            ),
                        ),
                    ]
                    .into(),
                ),
                value: None,
            },
            Predicate {
                name: doc,
                key: PredicateTy::Record(vec![(f_decl, PredicateTy::Fact(DECL))].into()),
                // A value side, so both buffers are exercised.
                value: Some(PredicateTy::Record(vec![(f_text, PredicateTy::Str)].into())),
            },
        ]),
    )
}

fn decl_fact(n: usize, nested: bool) -> WireFact {
    let reference = if nested {
        WireRef::Nested(Box::new(WireFact {
            predicate: FILE,
            key: WireValue::Str(format!("src/file{}.rs", n % 16)),
            value: None,
        }))
    } else {
        WireRef::Id(FactId::new(FILE, 1 + (n as u64 % 16)).expect("an id"))
    };

    WireFact {
        predicate: DECL,
        key: WireValue::Record(Box::from([
            WireValue::Ref(reference),
            WireValue::Int(n as i64),
            WireValue::Str(format!("declaration_{n}")),
            WireValue::Record(Box::from([
                WireValue::Int(n as i64 * 7),
                WireValue::Int(n as i64 % 5),
            ])),
            WireValue::Union {
                disc: if n % 2 == 0 { 0 } else { 7 },
                value: Box::new(if n % 2 == 0 {
                    WireValue::Record(Box::from([]))
                } else {
                    WireValue::Int(n as i64)
                }),
            },
        ])),
        value: None,
    }
}

fn block_of(schema: &Schema, facts: &[WireFact], predicate: PredicateId) -> Vec<u8> {
    let mut out = Vec::new();
    encode_block(&mut out, schema, predicate, facts).expect("the fixture encodes");
    out
}

fn db() -> (tempfile::TempDir, FjallDb) {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let db = FjallDb::open(dir.path()).expect("a database");
    db.create_predicates([FILE, DECL, DOC]).expect("trees");
    (dir, db)
}

/// Every row of every predicate, as bytes, in a stable order.
fn rows(db: &FjallDb) -> Vec<(u32, Vec<u8>, Vec<u8>)> {
    let reader = db.reader();
    let mut out: Vec<(u32, Vec<u8>, Vec<u8>)> = fjord_store_mem::dump::rows_of(&reader, 3)
        .expect("the walk runs")
        .into_iter()
        .map(|row| (row.predicate.0, row.key, row.value))
        .collect();

    out.sort();
    out
}

/// **Both paths, same block, same rows.**
///
/// Compared on the stored bytes rather than on the ids: an id is allocation order and
/// two runs of anything would agree on it, where the key is what a reader finds a row
/// by and the value is what it holds.
#[test]
fn the_fused_path_stores_what_the_ordinary_one_stores() {
    let schema = schema();

    for nested in [false, true] {
        let facts: Vec<WireFact> = (0..64).map(|n| decl_fact(n, nested)).collect();
        let decls = block_of(&schema, &facts, DECL);

        // A second block over a *different* predicate, with a value side, so the
        // value buffer and the reference-to-a-record shape are both exercised.
        let docs: Vec<WireFact> = (0..16)
            .map(|n| WireFact {
                predicate: DOC,
                key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
                    decl_fact(n, true),
                )))])),
                value: Some(WireValue::Record(Box::from([WireValue::Str(format!(
                    "documentation for {n}"
                ))]))),
            })
            .collect();
        let docs = block_of(&schema, &docs, DOC);

        let (_ordinary_dir, ordinary) = db();
        let (_fused_dir, fused) = db();

        let mut scratch = Scratch::new();

        for block in [&decls, &docs] {
            let a = intern_block(&ordinary, &schema, block).expect("the ordinary path");
            let b = fuse_block(&fused, &schema, block, &mut scratch).expect("the fused path");

            assert_eq!(
                (a.created, a.deduped),
                (b.created, b.deduped),
                "nested={nested}: the two paths disagree on what they wrote"
            );
            assert_eq!(a.ids, b.ids, "nested={nested}: the ids differ");
        }

        assert_eq!(
            rows(&ordinary),
            rows(&fused),
            "nested={nested}: the stored bytes differ"
        );

        // The pool stops growing: two buffers a level, and the data here is two deep.
        assert!(
            scratch.buffers() <= 8,
            "the buffer pool grew to {}",
            scratch.buffers()
        );
    }
}

/// **Both paths refuse an over-long key, and neither panics.**
///
/// The reference path refuses inside `encode_key`, which returns a `Result`. The fused
/// walk writes into a buffer and never calls it, so it has to make the same refusal
/// itself — and until it did, this fact reached the backend's `assert!`, panicked a
/// write worker and poisoned the merge lock behind it. A differential guard that only
/// compared successes would have called that agreement.
#[test]
fn both_paths_refuse_a_key_the_store_cannot_hold() {
    use fjord_ingest::error::IngestError;

    let schema = schema();

    // `src.File` is a bare string key, so the key is the string.
    let fact = WireFact {
        predicate: FILE,
        key: WireValue::Str("x".repeat(70_000)),
        value: None,
    };
    let block = block_of(&schema, std::slice::from_ref(&fact), FILE);

    let (_ordinary_dir, ordinary) = db();
    let (_fused_dir, fused) = db();
    let mut scratch = Scratch::new();

    let a = intern_block(&ordinary, &schema, &block);
    let b = fuse_block(&fused, &schema, &block, &mut scratch);

    assert!(
        matches!(a, Err(IngestError::Codec { .. })),
        "the reference path refuses: {a:?}"
    );
    assert!(
        matches!(b, Err(IngestError::Codec { .. })),
        "the fused path refuses: {b:?}"
    );

    // And the refusal names the limit, since making the key shorter is the only thing
    // a producer can do about it.
    let why = b.expect_err("refused").to_string();
    assert!(why.contains("a stored key may be"), "{why}");
}
