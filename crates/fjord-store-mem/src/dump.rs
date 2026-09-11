//! **The rows of a store, walked** — what an exporter reads to write a database out.
//!
//! Bytes and an id, nothing decoded: the schema is what says how to read a key, and it
//! lives a crate away. A caller that has one turns these into facts; `fjord export` is
//! the one that does.
//!
//! **One snapshot for the whole walk.** Every predicate is scanned against the reader
//! handed in, so a database written to under the walk cannot contribute rows from two
//! states — which for an exporter is the difference between a file that reads back and
//! one holding a reference to a fact it never wrote.

use fjord_schema::schema::PredicateId;
use fjord_store::{error::StoreError, fact_store::FactStore};

/// One row, owning its bytes: where it is stored, and what is stored there.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OwnedRow {
    pub predicate: PredicateId,
    pub key: Vec<u8>,
    pub sequence: u64,
    pub value: Vec<u8>,
}

/// Read every row of one predicate.
///
/// **One scan, because that is what a scan is.** The seam bounds a scan to the
/// predicate named by `lo`'s leading bytes, so there is no walk of the whole key space
/// to be had and asking for one would be asking the store to do something it has said
/// it does not do.
///
/// Rows come back in **key** order, which is the order they are stored in and not the
/// order they were written in. A caller that needs the second sorts on
/// [`OwnedRow::sequence`] — which is what a predicate whose facts reference each other
/// has to do, because a referent is interned before its referrer and nothing else
/// recovers that.
pub fn rows_for<S: FactStore>(
    store: &S,
    predicate: PredicateId,
) -> Result<Vec<OwnedRow>, StoreError> {
    let lo = predicate.0.to_be_bytes();
    let hi = predicate.0.checked_add(1).map(u32::to_be_bytes);

    let mut rows = Vec::new();

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

    Ok(rows)
}

/// Read every row of `store`, predicate by predicate, for `predicate_count`
/// predicates — the schema's length, which is one past the largest valid id.
///
/// **The whole database, resident.** A caller that can work a predicate at a time wants
/// [`rows_for`] instead, and for a real index the difference is whether it fits.
pub fn rows_of<S: FactStore>(store: &S, predicate_count: u32) -> Result<Vec<OwnedRow>, StoreError> {
    let mut rows = Vec::new();

    for predicate in 0..predicate_count {
        rows.append(&mut rows_for(store, PredicateId(predicate))?);
    }

    Ok(rows)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::MemStore;
    use fjord_schema::id::FactId;

    /// **The walk answers every row, with the id it has.**
    ///
    /// What it owes a caller is that it reaches every predicate's rows and reports the
    /// sequence an id can be rebuilt from — a walk that dropped a predicate or lost a
    /// sequence would export a database missing facts, and say nothing.
    #[test]
    fn the_walk_reaches_every_row_with_its_own_sequence() {
        let mut store = MemStore::new();

        store.insert_valued(PredicateId(0), b"a".to_vec(), 1, Vec::new());
        store.insert_valued(PredicateId(0), b"b".to_vec(), 2, Vec::new());
        store.insert_valued(PredicateId(2), b"c".to_vec(), 1, b"v".to_vec());

        let mut rows = rows_of(&store, 3).expect("the walk runs");
        rows.sort_by_key(|row| (row.predicate.0, row.sequence));

        assert_eq!(rows.len(), 3);
        assert_eq!(
            rows.iter()
                .map(|row| (
                    row.predicate.0,
                    row.sequence,
                    row.key.clone(),
                    row.value.clone()
                ))
                .collect::<Vec<_>>(),
            vec![
                (0, 1, b"a".to_vec(), Vec::new()),
                (0, 2, b"b".to_vec(), Vec::new()),
                (2, 1, b"c".to_vec(), b"v".to_vec()),
            ]
        );

        // And the sequence composes back into the id the store minted.
        assert_eq!(
            FactId::new(rows[2].predicate, rows[2].sequence).expect("a well-formed id"),
            FactId::new(PredicateId(2), 1).expect("a well-formed id")
        );
    }

    /// A predicate with no rows contributes none, rather than an empty entry.
    #[test]
    fn a_predicate_with_no_rows_is_absent_rather_than_empty() {
        let mut store = MemStore::new();
        store.insert_valued(PredicateId(1), b"only".to_vec(), 1, Vec::new());

        let rows = rows_of(&store, 4).expect("the walk runs");

        assert_eq!(rows.len(), 1);
        assert_eq!(rows[0].predicate, PredicateId(1));
    }
}
