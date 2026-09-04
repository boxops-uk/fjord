//! **`config.Setting`, against a real database.**
//!
//! The claim is that a tool holding many handles can ask *which one is this* and get the
//! answer out of the database rather than out of its name — so the test is a database
//! created from `schemas/config.sigla`, the full reserved set written into it, and the
//! three questions a consumer actually asks.
//!
//! An integration test because a predicate nothing has queried is a name in a file.
//!
//! The second claim is `position-encoding`'s, and it needs more than one namespace: a
//! dimension is only worth declaring if a consumer that reads it converts an offset
//! correctly, so that one stands up two databases over one source file — one per unit —
//! and converts a real span through a real `src.FileLine` row.

use std::sync::Arc;

use fjord_client::{Connection, Endpoint, Mode};
use fjord_schema::schema::PredicateId;
use fjord_wire::{WireFact, WireRef, WireValue};

use std::{
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    thread,
    time::{Duration, Instant},
};

const CONFIG: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/config.sigla");

/// The reserved set, as a producer writes it — one fact per pair, `language` twice.
const SETTINGS: &[(&str, &str)] = &[
    ("repo", "github.com/boxops-uk/fjord"),
    ("revision", "5ac4f0e69729bbbb54e28b6219237608f2a4ebae"),
    ("index-root", "/home/ci/checkout"),
    ("position-encoding", "utf16"),
    ("style-encoding", "roslyn-lsp-1"),
    ("symbol-scheme", "scip-csharp-2"),
    ("language", "csharp"),
    ("language", "msbuild"),
    ("producer", "Boxops.Fjord.Indexer 0.1.0"),
    ("framework", "net9.0"),
    ("configuration", "Debug"),
];

struct Serving(Child);

impl Drop for Serving {
    fn drop(&mut self) {
        let _ = self.0.kill();
        let _ = self.0.wait();
    }
}

fn serve(root: &Path) -> Serving {
    let ready = root.join("ready");
    let child = Command::new(env!("CARGO_BIN_EXE_fjord"))
        .arg("--data-dir")
        .arg(root)
        .arg("serve")
        .arg("--ready-file")
        .arg(&ready)
        .stdout(Stdio::null())
        .stderr(Stdio::inherit())
        .spawn()
        .expect("the server starts");

    let deadline = Instant::now() + Duration::from_secs(30);
    while !ready.exists() {
        assert!(Instant::now() < deadline, "the server never became ready");
        thread::sleep(Duration::from_millis(20));
    }
    Serving(child)
}

fn fjord(root: &Path, args: &[&str]) -> String {
    let out = Command::new(env!("CARGO_BIN_EXE_fjord"))
        .arg("--data-dir")
        .arg(root)
        .args(args)
        .output()
        .expect("the binary runs");

    assert!(
        out.status.success(),
        "`fjord {args:?}` failed:\n{}",
        String::from_utf8_lossy(&out.stderr)
    );
    String::from_utf8_lossy(&out.stdout).into_owned()
}

fn rows(root: &Path, query: &str) -> Vec<serde_json::Value> {
    rows_of(root, "config", query)
}

fn rows_of(root: &Path, db: &str, query: &str) -> Vec<serde_json::Value> {
    let json = fjord(root, &["query", db, query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

/// **A consumer can enumerate, seek a dimension, and find one absent.**
///
/// The middle question is the one the shape was chosen for: `language` holds two values,
/// which a `dimension -> value` predicate could not say — the second write would be a
/// conflict rather than a second fact.
#[test]
fn a_consumer_can_ask_what_this_database_was_built_for() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(&root, &["create", "config", "--schema", CONFIG]);
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "config",
        Arc::new(fjord_cli::sample_schema::schema()),
        Mode::ReadOnly,
        false,
    )
    .expect("a probe connection");
    let served = Arc::new(probe.served_schema().expect("the served schema"));
    drop(probe);

    let mut writer = Connection::open(&endpoint, "config", served, Mode::ReadWrite, true)
        .expect("a write connection");

    let facts: Vec<WireFact> = SETTINGS
        .iter()
        .map(|(dimension, value)| WireFact {
            predicate: PredicateId(0),
            key: WireValue::Record(Box::from([
                WireValue::Str((*dimension).to_owned()),
                WireValue::Str((*value).to_owned()),
            ])),
            value: None,
        })
        .collect();

    let written = writer
        .write(PredicateId(0), &facts)
        .expect("the settings are accepted");
    assert_eq!(
        written.created,
        SETTINGS.len() as u64,
        "one fact per pair, so a repeated dimension is not a conflict: {written:?}"
    );
    drop(writer);

    // **Everything, which is what a tool asks first.**
    let all = rows(
        &root,
        "{d = D, v = V} where config.Setting {dimension = D, value = V}",
    );
    assert_eq!(all.len(), SETTINGS.len(), "{all:#?}");

    // **One dimension's values — multi-valued, and this is the assertion that says so.**
    let languages = rows(
        &root,
        "V where config.Setting {dimension = \"language\", value = V}",
    );
    assert_eq!(
        languages,
        vec![serde_json::json!("csharp"), serde_json::json!("msbuild")],
        "`language` holds two values"
    );

    // **Dimension-leading, so this is a seek rather than a scan** — and one value.
    let framework = rows(
        &root,
        "V where config.Setting {dimension = \"framework\", value = V}",
    );
    assert_eq!(framework, vec![serde_json::json!("net9.0")]);

    // **An absent dimension is zero rows, not an error.** A consumer reading a database
    // that predates a dimension has to be able to tell that apart from a failure.
    let absent = rows(
        &root,
        "V where config.Setting {dimension = \"target-triple\", value = V}",
    );
    assert!(absent.is_empty(), "{absent:#?}");
}

/// **The two position encodings are not the same number for non-BMP text**, which is the
/// whole reason one declaration per database beats a unit per span.
///
/// The schema's own claim, tested without a renderer: a codepoint above the BMP is one
/// scalar value, two UTF-16 code units and four UTF-8 bytes, so a column counted in one
/// unit and read in another lands in the wrong place — silently, because every index
/// involved is in range.
///
/// The three units alone, over a literal — so it holds no database and gates no
/// consumer. The conversion through a `src.FileLine` row fetched out of a database that
/// declares its unit is [`a_span_is_read_through_the_unit_the_database_declares`], and
/// that is the gate on the decoder.
#[test]
fn the_position_encodings_disagree_exactly_where_it_matters() {
    // A grinning face, then an identifier. Everything a producer might count:
    let line = "let \u{1f600} = parse();";
    let reference = line.find("parse").expect("the identifier is there");

    let utf8 = reference;
    let utf16: usize = line[..reference].encode_utf16().count();
    let scalars = line[..reference].chars().count();

    // Three units, three answers, and the pairwise differences are exactly the
    // non-BMP codepoint's cost in each.
    assert_eq!(utf8, 11, "UTF-8 bytes: the emoji is four");
    assert_eq!(utf16, 9, "UTF-16 code units: the emoji is a surrogate pair");
    assert_eq!(scalars, 8, "scalar values: the emoji is one");

    assert_ne!(utf8, utf16);
    assert_ne!(utf16, scalars);

    // And the failure is silent: a column counted as UTF-16 and used as a scalar index
    // is in range and points at the wrong text, which is why nothing caught it.
    let chars: Vec<char> = line.chars().collect();
    assert!(utf16 < chars.len(), "in range, and wrong");
    assert_ne!(
        chars[utf16..utf16 + 5].iter().collect::<String>(),
        "parse",
        "reading a UTF-16 column as a scalar index must land somewhere else"
    );
    assert_eq!(
        chars[scalars..scalars + 5].iter().collect::<String>(),
        "parse"
    );
}

// ========================================================================================
// `position-encoding`, on the decoder's side
// ========================================================================================

const SCHEMAS: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas");

/// The file the fixture databases are about. A non-BMP codepoint on the **first** line
/// and another before the identifier on the second, so **both halves of the conversion
/// differ between the units**: the second line's own start (byte 15, code unit 13) and
/// the column inside it. Over ASCII neither would, and every reading below would agree.
const SOURCE: &[&str] = &["// \u{1f600} header", "let \u{1f600} = parse();", "}"];
const SOURCE_PATH: &str = "src/main.rs";
const SYMBOL: &str = "scip-rust rust . . parse().";

/// What the reference points at, and what the conversion has to land on.
const IDENT: &str = "parse";

/// The unit `config.Setting {dimension = "position-encoding"}` names.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum Unit {
    Utf8,
    Utf16,
}

impl Unit {
    /// The value a producer writes — and the fixture database's name.
    fn value(self) -> &'static str {
        match self {
            Unit::Utf8 => "utf8",
            Unit::Utf16 => "utf16",
        }
    }

    fn other(self) -> Unit {
        match self {
            Unit::Utf8 => Unit::Utf16,
            Unit::Utf16 => Unit::Utf8,
        }
    }

    /// How many of this unit `text` counts as.
    fn count(self, text: &str) -> i64 {
        match self {
            Unit::Utf8 => text.len() as i64,
            Unit::Utf16 => text.encode_utf16().count() as i64,
        }
    }

    /// One character's width in this unit.
    fn width(self, ch: char) -> i64 {
        match self {
            Unit::Utf8 => ch.len_utf8() as i64,
            Unit::Utf16 => ch.len_utf16() as i64,
        }
    }

    /// **What a consumer decides from the rows the database answered with.**
    ///
    /// No rows is not an error: `schemas/config.sigla` says a database that does not
    /// state its position encoding is read as `utf16`, because that is what the
    /// compilers this set indexes count.
    fn read(values: &[serde_json::Value]) -> Unit {
        match values.first().and_then(serde_json::Value::as_str) {
            Some("utf8") => Unit::Utf8,
            None | Some("utf16") => Unit::Utf16,
            Some(other) => panic!("`position-encoding` is `utf8` or `utf16`, not `{other}`"),
        }
    }
}

/// One `src.FileLine` row as a consumer holds it: the text it is about to render, and
/// the two starts that make the line table the conversion table.
struct Line {
    line: i64,
    text: String,
    start: i64,
    cstart: i64,
}

impl Line {
    /// This line's first position in `unit`. `start` is UTF-8 bytes always; `cstart` is
    /// the same start counted in the unit the database declares.
    fn base(&self, unit: Unit) -> i64 {
        match unit {
            Unit::Utf8 => self.start,
            Unit::Utf16 => self.cstart,
        }
    }
}

/// **The conversion a consumer owes: an offset in the declared unit → the line it falls
/// on, and a byte index into that line's own text.**
///
/// The unit is the only difference between the two readings, which is the claim. `None`
/// where the offset is past the line's text or lands *inside* a character — slicing
/// there is the panic a consumer must not take, and a UTF-16 offset read as bytes lands
/// mid-character on every surrogate pair.
fn locate(unit: Unit, lines: &[Line], offset: i64) -> Option<(&Line, usize)> {
    let line = lines.iter().rev().find(|line| line.base(unit) <= offset)?;
    let mut want = offset - line.base(unit);

    for (index, ch) in line.text.char_indices() {
        if want == 0 {
            return Some((line, index));
        }
        want -= unit.width(ch);
        if want < 0 {
            return None;
        }
    }

    (want == 0).then_some((line, line.text.len()))
}

/// The line table a producer owes for [`SOURCE`], in the unit its database declares.
fn table(unit: Unit) -> Vec<Line> {
    let (mut start, mut cstart) = (0i64, 0i64);
    SOURCE
        .iter()
        .enumerate()
        .map(|(index, text)| {
            let line = Line {
                line: index as i64 + 1,
                text: (*text).to_owned(),
                start,
                cstart,
            };
            // Plus the line terminator, which `text` excludes.
            start += text.len() as i64 + 1;
            cstart += unit.count(text) + 1;
            line
        })
        .collect()
}

/// The offset a producer writes into the reference's span: where [`IDENT`] sits, counted
/// in the declared unit from the start of the file.
///
/// **Straight off the source text, and deliberately not through [`Line::base`].** A
/// producer that computed its spans with the consumer's own conversion would agree with
/// it however wrong both were — which is a tautology, not a gate. The literals in
/// [`a_span_is_read_through_the_unit_the_database_declares`] pin this side down.
fn reference(unit: Unit) -> i64 {
    let column = SOURCE[1]
        .find(IDENT)
        .expect("the identifier is on the second line");

    // The first line, its terminator, then the second line up to the identifier.
    unit.count(SOURCE[0]) + 1 + unit.count(&SOURCE[1][..column])
}

/// Write the fixture into the database named for `unit`: the declaration, the file, its
/// line table, and one reference whose span counts in that unit.
fn fill(endpoint: &Endpoint, unit: Unit) {
    let db = unit.value();
    let mut probe = Connection::open(
        endpoint,
        db,
        Arc::new(fjord_cli::sample_schema::schema()),
        Mode::ReadOnly,
        false,
    )
    .expect("a probe connection");
    let served = Arc::new(probe.served_schema().expect("the served schema"));
    drop(probe);

    let id = |name: &str| {
        served
            .find_position(name)
            .map(|(id, _)| id)
            .unwrap_or_else(|| panic!("the served schema declares no `{name}`"))
    };

    let file = WireFact {
        predicate: id("src.File"),
        key: WireValue::Str(SOURCE_PATH.to_owned()),
        value: None,
    };
    let symbol = WireFact {
        predicate: id("src.Symbol"),
        key: WireValue::Str(SYMBOL.to_owned()),
        value: None,
    };
    let nested = |fact: &WireFact| WireValue::Ref(WireRef::Nested(Box::new(fact.clone())));

    let blocks: Vec<(PredicateId, Vec<WireFact>)> = vec![
        (id("src.File"), vec![file.clone()]),
        (id("src.Symbol"), vec![symbol.clone()]),
        (
            id("config.Setting"),
            vec![WireFact {
                predicate: id("config.Setting"),
                key: WireValue::Record(Box::from([
                    WireValue::Str("position-encoding".to_owned()),
                    WireValue::Str(unit.value().to_owned()),
                ])),
                value: None,
            }],
        ),
        (
            id("src.FileLine"),
            table(unit)
                .iter()
                .map(|line| WireFact {
                    predicate: id("src.FileLine"),
                    key: WireValue::Record(Box::from([nested(&file), WireValue::Int(line.line)])),
                    value: Some(WireValue::Record(Box::from([
                        WireValue::Str(line.text.clone()),
                        WireValue::Int(line.start),
                        WireValue::Int(line.text.len() as i64),
                        WireValue::Int(line.cstart),
                    ]))),
                })
                .collect(),
        ),
        (
            id("codemarkup.FileXRef"),
            vec![WireFact {
                predicate: id("codemarkup.FileXRef"),
                key: WireValue::Record(Box::from([
                    nested(&file),
                    WireValue::Record(Box::from([
                        WireValue::Int(reference(unit)),
                        WireValue::Int(unit.count(IDENT)),
                    ])),
                    nested(&symbol),
                    // `Role::call` is discriminant 4, and its payload is the empty record.
                    WireValue::Union {
                        disc: 4,
                        value: Box::new(WireValue::Record(Box::from([]))),
                    },
                ])),
                value: None,
            }],
        ),
    ];

    let mut writer = Connection::open(endpoint, db, Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    for (predicate, facts) in &blocks {
        let written = writer
            .write(*predicate, facts)
            .unwrap_or_else(|err| panic!("writing {predicate:?} into `{db}`: {err}"));
        assert_eq!(
            written.created as usize,
            facts.len(),
            "{predicate:?} in `{db}`"
        );
    }
}

/// The line table as a consumer fetches it — the window it is about to render, in the
/// order the key gives it.
fn fetched(root: &Path, db: &str) -> Vec<Line> {
    rows_of(
        root,
        db,
        "{line = L, of = X.value} where X = src.FileLine {file = F, line = L}",
    )
    .iter()
    .map(|row| Line {
        line: row["line"].as_i64().expect("a line number"),
        text: row["of"]["text"]
            .as_str()
            .expect("the line's text")
            .to_owned(),
        start: row["of"]["start"].as_i64().expect("a byte start"),
        cstart: row["of"]["cstart"].as_i64().expect("a declared-unit start"),
    })
    .collect()
}

/// **A reference's span is read through the unit the database declares.**
///
/// The decoder's side of `position-encoding`, and the test that would have caught the
/// retired viewer's `chars()` bug. Two databases over the same source file — one
/// declaring `utf8`, one `utf16` — each holding the same reference with its span counted
/// in its own unit. The consumer reads the declaration out of the database, fetches the
/// line table, converts, and lands on the identifier in both.
///
/// **The other declaration's reading misses, and the two directions miss differently** —
/// so each is asserted for what it is. In the `utf8` database the byte offset read as
/// UTF-16 code units lands on a character boundary two bytes past the identifier and
/// reads `rse()`: in range, on a boundary, wrong text, and nothing reports it. In the
/// `utf16` database the code-unit offset read as bytes lands *mid-surrogate*, and
/// `locate` answers `None` rather than slicing there — detectable, not silent. Asserting
/// only that neither reading found the identifier is satisfied by "found nothing", which
/// leaves half the differential proving the weaker claim.
///
/// The non-BMP codepoints are what give this teeth: over ASCII the two units are the
/// same number and every reading agrees.
#[test]
fn a_span_is_read_through_the_unit_the_database_declares() {
    // **The fixture's numbers, pinned as literals.** The point is that no shared helper
    // can move the producer's span and the consumer's conversion together: over ASCII the
    // two units would be the same number and every reading below would agree.
    assert_eq!(reference(Unit::Utf8), 26, "byte 26 of the file");
    assert_eq!(reference(Unit::Utf16), 22, "UTF-16 code unit 22");

    // Nothing declared is `utf16` — `schemas/config.sigla`'s sentence, on this side.
    assert_eq!(Unit::read(&[]), Unit::Utf16);

    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    // **A fixture entry file, not a schema of the set.** The gate needs `config` beside
    // `src` and `codemarkup`, and takes all three from the shipped files rather than
    // restating a predicate the set already declares.
    let entry = dir.path().join("position-probe.sigla");
    std::fs::write(
        &entry,
        "schema probe {\n  import config\n  import src\n  import codemarkup\n}\n",
    )
    .expect("the entry file is written");
    let entry = entry.to_str().expect("a utf-8 path");

    for unit in [Unit::Utf8, Unit::Utf16] {
        fjord(
            &root,
            &[
                "--schema-path",
                SCHEMAS,
                "create",
                unit.value(),
                "--schema",
                entry,
            ],
        );
    }

    let _serving = serve(&root);
    let endpoint = Endpoint::Unix(root.join("fjord.sock"));

    for unit in [Unit::Utf8, Unit::Utf16] {
        fill(&endpoint, unit);
    }

    for unit in [Unit::Utf8, Unit::Utf16] {
        let db = unit.value();

        // ---- what the database says it counts in --------------------------------
        let declared = Unit::read(&rows_of(
            &root,
            db,
            "V where config.Setting {dimension = \"position-encoding\", value = V}",
        ));
        assert_eq!(declared, unit, "`{db}` did not declare its own unit");

        // ---- the row a renderer already has in hand -----------------------------
        let lines = fetched(&root, db);
        assert_eq!(
            lines.len(),
            SOURCE.len(),
            "`{db}` holds a partial line table"
        );
        assert_eq!(
            lines[1].text, SOURCE[1],
            "the fixture's non-BMP line is what makes this a gate"
        );

        // **The declared-unit rule, as `src.sigla` writes it**: `start` is UTF-8 bytes
        // whatever the database declares, and `cstart` is that same start in the declared
        // unit — so the two coincide in a `utf8` database and diverge in a `utf16` one.
        assert_eq!(lines[1].start, 15, "line 2 begins at byte 15");
        assert_eq!(
            lines[1].cstart,
            match unit {
                Unit::Utf8 => 15,
                Unit::Utf16 => 13,
            },
            "`{db}`: line 2's start in its own unit"
        );

        // ---- the reference, as a producer wrote it ------------------------------
        let span = rows_of(
            &root,
            db,
            "SP where codemarkup.FileXRef {file = F, span = SP, target = S, role = R}",
        );
        assert_eq!(span.len(), 1, "{span:#?}");
        let offset = span[0]["start"].as_i64().expect("a span start");
        assert_eq!(offset, reference(unit), "the span is in `{db}`'s own unit");

        // ---- the conversion ----------------------------------------------------
        let (line, at) = locate(declared, &lines, offset)
            .unwrap_or_else(|| panic!("`{db}`: offset {offset} falls in no line"));
        assert_eq!(line.line, 2, "the reference is on the second line");
        assert_eq!(
            line.text.get(at..at + IDENT.len()),
            Some(IDENT),
            "`{db}`: offset {offset} landed at byte {at} of `{}`",
            line.text
        );

        // **And reading it in the other unit misses, in the way that unit misses.**
        let wrong = locate(unit.other(), &lines, offset)
            .map(|(line, at)| (line.line, at, line.text.get(at..at + IDENT.len())));

        match unit {
            // The emoji is two code units and four bytes, so counting it as two walks
            // two bytes further into the line: a boundary, in range, and the wrong five
            // bytes — the reading nothing in the stack can report.
            Unit::Utf8 => assert_eq!(
                wrong,
                Some((2, 13, Some("rse()"))),
                "`{db}`: the other unit has to read the wrong text silently"
            ),
            // The same difference the other way round leaves three bytes to spend
            // where the emoji costs four, so the walk stops inside it. `locate` refuses
            // that rather than slicing there, which is a consumer's one chance to
            // notice.
            Unit::Utf16 => assert_eq!(
                wrong, None,
                "`{db}`: the other unit has to land mid-character, which `locate` refuses"
            ),
        }
    }
}
