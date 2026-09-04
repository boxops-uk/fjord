//! **Blocks** — the unit a fact travels in, on a socket and on disk alike.
//!
//! One block is a run of facts of *one predicate*, so the predicate id is paid once
//! rather than per fact. An indexer writing in visitation order emits small blocks in
//! bursts; a writer that has grouped its output emits large ones
//! ([operations §8](https://github.com/boxops-uk/fjord/blob/main/website/content/operations.md)). The same bytes are a
//! `CopyData` frame's payload on the wire and a run of a fact file on disk, which is
//! what makes "one fact encoding, not two" a thing that can be checked.
//!
//! ```text
//!   [sync: FF × 10][magic "FJBK"][name_len u32][count u32][length u32][crc32 u32][name][payload]
//!    └ 10 B         └────────────────── header, 20 B ─────────────────┘  └ name_len  └ length B
//! ```
//!
//! # The predicate is named, not numbered
//!
//! A header carrying the *database's* predicate id would make a fact file meaningful only
//! against the database whose numbering produced it, and would make every client keep a
//! table of ids in step with a server's. A name costs about six more
//! bytes **once per block**, against payloads of hundreds to thousands of facts, and buys
//! both back: a client never learns a database's numbering, and a file is portable to any
//! database whose schema declares those names.
//!
//! It is also a better failure. A wrong id decoded the payload as some other predicate's
//! shape, silently; a name that is not there is [`WireError::UnknownPredicateName`], before
//! a byte of payload is trusted.
//!
//! The name sits **after** the fixed-width fields on purpose — see below, a splitter must
//! reach `length` at a fixed offset — and cannot contribute to a sync marker for the reason
//! a string cannot: it is UTF-8, and UTF-8 never uses `0xF8`–`0xFF`.
//!
//! # A marker is a candidate, and [`bytes`](crate::value::WireValue::Bytes) is why
//!
//! A fact file must be splittable at an arbitrary offset — seek anywhere, scan
//! forward to the next block, hand the rest to a worker — which is the property
//! Glean's opaque sequential `Batch` cannot offer and the reason this format has a
//! marker at all.
//!
//! [Operations §8](https://github.com/boxops-uk/fjord/blob/main/website/content/operations.md) specifies the marker as "a
//! reserved, structurally-illegal byte sequence (unused type-tag run the encoder
//! never emits)", and describes every hit as *only a candidate* because "values
//! carry arbitrary bytes (blobs/source text), so a marker can occur inside one".
//! The first half needs amending for this codec; **the second half is exactly
//! right, and holds here for the reason it gives**:
//!
//! - There are no type tags to reserve a run of. The value encoding is
//!   [schema-driven](crate::value) and emits none.
//! - A `bytes` payload is a length varint and then the bytes, raw — not validating
//!   them is precisely what the family is for — so ten `0xFF` inside one are
//!   ordinary data. **No fixed marker can be structurally impossible in a family
//!   that carries arbitrary bytes.** Escaping the payload would buy the
//!   impossibility back and cost what this module exists for: the same bytes are a
//!   `CopyData` frame's payload and a run of a fact file, which is what makes "one
//!   fact encoding, not two" checkable.
//!
//! What the encoding does buy is that a *false* candidate is rare rather than
//! routine, and each part of that is still true:
//!
//!   1. **Strings contribute no `0xFF` at all.** A string is length-prefixed UTF-8,
//!      and UTF-8 never uses `0xF8`–`0xFF` in any position.
//!   2. **A varint contributes at most nine.** Continuation bytes are `0x80`–`0xFF`
//!      and the final byte is below `0x80`, so a run ends where the varint does; the
//!      longest possible is `u64::MAX`, which is `FF` nine times and then `01`.
//!   3. **Runs cannot join across values.** A varint's last byte is below `0x80`,
//!      so it is never `0xFF`, and a string's bytes never are.
//!   4. **The header cannot contribute one.** `name_len`, `count` and `length` are
//!      capped to keep their top bytes zero, so the only field free to be all-ones
//!      is the four-byte checksum, and four is not ten.
//!
//! So a marker inside a block came from a `bytes` field or from nothing, and a
//! splitter calls [`find_block`] rather than [`find_sync`]: it confirms the magic
//! and the header checksum, and on failure **resumes scanning past that candidate**
//! instead of giving up. That puts an *accidental* false boundary at roughly 2⁻³².
//! It does not defeat a crafted one — a producer can write a correct CRC inside a
//! blob, and a checksum is not a signature — which is a limit of scanning, not a
//! fault to fix here.
//!
//! **Resuming has a price, and it is data loss.** A damaged block and a false
//! candidate are the same bytes to a scan — a marker whose header does not validate
//! — so scanning past one means scanning past the other. A block whose payload lost
//! a bit is skipped, and the next *valid* block is returned in its place: a
//! corruption [`decode_block`] would have reported as
//! [`WireError::ChecksumMismatch`] becomes a file that quietly holds fewer facts.
//! [`find_block`] therefore returns a [`Scan`], which carries the boundary and the
//! skipped candidate both, and a caller that reads only [`Scan::block`] is choosing
//! the silent answer.
//!
//! # A reader that can start at offset 0 should not scan at all
//!
//! Scanning is for **recovery**: a file cut mid-block, a flipped bit, a worker
//! handed a byte range that begins in the middle of a block. It is not how a whole
//! file is read. The header is fixed-width and [`decode_block`] already returns
//! `OVERHEAD + name_len + length` as its consumed count, so walking that chain from
//! offset 0 gives **exact** boundaries with no scanning and no guessing — no payload
//! byte is ever weighed as a boundary at all. A parallel ingest should walk the
//! chain to build its split points and hand workers exact offsets.
//!
//! # Fixed-width fields, and little-endian
//!
//! The header is fixed-width where the payload is varints, because a splitter has to
//! read `length` *before* it can trust anything else — a variable-width field would
//! have to be parsed to be skipped, and the whole point is to skip. Little-endian
//! because there is nothing to order: the storage codec's big-endian is an
//! [I1](https://github.com/boxops-uk/fjord/blob/main/website/content/invariants.md#i1) requirement, and this is the file where that
//! requirement is not inherited.

use fjord_schema::schema::{PredicateId, Schema};

use crate::{
    crc,
    error::WireError,
    value::{WireFact, decode_fact, encode_fact},
};

/// The resynchronisation marker: ten `0xFF` bytes, one longer than the longest run
/// a varint can make. A [`bytes`](crate::value::WireValue::Bytes) payload can hold
/// one anyway — see the module docs, and use [`find_block`] to scan.
pub const SYNC: [u8; 10] = [0xFF; 10];

/// Identifies a block header, so a candidate is rejected in four bytes before its
/// checksum is computed.
pub const MAGIC: [u8; 4] = *b"FJBK";

/// Bytes of header after the marker: magic, name length, count, length, checksum.
pub const HEADER_LEN: usize = MAGIC.len() + 4 + 4 + 4 + 4;

/// Total framing per block.
pub const OVERHEAD: usize = SYNC.len() + HEADER_LEN;

/// The most facts one block may declare.
///
/// Chosen to keep the field's top byte zero, so `count` can never contribute to a
/// run of `0xFF` — a header that could forge a marker inside itself would make a
/// scan guess at every block, not merely at the ones carrying a blob.
pub const MAX_FACTS: u32 = 0x00FF_FFFF;

/// The most payload bytes one block may carry (64 MiB), capped for the same reason
/// and one more: a `length` read off a socket sizes an allocation, and a peer is not
/// to be trusted with that.
pub const MAX_PAYLOAD: u32 = 0x0400_0000;

/// The longest predicate name a block may carry.
///
/// Capped for the same reason `count` and `length` are: the top two bytes stay zero, so the
/// field cannot contribute to a run of `0xFF`. A fully-qualified predicate name is a few
/// dozen bytes, so this is a bound on absurdity rather than a limit anyone meets.
pub const MAX_NAME: u32 = 0x0000_FFFF;

/// What a block says about itself, before its facts are decoded.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct BlockHeader<'a> {
    /// The predicate's fully-qualified name, borrowed from the block's own bytes.
    pub predicate: &'a str,
    pub count: u32,
    /// Payload bytes, *not* counting the name — so a block occupies
    /// `OVERHEAD + name.len() + length`.
    pub length: u32,
}

/// Append a block carrying `facts`, all of predicate `predicate`.
///
/// # Errors
///
/// [`WireError::TypeMismatch`] if a fact is of another predicate — the block header
/// names the predicate once, so a stray fact would be decoded as the wrong shape at
/// the far end rather than rejected.
pub fn encode_block(
    out: &mut Vec<u8>,
    schema: &Schema,
    predicate: PredicateId,
    facts: &[WireFact],
) -> Result<(), WireError> {
    let count = u32::try_from(facts.len()).ok().filter(|n| *n <= MAX_FACTS);
    let Some(count) = count else {
        return Err(WireError::BlockTooLarge {
            what: "facts",
            declared: facts.len() as u64,
            max: u64::from(MAX_FACTS),
        });
    };

    // The name is resolved here, from the schema this call already takes, so a caller
    // still speaks its own `PredicateId` and only the *wire* carries a name.
    let name = schema
        .get(predicate)
        .and_then(|p| p.name())
        .ok_or(WireError::UnknownPredicate(predicate.0))?;

    let name_len = u32::try_from(name.len()).ok().filter(|n| *n <= MAX_NAME);
    let Some(name_len) = name_len else {
        return Err(WireError::BlockTooLarge {
            what: "predicate name bytes",
            declared: name.len() as u64,
            max: u64::from(MAX_NAME),
        });
    };

    let mut payload = vec![];
    for fact in facts {
        if fact.predicate != predicate {
            return Err(WireError::TypeMismatch(
                "a block carries one predicate, and this fact is of another",
            ));
        }
        encode_fact(&mut payload, schema, fact)?;
    }

    let length = u32::try_from(payload.len())
        .ok()
        .filter(|n| *n <= MAX_PAYLOAD);
    let Some(length) = length else {
        return Err(WireError::BlockTooLarge {
            what: "payload bytes",
            declared: payload.len() as u64,
            max: u64::from(MAX_PAYLOAD),
        });
    };

    out.extend_from_slice(&SYNC);

    // The checksum covers the header's own fields as well as the payload, so a
    // corrupted `count` or `length` is caught rather than trusted — which matters
    // because `length` is what a splitter uses to skip.
    let mut header = Vec::with_capacity(HEADER_LEN - 4);
    header.extend_from_slice(&MAGIC);
    header.extend_from_slice(&name_len.to_le_bytes());
    header.extend_from_slice(&count.to_le_bytes());
    header.extend_from_slice(&length.to_le_bytes());

    // Name as well as payload: it is as load-bearing as either, and a corrupted one
    // would otherwise resolve to a different predicate or to none.
    let checksum = crc::finish(crc::update(
        crc::update(crc::update(crc::start(), &header), name.as_bytes()),
        &payload,
    ));

    out.extend_from_slice(&header);
    out.extend_from_slice(&checksum.to_le_bytes());
    out.extend_from_slice(name.as_bytes());
    out.extend_from_slice(&payload);

    Ok(())
}

/// Read the header of the block starting at `bytes[0]`, without decoding its facts.
///
/// What a splitter calls: it validates enough to trust `length`, then skips.
pub fn decode_header(bytes: &[u8]) -> Result<BlockHeader<'_>, WireError> {
    if bytes.len() < OVERHEAD {
        return Err(WireError::UnexpectedEof);
    }

    if bytes[..SYNC.len()] != SYNC {
        return Err(WireError::NoSyncMarker);
    }

    let header = &bytes[SYNC.len()..OVERHEAD];
    if header[..MAGIC.len()] != MAGIC {
        return Err(WireError::BadMagic);
    }

    let field = |at: usize| -> u32 {
        let start = MAGIC.len() + at * 4;
        u32::from_le_bytes(header[start..start + 4].try_into().expect("four bytes"))
    };

    let (name_len, count, length, declared_crc) = (field(0), field(1), field(2), field(3));

    if name_len > MAX_NAME {
        return Err(WireError::BlockTooLarge {
            what: "predicate name bytes",
            declared: u64::from(name_len),
            max: u64::from(MAX_NAME),
        });
    }

    if count > MAX_FACTS {
        return Err(WireError::BlockTooLarge {
            what: "facts",
            declared: u64::from(count),
            max: u64::from(MAX_FACTS),
        });
    }

    if length > MAX_PAYLOAD {
        return Err(WireError::BlockTooLarge {
            what: "payload bytes",
            declared: u64::from(length),
            max: u64::from(MAX_PAYLOAD),
        });
    }

    let name_at = OVERHEAD;
    let payload_at = name_at + name_len as usize;
    let payload_end = payload_at + length as usize;

    if payload_end > bytes.len() {
        return Err(WireError::LengthOutOfRange {
            declared: u64::from(name_len) + u64::from(length),
            available: bytes.len() - name_at,
        });
    }

    let name =
        std::str::from_utf8(&bytes[name_at..payload_at]).map_err(|_| WireError::BadString)?;

    // Header, then name, then payload — the same order and the same bytes
    // `encode_block` folded, minus the checksum field itself.
    let checksum = crc::finish(crc::update(
        crc::update(
            crc::update(crc::start(), &header[..MAGIC.len() + 12]),
            name.as_bytes(),
        ),
        &bytes[payload_at..payload_end],
    ));

    if checksum != declared_crc {
        return Err(WireError::ChecksumMismatch {
            declared: declared_crc,
            computed: checksum,
        });
    }

    Ok(BlockHeader {
        predicate: name,
        count,
        length,
    })
}

/// Decode a whole block, returning its facts and the bytes it occupied.
pub fn decode_block(bytes: &[u8], schema: &Schema) -> Result<(Vec<WireFact>, usize), WireError> {
    let header = decode_header(bytes)?;

    // Name → *this* reader's id. Two databases may number a predicate differently and
    // neither has to care, which is the whole point of naming it on the wire.
    let predicate = schema
        .find_position(header.predicate)
        .map(|(id, _)| id)
        .ok_or_else(|| WireError::UnknownPredicateName(header.predicate.to_owned()))?;

    let name_len = header.predicate.len();
    let payload = &bytes[OVERHEAD + name_len..OVERHEAD + name_len + header.length as usize];
    let mut facts = Vec::with_capacity(header.count.min(4096) as usize);
    let mut at = 0;

    for _ in 0..header.count {
        let (fact, used) = decode_fact(&payload[at..], schema, predicate)?;
        facts.push(fact);
        at += used;
    }

    // The count and the length have to agree with each other and with the facts. A
    // block whose payload is longer than its facts is not a block with slack in it —
    // it is a block whose header and body disagree, which means one of them is a
    // different block's.
    if at != payload.len() {
        return Err(WireError::TrailingBytes(payload.len() - at));
    }

    Ok((facts, OVERHEAD + name_len + header.length as usize))
}

/// The offset of the next marker at or after `from`, or `None`.
///
/// `memchr` finds the marker's first byte at memory bandwidth and the run is
/// confirmed behind it — the "SIMD `memchr`-style scan" operations §8 calls for.
///
/// A hit is a **candidate**, not a block: this deliberately does not validate the
/// header, because the two failures want telling apart. A candidate can be a
/// `bytes` payload's own data rather than a boundary (see the module docs), so a
/// splitter wants [`find_block`], which validates and scans on; this is the raw
/// scan under it.
#[must_use]
pub fn find_sync(haystack: &[u8], from: usize) -> Option<usize> {
    let mut at = from;

    while at < haystack.len() {
        let found = memchr::memchr(SYNC[0], &haystack[at..])? + at;

        if haystack.len() - found >= SYNC.len() && haystack[found..found + SYNC.len()] == SYNC {
            return Some(found);
        }

        at = found + 1;
    }

    None
}

/// What [`find_block`] found: the next boundary, **and** what it had to skip to
/// reach it.
///
/// The two fields are independent, and reading only `block` is how a scan turns a
/// reported corruption into silent data loss — see [`find_block`].
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct Scan {
    /// The offset of the next validated block at or after `from`, or `None`.
    pub block: Option<usize>,
    /// The first candidate that carried [`MAGIC`] and then failed validation, with
    /// the failure. `Some` means bytes shaped like a block were skipped, and a scan
    /// cannot say whether they were a damaged block or a blob's contents.
    pub damaged: Option<(usize, WireError)>,
}

/// Scan for the next **validated** block at or after `from`.
///
/// The splitter's primitive: seek anywhere in a file, call this, and start reading
/// whole blocks from [`Scan::block`]. A candidate is confirmed by [`MAGIC`] and then
/// by the header checksum, and a candidate that fails is **scanned past** rather
/// than surrendered to — which is the whole difference from [`find_sync`], and is
/// required because a [`bytes`](crate::value::WireValue::Bytes) payload can hold a
/// marker.
///
/// # Scanning past a failure can skip a real block, and [`Scan::damaged`] is the
/// only warning
///
/// A scan cannot distinguish a *damaged block* from a *false candidate*: both are a
/// marker whose header does not validate, and the bytes say nothing about which. So
/// resuming past a failure is the only behaviour that recovers a file at all — a
/// blob's own marker must not stop the scan — and it costs this: a block whose
/// payload lost a bit is skipped, and the next validated block is returned in its
/// place. Reading `block` alone therefore converts a corruption that
/// [`decode_block`] would have *reported* as [`WireError::ChecksumMismatch`] into a
/// file that quietly holds fewer facts. **A caller that reads `block` must read
/// `damaged` too**, and treat it as the ambiguity it is: probable damage, not proven
/// damage.
///
/// Only the first such candidate is kept, and only one past [`MAGIC`] — a blob of
/// `0xFF` would otherwise report a candidate per byte. A candidate rejected on the
/// magic is ordinary blob content and is not reported.
///
/// # What this defeats, and what it does not
///
/// Magic and a CRC32 put an *accidental* false boundary at roughly 2⁻³², and the
/// caps on `name_len`, `count` and `length` reject most candidates before a
/// checksum is ever computed. A **crafted** one is not defeated: a producer that
/// writes a well-formed header, correct checksum and all, inside a blob gets a
/// boundary here, because a checksum is not a signature. Scanning recovers a
/// damaged file; it does not parse a hostile one. A caller that can read from
/// offset 0 should walk the header chain instead — see the module docs; it never
/// weighs a payload byte as a boundary, so neither ambiguity arises.
///
/// A block whose declared bytes run past the end of `haystack` is not a hit: the
/// checksum cannot be computed without them. A worker handed a byte range therefore
/// finds the boundaries it can read whole, and the truncated tail belongs to the
/// next range.
///
/// **How that tail is reported turns on how much of the header the range holds**, and
/// the quiet case is the one to design around. With the header complete, the tail is
/// the benign instance of `damaged`, carrying [`WireError::LengthOutOfRange`] rather
/// than a checksum failure. With the range ending *inside* the header — fewer than
/// [`OVERHEAD`] bytes past the marker — there is no header yet to disbelieve, so the
/// scan reports neither a block nor damage and answers `Scan { block: None, damaged:
/// None }`: the same answer it gives for a range holding no block at all. Nothing
/// distinguishes them, and nothing can, which is why a caller **splitting a file into
/// ranges must overlap them by at least [`OVERHEAD`] bytes**. Ranges that meet exactly
/// lose a block whose marker and header straddle the join: the first range cannot
/// validate it and the second begins past its marker, so neither range finds it and
/// neither reports anything missing.
#[must_use]
pub fn find_block(haystack: &[u8], from: usize) -> Scan {
    let mut at = from;
    let mut damaged = None;

    while let Some(candidate) = find_sync(haystack, at) {
        match decode_header(&haystack[candidate..]) {
            Ok(_) => {
                return Scan {
                    block: Some(candidate),
                    damaged,
                };
            }
            // Neither is the shape of a block: `BadMagic` is ordinary blob content,
            // and `UnexpectedEof` is a marker at the very end of the range.
            Err(WireError::BadMagic | WireError::UnexpectedEof | WireError::NoSyncMarker) => {}
            Err(why) => {
                if damaged.is_none() {
                    damaged = Some((candidate, why));
                }
            }
        }

        // Past the candidate's *first byte*, not past the marker. A payload ending
        // in `0xFF` runs into the next block's marker and makes one long run, so the
        // real boundary can start inside the candidate that just failed; skipping
        // `SYNC.len()` would step over it and lose every block after.
        at = candidate + 1;
    }

    Scan {
        block: None,
        damaged,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        value::{
            WireValue,
            proptest::{SchemaAndFact, arb_schema_and_fact},
        },
        varint,
    };
    use ::proptest::prelude::*;
    use fjord_schema::schema::{Predicate, PredicateTy};
    use lasso::Rodeo;
    use std::sync::Arc;

    /// The longest run of `0xFF` anywhere in `bytes`.
    fn longest_ff_run(bytes: &[u8]) -> usize {
        let (mut best, mut run) = (0, 0);
        for &byte in bytes {
            run = if byte == 0xFF { run + 1 } else { 0 };
            best = best.max(run);
        }
        best
    }

    fn blocked(spec: &SchemaAndFact) -> (fjord_schema::schema::Schema, Vec<u8>, Vec<WireFact>) {
        let schema = spec.schema();
        let fact = spec.fact(&schema);
        let facts = vec![fact.clone(), fact];

        let mut out = vec![];
        encode_block(&mut out, &schema, facts[0].predicate, &facts).expect("a well-typed block");

        (schema, out, facts)
    }

    /// **A payload can contain the marker**, and the generator reaches the case —
    /// which is the population assertion every splitter property below rests on.
    ///
    /// A `bytes` field is written raw, so ten `0xFF` inside one are data. The three
    /// cases in the module docs (UTF-8 emits no `0xFF`, a varint's last byte is
    /// below `0x80` so runs cannot join, nine is the longest run one varint makes)
    /// were exhaustive before that family existed and are still why a *false*
    /// candidate stays rare; they are no longer an impossibility proof. A generator
    /// drawing only UTF-8 text reaches a run of one, so the case is injected rather
    /// than hoped for — and asserting the draw is present is what stops these
    /// properties passing vacuously again.
    #[test]
    fn a_payload_can_contain_the_marker() {
        use ::proptest::{
            strategy::{Strategy, ValueTree},
            test_runner::TestRunner,
        };

        const RUNS: usize = 400;

        let mut runner = TestRunner::deterministic();
        let mut worst = 0;
        let mut carrying = 0;

        for _ in 0..RUNS {
            let spec = arb_schema_and_fact()
                .new_tree(&mut runner)
                .unwrap()
                .current();
            let schema = spec.schema();
            let fact = spec.fact(&schema);

            let payload = crate::value::to_bytes(&schema, &fact).expect("encodes");
            let run = longest_ff_run(&payload);

            worst = worst.max(run);
            carrying += usize::from(run >= SYNC.len());
        }

        assert!(
            carrying >= POPULATION_FLOOR,
            "only {carrying} of {RUNS} generated payloads held a marker (longest run \
             {worst}); the draws cannot exercise the splitter, so the properties below \
             are vacuous"
        );
    }

    /// How many of a battery's draws must carry a marker in their payload.
    ///
    /// A floor, not the measurement — the counts are several times this. A generator
    /// change that dropped one to a handful would leave the splitter properties
    /// nearly as vacuous as no marker at all, and nothing else would notice.
    const POPULATION_FLOOR: usize = 20;

    /// The bound on a **varint** is nine, not "small enough" — and `u64::MAX` is what
    /// reaches it, so the marker's length is one more than that worst case rather
    /// than a round number someone picked. A `bytes` payload is bounded by neither;
    /// see [`a_payload_can_contain_the_marker`].
    #[test]
    fn the_longest_run_a_varint_can_reach_is_nine() {
        let mut out = vec![];
        varint::put_u64(&mut out, u64::MAX);
        assert_eq!(longest_ff_run(&out), 9);
        assert_eq!(SYNC.len(), 10);

        // Two maximal varints back to back still cannot join: the first one's last
        // byte is a terminator, and a terminator is below 0x80.
        let mut pair = vec![];
        varint::put_u64(&mut pair, u64::MAX);
        varint::put_u64(&mut pair, u64::MAX);
        assert_eq!(longest_ff_run(&pair), 9);
    }

    /// **A block can hold several markers and exactly one boundary**, and telling
    /// the two apart is what [`find_block`] is for.
    ///
    /// [`find_sync`] answers candidates: a blob carrying `0xFF` puts one inside the
    /// payload, and a splitter that trusted it would read a fact's bytes as a
    /// header. The header itself still cannot forge one — `name_len`, `count` and
    /// `length` are capped to keep their top bytes zero, and four checksum bytes are
    /// not ten — so a candidate past the block's start came from a value.
    #[test]
    fn only_one_marker_in_a_block_is_a_boundary() {
        use ::proptest::{
            strategy::{Strategy, ValueTree},
            test_runner::TestRunner,
        };

        const RUNS: usize = 400;

        let mut runner = TestRunner::deterministic();
        let mut with_a_candidate_inside = 0;

        for _ in 0..RUNS {
            let spec = arb_schema_and_fact()
                .new_tree(&mut runner)
                .unwrap()
                .current();
            let (_, block, _) = blocked(&spec);

            assert_eq!(find_sync(&block, 0), Some(0));
            assert_eq!(find_block(&block, 0).block, Some(0));
            assert_eq!(
                find_block(&block, 1).block,
                None,
                "a candidate inside a payload was reported as a second boundary"
            );

            with_a_candidate_inside += usize::from(find_sync(&block, 1).is_some());
        }

        assert!(
            with_a_candidate_inside >= POPULATION_FLOOR,
            "only {with_a_candidate_inside} of {RUNS} blocks held a candidate past their \
             own marker, so validation was never the thing under test"
        );
    }

    /// A schema of one predicate keyed by `bytes` — the family that can carry a
    /// marker, and so the only one the scanning tests below have any use for.
    fn blob_schema() -> fjord_schema::schema::Schema {
        let mut rodeo = Rodeo::new();
        let name = rodeo.get_or_intern("gen.P0");

        fjord_schema::schema::Schema::new(
            rodeo.into_reader(),
            Arc::from(vec![Predicate {
                name,
                key: PredicateTy::Bytes,
                value: None,
            }]),
        )
    }

    fn blob_fact(payload: &[u8]) -> WireFact {
        WireFact {
            predicate: PredicateId(0),
            key: WireValue::Bytes(payload.to_vec()),
            value: None,
        }
    }

    /// Three blocks, of which the middle one is damaged by a flipped bit.
    fn a_file_with_a_damaged_middle_block() -> (fjord_schema::schema::Schema, Vec<u8>, Vec<usize>) {
        let schema = blob_schema();

        let mut file = vec![];
        let mut boundaries = vec![];
        for payload in [b"one".as_slice(), b"two", b"three"] {
            boundaries.push(file.len());
            encode_block(&mut file, &schema, PredicateId(0), &[blob_fact(payload)])
                .expect("a block");
        }

        // A bit inside the second block's payload: its header still parses and its
        // caps still pass, so it fails on the checksum and nowhere earlier.
        file[boundaries[1] + OVERHEAD + "gen.P0".len() + 1] ^= 0x01;

        (schema, file, boundaries)
    }

    /// **A damaged block is scanned past, and [`Scan::damaged`] is what stops that
    /// being silent data loss.**
    ///
    /// A flipped bit in a payload leaves a marker and a header that still parse, so
    /// the scan rejects the block's *real* boundary and returns the next one. Read
    /// through [`Scan::block`] alone, a file that [`decode_block`] would have failed
    /// with [`WireError::ChecksumMismatch`] instead yields one block fewer and no
    /// error at all — the corruption becomes a shorter answer. The scan cannot tell
    /// this from a blob whose contents happen to carry a marker, so it reports both
    /// halves and the caller decides.
    #[test]
    fn a_damaged_block_is_skipped_but_reported() {
        let (schema, file, boundaries) = a_file_with_a_damaged_middle_block();

        // The damaged block's boundary is real, and the raw scan still lands on it.
        assert_eq!(find_sync(&file, 1), Some(boundaries[1]));
        assert!(matches!(
            decode_header(&file[boundaries[1]..]),
            Err(WireError::ChecksumMismatch { .. })
        ));

        let scan = find_block(&file, 1);

        // The trade, pinned: the block at `boundaries[1]` is skipped.
        assert_eq!(
            scan.block,
            Some(boundaries[2]),
            "a failed candidate is scanned past, damaged or not"
        );

        // And the trade is reported rather than taken in silence.
        let (at, why) = scan
            .damaged
            .expect("the skipped block is reported, not lost in silence");
        assert_eq!(at, boundaries[1]);
        assert!(matches!(why, WireError::ChecksumMismatch { .. }));

        // The block the scan did return is whole — the loss is the middle one only.
        let (facts, _) = decode_block(&file[boundaries[2]..], &schema).expect("a block decodes");
        assert_eq!(facts, vec![blob_fact(b"three")]);
    }

    /// **A clean file reports no damage**, so `damaged` distinguishes rather than
    /// merely alarms: a `Some` that a whole file also produced would tell a caller
    /// nothing, and the guard above would pass on a constant.
    #[test]
    fn an_undamaged_file_reports_no_damage() {
        let schema = blob_schema();

        let mut file = vec![];
        let mut boundaries = vec![];
        for payload in [b"one".as_slice(), b"two", b"three"] {
            boundaries.push(file.len());
            encode_block(&mut file, &schema, PredicateId(0), &[blob_fact(payload)])
                .expect("a block");
        }

        for from in 0..file.len() {
            let scan = find_block(&file, from);
            assert_eq!(
                scan.block,
                boundaries.iter().copied().find(|b| *b >= from),
                "scanning from offset {from}"
            );
            assert_eq!(scan.damaged, None, "scanning from offset {from}");
        }
    }

    /// **A false candidate is scanned past, not surrendered to** — and the resume
    /// steps one byte, not one marker.
    ///
    /// A payload ending in `0xFF` runs into the next block's marker and makes one
    /// long run, so the real boundary begins *inside* the candidate that just
    /// failed. Resuming at `candidate + SYNC.len()` would step over it and lose
    /// every block after, silently: a scan that finds fewer boundaries reports no
    /// error, it just returns fewer facts.
    #[test]
    fn a_false_candidate_is_scanned_past_one_byte_at_a_time() {
        let schema = blob_schema();

        // Every check but the checksum passes on this one: a marker, the magic, and
        // a header whose three caps are all satisfied by zeros.
        let mut poison = vec![];
        poison.extend_from_slice(&SYNC);
        poison.extend_from_slice(&MAGIC);
        poison.extend_from_slice(&[0x00; 16]);
        // And a tail that merges with the marker of whatever block comes next.
        poison.extend_from_slice(&[0xFF; 3]);

        let mut file = vec![];
        encode_block(&mut file, &schema, PredicateId(0), &[blob_fact(&poison)]).expect("a block");
        let second = file.len();
        encode_block(&mut file, &schema, PredicateId(0), &[blob_fact(b"plain")]).expect("a block");

        // The raw scan is fooled twice over: by the payload's own marker, and by the
        // merged run that starts three bytes before the real boundary.
        let candidate = find_sync(&file, 1).expect("a candidate inside the payload");
        assert!(
            candidate < second,
            "the payload's own marker is not a boundary"
        );
        assert_eq!(
            find_sync(&file, second - 3),
            Some(second - 3),
            "the payload's trailing 0xFF merge with the next block's marker"
        );

        assert_eq!(find_block(&file, 0).block, Some(0));
        assert_eq!(find_block(&file, 1).block, Some(second));
        assert_eq!(find_block(&file, second + 1).block, None);

        // And the boundary it found is a block.
        let (facts, used) = decode_block(&file[second..], &schema).expect("a block decodes");
        assert_eq!(facts, vec![blob_fact(b"plain")]);
        assert_eq!(used, file.len() - second);
    }

    /// **Validation defeats an accidental false boundary, not a crafted one.** A
    /// whole valid block written inside a blob *is* a boundary to a scan, checksum
    /// and all, because a CRC is a checksum and not a signature.
    ///
    /// Pinned rather than hedged in a doc comment: a reader that needs exact
    /// boundaries against a producer it does not trust must walk the header chain
    /// from offset 0, which never weighs a payload byte at all.
    #[test]
    fn a_crafted_block_inside_a_payload_is_indistinguishable_from_a_boundary() {
        let schema = blob_schema();

        let mut smuggled = vec![];
        encode_block(
            &mut smuggled,
            &schema,
            PredicateId(0),
            &[blob_fact(b"inner")],
        )
        .expect("a block");

        let mut file = vec![];
        encode_block(&mut file, &schema, PredicateId(0), &[blob_fact(&smuggled)]).expect("a block");

        let at = find_block(&file, 1)
            .block
            .expect("the crafted header validates");
        assert!(
            at < file.len(),
            "the smuggled block sits inside the outer one"
        );
        assert_eq!(
            decode_block(&file[at..], &schema).map(|(facts, _)| facts),
            Ok(vec![blob_fact(b"inner")])
        );

        // The chain, by contrast, reads one block and lands exactly on the end.
        let (_, used) = decode_block(&file, &schema).expect("the outer block decodes");
        assert_eq!(used, file.len());
    }

    /// A block round-trips, and the splitter finds every boundary in a run of them —
    /// from an arbitrary offset, which is the whole point of a marker.
    #[test]
    fn blocks_round_trip_and_split_from_any_offset() {
        let mut rodeo = Rodeo::new();
        let name = rodeo.get_or_intern("gen.P0");
        let schema = fjord_schema::schema::Schema::new(
            rodeo.into_reader(),
            Arc::from(vec![Predicate {
                name,
                key: PredicateTy::Str,
                value: None,
            }]),
        );

        let block_of = |texts: &[&str]| {
            let facts: Vec<WireFact> = texts
                .iter()
                .map(|t| WireFact {
                    predicate: PredicateId(0),
                    key: WireValue::Str((*t).to_owned()),
                    value: None,
                })
                .collect();
            let mut out = vec![];
            encode_block(&mut out, &schema, PredicateId(0), &facts).expect("a block");
            (out, facts)
        };

        let (a, a_facts) = block_of(&["store/keys.py", "store/codec.py"]);
        let (b, b_facts) = block_of(&["query/plan.py"]);
        let (c, c_facts) = block_of(&[]);

        let mut file = vec![];
        for part in [&a, &b, &c] {
            file.extend_from_slice(part);
        }

        // Read it as a stream of blocks.
        let mut at = 0;
        let mut seen = vec![];
        while at < file.len() {
            let (facts, used) = decode_block(&file[at..], &schema).expect("a block decodes");
            seen.push(facts);
            at += used;
        }
        assert_eq!(seen, vec![a_facts, b_facts, c_facts.clone()]);

        // And find the boundaries from anywhere, which is what a splitter does after
        // seeking blindly into the middle of a file.
        let boundaries: Vec<usize> = vec![0, a.len(), a.len() + b.len()];
        for from in 0..file.len() {
            let expected = boundaries.iter().copied().find(|b| *b >= from);
            assert_eq!(find_sync(&file, from), expected, "scanning from {from}");
        }
    }

    /// An empty block is a block: zero facts, a real header, a real checksum. The
    /// merge emits them, and a reader that treated one as end-of-input would stop
    /// early.
    #[test]
    fn an_empty_block_is_still_a_block() {
        let mut rodeo = Rodeo::new();
        let name = rodeo.get_or_intern("gen.P0");
        let schema = fjord_schema::schema::Schema::new(
            rodeo.into_reader(),
            Arc::from(vec![Predicate {
                name,
                key: PredicateTy::Int,
                value: None,
            }]),
        );

        let mut out = vec![];
        encode_block(&mut out, &schema, PredicateId(0), &[]).expect("an empty block");

        // Framing plus the name it carries — a block names its predicate now, so the
        // floor is `OVERHEAD` and the name rather than `OVERHEAD` alone.
        let framed = OVERHEAD + "gen.P0".len();
        assert_eq!(out.len(), framed);
        assert_eq!(decode_block(&out, &schema), Ok((vec![], framed)));
    }

    /// A fact of another predicate is refused at encode, because the header names
    /// the predicate once: sending it would have the far end decode the fact against
    /// the wrong shape rather than reject it.
    #[test]
    fn a_block_carries_one_predicate() {
        let mut rodeo = Rodeo::new();
        let (p0, p1) = (rodeo.get_or_intern("gen.P0"), rodeo.get_or_intern("gen.P1"));
        let schema = fjord_schema::schema::Schema::new(
            rodeo.into_reader(),
            Arc::from(vec![
                Predicate {
                    name: p0,
                    key: PredicateTy::Int,
                    value: None,
                },
                Predicate {
                    name: p1,
                    key: PredicateTy::Int,
                    value: None,
                },
            ]),
        );

        let stray = WireFact {
            predicate: PredicateId(1),
            key: WireValue::Int(1),
            value: None,
        };

        let mut out = vec![];
        assert!(matches!(
            encode_block(&mut out, &schema, PredicateId(0), &[stray]),
            Err(WireError::TypeMismatch(_))
        ));
    }

    proptest! {
        /// **A block round-trips**, over generated schemas and facts.
        #[test]
        fn a_block_round_trips(spec in arb_schema_and_fact()) {
            let (schema, block, facts) = blocked(&spec);
            prop_assert_eq!(decode_block(&block, &schema), Ok((facts, block.len())));
        }

        /// **Corruption anywhere in a block is caught.** Every byte, every bit — the
        /// checksum covers the header's own fields as well as the payload, so a
        /// corrupted `length` is refused rather than used to skip to the wrong place.
        ///
        /// The marker itself is excluded: flipping a bit there does not corrupt a
        /// block, it *destroys* the boundary, and the splitter's answer is to find
        /// the next one rather than to report a bad block.
        #[test]
        fn a_corrupted_block_never_decodes_as_a_good_one(
            spec in arb_schema_and_fact(),
            bit in 0u8..8,
            offset in 0usize..64,
        ) {
            let (schema, block, facts) = blocked(&spec);
            let at = SYNC.len() + offset % (block.len() - SYNC.len());

            let mut corrupt = block.clone();
            corrupt[at] ^= 1 << bit;

            match decode_block(&corrupt, &schema) {
                Err(_) => {}
                Ok((decoded, _)) => prop_assert_eq!(
                    decoded, facts,
                    "a corrupted block decoded to different facts without an error"
                ),
            }
        }

        /// A block cut short is an error at every cut point, never a shorter block.
        #[test]
        fn a_truncated_block_never_decodes(spec in arb_schema_and_fact()) {
            let (schema, block, _) = blocked(&spec);

            for cut in 0..block.len() {
                prop_assert!(
                    decode_block(&block[..cut], &schema).is_err(),
                    "a block cut to {} of {} bytes decoded", cut, block.len()
                );
            }
        }
    }

    /// **Each kind of damage is reported by name**, not merely refused.
    ///
    /// The properties above prove no corruption survives; this pins *which* error each
    /// fault draws, because a caller routes on it — a splitter treats [`NoSyncMarker`]
    /// as "scan on", where a [`ChecksumMismatch`] is a block to report.
    ///
    /// [`NoSyncMarker`]: WireError::NoSyncMarker
    /// [`ChecksumMismatch`]: WireError::ChecksumMismatch
    #[test]
    fn each_kind_of_damage_is_reported_by_name() {
        use ::proptest::{strategy::ValueTree, test_runner::TestRunner};

        let mut runner = TestRunner::deterministic();
        let spec = arb_schema_and_fact()
            .new_tree(&mut runner)
            .expect("a spec")
            .current();
        let (schema, block, _) = blocked(&spec);

        // A first byte that is not the marker's: the boundary is gone, not the block.
        let mut torn = block.clone();
        torn[0] = 0x00;
        assert_eq!(decode_block(&torn, &schema), Err(WireError::NoSyncMarker));

        // The marker intact and the magic wrong: a boundary that is not a block.
        let mut stamped = block.clone();
        stamped[SYNC.len()] ^= 0xFF;
        assert_eq!(decode_block(&stamped, &schema), Err(WireError::BadMagic));

        // A payload bit flipped past every header check: caught by the checksum,
        // and the error carries both numbers so the fault is diagnosable.
        let mut flipped = block.clone();
        let last = flipped.len() - 1;
        flipped[last] ^= 0x01;
        assert!(matches!(
            decode_block(&flipped, &schema),
            Err(WireError::ChecksumMismatch { declared, computed }) if declared != computed
        ));

        // A predicate name that is not UTF-8 — refused as a bad string before the
        // checksum is even computed, so a name is never interned from garbage.
        let mut named = block.clone();
        named[OVERHEAD] = 0xFF;
        assert_eq!(decode_block(&named, &schema), Err(WireError::BadString));
    }

    /// The name resolution fails by name on both sides of the wire: an encoder
    /// asked for a predicate its schema does not hold, and a decoder handed a block
    /// whose named predicate its *own* schema does not declare — two databases may
    /// number a predicate differently, so the id in the error is the caller's.
    #[test]
    fn a_predicate_neither_schema_knows_is_refused_by_name() {
        use ::proptest::{strategy::ValueTree, test_runner::TestRunner};

        let mut runner = TestRunner::deterministic();
        let spec = arb_schema_and_fact()
            .new_tree(&mut runner)
            .expect("a spec")
            .current();
        let (schema, block, facts) = blocked(&spec);

        // Encoding: an id the schema has no predicate for.
        let missing = PredicateId(9_999);
        let mut out = vec![];
        assert_eq!(
            encode_block(&mut out, &schema, missing, &facts),
            Err(WireError::UnknownPredicate(9_999))
        );

        // Decoding: a well-formed block against a reader whose schema declares
        // nothing — the name travels, so the error carries it.
        let empty = fjord_schema::schema::Schema::empty();
        assert!(matches!(
            decode_block(&block, &empty),
            Err(WireError::UnknownPredicateName(_))
        ));
    }
}
