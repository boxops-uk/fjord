//! `fjord export <db> --to <file.jsonl>` — a database as the portable format.
//!
//! **The same grammar `fjord write` reads**, so an export can be written back: one JSON
//! object per line, a local `id` naming the fact within the file, and a reference
//! carrying the id of a fact written earlier.
//!
//! # Why the order is not the order they are stored in
//!
//! A reference must name a line that has already gone past — that is the rule that makes
//! a reader one forward pass, and it is the reader's rule whether the writer is a person
//! or this. Facts are stored grouped by predicate, and a predicate's id comes from where
//! it sits in the schema rather than from what it references — so nothing says a target's
//! group is emitted before the group naming it.
//!
//! So this emits **targets first**, depth first, and a fact is written the moment
//! everything it names has been. References cannot cycle — a key's bytes do not exist
//! until the facts it references have ids — so the walk terminates without a cycle check.
//!
//! # It reads the store directly
//!
//! Which means it opens fjall, which `ops-I1` gives to one process: a server holding this
//! root is a refusal rather than something to work around.

use std::{collections::HashMap, io::Write, path::Path};

use fjord_encoding::tuple::{self, Value};
use fjord_schema::{
    id::FactId,
    schema::{LocalInterner, PredicateId},
};

use crate::{
    CliError,
    commands::{self, Route, Target},
};

/// What an export came to, for the line the tool prints.
pub struct Dumped {
    pub facts: usize,
    pub bytes: u64,
}

/// One fact, decoded and waiting for its turn.
struct Row {
    predicate: PredicateId,
    key: Value,
    value: Option<Value>,
}

/// # Errors
///
/// [`CliError::ExportNeedsTheRoot`] if a server holds it, [`CliError::NoEmbeddedSchema`]
/// for a database carrying no schema copy, or whatever reading or writing reports.
pub fn run(root: &Path, target: &Target, to: &Path) -> Result<Dumped, CliError> {
    let catalog = match commands::route(root, target)? {
        Route::Local(catalog, _lock) => catalog,
        Route::Server(_) => return Err(CliError::ExportNeedsTheRoot),
    };

    let entry = catalog.resolve(
        &fjord_store_fjall::catalog::Selector::parse(&target.database)?,
        fjord_store_fjall::catalog::Intent::Read,
    )?;

    let schema = fjord_store_fjall::schema_doc::read(&entry.path)?.ok_or_else(|| {
        CliError::NoEmbeddedSchema {
            name: target.database.clone(),
        }
    })?;

    let db = entry.open_store()?;
    let interner = LocalInterner::new(schema.interner().clone());

    // **One reader, so every predicate is scanned against one snapshot**: a dump of a
    // database that moved under the walk would hold rows from two states.
    let reader = db.reader();
    let raw =
        fjord_store_mem::dump::rows_of(&reader, u32::try_from(schema.len()).unwrap_or(u32::MAX))?;

    // Decoded once. A key is stored flat — its top-level fields back to back with no
    // record wrapper — which is why it takes `decode_key` and a value takes the other.
    let mut rows: Vec<Row> = Vec::with_capacity(raw.len());
    let mut at: HashMap<u64, usize> = HashMap::with_capacity(raw.len());

    for row in &raw {
        let declared = schema.get(row.predicate).ok_or_else(|| {
            CliError::Diagnosed(format!(
                "fjord: this database holds a fact of predicate {}, which its schema does \
                 not declare\n",
                row.predicate.0
            ))
        })?;

        let key = tuple::decode_key(&interner, &row.key, declared.key().ty)
            .map_err(|why| CliError::Diagnosed(format!("fjord: a stored key: {why}\n")))?;

        let value = match declared.value() {
            Some(ty) if !row.value.is_empty() => Some(
                tuple::decode_typed(&interner, &row.value, ty.ty).map_err(|why| {
                    CliError::Diagnosed(format!("fjord: a stored value: {why}\n"))
                })?,
            ),
            _ => None,
        };

        let id = FactId::new(row.predicate, row.sequence)
            .map_err(|why| CliError::Diagnosed(format!("fjord: a stored id: {why}\n")))?;

        at.insert(id.raw(), rows.len());
        rows.push(Row {
            predicate: row.predicate,
            key,
            value,
        });
    }

    let file = std::fs::File::create(to)?;
    let mut out = std::io::BufWriter::new(file);

    let mut local: HashMap<u64, u64> = HashMap::with_capacity(rows.len());
    let mut next = 1u64;
    let mut written = 0usize;

    // **Explicit stack, because a reference chain can be deep** — a doc names a decl
    // which names a file — and a recursive walk would put that depth on the call stack.
    for start in raw.iter() {
        let start = FactId::new(start.predicate, start.sequence)
            .map_err(|why| CliError::Diagnosed(format!("fjord: a stored id: {why}\n")))?;

        let mut stack = vec![start];

        while let Some(id) = stack.last().copied() {
            if local.contains_key(&id.raw()) {
                stack.pop();
                continue;
            }

            let Some(index) = at.get(&id.raw()).copied() else {
                // A reference naming no stored fact is a damaged database, and saying so
                // is better than writing a file whose references dangle.
                return Err(CliError::Diagnosed(format!(
                    "fjord: a reference names {id:?}, which this database does not hold\n"
                )));
            };

            // Everything it names, first.
            let mut waiting = false;
            let row = &rows[index];

            for named in references(&row.key).chain(row.value.iter().flat_map(references)) {
                if !local.contains_key(&named.raw()) {
                    stack.push(named);
                    waiting = true;
                }
            }

            if waiting {
                continue;
            }

            stack.pop();

            let name = schema
                .get(row.predicate)
                .and_then(|predicate| predicate.name())
                .unwrap_or_default();

            // **Written field by field, so the order reads.** `serde_json`'s map sorts,
            // which would put `fact` before `id` on every line of a format whose whole
            // point is that a person can read and edit it.
            write!(
                out,
                "{{\"id\":{next},\"predicate\":{},\"fact\":{}",
                serde_json::Value::from(name),
                json(&row.key, &local)
            )?;

            if let Some(value) = &row.value {
                write!(out, ",\"value\":{}", json(value, &local))?;
            }

            writeln!(out, "}}")?;

            local.insert(id.raw(), next);
            next += 1;
            written += 1;
        }
    }

    out.flush()?;
    drop(out);

    Ok(Dumped {
        facts: written,
        bytes: std::fs::metadata(to)?.len(),
    })
}

/// Every fact this value names, directly or through a record or a union.
fn references(value: &Value) -> Box<dyn Iterator<Item = FactId> + '_> {
    match value {
        Value::FactRef(id) => Box::new(std::iter::once(*id)),
        Value::Record(fields) => Box::new(fields.iter().flat_map(|(_, inner)| references(inner))),
        Value::Union { value, .. } => references(value),
        _ => Box::new(std::iter::empty()),
    }
}

/// A decoded value as JSON, with a reference as the **local id** of the line that wrote
/// it — which is what makes the file readable back by `fjord write`.
fn json(value: &Value, local: &HashMap<u64, u64>) -> serde_json::Value {
    match value {
        Value::Null => serde_json::Value::Null,
        Value::Int(int) => serde_json::Value::from(*int),
        Value::Str(text) => serde_json::Value::from(text.clone()),
        Value::Bytes(payload) => serde_json::Value::from(hex(payload)),

        // The whole reason this is not `fjord_inspect::value::json`: that one renders a
        // reference as the database's own `#p:s`, which means nothing in another
        // database. This one names the line.
        Value::FactRef(id) => local
            .get(&id.raw())
            .map_or(serde_json::Value::Null, |named| {
                serde_json::Value::from(*named)
            }),

        Value::Record(fields) => serde_json::Value::Object(
            fields
                .iter()
                .map(|(name, inner)| (name.clone(), json(inner, local)))
                .collect(),
        ),

        Value::Union { alt, value, .. } => {
            serde_json::Value::Object([(alt.clone(), json(value, local))].into_iter().collect())
        }
    }
}

/// Lowercase hex, two digits a byte.
///
/// The same rendering the other JSON view uses, and the same reason: a byte pair reads in
/// a terminal, and a reader of this file holds the schema that says the field is `bytes`.
fn hex(bytes: &[u8]) -> String {
    use std::fmt::Write;

    bytes
        .iter()
        .fold(String::with_capacity(bytes.len() * 2), |mut out, byte| {
            let _ = write!(out, "{byte:02x}");
            out
        })
}
