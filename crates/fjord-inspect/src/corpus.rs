//! **A real code index in the page** — an export loaded once, queried many times.
//!
//! [`crate::demo`] is a database *written* in Rust: twelve facts chosen to show
//! every construct the type model holds. This is the other kind — a database
//! **indexed** from real source by a real compiler, written out as JSONL by
//! `fjord export`, and fetched as a static asset. The engine cannot tell them
//! apart, which is the point: the same executor, the same plan, the same profile.
//!
//! **Loaded rather than built, because a browser cannot intern a fact.**
//! `fjord-ingest` reaches the fjall backend by name, so the write funnel is not in
//! a WebAssembly build at all. A JSONL export needs no funnel: its references only
//! ever name earlier lines, so [`crate::jsonl::read`] counts a sequence per
//! predicate as it goes — no interning, no backend, no lifecycle.
//!
//! **Nothing in the file says which schema it was written against, and the risk that
//! leaves is a narrow one.** A predicate or a field the page's schema does not declare
//! is refused by name on the line that uses it, which is most of a mismatch. What gets
//! through is a field that kept its name and changed its *type*, or an alternative
//! renumbered under the same name — decoded as whatever the page now says, silently.
//! The fingerprint is reported so a reader can see which schema answered; it is not a
//! check, because there is no second number to check it against.
//!
//! **State, deliberately, and thread-local.** Everything else in this crate is a
//! function of its arguments. A corpus is not: it is megabytes that must not be
//! re-read per keystroke, so it is held. A WebAssembly module is single-threaded,
//! and a thread local is the smallest thing that survives across calls without
//! making every caller carry a handle it has nowhere to put.

use std::cell::RefCell;

use fjord_schema::{fingerprint, schema::Schema};
use fjord_store_mem::MemStore;
use serde::Serialize;

use crate::{rows::Rows, schema::compile as compile_schema, view::DiagnosticView};

thread_local! {
    static LOADED: RefCell<Option<Corpus>> = const { RefCell::new(None) };
}

/// What a page has loaded: the schema its queries compile against, and the rows.
struct Corpus {
    schema: Schema,
    store: MemStore,
    rows: usize,
    fingerprint: u64,
}

/// What a load answered — enough for a page to say what it is holding, or why it
/// is holding nothing.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Loaded {
    pub ok: bool,
    /// Rows now in the store, zero if the load failed.
    pub rows: usize,
    /// The schema fingerprint both sides agreed on, as `0x…`, when they did.
    pub fingerprint: Option<String>,
    /// Why not, when they did not. One sentence, meant to be shown.
    pub problem: Option<String>,
    /// Diagnostics from the schema source, which are the caller's to fix.
    pub diagnostics: Vec<DiagnosticView>,
}

/// Load `text` as the page's corpus — the **portable format**, keyed against
/// `schema_source`.
///
/// Replaces whatever was loaded before. A failure leaves **nothing** loaded rather than
/// the previous corpus: a page that fetched a new asset and got a refusal should not go
/// on answering from the old one as though it had succeeded.
///
/// **The ids are this load's own.** A line's `id` names a fact within the file, and the
/// sequence a fact gets here is the next one for its predicate — so the numbering differs
/// from the database the file came from. `ops-I4`'s content identity is a multiset hash
/// over each fact's logical form, so the loaded copy is the same database.
///
/// **A wrong schema is caught by name, not up front.** Nothing in the file identifies
/// the schema it was written against, so the refusal comes on the first line naming a
/// predicate or a field this one does not declare — late, and only for a mismatch that
/// changes a name.
#[must_use]
pub fn load_jsonl(text: &str, schema_source: &str) -> Loaded {
    LOADED.with_borrow_mut(|slot| *slot = None);

    let (schema, diagnostics) = compile_schema(schema_source);

    let Some(schema) = schema else {
        return refused("the schema this page states does not compile", diagnostics);
    };

    let read = match crate::jsonl::read(text, &schema) {
        Ok(read) => read,
        Err(problem) => return refused(&problem, diagnostics),
    };

    let fingerprint = fingerprint::of(&schema);
    let rows = read.facts;

    LOADED.with_borrow_mut(|slot| {
        *slot = Some(Corpus {
            schema,
            store: read.store,
            rows,
            fingerprint,
        });
    });

    Loaded {
        ok: true,
        rows,
        fingerprint: Some(format!("{fingerprint:#018x}")),
        problem: None,
        diagnostics,
    }
}

/// [`load_jsonl`], as the JSON a page reads.
#[must_use]
pub fn load_jsonl_json(text: &str, schema_source: &str) -> String {
    serde_json::to_string(&load_jsonl(text, schema_source)).unwrap_or_default()
}

/// What is loaded, without loading anything — for a page restoring its own state.
#[must_use]
pub fn loaded() -> Loaded {
    LOADED.with_borrow(|slot| match slot {
        Some(corpus) => Loaded {
            ok: true,
            rows: corpus.rows,
            fingerprint: Some(format!("{:#018x}", corpus.fingerprint)),
            problem: None,
            diagnostics: Vec::new(),
        },
        None => Loaded {
            ok: false,
            rows: 0,
            fingerprint: None,
            problem: Some("no corpus is loaded".to_owned()),
            diagnostics: Vec::new(),
        },
    })
}

/// Run `query` against the loaded corpus.
///
/// **The store is cloned per query**, which is two refcount bumps rather than a
/// copy: `Executor::new` takes its store by value, and `MemStore` holds both its
/// maps behind an `Arc` for exactly this call.
#[must_use]
pub fn rows(query: &str) -> Rows {
    LOADED.with_borrow(|slot| match slot {
        Some(corpus) => crate::rows::run_over(&corpus.schema, query, corpus.store.clone()),
        // **Said, not silently empty.** A page that asks before its asset has
        // arrived gets an answer that explains itself, because "no rows" and "no
        // corpus" look identical in a table and mean entirely different things.
        None => Rows {
            rows: Vec::new(),
            examined: Vec::new(),
            examined_total: 0,
            truncated: false,
            diagnostics: vec![DiagnosticView {
                code: None,
                message: "no corpus is loaded — fetch the image and load it first".to_owned(),
                labels: Vec::new(),
            }],
        },
    })
}

/// Run `query` over the loaded corpus and answer each row's **decoded value**.
///
/// The rows [`rows`] renders, before rendering: `Value` rather than
/// `serde_json::Value`, because [`crate::codeview`] destructures fields out of them
/// and going through JSON to do that would parse a document to reach numbers the
/// engine already had.
///
/// `Err` carries what a caller can show — a query that did not compile, or a fault
/// while running. There is no corpus-not-loaded arm: that is an `Err` too, and one
/// message covers it.
pub fn values(query: &str, cap: usize) -> Result<Vec<fjord_encoding::tuple::Value>, String> {
    LOADED.with_borrow(|slot| {
        let Some(corpus) = slot else {
            return Err("no corpus is loaded — fetch the image and load it first".to_owned());
        };

        crate::rows::values_over(&corpus.schema, query, corpus.store.clone(), cap)
    })
}

/// The same answer, already JSON.
#[must_use]
pub fn rows_json(query: &str) -> String {
    serde_json::to_string(&rows(query)).expect("a rows view serialises")
}

/// The same, for [`loaded`].
#[must_use]
pub fn loaded_json() -> String {
    serde_json::to_string(&loaded()).expect("a load view serialises")
}

fn refused(problem: &str, diagnostics: Vec<DiagnosticView>) -> Loaded {
    Loaded {
        ok: false,
        rows: 0,
        fingerprint: None,
        problem: Some(problem.to_owned()),
        diagnostics,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// **Tests hold their own corpus.** The slot is a thread local and the harness
    /// runs each test on its own thread, so a load here is invisible to every other
    /// test — which is what lets these run in parallel while sharing a name for the
    /// thing they load.
    ///
    /// Written out by hand rather than round-tripped through an exporter: the writer
    /// lives with the tool that walks a real database, and what this module owes is that
    /// it can *read* the format.
    const DUMP: &str = concat!(
        r#"{"id":1,"predicate":"code.File","fact":"src/main.rs"}"#,
        "\n",
        r#"{"id":2,"predicate":"code.File","fact":"src/lib.rs"}"#,
        "\n",
        r#"{"id":3,"predicate":"code.Decl","fact":{"file":1,"name":"main","line":1},"value":"fn"}"#,
        "\n",
        r#"{"id":4,"predicate":"code.Decl","fact":{"file":2,"name":"run","line":9},"value":"fn"}"#,
        "\n",
    );

    /// The load-and-query path end to end: what went in comes back, through a reference.
    #[test]
    fn a_dump_answers_the_facts_it_carries() {
        let loaded = load_jsonl(DUMP, crate::demo::SCHEMA);
        assert!(loaded.ok, "{:?}", loaded.problem);
        assert_eq!(loaded.rows, 4);

        // A whole-predicate scan.
        let files = rows("P where code.File P");
        assert_eq!(files.rows.len(), 2, "{files:?}");

        // And a join *through* a reference, which is the half a local id has to have
        // resolved correctly for: the decl's `file` must name the file's own row.
        let joined =
            rows("{p = P, n = N} where code.Decl {file = F, name = N, line = L}; F = code.File P");
        assert_eq!(joined.rows.len(), 2, "{joined:?}");
    }

    /// A dump read against a schema that does not declare its predicates is refused,
    /// naming the first line that does not fit.
    ///
    /// **This is what a store image's fingerprint used to do**, one line earlier and as
    /// two numbers. A dump carries no fingerprint, so the refusal comes from the first
    /// line naming a predicate the schema has not got — which says more, not less.
    #[test]
    fn a_dump_read_against_another_schema_is_refused_by_name() {
        let loaded = load_jsonl(DUMP, "schema other { predicate Thing : string }");

        assert!(!loaded.ok);
        let problem = loaded.problem.expect("a refusal says why");
        assert!(
            problem.contains("code.File") && problem.contains("line 1"),
            "the refusal should name the line and the predicate: {problem}"
        );
        assert!(!super::loaded().ok, "a refused load left a corpus loaded");
    }

    /// A page whose asset has not arrived asks anyway. "No rows" and "no corpus"
    /// render identically in a table and mean entirely different things.
    #[test]
    fn a_query_before_any_corpus_is_loaded_says_so() {
        let answered = rows("P where F = code.File {path = P}");

        assert!(answered.rows.is_empty());
        assert_eq!(answered.diagnostics.len(), 1);
        assert!(
            answered.diagnostics[0]
                .message
                .contains("no corpus is loaded"),
            "{:?}",
            answered.diagnostics[0].message
        );
    }

    /// A page that fetched a new asset and got a refusal must not go on answering
    /// from the old one as though the fetch had worked.
    #[test]
    fn a_refused_load_replaces_nothing_and_leaves_nothing() {
        assert!(load_jsonl(DUMP, crate::demo::SCHEMA).ok);
        assert!(super::loaded().ok);

        let refused = load_jsonl("not a dump at all", crate::demo::SCHEMA);

        assert!(!refused.ok);
        assert_eq!(refused.rows, 0);
        assert!(
            !super::loaded().ok,
            "the previous corpus survived a failed load"
        );
    }
}
