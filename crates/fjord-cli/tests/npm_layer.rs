//! **A repository with a lockfile and no TypeScript is an `npm` index and nothing else.**
//!
//! That is the argument for splitting the package layer out of `typescript.sigla`, and it
//! is only an argument if a database created from `npm.sigla` alone actually creates,
//! ingests and answers — otherwise the split is tidiness. So this is that, end to end:
//! a project, its lockfile, two workspaces, an external package and the resolution that
//! ties a requirement to what it resolved to.
//!
//! The model is Yarn's vocabulary verbatim — locator, descriptor, resolution, workspace,
//! project — so the two agree by construction rather than by translation.

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

const NPM: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/npm.sigla");
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
    let json = fjord(root, &["query", "pkg", query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

#[test]
fn a_lockfile_and_no_typescript_is_a_whole_index() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(
        &root,
        &["--schema-path", SCHEMAS, "create", "pkg", "--schema", NPM],
    );
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "pkg",
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

    let file = |path: &str| WireFact {
        predicate: id("src.File"),
        key: WireValue::Str(path.to_owned()),
        value: None,
    };
    let of_file = |path: &str| WireValue::Ref(WireRef::Nested(Box::new(file(path))));

    let name = |n: &str| WireFact {
        predicate: id("npm.Name"),
        key: WireValue::Str(n.to_owned()),
        value: None,
    };
    let of_name = |n: &str| WireValue::Ref(WireRef::Nested(Box::new(name(n))));

    let locator = |n: &str, reference: &str| WireFact {
        predicate: id("npm.Locator"),
        key: WireValue::Record(Box::from([
            of_name(n),
            WireValue::Str(reference.to_owned()),
        ])),
        value: None,
    };
    let of_locator = |n: &str, r: &str| WireValue::Ref(WireRef::Nested(Box::new(locator(n, r))));

    let descriptor = |n: &str, range: &str| WireFact {
        predicate: id("npm.Descriptor"),
        key: WireValue::Record(Box::from([of_name(n), WireValue::Str(range.to_owned())])),
        value: None,
    };

    let project = WireFact {
        predicate: id("npm.Project"),
        key: WireValue::Record(Box::from([of_file(".")])),
        value: Some(WireValue::Record(Box::from([of_file("yarn.lock")]))),
    };
    let of_project = WireValue::Ref(WireRef::Nested(Box::new(project.clone())));

    // Two workspaces and one external dependency — react at a real version, and `app`
    // requiring it by range. Enough to ask every question the layer is for.
    let blocks: Vec<(PredicateId, Vec<WireFact>)> = vec![
        (
            id("src.File"),
            vec![file("."), file("yarn.lock"), file("packages/app")],
        ),
        (
            id("npm.Name"),
            vec![name("@internal/app"), name("@internal/lib"), name("react")],
        ),
        (
            id("npm.Locator"),
            vec![
                locator("@internal/app", "workspace:packages/app"),
                locator("@internal/lib", "workspace:packages/lib"),
                locator("react", "npm:18.2.0"),
            ],
        ),
        (id("npm.Descriptor"), vec![descriptor("react", "^18")]),
        (id("npm.Project"), vec![project.clone()]),
        (
            id("npm.Resolution"),
            vec![WireFact {
                predicate: id("npm.Resolution"),
                key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
                    descriptor("react", "^18"),
                )))])),
                value: Some(WireValue::Record(Box::from([of_locator(
                    "react",
                    "npm:18.2.0",
                )]))),
            }],
        ),
        (
            id("npm.Workspace"),
            vec![WireFact {
                predicate: id("npm.Workspace"),
                key: WireValue::Record(Box::from([
                    of_project.clone(),
                    of_locator("@internal/app", "workspace:packages/app"),
                ])),
                value: Some(WireValue::Record(Box::from([of_file("packages/app")]))),
            }],
        ),
        (
            id("npm.WorkspaceDependency"),
            vec![WireFact {
                predicate: id("npm.WorkspaceDependency"),
                key: WireValue::Record(Box::from([
                    of_locator("@internal/app", "workspace:packages/app"),
                    // `prod` is DependencyKind 0.
                    WireValue::Union {
                        disc: 0,
                        value: Box::new(WireValue::Record(Box::from([]))),
                    },
                    of_locator("react", "npm:18.2.0"),
                ])),
                value: None,
            }],
        ),
        (
            id("npm.WorkspaceDependent"),
            vec![WireFact {
                predicate: id("npm.WorkspaceDependent"),
                key: WireValue::Record(Box::from([
                    of_locator("react", "npm:18.2.0"),
                    of_locator("@internal/app", "workspace:packages/app"),
                ])),
                value: None,
            }],
        ),
    ];

    let mut writer = Connection::open(&endpoint, "pkg", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");
    for (predicate, facts) in &blocks {
        let written = writer
            .write(*predicate, facts)
            .unwrap_or_else(|err| panic!("writing {predicate:?}: {err}"));
        assert_eq!(written.created as usize, facts.len(), "{predicate:?}");
    }
    drop(writer);

    // **Every version of a package is a range**, because `name` leads the locator's key.
    let versions = rows(
        &root,
        "R where N = npm.Name \"react\"; npm.Locator {name = N, reference = R}",
    );
    assert_eq!(versions, vec![serde_json::json!("npm:18.2.0")]);

    // **What a requirement resolved to** — `yarn.lock`'s core mapping, one seek.
    let resolved = rows(
        &root,
        "X.value where N = npm.Name \"react\"; \
         D = npm.Descriptor {name = N, range = \"^18\"}; \
         X = npm.Resolution {descriptor = D}",
    );
    assert_eq!(resolved.len(), 1, "{resolved:#?}");

    // **This project's workspaces**, which is why `project` leads that key.
    let workspaces = rows(
        &root,
        "L where P = npm.Project {root = R}; npm.Workspace {project = P, locator = L}",
    );
    assert_eq!(workspaces.len(), 1);

    // **Both directions of the dependency graph**, which is why there are two predicates:
    // "what does this workspace need" and "who needs this", and a predicate leads with one.
    let needs = rows(
        &root,
        "D where N = npm.Name \"@internal/app\"; \
         L = npm.Locator {name = N, reference = \"workspace:packages/app\"}; \
         npm.WorkspaceDependency {dependent = L, kind = K, dependency = D}",
    );
    assert_eq!(needs.len(), 1);

    let needed_by = rows(
        &root,
        "D where N = npm.Name \"react\"; \
         L = npm.Locator {name = N, reference = \"npm:18.2.0\"}; \
         npm.WorkspaceDependent {dependency = L, dependent = D}",
    );
    assert_eq!(needed_by.len(), 1);

    // And the database really does hold no semantic layer — which is the claim. Asking
    // for one is a compile error against this schema, not an empty result.
    let out = Command::new(env!("CARGO_BIN_EXE_fjord"))
        .arg("--data-dir")
        .arg(&root)
        .args(["query", "pkg", "D where typescript.Decl D"])
        .output()
        .expect("the binary runs");
    assert!(!out.status.success());
    assert!(
        String::from_utf8_lossy(&out.stderr).contains("typescript.Decl"),
        "a partial index names what it does not have: {}",
        String::from_utf8_lossy(&out.stderr)
    );
}
