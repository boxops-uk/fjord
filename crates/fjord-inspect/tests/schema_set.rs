//! **A browser can resolve a schema that spans files.**
//!
//! `wasm/src/lib.rs` used to list schema `import` as something a browser cannot do,
//! because resolution read files. It does not any more, and a caveat deleted with
//! nothing to replace it is a claim: this is the test that makes the deletion mean
//! something.
//!
//! An integration test because `schema_set_json` is the shape `wasm/` exports — a string
//! of JSON in, a string of JSON out — and that boundary is what a page actually calls.

use fjord_inspect::{schema_set, schema_set_json};

const ENTRY: &str = "schema app { import base\n predicate Use : { of : base.Thing } }";
const BASE: &str = "schema base { predicate Thing : string }";

/// The set resolves, and the reference **crosses the file boundary** — which is what
/// would fail if each source were lowered on its own.
#[test]
fn a_multi_file_schema_resolves_with_no_filesystem() {
    let view = schema_set(&[("app.sigla", ENTRY), ("base.sigla", BASE)]);

    assert!(view.ok, "{:?}", view.diagnostics);
    assert_eq!(
        view.predicates
            .iter()
            .map(|predicate| predicate.name.as_str())
            .collect::<Vec<_>>(),
        ["app.Use", "base.Thing"]
    );

    // The type is printed by the schema's own printer, so the cross-file reference
    // reads as the qualified name it resolved to rather than as a position.
    assert!(
        view.predicates[0].ty.contains("base.Thing"),
        "{}",
        view.predicates[0].ty
    );
}

/// The entry alone is **refused**, rather than quietly answering with half a schema.
///
/// This is the failure the old single-source reader had: it lowered one block, so a
/// schema with an `import` came back missing everything it imported and every reference
/// into it unresolved.
#[test]
fn an_unanswered_import_is_refused_rather_than_dropped() {
    let view = schema_set(&[("app.sigla", ENTRY)]);

    assert!(!view.ok);
    assert!(
        view.diagnostics
            .iter()
            .any(|d| d.message.contains("declares `base`")),
        "{:?}",
        view.diagnostics
    );
}

/// The JSON boundary `wasm/` exports, driven as a page drives it.
#[test]
fn the_json_boundary_takes_an_array_of_pairs() {
    let sources = serde_json::to_string(&[("app.sigla", ENTRY), ("base.sigla", BASE)])
        .expect("the pairs serialise");

    let view: serde_json::Value =
        serde_json::from_str(&schema_set_json(&sources)).expect("the view is JSON");

    assert_eq!(view["ok"], serde_json::json!(true), "{view}");
    assert_eq!(
        view["predicates"].as_array().map(Vec::len),
        Some(2),
        "{view}"
    );

    // **Malformed input is a diagnostic, not a panic.** A page hands this whatever a
    // user typed, and a `wasm_bindgen` export that panics takes the module with it.
    let refused: serde_json::Value =
        serde_json::from_str(&schema_set_json("not json at all")).expect("the view is JSON");
    assert_eq!(refused["ok"], serde_json::json!(false), "{refused}");
}
