//! **What the write path allocates, and what fusing the two hops saves.**
//!
//! An instrument, not a guard: it prints, and its only assertions stop it reporting
//! numbers from a probe that was not measuring anything. The claims it supports are
//! guarded elsewhere — [`fused_agrees`] and [`fused_agrees_always`] for the fused
//! walk being byte-identical, [`fused_is_allocation_free`] for the walk costing
//! nothing per fact.
//!
//! ```sh
//! cargo run --release -p fjord-ingest --example write_allocations
//! ```
//!
//! Release, because the reference path's cost is a tree of small allocations and a
//! debug build prices those quite differently from a shipped one.
//!
//! # The four sections, and what each one separates
//!
//! 1. **the hops** — wire bytes become storage bytes through three representations,
//!    not two: [`block::decode_block`] builds a `Vec<WireFact>` owning a `String` per
//!    string field, `intern`'s `resolve` walks that to build the codec's `Value`
//!    owning the same strings again, and only then does `encode_key` produce the bytes
//!    that are kept. Nothing but the third survives the fact.
//! 2. **the read path, for comparison** — measured over [`FrozenStore`], the
//!    allocation-free store the engine's own I9 guard reads from. Not `MemStore`: its
//!    scan clones a `Vec` per row, which would put the model store's cost in a number
//!    about the engine.
//! 3. **what fusing saves** — both paths over the same block into real fjall stores.
//!    Both totals include the store, which is most of the work and identical either
//!    way, so read the *difference*.
//! 4. **where the rest of it is** — [`FactSink::resolve_or_create`] asked directly,
//!    with the key bytes already in hand, so nothing above it is in the number.
//!
//! [`fused_agrees`]: ../../tests/fused_agrees.rs
//! [`fused_agrees_always`]: ../../tests/fused_agrees_always.rs
//! [`fused_is_allocation_free`]: ../../tests/fused_is_allocation_free.rs
//! [`FrozenStore`]: fjord_store::fixtures::FrozenStore

use fjord_encoding::tuple::{Value, encode_key};
use fjord_ingest::{
    fused::{Scratch, fuse_block},
    intern_block,
    sink::FactSink,
};
use fjord_schema::{
    id::FactId,
    schema::{Predicate, PredicateId, PredicateTy, Schema},
};
use fjord_store_fjall::store::FjallDb;
use fjord_wire::{WireFact, WireRef, WireValue, block, encode_block};
use lasso::Rodeo;
use std::sync::Arc;

const FILE: PredicateId = PredicateId(0);
const DECL: PredicateId = PredicateId(1);

/// `src.File : string` and `src.Decl : { file : File, line : int, name : string }` —
/// a reference in a key, which is the shape that forces the walk's order and the one
/// a fused path has to keep working.
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

/// `count` declarations naming the file they belong to — **nested**, the shape an
/// indexer sends where a reference travels as the whole target fact, or **by id**,
/// what a producer that interns for itself sends and the cheaper of the two.
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

fn db(predicates: &[PredicateId]) -> (tempfile::TempDir, FjallDb) {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let store = FjallDb::open(dir.path()).expect("a database");
    store
        .create_predicates(predicates.iter().copied())
        .expect("trees");
    (dir, store)
}

fn report(what: &str, per: usize, info: allocation_counter::AllocationInfo) {
    println!(
        "  {what:<30} {:>9} allocs {:>11} bytes   |  each {:>6.2} allocs {:>8.1} bytes",
        info.count_total,
        info.bytes_total,
        info.count_total as f64 / per as f64,
        info.bytes_total as f64 / per as f64,
    );
}

/// The probe is a dev-dependency and installs its own `#[global_allocator]`. If that
/// wiring breaks it reports zeroes, and every section below would read as a triumph.
fn probe_is_installed() {
    let control = allocation_counter::measure(|| {
        std::hint::black_box(Vec::<u8>::with_capacity(4096));
    });
    assert!(
        control.count_total > 0,
        "the counting allocator is not installed: {control:?}"
    );
}

fn main() {
    probe_is_installed();

    let schema = schema();

    the_hops(&schema);
    the_read_path();
    what_fusing_saves(&schema);
    where_the_stores_allocations_are();
}

// ---------------------------------------------------------------------------------
// 1. The hops nothing keeps.
// ---------------------------------------------------------------------------------

fn the_hops(schema: &Schema) {
    const FACTS: usize = 2_000;

    for nested in [true, false] {
        let bytes = block_of(schema, FACTS, nested);
        let shape = if nested {
            "nested (an indexer's shape)"
        } else {
            "by id (a self-interning one)"
        };
        println!("\n{shape}, {FACTS} facts, {} wire bytes", bytes.len());

        let decoded = allocation_counter::measure(|| {
            let (facts, _) = block::decode_block(&bytes, schema).expect("it decodes");
            std::hint::black_box(&facts);
        });
        report("decode_block", FACTS, decoded);

        let (facts, _) = block::decode_block(&bytes, schema).expect("it decodes");

        // Only the top-level key, and only for a fact whose reference is already an
        // id: encoding a nested one needs a store to intern against, which would put
        // fjall's own allocations in the number.
        let flat: Vec<&WireFact> = facts
            .iter()
            .filter(|fact| {
                matches!(&fact.key, WireValue::Record(f) if matches!(
                    f.first(), Some(WireValue::Ref(WireRef::Id(_)))))
            })
            .collect();

        if flat.is_empty() {
            continue;
        }

        let declared = schema.get(DECL).expect("declared").predicate().key.clone();
        let values: Vec<Value> = flat.iter().map(|fact| as_value(&fact.key)).collect();

        let encoded = allocation_counter::measure(|| {
            for value in &values {
                std::hint::black_box(encode_key(&declared, value).expect("it encodes"));
            }
        });
        report("encode_key (top level only)", flat.len(), encoded);
    }
}

/// A `WireValue` as the codec's `Value`, which is what `encode_key` takes.
///
/// **This conversion is itself part of the cost being measured**: the wire type and
/// the codec type are two spellings of one thing, and the reference path walks the
/// first to build the second before the encoder walks *that*.
fn as_value(wire: &WireValue) -> Value {
    match wire {
        WireValue::Int(n) => Value::Int(*n),
        WireValue::Str(s) => Value::Str(s.clone()),
        WireValue::Bytes(b) => Value::Bytes(b.clone()),
        WireValue::Ref(WireRef::Id(id)) => Value::FactRef(*id),
        WireValue::Ref(WireRef::Nested(_)) => {
            unreachable!("filtered out above: a nested reference needs a store")
        }
        WireValue::Record(fields) => Value::Record(
            fields
                .iter()
                .enumerate()
                .map(|(at, field)| (format!("f{at}"), as_value(field)))
                .collect(),
        ),
        // The wire carries a union's *discriminant*; the codec's `Value` wants the
        // alternative's name too, which only the schema has — another place the two
        // spellings do not line up. This fixture declares no union, so it is unreached.
        WireValue::Union { .. } => unreachable!("this fixture declares no union"),
    }
}

// ---------------------------------------------------------------------------------
// 2. The read path, for comparison.
// ---------------------------------------------------------------------------------

fn seeded_store(count: usize) -> fjord_store::fixtures::FrozenStore {
    fjord_store::fixtures::FrozenStore::from_keys(
        FILE,
        (0..count).map(|n| {
            (
                encode_key(
                    &PredicateTy::Str,
                    &Value::Str(format!("declaration_number_{n}")),
                )
                .expect("it encodes"),
                1 + n as u64,
            )
        }),
    )
}

/// A scan over every row of `FILE`.
fn scanning_plan(project: fjord_engine::plan::Project) -> fjord_engine::plan::Plan {
    use fjord_engine::plan::{Access, Address, Level, Plan, SeekKey, Step};

    Plan {
        nvars: 1,
        body: Step::levels([Level::seek(
            Access {
                predicate_id: FILE,
                seek_key: SeekKey::Prefix(Box::new([])),
            },
            Box::new([Address::new(0)]),
            Box::new([]),
        )]),
        head: project,
    }
}

fn the_read_path() {
    const ROWS: usize = 2_000;

    let interner = fjord_schema::schema::LocalInterner::new(
        fjord_schema::schema::SchemaInterner::new(Rodeo::new().into_reader()),
    );

    println!("\nthe read path, {ROWS} rows");

    // **The hot path with nothing retained.** A fold that counts rather than collects
    // — which is what `QUERY_COUNT` does over the wire, and the closest a read gets to
    // the write path's "transform and move on".
    let store = std::hint::black_box(seeded_store(ROWS));
    let counted = allocation_counter::measure(move || {
        let plan = scanning_plan(fjord_engine::plan::Project::FactRef(
            fjord_engine::plan::Address::new(0),
        ));
        let executor = fjord_engine::iter::Executor::new(store, plan);
        let n = executor
            .enumerate(
                0u64,
                |acc, _row| Ok(fjord_engine::iter::Stream::Continue(acc + 1)),
                &tokio_util::sync::CancellationToken::new(),
            )
            .expect("it runs");
        std::hint::black_box(n);
    });
    report("scan + count (nothing kept)", ROWS, counted);

    // **The escape boundary.** A string projection is the one thing I9 says copies, and
    // it is the fair comparison with the write path: one fact in, one owned thing out.
    let store = std::hint::black_box(seeded_store(ROWS));
    let projected = allocation_counter::measure(move || {
        let plan = scanning_plan(fjord_engine::plan::Project::RegisterField {
            address: fjord_engine::plan::Address::new(0),
            path: fjord_engine::plan::FieldPath::field(0),
            ty: PredicateTy::Str,
        });
        let executor = fjord_engine::iter::Executor::new(store, plan);
        let rows = executor
            .enumerate(
                Vec::new(),
                |mut acc: Vec<Value>, mut row| {
                    acc.push(row.to_value(&interner)?);
                    Ok(fjord_engine::iter::Stream::Continue(acc))
                },
                &tokio_util::sync::CancellationToken::new(),
            )
            .expect("it runs");
        std::hint::black_box(rows);
    });
    report("scan + project a string", ROWS, projected);
}

// ---------------------------------------------------------------------------------
// 3. What fusing the two hops saves.
// ---------------------------------------------------------------------------------

fn what_fusing_saves(schema: &Schema) {
    const FACTS: usize = 4_000;

    for nested in [false, true] {
        let block = block_of(schema, FACTS, nested);
        let shape = if nested { "nested" } else { "by id " };

        let (_a_dir, a) = db(&[FILE, DECL]);
        let reference = allocation_counter::measure(|| {
            std::hint::black_box(intern_block(&a, schema, &block).expect("the reference path"));
        });

        let (_b_dir, b) = db(&[FILE, DECL]);
        let mut scratch = Scratch::new();
        // One pass first, so the pool is warm and the number is the steady state
        // rather than the pool filling.
        let (_warm_dir, warm) = db(&[FILE, DECL]);
        fuse_block(&warm, schema, &block, &mut scratch).expect("warm-up");

        let fused = allocation_counter::measure(|| {
            std::hint::black_box(fuse_block(&b, schema, &block, &mut scratch).expect("fused"));
        });

        println!("\n{shape}, {FACTS} facts, {} wire bytes", block.len());
        report("reference (decode, then encode)", FACTS, reference);
        report("fused", FACTS, fused);
        println!(
            "  saved                          {:>9} allocs {:>11} bytes   |  {:.0}% of allocations, {:.0}% of bytes",
            reference.count_total as i64 - fused.count_total as i64,
            reference.bytes_total as i64 - fused.bytes_total as i64,
            100.0 * (reference.count_total as f64 - fused.count_total as f64)
                / reference.count_total as f64,
            100.0 * (reference.bytes_total as f64 - fused.bytes_total as f64)
                / reference.bytes_total as f64,
        );
        println!("  pool grew to {} buffer(s)", scratch.buffers());

        let mut best_reference = f64::MAX;
        let mut best_fused = f64::MAX;

        for _ in 0..5 {
            let (_dir, store) = db(&[FILE, DECL]);
            let started = std::time::Instant::now();
            intern_block(&store, schema, &block).expect("the reference path");
            best_reference = best_reference.min(started.elapsed().as_secs_f64());

            let (_dir, store) = db(&[FILE, DECL]);
            let mut scratch = Scratch::new();
            let started = std::time::Instant::now();
            fuse_block(&store, schema, &block, &mut scratch).expect("fused");
            best_fused = best_fused.min(started.elapsed().as_secs_f64());
        }

        println!(
            "  time, best of five             reference {:>7.1} ms   fused {:>7.1} ms   {:+.1}%",
            best_reference * 1000.0,
            best_fused * 1000.0,
            100.0 * (best_fused - best_reference) / best_reference,
        );
    }
}

// ---------------------------------------------------------------------------------
// 4. Where the rest of it is: below the codec, in the store.
// ---------------------------------------------------------------------------------

/// Three cases, because they cost differently and the ratio between them is the whole
/// shape of an ingest:
///
/// - **create** — the key is new: a cache miss, a tree read that finds nothing, and a
///   commit.
/// - **dedup** — the key is in the interning cache: no tree read at all. A real index
///   is mostly this one, since a single `src.File` is named by a hundred thousand
///   facts.
/// - **create, staged** — what the server does under `--commit-per-block`: one batch
///   for the whole block rather than one per fact.
///
/// There is no cold-dedup row. Nothing here empties the cache, and a second handle on
/// the same directory would fight `ops-I1`'s lock — so the cold figure is
/// `fjord-cli`'s `examples/ingest` `dedup:cold` rung, which reopens the database to
/// get it, rather than a warm number reported under a cold name.
fn where_the_stores_allocations_are() {
    const FACTS: usize = 2_000;

    let keys: Vec<Vec<u8>> = (0..FACTS)
        .map(|n| {
            encode_key(
                &PredicateTy::Str,
                &Value::Str(format!("src/module{}/file{n}.rs", n % 8)),
            )
            .expect("it encodes")
        })
        .collect();

    println!("\nresolve_or_create, {FACTS} facts, key bytes already in hand");

    let (_dir, store) = db(&[FILE]);
    let created = allocation_counter::measure(|| {
        for key in &keys {
            std::hint::black_box(
                store
                    .resolve_or_create(FILE, key, &[], true)
                    .expect("it interns"),
            );
        }
    });
    report("create", FACTS, created);

    // The same store, the same keys: every one is now in the interning cache.
    let deduped = allocation_counter::measure(|| {
        for key in &keys {
            std::hint::black_box(
                store
                    .resolve_or_create(FILE, key, &[], true)
                    .expect("it interns"),
            );
        }
    });
    report("dedup, from cache", FACTS, deduped);

    let (_staged_dir, staged_store) = db(&[FILE]);
    let staged_total = {
        let staged = staged_store.staged();
        let info = allocation_counter::measure(|| {
            for key in &keys {
                std::hint::black_box(
                    staged
                        .resolve_or_create(FILE, key, &[], true)
                        .expect("it interns"),
                );
            }
        });
        staged.commit().expect("the batch commits");
        info
    };
    report("create, staged", FACTS, staged_total);

    println!(
        "\n  creating costs {:.1} allocations a fact more than deduplicating from cache",
        (created.count_total as f64 - deduped.count_total as f64) / FACTS as f64
    );
}
