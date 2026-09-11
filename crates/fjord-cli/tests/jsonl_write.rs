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
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");
    let schema = dir.path().join("demo.sigla");
    std::fs::write(&schema, SCHEMA).expect("written");

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

    let schema = server.root.parent().expect("a parent").join("demo.sigla");
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
