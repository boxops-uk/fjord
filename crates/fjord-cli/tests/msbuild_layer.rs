//! **`msbuild.Project` is identified by its project file, and nothing else is in it.**
//!
//! `csharp.Project`'s key was all seven fields — the path *and* the target framework, SDK,
//! output type, assembly name and root namespace MSBuild resolved. Re-evaluating one
//! `.csproj` under a different SDK therefore minted a **different project**, so a re-index
//! silently doubled the graph and every reference edge pointed at whichever variant the
//! walk reached first. It is the argument the overhaul plan makes about `src.Decl`: an
//! identity must not carry an evaluation detail.
//!
//! So the identity is keyed `{ file }` and the evaluated attributes are a predicate of
//! their own — `msbuild.ProjectEvaluated`, keyed on the project. Two evaluations reach
//! **one** key with two different value sets, and the second is refused. That is
//! `ops-I4`'s rule rather than a choice this schema gets to make: *"a conflict rule that
//! picks a winner … is the one thing `ops-I4` really forbids"*, and `ops-I5`'s dedup
//! covers only the identical case.
//!
//! **The attributes are a predicate of their own because the conflict had a second cause**
//! (issue #82). Every observer of a project can read its path; only the run that
//! design-time built it can read its SDK. While both lived in one fact, a project a run
//! merely discovered by glob wrote `nothing` for what it could not see — which is a
//! *claim* that this project has no SDK, and conflicts with the authority's. Split, an
//! uninformed observer writes the identity and abstains from the rest, and the identity is
//! a fact every observer can agree on.

use std::sync::Arc;

use fjord_client::{ClientError, Connection, Endpoint, Mode};
use fjord_wire::{WireFact, WireRef, WireValue};

use std::{
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    thread,
    time::{Duration, Instant},
};

const MSBUILD: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/msbuild.sigla");
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
    let json = fjord(root, &["query", "b", query, "--format", "json"]);
    serde_json::from_str(&json).unwrap_or_else(|err| panic!("{query}: {json} is not JSON: {err}"))
}

/// `{ nothing = 0 | just : string = 1 }`.
fn just(text: &str) -> WireValue {
    WireValue::Union {
        disc: 1,
        value: Box::new(WireValue::Str(text.to_owned())),
    }
}

fn nothing() -> WireValue {
    WireValue::Union {
        disc: 0,
        value: Box::new(WireValue::Record(Box::from([]))),
    }
}

#[test]
fn one_csproj_is_one_project_however_many_times_it_is_evaluated() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(
        &root,
        &["--schema-path", SCHEMAS, "create", "b", "--schema", MSBUILD],
    );
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "b",
        Arc::new(fjord_cli::sample_schema::schema()),
        Mode::ReadOnly,
        false,
    )
    .expect("a probe connection");
    let served = Arc::new(probe.served_schema().expect("the served schema"));
    drop(probe);

    let file_id = served.find_position("src.File").expect("src.File").0;
    let project_id = served.find_position("msbuild.Project").expect("Project").0;
    let evaluated_id = served
        .find_position("msbuild.ProjectEvaluated")
        .expect("ProjectEvaluated")
        .0;

    let csproj = || WireFact {
        predicate: file_id,
        key: WireValue::Str("src/App/App.csproj".to_owned()),
        value: None,
    };

    let project = || WireFact {
        predicate: project_id,
        key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
            csproj(),
        )))])),
        value: None,
    };

    let evaluated = |framework: &str| WireFact {
        predicate: evaluated_id,
        key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
            project(),
        )))])),
        value: Some(WireValue::Record(Box::from([
            nothing(),
            just(framework),
            just("Microsoft.NET.Sdk"),
            just("Library"),
            just("App"),
            just("App"),
        ]))),
    };

    let mut writer = Connection::open(&endpoint, "b", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    // **The identity itself is writable by every observer, which is the point of the
    // split.** A project a run discovered by glob and the same project design-time built
    // agree on the path and can disagree about everything else, so the path is the only
    // thing in this fact — and the second observer writing it is `ops-I5` dedup rather
    // than the conflict issue #82 reported.
    writer
        .write(project_id, &[project()])
        .expect("one observer writes the identity");
    let seen_again = writer
        .write(project_id, &[project()])
        .expect("a second observer of the same project is not a conflict");
    assert_eq!(seen_again.created, 0, "{seen_again:?}");

    writer
        .write(evaluated_id, &[evaluated("net9.0")])
        .expect("the first evaluation is written");

    // **Writing the identical fact again is free** — `ops-I5`'s dedup, and the reason a
    // producer needs no book of what it has already sent.
    let again = writer
        .write(evaluated_id, &[evaluated("net9.0")])
        .expect("an identical fact is not a conflict");
    assert_eq!(again.created, 0, "{again:?}");

    // **A second evaluation of the same `.csproj` is refused.** Same key, different value
    // — and `ops-I4` forbids picking a winner, so this is a rejection rather than a
    // last-write-wins that would make the database depend on walk order.
    let conflict = writer
        .write(evaluated_id, &[evaluated("netstandard2.0")])
        .expect_err("two evaluations of one project must conflict");
    assert!(
        matches!(conflict, ClientError::Server { .. }),
        "expected the server to refuse it: {conflict:?}"
    );

    drop(writer);

    // **And it minted one project, not two** — the defect the new identity closes. With
    // the old seven-field key both evaluations would be here, and every reference edge
    // would point at whichever the walk reached first.
    let projects = rows(&root, "P where P = msbuild.Project {file = F}");
    assert_eq!(
        projects.len(),
        1,
        "one `.csproj` is one project, and with the old seven-field key both evaluations \
         would be here: {projects:#?}"
    );

    // And the evaluation that is there is the *first* one, unchanged by the refusal.
    let value = rows(
        &root,
        "P.value where P = msbuild.ProjectEvaluated {project = Q}",
    );
    assert_eq!(
        value[0]["targetFramework"],
        serde_json::json!({"just": "net9.0"})
    );
}

/// **The order does not decide the winner, because there is no winner.**
///
/// `ops-I4` is order-independent rejection: whichever evaluation arrives first, the second
/// is refused and the database holds the first. Asserted both ways round, because a
/// conflict rule that happened to be stable only in one order would satisfy a single-order
/// test and still be wrong.
#[test]
fn which_evaluation_arrives_first_does_not_change_that_the_second_is_refused() {
    for (first, second) in [("net9.0", "netstandard2.0"), ("netstandard2.0", "net9.0")] {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let root: PathBuf = dir.path().join("store");
        std::fs::create_dir_all(&root).expect("a store root");

        fjord(
            &root,
            &["--schema-path", SCHEMAS, "create", "b", "--schema", MSBUILD],
        );
        let _serving = serve(&root);

        let endpoint = Endpoint::Unix(root.join("fjord.sock"));
        let mut probe = Connection::open(
            &endpoint,
            "b",
            Arc::new(fjord_cli::sample_schema::schema()),
            Mode::ReadOnly,
            false,
        )
        .expect("a probe connection");
        let served = Arc::new(probe.served_schema().expect("the served schema"));
        drop(probe);

        let file_id = served.find_position("src.File").expect("src.File").0;
        let project_id = served.find_position("msbuild.Project").expect("Project").0;
        let evaluated_id = served
            .find_position("msbuild.ProjectEvaluated")
            .expect("ProjectEvaluated")
            .0;

        let evaluated = |framework: &str| WireFact {
            predicate: evaluated_id,
            key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
                WireFact {
                    predicate: project_id,
                    key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(
                        WireFact {
                            predicate: file_id,
                            key: WireValue::Str("src/App/App.csproj".to_owned()),
                            value: None,
                        },
                    )))])),
                    value: None,
                },
            )))])),
            value: Some(WireValue::Record(Box::from([
                nothing(),
                just(framework),
                nothing(),
                nothing(),
                nothing(),
                nothing(),
            ]))),
        };

        let mut writer =
            Connection::open(&endpoint, "b", Arc::clone(&served), Mode::ReadWrite, true)
                .expect("a write connection");

        writer
            .write(evaluated_id, &[evaluated(first)])
            .unwrap_or_else(|err| panic!("{first} first: {err}"));

        writer
            .write(evaluated_id, &[evaluated(second)])
            .expect_err(&format!("{second} after {first} must be refused"));
    }
}
