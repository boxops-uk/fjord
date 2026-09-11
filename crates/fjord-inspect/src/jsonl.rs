//! **JSONL into a `MemStore`** — the portable format, loaded where nothing can intern.
//!
//! What `fjord export` writes and a person edits, read back into a store the engine can
//! answer over. [`crate::corpus`] is the caller: a real index, fetched as a static asset
//! and queried in the page.
//!
//! # It assigns the ids, and that is the whole trick
//!
//! Nothing in a browser can intern a fact: interning claims ids durably through a batch,
//! which is the fjall backend's. But a file whose references only ever name **earlier
//! lines** needs no interning to load — it needs counting. Each line gets the next
//! sequence for its predicate, a reference resolves through the ids already handed out,
//! and one forward pass is enough.
//!
//! So the ids differ from the database the file came from. That costs nothing:
//! `ops-I4`'s content identity is a multiset hash over each fact's *logical* form, so a
//! loaded copy is the same database under different numbering.
//!
//! # A load is a parse and an encode, and that is what it costs
//!
//! Every line is parsed as JSON and every key **encoded back into storage bytes**,
//! because `MemStore` holds what the real store holds and a seek there is a byte
//! comparison. Measured in the browser, that is roughly 3× what handing a store its own
//! bytes back would cost — the price of one portable format anybody can read, paid once
//! per load rather than per query.

use std::collections::HashMap;

use fjord_encoding::tuple::{self, Value};
use fjord_schema::{
    id::FactId,
    schema::{PredicateId, PredicateTy, Schema},
};
use fjord_store_mem::MemStore;

/// A store built from a file, and how many facts went into it.
pub struct Read {
    pub store: MemStore,
    pub facts: usize,
}

/// Read `text` into a store against `schema`.
///
/// # Errors
///
/// A sentence naming the line: a predicate the schema does not declare, a reference to an
/// id that has not been seen, or a value that does not fit its declared type.
pub fn read(text: &str, schema: &Schema) -> Result<Read, String> {
    let mut store = MemStore::new();

    // The next sequence for each predicate. Sequence 0 is reserved, so these start at 1.
    let mut next: HashMap<u32, u64> = HashMap::new();
    let mut local: HashMap<String, (PredicateId, FactId)> = HashMap::new();
    let mut facts = 0usize;

    for (at, line) in text.lines().enumerate() {
        let line = line.trim();
        if line.is_empty() || line.starts_with('#') {
            continue;
        }

        let at = at + 1;
        let parsed: serde_json::Value =
            serde_json::from_str(line).map_err(|why| format!("line {at}: not JSON: {why}"))?;

        let object = parsed
            .as_object()
            .ok_or_else(|| format!("line {at}: a line is an object"))?;

        let name = object
            .get("predicate")
            .and_then(serde_json::Value::as_str)
            .ok_or_else(|| format!("line {at}: a line needs a `predicate`"))?;

        let (id, declared) = schema
            .find_position(name)
            .ok_or_else(|| format!("line {at}: `{name}` is not a predicate of this schema"))?;

        let key_json = object
            .get("fact")
            .ok_or_else(|| format!("line {at}: a line needs a `fact`"))?;

        let key = value(declared.key().ty, key_json, &local, schema)
            .map_err(|why| format!("line {at}: fact: {why}"))?;

        let key_bytes = tuple::encode_key(declared.key().ty, &key)
            .map_err(|why| format!("line {at}: fact: {why}"))?;

        let value_bytes = match (declared.value(), object.get("value")) {
            (Some(ty), Some(json)) => {
                let decoded = value(ty.ty, json, &local, schema)
                    .map_err(|why| format!("line {at}: value: {why}"))?;

                tuple::encode_typed(ty.ty, &decoded)
                    .map_err(|why| format!("line {at}: value: {why}"))?
            }
            (Some(_), None) => {
                return Err(format!(
                    "line {at}: `{name}` has a value side and none is given"
                ));
            }
            (None, Some(_)) => {
                return Err(format!("line {at}: `{name}` has no value side"));
            }
            (None, None) => Vec::new(),
        };

        let sequence = next.entry(id.0).or_insert(1);
        let assigned = FactId::new(id, *sequence).map_err(|why| format!("line {at}: {why}"))?;

        store.insert_valued(id, key_bytes, *sequence, value_bytes);
        *sequence += 1;
        facts += 1;

        if let Some(named) = object.get("id") {
            let named = local_id(named).map_err(|why| format!("line {at}: {why}"))?;

            if local.insert(named.clone(), (id, assigned)).is_some() {
                return Err(format!("line {at}: `{named}` names two facts"));
            }
        }
    }

    Ok(Read { store, facts })
}

/// Coerce one JSON value to its declared type.
fn value(
    ty: &PredicateTy,
    json: &serde_json::Value,
    local: &HashMap<String, (PredicateId, FactId)>,
    schema: &Schema,
) -> Result<Value, String> {
    match ty {
        PredicateTy::Int => json
            .as_i64()
            .map(Value::Int)
            .ok_or_else(|| "expected an integer".to_owned()),

        PredicateTy::Str => json
            .as_str()
            .map(|text| Value::Str(text.to_owned()))
            .ok_or_else(|| "expected a string".to_owned()),

        // Hex, which is what `export` writes and what the other JSON view renders.
        PredicateTy::Bytes => {
            let text = json.as_str().ok_or("expected hex in a string")?;
            Ok(Value::Bytes(unhex(text)?))
        }

        PredicateTy::Fact(target) => {
            let named = local_id(json)?;

            let (predicate, id) = local
                .get(&named)
                .ok_or_else(|| format!("`{named}` has not been read yet"))?;

            if predicate != target {
                return Err(format!("`{named}` is a fact of another predicate"));
            }

            Ok(Value::FactRef(*id))
        }

        PredicateTy::Record(fields) => {
            let object = json.as_object().ok_or("expected an object")?;
            let mut out = Vec::with_capacity(fields.len());

            for (name, field_ty) in fields.iter() {
                let name = schema
                    .interner()
                    .resolve(*name)
                    .ok_or("this schema cannot name one of its fields")?;

                let found = object
                    .get(name)
                    .ok_or_else(|| format!("`{name}` is missing"))?;

                out.push((
                    name.to_owned(),
                    value(field_ty, found, local, schema)
                        .map_err(|why| format!("{name}: {why}"))?,
                ));
            }

            Ok(Value::Record(out.into_boxed_slice()))
        }

        PredicateTy::Union(alternatives) => {
            let object = json.as_object().ok_or("expected an object")?;

            let (chosen, payload) = match object.iter().next() {
                Some(one) if object.len() == 1 => one,
                _ => return Err("a union takes exactly one alternative".to_owned()),
            };

            let alternative = alternatives
                .iter()
                .find(|a| schema.interner().resolve(a.name) == Some(chosen.as_str()))
                .ok_or_else(|| format!("`{chosen}` is not an alternative here"))?;

            Ok(Value::Union {
                disc: alternative.disc,
                alt: chosen.clone(),
                value: Box::new(
                    value(&alternative.ty, payload, local, schema)
                        .map_err(|why| format!("{chosen}: {why}"))?,
                ),
            })
        }
    }
}

/// A local id, as a string, however it was written.
fn local_id(json: &serde_json::Value) -> Result<String, String> {
    match json {
        serde_json::Value::String(text) => Ok(text.clone()),
        serde_json::Value::Number(number) => Ok(number.to_string()),
        _ => Err("an id is a string or a number".to_owned()),
    }
}

fn unhex(text: &str) -> Result<Vec<u8>, String> {
    if text.len() % 2 != 0 {
        return Err("hex has two digits a byte".to_owned());
    }

    (0..text.len())
        .step_by(2)
        .map(|at| u8::from_str_radix(&text[at..at + 2], 16).map_err(|_| "not hex".to_owned()))
        .collect()
}
