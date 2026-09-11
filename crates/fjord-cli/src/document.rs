//! **A JSONL document over any schema** — the fixture a generative battery writes.
//!
//! Composed from the *type* rather than from a store, so the document is something a
//! third party wrote: a round trip of the exporter's own output would agree with itself
//! however wrong both halves were, and this does not.
//!
//! Here rather than in one of the batteries because two targets need one statement of
//! it — the binary's own unit tests, which put a document through the reader and the
//! writer in-process, and `tests/jsonl_write.rs`, which puts one through the tool.
//!
//! **Every reference names an earlier line**, by construction: a target is written
//! before the fact naming it and the fact carries its local id. The walk terminates
//! because a generated schema's references point at predicates declared *earlier*
//! ([`fjord_wire::value::proptest`] resolves them modulo the predicate's own index).

use std::collections::HashMap;

use fjord_schema::schema::{PredicateId, PredicateTy, Schema};

/// Draws for a generated document, cycled — the same tape shape
/// [`fjord_wire::value::proptest`] uses, because the values worth drawing are the same
/// ones.
pub struct Draws {
    ints: Vec<i64>,
    texts: Vec<String>,
    picks: Vec<u8>,
    at_int: usize,
    at_text: usize,
    at_pick: usize,
}

impl Draws {
    /// The tapes a generated fact is filled from, as
    /// [`fjord_wire::value::proptest`] draws them.
    #[must_use]
    pub fn new(ints: Vec<i64>, texts: Vec<String>, picks: Vec<u8>) -> Self {
        Self {
            ints,
            texts,
            picks,
            at_int: 0,
            at_text: 0,
            at_pick: 0,
        }
    }

    fn int(&mut self) -> i64 {
        let value = self.ints[self.at_int % self.ints.len()];
        self.at_int += 1;
        value
    }

    fn text(&mut self) -> String {
        let value = self.texts[self.at_text % self.texts.len()].clone();
        self.at_text += 1;
        value
    }

    fn pick(&mut self) -> usize {
        let value = self.picks[self.at_pick % self.picks.len()] as usize;
        self.at_pick += 1;
        value
    }
}

/// A JSONL document over `schema`: three facts of every predicate, references and all.
///
/// **Written by hand rather than by the exporter**, which is the point — a document a
/// third party composed is what says the *grammar* is readable, where a round trip of
/// the exporter's own output would agree with itself however wrong both halves were.
///
/// A reference is emitted as the target's whole line first and then as its local id, so
/// the no-forward-reference rule holds by construction. It terminates because a
/// generated schema's references point at predicates declared *earlier*
/// ([`fjord_wire::value::proptest`] resolves them modulo the predicate's own index), so
/// the walk strictly descends.
pub fn a_document_over(schema: &Schema, draws: &mut Draws) -> String {
    let mut out = String::new();
    let mut written = Written {
        next: 0,
        by_key: HashMap::new(),
    };

    for index in 0..schema.len() {
        let predicate = PredicateId(index as u32);
        for _ in 0..3 {
            a_line_of(predicate, schema, draws, &mut written, &mut out);
        }
    }

    out
}

/// What the document has written, so a key is written once.
///
/// **Interning, because a key is an identity.** Two draws can land on one key with
/// different values, and a database refuses that outright rather than picking a winner
/// — so a document that emitted both would be testing the conflict rule instead of the
/// format. The first line wins and every later reference to that key takes its id,
/// which is what a producer does.
struct Written {
    next: u64,
    by_key: HashMap<(u32, String), u64>,
}

/// One fact of `predicate`, its targets written first. Answers the local id it took.
fn a_line_of(
    predicate: PredicateId,
    schema: &Schema,
    draws: &mut Draws,
    written: &mut Written,
    out: &mut String,
) -> u64 {
    let declared = schema.get(predicate).expect("a declared predicate");

    let key = a_value_of(declared.key().ty, schema, draws, written, out);
    let value = declared
        .value()
        .map(|side| a_value_of(side.ty, schema, draws, written, out));

    let at = (predicate.0, key.to_string());
    if let Some(already) = written.by_key.get(&at) {
        return *already;
    }

    written.next += 1;
    let id = written.next;
    written.by_key.insert(at, id);

    let name = declared.name().unwrap_or_default();
    let mut line = format!(
        "{{\"id\":{id},\"predicate\":{},\"fact\":{key}",
        serde_json::Value::from(name)
    );
    if let Some(value) = value {
        line.push_str(&format!(",\"value\":{value}"));
    }
    line.push_str("}\n");
    out.push_str(&line);

    id
}

/// A well-typed JSON value for `ty`, emitting a line for every reference it holds.
fn a_value_of(
    ty: &PredicateTy,
    schema: &Schema,
    draws: &mut Draws,
    written: &mut Written,
    out: &mut String,
) -> serde_json::Value {
    match ty {
        PredicateTy::Int => serde_json::Value::from(draws.int()),
        PredicateTy::Str => serde_json::Value::from(draws.text()),

        // The bytes a string cannot hold, rendered the way the format renders them.
        PredicateTy::Bytes => {
            let text = draws.text();
            let mut bytes = text.into_bytes();
            bytes.push(0xff);
            let hex: String = bytes.iter().map(|byte| format!("{byte:02x}")).collect();
            serde_json::Value::from(hex)
        }

        PredicateTy::Fact(target) => {
            serde_json::Value::from(a_line_of(*target, schema, draws, written, out))
        }

        PredicateTy::Record(fields) => serde_json::Value::Object(
            fields
                .iter()
                .map(|(name, field)| {
                    let name = schema
                        .interner()
                        .resolve(*name)
                        .expect("a schema names its fields")
                        .to_owned();
                    (name, a_value_of(field, schema, draws, written, out))
                })
                .collect(),
        ),

        PredicateTy::Union(alternatives) => {
            let chosen = &alternatives[draws.pick() % alternatives.len()];
            let name = schema
                .interner()
                .resolve(chosen.name)
                .expect("a schema names its alternatives")
                .to_owned();

            serde_json::Value::Object(
                [(name, a_value_of(&chosen.ty, schema, draws, written, out))]
                    .into_iter()
                    .collect(),
            )
        }
    }
}

/// The same schema with its predicates **renamed so their ids come out reversed**.
///
/// **Because a generator's references only ever point backwards.**
/// [`fjord_wire::value::proptest`] resolves a reference modulo the predicate's own
/// index, which it has to: the fact it draws nests its targets, and a forward reference
/// would let that recurse forever. The side effect is that a predicate's id is always
/// above everything it names — so an exporter that emitted rows in plain predicate order
/// would pass every generated case, and the walk that puts targets first would be dead
/// code the battery never touched.
///
/// **Renamed rather than reordered, because declaration order is not what decides an
/// id.** Lowering sorts predicates by name, so moving the lines about changes nothing;
/// `gen.P0` is id 0 wherever it is written. Mapping the `i`th name to `Z{n-1-i}` puts
/// the sort in the opposite order while leaving the reference graph exactly as it was,
/// and every reference then points at a *higher* id — the case the walk exists for.
/// Sigla allows it, and `codemarkup` and `csharp` are full of it.
///
/// Textual because a [`Schema`] cannot be rebuilt from outside `fjord-schema`: its
/// constructor wants the interner's `RodeoReader`, and a schema only lends out the
/// wrapper. Re-reading the source is also the more honest transform — the parser and the
/// lowerer see the forward references too.
#[must_use]
pub fn with_the_predicate_order_reversed(source: &str) -> String {
    let declared: Vec<String> = source
        .lines()
        .filter_map(|line| {
            let rest = line.trim_start().strip_prefix("predicate ")?;
            let name: String = rest
                .chars()
                .take_while(|c| c.is_alphanumeric() || *c == '_')
                .collect();
            (!name.is_empty()).then_some(name)
        })
        .collect();

    // `Z` because nothing the generator writes starts with one, so a renamed predicate
    // cannot collide with an original and the pass needs no second phase.
    let mut out = source.to_owned();
    for (index, name) in declared.iter().enumerate() {
        let to = format!("Z{}", declared.len() - 1 - index);
        out = replace_word(&out, name, &to);
    }
    out
}

/// Replace whole-word occurrences of `what` with `to`.
///
/// Whole-word, because `P1` is a prefix of `P10` and a plain substring replace would
/// rename one predicate into another's name — which the schema would still lower, into
/// the wrong graph, silently.
fn replace_word(text: &str, what: &str, to: &str) -> String {
    let mut out = String::with_capacity(text.len());
    let mut rest = text;

    while let Some(at) = rest.find(what) {
        let before = rest[..at].chars().next_back();
        let after = rest[at + what.len()..].chars().next();

        let boundary = |c: Option<char>| !matches!(c, Some(c) if c.is_alphanumeric() || c == '_');

        out.push_str(&rest[..at]);
        if boundary(before) && boundary(after) {
            out.push_str(to);
        } else {
            out.push_str(what);
        }
        rest = &rest[at + what.len()..];
    }

    out.push_str(rest);
    out
}
