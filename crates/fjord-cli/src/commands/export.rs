//! `fjord export <db> --to <file.jsonl>` — a database as the portable format.
//!
//! **The same grammar `fjord write` reads**, so an export can be written back: one JSON
//! object per line, a local `id` naming the fact within the file, and a reference
//! carrying the id of a fact written earlier.
//!
//! # Two orders, because a reader and a person want different ones
//!
//! Both satisfy the rule that makes the format one forward pass — a reference names a
//! line already gone past — and they differ in *how far back* that line is.
//!
//! **Dependency order**, the default, puts a fact's targets in the lines immediately
//! above it: a decl, then the file it names, then the next decl. That is how a person
//! reads one, because the answer to "what is this `3`?" is a line or two up.
//!
//! **Grouped order**, under `--compact`, writes every fact of one predicate before any
//! fact of the next, with the predicates themselves in dependency order. What that buys
//! is the **export itself**, not the file: a component at a time is the only thing
//! resident, and there is no walk to do. Measured on a 550,000-fact database, **2.5s and
//! 299 MB against 6.0s and 364 MB** — and on that schema the two orders emit byte-for-byte
//! the same file, because its predicates are already declared in dependency order.
//!
//! What it does **not** buy, measured rather than assumed, on the code browser's
//! 24,612-fact corpus: the file is the same size, gzips 1.5% smaller, and writes back
//! with 6% fewer redundant targets. The wire saving is small because `fjord write`
//! batches ten thousand lines, so a target and the facts naming it usually land in one
//! batch either way — grouping only helps across a batch boundary.
//!
//! # Why the order is not the order they are stored in
//!
//! Facts are stored grouped by predicate, and a predicate's id comes from where it sits
//! in the schema rather than from what it references — so nothing says a target's group
//! is emitted before the group naming it. Both orders fix that, and `--compact` fixes it
//! with an order the schema already knows: `fjord_schema::refs::components` answers
//! strongly-connected components, each after everything it reaches.
//!
//! **A component can hold more than one predicate**, because a schema may declare two
//! that name each other, and then no per-predicate order exists. Within a component that
//! can cycle the walk is the depth-first one, over that component's rows alone — every
//! other component is already written, so it bottoms out at once.
//!
//! A predicate that names **itself** is a component of one that can still cycle, which is
//! why the size of a component is not the test.
//!
//! # It reads the store directly
//!
//! Which means it opens fjall, which `ops-I1` gives to one process: a server holding this
//! root is a refusal rather than something to work around.

use std::{collections::HashMap, io::Write, path::Path};

use fjord_encoding::tuple::{self, Value};
use fjord_schema::{
    id::FactId,
    refs,
    schema::{LocalInterner, PredicateId, Schema},
};
use fjord_store::fact_store::FactStore;
use fjord_store_mem::dump::OwnedRow;

use crate::{
    CliError,
    commands::{self, Route, Target},
};

/// What an export came to, for the line the tool prints.
pub struct Dumped {
    pub facts: usize,
    pub bytes: u64,
}

/// Which order the facts come out in.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Order {
    /// A fact's targets in the lines just above it.
    Dependency,
    /// Every fact of a predicate together, predicates in dependency order.
    Grouped,
}

/// One fact, decoded and waiting for its turn.
struct Row {
    predicate: PredicateId,
    key: Value,
    value: Option<Value>,
}

/// The local ids handed out so far, and the next one to hand out.
///
/// **A reference renders through this**, so a fact reaching it for a target that is not
/// in it yet would write `null` — which is why every path here writes a fact only once
/// everything it names is present.
struct Numbering {
    local: HashMap<u64, u64>,
    next: u64,
}

/// # Errors
///
/// [`CliError::ExportNeedsTheRoot`] if a server holds it, [`CliError::NoEmbeddedSchema`]
/// for a database carrying no schema copy, or whatever reading or writing reports.
pub fn run(root: &Path, target: &Target, to: &Path, order: Order) -> Result<Dumped, CliError> {
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

    // **One reader, so every predicate is scanned against one snapshot**: an export of a
    // database that moved under the walk would hold rows from two states.
    let reader = db.reader();

    let file = std::fs::File::create(to)?;
    let mut out = std::io::BufWriter::new(file);

    let mut numbering = Numbering {
        local: HashMap::new(),
        next: 1,
    };

    let facts = match order {
        Order::Dependency => {
            dependency_order(&reader, &schema, &interner, &mut numbering, &mut out)?
        }
        Order::Grouped => grouped_order(&reader, &schema, &interner, &mut numbering, &mut out)?,
    };

    out.flush()?;
    drop(out);

    Ok(Dumped {
        facts,
        bytes: std::fs::metadata(to)?.len(),
    })
}

/// Every stored predicate, in an order where a component follows everything it reaches.
fn stored_order(schema: &Schema) -> Vec<Vec<PredicateId>> {
    let stored: Vec<PredicateId> = (0..schema.len())
        .map(|index| PredicateId(index as u32))
        .filter(|id| !schema.is_virtual(*id))
        .collect();

    refs::components(schema, &stored)
}

/// A predicate at a time, in dependency order — `--compact`.
///
/// **Only one component's rows are resident**, which for a real index is the difference
/// between an export that fits in memory and one that does not. The numbering is not:
/// a reference renders as a local id, so every id handed out so far has to be in reach.
fn grouped_order<S: FactStore, W: Write>(
    reader: &S,
    schema: &Schema,
    interner: &LocalInterner,
    numbering: &mut Numbering,
    out: &mut W,
) -> Result<usize, CliError> {
    let mut written = 0usize;

    for group in stored_order(schema) {
        // A component of one predicate that does not name itself has no reference into
        // its own rows, so scan order is already an order that reads back.
        let simple = group.len() == 1 && !refs::self_referencing(schema, group[0]);

        let mut rows: Vec<(FactId, Row)> = Vec::new();

        for predicate in &group {
            for raw in fjord_store_mem::dump::rows_for(reader, *predicate)? {
                rows.push(decode(&raw, schema, interner)?);
            }
        }

        if simple {
            for (id, row) in &rows {
                emit(*id, row, schema, numbering, out)?;
                written += 1;
            }
            continue;
        }

        // A component that can cycle: the rows it holds are the only ones not yet
        // written, so a depth-first walk over just these bottoms out on the first
        // reference leaving the component.
        written += walk(&rows, schema, numbering, out)?;
    }

    Ok(written)
}

/// Targets in the lines immediately above the fact naming them — the default.
///
/// The whole database is resident, because a walk that can reach any row has to be able
/// to reach any row.
fn dependency_order<S: FactStore, W: Write>(
    reader: &S,
    schema: &Schema,
    interner: &LocalInterner,
    numbering: &mut Numbering,
    out: &mut W,
) -> Result<usize, CliError> {
    let mut rows: Vec<(FactId, Row)> = Vec::new();

    for index in 0..schema.len() {
        let predicate = PredicateId(index as u32);
        if schema.is_virtual(predicate) {
            continue;
        }

        for raw in fjord_store_mem::dump::rows_for(reader, predicate)? {
            rows.push(decode(&raw, schema, interner)?);
        }
    }

    walk(&rows, schema, numbering, out)
}

/// Depth first over `rows`, writing a fact once everything it names has been written.
///
/// A reference to a fact **outside** `rows` is expected to be numbered already; one that
/// is neither is a damaged database and says so rather than writing a dangling file.
fn walk<W: Write>(
    rows: &[(FactId, Row)],
    schema: &Schema,
    numbering: &mut Numbering,
    out: &mut W,
) -> Result<usize, CliError> {
    let at: HashMap<u64, usize> = rows
        .iter()
        .enumerate()
        .map(|(index, (id, _))| (id.raw(), index))
        .collect();

    let mut written = 0usize;

    // **Explicit stack, because a reference chain can be deep** — a doc names a decl
    // which names a file — and a recursive walk would put that depth on the call stack.
    let mut stack: Vec<FactId> = Vec::new();

    for (start, _) in rows {
        stack.push(*start);

        while let Some(id) = stack.last().copied() {
            if numbering.local.contains_key(&id.raw()) {
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

            let (_, row) = &rows[index];

            // Everything it names, first. A node is reached twice — once to push its
            // targets, once when they are all numbered — and a target pushed by two
            // parents costs a visit that pops immediately, so this is O(edges).
            let mut waiting = false;

            for named in references(&row.key).chain(row.value.iter().flat_map(references)) {
                if !numbering.local.contains_key(&named.raw()) {
                    stack.push(named);
                    waiting = true;
                }
            }

            if waiting {
                continue;
            }

            stack.pop();
            emit(id, row, schema, numbering, out)?;
            written += 1;
        }
    }

    Ok(written)
}

/// One stored row, decoded against the schema, with the id it is stored under.
///
/// A key is stored flat — its top-level fields back to back with no record wrapper —
/// which is why it takes `decode_key` and a value takes the other.
fn decode(
    raw: &OwnedRow,
    schema: &Schema,
    interner: &LocalInterner,
) -> Result<(FactId, Row), CliError> {
    let declared = schema.get(raw.predicate).ok_or_else(|| {
        CliError::Diagnosed(format!(
            "fjord: this database holds a fact of predicate {}, which its schema does \
             not declare\n",
            raw.predicate.0
        ))
    })?;

    let key = tuple::decode_key(interner, &raw.key, declared.key().ty)
        .map_err(|why| CliError::Diagnosed(format!("fjord: a stored key: {why}\n")))?;

    let value = match declared.value() {
        Some(ty) if !raw.value.is_empty() => Some(
            tuple::decode_typed(interner, &raw.value, ty.ty)
                .map_err(|why| CliError::Diagnosed(format!("fjord: a stored value: {why}\n")))?,
        ),
        _ => None,
    };

    let id = FactId::new(raw.predicate, raw.sequence)
        .map_err(|why| CliError::Diagnosed(format!("fjord: a stored id: {why}\n")))?;

    Ok((
        id,
        Row {
            predicate: raw.predicate,
            key,
            value,
        },
    ))
}

/// Write one fact, and give it the next local id.
fn emit<W: Write>(
    id: FactId,
    row: &Row,
    schema: &Schema,
    numbering: &mut Numbering,
    out: &mut W,
) -> Result<(), CliError> {
    let name = schema
        .get(row.predicate)
        .and_then(|predicate| predicate.name())
        .unwrap_or_default();

    // **Written field by field, so the order reads.** `serde_json`'s map sorts, which
    // would put `fact` before `id` on every line of a format whose whole point is that a
    // person can read and edit it.
    write!(
        out,
        "{{\"id\":{},\"predicate\":{},\"fact\":{}",
        numbering.next,
        serde_json::Value::from(name),
        json(&row.key, &numbering.local)
    )?;

    if let Some(value) = &row.value {
        write!(out, ",\"value\":{}", json(value, &numbering.local))?;
    }

    writeln!(out, "}}")?;

    numbering.local.insert(id.raw(), numbering.next);
    numbering.next += 1;

    Ok(())
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
