//! What a schema declares, as data — and what it refuses when it does not.
//!
//! A schema is what the phases after parsing resolve names against, so a page
//! that wants to show typechecking has to hold one. It is *text* here, the same
//! text a `.sigla` file holds, because that is the only form a browser can have
//! one in.
//!
//! **A schema may span files, and a browser can resolve one.** [`schema`] takes a
//! single source, which is what a page editing one block wants;
//! [`schema_set`] takes the whole set, the entry first, and follows the imports
//! through it — `resolve_from` touches no filesystem, so there is nothing here a
//! browser cannot do.
//!
//! **A predicate's type is rendered by the schema's own printer**
//! ([`print::signature`](fjord_schema::syntax::print::signature)), not by
//! anything here. A second way of writing a type down is a second thing that can
//! disagree with the first, and this one would disagree in the place a reader is
//! least able to check: the field *order*, which is the key order, which is what
//! decides what a query can seek on.

use fjord_schema::{
    schema::{PredicateId, Schema},
    syntax::{diag::Diagnostic, lower, parse, print},
};
use serde::Serialize;

use crate::view::{DiagnosticView, view_of};

/// One predicate, as a reader meets it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct PredicateView {
    /// The id the database numbers it by, which the wire carries names instead of.
    pub id: u32,
    /// Qualified, as a query writes it: `src.File`.
    pub name: String,
    /// The declared type, printed as the schema language writes it:
    /// `{ module: src.Module, name: string, line: int } -> string`.
    pub ty: String,
}

/// A schema, or the reasons it is not one.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct SchemaView {
    /// Whether the text lowered to a schema. A schema that lowered *and*
    /// complained is still refused — every diagnostic this front end raises is an
    /// error, and half a schema is not the schema anybody wrote.
    pub ok: bool,
    pub predicates: Vec<PredicateView>,
    pub diagnostics: Vec<DiagnosticView>,
}

/// Read `source` as a schema and describe what it declares.
#[must_use]
pub fn schema(source: &str) -> SchemaView {
    let (schema, diagnostics) = compile(source);

    SchemaView {
        ok: schema.is_some(),
        predicates: schema.as_ref().map(predicates).unwrap_or_default(),
        diagnostics,
    }
}

/// The same view, already JSON.
#[must_use]
pub fn schema_json(source: &str) -> String {
    serde_json::to_string(&schema(source)).expect("a schema view serialises")
}

/// Read a **set** of sources as one schema, the entry first, following its imports
/// through the rest.
///
/// The multi-file form of [`schema`]. Diagnostics come back rendered rather than
/// structured, because resolution reports across sources and a span into one of them
/// means nothing without saying which — which is what the rendered form carries.
#[must_use]
pub fn schema_set(sources: &[(&str, &str)]) -> SchemaView {
    match fjord_schema::syntax::resolve::resolve_from(sources.iter().copied()) {
        Ok(resolved) => SchemaView {
            ok: true,
            predicates: predicates(&resolved.schema),
            diagnostics: vec![],
        },
        Err(rendered) => SchemaView {
            ok: false,
            predicates: vec![],
            diagnostics: vec![DiagnosticView::rendered(&rendered)],
        },
    }
}

/// The same view, already JSON, from a JSON array of `[name, text]` pairs.
///
/// A string in and a string out, as every export in `wasm/` is: a browser has no
/// filesystem to keep a set of schema files in, and an array of pairs is what it has
/// instead.
#[must_use]
pub fn schema_set_json(sources: &str) -> String {
    let parsed: Result<Vec<(String, String)>, _> = serde_json::from_str(sources);

    let view = match parsed {
        Ok(pairs) => {
            let borrowed: Vec<(&str, &str)> = pairs
                .iter()
                .map(|(name, text)| (name.as_str(), text.as_str()))
                .collect();
            schema_set(&borrowed)
        }
        Err(error) => SchemaView {
            ok: false,
            predicates: vec![],
            diagnostics: vec![DiagnosticView::rendered(&format!(
                "the schema set is not a JSON array of [name, text] pairs: {error}"
            ))],
        },
    };

    serde_json::to_string(&view).expect("a schema view serialises")
}

/// Parse and lower `source`, keeping the diagnostics **structured**.
///
/// Not [`fjord_schema::syntax::read`], which renders them to a string against a
/// `SimpleFile`: a page wants the spans so it can point at them, and rendering
/// is the one thing it can do for itself.
pub(crate) fn compile(source: &str) -> (Option<Schema>, Vec<DiagnosticView>) {
    let mut diagnostics: Vec<Diagnostic> = Vec::new();
    let lowered =
        parse::parse(source, &mut diagnostics).and_then(|cst| lower::lower(&cst, &mut diagnostics));

    let schema = match lowered {
        Some(lowered) if diagnostics.is_empty() => Some(lowered.schema),
        _ => None,
    };

    (schema, diagnostics.iter().map(view_of).collect())
}

fn predicates(schema: &Schema) -> Vec<PredicateView> {
    (0..schema.len())
        .filter_map(|index| {
            let id = PredicateId(index as u32);
            let predicate = schema.get(id)?;
            Some(PredicateView {
                id: id.0,
                name: predicate.name()?.to_owned(),
                ty: print::signature(schema, id)?,
            })
        })
        .collect()
}
