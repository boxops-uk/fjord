# Cost-based reorder — issue #18 (revision 9)

## Destination and route

**The destination is a dynamic program over subsets, exact to an order of magnitude more
statements than enumeration can reach. Exhaustive enumeration is not the product; it is the
oracle that makes shipping a DP legitimate, and the fallback when a query carries a hazard
the DP cannot absorb.**

The "order of magnitude" is deliberately vague, and Run 5 is what makes it precise. State
counting bounds the DP at roughly 12–15 statements; whether that range is *reachable* depends
on the per-transition constant — each transition still lowers one statement — and that
constant is unmeasured. A reviewer should read every specific `n` below as a state-count
bound awaiting a compile-cost measurement, not as a performance claim.

Revision 8 built the enumeration and mentioned a possible future DP in one sentence, which
read as a concession. That was the wrong emphasis, and the arithmetic says so. Each
enumeration candidate is a **full lowering** of every statement; a DP's states are
prefixes, each lowered once and reused by every extension:

| order-relevant statements | enumeration (statement-lowerings) | DP (statement-lowerings) | ratio |
|---|---|---|---|
| 5 | ~600 | ~160 | 4x |
| 6 | ~4,300 | ~384 | 11x |
| 8 | ~320,000 | ~2,000 | 157x |
| 10 | ~3.6e7 | ~10,000 | 3,500x |
| 12 | ~5.7e9 | ~49,000 | 100,000x |

Revision 8's own budget of 256 complete candidates is exceeded at **six** independent
statements. So revision 8's "exhaustive" mode covered about five order-relevant statements
and dropped everything above that to a greedy rule that makes no optimality claim at all.
`Constrain` and `Compare` are partitioned out first, so filter-heavy queries count lower
than their statement count — but a seven-generator query is not exotic, and it was already
past the ceiling. A DP moves exact optimality from *toy* to *useful*.

What revision 9 does not do is ship the DP first. The prefix-cost recurrence is **false as
stated** against the code (verified; see the amendment taxonomy below), any repair is a
non-trivial claim, and there is no way to certify such a claim except against a trusted
answer for the same query. You cannot bootstrap a DP into correctness using only itself.
So the route is: build the oracle, prove the recurrence against it, then ship the DP with
the oracle retained as a permanent cross-check and fallback.

`reorder` today takes the lowest-numbered member of the runnable frontier
(`reorder.rs:263-292`, the pick at `:274-280`). That is a deterministic legality rule, not
a cost model — the module says so at `reorder.rs:236-256`, and the book says so at
`query-efficiency.md:212-244`. This work replaces the tie-break with a costed choice
without changing the set of legal orders or the executor.

Nine independently reviewable runs, numbered 0–8. Every run has one falsifiable claim, a
mechanical gate for that claim, and an explicit statement of what later runs may rely on.

Two limits are part of the specification:

1. The optimiser is **exact under its declared, clamped estimate model only when it
   completes a search within its budgets** — exhaustively in enumeration mode, or over the
   full state table in DP mode. On overflow it discards the entire search result and runs
   deterministic greedy selection from the beginning.
2. No statistics-free estimate can promise universal runtime improvement. The optimiser
   preserves answers, is exact under its model when complete, and improves named scaling
   witnesses. The book states that a different data distribution may favour another legal
   plan.

---

## What revision 9 changes

| # | Change | Why |
|---|---|---|
| A | **DP promoted from a deferred aside to Run 5, the plan's stated destination.** Enumeration is reframed as oracle plus fallback, and may stay capped at n≈6 permanently in that role. | The value gap is 100,000x at n=12; revision 8 buried it. |
| B | **The amendment taxonomy** — every in-place level mutation classified as retroactive or prefix-determined, with code evidence. Two are retroactive, four are not. | Revision 8 asserted "the recurrence is broken" without saying *by exactly what*, which is unarguable-with and therefore unreviewable. |
| C | **`apply_selects` identified as a second retroactive site.** A union select `X.alt?` read by a late statement prepends a discriminant filter to the level that bound `X`. | Found while grounding B. Any hazard test that names only `chase` is wrong. |
| D | **The hazard admission test made concrete**: no chasable row bind and no union select, both decidable before ordering. | Turns "a DP fast path someday" into a testable gate. |
| E | **Bellman locality as the validation strategy**, replacing "cross-check against the oracle" — which cannot reach n=12 by construction. | The obvious adversarial attack on any DP phase, answered structurally. |
| F | **Run 2 prices the non-retroactive-amendment variant** across the corpus. | If landing chased/selected residuals at their own position costs little, the hazard disappears and the DP is unconditional. Cheap to measure, decisive if it works. |
| G | Runs 5–7 of revision 8 shift to 6–8. | One insertion. |

Everything revision 8 established is carried forward unchanged: the admissible-order
predicate (Run 1), the split Run 0 exported facts, emission-inertness, the written-order
replacements, compile-cost bounds, the fault/skip separation, the defect ledger, and
verified citations. All citations were re-checked against the working tree at `51d4868`.

### Run numbering: revision 8 to revision 9

| rev 8 | rev 9 | Subject |
|---|---|---|
| 0 | 0 | virtual ids are functions of rows |
| 1 | 1 | the admissible-order predicate |
| 2 | 2 | cost specification, oracle, **amendment pricing** |
| 3 | 3 | one annotated emitter, chooser unchanged |
| 4 | 4 | bounded enumeration and greedy fallback — **the oracle** |
| — | **5** | **the subset DP — the destination** |
| 5 | 6 | exact per-predicate counts |
| 6 | 7 | `fjord.db.Stat` and virtual world digests |
| 7 | 8 | exact base counts feed planning |

Run 5 sits immediately after Run 4 deliberately: the DP's oracle is Run 4's enumeration,
its corpus is Run 4's corpus, and validating the recurrence on *nominal* costs — before
statistics change the values in Runs 6–8 — separates "is the recurrence sound" from "are
the estimates good". A reviewer preferring statistics earlier should say so; the
counter-argument is that Runs 6–8 deliver standalone value (`fjord db stat`, exact counts)
and would then land behind a research-shaped run.

---

## The defect ledger

Five adversarial reviews have now run against this plan. The table is the plan's memory: a
premise refuted here may not return without new evidence against the row that killed it.

| Rev | Defect | Verified at |
|---|---|---|
| 1→2 | DP substructure premise false — a product of per-statement *access* estimates is not a set function | counterexample, pinned as `a_product_of_access_estimates_is_order_dependent` |
| 1→2 | Cost model described statements, not emitted operators | `flatten.rs:2777`, `:2781`; `iter.rs:493` |
| 1→2 | `Cardinalities::prefix(pred, fields)` cannot express rung 3 | design |
| 1→2 | `fjord.db.Stat` an unguarded I4 regression | `session.rs:1469-1470`, `:1545`, `:1640` |
| 2→3 | Subset alone is not a sufficient DP state — `fetch_level` reuses registers | `flatten.rs:1966-1991` |
| 2→3 | One `Effect` cannot describe one statement; each disjunct builds its own seek | `flatten.rs:2646-2700` |
| 2→3 | The claim that moving `Constrain`/`Compare` shifts addresses was **wrong** | `flatten.rs:474-495` |
| 2→3 | `approximate_len()` is "reliable", not exact — and the exact count is free in the identity walk | `fjall-3.1.8/…/keyspace/mod.rs:476-481`; `identity.rs:100-111` |
| 3→4 | Fetch reuse is **amended**, not just reused | `flatten.rs:1997-2032`, loop at `:2022-2031` |
| 4→5,6 | A stateful DP is no longer bounded by `2^n`; cardinality still conflated semantic relations with physical access | design |
| 6→7 | **The prefix-cost recurrence is invalid outright** — a later statement retroactively changes cost already charged to a prefix | `flatten.rs:1997-2032` |
| 6→7 | An audit that grades the optimiser by its own cost model proves nothing; the baseline must be data | design |
| 7→8 | **The enumeration's legality predicate is strictly narrower than `safe()`** — clean queries get an empty candidate set and the optimality gate passes vacuously | `flatten.rs:2531-2543` vs `reorder.rs:263-292` |
| 7→8 | `preserves_written_order` is public API with a live proptest and a doc naming it as `Placement`'s purpose; a cost chooser breaks it by construction | `reorder.rs:62-88`, `:304-331`, `:628-653` |
| 7→8 | Placement-inertness asserted as "already proved" | `flatten.rs:474-495`, `:2777`, `:2781` |
| 7→8 | Compile time unbounded and unmeasured while execution rows are the objective | `flatten.rs:2602` |
| **8→9** | **`apply_selects` is a second retroactive amendment site** — a union select read late prepends a discriminant filter to the level that bound the row. Any hazard test naming only `chase` is incomplete | `flatten.rs:2842-2868`, recorded at `:4672` |
| **8→9** | **Revision 8 treated the DP as a deferred aside**, understating its value by ~5 orders of magnitude at n=12 and leaving the enumeration budget (n≈5) as the shipped optimality range | arithmetic above; budget at rev8 Run 4 |
| **8→9** | "Validate the DP against the oracle" is not a strategy — the oracle cannot reach the n the DP exists to serve | design; answered by Bellman locality |

---

## The amendment taxonomy

This section is new and is the plan's technical core. The recurrence

```text
cost(S + m) = cost(S) + card(S) * access(m | state(S))
```

is valid exactly when **no amendment triggered by placing `m` changes the cost of an
operator already emitted for `S`**. Revision 8 asserted that some do. Revision 9 says which,
because a reviewer cannot argue with an unqualified assertion and cannot design around one
either.

`Body::level_mut` (`flatten.rs:508`) is the only way an already-emitted level is modified.
Every call site, classified:

### Retroactive — reaches past the level that bound the variable

| Site | Trigger | Attaches to | Effect |
|---|---|---|---|
| `chase` (`flatten.rs:2022-2031`) | placing a chasable row bind whose reference was already fetched | the **earlier** fetch level found by `(address, path)` reuse (`:1973-1975`) | filters that level, reducing rows for every level in between |
| `apply_selects` (`flatten.rs:2842-2868`) | **any** statement reading `X.alt?`, however late | the level that bound `X`, recorded at `:4672` as `(address, path, disc)` where `address` is the union-holder's level | prepends `DiscriminantEq`, same shape |

Both fit the same pattern: the attachment point is chosen by *what the value is*, not by
*when it became available*, so placing a statement at position `k` can reduce the cost of
operators at positions `j < k` that a prefix walk has already charged and banked.

### Prefix-determined — attaches to the level that bound the variable

| Site | Attaches to |
|---|---|
| `apply_constraints` (`flatten.rs:3800-3850`) | `self.lookup(symbol)`'s level — the one that bound it |
| `apply_denials` (`flatten.rs:4298-4340`) | same |
| `apply_comparison` via `push_residual` (`:3995`, `:4274-4295`) | the level binding the compared field |
| `apply_compares` (`flatten.rs:2883-2940`) | the **later** of two bound levels (`:2919-2924`), which is the one whose placement completed the pair |

For all four, the constraint/denial/comparison set is collected from the whole body
**before** an order is chosen, so at the moment a prefix binds a variable, every amendment
that variable will ever attract is already known and attaches to the level just added.
Nothing reaches backwards.

**Proof obligation, discharged in Run 2, not assumed here:** that this classification is
exhaustive and correct. The property is mechanical — for every generated query, every
prefix `S` and every extension `m`, assert that the set of operators whose emitted form
differs between `lower(S)` and `lower(S + m)[0..|S|]` is empty when the query passes the
hazard test, and is confined to the two retroactive sites otherwise. That is a direct
observation of the plan structure, not an argument about it.

### The hazard admission test

A query is **hazard-free** when it contains no chasable row bind and no union select.

Both halves are decidable before any order is chosen:

- **Chasable.** `chasable()` (`flatten.rs:1614-1656`) sets a per-statement flag from the
  whole statement list during collection, before `claims()` and before ordering. `chase` is
  reached only through `generator.chasable` (`flatten.rs:2620-2629`). Condition 1 requires
  the key to give no constant bytes (`gives_no_constant`, `flatten.rs:1666-1676` — all
  captures and wildcards);
  condition 2 requires another statement to reference the row at a fact-typed field where
  splicing the id would *not* extend a seek. Narrow.
- **Union select.** A syntactic property of the query: does any expression use `p.alt?`.

The test must be **conservative** — hazard-free must imply genuinely no retroactive
amendment, never the reverse. Run 5's gate proves the implication directly rather than by
reading the conditions.

### Two routes out, and Run 2 prices both

1. **Hazard-free fast path.** Most queries have neither construct, and the queries that need
   large `n` are multi-join analytical ones where chasability's condition 2 rarely holds.
   Run 5 uses the DP for these and enumeration otherwise.
2. **Make the amendments non-retroactive.** Emit the chased bind's residuals and the
   discriminant filter as their own step at their own position instead of reaching back.
   Strictly worse plans — a filter at position 7 does not reduce levels 4–6 — but bounded,
   local to two functions, and it makes the hazard vanish so the DP becomes
   **unconditional**. Whether that trade is worth taking is an empirical question about
   plan quality across the corpus, and Run 2 measures it rather than arguing it.

If (2) turns out cheap, (1) is unnecessary and Run 5 simplifies enormously. That is why the
measurement lands in Run 2, before either is committed to.

---

## The two order predicates, and why the difference is load-bearing

Carried from revision 8, where it was the central correction. Revision 7 said the search
would use "the same monotone legality condition as today", and that sentence hid a real
difference between two conditions the codebase already has.

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
feasibility oracle the completeness proptest at `reorder.rs:605-614` compares against — use
that same empty-seeded predicate.

`safe()`, the check that actually decides whether a compiled order is legal, seeds from
**every folded constant** first:

```rust
// flatten.rs:2531-2543
let mut bound: Vec<Symbol> = self
    .bindings
    .iter()
    .filter(|(_, slot)| matches!(slot, Slot::Const(_)))
    .map(|(symbol, _)| *symbol)
    .collect();
```

The gap is real because **a constant bind produces no statement at all**. `X = 42` is folded
at collect time (`flatten.rs:1461-1464` → `fold_into`, `:4472-4474`, recording
`Slot::Const`) and contributes no entry to `stmts` and therefore none to `Deps`. So nothing
in the graph *captures* `X`, while statements that *read* it keep it in `reads`
permanently: a negation moves every capture into reads (`flatten.rs:1051-1058`), a
constraint reads its variable (`:1065`), a comparison reads both sides (`:1071-1074`), a
derived bind reads its operands (`:1077-1080`), an alias reads what its value is rooted at
(`:1012-1029`).

That the asymmetry is deliberate is visible in the code compensating for it:
`negated_wildcards` (`flatten.rs:2141-2154`) seeds its own `bindable` set with the folded
constants before deciding whether a negation's read is a genuine unbound-variable error.

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

Not a negation quirk: every statement kind that carries `reads`, over any variable bound
only by a constant fold.

### What that did to revision 7, and what revision 8 decided

Admission admitted these queries; enumeration under the narrow predicate yielded **zero**
candidates; that is not a budget crossing so no fallback fired; and the criterion
*"exhaustive selections equal the independent global optimum"* passed **vacuously** over a
class the optimiser silently never touched.

The decision, retained:

**`reorder` is frozen.** It keeps the empty-seeded predicate and is not modified. Its only
remaining production role is to compute the **admission baseline order** in Run 4. Seeding
`reorder` itself would change which order the dead-end path returns for the class above,
moving plans and fingerprints in a run whose gate is a legality correction; plan movement
belongs where plan movement is the subject.

**The admissible-order predicate** is defined to be `safe()`'s:

> A complete permutation `o` is **admissible** when, walking `o` from a bound set seeded
> with the query's folded-constant symbols, every statement's `reads` are bound at the
> position it occupies.

Run 1 proves: **for a complete permutation, `admissible(o)` holds exactly when `safe(o)`
returns true.** Two halves — the loop half is a syntactic correspondence
(`flatten.rs:2549-2566`); the head half is **order-invariant**, because `safe()` also checks
`collected.head_reads` against the final bound set (`flatten.rs:2568-2576`) and after a
complete permutation that set is `consts ∪ every capture` regardless of order, `Deps` being
computed once at collect time. So head reads can never make one complete order safe and
another unsafe.

Three consequences, each replacing something revision 7 left open:

1. **The candidate set is never empty after admission.** Admission ran `safe()` on the
   baseline order; if it passed, that order is itself admissible. `|candidates| >= 1`
   always, asserted rather than assumed.
2. **Every enumerated candidate is safe by construction**, so a candidate can never be
   rejected for safety mid-search.
3. **The optimiser reaches orders `reorder` cannot produce** — exactly the probe class.
   More optimisation, but plans move for queries never reordered before, so the corpus
   carries a class for it with today's collection-order plan frozen as the baseline.

`Deps` gains `pre_bound: Box<[Symbol]>` from the folded constants, and
`respects_admissible` / `antichains_admissible` alongside the existing pair. The existing
pair is **not** changed — the proptest at `reorder.rs:605-614` pairs `respects` with
`antichains` as `reorder`'s own oracle, and changing either stops that test testing
`reorder`.

---

## The permanent acceptance artifact: the cost-plan corpus

`fjord_engine::cost::corpus` is a table of complete planning cases, following the discipline
of the existing target-feature corpus (`fjord-engine/src/corpus.rs`, gated at `lib.rs:54-55`
under `cfg(any(test, feature = "proptest"))`) for the reason that module gives: a written
target in prose drifts, so the table lives as data next to the tests that check it.

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
hazard classification and, where it differs, the non-retroactive-variant plan and cost
coverage tags
```

`selection mode` is one of `DynamicProgram`, `Exhaustive`, `GreedyStatementLimit`,
`GreedyCandidateBudget`, `GreedyTransitionBudget`, `GreedyStateBudget`,
`BaselineCandidateFault`, or `Unorderable`. Planned cases state every field. A diagnosed
entry instead states the exact diagnostic code and span and proves that neither baseline
nor selected compilation produced a plan. A case cannot say only "supported".

The baseline is not compiled through a retained production chooser. Its order is data in
the entry, handed to `flatten_in_order` (`flatten.rs:678-686`, itself gated under
`cfg(any(test, feature = "proptest"))`), so changing the optimiser cannot silently move both
sides of the comparison. The selected side is compiled through the real production entry
point. Both plans execute against the entry's store and must return their separately stated
ordered rows; sorting those rows must produce the one stated semantic multiset. Reordering
legitimately changes traversal order, so one shared ordered expectation would either reject
correct plans or hide the difference by sorting too early.

Full structural snapshots use `fjord-inspect`'s interner-free JSON view. Corpus data lives
in `fjord-engine`; the snapshot runner lives in `fjord-inspect/tests`, the first crate
allowed to see both the engine and its JSON view. Snapshot regeneration is an explicit
command writing to a temporary candidate file; the test never blesses a change
automatically.

A census fails unless the corpus reaches every item in this taxonomy:

- physical access: empty source, full scan, partial prefix seek, point seek, bounded range,
  guided seek, fetch, and probe;
- lowering: one and several levels, disjunction, empty disjunction, whole-key variable,
  nested record, repeated variable, intra-row residual, alias, derive, head fetch, lookup
  chase, access-chain fetch, reused fetch, and reused fetch amended once and more than once;
- filters: positive constraint at a seek, positive residual constraint, denial, constant
  comparison, field/constant comparison, two-register comparison, derived comparison,
  negation, and residual-order sensitivity;
- admissibility: a folded constant read by a negation, by a derived bind, by a comparison,
  by a constraint, and by an alias value; the same query with and without the fold; and a
  query where the admissible space strictly exceeds `reorder`'s reachable space, with
  `reorder`'s dead-end plan frozen as the baseline;
- **amendment (new in revision 9): each of the six `level_mut` sites reached at least
  once; a chase amendment reaching back across one intervening level and across several; a
  union select read at the binding statement, one statement later, and several later; a
  query with both hazards; a query with neither; and for every hazardous entry, the
  non-retroactive variant's plan and cost recorded beside the current one;**
- selection: source order wins, a later statement wins, a genuine cost tie, folded
  statements in every legal region, an unorderable query, each exact budget boundary, each
  one-past fallback, injected candidate failure, and discovery-order perturbation;
- **DP (new in revision 9): a hazard-free query where DP and enumeration agree; one where
  the DP state table is exercised at each boundary; a hazardous query routed to enumeration;
  and a query where DP and greedy differ, proving the DP is doing work;**
- arithmetic: zero, one, every nominal constant, every clamp, and `u64::MAX`;
- statistics: nominal, exact zero, exact skew, legacy/no statistics, and two
  content-equivalent builds with different physical fact ids;
- compatibility: unchanged fingerprint, deliberately changed fingerprint, native/wasm
  equality, resume success, and resume refusal;
- compile cost: a query at each budget boundary with a recorded compile-time bound, in both
  search modes.

The census is about populations as well as names: generated companions must reach each shape
a minimum number of times, and mutation controls must make the relevant gate fail when an
access rank, factor attachment, amendment, tie-break, clamp, fingerprint tag, fallback
boundary, admissibility seed, **or hazard classification** is changed.

---

## Run 0 — virtual ids are functions of rows, not iterator order

**Claim:** every permutation of the same virtual rows produces identical scan rows,
`FactId` mappings, per-table digests, and point-read answers.

`Table::of` (`fjord-server/src/catalogue.rs:349-393`) mints
`FactId::new(predicate, sequence + 1)` from the **input iterator order**, inside the loop
(`:373`), and sorts into key order **afterwards** (`:386`); `digest_of_rows` (`:396-409`)
hashes the predicate, the row count, and the framed keys — never the ids. `listing_rows`
walks the store root, so input order is filesystem-dependent. Two permutations of the same
logical rows give identical scan bytes and an identical digest but a **different
`FactId` → row mapping**.

Shipped behaviour on `fjord.db.List` today, not a hazard this work introduces. Encode key
bytes first, sort, then enumerate from one to mint dense ids. The digest continues to hash
predicate and sorted keys; ids become a function of those keys. Ids still start at 1 and are
still dense per predicate, so nothing downstream meets a fact id shaped differently from
every other (`catalogue.rs:371-372`).

### Proof

- A generated permutation property compares the complete `(key, FactId)` scan, digest, and
  every valid and one-past point read across all permutations. Includes empty, singleton,
  duplicate-identical rows, several rows, and both current virtual tables.
- A legacy/new resume case constructs the same plan, same world digest, and same saved key
  under the two id assignments, proves those premises, then requires `BadResumeKey`. A plan
  or world mismatch is not allowed to make the test pass.
- A fetch case proves a stale virtual id is refused through the per-predicate digest.

No `CURSOR_VERSION` bump unless the forced legacy/new case is accepted. Record the shipped
defect and its guard in PLAN.md.

### Exported facts, split

- **F0-a (construction).** Equal sorted row bytes imply equal `key -> FactId` mappings.
  After the fix this holds **by construction** — ids are enumerated from the sorted keys —
  and needs no collision model.
- **F0-b (digest).** Equal per-table digests imply equal sorted row bytes, **under the
  repository's accepted 64-bit digest collision model**. This is what Run 7 consumes to turn
  "the world stamp matches" into "the rows the plan reads did not move", and the only place
  the collision model is needed.

---

## Run 1 — the admissible-order predicate

**Claim:** a complete permutation is admissible exactly when `safe()` accepts it; the
admissible space is non-empty for every query that compiles; and it strictly contains the
space `reorder` can reach.

Nothing in production consumes this run. Its entire output is a proved predicate that Runs
2, 4 and 5 build on — separated because revision 7 folded this premise into two runs as an
assumption and it was false.

### What is built

- `Deps` gains `pre_bound: Box<[Symbol]>`, populated at collect time from the
  folded-constant bindings — the same set `safe()` seeds from (`flatten.rs:2534-2540`) and
  `negated_wildcards` already seeds from (`:2148-2154`).
- `Deps::respects_admissible` and `Deps::antichains_admissible`, seeded from `pre_bound`.
- `Deps::respects` (`reorder.rs:147-171`), `Deps::antichains` (`:183`) and `reorder`
  (`:263-292`) are **not modified**.

### Proof

- **The equivalence**, as a property over the generated schema-first model: for every
  generated query and every complete permutation, assert
  `respects_admissible(o) == safe_accepts(o)`, where `safe_accepts` observes `safe()`'s
  verdict through a test-only hook without compiling. Both halves asserted separately — the
  prefix walk, and the order-invariance of the head-reads check (the final bound set is
  identical across all permutations of one query).
- **The strictness**, as named cases: the two probe queries above, pinned as
  `a_folded_constant_read_by_a_negation_is_admissible_but_unreachable` and
  `a_folded_constant_read_by_a_derived_bind_is_admissible_but_unreachable`, each asserting
  all four of — nothing in `Deps` captures the variable; `respects` is false for the order
  `reorder` returns; `respects_admissible` is true for it; the query compiles cleanly.
- **The coincidence condition.** `respects_admissible == respects` for every order exactly
  when `pre_bound` is empty — what says the new predicate generalises rather than differs.
- **Non-emptiness.** For every generated query that compiles, `reorder`'s returned order is
  admissible.
- Mutation controls: removing the `pre_bound` seed, seeding it with the wrong slot kind, or
  seeding it after the loop must each make the equivalence property fail.

Update `reorder.rs`'s "Why greedy is complete" argument (`:17-30`): it is correct **about its
own predicate** and should say so, naming the admissible predicate as the wider one and
pointing at where the difference is proved.

**Exported facts for Runs 2, 4, 5:** admissible == `safe()` on complete permutations; the
admissible space is non-empty whenever the query compiles; it strictly contains `reorder`'s
reachable space, strictly exactly for queries with a non-empty `pre_bound` read by some
statement.

---

## Run 2 — cost specification, independent oracle, and amendment pricing

**Claim:** the abstract cost of a finalized plan is total and deterministic; the reference
evaluator assigns the same declared cost without using production costing machinery; the
amendment taxonomy is exhaustive; and the non-retroactive variant's plan-quality cost is
measured, not argued.

New `fjord-engine::cost`, used by no production chooser yet.

### Strong units and the objective

The objective is estimated **rows examined**, the quantity `Profile::total` measures
(`iter.rs:503`, `:519`). Distinct `Rows`, `Fanout`, `Selectivity`, `ExaminedCost` newtypes.
`Selectivity` is a reduced integer ratio; application uses zero-preserving ceiling division.
All multiplication and addition saturate at declared clamps. No `f64`, platform-sized
arithmetic, random iteration order, or backend-dependent value — `fjord-engine` builds for
`wasm32-unknown-unknown`, and a plan compiled in the browser must be byte-identical to one
compiled on the server or a cursor minted in one will not resume in the other.

One module-doc audit table states every nominal base, fan-out, filter selectivity, probe
survival estimate, and clamp. Each constant has a named Run 2 corpus/audit case. Boundary
properties cover zero, one, numerator/denominator boundaries, every clamp, and `u64::MAX`
for both cardinality and accumulated cost. Runs 4 and 5 add their budgets and ranks to the
same table when they acquire a production consumer.

### Finalized operator trace

Cost is evaluated over an internal, interner-free `FinalizedCostPlan`, in the physical order
the executor will run:

```rust
struct FinalizedCostPlan { operators: Box<[CostOperator]> }

enum CostOperator {
    Level   { sources: Box<[CostSource]> },
    Probe   { sources: Box<[CostSource]>, survival: Selectivity },
    Derive,
    Compare { survival: Selectivity },
}
```

Each `CostSource` carries its physical access class and the ordered semantic factors that
actually run there. Alternatives sum. A fetch reads at most one row. An access-chain fetch is
cardinality-neutral. A chased predicate contributes its semantic predicate base and
functional-dependency factor at the fetch it ultimately amends. Constraints attach to the
capture that applies them; comparisons to the seek or later residual that applies them;
negation is a probe plus a survival filter. Empty sources produce zero rows.

Walking the finalized sequence maintains estimated input rows: for each operator, add
`input_rows * examined_per_invocation`, then apply the operator's ordered output factors
before visiting its inner successor. Because the walk happens after lowering, a chased
residual attached to an earlier fetch reduces every downstream invocation it actually
precedes.

Factors are canonical semantic identities, not "whichever statement saw this variable". A
variable shared by `k` relational occurrences gets the declared `k`-occurrence
factor/hyperfactor once, not an accidental set of pairwise factors. Constants require no
producer. The module's factor table defines activation and attachment for unary, binary,
multiway, functional-dependency, and constant factors.

### Independent oracle

Tests contain a deliberately slow `ReferenceOrderEvaluator`. Given the collected semantic
statements and one complete order, it builds its own small operator list and applies the
audit-table formula directly. It must not call or construct `FinalizedCostPlan`,
`CostOperator`, production transition/emission helpers, access classification helpers,
production factor activation, or production arithmetic beyond the strong-unit constructors.

Run 2 proves the reference evaluator itself with hand-calculated audit cases for every factor
and operator rule, algebraic properties for factor canonicalisation and total arithmetic, and
a token interpreter: within deliberately small bounds it materialises `input_rows` unit
tokens, applies each declared operator/factor one token at a time, and counts examined tokens
directly.

For small generated abstract cases:

1. enumerate every **admissible** complete order — Run 1's predicate, not `reorder`'s. An
   oracle enumerating the narrower space cannot detect that the search enumerated the
   narrower space;
2. evaluate each with the reference evaluator;
3. compare every operator's breakdown with the token interpreter and the hand-stated boundary
   cases, not only the winning total;
4. choose the minimum `(cost, order)` independently and pin the winner.

The oracle asserts `|admissible orders| >= 1` for every case it is given.

### The amendment taxonomy, proved

The classification in the taxonomy section is a claim about the code, so it is discharged
mechanically rather than by reading:

- **Exhaustiveness.** A guard asserts `Body::level_mut` (`flatten.rs:508`) has exactly the six
  call sites named, and fails if a seventh appears. This is the guard that stops a future
  change silently reintroducing a hazard.
- **The classification property.** For every generated query, every admissible prefix `S` and
  every extension `m`: lower `S`, lower `S + m`, and compare the first `|S|` operators. For a
  hazard-free query the comparison must be **empty** — no operator emitted for `S` differs.
  For a hazardous query, every difference must be attributable to `chase` or `apply_selects`
  and to no other site. This is a direct observation of plan structure, not an argument about
  it, and it is the fact Run 5 rests on.
- **The hazard test's conservatism.** For every generated query classified hazard-free, the
  above comparison is empty for every `(S, m)`. A query classified hazardous need not exhibit
  a difference — the test may over-approximate; it may never under-approximate.

### Amendment pricing

For every corpus entry containing a hazard, record **both** plans and costs: the current
one, and the one produced by a variant emitter in which a chased bind's residuals and a
union select's discriminant filter are emitted at their own position instead of attaching to
an earlier level. The gate records the delta; it does not yet act on it.

This is the measurement that decides Run 5's shape. If the delta is small across the corpus,
the non-retroactive lowering becomes the shipped behaviour, both hazards disappear, and the
DP is unconditional — a far simpler Run 5 than the hybrid. If it is large, the hybrid stands
and the pricing is the evidence for choosing it. Either way the decision is made on numbers
produced before either path is built.

### Counterexamples pinned

- a product of per-statement access estimates is not a set cardinality;
- a later chased bind amends an earlier fetch and changes the cost of an intervening level
  already visited in statement order;
- **a union select read three statements after its binding level changes that level's cost**;
- three relational occurrences of one variable apply one declared multiway rule;
- a clamped tie falls back to lexicographic source order;
- an admissible order that `reorder` cannot reach is enumerated and costed.

Mutation controls alter the reference rule, token rule, a hand-stated expected breakdown, or
a hazard classification one at a time and must make the comparison fail. Run 3 adds mutations
between reference and production implementations. This is what licenses "independent".

**Exported facts for Runs 3–5:** the finalized-plan formula is total; the reference
complete-order evaluator is the oracle for optimality over the admissible space; the
amendment taxonomy is exhaustive and the hazard test is conservative; the non-retroactive
variant's plan-quality cost is a measured number.

---

## Run 3 — one annotated emitter, chooser unchanged, baseline corpus frozen

**Claim:** producing a finalized cost trace alongside a plan preserves every current plan,
diagnostic, fingerprint, and answer; the trace describes the plan it accompanies; and
`Constrain`/`Compare` are emission-inert.

Refactor emission around an internal annotated body. Operator provenance and semantic factor
identities live only while planning; stripping annotations yields the existing `Plan`. Fetch
reuse and amendments mutate this one body, so plan and cost trace cannot disagree because two
builders made different decisions.

Collection produces an immutable `EmissionSeed`. Every forced candidate starts with a fresh
emitter holding its own bindings, fetched map, folded factors, annotated body, and scratch
diagnostics. Candidate evaluation cannot mutate the shared interner, leak a register number or
diagnostic into the next candidate, or choose the winner by discovery side effect. Only
already-collected public diagnostics and the selected plan escape.

The interner half is already true and is recorded as a checked premise rather than a hope: the
only mutating interner call in flatten is `fresh` (`flatten.rs:1880-1882`), reached once from
hoisting during collection (`:1371`); every other use is `try_resolve`. Collection runs once,
before any order is chosen (`:794-805`). A guard asserts the interner's length is unchanged
across a candidate evaluation.

The production chooser remains the current lowest-runnable frontier for all of Run 3.

### Emission-inertness, proved rather than assumed

The word does two jobs, separated here:

- **Emission-inert** — moving the statement within a legal order cannot change the emitted
  plan. This licenses partitioning them out of the search space, and is proved in this run.
- **Legality-relevant** — the statement still carries `reads` (`flatten.rs:1065`,
  `:1071-1074`), so it still constrains where it may legally sit, and must still appear in the
  returned permutation so `safe()` sees it and can report its span.

The mechanism is register allocation, and it is mechanical:

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

`registers` advances only in `push_level` and `push_derive` — off **steps emitted**, not off
position in the order. `Stmt::Constrain(_) => {}` (`flatten.rs:2777`) and
`Stmt::Compare(_) => {}` (`:2781`) emit nothing, and the work they represent is done after the
body loop by `apply_compares` and `apply_constraints` (`:2799-2800`). So a statement emitting
no step cannot shift any address, and moving it cannot change a plan or a fingerprint. Both
are also `Placement::Floating` (`:194-201`), so they were never constrained by written order
either.

### Proof

- Retain the old emitter as test-only `legacy_emit` from this run onward. Differentially
  compare old and annotated emitters over the canonical schema-first generator extended to
  include `Alias`, `Derive`, binary `Compare`, access-chain fetch, reused fetch, union select,
  and multiple amendments, across **every admissible forced order**.
- Compare the complete `Plan`, fingerprint, rendered diagnostics including spans for
  unsafe/unsupported generated cases, and executed rows. A fixed corpus is not an alternative
  to this differential.
- Independently classify the stripped concrete plan and compare each finalized cost operator's
  access kind, physical position, sources, residual order, and amendment target. This
  classifier may inspect `Plan` but not emitter annotations.
- Compare every generated order's production cost breakdown with Run 2's reference evaluator.
- `every_legal_placement_of_a_folded_statement_compiles_identically`: for every generated query
  and every admissible position of each `Constrain`/`Compare`, the complete plan, fingerprint,
  diagnostics and rows are byte-identical. A separate invalid-query property pins the original
  diagnostic code and span after reinsertion.
- Run the existing compiler/executor model, permutation, resume, corpus, allocation, decode,
  and store-spy batteries unchanged.

### Baseline cost-plan corpus

Create every nominal/core entry now, before changing the chooser. Run 8 adds exact-statistics
entries before enabling statistics-based choice. Freeze per entry: the explicit baseline
forced order; its complete PlanView snapshot and fingerprint; its abstract cost breakdown; its
exact ordered rows and semantic multiset; exact profiles/store calls for the named performance
witnesses; the compile-time bound for compile-cost witnesses; and the hazard classification
with the non-retroactive variant recorded where it differs.

For the admissibility class, the baseline is the plan `reorder` produces today via its
dead-end path (`reorder.rs:281-285`) — what those queries currently compile to, and freezing it
is what makes Run 4's movement visible.

The selected fields temporarily equal the baseline. The Run 3 gate rejects any plan or
fingerprint churn.

Keep the old emitter permanently as `legacy_emit` under test configuration, frozen to the
pre-refactor surface and excluded from production/rustdoc builds. Its generated forced-order
differential remains a green proof rather than a test deleted after it once passed.

**Exported facts for Runs 4–5:** any admissible forced order lowers to the same plan as
before; its finalized cost trace faithfully describes that plan; `Constrain`/`Compare` are
emission-inert; candidate evaluation is side-effect-free including the interner; the baseline
half of every acceptance case is immutable test data.

---

## Run 4 — bounded enumeration and greedy fallback (the oracle)

**Claim:** exhaustive selection returns the globally cheapest admissible finalized plan under
the declared model; every fallback is deterministic and legal; no selection changes an answer;
and compile cost stays inside a stated bound.

This run ships a working optimiser and, more importantly, produces the trusted answer Run 5
must be certified against. Its exhaustive mode may remain permanently capped at a small `n` in
that role once Run 5 lands.

### Admission

Compute the baseline order with the **frozen** `reorder` (`reorder.rs:263-292`) and run the
unchanged safety check and annotated emitter once with public diagnostics. If unsafe,
diagnosed, or no plan, return exactly that result and do not search. The optimiser therefore
cannot make an unsupported construct appear supported by choosing around its diagnostic, or
change which variable/span an unorderable query reports.

Admission also establishes non-emptiness: if `safe()` accepted the baseline order then by Run
1's equivalence that order is admissible, so the candidate set contains at least it. An
assertion states this; an empty candidate set is a bug, not a fallback reason.

### Search

Partition out the emission-inert statements per Run 3's exported fact. Enumerate complete
orders of the remainder under Run 1's **admissible** predicate, seeded from `Deps::pre_bound`.
Lower and cost each candidate from a fresh Run 3 `EmissionSeed`. Do not merge equal subsets,
equal bound sets, or equal-looking planning states — that is Run 5's job, and only after Run 5
has earned it.

| Budget | Counts | Initial value |
|---|---|---|
| `MAX_ORDERED_STATEMENTS` | order-relevant statements, before enumeration | 8 |
| `MAX_SEARCH_TRANSITIONS` | admissible prefix extensions visited | 8192 |
| `MAX_COMPLETE_CANDIDATES` | complete plans lowered and costed | 256 |

Values are proposals pinned by the compile-cost witnesses, not constants asserted in advance.
Each candidate is a full `emit` (`flatten.rs:2602`), so compile cost is `candidates x emit`
and `MAX_COMPLETE_CANDIDATES` is the budget that actually bounds compile time.

Crossing any budget discards every candidate and best-so-far value and starts greedy from the
empty order. No partial-search/greedy hybrid. Exhaustive ties break on
`(ExaminedCost, complete_order)` lexicographically.

### Two failure modes, separated

- **A candidate that is not admissible** is never generated, because the enumerator's predicate
  *is* admissibility and by Run 1 admissibility is `safe()`. Stated as a debug assertion over
  every generated candidate, not a runtime branch.
- **A candidate that is admissible but whose emitter faults** returns the already-built baseline
  plan whole, as `BaselineCandidateFault`. A fail-safe compiler path, not a user diagnostic,
  reachable **only** by fault injection after the separation above.

### Greedy fallback

Repeatedly select the runnable statement with minimum `(immediate_access_rank, source_index)`,
rebuilding the local emitted effect against the state at that point. No optimality claim.

Its frontier uses the **admissible** predicate, the same space the exhaustive search uses. Two
reasons: greedy and exhaustive must search the same space or a budget overflow silently changes
which plans are considered; and the seeded frontier cannot dead-end after admission, because
admission proved a complete admissible order exists and the seeded predicate is monotone in
exactly the way `reorder.rs:17-30` argues. So greedy always returns a complete admissible
permutation and its dead-end branch is dead code in this path.

The dead-end branch stays in the frozen `reorder`, where it is still the right answer for the
genuinely unorderable query and still lets flatten's safety pass name the original variable and
span (`reorder.rs:281-285`).

Emission-inert statements are reinserted at their earliest admissible position, stable by
source index. Their semantic factors were collected from the whole body and attach where the
finalized plan applies them, not where their syntax is reinserted. An assertion over the
returned permutation proves none was omitted.

### Written order: what survives, and what does not

`preserves_written_order` (`reorder.rs:304-331`) says a written statement is never taken ahead
of an earlier written one that could have run instead. A cost chooser breaks it by construction
— moving the selective generator first is the entire point of issue #18 — so it cannot remain a
global invariant. Revision 6's proposed restatement ("of equal-or-better cost") is a
*greedy-step* property and is wrong for a chooser that picks by global total.

1. **`reorder` is not modified, so its property and its proptest stay green untouched.**
   `reorder_keeps_the_written_statements_in_written_order` (`reorder.rs:628-653`) asserts
   `preserves_written_order(&graph, &reorder(&graph))`; `reorder` is frozen as the admission
   baseline, so that test keeps testing what it always tested.
2. **Complete-search modes retire it** and state the residual that is provable: ties break on
   `(ExaminedCost, complete_order)` lexicographically, and `complete_order` is the permutation
   of source indices, so **the written order wins every tie and is displaced only by a strictly
   cheaper complete plan.** Guard: `a_cost_tie_selects_the_written_order`. This binds Run 5's
   DP identically, since it uses the same tie-break.
3. **The greedy fallback restates it in terms of its actual rank:** never ahead of an earlier
   written statement that was runnable and of equal-or-better `immediate_access_rank`. Guard:
   `greedy_keeps_written_order_among_equal_ranks`.
4. `Placement::Floating` covers `Alias`, `Constrain`, `Compare` and `Derive`
   (`flatten.rs:194-201`), so the property only ever constrained `Scan` and `Negate`. The doc at
   `reorder.rs:62-88`, which currently names `preserves_written_order` as the tag's purpose, is
   rewritten to name (2) and (3) as its consumers.

### Selector proofs

- For generated dependency graphs, every mode returns a complete permutation.
- The result respects the graph exactly when the independent **admissible** antichain oracle
  says an order exists, for exhaustive search and every fallback reason.
- Exhaustive results match Run 2's independently enumerated winner including the full cost
  breakdown — and Run 2's oracle enumerates the admissible space, so the comparison is between
  two enumerations of the same set rather than two enumerations of the same mistake.
- Non-emptiness asserted for every admitted query, with a named case per admissibility entry.
- Exact boundary and one-past tests for all three budgets. One-past proves the partial winner is
  discarded, by choosing a case where it differs from fresh greedy.
- Injected candidate-emission failure returns the admitted baseline plan whole, no scratch
  diagnostic leakage.
- Perturbing candidate-discovery order leaves the exhaustive winner and every fallback result
  unchanged.

### Compile cost

Named corpus entries at each budget boundary record a compile-time bound — candidate lowerings
and store-free wall time — for baseline and optimised compilation. Issue-level acceptance adds
a ratio bound: optimised compilation of every corpus entry stays within a stated multiple of
baseline compilation of the same entry, with the multiple and the budgets that produce it in
the audit table.

### End-to-end corpus acceptance

For every entry the gate: compiles the explicit baseline order through `flatten_in_order`;
compiles normally through the production optimiser; compares both full plans and fingerprints
to their snapshots; independently recomputes both abstract costs; asserts complete-search
selections are the global oracle winner over the admissible space, or proves the exact fallback
reason and fresh-greedy result; executes both plans and compares each with its separately
stated ordered rows and both sorted results with the stated semantic multiset; compares exact
`Profile::total` and store-operation counts for performance witnesses and proves the improvement
grows with fixture scale; and asserts the recorded compile bound.

The corpus includes an honest hostile-distribution entry where nominal costing chooses a slower
legal plan; its answers must still agree and the book uses it to state the proof boundary. It
is not marked as an optimiser failure.

### Fingerprint and cross-target proof

Plan order and every changed residual/source position already participate in the plan
fingerprint: `PlanFingerprint`'s plan walk (`plan.rs:1286-1330`, entered from
`Plan::fingerprint`, `:833`) hashes `plan.body` in order and each source's residuals in order.
A moved selected plan moves the fingerprint and refuses an old cursor as `CursorPlan`; unchanged
plans retain their exact value. No cursor layout changes, so no `CURSOR_VERSION` bump.

The admissibility class is where plans move for queries never reordered before. Those entries
carry both fingerprints and an explicit note that the movement is the Run 1 correction landing.

Native and real wasm compilers consume the same serialized corpus inputs and emit the selected
order, selection mode, full PlanView JSON, cost breakdown, and fingerprint; `web` smoke compares
byte for byte in the same gate, over every arithmetic boundary and selection/fallback entry.

Rewrite `query-efficiency.md:212-244`; retain the key-order warning and state the exhaustive and
runtime proof boundaries. Update PLAN.md's cost-model decision.

**Exported facts for Runs 5–8:** default planning is deterministic; complete search is globally
optimal over the admissible space under the model; every fallback is deterministic; all modes
preserve answers and fingerprint/resume safety; **and the exhaustive winner for any query within
budget is the certified answer Run 5 is measured against.**

---

## Run 5 — the subset dynamic program (the destination)

**Claim:** for a query the amendment taxonomy classifies hazard-free, the subset recurrence
is exact — its incremental charge equals the difference between fully-lowered costs — and the
resulting plan is identical to Run 4's certified exhaustive winner wherever Run 4 can reach.

### Shape decided by Run 2, not here

Run 2's amendment pricing determines which of two forms this run takes, and the decision is
made on recorded numbers before any of it is built:

- **Unconditional.** If landing chased residuals and discriminant filters at their own
  position costs little plan quality across the corpus, that lowering becomes the shipped
  behaviour, both retroactive sites disappear, every query is hazard-free, and this run is a
  DP with no routing at all. Much the simpler outcome.
- **Hybrid.** Otherwise the retroactive lowering is retained, the hazard test routes, and
  hazardous queries fall to Run 4's enumeration.

The rest of this section is written for the hybrid, which is the harder case; the
unconditional form is the same minus the routing and its guards.

### The recurrence and the state

```text
cost(S + m) = cost(S) + card(S) * access(m | state(S))
```

with the DP table keyed on the **subset** of placed order-relevant statements, and ties broken
on `(ExaminedCost, complete_order)` lexicographically — the same rule as Run 4, so written
order still wins every tie (Run 4's replacement property 2 binds this run unchanged).

Revision 3's review recorded that subset alone is insufficient because `fetch_level` reuses
registers (`flatten.rs:1966-1991`). That finding is about *plan identity*, and this run must
show it does not extend to *cost*. The obligation, discharged mechanically rather than argued:

> **F5-a.** For a hazard-free query, the set of distinct fetches existing after a prefix is a
> function of the prefix **subset** alone, not of the order within it.

The intuition is that `fetched` is keyed by `(address, path)` where `address` is the register
holding the referring row; registers are order-dependent numbers, but the referring row is
bound by exactly one level in any order, so the same logical reference is reused in every
order of the same subset. That is an argument; the gate is a property asserting the set of
`(referring variable, path)` pairs after `lower(S)` is identical across every admissible
permutation of `S`, for every generated query and every subset.

F5-a is the hard part of a broader precondition that the recurrence needs and that revision 8
never stated, because revision 8 had no DP to need it:

> **F5-c (future-equivalence).** For a hazard-free query, `state(S)` — the bound variable set,
> the fetch set, and the accumulated cardinality — is a function of the subset alone. Hence two
> prefixes over the same subset have identical futures, and keeping the cheaper is sound.

The bound set is a set union and the cardinality is a product of per-statement bases and
applicable factors over the set (`card(S) = Π base(m) × Π sel(p)`, both products over the
subset), so order within the subset cannot matter for either; F5-a supplies the third. The
gate asserts all three components directly across every admissible permutation of every
subset, rather than deriving them.

The recurrence also needs **monotonicity** — `cost(S + m) >= cost(S)` — so that a cheaper
prefix cannot lose to a dearer one later. This holds because every charge is a saturating
addition of a non-negative `input_rows * examined_per_invocation` term, and in hazard-free
mode nothing subtracts from an already-charged operator. It is asserted as an arithmetic
property in Run 2's boundary suite and re-asserted here over generated transitions.

### Bellman locality — how this is validated beyond the oracle's reach

The obvious validation is "compare the DP's answer with Run 4's exhaustive answer". That
cannot be the strategy, because Run 4's exhaustive mode reaches about six statements and this
run exists to serve twelve to fifteen. An oracle that cannot reach the domain cannot certify
it.

The strategy is instead to validate the **recurrence**, which is a *local* property and
therefore checkable at any `n` without enumerating anything:

> **F5-b (Bellman locality).** For every admissible prefix `S` and every extension `m`:
> ```
> incremental_charge(S, m)  ==  cost(lower(S + m)) - cost(lower(S))
> ```
> where both right-hand terms come from Run 2's finalized-plan evaluator over a full lowering.

This needs two full lowerings per transition — expensive, but only in tests, and it scales
with the number of *transitions* rather than the number of *orders*. It is the difference
between checking `2^n * n` local facts and checking `n!` global ones, and it is what makes a
twelve-statement DP certifiable at all.

Optimality then follows from locality plus the standard argument, and the standard argument is
itself pinned: if F5-b holds for every transition and the table keeps the minimum per subset,
the table's final entry is the minimum over all admissible complete orders. That step is
proved once, over an abstract cost algebra, independent of fjord.

### The validation ladder

| Level | What it checks | Range |
|---|---|---|
| Exhaustive agreement | DP answer == Run 4's certified exhaustive winner, including full cost breakdown | n ≤ 6 — the base case |
| Bellman locality (F5-b) | every transition's incremental charge equals the lowered difference | every `n` the DP admits |
| Fetch-set determinacy (F5-a) | the fetch set after a prefix depends only on the subset | every `n` |
| Adversarial sampling | for large `n`, sample admissible complete orders uniformly, lower and cost each, assert none beats the DP's answer | n up to the statement limit |
| Metamorphic invariance | DP result unchanged under permutation of statement *discovery* order and under relabelling | every `n` |
| Hazard conservatism | every query routed to the DP satisfies Run 2's empty-difference property for all `(S, m)` | every `n` |

Adversarial sampling is a necessary condition rather than a proof, and is labelled as such. It
is there because it is the one check that would catch a recurrence that is locally consistent
and globally wrong, which is exactly the failure a purely local argument cannot see.

### Budgets and the fallback ladder

| Budget | Counts | Initial value |
|---|---|---|
| `MAX_DP_STATEMENTS` | order-relevant statements admitted to the DP | 14 |
| `MAX_DP_STATES` | subsets materialised | 32768 |
| `MAX_DP_TRANSITIONS` | incremental extensions evaluated | 262144 |

Values are proposals pinned by compile-cost witnesses, exactly as Run 4's are, and they enter
the same audit table.

Selection proceeds down a ladder, and each rung is a distinct selection mode in the corpus:

1. `DynamicProgram` — hazard-free and inside the DP budgets.
2. `Exhaustive` — hazardous, or over a DP budget, and inside Run 4's candidate budgets.
3. `Greedy*` — over Run 4's budgets too.

Crossing a DP budget discards the whole table and re-enters at rung 2 from the empty order.
There is no partial-DP/partial-enumeration hybrid, for the reason revision 6 gave and revision
9 keeps: a partially explored table is a result nobody can state a property about.

### Proof

- The full validation ladder above, each rung a named gate.
- **The routing property.** For every generated query, the mode selected matches the hazard
  classification and budget arithmetic exactly; and for every query where both rungs 1 and 2
  are reachable, they return the identical plan, fingerprint and cost breakdown. This is the
  strongest single guard in the run, and it is why Run 4's enumeration is retained permanently
  rather than deleted once the DP works.
- **Answer preservation.** Every corpus entry executes both the DP-selected and baseline plans
  and compares ordered rows and the semantic multiset, exactly as Run 4 does.
- **The exhaustiveness guard on `level_mut`** from Run 2 is re-run here, because a seventh call
  site appearing later would silently invalidate the hazard test that routes to this run.
- Fault injection: a DP state-table failure returns the Run 4 result whole; a Run 4 failure
  returns the admitted baseline whole. The ladder degrades, never guesses.
- Native/wasm byte-identical outputs extended to cover the DP mode and its budget boundaries.
- Compile-cost witnesses comparing rungs 1 and 2 on the same queries, which is where the
  100,000x claim above becomes a recorded number rather than an estimate in a plan document.

Update the book: `query-efficiency.md` states the DP, its statement range, the hazard that
routes around it, and the fact that exact optimality is claimed only within the declared model
and the completed search.

**Exported facts for Runs 6–8:** planning is deterministic in every mode; the selected plan is
a pure function of query, schema, statistics mode and budgets; the mode itself never changes an
answer.

---

## Run 6 — exact per-predicate counts in the one seal walk

**Claim:** a sealed database records one exact count for every non-virtual declared predicate,
and old sidecars remain readable.

`approximate_len()` was the wrong source twice: fjall promises only *"For insert-only workloads
… this value is reliable"* (`fjall-3.1.8/…/keyspace/mod.rs:476-481`), and the exact count is
already free — `identity::compute` (`identity.rs:100`) already walks every fact, counting on the
way (`:86-92`).

Initialize a count slot **from the embedded schema** for every non-virtual predicate, including
zero-count ones. This is the correction the existing loop needs: `identity::compute` iterates
`for predicate in db.predicate_ids()` (`identity.rs:111`), which yields only predicates that have
trees, so a declared predicate with no facts would otherwise be absent rather than zero — and
"no facts" must be distinguishable from "no statistics". Increment the slot in that existing
walk. Return the ordered counts with `Identity` and carry them through `Finished`. `record`
writes identity, total count, per-predicate counts, bytes, and the status flip in its existing
single atomic sidecar write.

```rust
struct PredicateCount { id: PredicateId, name: String, facts: u64 }
```

`Meta` gains `predicates: Option<Box<[PredicateCount]>>` with `default` and
`skip_serializing_if`; no version bump — these are the additions the versioned format can take,
per `meta.rs:17-26`, whose "the field list is fixed" paragraph updates with it. Predicate-id /
declaration order. Virtual predicates never appear. At construction and load-for-planning,
validate exactly one entry per non-virtual declaration, the id/name pairing, order, and count
bound. Malformed present statistics are not silently treated as zero or as legacy absence.

### Proof

- A schema-first generated database is sealed and compared with an independent full scan grouped
  by predicate.
- Structural assertions: every non-virtual declaration exactly once, no virtual entry, ascending
  ids, correct names, per-predicate sum equals `Identity::facts`, zeros present.
- Cases cover deduplication, staged ingest, concurrent ingest, multiple predicates, zero facts,
  reopen, and compaction.
- A meta-write probe proves finish still performs one sidecar replacement carrying all fields and
  the status flip.
- New metadata round-trips. A hand-written version-1 sidecar without `predicates` loads as `None`.
  Duplicate, missing, reordered, wrong-name, wrong-id, and overflowed present statistics provoke
  their contract-layer error.
- Two content-equivalent builds with shuffled ingest and different physical ids agree on total
  count, ordered predicate counts, and content fingerprint.

Correct PLAN.md's claim that `approximate_len()` is exact. It remains at most a diagnostic
cross-check and is never refusal evidence.

**Exported facts for Runs 7–8:** `Some(counts)` is a complete validated generation-1 statistics
set and a pure function of sealed content; `None` means legacy/no statistics, not zero.

---

## Run 7 — `fjord.db.Stat` and per-virtual-table world digests

**Claim:** holding cursor version, plan, and base world equal, and under F0-b's collision model,
a cross-request resume over stable virtual predicates is accepted exactly when every virtual
table the plan reads has identical row bytes.

Add `fjord.db.Stat {name, instance, predicate, facts}`. Several instances may share a name, hence
both identity fields. Complete entries with validated counts produce one row per non-virtual
predicate, including zeros. Writable and legacy entries produce no Stat rows; absence is not a
fabricated sentinel, and a `-1` cannot be built anyway, since a sidecar with no counts also has
no predicate names.

Generalise the `reads_listing` flag (`session.rs:1469-1470`, set at `:1545`, read at `:1021` and
`:1201`) and `with_listing_digest` (`:1640`) to a sorted, length-framed sequence of
`(predicate_id, table_digest)` for every stable virtual predicate the plan reads, including an
empty table. `Table` already owns a per-table `digest` (`catalogue.rs:202-208`), so this is a
generalisation rather than a third boolean. `digests()` (`catalogue.rs:281`) filters empty tables
because it answers "what could have minted an id", a different question; reusing it here would
conflate zero rows with absent. Keep `fjord.db.Interning` excluded with its existing cross-request
refusal. Fetch frames continue to carry digests only for non-empty tables that could have minted
ids.

The protocol already carries a framed per-predicate list — `listing_digests:
Vec<(PredicateId, u64)>` on the client's `Rows` (`rows.rs:64`, `:127`), decoded per
`LISTING_DIGEST` frame (`connection.rs:772`) — so this is server-side only: no wire shape change
and no client change.

Move complete resume preflight ahead of `ROW_DESCRIPTION` (`session.rs:1145`) in this run. A
resumed request materialises its catalogue, obtains the first execution reader and its fully
composed base-plus-virtual world, checks cursor version, plan, then world in the same precedence
as `Executor::resume` (`iter.rs:2025`), and retains that exact reader/world pair for the first
chunk. This is required evidence for Run 7's own refusal claim — a refusal you cannot observe
before the row descriptor is not a proved refusal — not work deferred until statistics planning.

### Proof

- Run 0's permutation property is instantiated for Stat.
- A generated `Listing -> expected Stat rows` model proves exact names, instances, predicate
  names, counts, zeros, multiplicity, ordering, and the Writable/legacy exclusions.
- A pair-generated catalogue property chooses an arbitrary set of stable virtual predicates and
  proves: selected world bytes agree when all selected tables' row bytes agree; changing any
  selected table changes its digest/world in the tested domain; changing only unselected or
  volatile tables leaves the world unchanged; materialisation and input table order cannot change
  the framed world; empty and absent tables encode differently where the schema distinguishes
  them.
- A framing mutation battery covers count, predicate id, digest, ordering, truncation, and
  boundary shifts.
- Server-level paging uses the actual protocol and asserts success/refusal before any row
  descriptor for: zero-row create/remove, finish, removal of a Complete instance, independent
  List+Stat changes, same-row-count/different-content changes, and unchanged negative controls.
- Fetch tests prove a stale Stat virtual id is refused and unchanged ids resolve to the same
  bytes — the consumer of **F0-b**, and the test names it.

Add `fjord db stat` and `:stat` with automated CLI/shell output tests and update the book
(`operations.md`, `cli.md`, `shell.md`, `status.md`) and the PLAN.md gap row.

---

## Run 8 — exact base counts feed planning

**Claim:** content-equivalent Complete databases carrying the same validated statistics generation
compile identically; legacy and live Writable databases plan nominally; statistics-mode changes
are resume-safe.

### Settled legacy policy

Do **not** backfill and do **not** put a statistics digest in the world stamp.

- `Some(valid generation-1 counts)` on a Complete database selects exact-base mode.
- `None` selects nominal mode, including legacy Complete databases.
- Writable databases select nominal mode until sealing publishes their `Completion`.

The plan fingerprint already hashes the finalized plan structure (`plan.rs:1286-1330`). If two
modes choose different plans, cross-mode resume is `CursorPlan`. If they choose the same plan,
resume is semantically safe and succeeds; refusing merely because unused estimates differed would
conflate planning input with database world. The world stamp continues to identify rows, not
optimiser policy — and by Run 5's exported fact, not search mode either.

### Ownership and race-free publication

```rust
struct Completion {
    fingerprint: u64,
    predicate_counts: Option<Arc<[PredicateCount]>>,
}
```

`Database` initializes it from Complete metadata at registry open. A live finish carries Run 6's
counts through `Finished`, publishes `Completion`, then clears `writable` while holding the
existing seal barrier. `prepare` snapshots one `PlanningContext` from that publication and passes
only a narrow predicate-count view into `fjord-engine`; the engine depends on no backend or
sidecar type. `Compilation::new` remains the nominal default for embedded and wasm callers.

`planning_context` reads `writable` first: `true` means nominal even if finish is in its
publication window; `false` requires the already-published `Completion`, otherwise it fails toward
refusal as the existing unknown-world arm does. So a caller observes the old complete context or
the new one, never a partial count vector. A plan compiled nominally just before the transition
remains legal; a later page either reproduces it or refuses through the normal plan/world checks.

Run 7's retained-reader preflight is reused unchanged. Statistics remain outside the world: prepare
compiles from its one `PlanningContext`, then preflight obtains and retains the first execution
snapshot. When both plan and world mismatch, preflight reports `CursorPlan`, matching the engine's
existing order (`iter.rs:2025`), and every refusal occurs before `ROW_DESCRIPTION`.

### Proof

- Extend the corpus with exact-count cases: zero, equal bases, extreme skew, a count that changes
  the nominal winner, a legacy/current pair with the same winner, and one with different winners.
- For two equivalent builds with shuffled ingest and different physical ids, assert identical
  validated counts, selected order, full PlanView, cost, fingerprint, world, canonical logical rows
  and profile. Each build independently satisfies resume equals uninterrupted. A cursor crossing
  between them is refused as `BadResumeKey` when the saved key's physical id differs; content
  equivalence is not permission to replay a physical cursor into a different id assignment.
- An exact-count scaling witness must select a cheaper plan than nominal and reduce exact
  examined/store-operation counts while returning identical rows.
- Legacy/current with different selected plans refuses as `CursorPlan` before the row descriptor.
  On the same store/id mapping, legacy/current with the same selected plan resumes successfully,
  proving unused estimate-mode differences are not a hidden refusal.
- **Statistics must not change the search mode's correctness**: for every exact-count entry, assert
  the DP and enumeration rungs still agree wherever both are reachable. Estimates change values,
  not the recurrence, and this is where that is checked rather than assumed.
- A live server query before, during, and after finish proves nominal publication, exact
  publication, no partially visible count set, and safe `CursorPlan`/`CursorWorld` behaviour.
- Generated zero counts never become absent statistics; malformed present counts are refused at
  metadata/planning-context construction, not treated nominally.
- Native and wasm cost-corpus comparison extended so the wasm harness accepts the same serialized
  count view; equal inputs remain byte-identical. Browser callers supplying no counts intentionally
  exercise nominal mode.

Update PLAN.md's "spend it on pruning, not join ordering" decision, the operations and
query-efficiency chapters, and the resume check-order documentation.

### Rung 3 remains recorded, not scheduled

Prefix/range histograms, cross-key fan-out, and residual selectivity require a new statistics
generation and this same corpus discipline. Statistics may use only compile-time constant key
material: a `SeekKeyPart::RegisterField`/`RegisterFactId` has no compile-time value, so a
bound-variable join uses a join statistic or the nominal fallback, never a prefix lookup. Reference
histograms may not key on physical `FactId` — claimed at intern time and ingest-order dependent,
which is why the content fingerprint hashes logical form; they must aggregate canonical logical
targets or an order-independent distribution, and prove equivalence across builds assigning
different ids. The rebuild-stability rule stands: a cost model may read only quantities that are a
pure function of database content. Fjall exposes `approximate_len()` and an O(n) `len()` but **no
rank or range-count API**, so the issue's "`rank(hi) - rank(lo)`, two seek positions, no scan" is
unavailable and exact prefix counts require seal precomputation.

No `#[ignore]`d guards for this unscheduled work: `scripts/check-guards.py` owns a single global
movement namespace (`MOVEMENTS = range(0, 9)`, the recursion movements) and nothing here is
invariant-critical. PLAN.md is the home.

---

## What this revision does not prove

Written for the adversarial pass, so that a reviewer spends their time on things this document
has not already conceded. Each item is a live obligation with a named home, not a hedge.

1. **F5-a and F5-c are argued in prose and gated by property, not proved on paper.** The
   arguments given (one binding level per variable; cardinality as a product over a set) are
   plausible and the gates are direct observations, but a counterexample would invalidate Run 5
   entirely rather than degrade it. This is the single highest-risk claim in the plan. It is
   also cheap to falsify early — the property can be written against Run 3's emitter before any
   DP exists, and *should be*, as a spike, before Run 5 is scheduled.
2. **The amendment taxonomy's exhaustiveness rests on a call-site guard over
   `Body::level_mut`.** That guard catches a seventh direct call site. It does not catch a new
   helper that takes `&mut Body` and mutates a level through some future accessor. The guard is
   necessary, not sufficient, and the classification property in Run 2 is what actually carries
   the weight.
3. **Adversarial sampling is a necessary condition, not a proof.** For `n` beyond the oracle's
   reach, nothing here establishes global optimality by exhaustion. The argument is
   locality-plus-induction, and sampling is the net under it.
4. **Every budget value in this document is a proposal.** `MAX_COMPLETE_CANDIDATES = 256`,
   `MAX_DP_STATES = 32768` and the rest are state-count guesses. The compile-cost witnesses pin
   them, and the DP's usable statement range is a *consequence* of that pinning, not an input to
   it. If the per-transition constant is poor, Run 5's range could land closer to 10 than 15,
   which would still be a large improvement on Run 4's ~5 but is a different headline.
5. **The corpus grows combinatorially.** Selection mode × statistics mode × hazard class × the
   existing taxonomy is a lot of entries, and no budget is stated for how many the census
   demands or how long the gate takes to run. A reviewer should push on whether the census is
   affordable, because an unaffordable gate gets weakened later under delivery pressure, which
   is how acceptance criteria rot.
6. **Run 5's position before the statistics runs is a judgement call.** The argument is proof
   adjacency — the DP's oracle and corpus are freshly built, and validating the recurrence on
   nominal costs separates "is it sound" from "are the estimates good". The counter-argument is
   that Runs 6–8 deliver standalone value and would sit behind a research-shaped run. Either
   ordering is defensible; the document has picked one and says so rather than pretending it is
   forced.
7. **The non-retroactive lowering, if adopted, changes plans.** Run 2 prices it but does not
   gate it. If Run 5 adopts the unconditional form, that plan movement needs the same corpus
   treatment the admissibility class gets in Run 4 — frozen before/after snapshots and an
   explicit note — and this document does not yet write that gate, because whether it is needed
   is Run 2's finding.
8. **A documentation inconsistency is unresolved.** `chase`'s doc comment offers
   `D = src.Decl {kind = "class"}` as the shared-register example, but condition 1
   (`gives_no_constant`, `flatten.rs:1666-1676`) rejects any key giving a constant, so that
   pattern is not chasable — the amendment actually arises from already-bound *variables* in the
   key becoming residuals. Either the comment is stale or the reading is wrong, and it matters
   because it describes how often the hazard fires. The "reused fetch amended once and more than
   once" corpus entries settle it.

## What the optimiser cannot fix

- A row bind that claims its variable (`flatten::Claims`, `flatten.rs:351`, decided at
  `:1195-1240`) prevents another occurrence from being a capture; ordering cannot invent a
  missing access path. `D = src.Decl _; src.SearchByName {to = D, name = N}` is the 30s-to-2ms
  case at `fjord-viewer/src/query.rs:236-238`, and no cost model reaches it. Note the
  interaction with chasing: it is precisely the chasable flag that *releases* the claim and makes
  the good order legal, which is why removing chasing to simplify the DP would move cases into
  this section rather than out of it.
- A join whose inner side cannot seek on the schema's declared leading fields is a key-order
  problem. FINDINGS §2's own case is already fixed at the schema (`src.Decl` is now
  `{module, name, line}`), so — contrary to the issue's acceptance sketch — §2 is not available
  as the witness. Run 4's witness constructs a case where the frontier genuinely has a choice.
  The corpus includes the key-order case as an **unchanged-plan negative control**, not a repair.

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
| 2 | cost arithmetic and monotonicity, complete-order reference evaluator over the admissible space, amendment taxonomy exhaustiveness and classification, hazard conservatism, non-retroactive pricing, mutation controls, census |
| 3 | old/new emitter differential over every admissible order, trace/plan classifier, emission-inertness, interner-stability guard, baseline snapshots |
| 4 | exhaustive/fallback selector properties, written-order replacements, budget boundaries, compile-cost witnesses, complete pre/post corpus, native/wasm comparison, examined scaling witnesses |
| 5 | F5-a/F5-b/F5-c, the validation ladder, routing agreement between rungs, ladder degradation under fault injection, DP compile-cost witnesses |
| 6 | identity/count model, structural count contract, sidecar compatibility, one-write seal |
| 7 | Stat row model, virtual-world pair property, server paging/fetch, CLI and shell |
| 8 | exact-count corpus, equivalent builds, mode agreement under statistics, live finish publication, cursor precedence, native/wasm counts |

### By hand, once the runs land

- after Run 4: `:plan` a query written with its selective generator second and see it compile
  with that generator first; `query --profile` before and after shows the examined count moving
  with identical rows; `:plan` one of the admissibility-class queries and see a plan `reorder`
  could not have produced.
- after Run 5: `:plan` a ten-generator query and watch it compile at all, then add a union select
  and watch the mode drop to `Exhaustive` or `Greedy`; compare compile times.
- after Run 7: `fjord db stat <name>` and `:stat` report per-predicate counts; page
  `fjord.db.Stat`, `finish` a database mid-page and watch the resume be refused, then repeat with
  a `create` and watch it resume.

## Issue-level acceptance

Issue #18 closes only when all of the following are simultaneously true:

- Every planned corpus entry has reviewed baseline and selected full-plan snapshots,
  fingerprints, independent costs, ordered rows, semantic multiset, hazard classification, and a
  selection mode; every diagnosed entry has its exact code and span.
- The census and mutation controls are green, including the admissibility, amendment, DP and
  compile-cost classes.
- **The admissible predicate is proved equal to `safe()` on complete permutations**, and every
  optimality claim is scoped to that space explicitly. No optimality assertion passes over an
  empty candidate set: the non-emptiness assertion is part of the gate.
- **The amendment taxonomy is proved exhaustive**, the hazard test proved conservative, and the
  `level_mut` call-site guard is green.
- **Where both are reachable, the DP and the enumeration return the identical plan, fingerprint
  and cost breakdown**; Bellman locality, fetch-set determinacy, future-equivalence and
  monotonicity are green at every `n` the DP admits; adversarial sampling finds no cheaper
  admissible order.
- Complete-search selections equal the independent global optimum over the admissible space where
  the oracle reaches; every non-complete case proves its exact fallback reason and result.
- Baseline and selected plans agree on the semantic result multiset for every entry and for the
  generated model across every admissible forced order; each also matches its own recorded
  execution order.
- `preserves_written_order` and its proptest are unchanged and green over the frozen `reorder`;
  the tie-break and greedy replacements are green over their modes; `reorder.rs:62-88` names its
  actual consumers.
- Named witnesses improve exact examined/store-operation counts with scale; the hostile nominal
  witness documents the absence of a universal runtime claim.
- **Optimised compilation stays within the stated multiple of baseline compilation in every mode**,
  and the budget values that produce it are in the audit table with the measured per-transition
  constant beside them.
- Native and wasm outputs are byte-identical for the complete nominal and exact-count corpus
  inputs, in every selection mode.
- Plan changes move fingerprints and stale cursors refuse before a descriptor; unchanged plans
  retain fingerprints and resume. Every plan that moves for a query never reordered before is a
  corpus entry with the movement stated.
- Exact counts are a validated pure function of sealed content, including zero and
  content-equivalent rebuilds, and the live-finish path publishes them whole.
- The complete repository gate is green and the book and PLAN.md state the shipped model, limits,
  fallbacks, statistics policy, search modes and proof boundary exactly as the tests do.
