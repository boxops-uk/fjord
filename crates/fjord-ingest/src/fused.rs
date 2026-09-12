//! **Wire bytes to storage bytes in one pass** — what a write stream calls.
//!
//! The ordinary write path goes through three representations to turn a block into
//! rows: the wire's bytes become a [`WireFact`](fjord_wire::WireFact) tree that owns a
//! `String` per string field, that tree becomes a
//! [`Value`](fjord_encoding::tuple::Value) tree that owns them again, and that becomes
//! the storage bytes. Only the third is kept. Measured on a 2,000-fact block, decoding
//! alone allocates **seven times the bytes it is reading**, all of it dropped before
//! the fact is written.
//!
//! This walks the wire and the declared type together, writing storage bytes straight
//! into a reused buffer. It allocates nothing per fact after the first, because
//! [`FactSink::resolve_or_create`] takes `&[u8]` — the store never wanted an owned
//! `Vec`, only somewhere to read from.
//!
//! # What forces the shape
//!
//! **A nested reference is why this is not simply streaming.** A parent's key holds the
//! `FactId` of its target, and that id does not exist until the target has been
//! interned — so the walk is depth first, and a child's bytes have to be built
//! somewhere while the parent's buffer is half-written. A pool of buffers, one taken
//! per level and returned on the way out, is what makes that free: the deepest chain a
//! peer may send is bounded ([`fjord_wire::value::MAX_NESTED_DEPTH`]), so the pool
//! reaches its size and stays there.
//!
//! **A key's top-level record is flat.** `encode_key` writes a key's fields back to
//! back with no record wrapper, and only nested records are framed — so the top level
//! is walked field by field and everything below it goes through
//! [`TupleEncoder::record`](fjord_encoding::tuple::TupleEncoder::record).
//!
//! # What it owes
//!
//! It does no type checking [`intern_block`](crate::intern_block) does not, and it
//! reports the same errors — including the one a buffer-writing path is most likely to
//! forget, [`refuse_over_long_key`](fjord_encoding::tuple::refuse_over_long_key), since the backend answers an over-long key with an
//! `assert!` that poisons the merge lock behind it rather than with a `Result`.
//!
//! That "the same" is checked rather than intended: `fused_agrees` and
//! `fused_agrees_always` run both walks over the same block into two stores and compare
//! every row byte for byte, refusals included. Two paths that must agree and nothing
//! comparing them is what produced the hex/base64 bug this crate's history records.

use fjord_encoding::tuple::{TupleEncoder, refuse_over_long_key};
use fjord_schema::{
    id::FactId,
    schema::{PredicateId, PredicateTy, Schema},
};
use fjord_wire::{WireError, block, varint};

use crate::{error::IngestError, intern::Ingested, sink::FactSink};

/// Buffers the walk reuses, one per level of nesting.
///
/// Held by the caller so that a run of blocks pays for them once. Empty is a valid
/// start: the pool fills to the depth the data actually reaches.
#[derive(Debug, Default)]
pub struct Scratch {
    pool: Vec<Vec<u8>>,
}

impl Scratch {
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// How many buffers the pool grew to — the nesting depth the data reached, times
    /// the two buffers a fact needs.
    #[must_use]
    pub fn buffers(&self) -> usize {
        self.pool.len()
    }

    fn take(&mut self) -> Vec<u8> {
        let mut buffer = self.pool.pop().unwrap_or_default();
        buffer.clear();
        buffer
    }

    fn give(&mut self, buffer: Vec<u8>) {
        self.pool.push(buffer);
    }
}

/// Ingest a block, writing storage bytes straight from the wire.
///
/// # Errors
///
/// As [`intern_block`](crate::intern::intern_block): a predicate the schema does not
/// declare, bytes that do not fit the declared type, or whatever the sink reports.
pub fn fuse_block<S: FactSink>(
    sink: &S,
    schema: &Schema,
    bytes: &[u8],
    scratch: &mut Scratch,
) -> Result<Ingested, IngestError> {
    let header = block::decode_header(bytes)?;

    let predicate = schema
        .find_position(header.predicate)
        .map(|(id, _)| id)
        .ok_or_else(|| {
            IngestError::Wire(WireError::UnknownPredicateName(header.predicate.to_owned()))
        })?;

    let name_len = header.predicate.len();
    let start = block::OVERHEAD + name_len;
    let payload = &bytes[start..start + header.length as usize];

    let mut counts = Ingested::default();
    let mut at = 0;

    for _ in 0..header.count {
        let (id, used) = fuse_fact(
            sink,
            schema,
            predicate,
            &payload[at..],
            scratch,
            &mut counts,
        )?;
        counts.ids.push(id);
        at += used;
    }

    if at != payload.len() {
        return Err(IngestError::Wire(WireError::TrailingBytes(
            payload.len() - at,
        )));
    }

    Ok(counts)
}

/// One fact: its key and value written into buffers, then interned. Answers the id it
/// took and how many wire bytes it consumed.
fn fuse_fact<S: FactSink>(
    sink: &S,
    schema: &Schema,
    predicate: PredicateId,
    wire: &[u8],
    scratch: &mut Scratch,
    counts: &mut Ingested,
) -> Result<(FactId, usize), IngestError> {
    let declared = schema
        .get(predicate)
        .ok_or(IngestError::UnknownPredicate(predicate.0))?
        .predicate()
        .clone();

    let mut key = scratch.take();
    let mut value = scratch.take();

    let outcome = (|| -> Result<(FactId, usize), IngestError> {
        let mut at = 0;

        // **The top level of a key is flat** — fields back to back, no record wrapper,
        // which is what `encode_key` writes and therefore what a row is keyed by.
        {
            let mut enc = TupleEncoder::new(&mut key);
            at += fuse_top(
                sink,
                schema,
                &declared.key,
                &wire[at..],
                &mut enc,
                scratch,
                counts,
            )?;
        }

        // **And a value's top level is not flat**, which is the asymmetry that cost
        // this prototype its first differential run. `encode_key` writes a key's
        // fields back to back so a prefix seek can reach them; `encode_typed` writes
        // the value whole, record wrapper and all, because nothing seeks into it.
        if let Some(value_ty) = &declared.value {
            let mut enc = TupleEncoder::new(&mut value);
            at += fuse_value(
                sink,
                schema,
                value_ty,
                &wire[at..],
                &mut enc,
                scratch,
                counts,
            )?;
        }

        // **The same refusal `encode_key` makes, and for the same reason.** Writing
        // into a buffer skips the function that used to carry this check, and the
        // backend answers an over-long key with an `assert!` — which panics a write
        // worker and poisons the merge lock behind it, taking the database out of
        // service. Every fact passes here, nested targets included, since a nested
        // target is keyed in the store exactly like a top-level one.
        refuse_over_long_key(&key).map_err(|why| IngestError::Codec { what: "a key", why })?;

        let keyed_only = declared.value.is_none();
        let interned = sink.resolve_or_create(predicate, &key, &value, keyed_only)?;

        if interned.created {
            counts.created += 1;
        } else {
            counts.deduped += 1;
        }

        Ok((interned.id, at))
    })();

    scratch.give(value);
    scratch.give(key);
    outcome
}

/// A **key's** outermost type: a record is flat here — its fields back to back with no
/// wrapper — and everything else is written as it stands. A value side does not come
/// through here; see [`fuse_fact`].
fn fuse_top<S: FactSink>(
    sink: &S,
    schema: &Schema,
    ty: &PredicateTy,
    wire: &[u8],
    enc: &mut TupleEncoder<'_>,
    scratch: &mut Scratch,
    counts: &mut Ingested,
) -> Result<usize, IngestError> {
    match ty {
        PredicateTy::Record(fields) => {
            let mut at = 0;
            for (_, field) in fields.iter() {
                at += fuse_value(sink, schema, field, &wire[at..], enc, scratch, counts)?;
            }
            Ok(at)
        }
        other => fuse_value(sink, schema, other, wire, enc, scratch, counts),
    }
}

/// One value, wire in and storage bytes out.
fn fuse_value<S: FactSink>(
    sink: &S,
    schema: &Schema,
    ty: &PredicateTy,
    wire: &[u8],
    enc: &mut TupleEncoder<'_>,
    scratch: &mut Scratch,
    counts: &mut Ingested,
) -> Result<usize, IngestError> {
    match ty {
        PredicateTy::Int => {
            let (n, used) = varint::get_i64(wire)?;
            enc.put_i64(n);
            Ok(used)
        }

        // **Borrowed, not owned.** The ordinary path builds a `String` here and drops
        // it a moment later; the encoder only ever needed somewhere to read from.
        PredicateTy::Str => {
            let (payload, used) = blob(wire)?;
            let text = std::str::from_utf8(payload).map_err(|_| WireError::BadString)?;
            enc.put_str(text);
            Ok(used)
        }

        PredicateTy::Bytes => {
            let (payload, used) = blob(wire)?;
            enc.put_bytes(payload);
            Ok(used)
        }

        PredicateTy::Fact(target) => fuse_ref(sink, schema, *target, wire, enc, scratch, counts),

        // Nested records *are* framed, unlike a key's top level.
        PredicateTy::Record(fields) => {
            let mut at = 0;
            enc.record(|inner| {
                for (_, field) in fields.iter() {
                    // The sink and the pool cannot cross this closure, so the walk of a
                    // nested record is done against a second borrow of the same state —
                    // see `fuse_record_fields`.
                    at +=
                        fuse_record_field(sink, schema, field, &wire[at..], inner, scratch, counts)
                            .map_err(|_| fjord_encoding::error::StoreCodecError::BadRecord)?;
                }
                Ok(())
            })
            .map_err(|why| IngestError::Codec {
                what: "a nested record",
                why,
            })?;
            Ok(at)
        }

        PredicateTy::Union(alts) => {
            let (tag, used) = varint::get_u64(wire)?;

            let alt = alts
                .iter()
                .find(|alt| u64::from(alt.disc) == tag)
                .ok_or(WireError::UnknownDiscriminant(tag))?;

            let mut inner_used = 0;
            enc.union(alt.disc, |inner| {
                inner_used =
                    fuse_record_field(sink, schema, &alt.ty, &wire[used..], inner, scratch, counts)
                        .map_err(|_| fjord_encoding::error::StoreCodecError::BadRecord)?;
                Ok(())
            })
            .map_err(|why| IngestError::Codec {
                what: "a union",
                why,
            })?;

            Ok(used + inner_used)
        }
    }
}

/// [`fuse_value`] from inside an encoder closure.
///
/// A separate name because the error has to come back as the codec's, which loses the
/// ingest error's own words — a prototype's compromise, and the first thing a real
/// version would fix by giving `TupleEncoder` a fallible-in-the-caller's-error form.
fn fuse_record_field<S: FactSink>(
    sink: &S,
    schema: &Schema,
    ty: &PredicateTy,
    wire: &[u8],
    enc: &mut TupleEncoder<'_>,
    scratch: &mut Scratch,
    counts: &mut Ingested,
) -> Result<usize, IngestError> {
    fuse_value(sink, schema, ty, wire, enc, scratch, counts)
}

/// A reference: an id written straight through, or a nested fact interned first.
fn fuse_ref<S: FactSink>(
    sink: &S,
    schema: &Schema,
    target: PredicateId,
    wire: &[u8],
    enc: &mut TupleEncoder<'_>,
    scratch: &mut Scratch,
    counts: &mut Ingested,
) -> Result<usize, IngestError> {
    let (form, used) = varint::get_u64(wire)?;

    match form {
        // `REF_ID`
        0 => {
            let (raw, id_used) = varint::get_u64(&wire[used..])?;
            let id = FactId::from_raw(raw);

            if id.predicate() != target {
                return Err(IngestError::Wire(WireError::TypeMismatch(
                    "reference names a different predicate than the field declares",
                )));
            }

            enc.put_fact_id(id);
            Ok(used + id_used)
        }

        // `REF_NESTED` — **the reason this is depth first**: the parent's key cannot be
        // finished until the child has an id, and the child gets one by being interned.
        1 => {
            let (id, nested_used) =
                fuse_fact(sink, schema, target, &wire[used..], scratch, counts)?;
            enc.put_fact_id(id);
            Ok(used + nested_used)
        }

        other => Err(IngestError::Wire(WireError::UnknownRefForm(other))),
    }
}

/// A length-prefixed blob, borrowed from the wire.
///
/// `fjord_wire::value::take_blob` is the same function and is private; this is the
/// prototype not reaching into it.
fn blob(bytes: &[u8]) -> Result<(&[u8], usize), WireError> {
    let (len, used) = varint::get_u64(bytes)?;
    let len = len as usize;

    if len > bytes.len() - used {
        return Err(WireError::LengthOutOfRange {
            declared: len as u64,
            available: bytes.len() - used,
        });
    }

    Ok((&bytes[used..used + len], used + len))
}
