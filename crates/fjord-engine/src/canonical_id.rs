//! **Item 3 of the recursion plan: a local relation's tuples get a `FactId` from
//! their content, not their arrival order.**
//!
//! Assigning a derived tuple's id in derivation order makes the id a function of
//! rule scheduling, of rectangular-versus-triangular expansion ([item
//! 5b](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)),
//! and of the snapshot representation — three things a cursor cannot see and must
//! not depend on. Assigning it in **encoded-key order** makes it a function of
//! content alone: two derivations of the same relation, however they got there,
//! rank a given tuple identically. `Executor::resume` already hard-compares a
//! saved `fact_id` against the re-derived row's own, so this is not a new
//! requirement — it is the definition the executor was already assuming.
//!
//! **When this runs is not this module's job.** The ids of a relation are minted
//! when that relation is *finalised* — at fixpoint convergence for an accumulated
//! relation, at the end of its stratum for a `Once` one — never mid-fixpoint,
//! because a tuple's rank moves as later rounds insert content before it. This
//! module is the pure ranking function a driver calls exactly once, at that
//! moment; the moment itself belongs to Movement 2's driver.
//!
//! No overflow test exercises [`MAX_FACT_SEQUENCE`] here: [item
//! 6](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)'s
//! budget clamps a program's retained-facts limit to it *at configuration*, so a
//! relation this function ranks can never hold more tuples than the id space —
//! the same reasoning that lets item 6 say finalisation "has no failure to
//! report". [`canonical_ids`] still returns a [`Result`] rather than asserting
//! that, because the guarantee is the budget's to keep, not this function's to
//! assume.

use std::collections::BTreeSet;

use fjord_schema::{
    id::{FactId, FactIdError},
    schema::PredicateId,
};

/// Rank every **distinct** key in `keys`, in encoded byte order, and mint the
/// `predicate`-tagged [`FactId`] that rank names.
///
/// Duplicate keys collapse to one rank — a relation's tuples are already
/// deduplicated by the time they are finalised
/// ([step 5](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)
/// of the round transition), so a caller handing this function a raw candidate
/// stream rather than a finalised relation is asking a question this function
/// answers safely rather than one it assumes cannot arise.
///
/// Ranks start at **one**: sequence `0` is reserved
/// ([`FactId::new`]), so the first tuple in key order is rank 1, matching every
/// other predicate's own sequence numbering.
///
/// # Errors
///
/// [`FactIdError::FactIdSequence`] if `keys` holds more distinct entries than
/// [`MAX_FACT_SEQUENCE`](fjord_schema::id::MAX_FACT_SEQUENCE) allows, or
/// [`FactIdError::PredicateIdTooWide`] if `predicate` itself does not fit a
/// `FactId` tag. Neither is expected to fire in practice — see the module doc.
pub fn canonical_ids<'a, I>(
    predicate: PredicateId,
    keys: I,
) -> Result<Vec<(&'a [u8], FactId)>, FactIdError>
where
    I: IntoIterator<Item = &'a [u8]>,
{
    // A `BTreeSet` sorts *and* dedups in the one pass — encoded-key order is a
    // byte-lexicographic order, which is exactly what `&[u8]`'s own `Ord` gives,
    // so no custom comparator is needed here or anywhere else in this module.
    let distinct: BTreeSet<&'a [u8]> = keys.into_iter().collect();

    distinct
        .into_iter()
        .enumerate()
        .map(|(i, key)| {
            // `i` is zero-based; sequence 0 is reserved, so rank is `i + 1`.
            let sequence = i as u64 + 1;
            FactId::new(predicate, sequence).map(|id| (key, id))
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use proptest::prelude::*;

    fn pred() -> PredicateId {
        PredicateId(3)
    }

    /// **The oracle.** A completely different algorithm for the same claim —
    /// O(n²) counting instead of sorting — so agreement between the two is
    /// evidence of correctness rather than of the two implementations sharing a
    /// bug. A key's rank is one more than the number of *other* distinct keys
    /// strictly less than it, which is what "encoded-key order, one-indexed"
    /// means read literally rather than read as a sort.
    fn naive_rank(distinct: &[&[u8]], target: &[u8]) -> u64 {
        1 + distinct
            .iter()
            .filter(|candidate| **candidate < target)
            .count() as u64
    }

    fn naive_ranks<'a>(keys: &[&'a [u8]]) -> Vec<(&'a [u8], u64)> {
        let mut distinct: Vec<&[u8]> = keys.to_vec();
        distinct.sort_unstable();
        distinct.dedup();

        distinct
            .iter()
            .map(|key| (*key, naive_rank(&distinct, key)))
            .collect()
    }

    /// **The census.** The oracle property is about *ranking*, which a set of one key
    /// cannot exercise and an empty one cannot reach at all. Duplicates matter as much:
    /// collapsing them is this function's job, so a draw that never repeats a key would
    /// leave the dedup unmeasured.
    #[test]
    fn the_generator_reaches_multi_key_sets_and_duplicates() {
        use proptest::{
            strategy::{Strategy, ValueTree},
            test_runner::TestRunner,
        };

        const RUNS: usize = 2_000;

        let mut runner = TestRunner::deterministic();
        let strategy = prop::collection::vec(prop::collection::vec(any::<u8>(), 0..6), 0..40);
        let (mut trivial, mut many, mut with_duplicates) = (0, 0, 0);

        for _ in 0..RUNS {
            let keys = strategy.new_tree(&mut runner).unwrap().current();
            let distinct: BTreeSet<Vec<u8>> = keys.iter().cloned().collect();

            with_duplicates += usize::from(keys.len() != distinct.len());
            if distinct.len() < 2 {
                trivial += 1;
            } else {
                many += 1;
            }
        }

        assert!(
            many > RUNS / 2,
            "the generator rarely draws two distinct keys, so ranking is barely \
             exercised: {many} of {RUNS}"
        );
        assert!(
            with_duplicates > RUNS / 20,
            "duplicates are barely drawn: {with_duplicates}"
        );
        assert!(trivial > 0, "no draw reached the empty or single-key case");
    }

    proptest! {
        /// The real allocator and the naive counting oracle agree on every rank,
        /// for any set of keys in any arrival order — the property `canonical_ids`
        /// exists to guarantee, checked against an implementation that shares no
        /// code with it.
        #[test]
        fn agrees_with_the_naive_counting_oracle(
            keys in prop::collection::vec(prop::collection::vec(any::<u8>(), 0..6), 0..40)
        ) {
            let borrowed: Vec<&[u8]> = keys.iter().map(Vec::as_slice).collect();

            let real = canonical_ids(pred(), borrowed.iter().copied()).unwrap();
            let oracle = naive_ranks(&borrowed);

            let real_ranks: Vec<(&[u8], u64)> = real
                .iter()
                .map(|(key, id)| (*key, id.sequence()))
                .collect();

            prop_assert_eq!(real_ranks, oracle);

            // Every minted id also carries the predicate it was asked for —
            // ranking must not silently retag.
            for (_, id) in &real {
                prop_assert_eq!(id.predicate(), pred());
            }
        }

        /// **Arrival order is not identity.** Shuffling the input must not move a
        /// single rank — the whole reason this module exists rather than a
        /// derivation-order counter. `keys` here are already-distinct by
        /// construction (`unique_key_set`), so no dedup step can mask a bug this
        /// property would otherwise catch.
        #[test]
        fn order_of_arrival_does_not_move_a_rank(
            keys in unique_key_set(1..25),
            seed in any::<u64>(),
        ) {
            let mut shuffled = keys.clone();
            shuffle(&mut shuffled, seed);

            let from_sorted = canonical_ids(pred(), keys.iter().map(Vec::as_slice)).unwrap();
            let from_shuffled =
                canonical_ids(pred(), shuffled.iter().map(Vec::as_slice)).unwrap();

            let ranks = |assigned: &[(&[u8], FactId)]| -> Vec<(Vec<u8>, u64)> {
                let mut out: Vec<(Vec<u8>, u64)> = assigned
                    .iter()
                    .map(|(key, id)| (key.to_vec(), id.sequence()))
                    .collect();
                out.sort();
                out
            };

            prop_assert_eq!(ranks(&from_sorted), ranks(&from_shuffled));
        }

        /// **A tuple's rank moves exactly when something sorts before it — never
        /// otherwise.** This is the specific claim item 3 makes about *why*
        /// mid-fixpoint ids are unobservable: inserting one more key shifts every
        /// later key by exactly one rank and touches no earlier key at all.
        #[test]
        fn inserting_a_key_shifts_only_what_sorts_after_it(
            keys in unique_key_set(1..25),
            new_key in prop::collection::vec(any::<u8>(), 0..6),
        ) {
            prop_assume!(!keys.contains(&new_key));

            let before = canonical_ids(pred(), keys.iter().map(Vec::as_slice)).unwrap();

            let mut with_new = keys.clone();
            with_new.push(new_key.clone());
            let after = canonical_ids(pred(), with_new.iter().map(Vec::as_slice)).unwrap();

            let rank_of = |assigned: &[(&[u8], FactId)], key: &[u8]| -> u64 {
                assigned
                    .iter()
                    .find(|(k, _)| *k == key)
                    .expect("every requested key is ranked")
                    .1
                    .sequence()
            };

            for key in &keys {
                let old_rank = rank_of(&before, key);
                let new_rank = rank_of(&after, key);
                if key.as_slice() < new_key.as_slice() {
                    prop_assert_eq!(new_rank, old_rank, "a key sorting earlier keeps its rank");
                } else {
                    prop_assert_eq!(
                        new_rank,
                        old_rank + 1,
                        "a key sorting later shifts by exactly one"
                    );
                }
            }
        }
    }

    /// A duplicate key is one rank, not two — the specific case a caller handing
    /// this function raw candidates (rather than an already-deduplicated
    /// relation) must not silently double-count.
    #[test]
    fn a_duplicate_key_collapses_to_one_rank() {
        let a: &[u8] = b"a";
        let assigned = canonical_ids(pred(), [a, a, a]).unwrap();

        assert_eq!(assigned.len(), 1);
        assert_eq!(assigned[0].1.sequence(), 1);
    }

    /// The empty relation ranks to nothing — no phantom rank 1 for a relation
    /// that finalised with zero tuples.
    #[test]
    fn no_keys_is_no_ranks() {
        let assigned = canonical_ids(pred(), std::iter::empty()).unwrap();
        assert!(assigned.is_empty());
    }

    /// Every minted id names the predicate it was allocated for, sequence one for
    /// the lexicographically smallest key — pinned as a literal example beside
    /// the property above, because a property that only ever runs generated
    /// input is easy to misread.
    #[test]
    fn the_smallest_key_by_bytes_is_rank_one() {
        let low: &[u8] = &[0x00];
        let mid: &[u8] = &[0x01];
        let high: &[u8] = &[0xff];

        let assigned = canonical_ids(pred(), [high, low, mid]).unwrap();

        assert_eq!(
            assigned,
            vec![
                (low, FactId::new(pred(), 1).unwrap()),
                (mid, FactId::new(pred(), 2).unwrap()),
                (high, FactId::new(pred(), 3).unwrap()),
            ]
        );
    }

    prop_compose! {
        /// A set of *distinct* keys — proptest's `HashSet`-backed
        /// `sample::subsequence`-style dedup isn't needed here since `BTreeSet`
        /// does the same collapsing job the strategy would otherwise have to.
        fn unique_key_set(size: std::ops::Range<usize>)
            (keys in prop::collection::vec(prop::collection::vec(any::<u8>(), 1..6), size))
            -> Vec<Vec<u8>>
        {
            // A dedup can shrink the set below the requested lower bound; that is
            // fine — the properties above hold for any nonempty set, and an
            // occasional small set is more coverage, not less.
            let distinct: BTreeSet<Vec<u8>> = keys.into_iter().collect();
            distinct.into_iter().collect()
        }
    }

    /// A cheap, seeded, dependency-free shuffle — pulling in a `rand` crate for
    /// one Fisher–Yates pass in a test module is not worth the new dependency.
    fn shuffle<T>(items: &mut [T], seed: u64) {
        let mut state = seed | 1; // an even seed would make the LCG's low bit constant
        for i in (1..items.len()).rev() {
            state = state.wrapping_mul(6364136223846793005).wrapping_add(1);
            let j = (state >> 33) as usize % (i + 1);
            items.swap(i, j);
        }
    }
}
