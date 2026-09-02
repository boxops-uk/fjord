//! **`config.Setting`, against a real database.**
//!
//! The claim is that a tool holding many handles can ask *which one is this* and get the
//! answer out of the database rather than out of its name — so the test is a database
//! created from `schemas/config.sigla`, the full reserved set written into it, and the
//! three questions a consumer actually asks.
//!
//! An integration test because a predicate nothing has queried is a name in a file.

use std::sync::Arc;

use fjord_client::{Connection, Endpoint, Mode};
use fjord_schema::schema::PredicateId;
use fjord_wire::{WireFact, WireValue};

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
    ("symbol-scheme", "scip-csharp"),
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
    let json = fjord(root, &["query", "config", query, "--format", "json"]);
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
/// What is *not* here is the conversion through `src.FileLine.start`/`cstart`: that
/// predicate arrives with the source layer, and the test that maps a span through it
/// rides with it.
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
