//! **The composite, and the two joins it exists to make expressible.**
//!
//! Nine files resolve into one schema of 136 predicates, a database is created from it,
//! and the two queries in `index.sigla`'s own header run against it and return rows.
//! Those two are the whole claim of the set:
//!
//! - every reference in one file resolved to where its target is defined, **for any
//!   language**, from one query path;
//! - a symbol, its declaration site, and the project that compiled the file it sits in —
//!   across three namespaces filled by three different producers.
//!
//! Neither is expressible across three separate databases, because a `FactId` does not
//! leave the database that issued it ([I11]). Both are expressible here with no new
//! mechanism, and "expressible" means rows rather than a plan.
//!
//! [I11]: ../../../website/content/invariants.md#i11

use std::sync::Arc;

use fjord_client::{Connection, Endpoint, Mode};
use fjord_wire::{WireFact, WireRef, WireValue};

use std::{
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    thread,
    time::{Duration, Instant},
};

const INDEX: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/index.sigla");
const SCHEMAS: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas");

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
    let json = fjord(root, &["query", "ix", query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

fn tag(disc: u32) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(WireValue::Record(Box::from([]))),
    }
}

/// **The set composes, and the composite's own two queries answer.**
///
/// The fixture is deliberately cross-namespace and cross-producer: a TypeScript file
/// referencing a C# symbol, a C# file where that symbol is defined, and an MSBuild
/// project that compiled it. Three producers' worth of facts in one database, joined by
/// `src.File` and `src.Symbol` — which is what the shared source layer buys.
#[test]
fn the_composite_answers_both_of_its_headline_joins() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    let created = fjord(
        &root,
        &["--schema-path", SCHEMAS, "create", "ix", "--schema", INDEX],
    );
    assert!(created.starts_with("created ix ("), "{created}");

    let _serving = serve(&root);
    let endpoint = Endpoint::Unix(root.join("fjord.sock"));

    let mut probe = Connection::open(
        &endpoint,
        "ix",
        Arc::new(fjord_cli::sample_schema::schema()),
        Mode::ReadOnly,
        false,
    )
    .expect("a probe connection");
    let served = Arc::new(probe.served_schema().expect("the served schema"));
    drop(probe);

    // 136 stored, plus the two virtual `fjord.db.*` predicates the server answers out of
    // what it knows — the handshake includes them, because the question it answers is
    // what may be *asked* rather than what the database holds.
    assert_eq!(
        served.len(),
        138,
        "nine files, one schema, plus two virtuals"
    );
    assert_eq!(
        (0..served.len())
            .filter(|index| !served.is_virtual(fjord_schema::schema::PredicateId(*index as u32)))
            .count(),
        136
    );

    let id = |name: &str| {
        served
            .find_position(name)
            .map(|(id, _)| id)
            .unwrap_or_else(|| panic!("no `{name}`"))
    };
    let nested = |fact: WireFact| WireValue::Ref(WireRef::Nested(Box::new(fact)));

    let file = |path: &str| WireFact {
        predicate: id("src.File"),
        key: WireValue::Str(path.to_owned()),
        value: None,
    };
    let symbol = |s: &str| WireFact {
        predicate: id("src.Symbol"),
        key: WireValue::Str(s.to_owned()),
        value: None,
    };
    let span = |start: i64, length: i64| {
        WireValue::Record(Box::from([WireValue::Int(start), WireValue::Int(length)]))
    };

    const TSX: &str = "packages/app/src/App.tsx";
    const CS: &str = "src/Domain/Order.cs";
    const CSPROJ: &str = "src/Domain/Domain.csproj";
    const SYMBOL: &str = "scip-dotnet . Company.Domain . Company/Domain/Order#Total().";

    let mut writer = Connection::open(&endpoint, "ix", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    // The definition, in the C# file.
    writer
        .write(
            id("codemarkup.Definition"),
            &[WireFact {
                predicate: id("codemarkup.Definition"),
                key: WireValue::Record(Box::from([nested(symbol(SYMBOL)), nested(file(CS))])),
                value: Some(WireValue::Record(Box::from([
                    span(240, 5),
                    tag(6), // Kind::method_
                    WireValue::Str("Total".to_owned()),
                    WireValue::Str("Company.Domain.Order.Total".to_owned()),
                ]))),
            }],
        )
        .expect("the definition is written");

    // Two references to it — one from the TypeScript file, one from the C# file. The
    // first is the cross-language case the whole set is for.
    writer
        .write(
            id("codemarkup.FileXRef"),
            &[
                WireFact {
                    predicate: id("codemarkup.FileXRef"),
                    key: WireValue::Record(Box::from([
                        nested(file(TSX)),
                        span(880, 5),
                        nested(symbol(SYMBOL)),
                        tag(4), // Role::call
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: id("codemarkup.FileXRef"),
                    key: WireValue::Record(Box::from([
                        nested(file(CS)),
                        span(600, 5),
                        nested(symbol(SYMBOL)),
                        tag(2), // Role::read
                    ])),
                    value: None,
                },
            ],
        )
        .expect("the references are written");

    // The project that compiled the C# file — a third producer's facts.
    let project = WireFact {
        predicate: id("msbuild.Project"),
        key: WireValue::Record(Box::from([nested(file(CSPROJ))])),
        value: Some(WireValue::Record(Box::from([
            tag(0),
            WireValue::Union {
                disc: 1,
                value: Box::new(WireValue::Str("net9.0".to_owned())),
            },
            tag(0),
            tag(0),
            tag(0),
            tag(0),
        ]))),
    };
    writer
        .write(id("msbuild.Project"), std::slice::from_ref(&project))
        .expect("the project is written");
    writer
        .write(
            id("msbuild.SourceFileToProject"),
            &[WireFact {
                predicate: id("msbuild.SourceFileToProject"),
                key: WireValue::Record(Box::from([nested(file(CS)), nested(project.clone())])),
                value: None,
            }],
        )
        .expect("the file→project edge is written");
    drop(writer);

    // ---- headline join 1 ---------------------------------------------------------
    //
    // Every reference in one file, resolved to where its target is defined — **for any
    // language**, from one query path. The per-language layers can each answer this
    // within themselves; what they cannot do is answer it the same way as each other.
    let resolved = rows(
        &root,
        "{use = SP, defFile = DF, def = D.value} where \
         F = src.File \"packages/app/src/App.tsx\"; \
         codemarkup.FileXRef {file = F, span = SP, target = S, role = R}; \
         D = codemarkup.Definition {symbol = S, file = DF}",
    );

    assert_eq!(resolved.len(), 1, "{resolved:#?}");
    assert_eq!(
        resolved[0]["use"],
        serde_json::json!({"start": 880, "length": 5})
    );
    assert_eq!(resolved[0]["def"]["name"], serde_json::json!("Total"));

    // A reference written in a TypeScript file resolved to a definition in a C# one, and
    // nothing in the query said which language either was. That is the claim.

    // ---- headline join 2 ---------------------------------------------------------
    //
    // A symbol, its declaration site, and the project that compiled the file it sits in —
    // across three namespaces filled by three different producers, joined on `src.File`.
    let across = rows(
        &root,
        "{def = D.value, proj = P.value} where \
         S = src.Symbol \"scip-dotnet . Company.Domain . Company/Domain/Order#Total().\"; \
         D = codemarkup.Definition {symbol = S, file = F}; \
         msbuild.SourceFileToProject {src = F, project = P}",
    );

    assert_eq!(across.len(), 1, "{across:#?}");
    assert_eq!(across[0]["def"]["name"], serde_json::json!("Total"));
    assert_eq!(
        across[0]["proj"]["targetFramework"],
        serde_json::json!({"just": "net9.0"})
    );
}

/// **`describe --schema` round-trips text `create --schema` would accept.**
///
/// A database is served from the canonical copy it embedded, not from the files it was
/// created out of — so the copy has to be a schema in its own right. Nine files in, one
/// block per namespace out, and creating a second database from that output must reach
/// the same fingerprint.
#[test]
fn the_embedded_copy_of_the_composite_is_itself_a_schema() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(
        &root,
        &["--schema-path", SCHEMAS, "create", "ix", "--schema", INDEX],
    );

    let described = fjord(&root, &["describe", "ix", "--schema"]);
    let round_tripped = dir.path().join("round-tripped.sigla");
    std::fs::write(&round_tripped, &described).expect("it writes");

    fjord(
        &root,
        &[
            "create",
            "again",
            "--schema",
            round_tripped.to_str().expect("a utf-8 path"),
        ],
    );

    let of = |name: &str| {
        fjord(&root, &["describe", name])
            .lines()
            .find_map(|line| {
                let rest = line.trim().strip_prefix("schema")?;
                Some(rest.trim().to_owned())
            })
            .unwrap_or_else(|| panic!("no fingerprint in `describe {name}`"))
    };

    assert_eq!(
        of("ix"),
        of("again"),
        "the embedded copy is not the schema it was made from"
    );
}
