//! **Item 4 of the recursion plan: a rule head is reconciled with declared key
//! order, explicitly rather than by encoding it straight through.**
//!
//! A local signature's declaration order **is** its physical key order; a
//! query's head record is sorted by *name* at lowering (`lower.rs:465`).
//! Encoding a projected record straight into a relation's declared layout would
//! therefore put values under the wrong physical fields — and same-typed fields
//! make that silent: the relation still scans and decodes consistently, it just
//! answers reversed edges. [`project`] is the fix: an explicit
//! name-to-declared-position materialisation, requiring **exactly** the
//! declared field set — no field missing, none extra, none supplied twice.
//!
//! **Record-only, key-only — both structural, not this module's job to
//! enforce.** The plan restricts a local relation's top-level type to a record
//! (`reject/non-record-relation`) and gives it no value side, so every relation
//! `project` ever reorders *is* the whole key. Those are shapes a `RelationDecl`
//! does not have yet (a later movement); this module works over any name type,
//! so wiring it to one costs nothing more than choosing what `N` is.
//!
//! Generic over `V` and unconstrained by it — a projection reorders values, it
//! never inspects or clones one, which is what lets it work uniformly over a
//! head record's `Value`s without this crate having an opinion about what a
//! `Value` is.

use std::collections::{HashMap, HashSet};

/// Why `supplied` could not be reconciled against `declared`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ProjectionError<N> {
    /// A declared field with no matching supplied name — checked in
    /// **declared** order, so the first missing field named is always the one
    /// that would be declared earliest.
    MissingField(N),
    /// A supplied field that no declared position claims — checked in
    /// **supplied** order, after every declared field is confirmed present.
    ExtraField(N),
    /// The same name supplied more than once — checked first, in **supplied**
    /// order, because a duplicate makes "the value named `X`" ambiguous before
    /// missing/extra can even be asked.
    DuplicateField(N),
}

/// Reorder `supplied`'s values into `declared`'s order.
///
/// # Errors
///
/// [`ProjectionError::DuplicateField`] if a name appears twice in `declared`
/// itself (checked first — a self-contradictory declaration cannot be
/// satisfied by any `supplied` at all, and this function makes no assumption
/// that a caller's `declared` is already known distinct) or twice in
/// `supplied`; otherwise [`ProjectionError::MissingField`] for the first
/// declared name `supplied` lacks; otherwise [`ProjectionError::ExtraField`]
/// for the first supplied name `declared` does not claim. A `supplied` that
/// survives all three is exactly a permutation of `declared`, so the reorder
/// below cannot fail.
pub fn project<N, V>(declared: &[N], supplied: Vec<(N, V)>) -> Result<Vec<V>, ProjectionError<N>>
where
    N: Eq + std::hash::Hash + Clone,
{
    // A duplicate *within* `declared` is checked before anything about
    // `supplied` is even looked at: it is not a data-path property (whoever
    // built this relation's signature owns it), but assuming it away here
    // would make "consume each supplied value exactly once" below a claim
    // this function cannot actually keep — see the regression test pinning
    // exactly this.
    let mut seen_declared: HashSet<&N> = HashSet::with_capacity(declared.len());
    for name in declared {
        if !seen_declared.insert(name) {
            return Err(ProjectionError::DuplicateField(name.clone()));
        }
    }

    // Duplicate check, in supplied order: index every name, refusing the first
    // one already seen.
    let mut position_of: HashMap<N, usize> = HashMap::with_capacity(supplied.len());
    for (i, (name, _)) in supplied.iter().enumerate() {
        if position_of.insert(name.clone(), i).is_some() {
            return Err(ProjectionError::DuplicateField(name.clone()));
        }
    }

    // Missing check, in declared order.
    for name in declared {
        if !position_of.contains_key(name) {
            return Err(ProjectionError::MissingField(name.clone()));
        }
    }

    // Extra check, in supplied order — everything declared is present (the
    // loop above proved it), so anything not in `declared` is surplus.
    let declared_set: HashSet<&N> = declared.iter().collect();
    for (name, _) in &supplied {
        if !declared_set.contains(name) {
            return Err(ProjectionError::ExtraField(name.clone()));
        }
    }

    // `supplied`'s names are now known to be exactly `declared`'s, each once.
    // Consume the values into per-position slots and pull each declared name's
    // slot in turn — every slot is taken exactly once, by construction.
    let mut slots: Vec<Option<V>> = supplied.into_iter().map(|(_, v)| Some(v)).collect();
    let mut out = Vec::with_capacity(declared.len());
    for name in declared {
        let i = position_of[name];
        out.push(
            slots[i]
                .take()
                .expect("each supplied position is claimed by exactly one declared name"),
        );
    }

    Ok(out)
}

#[cfg(test)]
mod tests {
    use super::*;
    use proptest::prelude::*;

    // ---- the named scenario the plan exists to prevent -------------------

    /// **The scenario item 4 names by hand: two same-typed fields answering
    /// reversed edges.** `Ref { from : Decl, to : Decl }`'s declared order is
    /// `[from, to]`; a query supplying them in the *opposite* order must still
    /// land `from`'s value under position 0 and `to`'s under position 1 — the
    /// one case where "just encode the values in whatever order they arrived"
    /// would be silently, undetectably wrong, because both fields share a type
    /// and the relation would still scan and decode without error.
    #[test]
    fn same_typed_fields_supplied_reversed_still_land_in_declared_order() {
        let declared = vec!["from", "to"];
        let supplied = vec![("to", 200), ("from", 100)];

        assert_eq!(project(&declared, supplied), Ok(vec![100, 200]));
    }

    #[test]
    fn already_declared_order_round_trips() {
        let declared = vec!["from", "to"];
        let supplied = vec![("from", 100), ("to", 200)];

        assert_eq!(project(&declared, supplied), Ok(vec![100, 200]));
    }

    #[test]
    fn a_missing_field_is_refused_by_name() {
        let declared = vec!["file", "name", "line"];
        let supplied = vec![("file", 1), ("name", 2)];

        assert_eq!(
            project(&declared, supplied),
            Err(ProjectionError::MissingField("line"))
        );
    }

    #[test]
    fn an_extra_field_is_refused_by_name() {
        let declared = vec!["file", "name"];
        let supplied = vec![("file", 1), ("name", 2), ("line", 3)];

        assert_eq!(
            project(&declared, supplied),
            Err(ProjectionError::ExtraField("line"))
        );
    }

    #[test]
    fn a_duplicate_field_is_refused_by_name_before_missing_or_extra() {
        let declared = vec!["file", "name"];
        // "file" supplied twice; "name" also missing entirely. The duplicate
        // must win — it is checked first, and it makes the rest of the
        // question ("which value names `file`?") unanswerable anyway.
        let supplied = vec![("file", 1), ("file", 2)];

        assert_eq!(
            project(&declared, supplied),
            Err(ProjectionError::DuplicateField("file"))
        );
    }

    /// **A regression, found by the property test below rather than
    /// anticipated by hand.** `declared` repeating a name used to reach the
    /// final reorder loop, which consumes each supplied slot exactly once —
    /// so the *second* occurrence of the repeated declared name found its
    /// slot already taken and panicked instead of returning an error. Pinned
    /// as its own case: a property's shrunk counterexample is a fact about
    /// today's code, not a substitute for a named guard future changes are
    /// checked against.
    #[test]
    fn a_duplicate_name_within_declared_itself_is_refused_not_a_panic() {
        let declared = vec!["c", "c"];
        let supplied = vec![("c", 1)];

        assert_eq!(
            project(&declared, supplied),
            Err(ProjectionError::DuplicateField("c"))
        );
    }

    #[test]
    fn an_empty_declared_record_accepts_only_an_empty_supply() {
        let declared: Vec<&str> = vec![];
        assert_eq!(project(&declared, Vec::<(&str, i32)>::new()), Ok(vec![]));
        assert_eq!(
            project(&declared, vec![("x", 1)]),
            Err(ProjectionError::ExtraField("x"))
        );
    }

    // ---- the oracle: a string-name model, no hashing at all -------------
    //
    // Three independent linear scans, checked in the same declared precedence
    // (duplicate, then missing, then extra) as the real function — a
    // deliberately unindexed, O(n·m) reference an implementer would write by
    // just reading the rule off the plan text, sharing no code with the
    // HashMap-backed real implementation.

    fn naive_project<N: Eq + Clone, V: Clone>(
        declared: &[N],
        supplied: &[(N, V)],
    ) -> Result<Vec<V>, ProjectionError<N>> {
        for (i, name) in declared.iter().enumerate() {
            if declared[..i].contains(name) {
                return Err(ProjectionError::DuplicateField(name.clone()));
            }
        }

        for (i, (name, _)) in supplied.iter().enumerate() {
            if supplied[..i].iter().any(|(other, _)| other == name) {
                return Err(ProjectionError::DuplicateField(name.clone()));
            }
        }

        for name in declared {
            if !supplied.iter().any(|(n, _)| n == name) {
                return Err(ProjectionError::MissingField(name.clone()));
            }
        }

        for (name, _) in supplied {
            if !declared.iter().any(|d| d == name) {
                return Err(ProjectionError::ExtraField(name.clone()));
            }
        }

        Ok(declared
            .iter()
            .map(|name| {
                supplied
                    .iter()
                    .find(|(n, _)| n == name)
                    .expect("presence already checked above")
                    .1
                    .clone()
            })
            .collect())
    }

    /// Small alphabets on both `N` and the declared/supplied lengths, so
    /// generated cases collide (duplicates, missing, extra) often instead of
    /// almost always being trivially well-formed.
    fn small_name() -> impl Strategy<Value = &'static str> {
        prop::sample::select(vec!["a", "b", "c", "d"])
    }

    /// **The census.** Three of the four outcomes here are refusals, and a refusal
    /// agrees with the oracle cheaply — both say `Err`. It is the *successful*
    /// projection that carries the claim this module exists for, that each value lands
    /// under its declared position, and it is the rarest draw of the four.
    #[test]
    fn the_generator_reaches_every_outcome_including_success() {
        use proptest::{
            strategy::{Strategy, ValueTree},
            test_runner::TestRunner,
        };

        const RUNS: usize = 2_000;

        let mut runner = TestRunner::deterministic();
        let strategy = (
            prop::collection::vec(small_name(), 0..5),
            prop::collection::vec(small_name(), 0..5),
        );
        let (mut projected, mut duplicate, mut missing, mut extra) = (0, 0, 0, 0);

        for _ in 0..RUNS {
            let (declared, supplied_names) = strategy.new_tree(&mut runner).unwrap().current();
            let supplied: Vec<(&str, u32)> = supplied_names
                .iter()
                .enumerate()
                .map(|(i, name)| (*name, i as u32))
                .collect();

            match project(&declared, supplied) {
                Ok(_) => projected += 1,
                Err(ProjectionError::DuplicateField(_)) => duplicate += 1,
                Err(ProjectionError::MissingField(_)) => missing += 1,
                Err(ProjectionError::ExtraField(_)) => extra += 1,
            }
        }

        assert!(
            projected > RUNS / 50,
            "the generator almost never draws a well-formed pair, so the placement \
             claim is barely exercised: {projected} of {RUNS}"
        );
        assert!(duplicate > 0, "no draw was refused for a duplicate");
        assert!(missing > 0, "no draw was refused for a missing field");
        assert!(extra > 0, "no draw was refused for an extra field");
    }

    proptest! {
        /// The real projection and the naive string-name oracle agree exactly
        /// — same `Ok`/`Err`, same value, for any declared/supplied pair,
        /// well-formed or not.
        #[test]
        fn agrees_with_the_naive_string_name_oracle(
            declared in prop::collection::vec(small_name(), 0..5),
            supplied_names in prop::collection::vec(small_name(), 0..5),
        ) {
            let supplied: Vec<(&str, u32)> = supplied_names
                .into_iter()
                .enumerate()
                .map(|(i, name)| (name, i as u32))
                .collect();

            let real = project(&declared, supplied.clone());
            let oracle = naive_project(&declared, &supplied);

            prop_assert_eq!(real, oracle);
        }

        /// When it succeeds, position `i` of the output is always the value
        /// supplied under `declared[i]`'s name — the defining property,
        /// independent of both implementations above.
        #[test]
        fn a_successful_projection_places_each_value_under_its_declared_name(
            declared in prop::collection::hash_set(small_name(), 1..5)
                .prop_map(|set| set.into_iter().collect::<Vec<_>>()),
            seed in any::<u64>(),
        ) {
            // Build a well-formed supply by permuting `declared` deterministically
            // from `seed`, so every case is guaranteed to succeed and the
            // property is checked on the interesting (accepted) side.
            let mut supplied: Vec<(&str, u32)> = declared
                .iter()
                .enumerate()
                .map(|(i, name)| (*name, i as u32))
                .collect();
            let mut state = seed | 1;
            for i in (1..supplied.len()).rev() {
                state = state.wrapping_mul(6364136223846793005).wrapping_add(1);
                let j = (state >> 33) as usize % (i + 1);
                supplied.swap(i, j);
            }

            let by_name: HashMap<&str, u32> = supplied.iter().copied().collect();
            let out = project(&declared, supplied).expect("a permutation of declared is well-formed");

            prop_assert_eq!(out.len(), declared.len());
            for (i, name) in declared.iter().enumerate() {
                prop_assert_eq!(out[i], by_name[name]);
            }
        }
    }
}
