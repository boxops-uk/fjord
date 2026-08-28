//! The guided seek, end to end and measured.
//!
//! Two halves. The first compiles sigla text and shows the plan and the rows, so
//! the construct is visible from the outside. The second is the number the
//! feature exists for: how many rows a guided seek reads against how many a scan
//! with the same filter reads, over a corpus big enough for the answer to mean
//! something.
//!
//! `cargo run --release -p fjord-engine --features proptest --example e2e_fuzzy`

use std::sync::{
    Arc,
    atomic::{AtomicUsize, Ordering},
};

use byteview::ByteView;
use fjord_encoding::tuple::{Value, put_str};
use fjord_engine::{
    compile::Compilation,
    fixtures::collect_rows,
    plan::{
        Access, Address, FieldPath, Guide, Level, Plan, Project, Residual, ResidualOp, SeekKey,
        Source, Step,
    },
    print,
};
use fjord_schema::{
    id::FactId,
    schema::{LocalInterner, PredicateId, PredicateTy},
};
use fjord_store::{
    error::StoreError,
    fact_store::{Entity, FactStore},
};
use fjord_store_mem::MemStore;

const NAME: PredicateId = PredicateId(5);

fn str_field(s: &str) -> Vec<u8> {
    let mut out = Vec::new();
    put_str(&mut out, s);
    out
}

// ---------------------------------------------------------------------------
// Half one: from sigla text
// ---------------------------------------------------------------------------

/// The shared fixture database — the same facts the corpus runs against, so what
/// this prints is what a person can type at the shell prompt.
fn demo_store() -> MemStore {
    let mut store = MemStore::new();
    for fact in fjord_store::fixture::facts() {
        store.insert_valued(fact.predicate, fact.key, fact.sequence, fact.value);
    }
    store
}

fn run(source: &str) {
    println!("\n  \x1b[1m{source}\x1b[0m");

    let schema = fjord_store::fixture::schema();
    let mut compilation = Compilation::new(source, &schema);

    let Some(plan) = compilation.plan() else {
        let rendered = compilation.render_to_string();
        let first = rendered.lines().next().unwrap_or("").to_owned();
        println!("    refused: {first}");
        return;
    };

    for line in print::plan(&plan, &schema, compilation.interner()).lines() {
        println!("    {line}");
    }

    let interner = LocalInterner::new(schema.interner().clone());
    match collect_rows(demo_store(), plan, &interner) {
        Ok(rows) => {
            let rendered: Vec<String> = rows
                .iter()
                .map(|r| match r {
                    Value::Str(s) => s.to_string(),
                    other => format!("{other:?}"),
                })
                .collect();
            println!("    → {}", rendered.join(", "));
        }
        Err(e) => println!("    ERROR: {e}"),
    }
}

// ---------------------------------------------------------------------------
// Half two: the measurement
// ---------------------------------------------------------------------------

/// Counts every row a scan yields and every scan opened — a hop is a scan.
struct ScanSpy {
    inner: MemStore,
    rows: Arc<AtomicUsize>,
    scans: Arc<AtomicUsize>,
}

struct SpyScan {
    inner: <MemStore as FactStore>::Scan,
    rows: Arc<AtomicUsize>,
}

impl Iterator for SpyScan {
    type Item = Result<(ByteView, FactId), StoreError>;

    fn next(&mut self) -> Option<Self::Item> {
        let row = self.inner.next();
        if row.is_some() {
            self.rows.fetch_add(1, Ordering::Relaxed);
        }
        row
    }
}

impl FactStore for ScanSpy {
    type Scan = SpyScan;

    fn scan(&self, lo: &[u8], hi: Option<&[u8]>) -> Result<SpyScan, StoreError> {
        self.scans.fetch_add(1, Ordering::Relaxed);
        Ok(SpyScan {
            inner: self.inner.scan(lo, hi)?,
            rows: Arc::clone(&self.rows),
        })
    }

    fn point(&self, id: FactId) -> Result<Option<Entity>, StoreError> {
        self.inner.point(id)
    }
}

/// Identifier-ish names, deterministic, in the shape a code index holds: two or
/// three lowercase segments joined by `_`.
fn corpus(count: usize) -> Vec<String> {
    const HEADS: &[&str] = &[
        "parse", "encode", "decode", "read", "write", "build", "visit", "emit", "scan", "seek",
        "push", "pop", "open", "close", "flush", "merge", "split", "join", "hash", "sort",
    ];
    const TAILS: &[&str] = &[
        "str", "bytes", "node", "token", "value", "field", "row", "key", "id", "span", "block",
        "frame", "chunk", "page", "iter", "ref",
    ];

    let mut seed = 0x2545_F491_4F6C_DD1Du64;
    let mut next = move || {
        seed ^= seed << 13;
        seed ^= seed >> 7;
        seed ^= seed << 17;
        seed
    };

    let mut names: Vec<String> = (0..count)
        .map(|_| {
            let head = HEADS[(next() % HEADS.len() as u64) as usize];
            let tail = TAILS[(next() % TAILS.len() as u64) as usize];
            let suffix = next() % 1000;
            format!("{head}_{tail}{suffix}")
        })
        .collect();

    names.sort();
    names.dedup();
    names
}

fn store_of(names: &[String]) -> MemStore {
    let mut store = MemStore::new();
    for (i, name) in names.iter().enumerate() {
        store.insert(NAME, str_field(name), (i + 1) as u64);
    }
    store
}

fn spy(names: &[String]) -> (ScanSpy, Arc<AtomicUsize>, Arc<AtomicUsize>) {
    let rows = Arc::new(AtomicUsize::new(0));
    let scans = Arc::new(AtomicUsize::new(0));
    (
        ScanSpy {
            inner: store_of(names),
            rows: Arc::clone(&rows),
            scans: Arc::clone(&scans),
        },
        rows,
        scans,
    )
}

fn head() -> Project {
    Project::RegisterField {
        address: Address::new(0),
        path: FieldPath::field(0),
        ty: PredicateTy::Str,
    }
}

fn anchored_key(anchor: &str) -> SeekKey {
    if anchor.is_empty() {
        return SeekKey::Prefix(Box::new([]));
    }
    let mut bytes = str_field(anchor);
    bytes.pop();
    SeekKey::Prefix(bytes.into())
}

fn guided(term: &str, distance: u8, anchor: &str) -> Plan {
    Plan {
        nvars: 1,
        body: Box::new([Step::Level(Level {
            sources: Box::new([Source::Guided {
                access: Access {
                    predicate_id: NAME,
                    seek_key: anchored_key(anchor),
                },
                guide: Guide {
                    path: FieldPath::field(0),
                    term: Arc::from(term),
                    distance,
                },
                residuals: Box::new([]),
            }]),
            binds: Box::new([Address::new(0)]),
        })]),
        head: head(),
    }
}

fn filtered(term: &str, distance: u8, anchor: &str) -> Plan {
    Plan {
        nvars: 1,
        body: Box::new([Step::Level(Level::seek(
            Access {
                predicate_id: NAME,
                seek_key: anchored_key(anchor),
            },
            Box::new([Address::new(0)]),
            Box::new([Residual {
                path: FieldPath::field(0),
                op: ResidualOp::Fuzzy {
                    term: Arc::from(term),
                    distance,
                },
            }]),
        ))]),
        head: head(),
    }
}

struct Measured {
    rows: usize,
    scans: usize,
    answers: usize,
    micros: u128,
}

fn measure(names: &[String], plan: Plan, interner: &LocalInterner) -> Measured {
    let (store, rows, scans) = spy(names);

    let started = std::time::Instant::now();
    let answers = collect_rows(store, plan, interner).expect("run").len();
    let micros = started.elapsed().as_micros();

    Measured {
        rows: rows.load(Ordering::Relaxed),
        scans: scans.load(Ordering::Relaxed),
        answers,
        micros,
    }
}

fn main() {
    println!("\x1b[1m═══ from sigla text ═══\x1b[0m");

    for source in [
        r#"N where test.Name N; N = "ann"~"#,
        r#"N where test.Name N; N = "ann"~2"#,
        r#"X where X = test.Name "ann"~1"#,
        r#"N where test.Name N; N = "an"..; N = "ann"~2"#,
        r#"N where test.Name N; N = "ann"~2; N = "an".."#,
        r#"X where X = test.Foo {id = _, name = N}; N = "ann"~1"#,
        r#"N where test.Name N; N = "ann"~9"#,
        r#"N where test.Name N; N != "ann"~1"#,
    ] {
        run(source);
    }

    println!("\n\n\x1b[1m═══ rows read, guided against scan-and-filter ═══\x1b[0m");

    let names = corpus(200_000);
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    println!("\n  a predicate of {} distinct names\n", names.len());

    println!(
        "  {:<22} {:>7} {:>10} {:>10} {:>7} {:>9} {:>9}",
        "query", "answers", "scan rows", "guided", "read", "hops", "speedup"
    );
    println!("  {}", "─".repeat(80));

    for (term, distance, anchor) in [
        ("parse_node", 1, ""),
        ("parse_node", 2, ""),
        ("parse_node", 3, ""),
        ("encode_key", 1, ""),
        ("prase_node", 1, ""),
        ("parse_node", 2, "p"),
        ("parse_node", 2, "pa"),
        ("parse_node", 3, "pa"),
        ("nosuchname", 1, ""),
        ("nosuchname", 2, ""),
    ] {
        let scan = measure(&names, filtered(term, distance, anchor), &interner);
        let walk = measure(&names, guided(term, distance, anchor), &interner);

        assert_eq!(
            scan.answers, walk.answers,
            "guided and filtered disagreed on {term:?}~{distance} anchored {anchor:?}"
        );

        let label = if anchor.is_empty() {
            format!("{term:?}~{distance}")
        } else {
            format!("{anchor:?}.. {term:?}~{distance}")
        };

        println!(
            "  {:<22} {:>7} {:>10} {:>10} {:>6.1}% {:>9} {:>8.1}×",
            label,
            walk.answers,
            scan.rows,
            walk.rows,
            100.0 * walk.rows as f64 / scan.rows as f64,
            walk.scans.saturating_sub(1),
            scan.micros as f64 / walk.micros.max(1) as f64,
        );
    }

    println!("\n  \x1b[2m`read` is the share of the predicate the guide touched; `hops` is how");
    println!("  many times it re-opened the scan. Timings are MemStore, where a hop is");
    println!("  a BTreeMap range — on fjall a hop is ~4.3 µs and a row ~0.43 µs.\x1b[0m");

    println!("\n\x1b[1m═══ does the cost track the data? ═══\x1b[0m\n");
    println!(
        "  {:<12} {:>10} {:>12} {:>10}",
        "predicate", "scan rows", "guided rows", "hops"
    );
    println!("  {}", "─".repeat(48));

    for size in [50_000, 100_000, 200_000, 400_000] {
        let names = corpus(size);
        let scan = measure(&names, filtered("parse_node", 1, ""), &interner);
        let walk = measure(&names, guided("parse_node", 1, ""), &interner);

        println!(
            "  {:<12} {:>10} {:>12} {:>10}",
            names.len(),
            scan.rows,
            walk.rows,
            walk.scans.saturating_sub(1)
        );
    }

    println!("\n  \x1b[2mThe scan column doubles with the data and the guided column does not.");
    println!("  That is the whole claim, and `doubling_the_predicate_does_not_double_the");
    println!("  _rows_examined` in tests/guided_seek.rs is what keeps it true.\x1b[0m\n");
}
