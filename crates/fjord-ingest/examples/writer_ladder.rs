//! **EXPERIMENT, NOT FOR MERGE** — throughput against concurrent writers, in process.
//!
//! The end-to-end ladder peaks at four writers and declines after, and the suspect is
//! `fjall::WriteBatch::commit`: it takes one global journal writer and holds it across
//! the journal append, the lz4 compression of that append, **and the memtable insert
//! for every row in the batch**, releasing it only after `snapshot_tracker.publish`.
//! If that is the ceiling then it is CPU-bound serialisation, and no amount of writers
//! helps.
//!
//! This measures the store with nothing else in the way — no socket, no tokio, no
//! client host — so the shape is attributable. Absolute numbers are not comparable
//! with an end-to-end ladder; the *shape* and the *deltas between arms* are the point.
//!
//! **Fixed total work, varying parallelism.** Every writer count moves the same number
//! of facts, so a flat line is perfect scaling's opposite and a rising one is a real
//! gain.
//!
//! **The arms are interleaved and repeated**, because the first pass at 400k facts put
//! every arm inside ±3% of every other and arm C — both changes — came out below both
//! of its halves, which is what noise looks like rather than an effect. Alternating the
//! arms under one process at each writer count is what stops host drift being read as
//! a result, and each cell is the best of `REPS` passes.
//!
//! **Volume matters for one of the arms.** Flush workers only constrain anything once
//! memtables rotate repeatedly, so a ladder that writes 50 MB cannot see them however
//! many times it is repeated. The default here is deliberately past that.
//!
//! ```sh
//! cargo run --release -p fjord-ingest --example writer_ladder
//! TOTAL=4000000 REPS=3 WRITERS=1,4,16,64 cargo run --release … --example writer_ladder
//! ```

use fjord_ingest::fused::{Scratch, fuse_block};
use fjord_schema::schema::{Predicate, PredicateId, PredicateTy, Schema};
use fjord_store_fjall::store::FjallDb;
use fjord_wire::{WireFact, WireValue, encode_block};
use lasso::Rodeo;
use std::sync::{Arc, Barrier};
use std::time::Instant;

const FILE: PredicateId = PredicateId(0);

/// One predicate, a bare string key. Two rows a fact — the `keys` row and the
/// `entities` row — which is what every fact costs however deep the schema is, and
/// they are what the journal lock is held across.
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

/// Blocks of disjoint keys for one writer, encoded up front so the timed region is the
/// store and the fused walk rather than the fixture.
fn blocks(schema: &Schema, writer: usize, facts: usize, block: usize) -> Vec<Vec<u8>> {
    let mut out = Vec::new();
    let mut at = 0;

    while at < facts {
        let take = block.min(facts - at);
        let group: Vec<WireFact> = (at..at + take)
            .map(|n| WireFact {
                predicate: FILE,
                key: WireValue::Str(format!("w{writer}/src/module{}/file{n}.rs", n % 64)),
                value: None,
            })
            .collect();

        let mut bytes = Vec::new();
        encode_block(&mut bytes, schema, FILE, &group).expect("the fixture encodes");
        out.push(bytes);
        at += take;
    }

    out
}

/// One pass: build a store under `arm`, move `total` facts through `writers` threads,
/// and return the rate.
fn pass(schema: &Arc<Schema>, work: &[Vec<Vec<u8>>]) -> (f64, u64) {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let db = Arc::new(FjallDb::open(dir.path()).expect("a database"));
    db.create_predicates([FILE]).expect("trees");

    // Every writer waits for the last one to be ready, so the measurement is the
    // steady state rather than the thread pool starting.
    let gate = Arc::new(Barrier::new(work.len() + 1));
    let mut threads = Vec::with_capacity(work.len());

    for mine in work {
        let (db, schema, gate, mine) = (db.clone(), schema.clone(), gate.clone(), mine.clone());
        threads.push(std::thread::spawn(move || {
            let mut scratch = Scratch::new();
            gate.wait();

            for block in &mine {
                // What the server does under `--commit-per-block`.
                let staged = db.staged();
                fuse_block(&staged, &schema, block, &mut scratch).expect("the block ingests");
                staged.commit().expect("the block commits");
            }
        }));
    }

    gate.wait();
    let started = Instant::now();
    for thread in threads {
        thread.join().expect("a writer finished");
    }
    let elapsed = started.elapsed().as_secs_f64();

    // What the store says about its own parallelism, rather than what the curve
    // implies: `peak` reaches 1 however many writers a serialised path has.
    let (_, peak) = db.intern_concurrency();
    (elapsed, peak)
}

fn main() {
    let total: usize = var("TOTAL", 2_000_000);
    let block: usize = var("BLOCK", 1_000);
    let reps: usize = var("REPS", 2);

    let ladder: Vec<usize> = std::env::var("WRITERS")
        .ok()
        .map(|s| s.split(',').filter_map(|n| n.trim().parse().ok()).collect())
        .unwrap_or_else(|| vec![1, 2, 4, 8, 16, 32, 64]);

    let schema = Arc::new(schema());

    println!(
        "{} facts a point, {block} a block, one commit a block, best of {reps}, {} cores",
        thousands(total as u64),
        std::thread::available_parallelism().map_or(0, usize::from),
    );
    println!("one build; A/B against the other build of fjall\n");

    println!("  writers      facts/s   ms   speedup   intern peak");

    let mut single = 0.0f64;

    for &writers in &ladder {
        let each = total / writers;
        let work: Vec<Vec<Vec<u8>>> = (0..writers)
            .map(|w| blocks(&schema, w, each, block))
            .collect();

        let mut best = f64::MAX;
        let mut seen = 0u64;
        for _ in 0..reps {
            let (elapsed, peak) = pass(&schema, &work);
            best = best.min(elapsed);
            seen = seen.max(peak);
        }

        let rate = (each * writers) as f64 / best;
        if writers == ladder[0] {
            single = rate;
        }

        println!(
            "  {writers:>7}  {:>11}  {:>4.0}   {:>6.2}x   {seen:>4}",
            thousands(rate as u64),
            best * 1000.0,
            rate / single,
        );
    }
}

fn var<T: std::str::FromStr>(name: &str, fallback: T) -> T {
    std::env::var(name)
        .ok()
        .and_then(|v| v.parse().ok())
        .unwrap_or(fallback)
}

fn thousands(n: u64) -> String {
    let digits = n.to_string();
    let mut out = String::new();
    for (at, ch) in digits.chars().enumerate() {
        if at > 0 && (digits.len() - at) % 3 == 0 {
            out.push(',');
        }
        out.push(ch);
    }
    out
}
