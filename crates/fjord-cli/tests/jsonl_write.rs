//! `fjord write` — the JSONL way in, driven as a person drives it.
//!
//! Runs the real binary against a real server, because what is being checked is the
//! whole path: a line of text becomes a fact, a local id becomes a reference, and the
//! refusals name the line somebody has to go and edit.

use std::{
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    thread,
    time::{Duration, Instant},
};

const SCHEMA: &str = "schema demo {\n  \
     predicate File : string\n  \
     predicate Decl : { file : File, name : string, line : int }\n  \
     predicate Digest : { file : File } -> { sha : string }\n  \
     predicate Styles : { file : File } -> { payload : bytes }\n}\n";

struct Serving {
    child: Child,
    root: PathBuf,
    /// The schema file the database was created against, for a test that creates a
    /// second one from a dump.
    schema: PathBuf,
    _dir: tempfile::TempDir,
}

impl Serving {
    /// Stop the server, keeping the directory.
    ///
    /// `export` reads the store directly, which `ops-I1` gives to one process — so a
    /// test that exports has to put the server down first and cannot simply drop this,
    /// which would take the databases with it.
    fn stop(&mut self) {
        let _ = self.child.kill();
        let _ = self.child.wait();
    }

    /// Serve the same root again.
    fn restart(&mut self) {
        let ready = self.root.parent().expect("a parent").join("ready-again");
        let _ = std::fs::remove_file(&ready);

        self.child = Command::new(env!("CARGO_BIN_EXE_fjord"))
            .arg("--data-dir")
            .arg(&self.root)
            .args(["serve", "--ready-file"])
            .arg(&ready)
            .stdout(Stdio::null())
            .stderr(Stdio::inherit())
            .spawn()
            .expect("the server starts");

        let deadline = Instant::now() + Duration::from_secs(30);
        while !ready.exists() {
            assert!(Instant::now() < deadline, "the server never came back");
            thread::sleep(Duration::from_millis(20));
        }
    }
}

impl Drop for Serving {
    fn drop(&mut self) {
        self.stop();
    }
}

fn fjord(root: &Path, args: &[&str]) -> (bool, String, String) {
    let out = Command::new(env!("CARGO_BIN_EXE_fjord"))
        .arg("--data-dir")
        .arg(root)
        .args(args)
        .output()
        .expect("the binary runs");

    (
        out.status.success(),
        String::from_utf8_lossy(&out.stdout).into_owned(),
        String::from_utf8_lossy(&out.stderr).into_owned(),
    )
}

/// A server over a database created against [`SCHEMA`].
fn serving() -> Serving {
    serving_against("demo", SCHEMA)
}

/// The same, against a schema of the caller's — for a fixture that has to declare
/// shapes `SCHEMA` deliberately does not.
fn serving_against(name: &str, source: &str) -> Serving {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");
    let schema = dir.path().join(format!("{name}.sigla"));
    std::fs::write(&schema, source).expect("written");

    let (ok, _, why) = fjord(
        &root,
        &["create", "code", "--schema", schema.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    let ready = dir.path().join("ready");
    let child = Command::new(env!("CARGO_BIN_EXE_fjord"))
        .arg("--data-dir")
        .arg(&root)
        .args(["serve", "--ready-file"])
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

    Serving {
        child,
        root,
        schema,
        _dir: dir,
    }
}

/// Write `lines` to a scratch file and hand back its path.
fn file_of(serving: &Serving, name: &str, lines: &str) -> PathBuf {
    let path = serving.root.parent().expect("a parent").join(name);
    std::fs::write(&path, lines).expect("written");
    path
}

/// **The acceptance criterion**: a file of facts becomes those facts, references and all.
#[test]
fn a_file_of_lines_becomes_the_facts_it_names() {
    let serving = serving();

    let path = file_of(
        &serving,
        "good.jsonl",
        r#"# a file, its declarations, and its digest
{"id": "1", "predicate": "demo.File", "fact": "store/keys.py"}
{"id": 2, "predicate": "demo.Decl", "fact": {"file": "1", "name": "put", "line": 12}}
{"predicate": "demo.Decl", "fact": {"file": "1", "name": "get", "line": 30}}
{"predicate": "demo.Digest", "fact": {"file": "1"}, "value": {"sha": "abc123"}}
"#,
    );

    let (ok, said, why) = fjord(
        &serving.root,
        &["write", "code", path.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");
    assert!(said.contains("4 line(s)"), "{said}");

    // The declarations read back with the file they name, which is the reference having
    // resolved — and a blank line and a `#` note having been skipped rather than refused.
    let (ok, rows, why) = fjord(
        &serving.root,
        &[
            "query",
            "code",
            "X where demo.Decl X",
            "--format",
            "jsonl",
            "--expand",
            "1",
        ],
    );
    assert!(ok, "{why}");
    assert!(rows.contains(r#""file": "store/keys.py""#), "{rows}");
    assert!(rows.contains(r#""name": "put""#), "{rows}");

    // ...and the value side landed.
    let (_, digest, _) = fjord(
        &serving.root,
        &[
            "query",
            "code",
            "{v = D.value} where D = demo.Digest {file = F}",
            "--format",
            "jsonl",
        ],
    );
    assert!(digest.contains("abc123"), "{digest}");

    // Reading the same file again writes nothing: every fact is already there.
    let (ok, again, why) = fjord(
        &serving.root,
        &["write", "code", path.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");
    assert!(again.contains("0 fact(s) written"), "{again}");
}

/// **A forward reference is refused, not held over** — the rule that makes a reader one
/// pass, and the reason a file reads in the order it was built.
#[test]
fn a_reference_to_a_line_that_has_not_happened_is_refused() {
    let serving = serving();

    let path = file_of(
        &serving,
        "forward.jsonl",
        r#"{"predicate": "demo.Decl", "fact": {"file": "later", "name": "x", "line": 1}}
{"id": "later", "predicate": "demo.File", "fact": "a.py"}
"#,
    );

    let (ok, _, why) = fjord(
        &serving.root,
        &["write", "code", path.to_str().expect("utf8")],
    );

    assert!(!ok, "a forward reference is refused");
    assert!(why.contains("forward.jsonl:1"), "the line is named: {why}");
    assert!(why.contains("has not been written yet"), "{why}");

    // And nothing was written — the refusal happened before the batch was sent.
    let (_, count, _) = fjord(
        &serving.root,
        &["query", "code", "X where demo.File X", "--count"],
    );
    assert_eq!(count.trim(), "0", "a refused file writes nothing");
}

/// Every other way a line can be wrong names the line and the field.
#[test]
fn a_line_that_does_not_fit_the_schema_says_which_and_why() {
    let serving = serving();

    for (name, lines, expected) in [
        (
            "reused.jsonl",
            "{\"id\":\"1\",\"predicate\":\"demo.File\",\"fact\":\"a\"}\n\
             {\"id\":\"1\",\"predicate\":\"demo.File\",\"fact\":\"b\"}\n",
            "an id is used once",
        ),
        (
            "kind.jsonl",
            "{\"id\":\"f\",\"predicate\":\"demo.File\",\"fact\":\"a.py\"}\n\
             {\"id\":\"d\",\"predicate\":\"demo.Decl\",\"fact\":{\"file\":\"f\",\"name\":\"x\",\"line\":1}}\n\
             {\"predicate\":\"demo.Digest\",\"fact\":{\"file\":\"d\"},\"value\":{\"sha\":\"z\"}}\n",
            "is a `demo.Decl`, and a `demo.File` is declared here",
        ),
        (
            "unknown.jsonl",
            "{\"predicate\": \"demo.Nope\", \"fact\": \"x\"}\n",
            "is not a predicate of this database",
        ),
        (
            "missing.jsonl",
            "{\"predicate\": \"demo.Decl\", \"fact\": {\"name\": \"x\", \"line\": 1}}\n",
            "fact.file: missing",
        ),
        (
            "scalar.jsonl",
            "{\"id\":\"f\",\"predicate\":\"demo.File\",\"fact\":\"a\"}\n\
             {\"predicate\":\"demo.Decl\",\"fact\":{\"file\":\"f\",\"name\":\"x\",\"line\":\"twelve\"}}\n",
            "expected an integer, found a string",
        ),
        (
            "hash.jsonl",
            "{\"id\": \"#3:7\", \"predicate\": \"demo.File\", \"fact\": \"a\"}\n",
            "which a local id may not",
        ),
        (
            "novalue.jsonl",
            "{\"predicate\":\"demo.File\",\"fact\":\"a\",\"value\":{\"x\":1}}\n",
            "has no value side",
        ),
        // **Base64 is what a `bytes` field must not quietly accept.** The alphabets
        // overlap, so a base64 reader takes every hex string and decodes it to
        // three-quarters of the wrong bytes without a word — which is a database that
        // is silently not the one exported. Hex is narrow enough to say so.
        (
            "base64.jsonl",
            "{\"id\":\"f\",\"predicate\":\"demo.File\",\"fact\":\"a\"}\n\
             {\"predicate\":\"demo.Styles\",\"fact\":{\"file\":\"f\"},\"value\":{\"payload\":\"AP8Qpw==\"}}\n",
            "is not a lowercase hex digit",
        ),
        (
            "oddhex.jsonl",
            "{\"id\":\"f\",\"predicate\":\"demo.File\",\"fact\":\"a\"}\n\
             {\"predicate\":\"demo.Styles\",\"fact\":{\"file\":\"f\"},\"value\":{\"payload\":\"00f\"}}\n",
            "hex has two digits a byte",
        ),
        ("garbage.jsonl", "this is not json\n", "not JSON"),
    ] {
        let path = file_of(&serving, name, lines);
        let (ok, _, why) = fjord(
            &serving.root,
            &["write", "code", path.to_str().expect("utf8")],
        );

        assert!(!ok, "{name} should be refused");
        assert!(why.contains(name), "{name}: the file is named: {why}");
        assert!(
            why.contains(expected),
            "{name}: expected `{expected}`, got: {why}"
        );
    }
}

/// **A reference resolves to an id once its target has been written**, which is what the
/// ids a write reports are for.
///
/// Counted rather than asserted structurally, because the count is the observable: a
/// target referenced from a *later* batch is sent as an id and deduplicates nothing,
/// while one referenced from its own batch is inlined and deduplicates every time. So a
/// file crossing the batch boundary shows far fewer dedups than it has references —
/// without the ids, every one of them would be a dedup.
#[test]
fn a_target_written_in_an_earlier_batch_is_referenced_by_id() {
    let serving = serving();

    // The batch is 10,000 facts, so 25,001 lines is three batches.
    //
    // **Two predicates interleaved, and the extra files named.** A batch is grouped by
    // predicate before it is sent, so the order the ids come back in is not the order the
    // lines were read in — a single-predicate file passes whether or not a name is kept
    // with its fact through that grouping, because there the two orders coincide.
    let mut lines = String::from(r#"{"id": "f", "predicate": "demo.File", "fact": "hot.py"}"#);
    lines.push('\n');
    for n in 0..25_000 {
        if n % 100 == 0 {
            lines.push_str(&format!(
                r#"{{"id": "extra{n}", "predicate": "demo.File", "fact": "other{n}.py"}}"#
            ));
            lines.push('\n');
        }

        lines.push_str(&format!(
            r#"{{"predicate": "demo.Decl", "fact": {{"file": "f", "name": "n{n}", "line": {n}}}}}"#
        ));
        lines.push('\n');
    }

    let path = file_of(&serving, "hot.jsonl", &lines);
    let (ok, said, why) = fjord(
        &serving.root,
        &["write", "code", path.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    // Every line became a fact: the hot file, 250 others, and 25,000 declarations.
    assert!(said.contains("25251 fact(s) written"), "{said}");

    // And the target was inlined only by the batch it shared — which is one batch's
    // worth, not all 25,000.
    let deduped: u64 = said
        .split("written, ")
        .nth(1)
        .and_then(|rest| rest.split_whitespace().next())
        .and_then(|n| n.parse().ok())
        .unwrap_or_else(|| panic!("a dedup count in: {said}"));

    assert!(
        deduped < 15_000,
        "the later batches should reference by id, not re-send the target: {said}"
    );

    // Each file is one fact however often it was named.
    let (_, count, _) = fjord(
        &serving.root,
        &["query", "code", "P where demo.File P", "--count"],
    );
    assert_eq!(count.trim(), "251");
}

/// **An export written back is the same database** — which is the whole claim a portable
/// format makes.
///
/// Asserted on the **content identity**, not on the bytes or the ids. Every id changes: a
/// dump's are its own, and writing it back mints new ones. `ops-I4`'s identity is a
/// multiset hash over each fact's *logical* form, so a copy under different numbering
/// hashes the same — and if it did not, the format would be losing something.
#[test]
fn an_export_written_back_has_the_identity_it_started_with() {
    let mut server = serving();

    let seed = file_of(
        &server,
        "seed.jsonl",
        r#"{"id": "1", "predicate": "demo.File", "fact": "store/keys.py"}
{"id": "2", "predicate": "demo.File", "fact": "query/plan.py"}
{"id": "3", "predicate": "demo.Decl", "fact": {"file": "1", "name": "put", "line": 12}}
{"id": "4", "predicate": "demo.Decl", "fact": {"file": "1", "name": "get", "line": 30}}
{"id": "5", "predicate": "demo.Decl", "fact": {"file": "2", "name": "plan", "line": 3}}
{"predicate": "demo.Digest", "fact": {"file": "2"}, "value": {"sha": "abc"}}
{"predicate": "demo.Styles", "fact": {"file": "1"}, "value": {"payload": "00ff10a7"}}
"#,
    );

    let (ok, _, why) = fjord(
        &server.root,
        &["write", "code", seed.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    let (ok, sealed, why) = fjord(&server.root, &["finish", "code"]);
    assert!(ok, "{why}");

    let identity = sealed
        .split("identity ")
        .nth(1)
        .map(|rest| rest.trim().to_owned())
        .expect("finish reports an identity");

    let schema = server.schema.clone();
    let scratch = server.root.parent().expect("a parent").to_owned();

    // **Both orders, because both claim to be the same facts.** They differ in what a
    // line's neighbours are and in nothing else, so an order that dropped or duplicated
    // one would show here and only here.
    for (order, into) in [("dependency", "back"), ("grouped", "back_compact")] {
        // Export reads the store directly, so the server goes down for it.
        server.stop();

        let dump = scratch.join(format!("{order}.jsonl"));
        let mut args = vec!["export", "code", "--to", dump.to_str().expect("utf8")];
        if order == "grouped" {
            args.push("--compact");
        }

        let (ok, said, why) = fjord(&server.root, &args);
        assert!(ok, "{order}: {why}");
        assert!(said.contains("7 fact(s)"), "{order}: {said}");

        // A second database in the same root, built from that dump alone.
        let (ok, _, why) = fjord(
            &server.root,
            &["create", into, "--schema", schema.to_str().expect("utf8")],
        );
        assert!(ok, "{order}: {why}");

        server.restart();

        let (ok, said, why) = fjord(&server.root, &["write", into, dump.to_str().expect("utf8")]);
        assert!(ok, "{order}: {why}");
        assert!(said.contains("7 fact(s) written"), "{order}: {said}");

        let (ok, resealed, why) = fjord(&server.root, &["finish", into]);
        assert!(ok, "{order}: {why}");

        assert!(
            resealed.contains(&identity),
            "{order}: the round trip changed the identity: {identity} is not in {resealed}"
        );
    }
}

/// **`--compact` writes a predicate's facts as one run**, and the predicates themselves
/// still in dependency order.
///
/// Both halves matter and only together: a run per predicate that put `Decl` before the
/// `File` it names would be a file nothing can read back, and predicates in dependency
/// order that interleaved their facts would not be grouped at all.
#[test]
fn a_compact_export_writes_each_predicate_as_one_run() {
    let mut server = serving();

    let seed = file_of(
        &server,
        "seed.jsonl",
        r#"{"id": "1", "predicate": "demo.File", "fact": "a.py"}
{"id": "2", "predicate": "demo.Decl", "fact": {"file": "1", "name": "put", "line": 12}}
{"id": "3", "predicate": "demo.File", "fact": "b.py"}
{"id": "4", "predicate": "demo.Decl", "fact": {"file": "3", "name": "get", "line": 30}}
"#,
    );

    let (ok, _, why) = fjord(
        &server.root,
        &["write", "code", seed.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    server.stop();

    let dump = server
        .root
        .parent()
        .expect("a parent")
        .join("compact.jsonl");
    let (ok, _, why) = fjord(
        &server.root,
        &[
            "export",
            "code",
            "--to",
            dump.to_str().expect("utf8"),
            "--compact",
        ],
    );
    assert!(ok, "{why}");

    let text = std::fs::read_to_string(&dump).expect("the dump reads");
    let order: Vec<String> = text
        .lines()
        .filter_map(|line| {
            let at = line.find("\"predicate\":\"")? + 13;
            let rest = &line[at..];
            Some(rest[..rest.find('"')?].to_owned())
        })
        .collect();

    assert_eq!(
        order,
        vec!["demo.File", "demo.File", "demo.Decl", "demo.Decl"],
        "{text}"
    );
}

/// **Every construct the schema language has, in one database.**
///
/// `SCHEMA` above is the shape a person writes; this is the shape the *format* has to
/// survive, and the two are not the same list. What is here because it was not there:
/// a bare `int` and a bare `bytes` key, a record nested in a record, a union in a key
/// and a union on the value side, an alternative with an empty payload and ones
/// carrying a scalar, a record and a **reference**, a predicate that names itself, a
/// pair that name each other, and one fact naming the same target twice.
///
/// The last two are what the ordering has to answer for. A self-reference and a
/// mutually recursive pair are components a topological order cannot break, so
/// `--compact` falls back to the depth-first walk inside them — a path nothing else
/// reaches.
const LANGUAGE: &str = r#"schema lang {
  predicate Text : string
  predicate Count : int
  predicate Blob : bytes

  predicate Span : { of : Text, at : { start : int, length : int } } -> { label : string, raw : bytes }

  predicate Tagged : { pick : { none = 0 | some : int = 7 | pair : { a : int, b : string } = 900 | at : Text = 3 } }

  predicate Maybe : { of : Text } -> { held : { nothing = 0 | just : Text = 1 } }

  predicate Node : { name : string, parent : { nothing = 0 | just : Node = 1 } }

  predicate Left : { name : string, right : { nothing = 0 | just : Right = 1 } }
  predicate Right : { name : string, left : { nothing = 0 | just : Left = 1 } }

  predicate Edge : { from : Node, to : Node }
}
"#;

/// Facts over [`LANGUAGE`], one of every shape it can express.
///
/// The scalars are drawn at their **edges** rather than in the middle: `i64::MIN` and
/// `i64::MAX` because a renderer that went through `f64` would lose them silently, the
/// empty string and the empty byte string because a length-prefixed encoding is where
/// an off-by-one lives, a NUL inside a string because the storage codec escapes it and
/// JSON does not, and a string of JSON's own metacharacters because this is a format a
/// person edits by hand.
const LANGUAGE_FACTS: &str = r#"# one of every shape the language can express
{"id": "empty", "predicate": "lang.Text", "fact": ""}
{"id": "uni", "predicate": "lang.Text", "fact": "héllo → 😀"}
{"id": "meta", "predicate": "lang.Text", "fact": "quote\" back\\slash \n \t end"}
{"id": "nul", "predicate": "lang.Text", "fact": "a\u0000b"}
{"predicate": "lang.Count", "fact": -9223372036854775808}
{"predicate": "lang.Count", "fact": 9223372036854775807}
{"predicate": "lang.Count", "fact": 0}
{"predicate": "lang.Count", "fact": -1}
{"predicate": "lang.Blob", "fact": ""}
{"predicate": "lang.Blob", "fact": "00"}
{"predicate": "lang.Blob", "fact": "ff00ff10a7"}
{"predicate": "lang.Span", "fact": {"of": "uni", "at": {"start": 0, "length": 12}}, "value": {"label": "x", "raw": "deadbeef"}}
{"predicate": "lang.Tagged", "fact": {"pick": {"none": {}}}}
{"predicate": "lang.Tagged", "fact": {"pick": {"some": 42}}}
{"predicate": "lang.Tagged", "fact": {"pick": {"pair": {"a": 1, "b": "two"}}}}
{"predicate": "lang.Tagged", "fact": {"pick": {"at": "meta"}}}
{"predicate": "lang.Maybe", "fact": {"of": "empty"}, "value": {"held": {"nothing": {}}}}
{"predicate": "lang.Maybe", "fact": {"of": "uni"}, "value": {"held": {"just": "meta"}}}
{"id": "n1", "predicate": "lang.Node", "fact": {"name": "root", "parent": {"nothing": {}}}}
{"id": "n2", "predicate": "lang.Node", "fact": {"name": "child", "parent": {"just": "n1"}}}
{"id": "n3", "predicate": "lang.Node", "fact": {"name": "leaf", "parent": {"just": "n2"}}}
{"id": "l1", "predicate": "lang.Left", "fact": {"name": "l1", "right": {"nothing": {}}}}
{"id": "r1", "predicate": "lang.Right", "fact": {"name": "r1", "left": {"just": "l1"}}}
{"id": "l2", "predicate": "lang.Left", "fact": {"name": "l2", "right": {"just": "r1"}}}
{"id": "r2", "predicate": "lang.Right", "fact": {"name": "r2", "left": {"just": "l2"}}}
{"predicate": "lang.Edge", "fact": {"from": "n1", "to": "n3"}}
{"predicate": "lang.Edge", "fact": {"from": "n2", "to": "n2"}}
"#;

/// The fact count and identity a `finish` reported.
fn sealed_as(said: &str) -> (String, String) {
    let facts = said
        .split(": ")
        .nth(1)
        .and_then(|rest| rest.split(" facts").next())
        .map(str::to_owned)
        .expect("finish reports a fact count");

    let identity = said
        .split("identity ")
        .nth(1)
        .map(|rest| rest.trim().to_owned())
        .expect("finish reports an identity");

    (facts, identity)
}

/// Write `facts` into `code`, seal it, and check that an export written back seals to
/// the same thing — **under both orders**, because both claim to be the same facts.
///
/// Asserted on the content identity and the fact count together. The identity is the
/// claim; the count is what tells a dropped fact from a changed one when it fails.
fn the_round_trip_holds(server: &mut Serving, facts: &str) {
    let seed = file_of(server, "seed.jsonl", facts);
    let (ok, _, why) = fjord(
        &server.root,
        &["write", "code", seed.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    let (ok, sealed, why) = fjord(&server.root, &["finish", "code"]);
    assert!(ok, "{why}");
    let started_as = sealed_as(&sealed);

    let schema = server.schema.clone();
    let scratch = server.root.parent().expect("a parent").to_owned();

    for (order, into) in [("dependency", "back"), ("grouped", "back_compact")] {
        // Export reads the store directly, so the server goes down for it.
        server.stop();

        let dump = scratch.join(format!("{order}.jsonl"));
        let mut args = vec!["export", "code", "--to", dump.to_str().expect("utf8")];
        if order == "grouped" {
            args.push("--compact");
        }

        let (ok, _, why) = fjord(&server.root, &args);
        assert!(ok, "{order}: {why}");

        let (ok, _, why) = fjord(
            &server.root,
            &["create", into, "--schema", schema.to_str().expect("utf8")],
        );
        assert!(ok, "{order}: {why}");

        server.restart();

        let (ok, _, why) = fjord(&server.root, &["write", into, dump.to_str().expect("utf8")]);
        assert!(ok, "{order}: {why}");

        let (ok, resealed, why) = fjord(&server.root, &["finish", into]);
        assert!(ok, "{order}: {why}");

        assert_eq!(
            sealed_as(&resealed),
            started_as,
            "{order}: the round trip changed the database"
        );
    }
}

/// **Every construct survives, under both orders.**
///
/// The acceptance criterion for the format: what the language can say, the format can
/// carry. A constructor this misses is one the round trip has never been asked about —
/// which is how a `bytes` field spent a day being silently corrupted.
#[test]
fn every_construct_in_the_schema_language_survives_a_round_trip() {
    let mut server = serving_against("lang", LANGUAGE);
    the_round_trip_holds(&mut server, LANGUAGE_FACTS);
}

/// **A component that can cycle is written depth first, inside its own group.**
///
/// `lang.Left` and `lang.Right` name each other, so no order over the two predicates
/// exists and `--compact` cannot group them the way it groups the rest. What it must
/// not do is emit either group whole: the facts interleave, because that is the only
/// order in which a reference names an earlier line.
#[test]
fn a_cyclic_component_interleaves_rather_than_grouping() {
    let mut server = serving_against("lang", LANGUAGE);

    let seed = file_of(&server, "seed.jsonl", LANGUAGE_FACTS);
    let (ok, _, why) = fjord(
        &server.root,
        &["write", "code", seed.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    server.stop();

    let dump = server
        .root
        .parent()
        .expect("a parent")
        .join("compact.jsonl");
    let (ok, _, why) = fjord(
        &server.root,
        &[
            "export",
            "code",
            "--to",
            dump.to_str().expect("utf8"),
            "--compact",
        ],
    );
    assert!(ok, "{why}");

    let text = std::fs::read_to_string(&dump).expect("the dump reads");
    let cycle: Vec<&str> = text
        .lines()
        .filter_map(|line| {
            if line.contains("\"lang.Left\"") {
                Some("L")
            } else if line.contains("\"lang.Right\"") {
                Some("R")
            } else {
                None
            }
        })
        .collect();

    assert_eq!(cycle, vec!["L", "R", "L", "R"], "{text}");
}

/// **The browser's reader of this format agrees with the tool's.**
///
/// There are two readers of one grammar — `fjord_cli::jsonl`, which turns a line into a
/// fact on the wire, and `fjord_inspect::jsonl`, which turns one into a row in a
/// `MemStore` for a page that cannot intern. They share no code, which is exactly how a
/// `bytes` field came to be written as hex and read as base64 for a day.
///
/// So the export goes through the other one, and the facts it builds are queried. What
/// this catches that nothing else does is a disagreement about the *grammar* — an
/// encoding, an escape, an alternative's name — because a database and a `MemStore`
/// built from one file have to answer a query the same way.
#[test]
fn the_browsers_reader_agrees_with_the_tools() {
    let mut server = serving_against("lang", LANGUAGE);

    let seed = file_of(&server, "seed.jsonl", LANGUAGE_FACTS);
    let (ok, _, why) = fjord(
        &server.root,
        &["write", "code", seed.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    server.stop();

    let dump = server.root.parent().expect("a parent").join("both.jsonl");
    let (ok, _, why) = fjord(
        &server.root,
        &["export", "code", "--to", dump.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");

    let text = std::fs::read_to_string(&dump).expect("the dump reads");
    let schema = fjord_schema::syntax::read("lang", LANGUAGE).expect("the schema reads");

    let read = fjord_inspect::jsonl::read(&text, &schema).expect("the other reader accepts it");
    assert_eq!(read.facts, text.lines().count(), "every line became a fact");

    // The shapes an encoding disagreement hides in: a bytes payload, a string of
    // JSON's own metacharacters, and a reference reached *through a union* — the three
    // places the two readers could differ and still both look like they worked.
    let answers = |query: &str| -> Vec<serde_json::Value> {
        let rows = fjord_inspect::rows::run_over(&schema, query, read.store.clone());
        assert!(
            rows.diagnostics.is_empty(),
            "{query}: {:?}",
            rows.diagnostics
        );
        rows.rows.into_iter().map(|row| row.value).collect()
    };

    let blobs = answers("X where lang.Blob X");
    assert_eq!(blobs.len(), 3, "{blobs:?}");

    let spans = answers("S.value where S = lang.Span _");
    assert_eq!(spans.len(), 1, "{spans:?}");
    assert_eq!(
        spans[0].get("raw").and_then(serde_json::Value::as_str),
        Some("deadbeef"),
        "the bytes payload came back as it went in: {spans:?}"
    );

    // Through the union: `Tagged`'s `at` arm names a `Text`, and the string it names is
    // the one with the quote, the backslash, the newline and the tab in it.
    let through = answers("{t = S} where lang.Tagged {pick = {at = T}}; T = lang.Text S");
    assert_eq!(through.len(), 1, "{through:?}");
    assert_eq!(
        through[0].get("t").and_then(serde_json::Value::as_str),
        Some("quote\" back\\slash \n \t end"),
        "the metacharacters survived both readers: {through:?}"
    );

    let texts = answers("X where lang.Text X");
    assert_eq!(texts.len(), 4, "{texts:?}");
    assert!(
        texts.iter().any(|text| text.as_str() == Some("a\u{0}b")),
        "the NUL survived both readers: {texts:?}"
    );
}

/// **A database holding no facts exports a file holding no lines**, and that file reads.
///
/// The degenerate end of the format. An exporter that wrote a header, or a reader that
/// needed one, would be caught by nothing else — and an empty index is what a CI job
/// that silently indexed nothing produces.
#[test]
fn an_empty_database_exports_an_empty_file() {
    let mut server = serving_against("lang", LANGUAGE);

    let (ok, _, why) = fjord(&server.root, &["finish", "code", "--allow-zero-facts"]);
    assert!(ok, "{why}");

    server.stop();

    let dump = server.root.parent().expect("a parent").join("empty.jsonl");
    let (ok, said, why) = fjord(
        &server.root,
        &["export", "code", "--to", dump.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");
    assert!(said.contains("0 fact(s)"), "{said}");

    let text = std::fs::read_to_string(&dump).expect("the dump reads");
    assert!(text.is_empty(), "{text:?}");

    let schema = fjord_schema::syntax::read("lang", LANGUAGE).expect("the schema reads");
    let read = fjord_inspect::jsonl::read(&text, &schema).expect("an empty file reads");
    assert_eq!(read.facts, 0);

    // And the tool reads it back into a database of its own without complaint.
    let (ok, _, why) = fjord(
        &server.root,
        &[
            "create",
            "back",
            "--schema",
            server.schema.to_str().expect("utf8"),
        ],
    );
    assert!(ok, "{why}");

    server.restart();

    let (ok, said, why) = fjord(
        &server.root,
        &["write", "back", dump.to_str().expect("utf8")],
    );
    assert!(ok, "{why}");
    assert!(said.contains("0 fact(s) written"), "{said}");
}

proptest::proptest! {
    // **Few cases, because each one is a server and a dozen processes.** Reach is
    // `commands::export`'s own battery, which puts thousands of documents through the
    // reader and the writer in-process. What is left for this one is the part that only
    // exists as a process: `fjord write`'s reader, the wire, the funnel and fjall — so
    // it runs the same claim through the tool enough times to catch a disagreement
    // between reading a document into a model store and writing one into a database.
    //
    // **No persisted seeds.** Proptest wants to write a counterexample beside the
    // source, and from a test binary it cannot find one — so it drops a file next to
    // this one that the tree's `proptest-regressions/` ignore rule does not match, and
    // that nothing else in the repository keeps. A counterexample here is reported and
    // acted on, not carried.
    #![proptest_config(proptest::prelude::ProptestConfig {
        cases: 16,
        max_shrink_iters: 64,
        failure_persistence: None,
        ..proptest::prelude::ProptestConfig::default()
    })]

    /// **Any schema the language admits, any document over it, both orders.**
    ///
    /// The fixture beside this one names the constructors; this one combines them. What
    /// it is looking for is a shape where the export's ordering, its rendering or the
    /// reader's parsing disagree — and the assertion is the same one a person would
    /// make by hand, that the database you get back is the database you had.
    #[test]
    fn any_schema_and_document_survive_the_binary(
        drawn in fjord_wire::value::proptest::arb_schema_and_fact(),
        reversed in proptest::prelude::any::<bool>(),
    ) {
        // Reversed half the time, so the tool sees schemas whose references point at
        // *later* predicates — the case a generated schema never reaches on its own,
        // and the one the export's walk exists for.
        let source = fjord_schema::syntax::print::print(&drawn.schema());
        let source = if reversed {
            fjord_cli::document::with_the_predicate_order_reversed(&source)
        } else {
            source
        };

        let schema = fjord_schema::syntax::read("gen", &source)
            .expect("a printed schema reads back");

        let mut draws = fjord_cli::document::Draws::new(
            drawn.ints.clone(),
            drawn.texts.clone(),
            drawn.picks.clone(),
        );

        let document = fjord_cli::document::a_document_over(&schema, &mut draws);

        let mut server = serving_against("gen", &source);
        the_round_trip_holds(&mut server, &document);
    }
}
