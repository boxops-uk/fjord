//! **A `MemStore` as bytes**, so a store built by a real database can be rebuilt
//! somewhere that has no database at all.
//!
//! The browser is the reason this exists. `fjord-ingest` — the write funnel that
//! turns a nested wire fact into an interned one — depends on `fjord-store-fjall`
//! by name and on purpose, because interning claims ids durably and writes through
//! a batch. So **nothing compiled to WebAssembly can intern a fact**, and a corpus
//! cannot be assembled in the page.
//!
//! It does not need to be. Interning is what assigns ids, and ids are assigned
//! *once*: a database indexed offline already holds the answer. What the page needs
//! is not the funnel but the result — the rows, with the ids they were already
//! given — and putting those into a [`MemStore`] is [`MemStore::insert_valued`],
//! which takes exactly what a row is.
//!
//! **So this is not a serialisation format for databases.** It is the model store's
//! own shape written down: predicate, key bytes, sequence, value bytes, one record
//! each, in whatever order the writer met them. There is no schema in it, no
//! lifecycle, no durability — a `MemStore` has none of those either, and a format
//! that carried them would be claiming something the thing it rebuilds cannot hold.
//!
//! **The fingerprint is in the header and is not checked here.** An image is bytes
//! keyed and valued against one schema, so reading it against another decodes
//! nonsense — the same failure a client's handshake exists to catch, and caught the
//! same way: the number travels with the bytes, and the caller compares it to the
//! schema it holds. [`read`] returns it rather than judging it, because this module
//! does not know what the caller is about to do with the store.

use fjord_schema::{id::FactId, schema::PredicateId};
use fjord_store::{error::StoreError, fact_store::FactStore};
use fjord_wire::varint;

use crate::MemStore;

/// The eight bytes an image starts with. Version is in the magic rather than in a
/// field beside it: a format change that a reader must refuse should not be
/// readable far enough to reach a version field it then rejects.
pub const MAGIC: &[u8; 8] = b"FJMEM\0\0\x01";

/// One row of the model store — what [`MemStore::insert_valued`] takes, borrowed.
///
/// `sequence` is the fact's number *within its predicate* and not a raw [`FactId`],
/// for the reason `insert_valued` gives: the real store composes an id from the two,
/// so a model that carried whole ids could hold a fact tagged for another predicate.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Row<'a> {
    pub predicate: PredicateId,
    pub key: &'a [u8],
    pub sequence: u64,
    pub value: &'a [u8],
}

/// What an image was read as: the schema it was written against, and the store.
pub struct Image {
    /// The whole-schema fingerprint the writer stated. **Compare it before you
    /// trust a row** — see the module note.
    pub fingerprint: u64,
    pub store: MemStore,
}

/// Elides the store, which is not `Debug` and would not be readable if it were:
/// what identifies an image is the schema it was written against.
impl core::fmt::Debug for Image {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        f.debug_struct("Image")
            .field("fingerprint", &format_args!("{:#018x}", self.fingerprint))
            .finish_non_exhaustive()
    }
}

/// Why an image could not be read.
///
/// Every variant is provoked by a test in this module. A corrupt image is a
/// static asset that was truncated, swapped or built by another version, and each
/// of those says something different to whoever has to fix it.
#[derive(Debug, PartialEq, Eq)]
pub enum DumpError {
    /// The first eight bytes are not [`MAGIC`] — not an image, or an image of a
    /// format this build does not read.
    NotAnImage,
    /// The bytes ended inside a record. Names what was being read when they did.
    Truncated(&'static str),
    /// A varint ran past the end of the buffer or did not terminate.
    BadVarint(&'static str),
    /// A predicate id past what a [`FactId`] can tag, or a sequence past what it
    /// can hold — an image no `MemStore` could have produced.
    BadFactId { predicate: u32, sequence: u64 },
    /// The record count in the header and the records in the body disagree.
    CountMismatch { stated: u64, found: u64 },
}

impl core::fmt::Display for DumpError {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        match self {
            Self::NotAnImage => write!(f, "not a fjord store image"),
            Self::Truncated(what) => write!(f, "the image ends inside {what}"),
            Self::BadVarint(what) => write!(f, "{what} is not a varint"),
            Self::BadFactId {
                predicate,
                sequence,
            } => write!(
                f,
                "predicate {predicate} sequence {sequence} is not a fact id any store could mint"
            ),
            Self::CountMismatch { stated, found } => {
                write!(
                    f,
                    "the header states {stated} rows and the body holds {found}"
                )
            }
        }
    }
}

impl core::error::Error for DumpError {}

/// Write an image: the magic, the fingerprint, the row count, then the rows.
///
/// The count leads so a reader can size its work before it starts and can tell a
/// truncated image from a short one — a file that simply stops is otherwise a
/// perfectly well-formed image of fewer rows.
pub fn write<'a>(fingerprint: u64, rows: impl IntoIterator<Item = Row<'a>>) -> Vec<u8> {
    let rows: Vec<Row<'a>> = rows.into_iter().collect();

    let mut out = Vec::new();
    out.extend_from_slice(MAGIC);
    out.extend_from_slice(&fingerprint.to_le_bytes());
    varint::put_u64(&mut out, rows.len() as u64);

    for row in rows {
        varint::put_u64(&mut out, u64::from(row.predicate.0));
        varint::put_u64(&mut out, row.sequence);
        varint::put_u64(&mut out, row.key.len() as u64);
        out.extend_from_slice(row.key);
        varint::put_u64(&mut out, row.value.len() as u64);
        out.extend_from_slice(row.value);
    }

    out
}

/// Read an image back into a store.
pub fn read(bytes: &[u8]) -> Result<Image, DumpError> {
    let rest = bytes
        .strip_prefix(MAGIC.as_slice())
        .ok_or(DumpError::NotAnImage)?;

    let (fingerprint, rest) = take(rest, 8, "the fingerprint")?;
    let fingerprint = u64::from_le_bytes(fingerprint.try_into().expect("eight bytes"));

    let (stated, mut rest) = read_varint(rest, "the row count")?;

    let mut store = MemStore::new();
    let mut found = 0u64;

    while !rest.is_empty() {
        let (predicate, after) = read_varint(rest, "a predicate id")?;
        let (sequence, after) = read_varint(after, "a sequence")?;
        let (key_len, after) = read_varint(after, "a key length")?;
        let (key, after) = take(after, key_len as usize, "a key")?;
        let (value_len, after) = read_varint(after, "a value length")?;
        let (value, after) = take(after, value_len as usize, "a value")?;

        let predicate = u32::try_from(predicate).map_err(|_| DumpError::BadFactId {
            predicate: u32::MAX,
            sequence,
        })?;
        let predicate = PredicateId(predicate);

        // Checked rather than assumed: `insert_valued` panics on an id it cannot
        // mint, and a corrupt static asset must not be able to panic the page.
        FactId::new(predicate, sequence).map_err(|_| DumpError::BadFactId {
            predicate: predicate.0,
            sequence,
        })?;

        store.insert_valued(predicate, key.to_vec(), sequence, value.to_vec());
        found += 1;
        rest = after;
    }

    if found != stated {
        return Err(DumpError::CountMismatch { stated, found });
    }

    Ok(Image { fingerprint, store })
}

fn take<'a>(
    bytes: &'a [u8],
    n: usize,
    what: &'static str,
) -> Result<(&'a [u8], &'a [u8]), DumpError> {
    if bytes.len() < n {
        return Err(DumpError::Truncated(what));
    }
    Ok(bytes.split_at(n))
}

fn read_varint<'a>(bytes: &'a [u8], what: &'static str) -> Result<(u64, &'a [u8]), DumpError> {
    let (value, read) = varint::get_u64(bytes).map_err(|_| DumpError::BadVarint(what))?;
    Ok((value, &bytes[read..]))
}

/// One row, owning its bytes — what [`rows_of`] collects, and what [`Row`] borrows.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OwnedRow {
    pub predicate: PredicateId,
    pub key: Vec<u8>,
    pub sequence: u64,
    pub value: Vec<u8>,
}

impl OwnedRow {
    #[must_use]
    pub fn as_row(&self) -> Row<'_> {
        Row {
            predicate: self.predicate,
            key: &self.key,
            sequence: self.sequence,
            value: &self.value,
        }
    }
}

/// Read every row of `store`, predicate by predicate, for `predicate_count`
/// predicates — the schema's length, which is one past the largest valid id.
///
/// **One scan per predicate, because that is what a scan is.** The seam bounds a
/// scan to the predicate named by `lo`'s leading bytes, so there is no walk of the
/// whole key space to be had and asking for one would be asking the store to do
/// something it has said it does not do.
pub fn rows_of<S: FactStore>(store: &S, predicate_count: u32) -> Result<Vec<OwnedRow>, StoreError> {
    let mut rows = Vec::new();

    for predicate in 0..predicate_count {
        let lo = predicate.to_be_bytes();
        let hi = predicate.checked_add(1).map(u32::to_be_bytes);

        for found in store.scan(&lo, hi.as_ref().map(|end| end.as_slice()))? {
            let (_, id) = found?;

            // The key comes from `point` rather than from the scan: a scan yields the
            // *full* key, predicate prefix and all, and a row is stored without it.
            let Some(entity) = store.point(id)? else {
                continue;
            };

            rows.push(OwnedRow {
                predicate: id.predicate(),
                key: entity.key.to_vec(),
                sequence: id.sequence(),
                value: entity.value.to_vec(),
            });
        }
    }

    Ok(rows)
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A store with a value-less fact, a valued one, and two predicates — enough
    /// that a format confusing key with value, or losing a predicate boundary,
    /// cannot pass.
    fn built() -> MemStore {
        let mut store = MemStore::new();
        store.insert(PredicateId(1), vec![0x01, 0x02], 1);
        store.insert(PredicateId(1), vec![0x01, 0x03], 2);
        store.insert_valued(PredicateId(4), vec![0xff], 1, vec![0xaa, 0xbb, 0xcc]);
        store
    }

    #[test]
    fn a_store_survives_the_round_trip_row_for_row() {
        let store = built();
        let before = rows_of(&store, 8).expect("the built store scans");

        let bytes = write(0xdead_beef, before.iter().map(OwnedRow::as_row));
        let image = read(&bytes).expect("what write wrote, read reads");

        assert_eq!(image.fingerprint, 0xdead_beef);
        assert_eq!(
            rows_of(&image.store, 8).expect("the rebuilt store scans"),
            before
        );
    }

    /// **The empty image is a real case**, not a degenerate one: a database with a
    /// predicate nothing has written yet dumps to this, and a reader that treats
    /// "no rows" as "no image" would refuse it.
    #[test]
    fn an_empty_store_round_trips() {
        let bytes = write(7, []);
        let image = read(&bytes).expect("an empty image is an image");

        assert_eq!(image.fingerprint, 7);
        assert!(rows_of(&image.store, 8).expect("it scans").is_empty());
    }

    #[test]
    fn bytes_that_are_not_an_image_are_refused_by_name() {
        assert_eq!(
            read(b"not an image at all").unwrap_err(),
            DumpError::NotAnImage
        );
        assert_eq!(read(b"").unwrap_err(), DumpError::NotAnImage);
    }

    #[test]
    fn an_image_cut_short_names_what_it_was_reading() {
        let store = built();
        let rows = rows_of(&store, 8).expect("it scans");
        let full = write(1, rows.iter().map(OwnedRow::as_row));

        // Inside the fingerprint.
        assert_eq!(
            read(&full[..12]).unwrap_err(),
            DumpError::Truncated("the fingerprint")
        );

        // Inside the body — the exact offset does not matter, only that every prefix
        // of a well-formed image is refused rather than read as a shorter one.
        for cut in MAGIC.len() + 9..full.len() {
            assert!(
                read(&full[..cut]).is_err(),
                "a prefix of {cut} bytes was accepted as a whole image"
            );
        }
    }

    /// The count leads the body for exactly this: a file that simply stops is
    /// otherwise a well-formed image of fewer rows.
    #[test]
    fn a_body_that_disagrees_with_the_header_count_is_refused() {
        let store = built();
        let rows = rows_of(&store, 8).expect("it scans");
        let full = write(1, rows.iter().map(OwnedRow::as_row));

        // Rewrite the header count as one more than the body holds. The count is a
        // one-byte varint at this size, immediately after magic and fingerprint.
        let at = MAGIC.len() + 8;
        let mut tampered = full.clone();
        tampered[at] = rows.len() as u8 + 1;

        assert_eq!(
            read(&tampered).unwrap_err(),
            DumpError::CountMismatch {
                stated: rows.len() as u64 + 1,
                found: rows.len() as u64,
            }
        );
    }

    /// A predicate id past what a `FactId` can tag. `insert_valued` would panic on
    /// it, and a static asset a page fetches must not be able to panic the page.
    #[test]
    fn a_fact_id_no_store_could_mint_is_refused_rather_than_panicking() {
        let mut bytes = Vec::new();
        bytes.extend_from_slice(MAGIC);
        bytes.extend_from_slice(&1u64.to_le_bytes());
        varint::put_u64(&mut bytes, 1);
        varint::put_u64(&mut bytes, u64::from(u32::MAX)); // predicate, past MAX_TAGGABLE
        varint::put_u64(&mut bytes, 1); // sequence
        varint::put_u64(&mut bytes, 0); // key length
        varint::put_u64(&mut bytes, 0); // value length

        assert_eq!(
            read(&bytes).unwrap_err(),
            DumpError::BadFactId {
                predicate: u32::MAX,
                sequence: 1,
            }
        );
    }

    #[test]
    fn a_varint_that_does_not_terminate_is_refused_by_name() {
        let mut bytes = Vec::new();
        bytes.extend_from_slice(MAGIC);
        bytes.extend_from_slice(&1u64.to_le_bytes());
        // Ten continuation bytes: longer than any u64 varint, and never terminated.
        bytes.extend_from_slice(&[0x80; 10]);

        assert_eq!(
            read(&bytes).unwrap_err(),
            DumpError::BadVarint("the row count")
        );
    }
}
