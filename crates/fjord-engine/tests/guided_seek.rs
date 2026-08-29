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
    levenshtein::{self, FuzzyAnchor},
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

fn guided_plan(term: &str, distance: u8, range: Option<&str>, anchor: FuzzyAnchor) -> Plan {
    let seek_key = match range {
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
                    anchor,
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

/// The same guided source, projecting the row's **identity** rather than its
/// string.
///
/// For measuring what the *guide* decoded, and only usable for that: projecting
/// the matched field necessarily decodes all of it to build the answer, so a probe
/// over the ordinary plan measures the head and not the walk.
fn guided_plan_by_ref(term: &str, distance: u8, anchor: FuzzyAnchor) -> Plan {
    let mut plan = guided_plan(term, distance, None, anchor);
    plan.head = Project::FactRef(Address::new(0));
    plan
}

/// The same question as a plain scan with a fuzzy **residual** — the oracle the
/// guided form has to agree with, and deliberately the dullest possible plan.
fn filtered_plan(term: &str, distance: u8, anchor: FuzzyAnchor) -> Plan {
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
                    anchor,
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
                    guided_plan(term, distance, None, FuzzyAnchor::Whole),
                    &interner,
                )
                .expect("guided run"),
            );
            let filtered = names(
                collect_rows(
                    store_of(CORPUS),
                    filtered_plan(term, distance, FuzzyAnchor::Whole),
                    &interner,
                )
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
                    guided_plan("parse", distance, Some(anchor), FuzzyAnchor::Whole),
                    &interner,
                )
                .expect("anchored run"),
            );

            let expected: Vec<String> = names(
                collect_rows(
                    store_of(CORPUS),
                    filtered_plan("parse", distance, FuzzyAnchor::Whole),
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

fn examined(names: &[String], term: &str, distance: u8, anchor: FuzzyAnchor) -> (usize, usize) {
    let refs: Vec<&str> = names.iter().map(String::as_str).collect();
    let spy = ScanSpy {
        inner: store_of(&refs),
        rows: Arc::new(AtomicUsize::new(0)),
        scans: Arc::new(AtomicUsize::new(0)),
    };

    let rows = Arc::clone(&spy.rows);
    let scans = Arc::clone(&spy.scans);

    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    collect_rows(spy, guided_plan(term, distance, None, anchor), &interner).expect("guided run");

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

    let (rows_n, scans_n) = examined(&n, "parse", 1, FuzzyAnchor::Whole);
    let (rows_2n, _) = examined(&two_n, "parse", 1, FuzzyAnchor::Whole);

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

    let rows = collect_rows(
        spy,
        guided_plan("parse", 2, None, FuzzyAnchor::Whole),
        &interner,
    )
    .expect("guided run");

    assert!(!rows.is_empty(), "the run answered nothing to speak of");
    assert_eq!(
        points.load(Ordering::Relaxed),
        0,
        "a guided scan read a value"
    );
}

// ---- the anchored question -------------------------------------------------

/// **The property the anchored form is judged by**, and the twin of
/// `a_guided_seek_answers_what_a_filtered_scan_answers`: guiding and filtering
/// are the same question asked two ways, so they must answer the same rows in the
/// same order under `~<` exactly as under `~`.
#[test]
fn a_guided_prefix_seek_answers_what_a_filtered_scan_answers() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    // Padded names as well as bare ones: a suffix is what separates the two
    // questions, so a population without one would let a guide that quietly asked
    // the whole-string question pass.
    let padded: Vec<String> = CORPUS
        .iter()
        .map(|name| format!("{name}_suffix_that_goes_on"))
        .collect();
    let with_suffixes: Vec<&str> = CORPUS
        .iter()
        .copied()
        .chain(padded.iter().map(String::as_str))
        .collect();

    let mut with_matches = 0;
    let mut reached_further = 0;

    for term in CORPUS.iter().chain(["par", "xyz", "", "parsx"].iter()) {
        for distance in 0..=2u8 {
            let run =
                |plan| names(collect_rows(store_of(&with_suffixes), plan, &interner).expect("run"));

            let guided = run(guided_plan(term, distance, None, FuzzyAnchor::Prefix));
            let filtered = run(filtered_plan(term, distance, FuzzyAnchor::Prefix));

            assert_eq!(guided, filtered, "term {term:?} at distance {distance}");

            if !guided.is_empty() {
                with_matches += 1;
            }

            // And it is genuinely the other question: anchoring must reach rows
            // the whole-string form does not, or both halves of this file are
            // testing one matcher twice.
            let whole = run(guided_plan(term, distance, None, FuzzyAnchor::Whole));
            assert!(
                whole.iter().all(|name| guided.contains(name)),
                "term {term:?} at distance {distance}: `~` answered rows `~<` did not"
            );
            reached_further += usize::from(guided.len() > whole.len());
        }
    }

    assert!(
        with_matches > 30,
        "too few answering cases in the population: {with_matches}"
    );
    assert!(
        reached_further > 10,
        "anchoring never reached past the whole-string form: {reached_further}"
    );
}

/// **Once a prefix accepts, there is nothing left to skip.** Every key sharing an
/// accepting prefix is an answer, so a guide that computed a seek target inside
/// that band would be re-opening a scan over rows it was about to return anyway.
///
/// Stated as the scan count rather than as a timing: one `scan` call for a band of
/// two hundred accepting keys, and any hop at all inside it is the regression.
#[test]
fn a_guided_prefix_seek_never_seeks_past_an_accepting_prefix() {
    let band: Vec<String> = (0..200).map(|i| format!("parse_{i:03}")).collect();
    let refs: Vec<&str> = band.iter().map(String::as_str).collect();

    let spy = ScanSpy {
        inner: store_of(&refs),
        rows: Arc::new(AtomicUsize::new(0)),
        scans: Arc::new(AtomicUsize::new(0)),
    };
    let rows = Arc::clone(&spy.rows);
    let scans = Arc::clone(&spy.scans);

    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    let answered = collect_rows(
        spy,
        guided_plan("parse", 1, None, FuzzyAnchor::Prefix),
        &interner,
    )
    .expect("guided run")
    .len();

    assert_eq!(answered, band.len(), "the band is meant to answer entire");
    assert_eq!(
        scans.load(Ordering::Relaxed),
        1,
        "the guide re-opened the scan inside a band it was accepting"
    );
    assert_eq!(
        rows.load(Ordering::Relaxed),
        band.len(),
        "the guide read rows it did not answer"
    );
}

/// **An accepted row is bounded too, and only the anchored form is.** A
/// whole-string match is short by definition; an anchored one may accept five
/// characters into a four-kilobyte identifier, and must then stop.
///
/// The probe counts bytes the lazy decoder actually touched, so this is a bound on
/// decode work rather than on arithmetic.
#[test]
fn an_accepted_long_key_is_not_decoded_past_its_accepting_prefix() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    let inspected = |candidate: &str, answers: usize| {
        string_probe::reset();
        let rows = collect_rows(
            store_of(&[candidate]),
            guided_plan_by_ref("parse", 1, FuzzyAnchor::Prefix),
            &interner,
        )
        .expect("guided run");
        assert_eq!(rows.len(), answers, "{candidate:?} answered unexpectedly");
        string_probe::count()
    };

    let short = inspected("parsexxx", 1);
    let long = inspected(&format!("parse{}", "x".repeat(4096)), 1);

    assert!(short > 0, "the byte-inspection probe saw no decode work");
    assert_eq!(
        long, short,
        "a 4 KiB key cost {long} bytes of decoding where a short one cost {short} — \
         the walk read past its accepting prefix"
    );

    // The rejecting half still holds, and its bound is the older one: a dead state
    // stops the walk wherever the key ends.
    let short_reject = inspected("zzz", 0);
    let long_reject = inspected(&format!("zzz{}", "z".repeat(4096)), 0);
    assert_eq!(long_reject, short_reject, "a rejected long key cost more");
}

/// **One walk for a whole band, not one per row.** Keys sharing an accepting
/// prefix are usually *different* keys — `parse_000`, `parse_001` — so the cache
/// that turns a run into a `memcmp` has to compare the accepting **prefix** and
/// not the whole field, or it misses on every row in exactly the case anchoring
/// exists for.
///
/// Measured as decode work against band size, in the N-against-2N form the
/// allocation guards use: a cache that never hits makes the total track the data.
#[test]
fn an_accepting_prefix_is_walked_once_for_the_whole_band() {
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    let decoded = |count: usize| {
        let band: Vec<String> = (0..count).map(|i| format!("parse_{i:04}")).collect();
        let refs: Vec<&str> = band.iter().map(String::as_str).collect();

        string_probe::reset();
        let rows = collect_rows(
            store_of(&refs),
            guided_plan_by_ref("parse", 1, FuzzyAnchor::Prefix),
            &interner,
        )
        .expect("guided run");

        assert_eq!(rows.len(), count, "the band is meant to answer entire");
        string_probe::count()
    };

    let n = decoded(100);
    let two_n = decoded(200);

    assert!(n > 0, "the byte-inspection probe saw no decode work");
    assert_eq!(
        n, two_n,
        "decode work grew with the band: {n} bytes for 100 rows, {two_n} for 200 —          the accepted prefix is being re-walked per row"
    );
}

/// I6 for the anchored guide: it reads the key the scan is already holding.
#[test]
fn a_guided_prefix_seek_fetches_no_values() {
    use fjord_store::fixtures::PointSpy;

    let (spy, points) = PointSpy::new(store_of(CORPUS));
    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());

    let rows = collect_rows(
        spy,
        guided_plan("parse", 1, None, FuzzyAnchor::Prefix),
        &interner,
    )
    .expect("guided run");

    assert!(!rows.is_empty(), "the run answered nothing to speak of");
    assert_eq!(
        points.load(Ordering::Relaxed),
        0,
        "an anchored guided scan read a value"
    );
}

/// The seek-not-scan claim, for the anchored question.
///
/// The term has to be long enough that most of the predicate is *not* an answer —
/// a term no longer than its distance matches everything, and a guide over that is
/// a scan by definition rather than by regression.
#[test]
fn doubling_the_predicate_does_not_double_the_rows_examined_when_anchored() {
    let n = generated_names(2_000);
    let two_n = generated_names(4_000);

    let (rows_n, scans_n) = examined(&n, "parse", 1, FuzzyAnchor::Prefix);
    let (rows_2n, _) = examined(&two_n, "parse", 1, FuzzyAnchor::Prefix);

    assert!(
        rows_n * 8 < n.len(),
        "an anchored guided seek read {rows_n} of {} rows — that is a scan",
        n.len()
    );
    assert!(
        rows_2n < rows_n * 2,
        "rows examined doubled with the data: {rows_n} at N, {rows_2n} at 2N"
    );
    assert!(scans_n > 1, "the guide never re-opened the scan");
}

/// The automaton's bound on how much of a key it reads is what makes a long key
/// cost no more than a short one — so a store of very long names must not be
/// slower to reject than a store of short ones, and must still answer correctly.
///
/// The probe candidate below is a *rejection* under `~`, which is what makes it a
/// measurement. It is an acceptance under `~<` — the anchored bound is the
/// separate claim
/// [`an_accepted_long_key_is_not_decoded_past_its_accepting_prefix`] makes.
#[test]
fn a_long_key_is_walked_only_as_far_as_the_term_can_reach() {
    let padded: Vec<String> = CORPUS
        .iter()
        .map(|name| format!("{name}{}", "x".repeat(500)))
        .collect();
    let refs: Vec<&str> = padded.iter().map(String::as_str).collect();

    let interner = LocalInterner::new(fjord_store::fixture::schema().interner().clone());
    let guided = names(
        collect_rows(
            store_of(&refs),
            guided_plan("parse", 2, None, FuzzyAnchor::Whole),
            &interner,
        )
        .expect("guided run"),
    );
    let filtered = names(
        collect_rows(
            store_of(&refs),
            filtered_plan("parse", 2, FuzzyAnchor::Whole),
            &interner,
        )
        .expect("filtered run"),
    );

    assert_eq!(guided, filtered);

    let inspected = |candidate: &str| {
        string_probe::reset();
        let rows = collect_rows(
            store_of(&[candidate]),
            guided_plan("parse", 2, None, FuzzyAnchor::Whole),
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

    let error = collect_rows(
        store_of(CORPUS),
        guided_plan(&long, 1, None, FuzzyAnchor::Whole),
        &interner,
    )
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
        collect_rows(
            store_of(CORPUS),
            filtered_plan("parse", u8::MAX, FuzzyAnchor::Whole),
            &interner,
        )
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
