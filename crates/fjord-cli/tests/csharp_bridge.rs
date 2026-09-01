//! **The `csharp` bridges, which are W1's payoff.**
//!
//! `csharp.EntityXRef` targets a `Definition` — a seven-alternative union — and so does
//! `csharp.DefinitionLocation`. Joining them means one variable bound to a union by two
//! generators, and until `unify` grew a `Ty::Union` arm that was `reject/type-mismatch`:
//! the two sides never compared equal, so the error was built from the two types it had
//! just failed to compare, which are the same type printed twice.
//!
//! The workaround was one query per alternative — the eleven-query form — and it is
//! pinned here beside the shared-variable one, because `codemarkup` and every consumer
//! that has not migrated still uses it.
//!
//! The fixture is deliberately the *whole* chain a C# definition needs: a namespace, a
//! qualified name, a class, the named type wrapping it, the type wrapping that, and a
//! method. Six levels of nesting, interned bottom-up by the write path, and the point of
//! building it rather than a shallower stand-in is that a union in a key is only
//! joinable if the thing underneath it is inhabitable.

use std::sync::Arc;

use fjord_client::{Connection, Endpoint, Mode};
use fjord_wire::{WireFact, WireRef, WireValue};

use std::{
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    thread,
    time::{Duration, Instant},
};

const CSHARP: &str = concat!(env!("CARGO_MANIFEST_DIR"), "/../../schemas/csharp.sigla");
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

fn count(root: &Path, query: &str) -> u64 {
    fjord(root, &["query", "cs", query, "--format", "count"])
        .trim()
        .parse()
        .expect("a count")
}

/// A union alternative with no payload: the tag, and the empty record.
fn tag(disc: u32) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(WireValue::Record(Box::from([]))),
    }
}

/// A union alternative carrying a payload.
fn alt(disc: u32, value: WireValue) -> WireValue {
    WireValue::Union {
        disc,
        value: Box::new(value),
    }
}

#[test]
fn a_union_typed_variable_joins_the_two_csharp_bridges() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root: PathBuf = dir.path().join("store");
    std::fs::create_dir_all(&root).expect("a store root");

    fjord(
        &root,
        &["--schema-path", SCHEMAS, "create", "cs", "--schema", CSHARP],
    );
    let _serving = serve(&root);

    let endpoint = Endpoint::Unix(root.join("fjord.sock"));
    let mut probe = Connection::open(
        &endpoint,
        "cs",
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
    let nested = |fact: WireFact| WireValue::Ref(WireRef::Nested(Box::new(fact)));

    // ---- the chain, bottom-up ----------------------------------------------------

    let name = |text: &str| WireFact {
        predicate: id("csharp.Name"),
        key: WireValue::Str(text.to_owned()),
        value: None,
    };

    // `namespace App` — `nothing` is MaybeNamespace 0.
    let namespace = WireFact {
        predicate: id("csharp.Namespace"),
        key: WireValue::Record(Box::from([nested(name("App")), tag(0)])),
        value: None,
    };

    let full_name = WireFact {
        predicate: id("csharp.FullName"),
        key: WireValue::Record(Box::from([
            nested(name("Parser")),
            nested(namespace.clone()),
        ])),
        value: None,
    };

    // `public class Parser` — Accessibility 9 is `public`, src.Bool 0 is `false_`.
    let class = WireFact {
        predicate: id("csharp.Class"),
        key: WireValue::Record(Box::from([
            nested(full_name.clone()),
            tag(0), // baseType: nothing
            tag(0), // containingType: nothing
            tag(9), // public
            tag(0), // isAbstract: false
            tag(0), // isStatic: false
            tag(0), // isSealed: false
        ])),
        value: None,
    };

    // NamedType 0 is `class_`; AType 1 is `namedType`.
    let named_type = alt(0, nested(class.clone()));
    let a_type = alt(1, named_type.clone());

    let method = WireFact {
        predicate: id("csharp.Method"),
        key: WireValue::Record(Box::from([
            nested(name("Parse")),
            named_type.clone(),
            a_type.clone(),
            tag(0), // isStatic: false
            tag(9), // public
            WireValue::Str("M:App.Parser.Parse".to_owned()),
        ])),
        value: None,
    };

    // Definition 1 is `method`.
    let definition = alt(1, nested(method.clone()));

    let file = WireFact {
        predicate: id("src.File"),
        key: WireValue::Str("src/Parser.cs".to_owned()),
        value: None,
    };
    let span = |start: i64, length: i64| {
        WireValue::Record(Box::from([WireValue::Int(start), WireValue::Int(length)]))
    };
    let location = WireValue::Record(Box::from([nested(file.clone()), span(120, 5)]));

    let mut writer = Connection::open(&endpoint, "cs", Arc::clone(&served), Mode::ReadWrite, true)
        .expect("a write connection");

    writer
        .write(
            id("csharp.DefinitionLocation"),
            &[WireFact {
                predicate: id("csharp.DefinitionLocation"),
                key: WireValue::Record(Box::from([definition.clone(), location])),
                value: None,
            }],
        )
        .expect("the definition's location is written");

    // Two uses of it, in one file.
    writer
        .write(
            id("csharp.EntityXRef"),
            &[
                WireFact {
                    predicate: id("csharp.EntityXRef"),
                    key: WireValue::Record(Box::from([
                        nested(file.clone()),
                        span(300, 5),
                        definition.clone(),
                    ])),
                    value: None,
                },
                WireFact {
                    predicate: id("csharp.EntityXRef"),
                    key: WireValue::Record(Box::from([
                        nested(file.clone()),
                        span(410, 5),
                        definition.clone(),
                    ])),
                    value: None,
                },
            ],
        )
        .expect("the uses are written");
    drop(writer);

    // ---- the join W1 unblocked ---------------------------------------------------
    //
    // `D` is bound by the first generator to a `Definition` and compared by the second
    // against the same union. Before the `Ty::Union` arm this was `reject/type-mismatch`
    // and no plan at all.
    assert_eq!(
        count(
            &root,
            "U where csharp.EntityXRef {file = F, use = U, target = D}; \
             csharp.DefinitionLocation {definition = D, location = L}"
        ),
        2,
        "both uses resolve to the definition through a shared union-typed variable"
    );

    // **The narrowed form, pinned.** One query per alternative, which is what a consumer
    // writes without the arm — `codemarkup` and anything not yet migrated still does.
    // It must keep answering exactly what the shared-variable form does.
    assert_eq!(
        count(
            &root,
            "U where csharp.EntityXRef {file = F, use = U, target = {method = M}}; \
             csharp.DefinitionLocation {definition = {method = M}, location = L}"
        ),
        2,
        "the eleven-query workaround's building block still answers"
    );

    // And an alternative nothing was written under answers nothing, rather than
    // everything — which is what a tag read as a position would do.
    assert_eq!(
        count(
            &root,
            "U where csharp.EntityXRef {file = F, use = U, target = {field = X}}"
        ),
        0
    );

    // **Find-references, the direction C# never had a predicate for.** `target` leads
    // `EntityRef`, so this is a seek rather than a scan of every reference in the index.
    assert_eq!(
        count(
            &root,
            "F where csharp.DefinitionLocation {definition = D, location = L}; \
             csharp.EntityRef {target = D, file = F, use = U}"
        ),
        0,
        "nothing was written to EntityRef, and the join still plans"
    );
}
