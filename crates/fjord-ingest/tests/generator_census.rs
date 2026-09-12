//! **What the generator actually reaches** — before trusting a property built on it.
//!
//! `fused_agrees_always` passed with a deliberate bug in it: the one the hand-written
//! fixture caught, a value's top-level record written flat like a key's. A property
//! that cannot fail is worse than no property, so this counts the shapes in a run of
//! draws and says which ones are rare enough to be absent from a hundred cases.

use fjord_schema::schema::{PredicateTy, Schema};
use fjord_wire::value::proptest::arb_schema_and_fact;
use proptest::{
    strategy::{Strategy, ValueTree},
    test_runner::TestRunner,
};

#[derive(Default, Debug)]
struct Census {
    draws: usize,
    with_a_value_side: usize,
    value_is_a_record: usize,
    key_is_a_record: usize,
    holds_bytes: usize,
    holds_a_union: usize,
    holds_a_reference: usize,
}

fn walks(ty: &PredicateTy, seen: &mut (bool, bool, bool)) {
    match ty {
        PredicateTy::Bytes => seen.0 = true,
        PredicateTy::Union(alts) => {
            seen.1 = true;
            for alt in alts.iter() {
                walks(&alt.ty, seen);
            }
        }
        PredicateTy::Fact(_) => seen.2 = true,
        PredicateTy::Record(fields) => {
            for (_, field) in fields.iter() {
                walks(field, seen);
            }
        }
        PredicateTy::Int | PredicateTy::Str => {}
    }
}

fn count(schema: &Schema, census: &mut Census) {
    let mut seen = (false, false, false);

    for index in 0..schema.len() {
        let id = fjord_schema::schema::PredicateId(index as u32);
        let Some(declared) = schema.get(id) else {
            continue;
        };
        let declared = declared.predicate();

        if matches!(declared.key, PredicateTy::Record(_)) {
            census.key_is_a_record += 1;
        }

        walks(&declared.key, &mut seen);

        if let Some(value) = &declared.value {
            census.with_a_value_side += 1;

            // **The shape the bug lived in.** A key's top-level record is written flat
            // and a value's is framed, so only a value that *is* a record can tell the
            // two rules apart.
            if matches!(value, PredicateTy::Record(_)) {
                census.value_is_a_record += 1;
            }

            walks(value, &mut seen);
        }
    }

    census.holds_bytes += usize::from(seen.0);
    census.holds_a_union += usize::from(seen.1);
    census.holds_a_reference += usize::from(seen.2);
}

/// Run with `-- --nocapture` to read the numbers.
#[test]
fn the_generator_reaches_the_shapes_the_property_needs() {
    const DRAWS: usize = 500;

    let mut runner = TestRunner::deterministic();
    let mut census = Census::default();

    for _ in 0..DRAWS {
        let drawn = arb_schema_and_fact()
            .new_tree(&mut runner)
            .expect("a draw")
            .current();

        census.draws += 1;
        count(&drawn.schema(), &mut census);
    }

    println!("\n{DRAWS} draws:");
    println!("  schemas holding bytes        {:>5}", census.holds_bytes);
    println!("  schemas holding a union      {:>5}", census.holds_a_union);
    println!(
        "  schemas holding a reference  {:>5}",
        census.holds_a_reference
    );
    println!(
        "  predicates with a key record {:>5}",
        census.key_is_a_record
    );
    println!(
        "  predicates with a value side {:>5}",
        census.with_a_value_side
    );
    println!(
        "  ...whose value is a record   {:>5}   <- the shape the bug lived in",
        census.value_is_a_record
    );

    // The three that a differential property most needs to distinguish the paths.
    assert!(census.holds_bytes > 0, "no draw held bytes: {census:?}");
    assert!(census.holds_a_union > 0, "no draw held a union: {census:?}");
    assert!(
        census.holds_a_reference > 0,
        "no draw held a reference: {census:?}"
    );
}
