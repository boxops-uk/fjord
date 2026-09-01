# Cost-based reorder — issue #18 (revision 7)

## Decision and proof boundary

`reorder` takes the lowest-numbered member of the runnable frontier. That is a
deterministic legality rule, not a cost model. This work replaces the tie-break with a
costed choice without changing the set of legal orders or the executor.

The implementation is staged as seven independently reviewable runs, numbered 0–6.
Every run has one falsifiable claim, a mechanical gate for that claim, and an explicit
statement of what later runs may rely on.

Two limits are part of the specification:

1. The optimiser is **exact under its declared, clamped estimate model only when it
   exhausts every legal candidate within its budgets**. On overflow it discards the
   entire search result and runs deterministic greedy selection from the beginning.
2. No statistics-free estimate can promise universal runtime improvement. The
   optimiser preserves answers, is exact under its model when exhaustive, and improves
   named scaling witnesses. The book states that a different data distribution may
   favour another legal plan.

### Revision 7's central correction: cost finalized plans, not prefixes

A later chased bind can reuse a fetch emitted by an earlier access chain and append
residuals to that already-emitted level (`flatten.rs:2020-2032`). Those residuals run at
the earlier level and can reduce the number of times every intervening inner level runs.
Therefore this recurrence from earlier revisions is invalid:

```text
cost(S + m) = cost(S) + card(S) * access(m | state(S))
```

The cost already charged to a prefix can change when a later statement amends it. A
larger `PlanningState` does not repair the recurrence unless it also supports exact
retroactive recomputation of every affected downstream contribution, and no such proof
exists.

Revision 7 consequently does **not** merge prefixes and does **not** claim a `2^n`
subset DP. It enumerates complete legal orders within explicit budgets, fully lowers
each order, lets every fetch amendment land, and only then costs the finalized operator
sequence. This is factorial in the worst case and bounded in fact; exactness is claimed
only when enumeration completes. A future subset DP may replace it only after proving
the Bellman property against the finalized-plan oracle built here.

### The permanent acceptance artifact: the cost-plan corpus

`fjord_engine::cost::corpus` is a table of complete planning cases. Each entry contains:

```text
name
schema source
query source
fixture facts
statistics mode and values
baseline forced order
expected selected order and selection mode
expected baseline and selected abstract costs
expected baseline and selected complete PlanView snapshots
expected baseline and selected fingerprints
expected baseline rows in execution order
expected selected rows in execution order
expected semantic result multiset
optional exact Profile/store-operation counts for performance witnesses
coverage tags
```

`selection mode` is one of `Exhaustive`, `GreedyStatementLimit`,
`GreedyCandidateBudget`, `GreedyTransitionBudget`, `BaselineCandidateFault`, or
`Unorderable`. Planned cases state every field above. An `Unorderable`/diagnosed entry
instead states the exact diagnostic code and span and proves that neither baseline nor
selected compilation produced a plan. A case cannot say only "supported".

The baseline is not compiled through a retained production chooser. Its order is data in
the entry and is handed to `flatten_in_order`, so changing the optimiser cannot silently
move both sides of the comparison. The selected side is compiled through the real
production entry point. Both plans execute against the entry's store and must return
their separately stated ordered rows; sorting those rows must produce the one stated
semantic multiset. Reordering may legitimately change traversal order, so one shared
ordered expectation would either reject correct plans or hide the difference by sorting
too early.

Full structural snapshots use `fjord-inspect`'s interner-free JSON view. The corpus data
lives in `fjord-engine`; the snapshot runner lives in `fjord-inspect/tests`, which is the
first crate allowed to see both the engine and its JSON view. Snapshot regeneration is an
explicit command that writes to a temporary candidate file; the test never blesses a
change automatically. Like the existing language corpus, the cost corpus is exposed only
under `cfg(test)` or the `proptest` support feature, not linked into production binaries.

A census fails unless the corpus reaches every item in this taxonomy:

- physical access: empty source, full scan, partial prefix seek, point seek, bounded
  range, guided seek, fetch, and probe;
- lowering: one and several levels, disjunction, empty disjunction, whole-key variable,
  nested record, repeated variable, intra-row residual, alias, derive, head fetch,
  lookup chase, access-chain fetch, reused fetch, and reused fetch amended once and more
  than once;
- filters: positive constraint at a seek, positive residual constraint, denial,
  constant comparison, field/constant comparison, two-register comparison, derived
  comparison, negation, and residual-order sensitivity;
- selection: source order wins, a later statement wins, a genuine cost tie, folded
  statements in every legal region, an unorderable query, each exact budget boundary,
  each one-past fallback, injected candidate failure, and discovery-order perturbation;
- arithmetic: zero, one, every nominal constant, every clamp, and `u64::MAX`;
- statistics: nominal, exact zero, exact skew, legacy/no statistics, and two
  content-equivalent builds with different physical fact ids;
- compatibility: unchanged fingerprint, deliberately changed fingerprint, native/wasm
  equality, resume success, and resume refusal.

The census is about populations as well as names: generated companions must reach each
shape a minimum number of times, and mutation controls must make the relevant gate fail
when an access rank, factor attachment, amendment, tie-break, clamp, fingerprint tag, or
fallback boundary is changed.

---

## Run 0 — virtual ids are functions of rows, not iterator order

**Claim:** every permutation of the same virtual rows produces identical scan rows,
`FactId` mappings, per-table digests, and point-read answers.

`Table::of` currently allocates ids before sorting. Encode key bytes first, sort them,
then enumerate from one to mint dense ids. The table digest continues to hash the
predicate and sorted keys; ids are now a function of those keys.

### Proof

- A generated permutation property compares the complete `(key, FactId)` scan, digest,
  and every valid and one-past point read across all permutations. It includes empty,
  singleton, duplicate-identical rows, several rows, and both current virtual tables.
- A legacy/new resume case constructs the same plan, same world digest, and same saved
  key under the two id assignments, proves those premises, then requires
  `BadResumeKey`. A plan or world mismatch is not allowed to make the test pass.
- A fetch case proves a stale virtual id is refused through the per-predicate digest.

No `CURSOR_VERSION` bump unless the forced legacy/new case is accepted. Record the
shipped defect and its guard in PLAN.md.

**Exported fact for Run 5:** for a virtual table, equal sorted row bytes imply equal
`key -> FactId` mappings under the repository's accepted 64-bit digest collision model.

---

## Run 1 — finalized-plan cost specification and independent complete-order oracle

**Claim:** the abstract cost of a finalized plan is total and deterministic, and the
reference evaluator assigns the same declared cost without using production costing
machinery.

New `fjord-engine::cost`, used by no production chooser yet.

### Strong units and the objective

The optimisation objective is estimated **rows examined**, the quantity
`Profile::total` measures. Use distinct `Rows`, `Fanout`, `Selectivity`, and
`ExaminedCost` newtypes. `Selectivity` is a reduced integer ratio; application uses
zero-preserving ceiling division. All multiplication and addition saturate at declared
clamps. There is no `f64`, platform-sized arithmetic, random iteration order, or
backend-dependent value.

One module-doc audit table states every nominal base, fan-out, filter selectivity, probe
survival estimate, and clamp. Each constant has a named Run 1 corpus/audit case. Boundary
properties cover zero, one, numerator/denominator boundaries, every clamp, and
`u64::MAX` for both cardinality and accumulated cost. Run 3 adds its search budgets and
greedy ranks to the same table when they acquire a production consumer.

### Finalized operator trace

Cost is evaluated over an internal, interner-free `FinalizedCostPlan`, in the physical
order the executor will run:

```rust
struct FinalizedCostPlan {
    operators: Box<[CostOperator]>,
}

enum CostOperator {
    Level { sources: Box<[CostSource]> },
    Probe { sources: Box<[CostSource]>, survival: Selectivity },
    Derive,
    Compare { survival: Selectivity },
}
```

Each `CostSource` carries its physical access class and the ordered semantic factors
that actually run there. Alternatives sum. A fetch reads at most one row. An
access-chain fetch is cardinality-neutral. A chased predicate contributes its semantic
predicate base and functional-dependency factor at the fetch it ultimately amends.
Constraints attach to the capture that applies them; comparisons attach to the seek or
later residual that applies them; negation is a probe plus a survival filter. Empty
sources produce zero rows.

Walking the finalized sequence maintains estimated input rows. For each operator it
adds `input_rows * examined_per_invocation`, then applies the operator's ordered output
factors before visiting its inner successor. Because the walk happens after lowering,
a chased residual attached to an earlier fetch reduces every downstream invocation it
actually precedes.

Factors are canonical semantic identities, not "whichever statement saw this
variable". A variable shared by `k` relational occurrences gets the declared
`k`-occurrence join factor/hyperfactor once, not an accidental set of pairwise factors.
Constants require no producer. The module's factor table defines activation and
attachment for unary, binary, multiway, functional-dependency, and constant factors.

### Independent oracle

Tests contain a deliberately slow `ReferenceOrderEvaluator`. Given the collected
semantic statements and one complete order, it builds its own small operator list and
applies the audit-table formula directly. It must not call or construct
`FinalizedCostPlan`, `CostOperator`, production transition/emission helpers, access
classification helpers, production factor activation, or production arithmetic beyond
the strong-unit constructors.

Run 1 proves the reference evaluator itself with hand-calculated audit cases for every
factor and operator rule, algebraic properties for factor canonicalisation and total
arithmetic, and a tiny token interpreter: within deliberately small bounds the
interpreter materialises `input_rows` unit tokens, applies each declared operator/factor
one token at a time, and counts examined tokens directly.

For small generated abstract cases:

1. enumerate every legal complete order independently;
2. evaluate each with the reference evaluator;
3. compare every operator's breakdown with the token interpreter and the hand-stated
   boundary cases, not only the winning total;
4. choose the minimum `(cost, order)` independently and pin the winner for the audit
   graphs.

Run 2, not this run, compares those saved reference breakdowns with production-lowered
plans. Run 1 therefore finishes with a proved specification and oracle while still
depending on no production emitter that does not exist yet.

The Run 1 census covers the access, lowering, filter, factor, and arithmetic portions of
the issue-level taxonomy above. Run 3 adds selection/fallback coverage and Run 6 adds
exact-statistics coverage; Run 1 does not claim populations for subsystems that do not
exist yet. Explicit counterexamples pin:

- a product of per-statement access estimates is not a set cardinality;
- a later chased bind amends an earlier fetch and changes the cost of an intervening
  level already visited in statement order;
- three relational occurrences of one variable apply one declared multiway rule;
- a clamped tie falls back to lexicographic source order.

Mutation controls alter the reference rule, token rule, or a hand-stated expected
breakdown one at a time and must make the comparison fail. Run 2 adds mutations between
the reference and production implementations. This is what licenses the word
"independent".

**Exported facts for Runs 2–3:** the finalized-plan formula is total; the reference
complete-order evaluator is the oracle for optimality; no prefix-substructure property
is assumed.

---

## Run 2 — one annotated emitter, chooser unchanged, baseline corpus frozen

**Claim:** producing a finalized cost trace alongside a plan preserves every current
plan, diagnostic, fingerprint, and answer, and the trace describes the plan it
accompanies.

Refactor emission around an internal annotated body. Operator provenance and semantic
factor identities live only while planning; stripping annotations yields the existing
`Plan`. Fetch reuse and amendments mutate this one body, so plan and cost trace cannot
disagree because two builders made different decisions.

Collection produces an immutable `EmissionSeed`. Every forced candidate starts with a
fresh emitter holding its own bindings, fetched map, folded factors, annotated body, and
scratch diagnostics. Candidate evaluation cannot mutate the shared interner, leak a
register number or diagnostic into the next candidate, or choose the winner by discovery
side effect. Only the already-collected public diagnostics and the selected plan escape.

The production chooser remains the current lowest-runnable frontier for all of Run 2.

### Proof

- Retain the old emitter as test-only `legacy_emit` from this run onward. Differentially
  compare old and annotated emitters over the canonical schema-first generator extended to include
  `Alias`, `Derive`, binary `Compare`, access-chain fetch, reused fetch, and multiple
  amendments, across **every safe forced order**.
- Compare the complete `Plan`, fingerprint, rendered diagnostics including spans for
  unsafe/unsupported generated cases, and executed rows. A fixed corpus is not an
  alternative to this differential.
- Independently classify the stripped concrete plan and compare each finalized cost
  operator's access kind, physical position, sources, residual order, and amendment
  target. This classifier may inspect `Plan` but not emitter annotations.
- Compare every generated order's production cost breakdown with Run 1's reference
  evaluator.
- Run the existing compiler/executor model, permutation, resume, corpus, allocation,
  decode, and store-spy batteries unchanged.

### Baseline cost-plan corpus

Create every nominal/core corpus entry now, before changing the chooser. Run 6 adds the
exact-statistics entries before enabling statistics-based choice. Freeze for each entry:

- the explicit baseline forced order;
- its complete PlanView snapshot and fingerprint;
- its abstract cost breakdown;
- its exact ordered rows and semantic multiset;
- exact profiles/store calls for the named performance witnesses.

The selected fields temporarily equal the baseline. The Run 2 gate rejects any plan or
fingerprint churn.

Keep the old emitter permanently as `legacy_emit` under test configuration, frozen to
the pre-refactor surface and excluded from production/rustdoc builds. Its generated
forced-order differential remains a green proof rather than a test deleted after it once
passed. The explicit baseline orders and snapshots are the second, reviewable proof and
remain permanently too.

**Exported facts for Run 3:** any safe forced order lowers to the same plan as before;
its finalized cost trace faithfully describes that plan; the baseline half of every
acceptance case is immutable test data.

---

## Run 3 — bounded complete-order optimisation and greedy fallback

**Claim:** exhaustive selection returns the globally cheapest legal finalized plan under
the declared model; every fallback is deterministic and legal; no selection changes an
answer.

### Search

Admission is deliberately conservative. First compute the old lowest-runnable baseline
order and run the unchanged safety check and annotated emitter once with public
diagnostics. If it is unsafe, diagnosed, or produces no plan, return exactly that result
and do not search. The optimiser therefore cannot make an unsupported construct appear
supported by choosing around its diagnostic, or change which variable/span an
unorderable query reports.

Partition out only statements already proved placement-inert: `Constrain` and `Compare`.
Enumerate complete orders of the remaining order-relevant statements with the same
monotone legality condition as today: a statement is extendable exactly when all its
`reads` are bound. Lower and cost each complete candidate from a fresh Run 2
`EmissionSeed`. Do not merge equal subsets, equal bound sets, or equal-looking planning
states.

Budgets are explicit and independently counted:

- `MAX_ORDERED_STATEMENTS` before enumeration begins;
- `MAX_SEARCH_TRANSITIONS` for legal prefix extensions visited;
- `MAX_COMPLETE_CANDIDATES` for complete plans lowered and costed.

Crossing any budget discards every candidate and best-so-far value and starts the greedy
fallback from the empty order. There is no partial-search/greedy hybrid. Exhaustive
selection ties on `(ExaminedCost, complete_order)` lexicographically.

If a safe candidate unexpectedly fails in its scratch emitter after the admitted
baseline succeeded, discard the search and return the already-built baseline plan as
`BaselineCandidateFault`. This is a fail-safe compiler path, not a user diagnostic.
Fault injection makes it mechanically reachable; the generated every-safe-order
differential proves ordinary inputs never take it.

The fallback repeatedly selects the runnable statement with minimum
`(immediate_access_rank, source_index)`, rebuilding the local emitted effect
against the state at that point. It makes no optimality claim. An empty frontier emits
the untouched remainder in collection order so the existing flatten safety pass reports
the original variable and span.

Placement-inert statements are reinserted at their earliest legal position, stable by
source index. Their semantic factors were collected from the whole body and attach where
the finalized plan applies them, not where their syntax is reinserted. Admission has
already returned on an unbound read, so every such statement now has a legal position;
an assertion over the returned full permutation proves none was omitted.

### Selector proofs

- For generated dependency graphs, every mode returns a complete permutation.
- The result respects the graph exactly when the independent antichain oracle says an
  order exists, for exhaustive search and every fallback reason.
- Exhaustive results match Run 1's independently enumerated winner, including the full
  cost breakdown.
- Exact boundary and one-past tests exist for all three budgets. One-past proves the
  partial winner is discarded by choosing a case where it differs from fresh greedy.
- An injected candidate-emission failure proves the admitted baseline plan is returned
  whole, with no scratch diagnostic leakage.
- Perturbing candidate-discovery order leaves the exhaustive winner and every fallback
  result unchanged.
- `every_legal_placement_of_a_folded_statement_compiles_identically` compares complete
  plans, fingerprints, diagnostics, and rows. A separate invalid-query property pins the
  original diagnostic code and span after reinsertion.

### End-to-end cost-plan corpus acceptance

Fill each entry's expected selected order, mode, cost, full PlanView, fingerprint, and
rows. For every entry the gate:

1. compiles the explicit baseline order through `flatten_in_order`;
2. compiles normally through the production optimiser;
3. compares both full plans and fingerprints to their snapshots;
4. independently recomputes both abstract costs;
5. asserts exhaustive selections are the global oracle winner, or proves the exact
   fallback reason and fresh-greedy result;
6. executes both plans, compares each with its separately stated ordered rows, and
   compares both sorted results with the stated semantic multiset;
7. for witnesses, compares exact `Profile::total` and store-operation counts and proves
   the improvement grows with fixture scale.

The corpus includes an honest hostile-distribution entry where nominal costing chooses a
slower legal plan; its answers must still agree and the book uses it to state the proof
boundary. It is not marked as an optimiser failure.

### Fingerprint and cross-target proof

Plan order and every changed residual/source position already participate in the plan
fingerprint. A moved selected plan therefore moves the fingerprint and refuses an old
cursor as `CursorPlan`; unchanged plans retain their exact value. No cursor layout
changes, so no `CURSOR_VERSION` bump.

The native and real wasm compilers consume the same serialized corpus inputs and emit
the selected order, selection mode, full PlanView JSON, cost breakdown, and fingerprint.
`web` smoke compares the two outputs byte for byte in the same gate. The comparison
includes every arithmetic boundary and selection/fallback taxonomy entry, not only the
language corpus.

Rewrite `query-efficiency.md`; retain the key-order warning and state the exhaustive and
runtime proof boundaries. Update PLAN.md's cost-model decision.

**Exported facts for Run 6:** default/nominal planning is deterministic; exhaustive mode
is globally optimal under the model; every fallback is deterministic; all modes preserve
answers and fingerprint/resume safety.

---

## Run 4 — exact per-predicate counts in the one seal walk

**Claim:** a sealed database records one exact count for every non-virtual declared
predicate, and old sidecars remain readable.

Initialize a count slot from the embedded schema for every non-virtual predicate,
including zero-count predicates. Increment the slot in `identity::compute`'s existing
fact walk. Return the ordered counts with `Identity` and carry them through `Finished`.
`record` writes identity, total count, per-predicate counts, bytes, and the status flip in
its existing single atomic sidecar write.

```rust
struct PredicateCount {
    id: PredicateId,
    name: String,
    facts: u64,
}
```

`Meta` gains `predicates: Option<Box<[PredicateCount]>>` with `default` and
`skip_serializing_if`; no version bump. The list is in predicate-id/declaration order.
Virtual predicates never appear. At construction and load-for-planning, validate exactly
one entry per non-virtual declaration, the id/name pairing, order, and count bound.
Malformed present statistics are not silently treated as zero or as legacy absence.

### Proof

- A schema-first generated database is sealed and compared with an independent full
  scan grouped by predicate.
- Structural assertions prove: every non-virtual declaration exactly once, no virtual
  entry, ascending ids, correct names, per-predicate sum equals `Identity::facts`, and
  zeros are present.
- Cases cover deduplication, staged ingest, concurrent ingest, multiple predicates,
  zero facts, reopen, and compaction.
- A meta-write probe proves finish still performs one sidecar replacement carrying all
  fields and the status flip.
- New metadata round-trips. A hand-written version-1 sidecar without `predicates` loads
  as `None`. Duplicate, missing, reordered, wrong-name, wrong-id, and overflowed present
  statistics provoke their contract-layer error.
- Two content-equivalent builds with shuffled ingest and different physical ids agree on
  total count, ordered predicate counts, and content fingerprint.

Correct PLAN.md's claim that `approximate_len()` is exact. It remains at most a diagnostic
cross-check and is never refusal evidence.

**Exported facts for Runs 5–6:** `Some(counts)` is a complete validated generation-1
statistics set and a pure function of sealed content; `None` means legacy/no statistics,
not zero.

---

## Run 5 — `fjord.db.Stat` and per-virtual-table world digests

**Claim:** holding cursor version, plan, and base world equal, and under the repository's
accepted 64-bit digest collision model, a cross-request resume over stable virtual
predicates is accepted exactly when every virtual table the plan reads has identical row
bytes.

Add `fjord.db.Stat {name, instance, predicate, facts}`. Several instances may share a
name, hence both identity fields. Complete entries with validated counts produce one row
per non-virtual predicate, including zeros. Writable and legacy entries produce no Stat
rows; absence is not a fabricated sentinel.

Generalise `reads_listing` and `with_listing_digest` to a sorted, length-framed sequence
of `(predicate_id, table_digest)` for every stable virtual predicate the plan reads,
including an empty table. Keep `fjord.db.Interning` excluded and keep its existing
cross-request refusal. Fetch frames continue to carry digests only for non-empty tables
that could have minted ids; no wire or client shape changes.

Move complete resume preflight ahead of `ROW_DESCRIPTION` in this run. A resumed request
materialises its catalogue, obtains the first execution reader and its fully composed
base-plus-virtual world, checks cursor version, plan, then world in the same precedence
as `Executor::resume`, and retains that exact reader/world pair for the first chunk.
This is required evidence for Run 5's own refusal claim, not work deferred until
statistics planning.

### Proof

- Run 0's permutation property is instantiated for Stat.
- A generated `Listing -> expected Stat rows` model proves exact names, instances,
  predicate names, counts, zeros, multiplicity, ordering, and the Writable/legacy
  exclusions.
- A pair-generated catalogue property chooses an arbitrary set of stable virtual
  predicates and proves:
  - selected world bytes agree when all selected tables' row bytes agree;
  - changing any selected table changes its digest/world in the tested domain;
  - changing only unselected or volatile tables leaves the world unchanged;
  - materialisation and input table order cannot change the framed world;
  - empty and absent tables encode differently where the schema distinguishes them.
- A framing mutation battery covers count, predicate id, digest, ordering, truncation,
  and boundary shifts.
- Server-level paging uses the actual protocol and asserts success/refusal before any row
  descriptor for: zero-row create/remove, finish, removal of a Complete instance,
  independent List+Stat changes, same-row-count/different-content changes, and unchanged
  negative controls.
- Fetch tests prove a stale Stat virtual id is refused and unchanged ids resolve to the
  same bytes.

Add `fjord db stat` and `:stat` with automated CLI/shell output tests and update the book
and PLAN.md gap row.

---

## Run 6 — exact base counts feed planning

**Claim:** content-equivalent Complete databases carrying the same validated statistics
generation compile identically; legacy and live Writable databases plan nominally;
statistics-mode changes are resume-safe.

### Settled legacy policy

Do **not** backfill and do **not** put a statistics digest in the world stamp.

- `Some(valid generation-1 counts)` on a Complete database selects exact-base mode.
- `None` selects nominal mode, including legacy Complete databases.
- Writable databases select nominal mode until sealing publishes their `Completion`.

The plan fingerprint already hashes the finalized plan structure. If two modes choose
different plans, cross-mode resume is `CursorPlan`. If they choose the same plan, resume
is semantically safe and succeeds; refusing merely because unused estimates differed
would conflate planning input with database world. The world stamp continues to identify
rows, not optimiser policy.

### Ownership and race-free publication

Replace the server's standalone completion fingerprint with one immutable publication:

```rust
struct Completion {
    fingerprint: u64,
    predicate_counts: Option<Arc<[PredicateCount]>>,
}
```

`Database` initializes it from Complete metadata at registry open. A live finish carries
Run 4's counts through `Finished`, publishes `Completion`, then clears `writable` while
holding the existing seal barrier. `prepare` snapshots one `PlanningContext` from that
publication and passes only a narrow predicate-count view into `fjord-engine`; the engine
depends on no backend or sidecar type. `Compilation::new` remains the nominal default for
embedded and wasm callers.

`planning_context` reads `writable` first: `true` means nominal even if finish is in its
publication window; `false` requires the already-published `Completion`, otherwise it
fails toward refusal as the existing unknown-world arm does. Thus a caller can observe
the old complete context or the new one, never a partial count vector. A plan compiled
nominally just before the transition remains legal; a later page either reproduces it or
refuses through the normal plan/world checks.

Run 5's retained-reader preflight is reused unchanged. Statistics remain outside the
world: prepare compiles from its one `PlanningContext`, then preflight obtains and retains
the first execution snapshot. Complete counts describe that immutable content; Writable
planning is nominal. When both plan and world mismatch, preflight reports `CursorPlan`,
matching the engine's existing order, and every refusal occurs before
`ROW_DESCRIPTION`.

### Proof

- Extend the cost-plan corpus with exact-count cases: zero, equal bases, extreme skew,
  a count that changes the nominal winner, a legacy/current pair with the same winner,
  and a legacy/current pair with different winners.
- For two equivalent builds with shuffled ingest and different physical ids, assert
  identical validated counts, selected order, full PlanView, cost, fingerprint, world,
  canonical logical rows and profile. Each build independently satisfies resume equals
  uninterrupted. A cursor crossing between them is refused as `BadResumeKey` when the
  saved key's physical id differs; content equivalence is not permission to replay a
  physical cursor into a different id assignment.
- An exact-count scaling witness must select a cheaper plan than nominal and reduce exact
  examined/store-operation counts while returning identical rows.
- Legacy/current with different selected plans refuses as `CursorPlan` before the row
  descriptor. On the same store/id mapping, legacy/current with the same selected plan
  resumes successfully, proving that unused estimate-mode differences are not a hidden
  refusal. Different artifacts retain the saved-key/id rule above.
- A live server query before, during, and after finish proves nominal publication, exact
  publication, no partially visible count set, and safe `CursorPlan`/`CursorWorld`
  behaviour across the transition.
- Generated zero counts never become absent statistics; malformed present counts are
  refused at metadata/planning-context construction, not treated nominally.
- Native and wasm cost-corpus comparison is extended so the wasm harness accepts the
  same serialized count view; equal inputs remain byte-identical. Browser callers that
  supply no counts intentionally exercise nominal mode.

Update PLAN.md's “spend it on pruning, not join ordering” decision, the operations and
query-efficiency chapters, and the resume check-order documentation.

### Rung 3 remains recorded, not scheduled

Prefix/range histograms, cross-key fan-out, and residual selectivity require a new
statistics generation and this same corpus discipline. Statistics may use only
compile-time constant key material. Reference histograms may not key on physical
`FactId`; they must aggregate canonical logical targets or an order-independent
distribution. Fjall exposes no rank/range-count API, so exact prefix counts require seal
precomputation. No ignored guards are added for this unscheduled work.

---

## What the optimiser cannot fix

- A row bind that claims its variable prevents another occurrence from being a capture;
  ordering cannot invent a missing access path.
- A join whose inner side cannot seek on the schema's declared leading fields is a
  key-order problem. The cost corpus must include this as an unchanged-plan negative
  control, not claim the optimiser repaired it.

## Per-run gates

Every run executes the complete repository gate:

```bash
export PATH="$HOME/.cargo/bin:$PATH"
cargo build && cargo test
cargo test -- --ignored --list && python3 scripts/check-guards.py
python3 -m unittest scripts/test_check_guards.py
cargo +1.97.1 clippy --all-targets --workspace -- -D warnings
cargo +1.97.1 fmt --all
python3 website/build.py --strict
cargo check -p fjord-engine --target wasm32-unknown-unknown
./scripts/build-wasm.sh && (cd web && npm run smoke)
```

Focused gates:

| Run | Focus |
|---|---|
| 0 | `fjord-server::catalogue` permutation, forced legacy/new resume, virtual fetch |
| 1 | cost arithmetic, complete-order reference evaluator, mutation controls, taxonomy census |
| 2 | old/new emitter differential, trace/plan classifier, baseline cost-plan snapshots |
| 3 | exhaustive/fallback selector properties, complete pre/post corpus, native/wasm comparison, examined scaling witnesses |
| 4 | identity/count model, structural count contract, sidecar compatibility, one-write seal |
| 5 | Stat row model, virtual-world pair property, server paging/fetch, CLI and shell |
| 6 | exact-count corpus, equivalent builds, live finish publication, cursor precedence, native/wasm counts |

## Issue-level acceptance

Issue #18 closes only when all of the following are simultaneously true:

- Every planned cost-plan corpus entry has reviewed baseline and selected full-plan
  snapshots, fingerprints, independent costs, ordered rows, semantic multiset, and a
  selection classification; every diagnosed entry has its exact code and span.
- The corpus taxonomy census and mutation controls are green.
- Exhaustive selections equal the independent global optimum; every non-exhaustive case
  proves its exact fallback reason and fresh-greedy result.
- Baseline and selected plans agree on the semantic result multiset for every entry and
  for the generated schema-first model across every safe forced order; each also matches
  its own recorded execution order.
- Named witnesses improve exact examined/store-operation counts with scale; the hostile
  nominal witness documents the absence of a universal runtime claim.
- Native and wasm outputs are byte-identical for the complete nominal and exact-count
  corpus inputs.
- Plan changes move fingerprints and stale cursors refuse before a descriptor; unchanged
  plans retain fingerprints and resume.
- Exact counts are a validated pure function of sealed content, including zero and
  content-equivalent rebuilds, and the live-finish path publishes them whole.
- The complete repository gate is green and the book and PLAN.md state the shipped
  model, limits, fallbacks, statistics policy, and proof boundary exactly as the tests do.
