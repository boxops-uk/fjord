//! **Item 2 of the recursion plan: an engine-side catalogue presenting base and
//! local declarations uniformly, and the tag bound it must never cross.**
//!
//! `PredicateId` is a predicate's position in a **dense array** —
//! [`Schema::len`](fjord_schema::schema::Schema::len)'s own doc says a schema's
//! length is "one past the largest valid `PredicateId`" — so extending that
//! array with a query's local relations, one id per name, continuing from
//! `schema.len()`, is the representation the published `Schema` already
//! commits to rather than a new one invented here. The published [`Schema`]
//! (and the content fingerprint embedded in a database) is untouched: this
//! catalogue only ever *reads* one, never builds or mutates it.
//!
//! **Two identity spaces, kept apart on purpose.** This module's own resolution
//! space (a name to a `PredicateId`, for `lower`/`ty`/`flatten`/diagnostics to
//! consume) is not the [`FactId`](fjord_schema::id::FactId) tag space
//! `FactId::new` bounds at
//! [`MAX_TAGGABLE_PREDICATE`](fjord_schema::id::MAX_TAGGABLE_PREDICATE) — but
//! every `PredicateId` this catalogue mints for a local relation still has to
//! fit that tag, because [item
//! 3](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)'s
//! canonical ids are tagged with it. [`check_tag_space`] is the bound, checked
//! **before** any executable or relation is built:
//!
//! ```text
//! augmented_predicate_count + generated_local_count <= MAX_TAGGABLE_PREDICATE + 1
//! ```
//!
//! **`augmented`, not the database's own count — which this module gets for
//! free.** `fjord-server` appends its own virtual-predicate schema to every
//! database's before compiling a query, so the `&Schema` this catalogue is
//! handed already includes `fjord.db.*` by the time it arrives here; `schema.len()`
//! *is* the augmented count, with no separate accounting owed. A local relation's
//! tags therefore start above every virtual predicate too, which is what keeps
//! them from landing on one.

use fjord_schema::{
    id::MAX_TAGGABLE_PREDICATE,
    schema::{PredicateId, Schema},
};

/// The tag bound item 2 states, checked as pure arithmetic over counts —
/// deliberately not over an actual catalogue, so this can be checked (and
/// tested, at its exact boundary) without allocating the ~16.7 million
/// predicates that would take to reach it for real.
///
/// # Errors
///
/// [`TagSpaceExhausted`] if `augmented_predicate_count + generated_local_count`
/// would exceed [`MAX_TAGGABLE_PREDICATE`] `+ 1` — including if the addition
/// itself would overflow `u64`, which is refused the same way rather than
/// wrapped.
pub fn check_tag_space(
    augmented_predicate_count: u64,
    generated_local_count: u64,
) -> Result<(), TagSpaceExhausted> {
    let max = u64::from(MAX_TAGGABLE_PREDICATE) + 1;
    let requested = augmented_predicate_count.saturating_add(generated_local_count);

    if requested > max {
        return Err(TagSpaceExhausted { requested, max });
    }

    Ok(())
}

/// The named diagnostic [`check_tag_space`] refuses under.
pub const REJECT_TAG_SPACE_EXHAUSTED: &str = "reject/tag-space-exhausted";

/// `requested` predicates (augmented base plus generated local) do not fit the
/// `FactId` tag space, which tops out at `max`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct TagSpaceExhausted {
    pub requested: u64,
    pub max: u64,
}

/// A uniform view of an augmented schema's predicates plus one query's local
/// relations — every name resolves to a `PredicateId` the same way, whichever
/// tier declared it.
///
/// `N` is a name type rather than a hardcoded `String` so a caller can hand
/// this whatever a local relation's name already is (a `Symbol`, once
/// Movement 1 exists) without a conversion at the boundary.
pub struct Catalogue<'a, N> {
    schema: &'a Schema,
    locals: Vec<N>,
}

impl<'a, N: AsRef<str>> Catalogue<'a, N> {
    /// Build a catalogue over `schema` (already augmented with whatever
    /// virtual predicates a server appended) plus `locals`, in declaration
    /// order — the order their `PredicateId`s are minted in, continuing
    /// densely from `schema.len()`.
    ///
    /// # Errors
    ///
    /// [`TagSpaceExhausted`] exactly when [`check_tag_space`] would refuse
    /// `schema.len()` augmented against `locals.len()` generated.
    pub fn build(schema: &'a Schema, locals: Vec<N>) -> Result<Self, TagSpaceExhausted> {
        check_tag_space(schema.len() as u64, locals.len() as u64)?;
        Ok(Catalogue { schema, locals })
    }

    /// The `PredicateId` `name` resolves to, checking the schema (base and
    /// virtual alike) before the query's own locals — a local relation cannot
    /// shadow a schema predicate, because nothing here gives it the chance to
    /// be checked first.
    #[must_use]
    pub fn resolve(&self, name: &str) -> Option<PredicateId> {
        if let Some((id, _)) = self.schema.find_position(name) {
            return Some(id);
        }
        self.local_index(name)
            .map(|i| PredicateId(self.schema.len() as u32 + i as u32))
    }

    /// Whether `id` names one of this catalogue's **local** relations, as
    /// opposed to a schema (base or virtual) predicate — the one place a
    /// caller needs to tell the two tiers apart, mirroring
    /// [`Schema::is_virtual`](fjord_schema::schema::Schema::is_virtual)'s own
    /// shape for the schema's own two tiers.
    #[must_use]
    pub fn is_local(&self, id: PredicateId) -> bool {
        let base = self.schema.len() as u32;
        id.0 >= base && (id.0 as usize) < self.schema.len() + self.locals.len()
    }

    /// How many local relations this catalogue declared.
    #[must_use]
    pub fn local_count(&self) -> usize {
        self.locals.len()
    }

    fn local_index(&self, name: &str) -> Option<usize> {
        self.locals.iter().position(|local| local.as_ref() == name)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use fjord_schema::schema::Predicate;
    use lasso::Rodeo;
    use proptest::prelude::*;
    use std::sync::Arc;

    fn base_schema(names: &[&str]) -> Schema {
        let mut rodeo = Rodeo::new();
        let predicates: Vec<Predicate> = names
            .iter()
            .map(|name| Predicate {
                name: rodeo.get_or_intern(name),
                key: fjord_schema::schema::PredicateTy::Int,
                value: None,
            })
            .collect();
        Schema::new(rodeo.into_reader(), Arc::from(predicates))
    }

    fn augmented_schema(base: &[&str], virtual_names: &[&str]) -> Schema {
        let all: Vec<&str> = base.iter().chain(virtual_names).copied().collect();
        let schema = base_schema(&all);
        let virtuals = virtual_names
            .iter()
            .enumerate()
            .map(|(i, _)| PredicateId((base.len() + i) as u32));
        schema.with_virtual(virtuals)
    }

    // ---- item 2's own named guards: virtual predicates present, the exact
    // boundary, and one past it ------------------------------------------

    #[test]
    fn locals_are_allocated_after_a_schemas_virtual_predicates() {
        let schema = augmented_schema(&["src.File"], &["fjord.db.List"]);
        let catalogue = Catalogue::build(&schema, vec!["Reach"]).unwrap();

        // "Reach" must land at id 2 — after src.File (0) *and* the virtual
        // fjord.db.List (1) — not at id 1, which would collide with the
        // virtual predicate the schema already assigned there.
        assert_eq!(catalogue.resolve("Reach"), Some(PredicateId(2)));
        assert!(catalogue.is_local(PredicateId(2)));
        assert!(!catalogue.is_local(PredicateId(1)));
        assert!(!schema.is_virtual(PredicateId(2)));
        assert!(schema.is_virtual(PredicateId(1)));
    }

    #[test]
    fn the_exact_last_usable_tag_is_accepted() {
        let max = u64::from(MAX_TAGGABLE_PREDICATE) + 1;
        assert_eq!(check_tag_space(max - 1, 1), Ok(()));
        assert_eq!(check_tag_space(max, 0), Ok(()));
    }

    #[test]
    fn one_past_the_last_usable_tag_is_refused() {
        let max = u64::from(MAX_TAGGABLE_PREDICATE) + 1;
        assert_eq!(
            check_tag_space(max, 1),
            Err(TagSpaceExhausted {
                requested: max + 1,
                max
            })
        );
        assert_eq!(
            check_tag_space(max + 1, 0),
            Err(TagSpaceExhausted {
                requested: max + 1,
                max
            })
        );
    }

    #[test]
    fn an_addition_that_would_overflow_u64_is_refused_not_wrapped() {
        let max = u64::from(MAX_TAGGABLE_PREDICATE) + 1;
        let err = check_tag_space(u64::MAX, u64::MAX).unwrap_err();
        assert_eq!(err.max, max);
        assert!(err.requested > max);
    }

    // ---- uniform resolution --------------------------------------------

    #[test]
    fn a_schema_predicate_resolves_before_any_local_is_consulted() {
        let schema = base_schema(&["src.File"]);
        // A local relation cannot claim the same name as a base predicate —
        // resolution must answer the schema's id, not a local one.
        let catalogue = Catalogue::build(&schema, vec!["src.File"]).unwrap();
        assert_eq!(catalogue.resolve("src.File"), Some(PredicateId(0)));
        assert!(!catalogue.is_local(PredicateId(0)));
    }

    #[test]
    fn an_unknown_name_resolves_to_nothing() {
        let schema = base_schema(&["src.File"]);
        let catalogue = Catalogue::build(&schema, vec!["Reach"]).unwrap();
        assert_eq!(catalogue.resolve("nowhere"), None);
    }

    // ---- the oracle: a plain combined dense array ------------------------
    //
    // "PredicateId is a predicate's position in a dense array" read literally:
    // concatenate every schema predicate's name (in id order) with every local
    // name (in declaration order) into one `Vec`, and a name's id is its
    // position in *that* array — computed with no `Catalogue` involved at all.

    fn oracle_array(schema: &Schema, locals: &[&str]) -> Vec<String> {
        let mut names: Vec<String> = (0..schema.len())
            .map(|i| {
                schema
                    .get(PredicateId(i as u32))
                    .and_then(|p| p.name())
                    .expect("every schema position names a predicate")
                    .to_owned()
            })
            .collect();
        names.extend(locals.iter().map(|s| (*s).to_owned()));
        names
    }

    proptest! {
        /// For any base schema plus any local names (distinct from the base
        /// and from each other, so resolution is unambiguous), the catalogue's
        /// `resolve` and `is_local` agree with a plain position lookup into the
        /// concatenated dense array — the defining property of "a predicate
        /// id is a position", extended to locals rather than assumed to hold.
        #[test]
        fn agrees_with_the_combined_dense_array_oracle(
            base in prop::collection::hash_set("[a-z]{1,6}", 0..8)
                .prop_map(|s| s.into_iter().collect::<Vec<_>>()),
            locals in prop::collection::hash_set("[A-Z]{1,6}", 0..8)
                .prop_map(|s| s.into_iter().collect::<Vec<_>>()),
        ) {
            let schema = base_schema(&base.iter().map(String::as_str).collect::<Vec<_>>());
            let local_refs: Vec<&str> = locals.iter().map(String::as_str).collect();
            let catalogue = Catalogue::build(&schema, local_refs.clone()).unwrap();

            let oracle = oracle_array(&schema, &local_refs);

            for (position, name) in oracle.iter().enumerate() {
                let id = PredicateId(position as u32);
                prop_assert_eq!(catalogue.resolve(name), Some(id));
                prop_assert_eq!(catalogue.is_local(id), position >= base.len());
            }

            prop_assert_eq!(catalogue.resolve("nowhere-at-all"), None);
        }
    }
}
