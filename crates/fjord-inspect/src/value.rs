//! A decoded value, as a page shows it.
//!
//! **One thing this does that the codec's own `Serialize` cannot**: a fact
//! reference reads as the fact it names. `Value`'s serialiser writes a
//! `FactRef` as the `u64` it is, which is right for a wire and unreadable in a
//! panel — `1099511627778` is a predicate tag and a sequence packed together,
//! and what a reader wants is `code.File#2`.
//!
//! Not a second codec: nothing here decodes bytes. It renders an *already
//! decoded* `Value`, which is a presentation choice and belongs on the
//! presentation side, in the same way the type renderer does.

use fjord_encoding::tuple::Value;
use fjord_schema::schema::Schema;

/// `value` as JSON, with every reference named.
#[must_use]
pub fn json(value: &Value, schema: &Schema) -> serde_json::Value {
    match value {
        Value::Null => serde_json::Value::Null,
        Value::Int(int) => serde_json::Value::from(*int),
        Value::Str(text) => serde_json::Value::from(text.clone()),

        // **A bare lowercase hex string, untagged.** Not `{"$bytes": …}`: both live
        // renderers hold the type at render time — this one a `&Schema`, the CLI's a
        // `Desc` carrying `TAG_BYTES` — so every consumer that can interpret the field
        // has the schema too. What loses is a reader of *detached* JSON text with no
        // schema, for whom `"00ff"` is indistinguishable from a string whose content
        // happens to be hex; that is stated in the book rather than paid for by every
        // consumer on every row.
        //
        // Not the union precedent: a union's `{"alt": payload}` carries something the
        // schema does not have — which alternative this row took. A bytes tag would
        // carry nothing.
        Value::Bytes(payload) => serde_json::Value::from(hex(payload)),

        // The whole reason this function exists.
        Value::FactRef(id) => serde_json::Value::from(fact(id, schema)),

        Value::Record(fields) => serde_json::Value::Object(
            fields
                .iter()
                .map(|(name, value)| (name.clone(), json(value, schema)))
                .collect(),
        ),

        // A union renders as the one-field object it is written as, which is
        // also how a query matches one.
        Value::Union { alt, value, .. } => {
            serde_json::Value::Object([(alt.clone(), json(value, schema))].into_iter().collect())
        }
    }
}

/// Lowercase hex, two digits a byte. Hex rather than base64 because a byte pair is
/// readable in a terminal and this crate has no base64 dependency.
#[must_use]
pub fn hex(bytes: &[u8]) -> String {
    use std::fmt::Write;

    let mut out = String::with_capacity(bytes.len() * 2);
    for byte in bytes {
        let _ = write!(&mut out, "{byte:02x}");
    }
    out
}

/// A fact's identity as a reader writes it: `code.File#2`.
#[must_use]
pub fn fact(id: &fjord_schema::id::FactId, schema: &Schema) -> String {
    let name = schema
        .get(id.predicate())
        .and_then(|predicate| predicate.name())
        .unwrap_or(crate::lowered::UNRESOLVED)
        .to_owned();

    format!("{name}#{}", id.sequence())
}
