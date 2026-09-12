//! **The fused path and the ordinary one agree on any schema** — a property, not a
//! fixture.
//!
//! `fused_agrees.rs` names the shapes by hand and is worth keeping for that: it says
//! which constructors are covered. What it cannot do is combine them in ways nobody
//! thought of, and that is exactly the gap that let a `bytes` field be written as hex
//! and read as base64 for a day earlier in this round — a guard whose fixture could not
//! express the fault.
//!
//! So this draws the schema as well as the facts.
//! [`fjord_wire::value::proptest::arb_schema_and_fact`] generates up to four predicates
//! of records, unions, references, ints, strings and bytes, nested three deep, with
//! union discriminants deliberately out of declaration order and text drawn from the
//! edges — the empty string, embedded NULs, an emoji. Both paths ingest the same block
//! into stores of their own, and **every stored row is compared byte for byte**.
//!
//! The comparison is the stored bytes rather than the ids: an id is allocation order,
//! which two runs of anything would agree on, where a key is what a reader finds a row
//! by and a value is what it holds.

use fjord_ingest::{
    fused::{Scratch, fuse_block},
    intern_block,
};
use fjord_schema::schema::{PredicateId, Schema};
use fjord_store_fjall::store::FjallDb;
use fjord_wire::{WireFact, encode_block, value::proptest::arb_schema_and_fact};
use proptest::prelude::*;

/// A store with every predicate's trees already made, so a failure is the walk's and
/// not lazy keyspace creation's.
fn db(schema: &Schema) -> (tempfile::TempDir, FjallDb) {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let db = FjallDb::open(dir.path()).expect("a database");

    db.create_predicates((0..schema.len()).map(|n| PredicateId(n as u32)))
        .expect("trees");

    (dir, db)
}

/// Every row of every predicate, as bytes, in a stable order.
fn rows(db: &FjallDb, schema: &Schema) -> Vec<(u32, u64, Vec<u8>, Vec<u8>)> {
    let reader = db.reader();
    let mut out: Vec<(u32, u64, Vec<u8>, Vec<u8>)> =
        fjord_store_mem::dump::rows_of(&reader, u32::try_from(schema.len()).unwrap_or(u32::MAX))
            .expect("the walk runs")
            .into_iter()
            .map(|row| (row.predicate.0, row.sequence, row.key, row.value))
            .collect();

    out.sort();
    out
}

proptest! {
    // **Each case builds two real stores in two temp directories**, so a case is
    // milliseconds rather than microseconds and the count is a budget rather than a
    // preference. Thirty-two runs in about nineteen seconds and still combines the
    // constructors; `generator_census` is what says the shapes are reached at all,
    // which is the job a larger number would otherwise be doing badly.
    //
    // Raise it when changing the walk — `PROPTEST_CASES=512 cargo test …` needs no
    // edit here.
    #![proptest_config(ProptestConfig {
        cases: 32,
        max_shrink_iters: 128,
        failure_persistence: None,
        ..ProptestConfig::default()
    })]

    /// **Any schema, any fact: the same rows, byte for byte.**
    ///
    /// The block holds the drawn fact **three times**, which is not padding: the first
    /// creates and the rest deduplicate, so the two paths have to agree about what was
    /// already there as well as about what to write. A fused path that produced a key
    /// one byte different would create three rows where the ordinary one created one,
    /// and the counts would say so before the bytes did.
    #[test]
    fn the_two_paths_agree_on_any_schema(drawn in arb_schema_and_fact()) {
        let schema = drawn.schema();
        let predicate = drawn.predicate_id();
        let fact: WireFact = drawn.fact(&schema);

        let facts = [fact.clone(), fact.clone(), fact];

        let mut block = Vec::new();
        // A fact the generator drew that this build will not encode — an over-long
        // key, say — is the encoder's business and not this guard's: skip the case
        // rather than assert about something it is not comparing.
        let Ok(()) = encode_block(&mut block, &schema, predicate, &facts) else {
            return Ok(());
        };

        let (_ordinary_dir, ordinary) = db(&schema);
        let (_fused_dir, fused) = db(&schema);

        let a = intern_block(&ordinary, &schema, &block);
        let mut scratch = Scratch::new();
        let b = fuse_block(&fused, &schema, &block, &mut scratch);

        match (a, b) {
            (Ok(a), Ok(b)) => {
                prop_assert_eq!(
                    (a.created, a.deduped),
                    (b.created, b.deduped),
                    "the paths disagree on what they wrote"
                );
                prop_assert_eq!(a.ids, b.ids, "the paths gave different ids");
                prop_assert_eq!(
                    rows(&ordinary, &schema),
                    rows(&fused, &schema),
                    "the stored bytes differ"
                );
            }

            // **A refusal has to be a refusal on both sides.** A fused path that
            // accepted what the ordinary one rejects would write a fact the database
            // has decided it does not hold, which is worse than being slow.
            (Err(a), Err(b)) => {
                prop_assert_eq!(
                    std::mem::discriminant(&a),
                    std::mem::discriminant(&b),
                    "both refused, for different reasons: {:?} against {:?}",
                    a,
                    b
                );
            }

            (a, b) => prop_assert!(
                false,
                "one path refused and the other did not: {:?} against {:?}",
                a.err(),
                b.err()
            ),
        }
    }
}
