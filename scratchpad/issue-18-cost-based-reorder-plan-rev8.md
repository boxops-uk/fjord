# Cost-based reorder — issue #18 (revision 8)

## Decision and proof boundary

`reorder` takes the lowest-numbered member of the runnable frontier
(`reorder.rs:263-292`, the pick at `:274-280`). That is a deterministic legality rule, not
a cost model — the module says so at `reorder.rs:236-256`, and the book says so at
`query-efficiency.md:212-244`. This work replaces the tie-break with a costed choice
without changing the set of legal orders or the executor.

The implementation is staged as eight independently reviewable runs, numbered 0–7.
Every run has one falsifiable claim, a mechanical gate for that claim, and an explicit
statement of what later runs may rely on.

Two limits are part of the specification:

1. The optimiser is **exact under its declared, clamped estimate model only when it
   exhausts every admissible candidate within its budgets**. On overflow it discards the
   entire search result and runs deterministic greedy selection from the beginning.
2. No statistics-free estimate can promise universal runtime improvement. The
   optimiser preserves answers, is exact under its model when exhaustive, and improves
   named scaling witnesses. The book states that a different data distribution may
   favour another legal plan.

---

## What revision 8 changes, and the evidence for each change

Revision 7's central correction — cost finalized plans, not prefixes — is retained
unchanged and is restated with its verification below. Revision 8 closes six findings
against revision 7. Five of the six are premises revision 7 *asserted*; one is a premise
that is **false against the code as it stands today**, and it was making a headline
acceptance criterion pass vacuously.

| # | Finding in revision 7 | Resolution in revision 8 | Verified at |
|---|---|---|---|
| P1 | "the same monotone legality condition as today" is false: the enumeration predicate is strictly narrower than the compiler's safety predicate, so a class of queries that compile cleanly has an **empty** candidate set — and the "exhaustive equals the global optimum" gate passes vacuously over it | Two predicates named and separated; `reorder`'s frozen; a new **admissible-order predicate** proved equal to `safe()`; it becomes the search space, the greedy frontier and the oracle's enumeration domain. New **Run 1** does nothing else. | `flatten.rs:2531-2543` vs `reorder.rs:263-292`; probe transcript below |
| P2 | `preserves_written_order` dropped without a decision, though it is public API with a live proptest, and the module doc says `Placement` exists for it | Retired as a global invariant with the reason recorded; replaced by three narrower properties, one per mode; `reorder`'s own proptest stays green because `reorder` is not modified | `reorder.rs:304-331`, `:628-653`, `:75-88` |
| P3 | "statements already proved placement-inert" — never proved anywhere; revision 6 carried the mechanism and revision 7 dropped it. The word also does two jobs | Mechanism restored and made a **Run 3** exported fact; the word split into *emission-inert* and *legality-relevant*, which are different claims about the same two statement kinds | `flatten.rs:474-495`, `:2777`, `:2781`, `:2799-2800`, `:1063-1073` |
| P4 | Run 0 exports the implication that holds by construction and needs no collision model; Run 5 consumes the converse, which nobody states | Split into two separately named exported facts, each with the assumption it actually needs | `catalogue.rs:349-393`, `:396-409` |
| P5 | Nothing bounds compile time. The objective is execution rows; the search lowers a full `emit` per candidate, so compile cost is `candidates x emit` | Budget values named in the audit table; a compile-time witness added to the corpus; a compile-time regression bound added to issue-level acceptance | `flatten.rs:2602` |
| P6 | `BaselineCandidateFault` conflates "candidate is unsafe" with "candidate emission faulted" | Separated. With P1 fixed the first case is **impossible by construction**, which is stated as an assertion rather than left as a hope | — |

Two things revision 6 carried and revision 7 dropped are restored, because both are the
kind of thing whose absence costs the next reviewer a session of re-derivation:

- **the defect ledger with a `Verified at` column**, so a later revision cannot quietly
  reinstate a premise an earlier one refuted;
- **verified code citations throughout.** Every citation in this document was re-checked
  against the working tree at `51d4868`. Where revision 6's line numbers had drifted,
  the current ones are given.

### Run numbering: revision 7 to revision 8

Revision 8 inserts one run and shifts the rest. Nothing else about the sequence moved.

| rev 7 | rev 8 | Subject |
|---|---|---|
| 0 | 0 | virtual ids are functions of rows |
| — | **1** | **the admissible-order predicate** (new; P1) |
| 1 | 2 | finalized-plan cost specification and oracle |
| 2 | 3 | one annotated emitter, chooser unchanged |
| 3 | 4 | bounded search and greedy fallback |
| 4 | 5 | exact per-predicate counts |
| 5 | 6 | `fjord.db.Stat` and virtual world digests |
| 6 | 7 | exact base counts feed planning |

---

## The defect ledger

Four adversarial reviews have now run against this plan. The table is the plan's memory:
a premise refuted here may not return without new evidence against the row that killed it.

| Rev | Defect | Verified at |
|---|---|---|
| 1→2 | DP substructure premise false — a product of per-statement *access* estimates is not a set function | counterexample, now pinned as `a_product_of_access_estimates_is_order_dependent` |
| 1→2 | Cost model described statements, not emitted operators | `flatten.rs:2777`, `:2781`; `iter.rs:493` |
| 1→2 | `Cardinalities::prefix(pred, fields)` cannot express rung 3 | design |
| 1→2 | `fjord.db.Stat` an unguarded I4 regression | `session.rs:1469-1470`, `:1545`, `:1640` |
| 2→3 | Subset alone is not a sufficient DP state — `fetch_level` reuses registers | `flatten.rs:1966-1991` |
| 2→3 | One `Effect` cannot describe one statement; each disjunct builds its own seek | `flatten.rs:2646-2700` |
| 2→3 | The claim that moving `Constrain`/`Compare` shifts addresses was **wrong** | `flatten.rs:474-495` |
| 2→3 | `approximate_len()` is "reliable", not exact — and the exact count is free in the identity walk | `fjall-3.1.8/…/keyspace/mod.rs:476-481`; `identity.rs:100-111` |
| 3→4 | Fetch reuse is **amended**, not just reused — `chase` extends an already-emitted level's residuals | `flatten.rs:1997-2032`, loop at `:2022-2031` |
| 4→5..6 | A stateful DP is no longer bounded by `2^n`; cardinality still conflated semantic relations with physical access | design |
| 6→7 | **The prefix-cost recurrence is invalid outright** — a later statement retroactively changes cost already charged to a prefix, and a larger state does not repair it | `flatten.rs:1997-2032` |
| 6→7 | An audit that grades the optimiser by its own cost model proves nothing; the baseline must be data | design |
| **7→8** | **The enumeration's legality predicate is strictly narrower than `safe()`** — queries that compile cleanly have an empty candidate set, and the optimality gate passes vacuously over them | `flatten.rs:2531-2543` vs `reorder.rs:263-292`; probe below |
| **7→8** | `preserves_written_order` is public API with a live proptest and a module doc that names it as `Placement`'s purpose; a cost chooser breaks it by construction | `reorder.rs:62-88`, `:304-331`, `:628-653` |
| **7→8** | Placement-inertness asserted as "already proved"; revision 6 held the only argument | `flatten.rs:474-495`, `:2777`, `:2781` |
| **7→8** | Compile time is unbounded and unmeasured while execution rows are the stated objective | `flatten.rs:2602` |

---

## Revision 7's central correction, retained: cost finalized plans, not prefixes

A later chased bind can reuse a fetch emitted by an earlier access chain and append
residuals to that already-emitted level. `chase` (`flatten.rs:1997`) calls `fetch_level`
(`:1966`), which **returns an existing register** when one matches (`:1973-1975`), and
then mutates that level in place:

```rust
// flatten.rs:2022-2031
if !walk.residuals.is_empty()
    && let Some(level) = body.level_mut(register)
{
    for source in level.sources.iter_mut() {
        let residuals = source.residuals_mut();
        let mut extended = residuals.to_vec();
        extended.extend(walk.residuals.iter().cloned());
        *residuals = extended.into();
    }
}
```

Those residuals run at the *earlier* level and reduce the number of times every
intervening inner level runs. Therefore this recurrence is invalid:

```text
cost(S + m) = cost(S) + card(S) * access(m | state(S))
```

The cost already charged to a prefix changes when a later statement amends it. A larger
`PlanningState` does **not** repair the recurrence: a richer state can say *what will be
amended*, but it does not undo a `cost(S)` term already computed from unamended
cardinalities. Repair would require exact retroactive recomputation of every affected
downstream contribution, and no such proof exists.

Revision 8 consequently does **not** merge prefixes and does **not** claim a `2^n`
subset DP. It enumerates complete admissible orders within explicit budgets, fully lowers
each order, lets every fetch amendment land, and only then costs the finalized operator
sequence. This is factorial in the worst case and bounded in fact; exactness is claimed
only when enumeration completes. A future subset DP may replace it only after proving
the Bellman property against the finalized-plan oracle built here.

---

## The two order predicates, and why the difference is load-bearing

This section is new in revision 8 and is the whole of Run 1. It exists because
revision 7 said the search would use "the same monotone legality condition as today",
and that sentence hides a real difference between two conditions the codebase already
has.

### They are not the same condition

`reorder` seeds its bound set **empty** and grows it only from statement captures:

```rust
// reorder.rs:263-292
let mut bound: Vec<Symbol> = vec![];
// ...
let runnable = (0..deps.len()).find(|stmt| {
    !emitted[*stmt] && deps.stmts[*stmt].reads.iter().all(|var| bound.contains(var))
});
```

`Deps::respects` (`reorder.rs:147-171`) and `Deps::antichains` (`:183`) — the independent
feasibility oracle the completeness proptest at `reorder.rs:605-614` compares against —
use that same empty-seeded predicate.

`safe()`, the check that actually decides whether a compiled order is legal, seeds its
bound set from **every folded constant** first:

```rust
// flatten.rs:2531-2543
let mut bound: Vec<Symbol> = self
    .bindings
    .iter()
    .filter(|(_, slot)| matches!(slot, Slot::Const(_)))
    .map(|(symbol, _)| *symbol)
    .collect();
```

The gap between them is real because **a constant bind produces no statement at all**.
`X = 42` is folded at collect time (`flatten.rs:1461-1464` → `fold_into`, `:4472-4474`,
which records `Slot::Const`) and contributes no entry to `stmts` and therefore none to
`Deps`. So nothing in the graph *captures* `X`, while statements that *read* it keep it
in `reads` permanently:

- a negation moves every capture into reads (`flatten.rs:1051-1058`) — *"a negation reads
  everything and captures nothing"*;
- a constraint reads its variable (`flatten.rs:1065`);
- a comparison reads both sides (`flatten.rs:1071-1074`);
- a derived bind reads its operands (`flatten.rs:1077-1080`);
- an alias reads what its value is rooted at (`flatten.rs:1012-1029`).

That the asymmetry is deliberate rather than accidental is visible in the code that
compensates for it: `negated_wildcards` (`flatten.rs:2141-2154`) seeds its own `bindable`
set with the folded constants before deciding whether a negation's read is a genuine
unbound-variable error — *"A folded constant binds before any level runs, so a negation
reading one is reading a value, not quantifying over a predicate."*

### The probe

Measured against the working tree, with a temporary test since reverted:

```text
src:   B where B = test.Bar {id = 2}; N = 1; !test.Node {id = N}
deps:  [ captures:[B] reads:[]  Written  ]
       [ captures:[]  reads:[N] Written  ]        <- nothing captures N
reorder order = [0, 1]                            <- via the dead-end path, reorder.rs:281-285
order satisfies reads-before-read: false
compiles: true, diagnostics = []                  <- a clean, supported query

src:   Y where B = test.Bar {id = 2}; N = 1; Y = N + 1
deps:  [ captures:[N'] reads:[]  Written  ]
       [ captures:[Y]  reads:[N] Floating ]
reorder order = [0, 1]
compiles: true, diagnostics = []
```

So the class is not a negation quirk: it is every statement kind that carries `reads`,
over any variable bound only by a constant fold.

### What that did to revision 7

1. Admission control admits these queries — the baseline is safe, produces a plan, and
   draws no diagnostic.
2. Enumeration of complete orders under the empty-seeded predicate yields **zero**
   candidates. That is not a budget crossing, so revision 7's only fallback trigger never
   fires, and revision 7 defines no behaviour for the case.
3. Revision 7 described the empty-frontier rule as the *diagnostic* path — "so the
   existing flatten safety pass reports the original variable and span". It is also the
   **success** path for this class, which the probe shows compiling with no diagnostics.
4. `Unorderable` was defined as "neither baseline nor selected compilation produced a
   plan", so this class had no selection mode.
5. Worst: revision 7's Run 1 oracle enumerates "every legal complete order", which for
   these queries is nothing. The issue-level criterion *"exhaustive selections equal the
   independent global optimum"* would therefore pass **vacuously** over a class the
   optimiser silently never optimises. That is precisely the failure mode the acceptance
   criteria exist to exclude.

### The decision

Keep both predicates, name both, and be explicit about which artifact uses which.

**`reorder` is frozen.** It keeps the empty-seeded predicate, and revision 8 does not
modify it. Its only remaining production role is to compute the **admission baseline
order** in Run 4. This is deliberate: seeding `reorder` itself would change which order
the dead-end path returns for the class above, which moves plans and fingerprints for
queries the optimiser has not even been asked about yet. Plan movement belongs in the run
whose gate is about plan movement, not in a legality correction.

**The admissible-order predicate** is new, and is defined to be `safe()`'s:

> A complete permutation `o` of the collected statements is **admissible** when, walking
> `o` from a bound set seeded with the query's folded-constant symbols, every statement's
> `reads` are bound at the position it occupies.

Run 1 proves the equivalence that makes this usable:

> **For a complete permutation, `admissible(o)` holds exactly when `safe(o)` returns
> true.**

The proof has two halves and the second is worth stating separately because it is what
makes the whole search space well-defined:

- the loop half is a syntactic correspondence — `safe()` is exactly a prefix walk over
  `reads` from the const-seeded set (`flatten.rs:2549-2566`);
- the head half is **order-invariant**. `safe()` also checks `collected.head_reads`
  against the final bound set (`flatten.rs:2568-2576`), and after a *complete*
  permutation the final bound set is `consts ∪ (every statement's captures)` regardless
  of order, because `Deps` is computed once at collect time and does not depend on the
  order. So head reads can never make one complete order safe and another unsafe: either
  every complete order fails on them or none does.

Three consequences follow immediately, and each replaces something revision 7 left open:

1. **The candidate set is never empty after admission.** Admission computes the baseline
   order and runs `safe()` on it; if it passed, that order is itself an admissible
   complete order. So `|candidates| >= 1` always, and revision 7's undefined empty-set
   case cannot arise. Run 4 asserts this rather than assuming it.
2. **Every enumerated candidate is safe by construction**, so a candidate can never be
   rejected for safety mid-search. This is what lets P6 be resolved cleanly below.
3. **The optimiser can now reach orders `reorder` cannot produce at all** — exactly the
   class the probe found. That is more optimisation, not less, but it means plans move
   for queries that have never been reordered before. The corpus taxonomy gains a class
   for it (see below), with the current collection-order plan as the frozen baseline.

`Deps` gains `pre_bound: Box<[Symbol]>`, populated at collect time from the folded
constants, and `Deps::respects_admissible` / `Deps::antichains_admissible` alongside the
existing pair. The existing pair is **not** changed — the completeness proptest at
`reorder.rs:605-614` pairs `respects` with `antichains`, and both must keep answering the
question `reorder` answers, or that test stops testing `reorder`.

---

## The permanent acceptance artifact: the cost-plan corpus

`fjord_engine::cost::corpus` is a table of complete planning cases. It follows the
discipline of the existing target-feature corpus (`fjord-engine/src/corpus.rs`, gated at
`lib.rs:54-55` under `cfg(any(test, feature = "proptest"))`), for the same reason that
module gives: a written target in prose drifts, so the table lives as data next to the
tests that check it. Each entry contains:

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
optional compile-time bound for compile-cost witnesses
coverage tags
```

`selection mode` is one of `Exhaustive`, `GreedyStatementLimit`, `GreedyCandidateBudget`,
`GreedyTransitionBudget`, `BaselineCandidateFault`, or `Unorderable`. Planned cases state
every field above. An `Unorderable`/diagnosed entry instead states the exact diagnostic
code and span and proves that neither baseline nor selected compilation produced a plan.
A case cannot say only "supported".

The baseline is not compiled through a retained production chooser. Its order is data in
the entry and is handed to `flatten_in_order` (`flatten.rs:678-686`, itself gated under
`cfg(any(test, feature = "proptest"))`), so changing the optimiser cannot silently move
both sides of the comparison. The selected side is compiled through the real production
entry point. Both plans execute against the entry's store and must return their
separately stated ordered rows; sorting those rows must produce the one stated semantic
multiset. Reordering may legitimately change traversal order, so one shared ordered
expectation would either reject correct plans or hide the difference by sorting too early.

Full structural snapshots use `fjord-inspect`'s interner-free JSON view. The corpus data
lives in `fjord-engine`; the snapshot runner lives in `fjord-inspect/tests`, which is the
first crate allowed to see both the engine and its JSON view. Snapshot regeneration is an
explicit command that writes to a temporary candidate file; the test never blesses a
change automatically.

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
- **admissibility (new in revision 8): a folded constant read by a negation, by a derived
  bind, by a comparison, by a constraint, and by an alias value; the same query with and
  without the fold; and a query where the admissible space is strictly larger than
  `reorder`'s reachable space, with `reorder`'s dead-end plan frozen as the baseline;**
- selection: source order wins, a later statement wins, a genuine cost tie, folded
  statements in every legal region, an unorderable query, each exact budget boundary,
  each one-past fallback, injected candidate failure, and discovery-order perturbation;
- arithmetic: zero, one, every nominal constant, every clamp, and `u64::MAX`;
- statistics: nominal, exact zero, exact skew, legacy/no statistics, and two
  content-equivalent builds with different physical fact ids;
- compatibility: unchanged fingerprint, deliberately changed fingerprint, native/wasm
  equality, resume success, and resume refusal;
- **compile cost (new in revision 8): a query at each budget boundary with a recorded
  compile-time bound.**

The census is about populations as well as names: generated companions must reach each
shape a minimum number of times, and mutation controls must make the relevant gate fail
when an access rank, factor attachment, amendment, tie-break, clamp, fingerprint tag,
fallback boundary, **or the admissibility seed** is changed.

---

## Run 0 — virtual ids are functions of rows, not iterator order

**Claim:** every permutation of the same virtual rows produces identical scan rows,
`FactId` mappings, per-table digests, and point-read answers.

`Table::of` (`fjord-server/src/catalogue.rs:349-393`) mints
`FactId::new(predicate, sequence + 1)` from the **input iterator order**, inside the loop
(`:373`), and sorts into key order **afterwards** (`:386`); `digest_of_rows` (`:396-409`)
hashes the predicate, the row count, and the framed keys — never the ids. `listing_rows`
walks the store root, so the input order is filesystem-dependent. Two permutations of the
same logical rows therefore give identical scan bytes and an identical digest but a
**different `FactId` → row mapping**.

This is shipped behaviour on `fjord.db.List` today, not a hazard this work introduces.
Encode key bytes first, sort them, then enumerate from one to mint dense ids. The table
digest continues to hash the predicate and sorted keys; ids become a function of those
keys. Ids still start at 1 and are still dense per predicate, so nothing downstream meets
a fact id shaped differently from every other (`catalogue.rs:371-372`).

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

### Exported facts, split (P4)

Revision 7 exported one fact, in the direction that does not need the assumption it
carried, while Run 6 consumed the other direction, which nobody stated. Two facts:

- **F0-a (construction).** For a virtual table, equal sorted row bytes imply equal
  `key -> FactId` mappings. After the fix this holds **by construction** — ids are
  enumerated from the sorted keys — and requires no collision model at all.
- **F0-b (digest).** For a virtual table, equal per-table digests imply equal sorted row
  bytes, **under the repository's accepted 64-bit digest collision model**. This is the
  direction Run 6 consumes to turn "the world stamp matches" into "the rows the plan
  reads did not move", and it is the only place the collision model is needed.

---

## Run 1 — the admissible-order predicate (new in revision 8)

**Claim:** a complete permutation is admissible exactly when `safe()` accepts it; the
admissible space is non-empty for every query that compiles; and it is a strict superset
of the space `reorder` can reach.

Nothing in production consumes this run. Its entire output is a proved predicate that
Runs 2 and 4 build on, which is why it is separated from both: revision 7 folded this
premise into two runs as an assumption, and it was false.

### What is built

- `Deps` gains `pre_bound: Box<[Symbol]>`, populated at collect time from the
  folded-constant bindings — the same set `safe()` seeds from (`flatten.rs:2534-2540`)
  and the same set `negated_wildcards` already seeds from (`flatten.rs:2148-2154`).
- `Deps::respects_admissible` and `Deps::antichains_admissible`, seeded from `pre_bound`.
- `Deps::respects` (`reorder.rs:147-171`), `Deps::antichains` (`:183`) and `reorder`
  (`:263-292`) are **not modified**. The completeness proptest at `reorder.rs:605-614`
  pairs `respects` with `antichains` as `reorder`'s own feasibility oracle; changing
  either would stop that test testing `reorder`.

### Proof

- **The equivalence, as a property over the generated schema-first model.** For every
  generated query and every complete permutation of its statements, assert
  `respects_admissible(o) == safe_accepts(o)`, where `safe_accepts` is observed through a
  test-only hook that reports `safe()`'s verdict without compiling. Both halves are
  asserted separately: the prefix walk, and the order-invariance of the head-reads check
  (assert the final bound set is identical across all permutations of one query).
- **The strictness, as named cases rather than a claim.** The two probe queries above are
  pinned by name — `a_folded_constant_read_by_a_negation_is_admissible_but_unreachable`
  and `a_folded_constant_read_by_a_derived_bind_is_admissible_but_unreachable` — each
  asserting all four of: nothing in `Deps` captures the variable; `respects` is false for
  the order `reorder` returns; `respects_admissible` is true for it; and the query
  compiles with no diagnostics.
- **The coincidence condition.** `respects_admissible == respects` for every order
  exactly when `pre_bound` is empty. This is what says the new predicate is a
  generalisation rather than a different rule.
- **Non-emptiness.** For every generated query that compiles, the order `reorder` returns
  is admissible. This is the fact Run 4's admission control converts into "the candidate
  set is never empty".
- Mutation controls: removing the `pre_bound` seed, seeding it with the wrong slot kind,
  or seeding it after the loop instead of before must each make the equivalence property
  fail.

Update `reorder.rs`'s module doc: the "Why greedy is complete" argument at
`reorder.rs:17-30` is correct **about its own predicate** and should say so, naming the
admissible predicate as the wider one and pointing at where the difference is proved.

**Exported facts for Runs 2 and 4:** admissible == `safe()` on complete permutations;
the admissible space is non-empty whenever the query compiles; it strictly contains
`reorder`'s reachable space, and the containment is strict exactly for queries with a
non-empty `pre_bound` read by some statement.

---

## Run 2 — finalized-plan cost specification and independent complete-order oracle

**Claim:** the abstract cost of a finalized plan is total and deterministic, and the
reference evaluator assigns the same declared cost without using production costing
machinery.

New `fjord-engine::cost`, used by no production chooser yet.

### Strong units and the objective

The optimisation objective is estimated **rows examined**, the quantity
`Profile::total` measures (`iter.rs:503`, `:519`). Use distinct `Rows`, `Fanout`,
`Selectivity`, and `ExaminedCost` newtypes. `Selectivity` is a reduced integer ratio;
application uses zero-preserving ceiling division. All multiplication and addition
saturate at declared clamps. There is no `f64`, platform-sized arithmetic, random
iteration order, or backend-dependent value — `fjord-engine` builds for
`wasm32-unknown-unknown`, and a plan compiled in the browser must be byte-identical to
one compiled on the server or a cursor minted in one will not resume in the other.

One module-doc audit table states every nominal base, fan-out, filter selectivity, probe
survival estimate, and clamp. Each constant has a named Run 2 corpus/audit case. Boundary
properties cover zero, one, numerator/denominator boundaries, every clamp, and
`u64::MAX` for both cardinality and accumulated cost. Run 4 adds its search budgets and
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

Run 2 proves the reference evaluator itself with hand-calculated audit cases for every
factor and operator rule, algebraic properties for factor canonicalisation and total
arithmetic, and a tiny token interpreter: within deliberately small bounds the
interpreter materialises `input_rows` unit tokens, applies each declared operator/factor
one token at a time, and counts examined tokens directly.

For small generated abstract cases:

1. **enumerate every *admissible* complete order** — Run 1's predicate, not `reorder`'s.
   This is the P1 correction landing in the oracle: an oracle that enumerates the
   narrower space cannot detect that the search enumerated the narrower space;
2. evaluate each with the reference evaluator;
3. compare every operator's breakdown with the token interpreter and the hand-stated
   boundary cases, not only the winning total;
4. choose the minimum `(cost, order)` independently and pin the winner for the audit
   graphs.

The oracle asserts `|admissible orders| >= 1` for every case it is given, which is Run 1's
non-emptiness fact used where a vacuous pass would otherwise be invisible.

Run 3, not this run, compares those saved reference breakdowns with production-lowered
plans. Run 2 therefore finishes with a proved specification and oracle while still
depending on no production emitter that does not exist yet.

The Run 2 census covers the access, lowering, filter, factor, arithmetic and
admissibility portions of the taxonomy above. Run 4 adds selection/fallback coverage and
Run 7 adds exact-statistics coverage; Run 2 does not claim populations for subsystems
that do not exist yet. Explicit counterexamples pin:

- a product of per-statement access estimates is not a set cardinality;
- a later chased bind amends an earlier fetch and changes the cost of an intervening
  level already visited in statement order;
- three relational occurrences of one variable apply one declared multiway rule;
- a clamped tie falls back to lexicographic source order;
- **an admissible order that `reorder` cannot reach is enumerated and costed.**

Mutation controls alter the reference rule, token rule, or a hand-stated expected
breakdown one at a time and must make the comparison fail. Run 3 adds mutations between
the reference and production implementations. This is what licenses the word
"independent".

**Exported facts for Runs 3–4:** the finalized-plan formula is total; the reference
complete-order evaluator is the oracle for optimality **over the admissible space**; no
prefix-substructure property is assumed.

---

## Run 3 — one annotated emitter, chooser unchanged, baseline corpus frozen

**Claim:** producing a finalized cost trace alongside a plan preserves every current
plan, diagnostic, fingerprint, and answer; the trace describes the plan it accompanies;
and `Constrain`/`Compare` are emission-inert.

Refactor emission around an internal annotated body. Operator provenance and semantic
factor identities live only while planning; stripping annotations yields the existing
`Plan`. Fetch reuse and amendments mutate this one body (`flatten.rs:2022-2031`), so plan
and cost trace cannot disagree because two builders made different decisions.

Collection produces an immutable `EmissionSeed`. Every forced candidate starts with a
fresh emitter holding its own bindings, fetched map, folded factors, annotated body, and
scratch diagnostics. Candidate evaluation cannot mutate the shared interner, leak a
register number or diagnostic into the next candidate, or choose the winner by discovery
side effect. Only the already-collected public diagnostics and the selected plan escape.

The interner half of that is already true and should be recorded as a checked premise
rather than a hope: the only mutating interner call in flatten is `fresh`
(`flatten.rs:1880-1882`), reached once from `hoist` during collection
(`flatten.rs:1371`); every other use is `try_resolve`. Collection runs once, before any
order is chosen (`flatten.rs:794-805`). A guard asserts the interner's length is
unchanged across a candidate evaluation.

The production chooser remains the current lowest-runnable frontier for all of Run 3.

### Emission-inertness, proved rather than assumed (P3)

Revision 7 partitioned `Constrain` and `Compare` out of the search as "already proved
placement-inert" and cited no proof; revision 6 held the only argument and revision 7
dropped it. The word also conflates two different claims, which are separated here:

- **Emission-inert** — moving the statement within a legal order cannot change the
  emitted plan. This is what licenses partitioning them out of the search space, and it
  is what is proved in this run.
- **Legality-relevant** — the statement still carries `reads` (`flatten.rs:1065`,
  `:1071-1074`), so it still constrains where it may legally sit, and it must still
  appear in the returned permutation so `safe()` sees it and can report its span.

The mechanism for emission-inertness is register allocation, and it is mechanical:

```rust
// flatten.rs:474-486
fn next_address(&self) -> Address { Address::new(self.registers) }

fn push_level(&mut self, level: Level) -> Address {
    let address = self.next_address();
    self.steps.push(Step::Level(level));
    self.levels += 1;
    self.registers += 1;
    address
}
```

`registers` advances only in `push_level` and `push_derive` — that is, **off steps
emitted, not off position in the order**. `Stmt::Constrain(_) => {}` (`flatten.rs:2777`)
and `Stmt::Compare(_) => {}` (`:2781`) emit nothing, and the work they represent is done
after the body loop by `apply_compares` and `apply_constraints` (`:2799-2800`). So a
statement emitting no step cannot shift any address, and moving it cannot change a plan
or a fingerprint. Both are also `Placement::Floating` (`flatten.rs:194-201`), so they were
never constrained by written order either.

### Proof

- Retain the old emitter as test-only `legacy_emit` from this run onward. Differentially
  compare old and annotated emitters over the canonical schema-first generator extended
  to include `Alias`, `Derive`, binary `Compare`, access-chain fetch, reused fetch, and
  multiple amendments, across **every admissible forced order** — Run 1's predicate, so
  the differential covers the orders the optimiser will actually be allowed to pick,
  including the ones `reorder` cannot reach.
- Compare the complete `Plan`, fingerprint, rendered diagnostics including spans for
  unsafe/unsupported generated cases, and executed rows. A fixed corpus is not an
  alternative to this differential.
- Independently classify the stripped concrete plan and compare each finalized cost
  operator's access kind, physical position, sources, residual order, and amendment
  target. This classifier may inspect `Plan` but not emitter annotations.
- Compare every generated order's production cost breakdown with Run 2's reference
  evaluator.
- `every_legal_placement_of_a_folded_statement_compiles_identically`: for every generated
  query and every admissible position of each `Constrain`/`Compare`, the complete plan,
  fingerprint, diagnostics and rows are byte-identical. A separate invalid-query property
  pins the original diagnostic code and span after reinsertion.
- Run the existing compiler/executor model, permutation, resume, corpus, allocation,
  decode, and store-spy batteries unchanged.

### Baseline cost-plan corpus

Create every nominal/core corpus entry now, before changing the chooser. Run 7 adds the
exact-statistics entries before enabling statistics-based choice. Freeze for each entry:

- the explicit baseline forced order;
- its complete PlanView snapshot and fingerprint;
- its abstract cost breakdown;
- its exact ordered rows and semantic multiset;
- exact profiles/store calls for the named performance witnesses;
- **the compile-time bound for the compile-cost witnesses.**

For the new admissibility class, the baseline is the plan `reorder` produces today via
its dead-end path (`reorder.rs:281-285`) — that is what those queries currently compile
to, and freezing it is what makes Run 4's movement visible.

The selected fields temporarily equal the baseline. The Run 3 gate rejects any plan or
fingerprint churn.

Keep the old emitter permanently as `legacy_emit` under test configuration, frozen to
the pre-refactor surface and excluded from production/rustdoc builds. Its generated
forced-order differential remains a green proof rather than a test deleted after it once
passed. The explicit baseline orders and snapshots are the second, reviewable proof and
remain permanently too.

**Exported facts for Run 4:** any admissible forced order lowers to the same plan as
before; its finalized cost trace faithfully describes that plan; `Constrain`/`Compare` are
emission-inert; candidate evaluation is side-effect-free including the interner; the
baseline half of every acceptance case is immutable test data.

---

## Run 4 — bounded complete-order optimisation and greedy fallback

**Claim:** exhaustive selection returns the globally cheapest admissible finalized plan
under the declared model; every fallback is deterministic and legal; no selection changes
an answer; and compile cost stays inside a stated bound.

### Admission

Admission is deliberately conservative. First compute the old lowest-runnable baseline
order with the **frozen** `reorder` (`reorder.rs:263-292`) and run the unchanged safety
check and annotated emitter once with public diagnostics. If it is unsafe, diagnosed, or
produces no plan, return exactly that result and do not search. The optimiser therefore
cannot make an unsupported construct appear supported by choosing around its diagnostic,
or change which variable/span an unorderable query reports.

Admission also establishes the search space's non-emptiness, and this is the load-bearing
step revision 7 was missing: if `safe()` accepted the baseline order, then by Run 1's
equivalence that order is admissible, so the candidate set contains at least it. An
assertion states this; an empty candidate set is a bug, not a fallback reason.

### Search

Partition out the emission-inert statements — `Constrain` and `Compare`, per Run 3's
exported fact. Enumerate complete orders of the remaining order-relevant statements under
Run 1's **admissible** predicate, seeded from `Deps::pre_bound`. Lower and cost each
complete candidate from a fresh Run 3 `EmissionSeed`. Do not merge equal subsets, equal
bound sets, or equal-looking planning states — the revision 7 correction forbids it.

Budgets are explicit and independently counted, and their shipped values live in Run 2's
audit table:

| Budget | Counts | Initial value |
|---|---|---|
| `MAX_ORDERED_STATEMENTS` | order-relevant statements, before enumeration begins | 8 |
| `MAX_SEARCH_TRANSITIONS` | admissible prefix extensions visited | 8192 |
| `MAX_COMPLETE_CANDIDATES` | complete plans lowered and costed | 256 |

The values are proposals to be **pinned by the compile-cost witnesses below**, not
constants asserted in advance. What matters for review is the shape: each complete
candidate is a full `emit` (`flatten.rs:2602`), so compile cost is
`candidates x emit`, and `MAX_COMPLETE_CANDIDATES` is the budget that actually bounds
compile time. `MAX_ORDERED_STATEMENTS` bounds the worst case before enumeration starts;
`MAX_SEARCH_TRANSITIONS` bounds the walk when legality keeps the candidate count low but
the prefix tree wide.

Crossing any budget discards every candidate and best-so-far value and starts the greedy
fallback from the empty order. There is no partial-search/greedy hybrid. Exhaustive
selection ties on `(ExaminedCost, complete_order)` lexicographically.

### Two failure modes, separated (P6)

Revision 7 had one fault path covering both. They are different:

- **A candidate that is not admissible** is never generated, because the enumerator's own
  predicate is admissibility, and by Run 1 admissibility is `safe()`. So "a candidate
  failed its safety check" cannot occur. This is stated as a debug assertion over every
  generated candidate, not as a runtime branch.
- **A candidate that is admissible but whose emitter faults** returns the already-built
  baseline plan whole, as `BaselineCandidateFault`. This is a fail-safe compiler path,
  not a user diagnostic, and after the separation above it is reachable **only** by fault
  injection. Fault injection makes it mechanically testable; Run 3's every-admissible-
  order differential is what proves ordinary inputs never reach it.

### Greedy fallback

The fallback repeatedly selects the runnable statement with minimum
`(immediate_access_rank, source_index)`, rebuilding the local emitted effect against the
state at that point. It makes no optimality claim.

Its frontier uses the **admissible** predicate, seeded from `Deps::pre_bound` — the same
space the exhaustive search uses. Two reasons, both consequences of Run 1:

- greedy and exhaustive must search the same space, or a budget overflow silently changes
  which plans are even considered;
- the seeded frontier cannot dead-end after admission, because admission proved a
  complete admissible order exists and the seeded predicate is monotone in exactly the
  way `reorder.rs:17-30` argues. So greedy always returns a complete admissible
  permutation, and the dead-end branch is dead code in this path.

The dead-end branch stays in the frozen `reorder`, where it is still the right answer for
the genuinely unorderable query and still lets flatten's safety pass name the original
variable and span (`reorder.rs:281-285`). Revision 7 attributed that branch's behaviour to
the new fallback and described it as the diagnostic path; it is neither.

Emission-inert statements are reinserted at their earliest admissible position, stable by
source index. Their semantic factors were collected from the whole body and attach where
the finalized plan applies them, not where their syntax is reinserted. An assertion over
the returned full permutation proves none was omitted.

### Written order: what survives, and what does not (P2)

`preserves_written_order` (`reorder.rs:304-331`) says a written statement is never taken
ahead of an earlier written one that could have run instead. A cost chooser breaks it by
construction — moving the selective generator first is the entire point of issue #18 — so
it cannot remain a global invariant. Revision 7 dropped it silently; revision 6 proposed
restating it as "of equal-or-better cost", which is a **greedy-step** property and is
wrong for an exhaustive chooser that picks by global total rather than per-step rank.

The decision, with what replaces it:

1. **`reorder` is not modified, so its property and its proptest stay green untouched.**
   `reorder_keeps_the_written_statements_in_written_order` (`reorder.rs:628-653`) asserts
   `preserves_written_order(&graph, &reorder(&graph))`; since `reorder` is frozen as the
   admission baseline, that test keeps testing exactly what it always tested.
2. **Exhaustive mode retires it, and states the residual guarantee that is actually
   provable:** ties break on `(ExaminedCost, complete_order)` lexicographically, and
   `complete_order` is the permutation of source indices, so **the written order wins
   every tie and is displaced only by a strictly cheaper complete plan.** Guard:
   `a_cost_tie_selects_the_written_order`.
3. **The greedy fallback restates it in terms of its actual rank:** a written statement is
   never taken ahead of an earlier written one that was runnable and of equal-or-better
   `immediate_access_rank`, which is precisely what `(immediate_access_rank,
   source_index)` gives. Guard: `greedy_keeps_written_order_among_equal_ranks`.
4. Note for review: `Placement::Floating` covers `Alias`, `Constrain`, `Compare` and
   `Derive` (`flatten.rs:194-201`), so the property only ever constrained `Scan` and
   `Negate`. The doc at `reorder.rs:62-88`, which currently names
   `preserves_written_order` as the tag's purpose, is rewritten to name (2) and (3) as its
   consumers.

### Selector proofs

- For generated dependency graphs, every mode returns a complete permutation.
- The result respects the graph exactly when the independent **admissible** antichain
  oracle says an order exists, for exhaustive search and every fallback reason.
- Exhaustive results match Run 2's independently enumerated winner, including the full
  cost breakdown — and Run 2's oracle enumerates the admissible space, so the comparison
  is between two enumerations of the same set rather than two enumerations of the same
  mistake.
- An assertion that the candidate set is non-empty for every admitted query, with a
  named case per admissibility taxonomy entry.
- Exact boundary and one-past tests exist for all three budgets. One-past proves the
  partial winner is discarded by choosing a case where it differs from fresh greedy.
- An injected candidate-emission failure proves the admitted baseline plan is returned
  whole, with no scratch diagnostic leakage.
- Perturbing candidate-discovery order leaves the exhaustive winner and every fallback
  result unchanged.

### Compile cost (P5)

The stated objective is examined rows, which is an execution quantity; nothing in
revision 7 would have caught an optimiser that made every query far slower to compile in
order to save a fraction of execution. Two mechanical guards:

- **Compile-cost witnesses in the corpus.** Named entries at each budget boundary record
  a compile-time bound, measured as candidate lowerings and store-free wall time, for
  baseline compilation and for optimised compilation. The gate asserts the bound.
- **A ratio bound in issue-level acceptance.** Optimised compilation of every corpus entry
  stays within a stated multiple of baseline compilation of the same entry, and the
  multiple is recorded in the audit table with the budget values that produce it. If the
  budgets have to move to satisfy it, they move here, in the open, rather than being
  discovered later as a performance report.

### End-to-end cost-plan corpus acceptance

Fill each entry's expected selected order, mode, cost, full PlanView, fingerprint, and
rows. For every entry the gate:

1. compiles the explicit baseline order through `flatten_in_order`;
2. compiles normally through the production optimiser;
3. compares both full plans and fingerprints to their snapshots;
4. independently recomputes both abstract costs;
5. asserts exhaustive selections are the global oracle winner over the admissible space,
   or proves the exact fallback reason and fresh-greedy result;
6. executes both plans, compares each with its separately stated ordered rows, and
   compares both sorted results with the stated semantic multiset;
7. for performance witnesses, compares exact `Profile::total` and store-operation counts
   and proves the improvement grows with fixture scale;
8. for compile-cost witnesses, asserts the recorded compile bound.

The corpus includes an honest hostile-distribution entry where nominal costing chooses a
slower legal plan; its answers must still agree and the book uses it to state the proof
boundary. It is not marked as an optimiser failure.

### Fingerprint and cross-target proof

Plan order and every changed residual/source position already participate in the plan
fingerprint: `PlanFingerprint`'s plan walk (`plan.rs:1286-1330`, entered from
`Plan::fingerprint`, `:833`) hashes `plan.body` in order and each source's residuals in
order. A moved selected plan therefore moves the fingerprint and refuses an old cursor as
`CursorPlan`; unchanged plans retain their exact value. No cursor layout changes, so no
`CURSOR_VERSION` bump.

The admissibility class is the one place where plans move for queries that were never
reordered before. Those entries carry both fingerprints and an explicit note that the
movement is the P1 correction landing, so a reviewer reading the diff sees a stated
consequence rather than unexplained churn.

The native and real wasm compilers consume the same serialized corpus inputs and emit
the selected order, selection mode, full PlanView JSON, cost breakdown, and fingerprint.
`web` smoke compares the two outputs byte for byte in the same gate. The comparison
includes every arithmetic boundary and selection/fallback taxonomy entry, not only the
language corpus.

Rewrite `query-efficiency.md:212-244`; retain the key-order warning and state the
exhaustive and runtime proof boundaries. Update PLAN.md's cost-model decision.

**Exported facts for Run 7:** default/nominal planning is deterministic; exhaustive mode
is globally optimal over the admissible space under the model; every fallback is
deterministic; all modes preserve answers and fingerprint/resume safety.

---

## Run 5 — exact per-predicate counts in the one seal walk

**Claim:** a sealed database records one exact count for every non-virtual declared
predicate, and old sidecars remain readable.

`approximate_len()` was the wrong source twice: fjall promises only *"For insert-only
workloads … this value is reliable"* (`fjall-3.1.8/…/keyspace/mod.rs:476-481`), and the
exact count is already free — `identity::compute` (`identity.rs:100`) already walks every
fact, counting on the way (`identity.rs:86-92`: *"Facts counted on the way — free, since
the walk visits every one"*).

Initialize a count slot **from the embedded schema** for every non-virtual predicate,
including zero-count predicates. This is the correction the existing loop needs:
`identity::compute` iterates `for predicate in db.predicate_ids()` (`identity.rs:111`),
which yields only predicates that have trees, so a declared predicate with no facts would
otherwise be absent rather than zero — and "no facts" must be distinguishable from "no
statistics". Increment the slot in that existing fact walk. Return the ordered counts with
`Identity` and carry them through `Finished`. `record` writes identity, total count,
per-predicate counts, bytes, and the status flip in its existing single atomic sidecar
write.

```rust
struct PredicateCount {
    id: PredicateId,
    name: String,
    facts: u64,
}
```

`Meta` gains `predicates: Option<Box<[PredicateCount]>>` with `default` and
`skip_serializing_if`; no version bump — these are the additions the versioned format can
take, per the module doc at `meta.rs:17-26`, whose "the field list is fixed" paragraph
updates with it. The list is in predicate-id/declaration order. Virtual predicates never
appear. At construction and load-for-planning, validate exactly one entry per non-virtual
declaration, the id/name pairing, order, and count bound. Malformed present statistics are
not silently treated as zero or as legacy absence.

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

**Exported facts for Runs 6–7:** `Some(counts)` is a complete validated generation-1
statistics set and a pure function of sealed content; `None` means legacy/no statistics,
not zero.

---

## Run 6 — `fjord.db.Stat` and per-virtual-table world digests

**Claim:** holding cursor version, plan, and base world equal, and under F0-b's 64-bit
digest collision model, a cross-request resume over stable virtual predicates is accepted
exactly when every virtual table the plan reads has identical row bytes.

Add `fjord.db.Stat {name, instance, predicate, facts}`. Several instances may share a
name, hence both identity fields. Complete entries with validated counts produce one row
per non-virtual predicate, including zeros. Writable and legacy entries produce no Stat
rows; absence is not a fabricated sentinel, and a `-1` cannot be built anyway, since a
sidecar with no counts also has no predicate names.

Generalise the `reads_listing` flag (`session.rs:1469-1470`, set at `:1545`, read at `:1021` and `:1201`) and `with_listing_digest` (`:1640`) to a
sorted, length-framed sequence of `(predicate_id, table_digest)` for every stable virtual
predicate the plan reads, including an empty table. `Table` already owns a per-table
`digest` (`catalogue.rs:202-208`), so this is a generalisation rather than a third boolean.
`digests()` (`catalogue.rs:281`) filters empty tables because it answers "what could have
minted an id", a different question; reusing it here would conflate zero rows with absent.
Keep `fjord.db.Interning` excluded and keep its existing cross-request refusal. Fetch
frames continue to carry digests only for non-empty tables that could have minted ids.

The protocol already carries a framed per-predicate list — `listing_digests:
Vec<(PredicateId, u64)>` on the client's `Rows` (`rows.rs:64`, `:127`), decoded per
`LISTING_DIGEST` frame (`connection.rs:772`) — so this is server-side only: no wire shape
change and no client change.

Move complete resume preflight ahead of `ROW_DESCRIPTION` (`session.rs:1145`) in this run. A resumed request
materialises its catalogue, obtains the first execution reader and its fully composed
base-plus-virtual world, checks cursor version, plan, then world in the same precedence
as `Executor::resume` (`iter.rs:2025`), and retains that exact reader/world pair for the
first chunk. This is required evidence for Run 6's own refusal claim — a refusal you
cannot observe before the row descriptor is not a proved refusal — not work deferred until
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
  same bytes — this is the consumer of **F0-b**, and the test names it.

Add `fjord db stat` and `:stat` with automated CLI/shell output tests and update the book
(`operations.md`, `cli.md`, `shell.md`, `status.md`) and the PLAN.md gap row.

---

## Run 7 — exact base counts feed planning

**Claim:** content-equivalent Complete databases carrying the same validated statistics
generation compile identically; legacy and live Writable databases plan nominally;
statistics-mode changes are resume-safe.

### Settled legacy policy

Do **not** backfill and do **not** put a statistics digest in the world stamp.

- `Some(valid generation-1 counts)` on a Complete database selects exact-base mode.
- `None` selects nominal mode, including legacy Complete databases.
- Writable databases select nominal mode until sealing publishes their `Completion`.

The plan fingerprint already hashes the finalized plan structure (`plan.rs:1286-1330`).
If two modes choose different plans, cross-mode resume is `CursorPlan`. If they choose the
same plan, resume is semantically safe and succeeds; refusing merely because unused
estimates differed would conflate planning input with database world. The world stamp
continues to identify rows, not optimiser policy.

### Ownership and race-free publication

Replace the server's standalone completion fingerprint with one immutable publication:

```rust
struct Completion {
    fingerprint: u64,
    predicate_counts: Option<Arc<[PredicateCount]>>,
}
```

`Database` initializes it from Complete metadata at registry open. A live finish carries
Run 5's counts through `Finished`, publishes `Completion`, then clears `writable` while
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

Run 6's retained-reader preflight is reused unchanged. Statistics remain outside the
world: prepare compiles from its one `PlanningContext`, then preflight obtains and retains
the first execution snapshot. Complete counts describe that immutable content; Writable
planning is nominal. When both plan and world mismatch, preflight reports `CursorPlan`,
matching the engine's existing order (`iter.rs:2025`), and every refusal occurs before
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

Update PLAN.md's "spend it on pruning, not join ordering" decision, the operations and
query-efficiency chapters, and the resume check-order documentation.

### Rung 3 remains recorded, not scheduled

Prefix/range histograms, cross-key fan-out, and residual selectivity require a new
statistics generation and this same corpus discipline. Statistics may use only
compile-time constant key material: a `SeekKeyPart::RegisterField`/`RegisterFactId` has no
compile-time value, so a bound-variable join uses a join statistic or the nominal
fallback, never a prefix lookup. Reference histograms may not key on physical `FactId` —
those are claimed at intern time and are ingest-order dependent, which is exactly why the
content fingerprint hashes logical form; they must aggregate canonical logical targets or
an order-independent distribution, and prove equivalence across builds assigning different
ids. The rebuild-stability rule stands: a cost model may read only quantities that are a
pure function of database content. Fjall exposes `approximate_len()` and an O(n) `len()`
but **no rank or range-count API**, so the issue's "`rank(hi) - rank(lo)`, two seek
positions, no scan" is unavailable and exact prefix counts require seal precomputation.

No `#[ignore]`d guards are added for this unscheduled work: `scripts/check-guards.py` owns
a single global movement namespace (`MOVEMENTS = range(0, 9)`, the recursion movements) and
nothing here is invariant-critical. PLAN.md is the home; recorded as a decision.

---

## What the optimiser cannot fix

- A row bind that claims its variable (`flatten::Claims`, `flatten.rs:351`, decided at `:1195-1240`) prevents
  another occurrence from being a capture; ordering cannot invent a missing access path.
  `D = src.Decl _; src.SearchByName {to = D, name = N}` is the 30s-to-2ms case at
  `fjord-viewer/src/query.rs:236-238`, and no cost model reaches it.
- A join whose inner side cannot seek on the schema's declared leading fields is a
  key-order problem. FINDINGS §2's own case is already fixed at the schema (`src.Decl` is
  now `{module, name, line}`), so — contrary to the issue's acceptance sketch — §2 is not
  available as the witness. Run 4's witness constructs a case where the frontier genuinely
  has a choice. The cost corpus includes the key-order case as an **unchanged-plan
  negative control**, not as a repair.

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
| 1 | admissible/`safe()` equivalence, strictness cases, coincidence condition, non-emptiness, seed mutations |
| 2 | cost arithmetic, complete-order reference evaluator over the admissible space, mutation controls, taxonomy census |
| 3 | old/new emitter differential over every admissible order, trace/plan classifier, emission-inertness, interner-stability guard, baseline snapshots |
| 4 | exhaustive/fallback selector properties, written-order replacements, budget boundaries, compile-cost witnesses, complete pre/post corpus, native/wasm comparison, examined scaling witnesses |
| 5 | identity/count model, structural count contract, sidecar compatibility, one-write seal |
| 6 | Stat row model, virtual-world pair property, server paging/fetch, CLI and shell |
| 7 | exact-count corpus, equivalent builds, live finish publication, cursor precedence, native/wasm counts |

### By hand, once the runs land

Cheap, and the only check a person actually looks at:

- after Run 4: `:plan` a query written with its selective generator second and see it
  compile with that generator first; `query --profile` before and after shows the examined
  count moving with identical rows; `:plan` one of the admissibility-class queries and see
  a plan `reorder` could not have produced.
- after Run 6: `fjord db stat <name>` and `:stat` report per-predicate counts; page
  `fjord.db.Stat`, `finish` a database mid-page and watch the resume be refused, then
  repeat with a `create` and watch it resume.

## Issue-level acceptance

Issue #18 closes only when all of the following are simultaneously true:

- Every planned cost-plan corpus entry has reviewed baseline and selected full-plan
  snapshots, fingerprints, independent costs, ordered rows, semantic multiset, and a
  selection classification; every diagnosed entry has its exact code and span.
- The corpus taxonomy census and mutation controls are green, including the
  admissibility and compile-cost classes.
- **The admissible predicate is proved equal to `safe()` on complete permutations, and
  every exhaustive claim in this document is scoped to that space explicitly.** No
  optimality assertion is allowed to pass over an empty candidate set: the non-emptiness
  assertion is part of the gate, not a comment.
- Exhaustive selections equal the independent global optimum over the admissible space;
  every non-exhaustive case proves its exact fallback reason and fresh-greedy result.
- Baseline and selected plans agree on the semantic result multiset for every entry and
  for the generated schema-first model across every admissible forced order; each also
  matches its own recorded execution order.
- `preserves_written_order` and its proptest are unchanged and green over the frozen
  `reorder`; the two replacement properties are green over the two selection modes; and
  `reorder.rs:62-88` names its actual consumers.
- Named witnesses improve exact examined/store-operation counts with scale; the hostile
  nominal witness documents the absence of a universal runtime claim.
- **Optimised compilation stays within the stated multiple of baseline compilation for
  every corpus entry, and the budget values that produce it are in the audit table.**
- Native and wasm outputs are byte-identical for the complete nominal and exact-count
  corpus inputs.
- Plan changes move fingerprints and stale cursors refuse before a descriptor; unchanged
  plans retain fingerprints and resume. Every plan that moves for a query that was never
  reordered before is an admissibility-class corpus entry with the movement stated.
- Exact counts are a validated pure function of sealed content, including zero and
  content-equivalent rebuilds, and the live-finish path publishes them whole.
- The complete repository gate is green and the book and PLAN.md state the shipped
  model, limits, fallbacks, statistics policy, and proof boundary exactly as the tests do.
