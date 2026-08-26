//! The guided seek, held to the two things that make it worth having: it answers
//! the same rows a scan-and-filter would, and it does not read them all.
//!
//! The first is the correctness property and everything else is judged by it. The
//! second is the *point* — a guided source that answered correctly by reading the
//! whole predicate would pass every other test in the tree and be worthless.

use std::sync::{
    Arc,
    atomic::{AtomicUsize, Ordering},
};

use byteview::ByteView;
use fjord_encoding::tuple::{Value, put_str, string_probe};
use fjord_engine::{
    error::FjordError,
    fixtures::collect_rows,
    levenshtein,
    plan::{
        Access, Address, Guide, Level, Plan, Project, Residual, ResidualOp, SeekKey, Source, Step,
    },
};
use fjord_schema::{
    id::FactId,
    schema::{LocalInterner, PredicateId},
};
use fjord_store::{
    error::StoreError,
    fact_store::{Entity, FactStore},
};
use fjord_store_mem::MemStore;

const P: PredicateId = PredicateId(0);

fn str_field(s: &str) -> Vec<u8> {
    let mut out = Vec::new();
    put_str(&mut out, s);
    out
}

/// A store of one string-keyed predicate, one row per name.
fn store_of(names: &[&str]) -> MemStore {
    let mut store = MemStore::new();

    let mut sorted: Vec<&&str> = names.iter().collect();
    sorted.sort();

    for (i, name) in sorted.iter().enumerate() {
        store.insert(P, str_field(name), (i + 1) as u64);
    }

    store
}

fn guided_plan(term: &str, distance: u8, anchor: Option<&str>) -> Plan {
    let seek_key = match anchor {
        None => SeekKey::Prefix(Box::new([])),
        // A string's encoding without its terminator: what every string starting
        // with it begins with. This is the anchored spelling — `X = "pa"..` —
        // whose whole job is to hand the automaton a bucket instead of a
        // predicate.
        Some(prefix) => {
            let mut bytes = str_field(prefix);
            bytes.pop();
            SeekKey::Prefix(bytes.into())
        }
    };

    Plan {
        nvars: 1,
        body: Box::new([Step::Level(Level {
            sources: Box::new([Source::Guided {
                access: Access {
                    predicate_id: P,
                    seek_key,
                },
                guide: Guide {
                    path: fjord_engine::plan::FieldPath::field(0),
                    term: Arc::from(term),
                    distance,
                },
                residuals: Box::new([]),
            }]),
            binds: Box::new([Address::new(0)]),
        })]),
        head: Project::RegisterField {
            address: Address::new(0),
            path: fjord_engine::plan::FieldPath::field(0),
            ty: fjord_schema::schema::PredicateTy::Str,
        },
    }
}

/// The same question as a plain scan with a fuzzy **residual** — the oracle the
/// guided form has to agree with, and deliberately the dullest possible plan.
fn filtered_plan(term: &str, distance: u8) -> Plan {
    Plan {
        nvars: 1,
        body: Box::new([Step::Level(Level::seek(
            Access {
                predicate_id: P,
                seek_key: SeekKey::Prefix(Box::new([])),
            },
            Box::new([Address::new(0)]),
            Box::new([Residual {
                path: fjord_engine::plan::FieldPath::field(0),
                op: ResidualOp::Fuzzy {
                    term: Arc::from(term),
                    distance,
                },
            }]),
        ))]),
        head: Project::RegisterField {
            address: Address::new(0),
            path: fjord_engine::plan::FieldPath::field(0),
            ty: fjord_schema::schema::PredicateTy::Str,
        },
    }
}

fn names(rows: Vec<Value>) -> Vec<String> {
    rows.into_iter()
        .map(|row| match row {
            Value::Str(s) => s.to_string(),
            other => panic!("expected a string, got {other:?}"),
        })
        .collect()
}

const CORPUS: &[&str] = &[
    "parse",
    "parser",
    "parse_str",
    "pars",
    "prase",
    "arse",
    "parsed",
    "parses",
    "encode",
    "decode",
    "encoder",
    "zzz",
    "a",
    "ab",
    "abc",
    "paste",
    "purse",
    "sparse",
    "part",
    "party",
    "pare",
    "pane",
    "cafe",
    "café",
];

/// **The property the whole feature is judged by.** A guided source and a scan
/// with a fuzzy residual are the same question asked two ways, so they must
/// answer the same rows in the same order.
#[test]
fn a_guided_seek_answers_what_a_filtered_scan_answers() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    let mut with_matches = 0;

    for term in CORPUS.iter().chain(["par", "xyz", "", "parsx"].iter()) {
        for distance in 0..=2u8 {
            let guided = names(
                collect_rows(
                    store_of(CORPUS),
                    guided_plan(term, distance, None),
                    &interner,
                )
                .expect("guided run"),
            );
            let filtered = names(
                collect_rows(store_of(CORPUS), filtered_plan(term, distance), &interner)
                    .expect("filtered run"),
            );

            assert_eq!(guided, filtered, "term {term:?} at distance {distance}");

            if !guided.is_empty() {
                with_matches += 1;
            }
        }
    }

    // A census: a population where every term answered nothing would leave the
    // equality above green and saying nothing at all.
    assert!(
        with_matches > 30,
        "too few answering cases in the population: {with_matches}"
    );
}

/// The anchored spelling — the seek key narrows to a bucket and the guide walks
/// inside it — must be the intersection of the two questions, not either one.
#[test]
fn an_anchor_narrows_the_range_the_guide_walks() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    for anchor in ["p", "pa", "par", "e", "z"] {
        for distance in 0..=2u8 {
            let anchored = names(
                collect_rows(
                    store_of(CORPUS),
                    guided_plan("parse", distance, Some(anchor)),
                    &interner,
                )
                .expect("anchored run"),
            );

            let expected: Vec<String> = names(
                collect_rows(
                    store_of(CORPUS),
                    filtered_plan("parse", distance),
                    &interner,
                )
                .expect("filtered run"),
            )
            .into_iter()
            .filter(|name| name.starts_with(anchor))
            .collect();

            assert_eq!(
                anchored, expected,
                "anchor {anchor:?} at distance {distance}"
            );
        }
    }
}

/// A store that counts every row a scan yields — the instrument for the claim
/// that a guided source is a *seek*.
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

fn generated_names(count: usize) -> Vec<String> {
    // Deterministic, and deliberately spread across the key space rather than
    // clustered near the term: a corpus that was all near-misses would make the
    // automaton read everything and the measurement below would say nothing.
    let alphabet = b"abcdefghijklmnopqrstuvwxyz";
    (0..count)
        .map(|i| {
            let mut seed = (i as u64).wrapping_mul(0x9E37_79B9_7F4A_7C15) ^ 0x5DEE_CE66_D3A1_1BAD;
            (0..6)
                .map(|_| {
                    seed = seed.wrapping_mul(6_364_136_223_846_793_005).wrapping_add(1);
                    alphabet[((seed >> 33) % 26) as usize] as char
                })
                .collect()
        })
        .collect()
}

fn examined(names: &[String], term: &str, distance: u8) -> (usize, usize) {
    let refs: Vec<&str> = names.iter().map(String::as_str).collect();
    let spy = ScanSpy {
        inner: store_of(&refs),
        rows: Arc::new(AtomicUsize::new(0)),
        scans: Arc::new(AtomicUsize::new(0)),
    };

    let rows = Arc::clone(&spy.rows);
    let scans = Arc::clone(&spy.scans);

    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    collect_rows(spy, guided_plan(term, distance, None), &interner).expect("guided run");

    (rows.load(Ordering::Relaxed), scans.load(Ordering::Relaxed))
}

/// **The claim the feature exists for, made mechanical.** Doubling the predicate
/// must not double the rows the guide reads.
///
/// Stated as a ratio against the store's own size rather than as an absolute,
/// because a bigger store legitimately costs a little more — what must not happen
/// is the cost tracking the data. Without this, "it is a seek, not a scan" is an
/// aspiration: a guided source that read every row would answer correctly and
/// pass every other test here.
#[test]
fn doubling_the_predicate_does_not_double_the_rows_examined() {
    let n = generated_names(2_000);
    let two_n = generated_names(4_000);

    let (rows_n, scans_n) = examined(&n, "parse", 1);
    let (rows_2n, _) = examined(&two_n, "parse", 1);

    assert!(
        rows_n * 8 < n.len(),
        "a guided seek read {rows_n} of {} rows — that is a scan",
        n.len()
    );

    assert!(
        rows_2n < rows_n * 2,
        "rows examined doubled with the data: {rows_n} at N, {rows_2n} at 2N"
    );

    // And it got there by seeking: one scan would mean the guide never hopped,
    // which is the other way this test could pass while measuring nothing.
    assert!(scans_n > 1, "the guide never re-opened the scan");
}

/// A guide reads the key it is already holding and fetches nothing
/// ([I6](../../website/content/invariants.md#i6)).
#[test]
fn a_guided_seek_fetches_no_values() {
    use fjord_store::fixtures::PointSpy;

    let (spy, points) = PointSpy::new(store_of(CORPUS));
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    let rows = collect_rows(spy, guided_plan("parse", 2, None), &interner).expect("guided run");

    assert!(!rows.is_empty(), "the run answered nothing to speak of");
    assert_eq!(
        points.load(Ordering::Relaxed),
        0,
        "a guided scan read a value"
    );
}

/// The automaton's bound on how much of a key it reads is what makes a long key
/// cost no more than a short one — so a store of very long names must not be
/// slower to reject than a store of short ones, and must still answer correctly.
#[test]
fn a_long_key_is_walked_only_as_far_as_the_term_can_reach() {
    let padded: Vec<String> = CORPUS
        .iter()
        .map(|name| format!("{name}{}", "x".repeat(500)))
        .collect();
    let refs: Vec<&str> = padded.iter().map(String::as_str).collect();

    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    let guided = names(
        collect_rows(store_of(&refs), guided_plan("parse", 2, None), &interner)
            .expect("guided run"),
    );
    let filtered = names(
        collect_rows(store_of(&refs), filtered_plan("parse", 2), &interner).expect("filtered run"),
    );

    assert_eq!(guided, filtered);

    let inspected = |candidate: &str| {
        string_probe::reset();
        let rows = collect_rows(
            store_of(&[candidate]),
            guided_plan("parse", 2, None),
            &interner,
        )
        .expect("guided run");
        assert!(rows.is_empty(), "the work probe needs a rejected candidate");
        string_probe::count()
    };

    let short = inspected("parsexxx");
    let long = inspected(&format!("parsexxx{}", "x".repeat(500)));

    assert!(short > 0, "the byte-inspection probe saw no decode work");
    assert_eq!(
        long, short,
        "an unreachable suffix changed decoder work: {short} bytes for the short key, \
         {long} for the long key"
    );
}

/// A plan built by hand can name a term the automaton will not build for, and it
/// must be an error rather than a truncated question silently answered.
#[test]
fn an_oversized_term_is_refused_rather_than_truncated() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    let long = "a".repeat(levenshtein::MAX_TERM_CHARS + 1);

    let error = collect_rows(store_of(CORPUS), guided_plan(&long, 1, None), &interner)
        .expect_err("an oversized term should refuse");

    assert!(
        format!("{error}").contains("fuzzy term"),
        "wrong error: {error}"
    );
}

/// A public hand-built residual is still a data path: an unsupported distance
/// must surface as an error and must never overflow inside the DP arithmetic.
#[test]
fn an_unsupported_residual_distance_is_an_error_not_a_panic() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    let outcome = std::panic::catch_unwind(|| {
        collect_rows(store_of(CORPUS), filtered_plan("parse", u8::MAX), &interner)
    });

    assert!(
        matches!(
            outcome,
            Ok(Err(FjordError::FuzzyTermUnsupported {
                chars: 5,
                distance: u8::MAX
            }))
        ),
        "an unsupported residual distance must return an error, got {outcome:?}"
    );
}
