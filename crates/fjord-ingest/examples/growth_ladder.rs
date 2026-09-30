//! **EXPERIMENT, NOT FOR MERGE** — write throughput against a database that is
//! *already large*.
//!
//! Every write instrument here measures a fresh database: `examples/ingest.rs` builds
//! one per iteration because ingest is not idempotent, and `writer_ladder` writes a
//! fixed total into an empty store. So the one shape a real ingest has — the index
//! growing under the writer — is measured by nothing, and a rate that decays with size
//! would be invisible to all of them.
//!
//! This writes one long stream of distinct keys into **one** database and reports the
//! rate per chunk, with the counters that would say why a chunk was slower than the one
//! before it: the lookup cache's hits, the live LSM reads interning did, and what fjall
//! holds.
//!
//! `FROM` starts the key numbering above an existing database's, which is how this
//! measures a *reopened* one — the state a server is almost always in, and the one a
//! runtime-only keyspace option can silently be missing from.
//!
//! ```sh
//! TOTAL=4000000 CHUNK=250000 cargo run --release -p fjord-ingest --example growth_ladder
//! SCRATCH=/tmp/db FROM=1000000 TOTAL=1000000 cargo run --release … --example growth_ladder
//! ```

use fjord_ingest::fused::{Scratch, fuse_block};
use fjord_schema::schema::{Predicate, PredicateId, PredicateTy, Schema};
use fjord_store_fjall::store::FjallDb;
use fjord_wire::{WireFact, WireValue, encode_block};
use lasso::Rodeo;
use std::sync::Arc;
use std::time::Instant;

const FILE: PredicateId = PredicateId(0);

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

/// One block of distinct keys starting at `from`.
fn block(schema: &Schema, from: usize, take: usize, out: &mut Vec<u8>) {
    let group: Vec<WireFact> = (from..from + take)
        .map(|n| WireFact {
            predicate: FILE,
            key: WireValue::Str(format!("src/module{}/file{n}.rs", n % 64)),
            value: None,
        })
        .collect();

    out.clear();
    encode_block(out, schema, FILE, &group).expect("the fixture encodes");
}

fn main() {
    let total: usize = var("TOTAL", 4_000_000);
    let chunk: usize = var("CHUNK", 250_000);
    let block_size: usize = var("BLOCK", 1_000);
    let from: usize = var("FROM", 0);

    let schema = schema();
    let dir = std::env::var("SCRATCH").map_or_else(
        |_| {
            let dir = tempfile::tempdir().expect("a scratch directory");
            dir.path().to_path_buf()
        },
        std::path::PathBuf::from,
    );
    std::fs::create_dir_all(&dir).expect("a scratch directory");

    let db = FjallDb::open(&dir).expect("a database");
    db.create_predicates([FILE]).expect("trees");

    println!(
        "{} facts, {} a chunk, {block_size} a block, one commit a block",
        thousands(total as u64),
        thousands(chunk as u64),
    );
    println!("scratch {}\n", dir.display());
    println!(
        "{:>12}  {:>10}  {:>9}  {:>9}  {:>10}  {:>10}  {:>8}  {:>8}",
        "written",
        "facts/s",
        "intern ms",
        "commit ms",
        "keys reads",
        "cache hit%",
        "journals",
        "disk MB"
    );

    let mut scratch = Scratch::new();
    let mut bytes = Vec::new();
    let mut at = from;
    let last = from + total;
    let (mut reads_before, mut hits_before, mut misses_before) = (0, 0, 0);

    while at < last {
        let end = (at + chunk).min(last);
        let started = Instant::now();

        let mut cursor = at;
        let (mut interning, mut committing) = (0.0f64, 0.0f64);
        while cursor < end {
            let take = block_size.min(end - cursor);
            block(&schema, cursor, take, &mut bytes);

            let staged = db.staged();
            let fused = Instant::now();
            fuse_block(&staged, &schema, &bytes, &mut scratch).expect("the block ingests");
            interning += fused.elapsed().as_secs_f64();

            let committed = Instant::now();
            staged.commit().expect("the block commits");
            committing += committed.elapsed().as_secs_f64();
            cursor += take;
        }

        let elapsed = started.elapsed().as_secs_f64();
        let (keys_reads, _) = db.intern_read_counters();
        let (hits, misses) = db.lookup_counters();
        let journals = db.journal_count();
        let disk = db.disk_bytes().unwrap_or(0);

        let looked = (hits - hits_before) + (misses - misses_before);
        println!(
            "{:>12}  {:>10}  {:>9.0}  {:>9.0}  {:>10}  {:>9.1}%  {:>8}  {:>8}",
            thousands(end as u64),
            thousands(((end - at) as f64 / elapsed) as u64),
            interning * 1000.0,
            committing * 1000.0,
            thousands(keys_reads - reads_before),
            if looked == 0 {
                0.0
            } else {
                (hits - hits_before) as f64 * 100.0 / looked as f64
            },
            journals,
            disk / (1 << 20),
        );

        reads_before = keys_reads;
        hits_before = hits;
        misses_before = misses;
        at = end;
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
