//! **The fused walk allocates nothing per fact.**
//!
//! Against a sink that stores nothing, so the number is the walk's and not fjall's.
//! `N` and `2N` allocate the same, which is the technique
//! [I9](../../../website/content/invariants.md#i9)'s own guard uses: equal counts mean
//! nothing scales with the fact.
//!
//! What the walk costs *with* a store, and what it saves against the reference
//! implementation, is `examples/write_allocations.rs` — an instrument, not this.

use fjord_ingest::fused::{Scratch, fuse_block};
use fjord_schema::{
    id::FactId,
    schema::{Predicate, PredicateId, PredicateTy, Schema},
};
use fjord_wire::{WireFact, WireRef, WireValue, encode_block};
use lasso::Rodeo;
use std::sync::Arc;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);

fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let (file, decl) = (
        rodeo.get_or_intern("src.File"),
        rodeo.get_or_intern("src.Decl"),
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
        ]),
    )
}

fn block_of(schema: &Schema, count: usize, nested: bool) -> Vec<u8> {
    let facts: Vec<WireFact> = (0..count)
        .map(|n| WireFact {
            predicate: DECL,
            key: WireValue::Record(Box::from([
                WireValue::Ref(if nested {
                    WireRef::Nested(Box::new(WireFact {
                        predicate: FILE,
                        key: WireValue::Str(format!("src/module{}/file{}.rs", n % 8, n % 64)),
                        value: None,
                    }))
                } else {
                    WireRef::Id(FactId::new(FILE, 1 + (n as u64 % 64)).expect("an id"))
                }),
                WireValue::Int(n as i64),
                WireValue::Str(format!("declaration_number_{n}")),
            ])),
            value: None,
        })
        .collect();

    let mut out = Vec::new();
    encode_block(&mut out, schema, DECL, &facts).expect("the fixture encodes");
    out
}

/// A sink that stores nothing, so a measurement against it is the **walk alone**.
///
/// The fused path's remaining allocations are fjall's — the index key it builds, the
/// batch, the cache entry — and none of them are the codec's to remove. This is what
/// separates the two.
struct Nowhere {
    seen: std::cell::Cell<u64>,
}

impl fjord_ingest::sink::FactSink for Nowhere {
    fn resolve_or_create(
        &self,
        predicate: PredicateId,
        _key_fields: &[u8],
        _value: &[u8],
        _keyed_only: bool,
    ) -> Result<fjord_store_fjall::store::Interned, fjord_ingest::error::IngestError> {
        let next = self.seen.get() + 1;
        self.seen.set(next);

        Ok(fjord_store_fjall::store::Interned {
            id: FactId::new(predicate, next).expect("an id"),
            created: true,
        })
    }
}

/// **The walk on its own allocates nothing per fact.**
///
/// Against a sink that stores nothing, the only allocations left are the buffer pool
/// filling — a handful, once — and after that a block of any size costs the same.
#[test]
fn the_walk_alone_is_allocation_free_per_fact() {
    let control = allocation_counter::measure(|| {
        std::hint::black_box(Vec::<u8>::with_capacity(4096));
    });
    assert!(
        control.count_total > 0,
        "no counting allocator: {control:?}"
    );

    let schema = schema();
    let mut scratch = Scratch::new();

    for nested in [false, true] {
        let shape = if nested { "nested" } else { "by id " };

        // N and 2N, the I9 technique: equal counts mean nothing scales with the row.
        let n = block_of(&schema, 1_000, nested);
        let two_n = block_of(&schema, 2_000, nested);

        // Warm the pool first, or its filling is what the first measurement sees.
        let sink = Nowhere {
            seen: std::cell::Cell::new(0),
        };
        fuse_block(&sink, &schema, &n, &mut scratch).expect("warm-up");

        let at_n = allocation_counter::measure(|| {
            std::hint::black_box(fuse_block(&sink, &schema, &n, &mut scratch).expect("n"));
        });
        let at_2n = allocation_counter::measure(|| {
            std::hint::black_box(fuse_block(&sink, &schema, &two_n, &mut scratch).expect("2n"));
        });

        println!(
            "\n  {shape}  walk alone:  1,000 facts {:>4} allocs {:>7} bytes   |   2,000 facts {:>4} allocs {:>7} bytes",
            at_n.count_total, at_n.bytes_total, at_2n.count_total, at_2n.bytes_total,
        );

        // `Ingested::ids` grows with the fact count and is the caller's answer, not the
        // walk's cost — so the two differ by its reallocations and nothing else.
        assert!(
            at_2n.count_total < at_n.count_total + 40,
            "{shape}: allocations track the fact count: {} then {}",
            at_n.count_total,
            at_2n.count_total
        );
    }
}
