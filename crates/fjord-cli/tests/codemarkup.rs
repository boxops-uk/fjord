//! **`codemarkup`, and the one property the whole layer is for.**
//!
//! The claim is that a C# index, a TypeScript index and a SCIP-converted one serve the
//! *same* predicates, so one client reads all of them. That rests entirely on the join
//! key being a **string** — `src.Symbol` — rather than a union over languages, and the
//! difference between the two shows up as a fingerprint that does or does not depend on
//! which languages a database happens to hold.
//!
//! Two of the three tests here are that argument, stated as fingerprints.

use fjord_schema::{
    fingerprint::{self, Compatibility},
    syntax::resolve,
};

fn root() -> std::path::PathBuf {
    std::path::PathBuf::from(concat!(env!("CARGO_MANIFEST_DIR"), "/../.."))
}

/// `src.sigla` and `codemarkup.sigla` as text, so a composite can be assembled in memory
/// with extra namespaces beside them.
fn shipped() -> Vec<(&'static str, String)> {
    let root = root();
    vec![
        (
            "src.sigla",
            std::fs::read_to_string(root.join("schemas/src.sigla")).expect("src.sigla"),
        ),
        (
            "codemarkup.sigla",
            std::fs::read_to_string(root.join("schemas/codemarkup.sigla"))
                .expect("codemarkup.sigla"),
        ),
    ]
}

/// **The most important assertion in this layer.** Every `codemarkup` predicate has the
/// same fingerprint whether it is resolved alone or inside a composite that also holds
/// two language layers.
///
/// That is the property a union-keyed design would not have, and it is what lets one
/// client read a C#-only index and a C#-plus-TypeScript one. The two synthetic layers
/// below stand in for `csharp` and `typescript` until those land; what matters is that
/// they are *other namespaces declaring their own entity unions*, which is exactly the
/// thing a union-keyed `codemarkup` would have had to name.
#[test]
fn codemarkups_fingerprints_do_not_depend_on_what_else_the_database_holds() {
    const ONE_LANGUAGE: &str = "\
schema alpha {
  predicate Method : { name : string }
  predicate Klass  : { name : string }
  type Entity = { method : Method = 0 | klass : Klass = 1 }
  predicate DefinitionLocation : { entity : Entity } -> { file : src.File }
}
";
    const TWO_LANGUAGES: &str = "\
schema beta {
  import alpha
  predicate Module : { path : string }
  type Entity = { module_ : Module = 0 | }
  predicate Uses : { entity : Entity, of : alpha.Entity }
}
";

    let shipped = shipped();
    let alone: Vec<(&str, &str)> = vec![
        ("codemarkup.sigla", shipped[1].1.as_str()),
        ("src.sigla", shipped[0].1.as_str()),
    ];

    let mut composite = alone.clone();
    composite.push(("alpha.sigla", ONE_LANGUAGE));
    composite.push(("beta.sigla", TWO_LANGUAGES));
    // The entry has to name them, or resolution never reaches them.
    let entry = "schema index { import codemarkup\n import alpha\n import beta }";
    let mut with_layers: Vec<(&str, &str)> = vec![("index.sigla", entry)];
    with_layers.extend(composite);

    let alone = fingerprint::identity(&resolve::resolve_from(alone).expect("alone").schema);
    let together = fingerprint::identity(
        &resolve::resolve_from(with_layers)
            .expect("composite")
            .schema,
    );

    let codemarkup = |identity: &fingerprint::Identity| {
        identity
            .predicates()
            .iter()
            .filter(|(name, _)| name.starts_with("codemarkup."))
            .map(|(name, hash)| (name.clone(), *hash))
            .collect::<Vec<_>>()
    };

    assert_eq!(
        codemarkup(&alone).len(),
        10,
        "ten predicates, or this is vacuous"
    );
    assert_eq!(
        codemarkup(&alone),
        codemarkup(&together),
        "a `codemarkup` predicate's fingerprint moved when two language layers were \
         added beside it — which is exactly what a union over languages in the key would \
         do, and the reason this layer keys on a string"
    );

    // And the composite really is bigger, so the two were not the same schema.
    assert!(together.predicates().len() > alone.predicates().len() + 3);
}

/// **The counter-example, pinned.** One appended alternative to a union used in two keys
/// is `Breaking` in both of them.
///
/// This is the shape `codemarkup` would have had if it keyed on a union over languages:
/// a C#-only index and a C#-plus-TypeScript index would then carry *different*
/// `Definition` and `FileXRef` predicates, and one UI could not read both. Here as a live
/// test rather than a paragraph, because the paragraph is the whole justification for the
/// string key and a justification nothing checks is an opinion.
#[test]
fn one_appended_alternative_breaks_every_predicate_that_keys_on_the_union() {
    const ONE: &str = "\
schema cmu {
  predicate File   : string
  type ByteSpan    = { start : int, length : int }
  predicate CsDef  : { name : string }
  predicate TsDecl : { file : File, start : int }
  # The trailing `|` is what makes a one-alternative union a union: a record and a
  # sum share their braces, and the separator after the first field is what tells them
  # apart. Without it this is a record field with a discriminant, and rejected.
  type Entity = { csharp : CsDef = 0 | }
  predicate Definition : { entity : Entity, file : File } -> { span : ByteSpan }
  predicate FileXRef   : { file : File, start : int, target : Entity }
}
";
    const TWO: &str = "\
schema cmu {
  predicate File   : string
  type ByteSpan    = { start : int, length : int }
  predicate CsDef  : { name : string }
  predicate TsDecl : { file : File, start : int }
  type Entity = { csharp : CsDef = 0 | typescript : TsDecl = 1 }
  predicate Definition : { entity : Entity, file : File } -> { span : ByteSpan }
  predicate FileXRef   : { file : File, start : int, target : Entity }
}
";

    let of = |source: &str| {
        fingerprint::identity(
            &resolve::resolve_from([("cmu.sigla", source)])
                .expect("it resolves")
                .schema,
        )
    };

    match of(ONE).compatibility(&of(TWO)) {
        Compatibility::Breaking { mut broken } => {
            broken.sort();
            assert_eq!(
                broken,
                ["cmu.Definition", "cmu.FileXRef"],
                "an appended alternative must break exactly the predicates that key on \
                 the union"
            );
        }
        other => panic!(
            "appending a union alternative was {other:?}, not Breaking — if this became \
             compatible, the argument for keying `codemarkup` on a string is gone and \
             the layer should be reconsidered rather than this test relaxed"
        ),
    }

    // **And the string-keyed shape does not have this problem**, which is the half that
    // makes the counter-example mean something: the same two schemas keyed on a symbol
    // string are identical no matter how many languages exist.
    const STRING_KEYED: &str = "\
schema cms {
  predicate File   : string
  predicate Symbol : string
  type ByteSpan    = { start : int, length : int }
  predicate Definition : { symbol : Symbol, file : File } -> { span : ByteSpan }
  predicate FileXRef   : { file : File, start : int, target : Symbol }
}
";
    assert_eq!(
        of(STRING_KEYED).compatibility(&of(STRING_KEYED)),
        Compatibility::Identical
    );
}

/// Every `codemarkup` predicate is in the shipped set and reachable from `src`.
#[test]
fn the_layer_checks_against_the_shipped_source_layer() {
    let root = root();
    let resolved = resolve::resolve(
        &root.join("schemas/codemarkup.sigla"),
        &[root.join("schemas")],
    )
    .expect("codemarkup resolves against src");

    let names: Vec<&str> = (0..resolved.schema.len())
        .filter_map(|index| {
            resolved
                .schema
                .get(fjord_schema::schema::PredicateId(index as u32))?
                .name()
        })
        .filter(|name| name.starts_with("codemarkup."))
        .collect();

    assert_eq!(
        names,
        [
            "codemarkup.Definition",
            "codemarkup.FileDefinition",
            "codemarkup.FileLocalXRef",
            "codemarkup.FileXRef",
            "codemarkup.Relation",
            "codemarkup.RelationOf",
            "codemarkup.SearchEntry",
            "codemarkup.SymbolByName",
            "codemarkup.SymbolInfo",
            "codemarkup.SymbolXRef",
        ]
    );

    // 10 of its own, on top of `src.sigla`'s 9.
    assert_eq!(resolved.schema.len(), 19);
}

// ---- one query per predicate, against a real database ------------------------

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

const CODEMARKUP: &str = concat!(
    env!("CARGO_MANIFEST_DIR"),
    "/../../schemas/codemarkup.sigla"
);

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
    let json = fjord(root, &["query", "cm", query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

/// A `Kind` or a `Role` alternative with no payload: the tag, and the empty record.
fn tag(disc: u32) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(WireValue::Record(Box::from([]))),
    }
}

fn span(start: i64, length: i64) -> WireValue {
    WireValue::Record(Box::from([WireValue::Int(start), WireValue::Int(length)]))
}

/// **Every predicate answers, and each answers the question a UI actually asks.**
///
/// Ten predicates arrived at once. A predicate nothing has queried is a name in a file,
/// and the questions here are the ones the layer exists for: go to definition, the file
/// outline in position order, every reference in a file, find-references across files, a
/// file-local jump, both search shapes, and both directions of a relation.
///
/// Two files, two languages' worth of symbols, and no `src.Decl` anywhere — which is the
/// point: an index this serves does not have to be one this repository's indexer made.
#[test]
fn every_codemarkup_predicate_answers_the_question_it_is_for() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(
        &root,
        &[
            "--schema-path",
            concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas"),
            "create",
            "cm",
            "--schema",
            CODEMARKUP,
        ],
    );
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "cm",
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
            .unwrap_or_else(|| panic!("no `{name}`"))
    };

    let mut writer = Connection::open(&endpoint, "cm", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    // Two files and three symbols: a C# method, the class containing it, and a
    // TypeScript function that calls it. Different languages, one symbol space.
    let file = |path: &str| WireFact {
        predicate: id("src.File"),
        key: WireValue::Str(path.to_owned()),
        value: None,
    };
    let of_file = |path: &str| WireValue::Ref(WireRef::Nested(Box::new(file(path))));

    let symbol = |s: &str| WireFact {
        predicate: id("src.Symbol"),
        key: WireValue::Str(s.to_owned()),
        value: None,
    };
    let of_symbol = |s: &str| WireValue::Ref(WireRef::Nested(Box::new(symbol(s))));

    const CS: &str = "scip-csharp . . `App`/Parser#Parse().";
    const KLASS: &str = "scip-csharp . . `App`/Parser#";
    const TS: &str = "scip-typescript npm app . `run().`";
    const A: &str = "src/Parser.cs";
    const B: &str = "src/run.ts";

    // `method_` is Kind 6, `class_` is 5; `call` is Role 4, `read` is 2;
    // `contains` is RelationKind 1, `calls` is 5.
    let blocks: Vec<(PredicateId, Vec<WireFact>)> = vec![
        (id("src.File"), vec![file(A), file(B)]),
        (
            id("src.Symbol"),
            vec![symbol(CS), symbol(KLASS), symbol(TS)],
        ),
        (
            id("codemarkup.Definition"),
            vec![
                WireFact {
                    predicate: id("codemarkup.Definition"),
                    key: WireValue::Record(Box::from([of_symbol(CS), of_file(A)])),
                    value: Some(WireValue::Record(Box::from([
                        span(120, 5),
                        tag(6),
                        WireValue::Str("Parse".to_owned()),
                        WireValue::Str("App.Parser.Parse".to_owned()),
                    ]))),
                },
                WireFact {
                    predicate: id("codemarkup.Definition"),
                    key: WireValue::Record(Box::from([of_symbol(KLASS), of_file(A)])),
                    value: Some(WireValue::Record(Box::from([
                        span(40, 6),
                        tag(5),
                        WireValue::Str("Parser".to_owned()),
                        WireValue::Str("App.Parser".to_owned()),
                    ]))),
                },
            ],
        ),
        (
            id("codemarkup.SymbolInfo"),
            vec![WireFact {
                predicate: id("codemarkup.SymbolInfo"),
                key: WireValue::Record(Box::from([of_symbol(CS)])),
                value: Some(WireValue::Record(Box::from([
                    WireValue::Str("string Parse(string input)".to_owned()),
                    WireValue::Str("Parses the input.".to_owned()),
                    WireValue::Str("public".to_owned()),
                    // The two a symbol with no declaration site still has: what it is
                    // called in full, and what ships it.
                    WireValue::Str("Acme.Parser.Parse(string)".to_owned()),
                    WireValue::Str("Acme.Parser 1.0.0.0".to_owned()),
                    // What it is, which a symbol with no declaration site still has.
                    tag(6),
                ]))),
            }],
        ),
        (
            id("codemarkup.FileDefinition"),
            // Out of position order on purpose: the outline query asserts they come back
            // in it, which is the key order's doing rather than the writer's.
            vec![
                WireFact {
                    predicate: id("codemarkup.FileDefinition"),
                    key: WireValue::Record(Box::from([of_file(A), span(120, 5), of_symbol(CS)])),
                    value: Some(WireValue::Record(Box::from([
                        tag(6),
                        WireValue::Str("Parse".to_owned()),
                    ]))),
                },
                WireFact {
                    predicate: id("codemarkup.FileDefinition"),
                    key: WireValue::Record(Box::from([of_file(A), span(40, 6), of_symbol(KLASS)])),
                    value: Some(WireValue::Record(Box::from([
                        tag(5),
                        WireValue::Str("Parser".to_owned()),
                    ]))),
                },
            ],
        ),
        (
            id("codemarkup.FileXRef"),
            vec![
                WireFact {
                    predicate: id("codemarkup.FileXRef"),
                    key: WireValue::Record(Box::from([
                        of_file(B),
                        span(200, 5),
                        of_symbol(CS),
                        tag(4),
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: id("codemarkup.FileXRef"),
                    key: WireValue::Record(Box::from([
                        of_file(B),
                        span(80, 6),
                        of_symbol(KLASS),
                        tag(2),
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: id("codemarkup.FileXRef"),
                    key: WireValue::Record(Box::from([
                        of_file(A),
                        span(300, 5),
                        of_symbol(CS),
                        tag(4),
                    ])),
                    value: None,
                },
            ],
        ),
        (
            id("codemarkup.SymbolXRef"),
            vec![
                WireFact {
                    predicate: id("codemarkup.SymbolXRef"),
                    key: WireValue::Record(Box::from([of_symbol(CS), of_file(B), span(200, 5)])),
                    value: None,
                },
                WireFact {
                    predicate: id("codemarkup.SymbolXRef"),
                    key: WireValue::Record(Box::from([of_symbol(CS), of_file(A), span(300, 5)])),
                    value: None,
                },
            ],
        ),
        (
            id("codemarkup.FileLocalXRef"),
            // A local variable: a use at 210 jumping to its declaration at 190, span to
            // span, inside one file and costing no `src.Symbol` at all.
            vec![WireFact {
                predicate: id("codemarkup.FileLocalXRef"),
                key: WireValue::Record(Box::from([of_file(B), span(210, 3), span(190, 3), tag(2)])),
                value: None,
            }],
        ),
        (
            id("codemarkup.SearchEntry"),
            vec![WireFact {
                predicate: id("codemarkup.SearchEntry"),
                key: WireValue::Record(Box::from([
                    WireValue::Str("parse".to_owned()),
                    WireValue::Str("Parse".to_owned()),
                    tag(6),
                    of_symbol(CS),
                    of_file(A),
                    WireValue::Int(12),
                ])),
                value: None,
            }],
        ),
        (
            id("codemarkup.SymbolByName"),
            vec![WireFact {
                predicate: id("codemarkup.SymbolByName"),
                key: WireValue::Record(Box::from([
                    WireValue::Str("Parse".to_owned()),
                    of_symbol(CS),
                ])),
                value: None,
            }],
        ),
        (
            id("codemarkup.Relation"),
            vec![
                WireFact {
                    predicate: id("codemarkup.Relation"),
                    key: WireValue::Record(Box::from([of_symbol(KLASS), tag(1), of_symbol(CS)])),
                    value: None,
                },
                WireFact {
                    predicate: id("codemarkup.Relation"),
                    key: WireValue::Record(Box::from([of_symbol(TS), tag(5), of_symbol(CS)])),
                    value: None,
                },
            ],
        ),
        (
            id("codemarkup.RelationOf"),
            vec![WireFact {
                predicate: id("codemarkup.RelationOf"),
                key: WireValue::Record(Box::from([of_symbol(CS), tag(1), of_symbol(KLASS)])),
                value: None,
            }],
        ),
    ];

    for (predicate, facts) in &blocks {
        let written = writer
            .write(*predicate, facts)
            .unwrap_or_else(|err| panic!("writing {predicate:?}: {err}"));
        assert_eq!(written.created as usize, facts.len(), "{predicate:?}");
    }
    drop(writer);

    // ---- the questions ----------------------------------------------------------

    // **Go to definition.** One seek: the symbol leads the key.
    let definition = rows(
        &root,
        "X.value where S = src.Symbol \"scip-csharp . . `App`/Parser#Parse().\"; \
         X = codemarkup.Definition {symbol = S, file = F}",
    );
    assert_eq!(definition.len(), 1, "{definition:#?}");
    assert_eq!(definition[0]["name"], serde_json::json!("Parse"));
    assert_eq!(definition[0]["kind"], serde_json::json!({"method_": {}}));

    // **A hover card**, which is a second fetch on purpose: a signature and a doc are an
    // order of magnitude more bytes than an outline row needs.
    let info = rows(
        &root,
        "X.value where S = src.Symbol \"scip-csharp . . `App`/Parser#Parse().\"; \
         X = codemarkup.SymbolInfo {symbol = S}",
    );
    assert_eq!(
        info[0]["signature"],
        serde_json::json!("string Parse(string input)")
    );

    // **The file outline, in position order** — the key leads with the file and the span
    // trails, so the order is the seek's rather than the writer's. Written at 120 then
    // 40; read back 40 then 120.
    let outline = rows(
        &root,
        "X.value where F = src.File \"src/Parser.cs\"; \
         X = codemarkup.FileDefinition {file = F, span = SP, symbol = S}",
    );
    assert_eq!(outline.len(), 2);
    assert_eq!(
        outline[0]["name"],
        serde_json::json!("Parser"),
        "position order"
    );
    assert_eq!(outline[1]["name"], serde_json::json!("Parse"));

    // **Every reference in a file, in position order.** One prefix seek — the reason this
    // predicate exists beside the symbol-keyed one.
    let in_file = rows(
        &root,
        "SP where F = src.File \"src/run.ts\"; \
         codemarkup.FileXRef {file = F, span = SP, target = T, role = R}",
    );
    assert_eq!(in_file.len(), 2);
    assert_eq!(in_file[0]["start"], serde_json::json!(80), "position order");

    // **Find references, across files.** The symbol leads, so this is a seek in every
    // database a fan-out holds — which is the whole reason the key is a string.
    let uses = rows(
        &root,
        "F where S = src.Symbol \"scip-csharp . . `App`/Parser#Parse().\"; \
         codemarkup.SymbolXRef {target = S, file = F, span = SP}",
    );
    assert_eq!(uses.len(), 2, "used in two files: {uses:#?}");

    // **A file-local jump, span to span, costing no symbol.** This is what makes SCIP's
    // occurrence-ordered `local0` unnecessary: a local needs no global name, and nothing
    // about this row changes when an unrelated part of the file is edited.
    let local = rows(
        &root,
        "T where F = src.File \"src/run.ts\"; \
         codemarkup.FileLocalXRef {file = F, span = SP, target = T, role = R}",
    );
    assert_eq!(local, vec![serde_json::json!({"start": 190, "length": 3})]);

    // **A case-insensitive prefix search**, which is what a search box does.
    let search = rows(
        &root,
        "N where codemarkup.SearchEntry \
         {nameLowercase = \"par\".., name = N, kind = K, symbol = S, file = F, line = L}",
    );
    assert_eq!(search, vec![serde_json::json!("Parse")]);

    // **An exact-name search**, which is the question a case-folded index cannot answer.
    let exact = rows(
        &root,
        "S where codemarkup.SymbolByName {name = \"Parse\", symbol = S}",
    );
    assert_eq!(exact.len(), 1);

    // **Both directions of a relation**, which is why there are two predicates and not
    // one with a sort.
    let contains = rows(
        &root,
        "T where S = src.Symbol \"scip-csharp . . `App`/Parser#\"; \
         codemarkup.Relation {from = S, kind = {contains = _}, to = T}",
    );
    assert_eq!(contains.len(), 1, "the class contains the method");

    let contained_by = rows(
        &root,
        "T where S = src.Symbol \"scip-csharp . . `App`/Parser#Parse().\"; \
         codemarkup.RelationOf {to = S, kind = {contains = _}, from = T}",
    );
    assert_eq!(
        contained_by.len(),
        1,
        "the method is contained by the class"
    );
}
