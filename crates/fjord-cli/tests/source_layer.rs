//! **The source layer, against a real database.**
//!
//! Nine predicates arrived at once, and an unexercised predicate is a name in a file
//! rather than a layer: this writes a small hand-built index over `schemas/src.sigla` and
//! asks one question of each of them.
//!
//! Two of the questions are not "does it answer" but "does it answer *this*" — the
//! offset→line recipe's three cases, including the one the schema comment says a consumer
//! must handle and issue #39 does not state.

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

const SRC: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/src.sigla");

/// The file this index is about, and the four lines of it. Deliberately **not** ASCII:
/// the third line holds a codepoint above the BMP, so `start` and `cstart` diverge and a
/// test that only used ASCII would prove nothing about the two.
const PATH: &str = "src/main.rs";
const LINES: &[&str] = &[
    "fn main() {",            // start 0,  cstart 0
    "    let x = 1;",         // start 12, cstart 12
    "    // \u{1f600} smile", // start 28, cstart 28  (the emoji: 4 bytes, 2 units)
    "}",                      // start 45, cstart 43
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
    let json = fjord(root, &["query", "src", query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

/// The offsets a producer owes, computed the way one has to: UTF-8 bytes for `start`,
/// UTF-16 code units for `cstart`, accumulated over the lines rather than recomputed.
fn offsets() -> Vec<(i64, i64, i64)> {
    let (mut start, mut cstart) = (0i64, 0i64);
    LINES
        .iter()
        .map(|line| {
            let row = (start, line.len() as i64, cstart);
            start += line.len() as i64 + 1; // the line, plus its `\n`
            cstart += line.encode_utf16().count() as i64 + 1;
            row
        })
        .collect()
}

fn file_fact() -> WireFact {
    WireFact {
        predicate: PredicateId(0),
        key: WireValue::Str(PATH.to_owned()),
        value: None,
    }
}

fn of_file() -> WireValue {
    WireValue::Ref(WireRef::Nested(Box::new(file_fact())))
}

/// **One query per predicate, and each asserts rows.**
#[test]
fn every_predicate_of_the_source_layer_answers() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(&root, &["create", "src", "--schema", SRC]);
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "src",
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

    let mut writer = Connection::open(&endpoint, "src", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    let offsets = offsets();
    let text_bytes: i64 = offsets.iter().map(|(_, bytes, _)| bytes + 1).sum();

    let mut blocks: Vec<(PredicateId, Vec<WireFact>)> = vec![
        (id("src.File"), vec![file_fact()]),
        (
            id("src.Symbol"),
            vec![WireFact {
                predicate: id("src.Symbol"),
                key: WireValue::Str("scip-rust rust . . main().".to_owned()),
                value: None,
            }],
        ),
        (
            id("src.FileLanguage"),
            vec![WireFact {
                predicate: id("src.FileLanguage"),
                key: WireValue::Record(Box::from([of_file()])),
                // `rust` is discriminant 6, and the alternative carries the empty record.
                value: Some(WireValue::Record(Box::from([WireValue::Union {
                    disc: 6,
                    value: Box::new(WireValue::Record(Box::from([]))),
                }]))),
            }],
        ),
        (
            id("src.FileDigest"),
            vec![WireFact {
                predicate: id("src.FileDigest"),
                key: WireValue::Record(Box::from([of_file()])),
                value: Some(WireValue::Record(Box::from([WireValue::Str(
                    "sha256:deadbeef".to_owned(),
                )]))),
            }],
        ),
        (
            id("src.FileOrigin"),
            vec![WireFact {
                predicate: id("src.FileOrigin"),
                key: WireValue::Record(Box::from([of_file()])),
                value: Some(WireValue::Record(Box::from([
                    WireValue::Str("github.com/boxops-uk/fjord".to_owned()),
                    WireValue::Str("5ac4f0e".to_owned()),
                ]))),
            }],
        ),
        (
            id("src.FileInfo"),
            vec![WireFact {
                predicate: id("src.FileInfo"),
                key: WireValue::Record(Box::from([of_file()])),
                value: Some(WireValue::Record(Box::from([
                    WireValue::Int(text_bytes),
                    WireValue::Int(LINES.len() as i64),
                    // `true_` is discriminant 1.
                    WireValue::Union {
                        disc: 1,
                        value: Box::new(WireValue::Record(Box::from([]))),
                    },
                ]))),
            }],
        ),
    ];

    blocks.push((
        id("src.FileLine"),
        LINES
            .iter()
            .zip(&offsets)
            .enumerate()
            .map(|(index, (text, (start, bytes, cstart)))| WireFact {
                predicate: id("src.FileLine"),
                key: WireValue::Record(Box::from([of_file(), WireValue::Int(index as i64 + 1)])),
                value: Some(WireValue::Record(Box::from([
                    WireValue::Str((*text).to_owned()),
                    WireValue::Int(*start),
                    WireValue::Int(*bytes),
                    WireValue::Int(*cstart),
                ]))),
            })
            .collect(),
    ));

    blocks.push((
        id("src.FileLineAt"),
        offsets
            .iter()
            .enumerate()
            .map(|(index, (start, _, _))| WireFact {
                predicate: id("src.FileLineAt"),
                key: WireValue::Record(Box::from([
                    of_file(),
                    WireValue::Int(*start),
                    WireValue::Int(index as i64 + 1),
                ])),
                value: None,
            })
            .collect(),
    ));

    blocks.push((
        id("src.FileLineStyles"),
        vec![WireFact {
            predicate: id("src.FileLineStyles"),
            key: WireValue::Record(Box::from([of_file(), WireValue::Int(1)])),
            // Opaque to the schema and to this test: what a producer writes here is
            // its business, and `config.Setting {dimension = "style-encoding"}` says
            // what it was. These are the first bytes of an LSP semantic-tokens array —
            // one token, delta 0/0, length 6, type 0, no modifiers.
            value: Some(WireValue::Record(Box::from([WireValue::Bytes(vec![
                0x00, 0x00, 0x06, 0x00, 0x00,
            ])]))),
        }],
    ));

    for (predicate, facts) in &blocks {
        let written = writer
            .write(*predicate, facts)
            .unwrap_or_else(|err| panic!("writing {predicate:?}: {err}"));
        assert_eq!(written.created as usize, facts.len(), "{predicate:?}");
    }
    drop(writer);

    // ---- one question per predicate ---------------------------------------------
    assert_eq!(
        rows(&root, "F where src.File F"),
        vec![serde_json::json!(PATH)]
    );
    assert_eq!(rows(&root, "S where src.Symbol S").len(), 1);

    // A value is projected whole, never field-wise (`nyi/value-field`), so each of these
    // asks for the fact and reads its value.
    for (what, query, expected) in [
        (
            "the digest",
            "X.value where X = src.FileDigest _",
            serde_json::json!({"digest": "sha256:deadbeef"}),
        ),
        (
            "the origin",
            "X.value where X = src.FileOrigin _",
            serde_json::json!({"repo": "github.com/boxops-uk/fjord", "revision": "5ac4f0e"}),
        ),
        (
            "the language",
            "X.value where X = src.FileLanguage _",
            serde_json::json!({"language": {"rust": {}}}),
        ),
        (
            "the file's shape",
            "X.value where X = src.FileInfo _",
            serde_json::json!({"bytes": 47, "lines": 4, "endsInNewline": {"true_": {}}}),
        ),
        (
            "the style runs",
            "X.value where X = src.FileLineStyles {file = F, line = 1}",
            // `bytes` renders as lowercase hex.
            serde_json::json!({"styles": "0000060000"}),
        ),
    ] {
        assert_eq!(rows(&root, query), vec![expected], "{what}");
    }

    // **A window is a range on the last key field** — lines 2 and 3, and not 1 or 4.
    let window = rows(
        &root,
        "X.value where X = src.FileLine {file = F, line = L}; L >= 2; L < 4",
    );
    assert_eq!(window.len(), 2, "{window:#?}");
    assert_eq!(window[0]["text"], serde_json::json!(LINES[1]));

    // **`start` and `cstart` diverge after the non-BMP line**, which is the whole reason
    // both are stored. Line 4 begins at byte 45 and code unit 43.
    let last = rows(&root, "X.value where X = src.FileLine {file = F, line = 4}");
    assert_eq!(last[0]["start"], serde_json::json!(45));
    assert_eq!(last[0]["cstart"], serde_json::json!(43));
}

/// **The offset→line recipe, including the case the issue does not state.**
///
/// `FileLineAt` is keyed `{file, start, line}` and there is no descending seek and no
/// `LIMIT` in sigla, so the shape is a range upward bounded by the client. Three cases,
/// and the third is the one a consumer must handle:
///
/// - an offset exactly at a line's start: the first row **is** the answer;
/// - an offset mid-line: the first row is the line *after*, so the answer is `line - 1`;
/// - an offset at or past the **last** line's start: the range is **empty**, and the
///   consumer falls back to `FileInfo.lines`. That is the common case for a reference in
///   the last line of a file, and it is not optional.
#[test]
fn offset_to_line_has_three_cases_and_the_third_is_empty() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(&root, &["create", "src", "--schema", SRC]);
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "src",
        Arc::new(fjord_cli::sample_schema::schema()),
        Mode::ReadOnly,
        false,
    )
    .expect("a probe connection");
    let served = Arc::new(probe.served_schema().expect("the served schema"));
    drop(probe);

    let at = served
        .find_position("src.FileLineAt")
        .map(|(id, _)| id)
        .expect("src.FileLineAt");

    let mut writer = Connection::open(&endpoint, "src", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    let offsets = offsets();
    let facts: Vec<WireFact> = offsets
        .iter()
        .enumerate()
        .map(|(index, (start, _, _))| WireFact {
            predicate: at,
            key: WireValue::Record(Box::from([
                of_file(),
                WireValue::Int(*start),
                WireValue::Int(index as i64 + 1),
            ])),
            value: None,
        })
        .collect();
    writer.write(at, &facts).expect("the offsets are accepted");
    drop(writer);

    let upward = |offset: i64| {
        rows(
            &root,
            // **A comparison statement, not `{offset}..`.** `..` is the *string* prefix
            // operator; an integer range is `S >= n`, which the level that captures `S`
            // folds into the seek. The schema comment said `start = X..` and was wrong —
            // it is corrected there too.
            &format!("L where src.FileLineAt {{file = F, start = S, line = L}}; S >= {offset}"),
        )
    };

    // Exact: line 2 begins at byte 12, and the first row up from there is line 2 itself.
    assert_eq!(upward(12).first(), Some(&serde_json::json!(2)));

    // Mid-line: byte 15 is inside line 2, so the first row up is line 3 — and the answer
    // the consumer wants is 3 − 1. Arithmetic on a row already in hand, no second seek.
    assert_eq!(upward(15).first(), Some(&serde_json::json!(3)));

    // **At the last line's start is still an exact hit.** Line 4 begins at 45, and the
    // range upward from 45 finds it. Worth pinning because both #39 and the schema
    // comment said "at *or past* the last line's start: the range is empty", and the
    // "at" half of that is wrong — it is the one case that answers exactly.
    assert_eq!(upward(45), vec![serde_json::json!(4)]);

    // **Past it: empty**, and this is the case a consumer must handle. There is nothing
    // above the last line, so the range returns nothing and the answer is
    // `FileInfo.lines` — which is the common case for a reference *in* the last line of
    // a file, not an edge. A consumer that read an empty range as "not found" would fail
    // on every one of them.
    assert!(upward(46).is_empty(), "{:#?}", upward(46));
    assert!(upward(1000).is_empty());
}
