//! **JSONL in, facts out** — the format a person or an agent writes by hand.
//!
//! One JSON object per line:
//!
//! ```text
//! {"id": "1", "predicate": "src.File", "fact": "store/keys.py"}
//! {"id": "2", "predicate": "src.Decl", "fact": {"file": "1", "name": "put", "line": 12}}
//! {"id": "3", "predicate": "src.Digest", "fact": {"file": "1"}, "value": {"sha256": "…"}}
//! ```
//!
//! **Not the ingestion path an indexer should use.** A producer writing at volume speaks
//! the wire protocol through a client library, where a fact is encoded once and a block
//! carries hundreds. This is for the other case: a person authoring a few facts, an agent
//! fiddling, a fixture. It is deliberately the simple thing.
//!
//! # An id is local to the file, and a reference must already have been seen
//!
//! `id` names a fact **within this document** and nothing else — it is not the id the
//! database will give it, and re-reading the same file into a fresh database renumbers
//! everything. That costs nothing: `ops-I4`'s content identity is a multiset hash over
//! each fact's *logical* form, so a renumbered copy of a database is the same database.
//!
//! A reference field carries the id of a fact **written earlier in the file**, and a
//! forward reference is refused rather than held over. Refusing is what makes a reader a
//! single forward pass: nothing is buffered waiting for a target, a cycle cannot be
//! expressed, and the order a person reads the file in is the order it is built in.
//!
//! # A local reference travels as the target fact, not as an id
//!
//! The server assigns ids, and a write stream answers with counts rather than with the
//! ids it minted — so this end never learns what `1` became. It does not need to: a
//! reference on the way in may be **the whole target fact**, nested, and the server
//! interns it. So `{"file": "1"}` is expanded to the fact `1` named, and writing the same
//! target under twenty referrers interns it once and deduplicates the rest.
//!
//! The cost is that every fact seen so far is held, in order to inline it later. That is
//! the trade this format makes, and the reason it is not the path an indexer takes.

use fjord_schema::{
    id::FactId,
    schema::{PredicateId, PredicateTy, Schema},
};
use fjord_wire::{WireFact, WireRef, WireValue};
use serde_json::Value;

/// What a local id names: the fact itself while it is unsent, its id once it is not.
///
/// **Both, because a reference resolves when a line is read and an id arrives when a
/// batch is flushed.** A line referencing a fact from the batch still being built has no
/// id to use, so the target travels inline and the server interns it. Once that batch has
/// been written the id is known, and every later reference is eight bytes instead of a
/// copy of the target.
#[derive(Clone)]
pub enum Named {
    /// Written but not yet sent: referencing it inlines it.
    Pending(WireFact),
    /// Sent, and the server said what it was called.
    Written(PredicateId, FactId),
}

impl Named {
    fn predicate(&self) -> PredicateId {
        match self {
            Named::Pending(fact) => fact.predicate,
            Named::Written(predicate, _) => *predicate,
        }
    }
}

/// A document being read: what each local id named, so a later line can reference it.
#[derive(Default)]
pub struct Seen {
    by_id: std::collections::HashMap<String, Named>,
    /// The ids named by the batch being built, in the order they were written, so the
    /// ids a flush reports can be matched back to them.
    pending: Vec<String>,
}

impl Seen {
    /// Called once a batch has been written, with the ids the server reported.
    ///
    /// **Matched by position**, which is the contract the reply states: the ids are the
    /// top-level facts in the order they were sent. A mismatch in length is this end
    /// having sent something it did not record, and is a bug here rather than a
    /// malformed file — so it leaves the names pending rather than pairing them wrongly.
    pub fn written(&mut self, sent: &[(PredicateId, WireFact)], ids: &[FactId]) {
        if sent.len() != ids.len() {
            return;
        }

        // Only the lines that gave themselves a name are in `pending`, and they are a
        // subsequence of what was sent — so they are matched through the facts, not by
        // counting.
        let mut names = self
            .pending
            .drain(..)
            .collect::<Vec<_>>()
            .into_iter()
            .peekable();

        for ((predicate, fact), id) in sent.iter().zip(ids) {
            let Some(name) = names.peek() else { break };

            if matches!(self.by_id.get(name.as_str()), Some(Named::Pending(held)) if held == fact) {
                let name = names.next().expect("peeked");
                self.by_id.insert(name, Named::Written(*predicate, *id));
            }
        }
    }
}

impl Seen {
    /// Read one line into a fact, resolving its references against what came before.
    ///
    /// # Errors
    ///
    /// A sentence naming what was wrong: an id already used, a predicate the schema does
    /// not declare, a reference to an id not yet seen, or a value that does not fit its
    /// declared type.
    pub fn line(&mut self, schema: &Schema, text: &str) -> Result<(PredicateId, WireFact), String> {
        let line: Value = serde_json::from_str(text).map_err(|why| format!("not JSON: {why}"))?;

        let Some(object) = line.as_object() else {
            return Err(format!("a line is an object, found {}", shape(&line)));
        };

        let name = object
            .get("predicate")
            .and_then(Value::as_str)
            .ok_or("a line needs a `predicate`, as a string")?;

        let (id, declared) = schema
            .find_position(name)
            .ok_or_else(|| format!("`{name}` is not a predicate of this database"))?;

        if schema.is_virtual(id) {
            return Err(format!(
                "`{name}` is answered by the server rather than stored, so nothing can be \
                 written to it"
            ));
        }

        let key_json = object
            .get("fact")
            .ok_or("a line needs a `fact`, which is the predicate's key")?;

        let key = self.value(schema, declared.key().ty, key_json, "fact")?;

        let value = match (declared.value(), object.get("value")) {
            (Some(ty), Some(json)) => Some(self.value(schema, ty.ty, json, "value")?),
            (Some(_), None) => {
                return Err(format!(
                    "`{name}` has a value side, so the line needs a `value`"
                ));
            }
            (None, Some(_)) => {
                return Err(format!(
                    "`{name}` has no value side, so `value` is not allowed"
                ));
            }
            (None, None) => None,
        };

        let fact = WireFact {
            predicate: id,
            key,
            value,
        };

        // **Recorded only for a line that says `id`.** A fact nothing references needs no
        // name, and making one up would hold it for the life of the read for nothing.
        if let Some(named) = object.get("id") {
            let named = local(named)?;

            if self.by_id.contains_key(&named) {
                return Err(format!("`{named}` names two facts; an id is used once"));
            }

            self.by_id
                .insert(named.clone(), Named::Pending(fact.clone()));
            self.pending.push(named);
        }

        Ok((id, fact))
    }

    /// Coerce `json` to the declared type, or say where it did not fit.
    fn value(
        &self,
        schema: &Schema,
        ty: &PredicateTy,
        json: &Value,
        at: &str,
    ) -> Result<WireValue, String> {
        match ty {
            PredicateTy::Int => json
                .as_i64()
                .map(WireValue::Int)
                .ok_or_else(|| format!("{at}: expected an integer, found {}", shape(json))),

            PredicateTy::Str => json
                .as_str()
                .map(|s| WireValue::Str(s.to_owned()))
                .ok_or_else(|| format!("{at}: expected a string, found {}", shape(json))),

            // **Base64, and stated as such in the tool's own schema.** A JSON array of
            // numbers would read as bytes too, and would silently accept `[300]`.
            PredicateTy::Bytes => {
                let text = json.as_str().ok_or_else(|| {
                    format!("{at}: expected base64 in a string, found {}", shape(json))
                })?;
                Ok(WireValue::Bytes(
                    base64(text).map_err(|why| format!("{at}: {why}"))?,
                ))
            }

            PredicateTy::Fact(target) => self.reference(schema, *target, json, at),

            PredicateTy::Record(fields) => {
                let Some(map) = json.as_object() else {
                    return Err(format!("{at}: expected an object, found {}", shape(json)));
                };

                // **Schema order, not the object's.** The wire carries no names.
                let mut out = Vec::with_capacity(fields.len());
                for (name, field_ty) in fields.iter() {
                    let name = schema.interner().resolve(*name).ok_or_else(|| {
                        format!("{at}: this schema cannot name one of its fields")
                    })?;

                    let found = map.get(name).ok_or_else(|| {
                        format!("{at}.{name}: missing, and the schema declares it")
                    })?;

                    out.push(self.value(schema, field_ty, found, &format!("{at}.{name}"))?);
                }

                for key in map.keys() {
                    if !fields
                        .iter()
                        .any(|(name, _)| schema.interner().resolve(*name) == Some(key.as_str()))
                    {
                        return Err(format!("{at}.{key}: the schema declares no such field"));
                    }
                }

                Ok(WireValue::Record(out.into_boxed_slice()))
            }

            // `{"just": 3}` or `{"nothing": {}}` — the alternative's name carries the tag,
            // which is the one thing the schema cannot supply.
            PredicateTy::Union(alternatives) => {
                let Some(map) = json.as_object() else {
                    return Err(format!(
                        "{at}: expected an object naming one alternative ({}), found {}",
                        names(schema, alternatives),
                        shape(json)
                    ));
                };

                if map.len() != 1 {
                    return Err(format!(
                        "{at}: a union takes exactly one alternative, of {}",
                        names(schema, alternatives)
                    ));
                }

                let (chosen, payload) = map.iter().next().expect("one entry");

                let alternative = alternatives
                    .iter()
                    .find(|a| schema.interner().resolve(a.name) == Some(chosen.as_str()))
                    .ok_or_else(|| {
                        format!(
                            "{at}: `{chosen}` is not an alternative here — it takes one of {}",
                            names(schema, alternatives)
                        )
                    })?;

                Ok(WireValue::Union {
                    disc: alternative.disc,
                    value: Box::new(self.value(
                        schema,
                        &alternative.ty,
                        payload,
                        &format!("{at}.{chosen}"),
                    )?),
                })
            }
        }
    }

    /// A reference: a **local id already seen**, or the whole target fact inline.
    ///
    /// The local id is the form this file is for, and the reason a forward reference is
    /// refused: resolving `"1"` means looking up a fact this reader has already built,
    /// and there is nothing to look up until the line that built it has gone past.
    ///
    /// Nesting is still accepted, because it costs nothing to accept and it is what a
    /// producer speaking the protocol writes: the target fact where the reference goes,
    /// interned by the server bottom-up.
    fn reference(
        &self,
        schema: &Schema,
        target: PredicateId,
        json: &Value,
        at: &str,
    ) -> Result<WireValue, String> {
        // An object is the target fact, written out where the reference goes.
        if json.is_object() {
            let nested = self
                .nested(schema, target, json)
                .map_err(|why| format!("{at} -> {why}"))?;

            return Ok(WireValue::Ref(WireRef::Nested(Box::new(nested))));
        }

        // Otherwise it names a fact this file has already written.
        let named = local(json).map_err(|why| format!("{at}: {why}"))?;

        let held = self.by_id.get(&named).ok_or_else(|| {
            format!(
                "{at}: `{named}` has not been written yet. A reference names a fact from an \
                 earlier line — a forward reference is refused rather than held over"
            )
        })?;

        let expected = schema
            .get(target)
            .and_then(|predicate| predicate.name())
            .unwrap_or("that predicate");

        if held.predicate() != target {
            let found = schema
                .get(held.predicate())
                .and_then(|predicate| predicate.name())
                .unwrap_or("something else");

            return Err(format!(
                "{at}: `{named}` is a `{found}`, and a `{expected}` is declared here"
            ));
        }

        Ok(WireValue::Ref(match held {
            // **The id, once there is one.** The target has been written and the server
            // said what it called it, so a reference costs eight bytes rather than a
            // copy of the target — which is what keeps a file with one heavily-referenced
            // fact from re-sending it once per referrer.
            Named::Written(_, id) => WireRef::Id(*id),

            // Still in the batch being built, so there is no id yet: the target travels
            // inline and the server interns it, deduplicating it against the line that
            // first wrote it.
            Named::Pending(fact) => WireRef::Nested(Box::new(fact.clone())),
        }))
    }

    /// A target fact written inline, which has no `id` and no `predicate` of its own —
    /// the field's declared target says which predicate it is.
    fn nested(
        &self,
        schema: &Schema,
        target: PredicateId,
        json: &Value,
    ) -> Result<WireFact, String> {
        let declared = schema
            .get(target)
            .ok_or_else(|| format!("predicate {} is not in this schema", target.0))?;

        let object = json.as_object().expect("an object, checked by the caller");

        // A nested target may carry its own value side, and usually does not.
        let key_json = object.get("fact").unwrap_or(json);

        Ok(WireFact {
            predicate: target,
            key: self.value(schema, declared.key().ty, key_json, "fact")?,
            value: match (declared.value(), object.get("value")) {
                (Some(ty), Some(found)) => Some(self.value(schema, ty.ty, found, "value")?),
                _ => None,
            },
        })
    }
}

/// A local id, as a string, however it was written.
///
/// A number and the string of that number are the same id: a person authoring by hand
/// writes `1` on one line and `"1"` on the next without meaning two different things.
fn local(json: &Value) -> Result<String, String> {
    match json {
        Value::String(text) if text.starts_with('#') => Err(format!(
            "`{text}` starts with `#`, which a local id may not — ids here name facts in \
             this file, not in the database"
        )),
        Value::String(text) => Ok(text.clone()),
        Value::Number(number) => Ok(number.to_string()),
        other => Err(format!(
            "an id is a string or a number, found {}",
            shape(other)
        )),
    }
}

fn names(schema: &Schema, alternatives: &[fjord_schema::schema::Alternative]) -> String {
    alternatives
        .iter()
        .filter_map(|a| schema.interner().resolve(a.name))
        .collect::<Vec<_>>()
        .join(", ")
}

fn shape(json: &Value) -> &'static str {
    match json {
        Value::Null => "null",
        Value::Bool(_) => "a boolean",
        Value::Number(_) => "a number",
        Value::String(_) => "a string",
        Value::Array(_) => "an array",
        Value::Object(_) => "an object",
    }
}

/// Standard base64, decoded by hand rather than by a dependency.
fn base64(text: &str) -> Result<Vec<u8>, String> {
    const ALPHABET: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    let trimmed = text.trim_end_matches('=');
    let mut out = Vec::with_capacity(trimmed.len() * 3 / 4);
    let mut accumulator: u32 = 0;
    let mut bits = 0u32;

    for byte in trimmed.bytes() {
        let Some(index) = ALPHABET.iter().position(|c| *c == byte) else {
            return Err(format!("`{}` is not base64", byte as char));
        };

        accumulator = (accumulator << 6) | index as u32;
        bits += 6;

        if bits >= 8 {
            bits -= 8;
            out.push((accumulator >> bits) as u8);
        }
    }

    Ok(out)
}
