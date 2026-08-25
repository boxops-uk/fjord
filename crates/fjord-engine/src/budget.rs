//! **Item 6 of the recursion plan: resource limits as a chokepoint, not a
//! convention.**
//!
//! Six limits exist; this module owns five of them. The rows-examined ceiling
//! (`Executor::with_examined_ceiling`, in [`crate::iter`]) is the sixth and is
//! already shipped — it is scoped to *one executor*, while every limit here is
//! scoped to *one fixpoint derivation* (a driver seeding many executors, one per
//! rule per round) or to *one compilation*, so it is deliberately not folded in
//! here.
//!
//! **All five are deployment policy, not semantics: none enters the program
//! fingerprint, and a limit refuses without changing an answer.** That
//! classification is argued in the plan text this module implements, not
//! repeated here.
//!
//! Two types, because the five limits fall into two lifetimes that must not be
//! confused: [`CompileBudget`] resets once per **compilation** and bounds only
//! generated-program size (adorned, magic and supplementary relations, produced
//! before any executor runs). [`DerivationBudget`] resets once per **chunk** and
//! bounds the other four — retained facts, retained bytes, rule-output attempts,
//! fixpoint rounds — all charged while one fixpoint is actually being derived.
//! Merging them into one type would let a caller reset the wrong half, or forget
//! to reset the right one; keeping them apart makes that a type error instead of
//! a bug report.
//!
//! **The chokepoint.** Every counter here is a private field. The only way to
//! move one is a `charge_*` method that returns whether the result is still
//! within the configured limit — there is no setter, no `pub` field, and no way
//! to construct a budget already holding an arbitrary tally. A driver or
//! generator that holds a `&mut DerivationBudget` (Movement 2) therefore has no
//! way to retain a tuple, allocate a generated rule, or copy between round
//! snapshots without going through a method that counts it — the same shape
//! [item 15](../../../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)
//! already used: an illegal state (an uncharged retention) that cannot be
//! constructed beats one a reviewer has to notice is missing.
//!
//! **A charge always applies, and the error is a signal rather than a veto.**
//! The resource it counts — a tuple already retained, a relation already
//! allocated — exists by the time the caller charges for it; a budget cannot
//! un-allocate it by refusing the call. So every `charge_*` method updates its
//! counter unconditionally and then reports whether the *result* is within the
//! limit, exactly as `outcome: limit/retained-facts` in the plan's table reads —
//! an outcome of derivation, not a permission gate in front of it.

use std::collections::HashMap;

use fjord_schema::id::MAX_FACT_SEQUENCE;

/// Which of the five limits this module owns was exceeded, named exactly as the
/// plan's own `Outcome` column.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Limit {
    RetainedFacts,
    RetainedBytes,
    RuleAttempts,
    Rounds,
    GeneratedProgram,
}

impl Limit {
    /// The diagnostic code a refusal on this limit is reported under.
    #[must_use]
    pub fn code(self) -> &'static str {
        match self {
            Limit::RetainedFacts => "limit/retained-facts",
            Limit::RetainedBytes => "limit/retained-bytes",
            Limit::RuleAttempts => "limit/rule-attempts",
            Limit::Rounds => "limit/rounds",
            Limit::GeneratedProgram => "limit/generated-program",
        }
    }
}

/// The four fixpoint-derivation-scoped limits, as configured — a `DerivationBudget`
/// counts against a fixed copy of these for the whole derivation it charges for.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct DerivationLimits {
    retained_facts: u64,
    retained_bytes: u64,
    rule_attempts: u64,
    rounds_per_scc: u64,
}

impl DerivationLimits {
    /// Configure the four limits, clamping `retained_facts` to
    /// [`MAX_FACT_SEQUENCE`] — a relation cannot hold more tuples than the id
    /// space [`crate::canonical_id`] can rank, so a caller-supplied limit above it
    /// is not a stricter policy, it is a limit finalisation could never actually
    /// reach. Clamping here is what item 3 means by "finalisation has no failure
    /// to report": the overflow [`fjord_schema::id::FactIdError`] finalisation
    /// could otherwise hit is refused earlier, by name, through a limit that
    /// already exists.
    #[must_use]
    pub fn new(
        retained_facts: u64,
        retained_bytes: u64,
        rule_attempts: u64,
        rounds_per_scc: u64,
    ) -> Self {
        DerivationLimits {
            retained_facts: retained_facts.min(MAX_FACT_SEQUENCE),
            retained_bytes,
            rule_attempts,
            rounds_per_scc,
        }
    }
}

/// An opaque handle to one strongly connected component in a program's
/// dependency graph — a driver (Movement 2) mints and hands these back; this
/// module only uses one to key a per-SCC round tally, per the plan's own
/// `Charged over: each SCC` reading of the rounds limit.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct SccId(pub usize);

/// The four fixpoint-derivation-scoped counters, reset once per chunk.
///
/// See the module doc for why this is a separate type from [`CompileBudget`]
/// rather than a fifth field on it.
#[derive(Debug, Clone)]
pub struct DerivationBudget {
    limits: DerivationLimits,
    retained_facts: u64,
    retained_bytes: u64,
    peak_retained_bytes: u64,
    rule_attempts: u64,
    rounds: HashMap<SccId, u64>,
}

impl DerivationBudget {
    /// A fresh budget, every counter at zero, for one fixpoint derivation.
    #[must_use]
    pub fn new(limits: DerivationLimits) -> Self {
        DerivationBudget {
            limits,
            retained_facts: 0,
            retained_bytes: 0,
            peak_retained_bytes: 0,
            rule_attempts: 0,
            rounds: HashMap::new(),
        }
    }

    /// Charge `n` more tuples as retained — **monotonic**, because nothing is
    /// ever removed from an accumulated relation mid-derivation. Charging zero
    /// is legal and simply reports the current state, which is what lets a
    /// caller probe "are we still under the limit" without inventing a reading
    /// that is not also a charge.
    pub fn charge_retained_facts(&mut self, n: u64) -> Result<(), Limit> {
        self.retained_facts = self.retained_facts.saturating_add(n);
        if self.retained_facts > self.limits.retained_facts {
            return Err(Limit::RetainedFacts);
        }
        Ok(())
    }

    /// Charge (or, with a negative `delta`, release) encoded bytes against the
    /// **live** total — unlike retained facts, this one genuinely goes down: a
    /// per-round snapshot or candidate-dedup buffer from an earlier round can be
    /// dropped once a later round no longer needs it, while the facts it
    /// produced stay retained forever. [`Self::peak_retained_bytes`] is the
    /// running maximum of the live total, for reporting a number that does not
    /// change depending on when it is read.
    pub fn charge_retained_bytes(&mut self, delta: i64) -> Result<(), Limit> {
        self.retained_bytes = self.retained_bytes.saturating_add_signed(delta);
        self.peak_retained_bytes = self.peak_retained_bytes.max(self.retained_bytes);
        if self.retained_bytes > self.limits.retained_bytes {
            return Err(Limit::RetainedBytes);
        }
        Ok(())
    }

    /// Charge `n` more rule-output attempts — monotonic over the whole
    /// executable, whichever rule produced them.
    pub fn charge_rule_attempts(&mut self, n: u64) -> Result<(), Limit> {
        self.rule_attempts = self.rule_attempts.saturating_add(n);
        if self.rule_attempts > self.limits.rule_attempts {
            return Err(Limit::RuleAttempts);
        }
        Ok(())
    }

    /// Charge one more round for `scc` — a defensive backstop, so a stratum
    /// that fails to converge is refused by a count rather than left to spin.
    /// Independent per SCC: a slow-converging stratum does not spend a
    /// fast-converging sibling's budget.
    pub fn charge_round(&mut self, scc: SccId) -> Result<(), Limit> {
        let count = self.rounds.entry(scc).or_insert(0);
        *count = count.saturating_add(1);
        if *count > self.limits.rounds_per_scc {
            return Err(Limit::Rounds);
        }
        Ok(())
    }

    #[must_use]
    pub fn retained_facts(&self) -> u64 {
        self.retained_facts
    }

    #[must_use]
    pub fn retained_bytes(&self) -> u64 {
        self.retained_bytes
    }

    /// The highest `retained_bytes` has ever been, independent of what it is
    /// right now — the number `retained encoded bytes, peak live` in the plan's
    /// table names.
    #[must_use]
    pub fn peak_retained_bytes(&self) -> u64 {
        self.peak_retained_bytes
    }

    #[must_use]
    pub fn rule_attempts(&self) -> u64 {
        self.rule_attempts
    }

    #[must_use]
    pub fn rounds(&self, scc: SccId) -> u64 {
        self.rounds.get(&scc).copied().unwrap_or(0)
    }
}

/// The one compile-scoped limit: generated-program size, charged incrementally
/// as adornment and delta-generation allocate relations and rules, reset once
/// per **compilation** rather than once per chunk — a compiled executable can
/// be chunked many times, and re-measuring generation on every chunk would
/// re-run work Movement 6 does exactly once.
///
/// **What this limit means on overflow is not this type's decision.** The plan
/// (item 7) makes overflow terminal for the mandatory expansion and a fallback
/// trigger for the magic rewrite — two different meanings for the same
/// [`Limit::GeneratedProgram`] outcome, decided by *which candidate* was being
/// generated when the charge failed. That is a fact about the compiler
/// pipeline, not about counting, so this type only ever reports whether the
/// limit was hit.
#[derive(Debug, Clone)]
pub struct CompileBudget {
    limit: u64,
    generated: u64,
}

impl CompileBudget {
    #[must_use]
    pub fn new(limit: u64) -> Self {
        CompileBudget {
            limit,
            generated: 0,
        }
    }

    /// Charge `n` more generated relations or rules.
    pub fn charge_generated(&mut self, n: u64) -> Result<(), Limit> {
        self.generated = self.generated.saturating_add(n);
        if self.generated > self.limit {
            return Err(Limit::GeneratedProgram);
        }
        Ok(())
    }

    #[must_use]
    pub fn generated(&self) -> u64 {
        self.generated
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use proptest::prelude::*;

    // ---- item 3 cross-reference: the clamp -----------------------------

    #[test]
    fn retained_facts_limit_above_the_id_space_is_clamped() {
        let limits = DerivationLimits::new(MAX_FACT_SEQUENCE + 1_000_000, 0, 0, 0);
        let mut budget = DerivationBudget::new(limits);

        // Charging exactly MAX_FACT_SEQUENCE must still succeed — the clamp
        // must not have rounded *down* past the real ceiling.
        assert_eq!(budget.charge_retained_facts(MAX_FACT_SEQUENCE), Ok(()));
        // One more must fail, proving the configured limit really did land at
        // MAX_FACT_SEQUENCE and not at the caller's oversized request.
        assert_eq!(budget.charge_retained_facts(1), Err(Limit::RetainedFacts));
    }

    proptest! {
        /// However large a caller's requested retained-facts limit is, the
        /// configured limit this budget actually enforces never exceeds the id
        /// space — the property behind the single pinned example above.
        #[test]
        fn the_configured_retained_facts_limit_never_exceeds_the_id_space(
            requested in any::<u64>()
        ) {
            let limits = DerivationLimits::new(requested, u64::MAX, u64::MAX, u64::MAX);
            let mut budget = DerivationBudget::new(limits);
            // Push it to exactly the id space: must always be accepted...
            prop_assert_eq!(budget.charge_retained_facts(MAX_FACT_SEQUENCE), Ok(()));
            // ...and one more must always be refused, whatever was requested.
            prop_assert_eq!(budget.charge_retained_facts(1), Err(Limit::RetainedFacts));
        }
    }

    // ---- the oracle: an i128 running total against the limit -----------
    //
    // A completely different accounting mechanism from the saturating-u64
    // counters above — wide enough that overflow is not a question the oracle
    // has to answer the same way the real type does, so agreement between them
    // is a property of the *policy*, not of shared overflow handling.

    fn oracle_outcomes(limit: u64, charges: &[u64]) -> Vec<bool> {
        let mut total: i128 = 0;
        charges
            .iter()
            .map(|&n| {
                total += i128::from(n);
                total <= i128::from(limit)
            })
            .collect()
    }

    proptest! {
        #[test]
        fn retained_facts_matches_the_running_total_oracle(
            limit in 0u64..2000,
            charges in prop::collection::vec(0u64..50, 0..80),
        ) {
            let mut budget = DerivationBudget::new(DerivationLimits::new(limit, u64::MAX, u64::MAX, u64::MAX));
            let expected = oracle_outcomes(limit, &charges);

            let actual: Vec<bool> = charges
                .iter()
                .map(|&n| budget.charge_retained_facts(n).is_ok())
                .collect();

            prop_assert_eq!(actual, expected);
        }

        #[test]
        fn rule_attempts_matches_the_running_total_oracle(
            limit in 0u64..2000,
            charges in prop::collection::vec(0u64..50, 0..80),
        ) {
            let mut budget = DerivationBudget::new(DerivationLimits::new(u64::MAX, u64::MAX, limit, u64::MAX));
            let expected = oracle_outcomes(limit, &charges);

            let actual: Vec<bool> = charges
                .iter()
                .map(|&n| budget.charge_rule_attempts(n).is_ok())
                .collect();

            prop_assert_eq!(actual, expected);
        }

        #[test]
        fn generated_program_matches_the_running_total_oracle(
            limit in 0u64..2000,
            charges in prop::collection::vec(0u64..50, 0..80),
        ) {
            let mut budget = CompileBudget::new(limit);
            let expected = oracle_outcomes(limit, &charges);

            let actual: Vec<bool> = charges
                .iter()
                .map(|&n| budget.charge_generated(n).is_ok())
                .collect();

            prop_assert_eq!(actual, expected);
        }

        /// The signed-delta oracle for retained bytes: a plain `i128` ledger
        /// that goes up and down exactly as the deltas say, compared against
        /// the real type's saturating live total.
        #[test]
        fn retained_bytes_matches_a_signed_running_total_oracle(
            limit in 0u64..2000,
            deltas in prop::collection::vec(-50i64..50, 0..80),
        ) {
            let mut budget = DerivationBudget::new(DerivationLimits::new(u64::MAX, limit, u64::MAX, u64::MAX));
            let mut oracle_total: i128 = 0;
            let mut oracle_peak: i128 = 0;

            for &delta in &deltas {
                oracle_total = (oracle_total + i128::from(delta)).max(0);
                oracle_peak = oracle_peak.max(oracle_total);
                let expected_ok = oracle_total <= i128::from(limit);

                let actual_ok = budget.charge_retained_bytes(delta).is_ok();
                prop_assert_eq!(actual_ok, expected_ok);
            }

            prop_assert_eq!(i128::from(budget.retained_bytes()), oracle_total);
            prop_assert_eq!(i128::from(budget.peak_retained_bytes()), oracle_peak);
        }

        /// Rounds are charged **per SCC**, so two SCCs must never share a
        /// budget — the property that makes "each SCC" in the plan's table
        /// mean something rather than reading as "each fixpoint".
        #[test]
        fn rounds_are_independent_per_scc(
            limit in 0u64..20,
            a_rounds in 0u64..40,
            b_rounds in 0u64..40,
        ) {
            let mut budget = DerivationBudget::new(DerivationLimits::new(u64::MAX, u64::MAX, u64::MAX, limit));
            let (scc_a, scc_b) = (SccId(0), SccId(1));

            let mut a_ok = true;
            for _ in 0..a_rounds {
                a_ok = budget.charge_round(scc_a).is_ok();
            }
            let mut b_ok = true;
            for _ in 0..b_rounds {
                b_ok = budget.charge_round(scc_b).is_ok();
            }

            prop_assert_eq!(a_ok, a_rounds <= limit);
            prop_assert_eq!(b_ok, b_rounds <= limit);
            prop_assert_eq!(budget.rounds(scc_a), a_rounds);
            prop_assert_eq!(budget.rounds(scc_b), b_rounds);
        }
    }

    /// A budget that has already exceeded a **monotonic** limit stays
    /// exceeded — nothing can bring retained facts, rule attempts or a
    /// generated-program count back down within one derivation.
    #[test]
    fn a_monotonic_limit_stays_exceeded_once_hit() {
        let mut budget =
            DerivationBudget::new(DerivationLimits::new(5, u64::MAX, u64::MAX, u64::MAX));
        assert_eq!(budget.charge_retained_facts(6), Err(Limit::RetainedFacts));
        // Charging zero more still reports over-limit rather than resetting.
        assert_eq!(budget.charge_retained_facts(0), Err(Limit::RetainedFacts));
    }

    /// Retained bytes is the one live gauge: a release can bring it back
    /// under the limit within the same derivation, and a charge after that
    /// succeeds again. This is the behaviour the "peak live" qualifier in the
    /// plan's table exists to distinguish from the other four counters.
    #[test]
    fn retained_bytes_recovers_after_a_release() {
        let mut budget =
            DerivationBudget::new(DerivationLimits::new(u64::MAX, 100, u64::MAX, u64::MAX));
        assert_eq!(budget.charge_retained_bytes(150), Err(Limit::RetainedBytes));
        assert_eq!(budget.charge_retained_bytes(-100), Ok(())); // live: 50
        assert_eq!(budget.charge_retained_bytes(40), Ok(())); // live: 90
        // The peak recorded while over the limit is remembered even after
        // recovering.
        assert_eq!(budget.peak_retained_bytes(), 150);
    }

    #[test]
    fn charging_zero_retained_facts_probes_without_moving_the_counter() {
        let mut budget =
            DerivationBudget::new(DerivationLimits::new(10, u64::MAX, u64::MAX, u64::MAX));
        assert_eq!(budget.charge_retained_facts(0), Ok(()));
        assert_eq!(budget.retained_facts(), 0);
    }
}
