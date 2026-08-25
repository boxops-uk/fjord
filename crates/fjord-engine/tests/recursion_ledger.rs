//! **The recursion work's coverage ledger, written before the subsystems exist.**
//!
//! AGENTS.md: every invariant owns a guard test, written up front; a guard whose
//! subsystem does not exist yet is `#[ignore]`d with the invariant in the message,
//! and `cargo test -- --ignored --list` is the coverage ledger. Movement 0's proof
//! boundary extends that to every assertion its fifteen items defer: each is
//! classified green-here or owned by **exactly one** named ignored guard in a named
//! later movement, and none is left as prose alone. These are those guards — one
//! per deferred row of the proof-boundary table in
//! [`PLAN.md`](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation),
//! plus the two the invariant registry names for itself.
//!
//! **Every body is `unimplemented!`, and that is the point.** An empty body would
//! pass the moment somebody deleted the `#[ignore]`, which is the failure this file
//! exists to make impossible: un-ignoring one of these without writing it turns the
//! suite red. The claim in the attribute is what the test must end up asserting.
//!
//! **They live together here and move when they go live.** A guard belongs with the
//! subsystem it measures — I8's with the store, item 8's with the server — but none
//! of those subsystems exists, and a guard scattered into a crate that cannot yet
//! name what it guards is a guard nobody finds. Un-ignoring one means moving it to
//! its own battery, which is where the movement that owns it will already be
//! working. `scripts/check-guards.py` reads the whole tree, so it does not care
//! where they sit.

// ---- items 1 and 9 — the program ---------------------------------------------

#[test]
#[ignore = "guard: a `with` block's source spelling parses, and the corpus executes it end to end, owned by Movement 7"]
fn a_with_block_parses_and_the_corpus_executes_it() {
    unimplemented!("Movement 7 — the surface")
}

// ---- item 2 — the predicate catalogue -----------------------------------------

#[test]
#[ignore = "guard: a generated magic or delta namespace that exhausts the tag space is refused by name rather than wrapping, owned by Movement 6"]
fn generated_namespace_exhaustion_is_refused_by_name() {
    unimplemented!("Movement 6 — magic sets")
}

// ---- item 3 — canonical identity ----------------------------------------------

#[test]
#[ignore = "guard: a relation's canonical ids are the same under any rule scheduling and either expansion strategy, owned by Movement 3"]
fn canonical_ids_survive_rule_scheduling_and_expansion_strategy() {
    unimplemented!("Movement 3 — semi-naive")
}

// ---- item 4 — the materialisation projection ----------------------------------

#[test]
#[ignore = "guard: the materialisation projection over the full source corpus, scalar and union heads refused by name, owned by Movement 7"]
fn the_materialisation_projection_holds_over_the_source_corpus() {
    unimplemented!("Movement 7 — the surface")
}

// ---- item 5 — the relation representation -------------------------------------

#[test]
#[ignore = "guard: an empty-range seek, a narrow seek, a point lookup and a full scan cost a bounded factor more on an N-round relation than on a batch-built one, and do not grow with N, owned by Movement 1"]
fn a_segmented_relation_reads_within_a_bounded_factor_of_a_batch_built_one() {
    unimplemented!("Movement 1 — the relation store and the overlay")
}

#[test]
#[ignore = "guard: rules of one SCC evaluated in the same round observe one another's accumulated relation and not one another's delta, owned by Movement 3"]
fn simultaneous_scc_rules_see_the_accumulated_relation() {
    unimplemented!("Movement 3 — semi-naive")
}

// ---- item 6 — the budget ------------------------------------------------------

#[test]
#[ignore = "guard: charging the budget is structural — the driver and the generator hold types through which no work can be done without charging it, owned by Movement 2"]
fn the_budget_is_charged_through_a_chokepoint_rather_than_by_convention() {
    unimplemented!("Movement 2 — the naive driver")
}

// ---- item 7 — the magic fallback ----------------------------------------------

#[test]
#[ignore = "guard: a fault injected at any phase of the transformed candidate takes the unmagicked fallback and emits no diagnostic the user did not provoke, owned by Movement 6"]
fn a_failure_in_any_transformed_phase_falls_back_silently() {
    unimplemented!("Movement 6 — magic sets")
}

// ---- item 8 — the executable seam ---------------------------------------------

#[test]
#[ignore = "guard: the plain-path dispatch contract holds through the executable seam, the server and inspection, owned by Movement 8"]
fn the_plain_path_dispatch_contract_holds_end_to_end() {
    unimplemented!("Movement 8 — the executable seam, and inspection")
}

// ---- item 10 — disjunction normalisation --------------------------------------

#[test]
#[ignore = "guard: DNF expansion re-enters every prefix level once per clause, measured with a store spy rather than argued, owned by Movement 3"]
fn dnf_expansion_amplifies_prefix_level_scans() {
    unimplemented!("Movement 3 — semi-naive")
}

// ---- item 11 — demand through negation ----------------------------------------

#[test]
#[ignore = "guard: a magicked program answers what the unmagicked one answers, negative-only and partially-bound predicates included, owned by Movement 6"]
fn magic_answers_what_the_unmagicked_program_answers() {
    unimplemented!("Movement 6 — magic sets")
}

// ---- items 12 and 13 — resume over a program ----------------------------------

#[test]
#[ignore = "guard: a cursor whose program fingerprint has moved is refused, and the envelope carries the world stamp through a program resume, owned by Movement 4"]
fn a_program_cursor_names_the_program_and_the_world_it_was_made_in() {
    unimplemented!("Movement 4 — resume, and I4 re-proved over a Program")
}

#[test]
#[ignore = "guard: `reads_virtual` is computed over generated rules, not only over the rules the user wrote, owned by Movement 8"]
fn reads_virtual_covers_generated_rules() {
    unimplemented!("Movement 8 — the executable seam, and inspection")
}

// ---- item 14 and I8 — one snapshot, owned by the driver -----------------------

#[test]
#[ignore = "guard: one base snapshot is observed by every rule and every round — not one per rule, which would multiply fjall's open-snapshot count, owned by Movement 2"]
fn one_base_snapshot_serves_every_rule_and_every_round() {
    unimplemented!("Movement 2 — the naive driver")
}

/// I8's **second witness**. The existing cross-check watches fjall's own
/// open-snapshot count, which cannot see a derived relation at all: a query-local
/// relation is an engine-side `Arc` with no storage-engine counterpart, so a
/// suspended recursive program could retain every derived tuple while that count
/// reports zero and passes. The two witnesses establish different halves and
/// neither substitutes for the other.
#[test]
#[ignore = "guard: base and relation snapshots are both live during execution and both at zero after an answer-page suspend, a cancellation mid-fixpoint, a materialisation or limit error, and normal completion, owned by Movement 4"]
fn relation_snapshots_are_released_on_every_exit_path() {
    unimplemented!("Movement 4 — resume, and I4 re-proved over a Program")
}

// ---- I9 — the third escape boundary -------------------------------------------

/// I9's recursive-materialisation guard, which the registry names and which no
/// source has held until now. Three measurements rather than one, because the
/// single-level caveat on `scan_is_alloc_free_per_row` multiplies here — a
/// fixpoint opens a level per rule per round.
#[test]
#[ignore = "guard: materialisation allocates with bytes actually retained and with nothing else — not with rejected attempts, not with duplicates, not with rounds, owned by Movement 3"]
fn materialisation_allocates_only_with_retained_bytes() {
    unimplemented!("Movement 3 — semi-naive")
}
