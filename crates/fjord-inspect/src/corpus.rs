//! **A real code index in the page** — an image loaded once, queried many times.
//!
//! [`crate::demo`] is a database *written* in Rust: twelve facts chosen to show
//! every construct the type model holds. This is the other kind — a database
//! **indexed** from real source by a real compiler, exported as a store image by
//! `fjord export`, and fetched as a static asset. The engine cannot tell them
//! apart, which is the point: the same executor, the same plan, the same profile.
//!
//! **Loaded rather than built, because a browser cannot intern a fact.**
//! `fjord-ingest` reaches the fjall backend by name, so the write funnel is not in
//! a WebAssembly build at all. An image carries rows with the ids they were already
//! given, and [`fjord_store_mem::dump::read`] puts them back — no interning, no
//! backend, no lifecycle.
//!
//! **The fingerprint is checked here, and refusing is the whole point.** An image
//! is bytes keyed against one schema; read against another, every row decodes as
//! whatever type sits at that position — silently, and wrongly. That is the failure
//! a client's handshake exists to catch, and this is the same check in the same
//! spirit: the number travels with the image, the page states the schema, and a
//! mismatch is a refusal that names both numbers rather than a page of nonsense.
//!
//! **State, deliberately, and thread-local.** Everything else in this crate is a
//! function of its arguments. A corpus is not: it is megabytes that must not be
//! re-read per keystroke, so it is held. A WebAssembly module is single-threaded,
//! and a thread local is the smallest thing that survives across calls without
//! making every caller carry a handle it has nowhere to put.

use std::cell::RefCell;

use fjord_schema::{fingerprint, schema::Schema};
use fjord_store_mem::{MemStore, dump};
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

/// Load `image` as the page's corpus, keyed against `schema_source`.
///
/// Replaces whatever was loaded before. A failure leaves **nothing** loaded rather
/// than the previous corpus: a page that fetched a new asset and got a refusal
/// should not go on answering from the old one as though it had succeeded.
#[must_use]
pub fn load(image: &[u8], schema_source: &str) -> Loaded {
    LOADED.with_borrow_mut(|slot| *slot = None);

    let (schema, diagnostics) = compile_schema(schema_source);

    let Some(schema) = schema else {
        return refused("the schema this page states does not compile", diagnostics);
    };

    let read = match dump::read(image) {
        Ok(image) => image,
        Err(problem) => return refused(&problem.to_string(), diagnostics),
    };

    let stated = fingerprint::of(&schema);

    if read.fingerprint != stated {
        return refused(
            &format!(
                "this image was written against schema {:#018x} and the page states \
                 {stated:#018x} — every row would decode against the wrong predicate",
                read.fingerprint
            ),
            diagnostics,
        );
    }

    // Counted once, here, rather than by scanning on every question about it.
    let rows = image_rows(&read.store, &schema);

    LOADED.with_borrow_mut(|slot| {
        *slot = Some(Corpus {
            schema,
            store: read.store,
            rows,
            fingerprint: stated,
        });
    });

    Loaded {
        ok: true,
        rows,
        fingerprint: Some(format!("{stated:#018x}")),
        problem: None,
        diagnostics,
    }
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

/// The same, for [`load`] and [`loaded`].
#[must_use]
pub fn load_json(image: &[u8], schema_source: &str) -> String {
    serde_json::to_string(&load(image, schema_source)).expect("a load view serialises")
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

/// How many rows the store holds, counted by walking it once at load.
fn image_rows(store: &MemStore, schema: &Schema) -> usize {
    dump::rows_of(store, u32::try_from(schema.len()).unwrap_or(u32::MAX))
        .map(|rows| rows.len())
        .unwrap_or_default()
}

#[cfg(test)]
mod tests {
    use fjord_store_mem::dump::OwnedRow;

    use super::*;

    /// **Tests hold their own corpus.** The slot is a thread local and the harness
    /// runs each test on its own thread, so a load here is invisible to every other
    /// test — which is what lets these run in parallel while sharing a name for the
    /// thing they load.
    fn image_of_the_demo_database() -> (Vec<u8>, &'static str) {
        let (schema, _) = compile_schema(crate::demo::SCHEMA);
        let schema = schema.expect("the demo schema compiles");
        let store = crate::demo::store(&schema).expect("the demo facts encode");

        let rows = dump::rows_of(&store, u32::try_from(schema.len()).unwrap_or(u32::MAX))
            .expect("the demo store scans");

        (
            dump::write(fingerprint::of(&schema), rows.iter().map(OwnedRow::as_row)),
            crate::demo::SCHEMA,
        )
    }

    /// The load-and-query path end to end, against the answer the same query gives
    /// over the database this crate builds in Rust. **Equality with `rows` is the
    /// claim**: an image is not a different database, it is the same one moved.
    #[test]
    fn an_image_answers_what_the_database_it_was_written_from_answers() {
        let (image, schema_source) = image_of_the_demo_database();

        let loaded = load(&image, schema_source);
        assert!(loaded.ok, "{:?}", loaded.problem);
        assert!(loaded.rows > 0, "the demo database is not empty");

        const QUERY: &str = "P where F = code.File {path = P}";

        assert_eq!(
            rows(QUERY).rows,
            crate::rows::rows(schema_source, QUERY).rows,
            "the image answered differently from the database it was written from"
        );
    }

    /// The check the whole module exists for. An image keyed against one schema and
    /// read against another decodes every row as whatever type sits at that
    /// position — so the refusal names both numbers rather than answering nonsense.
    #[test]
    fn an_image_written_against_another_schema_is_refused_naming_both() {
        let (image, _) = image_of_the_demo_database();

        let loaded = load(&image, "schema other { predicate Thing : string }");

        assert!(!loaded.ok);
        let problem = loaded.problem.expect("a refusal says why");
        assert!(
            problem.contains("0x") && problem.contains("decode"),
            "the refusal should name the two fingerprints: {problem}"
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
        let (image, schema_source) = image_of_the_demo_database();

        assert!(load(&image, schema_source).ok);
        assert!(super::loaded().ok);

        let refused = load(b"not an image at all", schema_source);

        assert!(!refused.ok);
        assert_eq!(refused.rows, 0);
        assert!(
            !super::loaded().ok,
            "the previous corpus survived a failed load"
        );
    }
}
