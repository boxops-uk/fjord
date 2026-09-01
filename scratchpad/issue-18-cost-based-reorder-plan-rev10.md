# Cost-based reorder — issue #18 (revision 10)

## What this revision is

Revision 9 designed a cost planner for a compiler that plans **one query into one plan**.
That compiler is being replaced. `PLAN.md`'s recursion work turns it into one that plans a
**program of named rules into an executable**, and `PLAN.md:3097` states the relationship
this document has to it:

> **New modules in `fjord-engine`** | `relation`, `program`, `stratify`, `magic`, plus a
> fixpoint driver. `flatten`/`reorder`/`compile` are **reused per rule, not rewritten**

So the cost planner is not invalidated. It is **multiplied** — it runs per executable rule,
inside a pipeline that generates rules the user did not write — and in one place it is
**contradicted**, by a phase-order cycle neither plan could have seen alone.

Revision 10 is revision 9 rewritten to be complementary to the recursion plan rather than
merely compatible with it. Where the two plans answer the same question, this document adopts
the recursion plan's answer and cites it rather than deriving a second one; where they
conflict, the conflict is named and resolved; and where the cost planner can *contribute* to
the recursion work rather than merely avoid it, that is stated as a sequencing recommendation
rather than left for whoever schedules them.

Everything revision 9 established is carried forward. The destination is still a subset DP
with enumeration as its oracle and fallback; the admissible-order predicate, the amendment
taxonomy, emission-inertness, the written-order replacements and the compile-cost bounds are
unchanged in substance. What changes is scope, sequencing, and four new couplings.

---

## The review: what was read, and what it found

Read in full: `PLAN.md:885-3122` — the fifteen Movement 0 items, Movements 1 through 8, the
cost table and the deferred closure operator — plus `docs/recursion-plan-adversarial-review.md`
and `scripts/check-guards.py`'s guard ledger. The recursion work is live: Movement 0 is green
through 0e, Movement 1 is "next, and unblocked", and its pure models are already in the tree
(`program.rs`, `dnf.rs`, `canonical_id.rs`, `local_identity.rs`, `materialise.rs`,
`work_bound.rs`, `budget.rs`, `borrow.rs`). Issue #18 does not appear in "What remains" at all.

| # | Coupling | Direction | Where |
|---|---|---|---|
| C1 | **The SIPS is `reorder`'s frontier order**, and adornment consumes it at phase step 6 while a cost-based order can only exist at step 9. A cycle. | **Conflict — resolved below** | `PLAN.md:2932`, `:1657` |
| C2 | **Derived predicates have no statistics and three distinct size classes** (accumulated, delta, magic). A cost model with one nominal base mis-orders every delta variant. | Gap in this plan | `PLAN.md:1302-1350` (item 4), `:1411-1443` (5b) |
| C3 | **Compile cost is per *program*, not per body** — rules × DNF product × magic + supplementary × up to two candidates. | Strengthens rev 9's DP reframe | `PLAN.md:1660-1672` (item 9), `:1706-1760` (item 10) |
| C4 | **Rows examined does not distinguish a memory-resident relation scan from a disk-backed base scan**, so the declared objective is store-blind exactly where recursion introduces two stores. | New limitation to declare | `PLAN.md:2573-2612` (Movement 1) |
| C5 | Every fingerprint, limit-classification and fallback question this plan wrestled with **is already settled** by items 6, 7 and 13 and Movement 4. | Adopt, do not re-derive | `PLAN.md:1463-1490`, `:1587-1637`, `:1921-1943`, `:2772-2795` |
| C6 | `collect` must become runnable over an arbitrary rule body independent of emission — **the same refactor as rev 9's Run 3 `EmissionSeed`**. | Shared work | `PLAN.md:1653-1657` |
| C7 | A predicate catalogue is threaded through `lower`, `ty`, `flatten`; `Deps` is both the SIPS input and rev 9's `pre_bound` carrier. | Merge surface | `PLAN.md:3096`, `:1653` |
| C8 | **Monotonicity survives.** `reorder.rs:24-30` warns it fails for nested statement groups; recursion introduces none. | Checked clear | below |

### C8 first, because it is the one that could have ended the plan

`reorder.rs:24-30`:

> Glean's `Reorder` does need a give-up branch, and the difference is *nested* statement groups
> (from negation and disjunction), whose own reads depend on how their branches are ordered —
> **which is where monotonicity fails**. sigla has none … so this argument must be re-proved
> when one lands, and not before.

Monotonicity is what Run 1's admissible predicate, Run 4's search and Run 5's DP all stand on.
Recursion does not introduce nested groups:

- negated groups stay refused, and Movement 6 *relies* on it — "demand … is not propagated
  through the negated subgoal into a nested body, **which cannot arise, because a negated
  *group* is itself still refused**" (`PLAN.md:2954`);
- subqueries are already **inlined** into the flat statement list (`flatten.rs:1419`,
  `self.inline(&body, stmts)`);
- a `with` block produces separate rule bodies, each a flat conjunction;
- DNF normalisation expands `(A|B); (C|D)` into four flat clauses — more bodies, still flat
  (`PLAN.md:1722-1729`).

Every body the optimiser will ever see is a flat conjunction. Recorded as a checked premise
with an owner: **Run 1 asserts it mechanically** over generated programs once `Program` exists,
so that the day a nested group does land, the assertion fails rather than the DP quietly
becoming unsound.

---

## C1 — the phase-order cycle, and its resolution

This is the one architectural conflict, and neither plan could have found it alone.

### The cycle

`PLAN.md:2932` settles magic's sideways-information-passing strategy by reusing what exists:

> **The SIPS is already built:** `reorder`'s runnable frontier is a sideways-information-passing
> strategy, and it is greedy-complete for the reason that module documents — reads are
> structural, `bound` only grows. **Reuse it rather than inventing a second notion of what is
> bound when.**

and item 9's phase order (`PLAN.md:1657`) puts adornment at step 6:

```text
3. run `collect` per rule for its statements and symbol dependencies
4. validate recursive safety and stratify the source program
5. normalise the bodies of the rules about to be rewritten   (DNF)
6. generate magic rules                                       <- adornment reads the order
7. re-collect, re-SCC, re-stratify the transformed candidate
8. generate semi-naive variants
9. flatten each executable rule to an ordinary `Plan`         <- cost order would live here
```

`reorder(&deps)` is a pure function of `Deps`, which step 3 produces — so adornment at step 6
can obtain the frontier order without flattening anything. That is exactly why the recursion
plan can say adornment "reconstructs nothing".

**A cost-based order is not a pure function of `Deps`.** Revision 7's central correction — cost
finalized plans, not prefixes — means the order can only be chosen by *lowering* candidates and
costing the finalized operator sequence. Lowering is step 9. Step 9 runs over the output of
step 6. Adornment would need the answer step 9 produces from adornment's own output.

### Three resolutions, and the one taken

**(A) Adornment uses the frontier order; flatten uses the cost order.** No cycle, but it makes
adornment's `b`/`f` strings a claim about an order that will not be run. That is not merely
untidy — it has a concrete failure. An adorned rule "gains a magic literal at the front"
(`PLAN.md:2936`), and the magic atom binds the `b` positions. In fjord's dependency language
both the magic atom and an ordinary body atom may *capture* the same variable, so the frontier
is free to place the magic literal **second**, after a base scan. The answer is unchanged — a
late magic atom is a filter rather than a seed — but the transformation is defeated, and
Movement 6's acceptance says so directly:

> **A store spy proves the seed is doing the work**, over a fixture where the seed comes from a
> multi-level base join and unrelated graph components dominate the database: those components
> are never scanned. Result equality on a small fixture cannot discharge this — an accidentally
> unseeded implementation computes the whole closure and still returns exactly the right rows.

A nominal cost model has no statistic for `magic_p^a` and every reason to rank a base scan
cheaper. **So an unconstrained cost optimiser would fail an existing Movement 6 acceptance
criterion, silently, with correct answers.** That is the single most important finding of this
review.

**(B) Two-pass: flatten with cost, feed the order back to adornment, regenerate.** A fixpoint
over compilation. Non-obviously terminating, doubles an already program-scaled compile cost, and
makes the selected executable a function of a search that ran over a different executable.
Rejected, and recorded so it is not re-proposed.

**(C) Taken: the frontier order remains the SIPS; the magic literal is pinned; the cost
optimiser orders what is left.**

1. **Adornment keeps reading the frontier order.** It stays a pure function of `Deps`, step 6
   stays acyclic, and `PLAN.md`'s "reuse it rather than inventing a second notion" stays true.
   The frontier order is the *logical* SIPS; the cost order is a *physical* choice downstream of
   it, which is the same division the plan already draws between `Deps` and `Plan`.
2. **In a magicked rule, the magic literal is placed first and is not subject to cost
   reordering.** This is not a concession to the optimiser — it is what the standard formulation
   already says ("each adorned rule gains a magic literal **at the front**") and what Movement
   6's store-spy guard requires. The optimiser orders the remaining body freely.
3. **The pin is structural, not conventional.** The magic atom is marked in the collected
   statements and the search's admissible-order predicate refuses any order that does not place
   it first, exactly as it refuses an order that reads an unbound variable. A guard asserts the
   store-spy criterion still holds under every mode the optimiser can select.

### What (C) costs, and the rung it defers

The SIPS stays frontier-quality, so demand propagation is no better than it would have been
without this work. That is a deferral, not a loss — and one cheap improvement is available
immediately, which is the subject of the next section.

**Recorded, not scheduled: a cost-based SIPS.** Choosing adornment from a costed order would
need the cycle broken some other way — most plausibly by costing over `Deps` and nominal
statistics alone, without lowering, accepting that the SIPS order and the physical order then
differ and proving the `b`-position refinement property between them. That is a research-shaped
item and it belongs in `PLAN.md` beside the closure operator, not in a movement.

### The free improvement: Run 1 improves the SIPS at zero cost to the phase order

Revision 9 found that `reorder`'s legality predicate is strictly narrower than the compiler's
own `safe()` check, and froze `reorder` rather than seeding it, to avoid moving plans in a
legality run. Under recursion that decision needs revisiting, and the answer is better than
either alternative.

The divergent class — a variable bound only by a folded constant, read by a negation, a derived
bind, a comparison, a constraint or an alias — makes `reorder` **dead-end** and emit the
remainder in collection order (`reorder.rs:281-285`). Under recursion that degenerate order *is
the SIPS*, so those rules get degenerate adornment: everything free, no demand propagated.

The resolution keeps both properties:

- **`reorder` stays frozen** for plain queries, so no existing plan or fingerprint moves.
- **Adornment consumes the seeded variant** (`respects_admissible` / a seeded frontier). It is
  still a pure function of `Deps` plus `Deps::pre_bound`, so step 6 stays acyclic — and
  adornment is a *new* consumer with no legacy to preserve, so improving it is free.

So Run 1 is not merely compatible with the recursion work; it is a **direct contribution to
Movement 6's SIPS quality**, deliverable before Movement 6 starts, and small enough to review in
a sitting.

---

## C2 — the predicate-class estimate table

Revision 9's cost model has one notion of a predicate's base cardinality: a nominal constant,
replaced by an exact count in Run 8 when a sealed database carries one. Under recursion that is
wrong in a way that would mis-order most generated rules, because the executable reads four
different kinds of predicate and only one of them can ever have a count.

| Class | What it is | Statistics available | Nominal shape |
|---|---|---|---|
| **Base** | a schema predicate in the frozen store | Run 7's `PredicateCount` on a Complete database; `None` otherwise | as revision 9 |
| **Accumulated derived** `A_p` | everything a local relation has derived through round `r` | never — item 4 makes a local relation query-local and never stored | large; grows across rounds |
| **Delta derived** `Δ_p` | the part of `A_p` that is new this round | never | **small**, and small is the entire premise of semi-naive |
| **Magic / supplementary** `magic_p^a`, `sup_i` | the demanded bindings, and shared body prefixes | never | small; demand-shaped, not predicate-shaped |

The consequence of collapsing these is concrete and not subtle. Semi-naive generates one delta
variant per recursive occurrence (`PLAN.md:1415`, step 2), and the **whole point** of the
variant is that the selected occurrence reads `Δ_r` while every other reads `A_r` (step 3). A
cost model that assigns `Δ_p` and `A_p` the same base will order the variant as if the two were
interchangeable, and will systematically choose to drive from the accumulated side — which is
naive evaluation wearing semi-naive's clothes, with correct answers and no failing test.

Likewise magic: `magic_p^a` holds the demanded subset, which is the reason it exists. A model
that ranks it like an ordinary relation is the same failure C1 describes, reached from the cost
side rather than the ordering side.

**So the audit table gains a per-class nominal tier**, with the same discipline every other
constant in it has: one named case per constant, boundary properties, and a mutation control
that makes a gate fail when a tier is changed. The tiers are **declared ordering assumptions**,
not measurements, and the module doc says so — `Δ ≪ magic ≲ A ≲ base` is the ordering semi-naive
and magic are built on, and the cost model is entitled to assume what the transformation
guarantees.

**What is deliberately *not* attempted.** No attempt is made to estimate a derived relation's
actual cardinality, to model growth across rounds, or to let round number enter the cost. All
three would make the selected plan a function of runtime state, which item 7's "fallback is
compile-time only" and Movement 4's "the selected executable is a pure function of source,
schema, engine build and compiler policy" both forbid. The plan is chosen once, per rule, at
compile time, from declared tiers. Round-aware costing is a rung, recorded with the cost-based
SIPS.

---

## C4 — the objective is store-blind, and that is now a declared limitation

Revision 9's objective is estimated **rows examined**, the quantity `Profile::total` measures
(`iter.rs:503`, `:519`). Under `Overlay<S>`, a scan of a derived predicate routes to an
in-memory `Relation` and a scan of a base predicate routes to fjall (`PLAN.md:2582-2588`), and
`Profile::total` counts a row from either identically.

That is fine as an *accounting* choice — it is what the profile reports and what Movement 6's
and Movement 8's guards measure — and wrong as a *cost* assumption, because a relation scan is
very much cheaper per row than a disk-backed one. An optimiser minimising rows examined will
systematically under-use derived relations relative to what actually runs fastest.

**The resolution is to declare the assumption rather than silently make it.** The audit table
gains a per-access-class **store weight**, defaulting to `1` for every class — so revision 10
ships exactly revision 9's arithmetic — with the weight for relation-backed access pinned by
**Movement 1's own benchmark**, which already measures an empty-range seek, a narrow seek, a
point lookup and a full scan against a batch-built oracle (`PLAN.md:2606-2613`). Until that
number exists the weight stays at `1` and the book states that rows examined is the objective
and that it does not distinguish the two stores.

This keeps the objective identical to what the guards measure, keeps the cost model honest
about what it is assuming, and puts the one number that would change it in the movement that is
already measuring it.

---

## C5 — questions already settled, adopted rather than re-derived

Revision 9 argued four things from first principles that `PLAN.md` has already decided, in some
cases through several adversarial rounds. Revision 10 adopts the existing answers, cites them,
and deletes its own derivations. This is the largest simplification in this revision.

**Limits are policy, not semantics, and do not enter a fingerprint** (`PLAN.md:1463-1490`):

> The classification is performed here rather than promised, and the answer is uniform: all of
> them are deployment policy. None is semantics, none enters the program fingerprint. … A limit
> refuses; it does not change an answer, and a reproducibility claim is about answers.

The cost planner's search budgets are compiler policy of exactly this kind. They join item 6's
table as a new row — with a different outcome column, because unlike every existing entry they
are neither terminal nor a fallback of the *executable*:

| Limit | Charged over | Scope | Reset | Outcome |
|---|---|---|---|---|
| Search budgets (`MAX_DP_STATES`, `MAX_COMPLETE_CANDIDATES`, …) | one rule body | one compilation | per compilation | **degrades to the next search rung**; never terminal, never an executable fallback |

**An optimiser may change resource outcomes; it may not change static validity**
(`PLAN.md:1480-1483`):

> An optimiser changing whether a query hits a resource ceiling is ordinary and unremarkable; an
> index that makes a query fit a timeout is the same phenomenon and nobody calls it a semantic
> change. An optimiser changing which programs are *well-formed* is the thing worth forbidding.

That is precisely what revision 9's admission control enforces structurally, and revision 10
keeps the mechanism while adopting this as its stated justification rather than arguing for one.

**The fallback trigger is a pipeline order, not a list of error kinds** (`PLAN.md:1603-1612`):

> So the pipeline is ordered rather than the errors classified: the unmagicked rules are
> retained, the magic form is carried through every downstream phase, and only a candidate that
> finishes compiling is selected.

Revision 9's Run 4 admission — compute the baseline first, carry candidates through, return the
baseline whole on any candidate fault — is the same shape. Revision 10 states it in item 7's
language so a reader meets one pattern twice rather than two patterns once.

**The selected executable's inputs are already enumerated** (`PLAN.md:2782-2795`):

> **The selected executable is a pure function of source, schema, engine build and compiler
> policy** … Compiler policy is the fourth input, and a draft of this criterion omitted it —
> which made the claim false rather than incomplete.

This resolves revision 9's Run 8 argument outright, and in the same direction. Statistics
generation becomes a **fifth** input, named the same way and for the same reason: it changes
which order is selected, the order is fingerprinted, and a change therefore produces a **named
refusal** rather than a silent resume into a different executable. Revision 9 reached the same
conclusion by reasoning about plan fingerprints; the program-level statement subsumes it, and
the "do not put a statistics digest in the world stamp" decision survives unchanged — the order
is already covered by the program fingerprint, so a separate digest would be redundant, not
merely undesirable.

**The order is already fingerprinted** (`PLAN.md:2772`):

> The program fingerprint covers **all** of: the answer plan, every relation declaration and its
> physical layout, its materialisation projection, **every rule's target and order**, every
> generated magic, supplementary, accumulated and delta relation, stratum kind and order …

So a cost-moved order moves the program fingerprint, and item 13's envelope refuses a stale
cursor before a row description. Revision 9's fingerprint reasoning composes without change.

---

## C3 — compile cost is per program, and the DP moves from desirable to close to required

Item 9's phase order runs flatten at **step 9, per executable rule**, over the magic candidate,
with the unmagicked baseline "re-analysed and flattened only if the fallback is actually taken"
(`PLAN.md:1630-1633`). Stack the multipliers:

- a program's rules, which the user wrote;
- **times DNF expansion**, which is a *product*: `(A|B); (C|D)` is four clauses, and item 10 is
  explicit that anything less answers differently (`PLAN.md:1722-1729`);
- **times semi-naive**, one delta variant per recursive occurrence (`PLAN.md:1415`);
- **plus** magic and supplementary relations, themselves adorned and delta-generated;
- **times up to two candidates**, when the fallback is taken.

A modest recursive program reaches tens of optimiser invocations. Revision 9's budgets are
stated per body; at Run 4's enumeration cost — `candidates × emit`, with `emit` a full lowering
— that dominates compile time for any recursive query, and compile time is paid **per chunk**,
because `run_query` re-prepares on every request and the count path re-derives per chunk
(`PLAN.md:1856`, `:3075-3082`).

Two consequences, both of which strengthen revision 9's reframe rather than undermining it:

1. **Every compile-cost bound in this plan is restated per program**, not per body: the ratio
   bound in issue-level acceptance, the witnesses in the corpus, and the budget table's scope
   column. A per-body bound that is satisfied fifty times over is not a bound.
2. **The subset DP stops being an optimisation of the optimiser and becomes a prerequisite for
   recursion having acceptable compile times.** Revision 9 already promoted it to the plan's
   destination on the strength of a 100,000× state-count ratio at n=12; C3 is the second,
   independent argument for the same conclusion, and it is the one that will actually be felt.

---

## What revision 10 changes

| # | Change | Source |
|---|---|---|
| A | **The magic literal is pinned first and exempt from cost reordering**; adornment keeps the frontier order as the SIPS. Resolves the phase-order cycle. | C1 |
| B | **Run 1's seeded predicate feeds adornment** while `reorder` stays frozen for plain queries — a free improvement to Movement 6's SIPS. | C1 |
| C | **A predicate-class estimate tier** — base, accumulated, delta, magic — in the audit table. | C2 |
| D | **A declared per-access-class store weight**, defaulting to 1, pinned by Movement 1's relation benchmark. | C4 |
| E | **Compile-cost bounds restated per program**; search budgets join item 6's limit table as a new row with a degrading outcome. | C3, C5 |
| F | **Four derivations deleted and replaced by citations** — limit classification, optimiser-versus-static-validity, the fallback pipeline order, and the selected executable's inputs. | C5 |
| G | **Statistics named as the fifth input** to executable selection, in Movement 4's own language. | C5 |
| H | **The optimiser runs per executable rule**, and the corpus gains a program tier. | `PLAN.md:3097` |
| I | **New invariants asserted rather than assumed**: reordering a body leaves derived tuples, canonical ids, round counts and rule-output attempts unchanged. | below |
| J | **An interleaving with the recursion movements**, replacing revision 9's implicit assumption that this work lands into a static compiler. | below |

Everything else is revision 9, carried: the amendment taxonomy and its two retroactive sites,
the admissible-order predicate, Bellman locality, emission-inertness, the written-order
replacements, the fault/skip separation, the defect ledger and verified citations. All code
citations re-checked against the working tree at `51d4868`.

### New invariants (change I)

Reordering a rule body changes which rows are examined. It must change nothing else, and under
recursion "nothing else" acquires three new members that revision 9 had no reason to name:

- **The derived tuple set is unchanged.** A body is a conjunction; its satisfying bindings are
  order-independent. This is the recursive analogue of "no selection changes an answer".
- **Canonical ids are unchanged.** Item 3 assigns them "by rank in encoded-key order", and
  Movement 3's acceptance already requires that "permuting rule order, switching rectangular for
  triangular expansion, or changing the snapshot representation leaves every id unchanged".
  Statement reordering joins that list — and it matters because `Executor::resume`
  hard-compares a saved `fact_id`, so a moved id is a cursor-compatibility surface.
- **Round counts and rule-output attempts are unchanged.** Rounds depend on what is derived, not
  on how; attempts count rule outputs, one per satisfying binding tuple, which is
  order-independent. Item 5b exists to keep both stable and free of declaration-order artefacts
  (`PLAN.md:1411-1443`), and the cost optimiser must not become a new source of the same defect.
  **Rows examined is the one number that legitimately moves**, and item 6 has already classified
  it as policy rather than semantics.

Each is a named guard in Run 4, run again under every search mode in Run 5.

---

## Destination and route (carried from revision 9, rescoped)

**The destination is a subset dynamic program, exact to an order of magnitude more statements
than enumeration can reach. Exhaustive enumeration is not the product; it is the oracle that
makes shipping a DP legitimate, and the fallback when a body carries a hazard the DP cannot
absorb.**

Each enumeration candidate is a **full lowering** of every statement in the body; a DP's states
are prefixes, each lowered once and reused by every extension:

| order-relevant statements | enumeration (statement-lowerings) | DP (statement-lowerings) | ratio |
|---|---|---|---|
| 5 | ~600 | ~160 | 4x |
| 6 | ~4,300 | ~384 | 11x |
| 8 | ~320,000 | ~2,000 | 157x |
| 10 | ~3.6e7 | ~10,000 | 3,500x |
| 12 | ~5.7e9 | ~49,000 | 100,000x |

State counting bounds the DP at roughly 12–15 statements; whether that range is *reachable*
depends on the per-transition constant — each transition still lowers one statement — and that
constant is unmeasured. Every specific `n` in this document is a state-count bound awaiting a
compile-cost measurement, not a performance claim. Under C3 the measurement is per program, and
it is the number that decides whether recursive queries compile in an acceptable time at all.

The prefix-cost recurrence is **false as stated** against the code (see the amendment taxonomy),
any repair is a non-trivial claim, and there is no way to certify such a claim except against a
trusted answer for the same body. So the route is: build the oracle, prove the recurrence
against it, then ship the DP with the oracle retained as a permanent cross-check and fallback.

Two limits are part of the specification:

1. The optimiser is **exact under its declared, clamped estimate model only when it completes a
   search within its budgets**. On overflow it discards the result and degrades one rung.
2. No statistics-free estimate can promise universal runtime improvement — and under recursion
   three of the four predicate classes can *never* have statistics, so the declared tiers of C2
   are load-bearing rather than a fallback.

---

## The defect ledger

Six adversarial reviews have now run against this plan. The table is its memory: a premise
refuted here may not return without new evidence against the row that killed it.

| Rev | Defect | Verified at |
|---|---|---|
| 1→2 | DP substructure premise false — a product of per-statement *access* estimates is not a set function | counterexample, pinned |
| 1→2 | Cost model described statements, not emitted operators | `flatten.rs:2777`, `:2781`; `iter.rs:493` |
| 1→2 | `fjord.db.Stat` an unguarded I4 regression | `session.rs:1469-1470`, `:1545`, `:1640` |
| 2→3 | Subset alone is not a sufficient DP state — `fetch_level` reuses registers | `flatten.rs:1966-1991` |
| 2→3 | The claim that moving `Constrain`/`Compare` shifts addresses was **wrong** | `flatten.rs:474-495` |
| 2→3 | `approximate_len()` is "reliable", not exact; the exact count is free in the identity walk | `identity.rs:100-111` |
| 3→4 | Fetch reuse is **amended**, not just reused | `flatten.rs:1997-2032` |
| 6→7 | **The prefix-cost recurrence is invalid outright** | `flatten.rs:1997-2032` |
| 6→7 | An audit that grades the optimiser by its own cost model proves nothing; the baseline must be data | design |
| 7→8 | **The enumeration's legality predicate is strictly narrower than `safe()`** — clean queries get an empty candidate set and the optimality gate passes vacuously | `flatten.rs:2531-2543` vs `reorder.rs:263-292` |
| 7→8 | `preserves_written_order` is public API with a live proptest; a cost chooser breaks it by construction | `reorder.rs:62-88`, `:304-331`, `:628-653` |
| 7→8 | Compile time unbounded while execution rows are the objective | `flatten.rs:2602` |
| 8→9 | **`apply_selects` is a second retroactive amendment site** | `flatten.rs:2842-2868`, `:4672` |
| 8→9 | The DP was treated as a deferred aside, understating its value by ~5 orders of magnitude | arithmetic above |
| 8→9 | "Validate the DP against the oracle" is not a strategy — the oracle cannot reach the n the DP serves | answered by Bellman locality |
| **9→10** | **An unconstrained cost optimiser defeats magic sets and fails an existing Movement 6 acceptance criterion**, silently, with correct answers: a nominal model has no statistic for `magic_p^a` and will order the magic literal late, turning a seed into a filter | `PLAN.md:2932`, `:2936`, `:3002-3006` |
| **9→10** | **A cost-based order cannot feed adornment** — it exists only at phase step 9 and adornment consumes an order at step 6 | `PLAN.md:1657` |
| **9→10** | **One nominal base for every predicate mis-orders every semi-naive delta variant**, choosing the accumulated side and reducing semi-naive to naive with no failing test | `PLAN.md:1415`, item 4 |
| **9→10** | Compile-cost bounds stated per body are meaningless in a pipeline that invokes the optimiser tens of times per program | `PLAN.md:1660-1672`, `:1721-1727` |
| **9→10** | Rows examined does not distinguish an in-memory relation scan from a disk-backed base scan | `PLAN.md:2582-2588` |
| **9→10** | Four questions were derived from first principles that `PLAN.md` had already settled through adversarial review | `PLAN.md:1463-1490`, `:1587-1637`, `:2782-2795` |

---

## The two order predicates (carried from revision 9)

`reorder` seeds its bound set **empty** (`reorder.rs:263-292`); `safe()`, the check that decides
whether a compiled order is legal, seeds from **every folded constant** first
(`flatten.rs:2531-2543`). The gap is real because a constant bind produces no statement at all
(`flatten.rs:1461-1464` → `fold_into`, `:4472-4474`), so nothing in `Deps` captures the
variable while statements that *read* it keep it in `reads` permanently — a negation
(`:1051-1058`), a constraint (`:1065`), a comparison (`:1071-1074`), a derived bind
(`:1077-1080`), an alias (`:1012-1029`). `negated_wildcards` (`:2141-2154`) exists to compensate
for exactly this asymmetry.

Measured, with a temporary test since reverted:

```text
src:   B where B = test.Bar {id = 2}; N = 1; !test.Node {id = N}
deps:  [ captures:[B] reads:[]  ] , [ captures:[] reads:[N] ]   <- nothing captures N
reorder order = [0, 1]                    <- the dead-end path, reorder.rs:281-285
order satisfies reads-before-read: false
compiles: true, diagnostics = []          <- a clean, supported query

src:   Y where B = test.Bar {id = 2}; N = 1; Y = N + 1
reorder order = [0, 1];  compiles: true, diagnostics = []
```

Under revision 7 these queries were admitted, enumerated to **zero** candidates, and the
criterion "exhaustive selections equal the independent global optimum" passed **vacuously**.

**The admissible-order predicate** is defined to be `safe()`'s: a complete permutation is
admissible when, walking from a bound set seeded with the query's folded-constant symbols, every
statement's `reads` are bound at the position it occupies. Run 1 proves `admissible(o)` holds
exactly when `safe(o)` returns true — the loop half a syntactic correspondence
(`flatten.rs:2549-2566`), the head half order-invariant because `Deps` is computed once at
collect time (`:2568-2576`).

Three consequences: the candidate set is never empty after admission; every enumerated candidate
is safe by construction; and the optimiser reaches orders `reorder` cannot produce.

**Under recursion the predicate gains a fourth clause**: in a magicked rule, an order is
admissible only if the magic literal is first (C1). It is expressed as an ordinary precedence
constraint over the collected statements, so the enumerator, the greedy fallback, the DP and the
Run 2 oracle all inherit it without a special case, and no search mode can produce an order that
defeats the seed.

`reorder`, `Deps::respects` and `Deps::antichains` are **not modified** — the proptest at
`reorder.rs:605-614` pairs `respects` with `antichains` as `reorder`'s own oracle. `Deps` gains
`pre_bound`, and `respects_admissible` / `antichains_admissible` alongside the existing pair.
Adornment consumes the seeded variant (change B).

---

## The amendment taxonomy (carried from revision 9)

The recurrence `cost(S + m) = cost(S) + card(S) * access(m | state(S))` is valid exactly when no
amendment triggered by placing `m` changes the cost of an operator already emitted for `S`.
`Body::level_mut` (`flatten.rs:508`) is the only way an emitted level is modified. Every call
site, classified:

**Retroactive — reaches past the level that bound the variable:**

| Site | Trigger | Attaches to |
|---|---|---|
| `chase` (`flatten.rs:2022-2031`) | placing a chasable row bind whose reference was already fetched | the **earlier** fetch level found by `(address, path)` reuse (`:1973-1975`) |
| `apply_selects` (`flatten.rs:2842-2868`) | **any** statement reading `X.alt?`, however late | the level that bound `X`, recorded at `:4672` |

**Prefix-determined — attaches to the level that bound the variable:**
`apply_constraints` (`:3800-3850`), `apply_denials` (`:4298-4340`), `apply_comparison` via
`push_residual` (`:3995`, `:4274-4295`), and `apply_compares` (`:2883-2940`, the later of two
bound levels at `:2919-2924`). All four draw from sets collected before an order is chosen, so
at the moment a prefix binds a variable every amendment it will attract is already known and
attaches to the level just added.

**The hazard admission test:** a body is hazard-free when it contains no chasable row bind and
no union select. `chasable()` (`flatten.rs:1614-1656`) sets a per-statement flag during
collection, before ordering; `chase` is reached only through it (`:2620-2629`). A union select
is syntactic. Both are decidable at phase step 3, before any rewrite — which matters under
recursion, because the classification must be available per generated rule and generated rules
do not exist until step 8.

**A generated rule inherits its source rule's hazards and adds none.** Adornment prepends a
magic atom; semi-naive substitutes a delta relation for one occurrence; DNF selects one
alternative per disjunction. None introduces a chasable bind or a union select that the source
body did not have — but that is a claim about three generators, so Run 5 asserts it directly
over generated programs rather than inheriting it.

---

## The permanent acceptance artifact: the cost-plan corpus

`fjord_engine::cost::corpus`, following the discipline of the existing target-feature corpus
(`fjord-engine/src/corpus.rs`, gated at `lib.rs:54-55`). Each entry:

```text
name / schema source / query or program source / fixture facts
statistics mode and values
baseline forced order (per rule)
expected selected order and selection mode (per rule)
expected baseline and selected abstract costs
expected baseline and selected complete PlanView snapshots
expected baseline and selected fingerprints (plan, and program where applicable)
expected baseline and selected rows in execution order
expected semantic result multiset
optional exact Profile/store-operation counts for performance witnesses
optional per-program compile-time bound
hazard classification, and the non-retroactive-variant plan where it differs
predicate-class tier assignments for every predicate the entry reads
coverage tags
```

`selection mode` is one of `DynamicProgram`, `Exhaustive`, `GreedyStatementLimit`,
`GreedyCandidateBudget`, `GreedyTransitionBudget`, `GreedyStateBudget`,
`BaselineCandidateFault`, or `Unorderable`.

The baseline is data, handed to `flatten_in_order` (`flatten.rs:678-686`), so changing the
optimiser cannot move both sides of the comparison. Both plans execute and must return their
separately stated ordered rows; sorting must produce the one stated multiset.

The census requires everything revision 9 required — access, lowering, filters, admissibility,
amendment, selection, DP, arithmetic, statistics, compatibility, compile cost — **plus a program
tier**, which is new:

- a `with` block with one non-recursive local relation, one self-recursive, and one mutually
  recursive pair;
- a rule body carrying each hazard, and one carrying neither;
- a magicked rule where the cost model *would* order the magic literal late if unpinned —
  constructed so the store-spy criterion fails without the pin, which is what makes the pin a
  tested constraint rather than a stated one;
- a delta variant where the accumulated and delta tiers select different orders, proving the
  tier table does work;
- a DNF-expanded body, with the cost model's prediction compared against item 10's own
  `2^(d-k-1)` prefix-scan arithmetic — a mutual cross-check between two independently derived
  numbers, and the cheapest validation of the cost model available anywhere in this plan;
- a program where the magic candidate is discarded and the unmagicked fallback is flattened,
  proving the optimiser runs over whichever rule set is selected and not over both;
- a program whose per-program compile-cost bound is recorded at each search rung.

Mutation controls must make the relevant gate fail when an access rank, factor attachment,
amendment, tie-break, clamp, fingerprint tag, fallback boundary, admissibility seed, hazard
classification, **predicate-class tier, store weight, or the magic-literal pin** is changed.

---

## The runs

Nine runs, numbered 0–8, unchanged in identity from revision 9. What changes is that four of
them now have a recursion-facing half, and the interleaving section below says when each should
land relative to the recursion movements.

### Run 0 — virtual ids are functions of rows, not iterator order

**Claim:** every permutation of the same virtual rows produces identical scan rows, `FactId`
mappings, per-table digests, and point-read answers.

`Table::of` (`catalogue.rs:349-393`) mints `FactId::new(predicate, sequence + 1)` from the
**input iterator order** (`:373`) and sorts into key order afterwards (`:386`);
`digest_of_rows` (`:396-409`) hashes predicate, count and framed keys, never ids. `listing_rows`
walks the store root, so input order is filesystem-dependent. Encode key bytes first, sort, then
enumerate to mint dense ids.

Independent of recursion, and a shipped defect — but item 12 makes it *more* load-bearing, not
less: a local relation may legally hold a virtual reference, and `run_query` re-prepares on every
request, so a virtual id that moves between two pages of a recursive read is a wrong answer
inside a fixpoint rather than a stale fetch.

**Proof:** a generated permutation property over the complete `(key, FactId)` scan, digest, and
every valid and one-past point read; a forced legacy/new resume case requiring `BadResumeKey`
with plan and world proved equal; a fetch case proving a stale virtual id is refused.

**Exported facts, split:** **F0-a (construction)** — equal sorted row bytes imply equal
`key -> FactId` mappings, true by construction after the fix, needing no collision model.
**F0-b (digest)** — equal per-table digests imply equal sorted row bytes under the repository's
accepted 64-bit collision model; the only place the model is needed, consumed by Run 7.

### Run 1 — the admissible-order predicate, and the SIPS it feeds

**Claim:** a complete permutation is admissible exactly when `safe()` accepts it; the admissible
space is non-empty for every body that compiles; and it strictly contains `reorder`'s reachable
space.

Nothing in production consumes it for *plan selection* yet. What is new in revision 10 is that
it acquires a consumer immediately: **adornment**, which under change B reads the seeded frontier
rather than the empty-seeded one.

- `Deps` gains `pre_bound: Box<[Symbol]>` from the folded-constant bindings — the same set
  `safe()` seeds from (`flatten.rs:2534-2540`) and `negated_wildcards` already seeds from
  (`:2148-2154`).
- `Deps::respects_admissible`, `Deps::antichains_admissible`, and a seeded frontier for adornment.
- `reorder` (`reorder.rs:263-292`), `Deps::respects` (`:147-171`) and `Deps::antichains` (`:183`)
  are **not modified**.

**Proof:** the equivalence as a property over the generated model, both halves separately; the
two probe queries pinned by name; the coincidence condition (`respects_admissible == respects`
exactly when `pre_bound` is empty); non-emptiness; and mutation controls on the seed.
**Plus, once `Program` exists:** the flatness assertion of C8 — every body the optimiser sees is
a flat conjunction — so that a future nested group fails a test rather than silently invalidating
the DP.

**Exported facts:** admissible == `safe()` on complete permutations; the space is non-empty
whenever the body compiles; adornment's SIPS is non-degenerate for the folded-constant class.

### Run 2 — cost specification, oracle, amendment pricing, and the class tiers

**Claim:** the abstract cost of a finalized plan is total and deterministic; the reference
evaluator assigns the same declared cost without production machinery; the amendment taxonomy is
exhaustive; and the non-retroactive variant's plan-quality cost is measured, not argued.

The objective is estimated **rows examined**, the quantity `Profile::total` measures
(`iter.rs:503`, `:519`). Distinct `Rows`, `Fanout`, `Selectivity`, `ExaminedCost` newtypes;
`Selectivity` a reduced integer ratio with zero-preserving ceiling division; saturating
arithmetic at declared clamps; no `f64`, platform-sized arithmetic or backend-dependent value,
because `fjord-engine` builds for `wasm32-unknown-unknown` and a browser-compiled plan must be
byte-identical to a server-compiled one.

The audit table states every nominal base, fan-out, filter selectivity, probe survival estimate
and clamp — **plus, new in revision 10, the predicate-class tiers of C2 and the store weights of
C4**, each with a named case, boundary properties and a mutation control.

Cost is evaluated over an interner-free `FinalizedCostPlan` in physical execution order:

```rust
struct FinalizedCostPlan { operators: Box<[CostOperator]> }
enum CostOperator {
    Level   { sources: Box<[CostSource]> },
    Probe   { sources: Box<[CostSource]>, survival: Selectivity },
    Derive,
    Compare { survival: Selectivity },
}
```

Each `CostSource` carries its physical access class, its **predicate class tier**, and the
ordered semantic factors that run there. Alternatives sum; a fetch reads at most one row; an
access-chain fetch is cardinality-neutral; a chased predicate contributes its base and
functional-dependency factor at the fetch it amends; negation is a probe plus a survival filter.

**The independent oracle** is a deliberately slow `ReferenceOrderEvaluator` forbidden from
calling `FinalizedCostPlan`, `CostOperator`, production emission or classification helpers,
production factor activation, or production arithmetic beyond the strong-unit constructors. It
is itself proved by hand-calculated audit cases, algebraic properties, and a token interpreter
that materialises `input_rows` unit tokens and counts examined tokens directly. It enumerates the
**admissible** space — an oracle over the narrower space cannot detect that the search used the
narrower space — and asserts `|admissible orders| >= 1` for every case.

**The amendment taxonomy is discharged mechanically:** a guard asserts `Body::level_mut` has
exactly the six named call sites and fails if a seventh appears; and for every generated body,
every admissible prefix `S` and extension `m`, lowering `S` and `S + m` and comparing the first
`|S|` operators must yield an empty difference for a hazard-free body, and differences
attributable only to `chase` and `apply_selects` otherwise.

**Amendment pricing** records, for every hazardous entry, both plans and costs: the current one,
and the one a variant emitter produces by emitting chased residuals and discriminant filters at
their own position instead of attaching to an earlier level. The gate records the delta; it does
not act on it. This is the measurement that decides Run 5's shape.

**Exported facts:** the formula is total and monotone; the reference evaluator is the oracle over
the admissible space; the taxonomy is exhaustive and the hazard test conservative; the class
tiers and store weights are declared and audited; the non-retroactive variant's cost is a number.

### Run 3 — one annotated emitter, chooser unchanged, baseline corpus frozen

**Claim:** producing a finalized cost trace alongside a plan preserves every current plan,
diagnostic, fingerprint and answer; the trace describes the plan it accompanies; and
`Constrain`/`Compare` are emission-inert.

Emission is refactored around an internal annotated body; stripping annotations yields the
existing `Plan`. Fetch reuse and amendments mutate that one body, so plan and trace cannot
disagree. Collection produces an immutable `EmissionSeed`; each candidate starts from a fresh
emitter with its own bindings, fetched map, folded factors, annotated body and scratch
diagnostics.

**This run and item 9's step 3 are the same refactor** (C6). `PLAN.md:1653-1657` requires
collection to become "runnable over an arbitrary rule body independently of plan emission" so
adornment can read collected statements without reconstructing anything from `Plan`. That is
exactly what `EmissionSeed` is. Doing them together is one piece of work; doing them separately
is two plus a merge, in the file both are rewriting.

The interner half is already true and is recorded as a checked premise: the only mutating
interner call in flatten is `fresh` (`flatten.rs:1880-1882`), reached once from hoisting during
collection (`:1371`); everything else is `try_resolve`; collection runs once, before an order is
chosen (`:794-805`). A guard asserts the interner's length is unchanged across a candidate.

**Emission-inertness, proved:** `next_address` is `Address::new(self.registers)` and `registers`
advances only in `push_level`/`push_derive` (`flatten.rs:474-486`) — off **steps emitted**, not
off position. `Stmt::Constrain(_) => {}` (`:2777`) and `Stmt::Compare(_) => {}` (`:2781`) emit
nothing, their work being done after the body loop by `apply_compares`/`apply_constraints`
(`:2799-2800`). So a statement emitting no step cannot shift an address. Both are
`Placement::Floating` (`:194-201`), so written order never constrained them either. The word is
split: *emission-inert* licenses partitioning them out of the search; *legality-relevant* is why
they still carry `reads` and must appear in the returned permutation so `safe()` can report
their span.

**Proof:** retain the old emitter as test-only `legacy_emit`; differentially compare old and
annotated emitters over the generator extended with `Alias`, `Derive`, binary `Compare`,
access-chain fetch, reused fetch, union select and multiple amendments, across **every admissible
forced order**, comparing complete `Plan`, fingerprint, rendered diagnostics with spans, and
executed rows. An independent classifier compares each cost operator's access kind, physical
position, sources, residual order and amendment target against the *stripped* plan, without
reading annotations. Every generated order's production breakdown is compared with Run 2's
reference evaluator.

**Baseline corpus frozen.** For the admissibility class the baseline is the plan `reorder`
produces today via its dead-end path (`reorder.rs:281-285`). The Run 3 gate rejects any plan or
fingerprint churn — which is also `PLAN.md`'s own Movement 1 criterion ("every corpus entry's
plan fingerprint is unchanged").

### Run 4 — bounded enumeration and greedy fallback (the oracle)

**Claim:** exhaustive selection returns the globally cheapest admissible finalized plan under the
declared model; every fallback is deterministic and legal; no selection changes an answer, a
derived tuple, a canonical id, a round count or an attempt tally; and compile cost stays inside a
stated **per-program** bound.

**Admission**, in item 7's language: the baseline order is computed with the frozen `reorder`,
the unchanged safety check and annotated emitter run once with public diagnostics, and if the
body is unsafe, diagnosed or produces no plan that result is returned exactly. The optimiser
cannot make an unsupported construct appear supported or change which variable an unorderable
body reports. Admission also establishes non-emptiness: `safe()` accepted the baseline order, so
by Run 1 that order is admissible, so the candidate set contains at least it.

**Search:** partition out the emission-inert statements; enumerate complete orders of the
remainder under the admissible predicate, including the magic-literal pin; lower and cost each
from a fresh `EmissionSeed`. Do not merge subsets — that is Run 5's job, and only after Run 5 has
earned it.

| Budget | Counts | Scope | Initial |
|---|---|---|---|
| `MAX_ORDERED_STATEMENTS` | order-relevant statements in one body | per body | 8 |
| `MAX_SEARCH_TRANSITIONS` | admissible prefix extensions | per body | 8192 |
| `MAX_COMPLETE_CANDIDATES` | complete plans lowered and costed | per body | 256 |
| `MAX_PROGRAM_LOWERINGS` | total statement-lowerings across every executable rule | **per compilation** | to be pinned |

The last is new in revision 10 and is the one C3 makes necessary: three per-body budgets
satisfied fifty times over are not a bound. Values are proposals pinned by the compile-cost
witnesses, and they enter the audit table and item 6's limit table together.

**Two failure modes, separated:** a candidate that is not admissible is never generated, because
the enumerator's predicate *is* admissibility — a debug assertion, not a runtime branch. A
candidate that is admissible but whose emitter faults returns the admitted baseline whole as
`BaselineCandidateFault`, reachable only by fault injection.

**Greedy fallback:** minimum `(immediate_access_rank, source_index)`, rebuilding the local effect
against the state at that point, over the **admissible** frontier so greedy and exhaustive search
the same space and greedy cannot dead-end after admission. `reorder`'s dead-end branch stays where
it is, still the right answer for a genuinely unorderable body.

**Written order:** `reorder` is unmodified so `reorder_keeps_the_written_statements_in_written_order`
(`reorder.rs:628-653`) stays green untouched. Complete-search modes retire the global invariant
and state the provable residual — ties break on `(ExaminedCost, complete_order)`
lexicographically, so **written order wins every tie and is displaced only by a strictly cheaper
complete plan**. Greedy restates it over `immediate_access_rank`. `Placement::Floating` covers
`Alias`, `Constrain`, `Compare` and `Derive` (`flatten.rs:194-201`), so the property only ever
constrained `Scan` and `Negate`; `reorder.rs:62-88` is rewritten to name its actual consumers.

**Recursion-facing proofs (change I):** for every generated program, reordering any body leaves
the derived tuple set, every canonical id, every round count and every rule-output attempt tally
identical, with only rows examined moving. Plus the C1 guard: Movement 6's store-spy criterion
holds under every search mode.

**Fingerprint:** `PlanFingerprint`'s plan walk (`plan.rs:1286-1330`, from `Plan::fingerprint`,
`:833`) hashes `plan.body` in order and each source's residuals in order, and the program
fingerprint covers "every rule's target and order" (`PLAN.md:2772`). A moved order moves both, and
item 13's envelope refuses a stale cursor before a row description. No cursor layout changes.

### Run 5 — the subset dynamic program (the destination)

**Claim:** for a body the amendment taxonomy classifies hazard-free, the subset recurrence is
exact, and the resulting plan is identical to Run 4's certified winner wherever Run 4 reaches.

**Shape decided by Run 2's pricing**, not here. If the non-retroactive lowering costs little plan
quality it becomes the shipped behaviour, both retroactive sites disappear, every body is
hazard-free, and this run is a DP with no routing. Otherwise the hybrid stands and hazardous
bodies fall to Run 4.

**Preconditions**, each gated by property rather than argued:

> **F5-a.** For a hazard-free body, the set of distinct fetches existing after a prefix is a
> function of the prefix **subset** alone. (The hard part of F5-c; `fetched` is keyed by
> `(address, path)` and the referring row is bound by exactly one level in any order.)
>
> **F5-c (future-equivalence).** `state(S)` — bound set, fetch set, accumulated cardinality — is
> a function of the subset alone, so two prefixes over one subset have identical futures.
>
> **Monotonicity.** `cost(S + m) >= cost(S)`: every charge is a saturating addition of a
> non-negative term and nothing subtracts from an already-charged operator in hazard-free mode.

**Bellman locality is the validation strategy**, because Run 4's exhaustive mode reaches about
six statements and this run serves twelve to fifteen — an oracle that cannot reach the domain
cannot certify it:

> **F5-b.** For every admissible prefix `S` and extension `m`:
> `incremental_charge(S, m) == cost(lower(S + m)) - cost(lower(S))`, both right-hand terms from
> Run 2's evaluator over a full lowering.

Two full lowerings per transition — expensive, but only in tests, and it scales with
*transitions* rather than *orders*: `2^n · n` local facts instead of `n!` global ones.

| Level | Checks | Range |
|---|---|---|
| Exhaustive agreement | DP == Run 4's certified winner, full breakdown | n ≤ 6 — the base case |
| Bellman locality | every transition's charge equals the lowered difference | every n |
| F5-a / F5-c / monotonicity | state is a subset function; charges non-negative | every n |
| Adversarial sampling | sample admissible complete orders, cost each, none beats the DP | to the statement limit |
| Metamorphic | invariant under discovery-order permutation and relabelling | every n |
| Hazard conservatism | every body routed to the DP satisfies Run 2's empty-difference property | every n |
| **Generated-rule inheritance** | adornment, semi-naive and DNF introduce no new hazard | every generated program |

Sampling is a necessary condition, not a proof, and is labelled so.

**Ladder:** `DynamicProgram` (hazard-free, within DP budgets) → `Exhaustive` (hazardous or over a
DP budget, within candidate budgets) → `Greedy*`. Crossing a budget discards the whole table and
re-enters one rung down from the empty order; no partial hybrid. **The routing property is the
strongest guard in the run:** where both rungs are reachable they return the identical plan,
fingerprint and cost breakdown — which is why Run 4's enumeration is retained permanently rather
than deleted once the DP works.

### Runs 6, 7, 8 — statistics

Unchanged from revision 9 in substance: **Run 6** initialises a count slot from the embedded
schema for every non-virtual predicate — the correction `identity::compute`'s existing walk needs,
since `for predicate in db.predicate_ids()` (`identity.rs:111`) yields only predicates with trees
and "no facts" must be distinguishable from "no statistics" — and persists them in `record`'s
single sidecar write, with `Meta` gaining `predicates: Option<Box<[PredicateCount]>>` and no
version bump (`meta.rs:17-26`). **Run 7** adds `fjord.db.Stat`, generalises the listing digest to
a framed `(predicate_id, table_digest)` sequence over every stable virtual predicate a plan reads,
and moves complete resume preflight ahead of `ROW_DESCRIPTION` (`session.rs:1145`). **Run 8** feeds
exact counts to planning: `Some(valid counts)` on a Complete database selects exact-base mode,
`None` selects nominal, Writable selects nominal until sealing publishes its `Completion`.

Three recursion-facing amendments:

- **Statistics is the fifth input** to executable selection, stated in Movement 4's own language
  (change G). No statistics digest goes in the world stamp — not because resume would be unsafe,
  but because the order is *already* covered by the program fingerprint, making a digest
  redundant rather than merely undesirable.
- **Exact counts never cover derived predicates.** Item 4 makes a local relation query-local and
  never stored, so the accumulated, delta and magic tiers of C2 stay nominal in every mode. Run 8
  asserts this rather than leaving it to be discovered: an exact-count corpus entry over a
  recursive program must show base predicates moving to exact and derived ones staying on their
  declared tier.
- **Mode agreement under statistics.** For every exact-count entry, the DP and enumeration rungs
  still agree wherever both are reachable. Estimates change values, not the recurrence — and this
  is where that is checked rather than assumed.

---

## Interleaving with the recursion movements

Revision 9 assumed this work lands into a static compiler. It does not: recursion is the
scheduled work, Movement 1 is next and unblocked, and every flatten-adjacent phase of it touches
the files these runs rewrite. The interleaving below is a recommendation with reasons, not a
schedule.

| Cost run | Lands | Why there |
|---|---|---|
| **Run 0** — virtual ids | **now**, independent of everything | A shipped defect on `fjord.db.List` today. Item 12 makes it worse under recursion, and Movement 7's Stat work depends on it. Touches `fjord-server::catalogue` and nothing the recursion compiler work touches. |
| **Run 1** — admissible predicate | **before Movement 6**, ideally before Movement 3 | Small, self-contained, and a *contribution*: it makes the SIPS non-degenerate for the folded-constant class at zero cost to the phase order (change B). Also lands the C8 flatness assertion where a future nested group would break the DP. |
| **Run 3's collect/emit split** | **inside Movement 1 or 3**, as item 9 step 3 | The same refactor (C6). `PLAN.md:1653-1657` needs collection runnable over an arbitrary rule body independent of emission; `EmissionSeed` is that. One piece of work, or two plus a merge in `flatten.rs`. |
| **Run 3's annotated body and cost trace** | after `Program` exists (Movement 2) | The trace must describe a rule's plan, and rules do not exist until Movement 2. |
| **Run 2** — cost model and oracle | after Movement 2, before Movement 3 | The predicate-class tiers (C2) need `Program` to have predicate classes at all, and Movement 3's delta variants are the first thing that would be mis-ordered without them. Its DNF cross-check against item 10's `2^(d-k-1)` arithmetic is available as soon as `dnf.rs` has a consumer. |
| **Run 4** — enumeration | after Movement 3 | Needs delta variants to exist to have anything recursive to order, and its per-program compile bound needs a program shape to be charged over. |
| **Run 5** — the DP | after Movement 6 | The generated-rule inheritance property needs adornment and semi-naive to exist to be asserted over. And C3 says this is where compile time starts to hurt, which is the movement that produces the rule count. |
| **Runs 6–8** — statistics | independent; any time after Run 2 | Nothing in them touches the recursion compiler. Run 8's fifth-input statement should land with or after Movement 4, whose criterion it amends. |

**The one thing that would change this ordering** is Run 2's amendment pricing. If the
non-retroactive lowering turns out cheap, Run 5 collapses to a DP with no routing and could move
much earlier — plausibly before Movement 6, which would put the compile-cost relief in place
*before* the movement that multiplies the rule count rather than after it. That is a strong
reason to run the pricing early even if the rest of Run 2 waits.

---

## What this revision does not prove

Written for the adversarial pass, so a reviewer spends their time on things this document has
not already conceded.

1. **The magic-literal pin is a design decision, not a proved-minimal one.** It resolves C1 and
   preserves Movement 6's store-spy guard, but nothing here proves it is the *weakest* constraint
   that does so. A cost model that knew `magic_p^a` was small might place it first unaided — the
   tier table of C2 is precisely such a model, so the pin may be redundant once tiers exist. It is
   kept because a guard that depends on the optimiser agreeing with a nominal constant is weaker
   than one that depends on a structural constraint, and because C2's tiers are declared
   assumptions rather than measurements.
2. **F5-a and F5-c are argued in prose and gated by property, not proved on paper.** A
   counterexample would invalidate Run 5 entirely rather than degrade it. This remains the
   highest-risk claim in the plan, and it is cheap to falsify early — the property can be written
   against Run 3's emitter before any DP exists, and should be, as a spike.
3. **The amendment taxonomy's exhaustiveness rests on a call-site guard** over
   `Body::level_mut`. That catches a seventh direct call site; it does not catch a future helper
   that takes `&mut Body` and mutates through some new accessor. The classification property
   carries the weight.
4. **The generated-rule inheritance claim is asserted over three generators** — adornment,
   semi-naive, DNF — none of which exists yet. If a fourth generator is added later, the claim
   silently narrows. It should be stated as a structural obligation on *any* rule generator, and
   this document does not yet say where that obligation is recorded.
5. **Every budget value here is a proposal.** `MAX_PROGRAM_LOWERINGS` does not even have one,
   because it cannot be guessed before there is a program to measure. The DP's usable statement
   range is a *consequence* of pinning them, not an input.
6. **The corpus is now three-dimensional** — selection mode × statistics mode × hazard class,
   times a program tier — and no budget is stated for how many entries the census demands or how
   long the gate runs. A reviewer should push here: an unaffordable gate gets weakened later under
   delivery pressure, which is how acceptance criteria rot.
7. **C4's store weight is declared at 1 and left there.** Until Movement 1's benchmark pins it,
   the cost model demonstrably mis-prices relation-backed access, and the plan ships knowing it.
   That is a stated limitation rather than a solved problem.
8. **The cost-based SIPS is deferred without a design.** "Cost over `Deps` and nominal statistics
   alone, then prove the `b`-position refinement property" is a sketch, not a plan, and the
   refinement property has not been stated precisely enough to be attacked.
9. **A documentation inconsistency is unresolved.** `chase`'s doc comment offers
   `D = src.Decl {kind = "class"}` as the shared-register example, but condition 1
   (`gives_no_constant`, `flatten.rs:1666-1676`) rejects any key giving a constant, so that pattern
   is not chasable — the amendment arises from already-bound *variables* becoming residuals.
   It matters because it describes how often the hazard fires.
10. **This review read `PLAN.md:885-3122` and the adversarial review's SIPS finding, not the
    seven review rounds in full.** Movements 1–5 and 7–8 were read for cost couplings; a coupling
    living in an acceptance criterion I read past is possible.

## What the optimiser cannot fix

- A row bind that claims its variable (`flatten::Claims`, `flatten.rs:351`, decided at
  `:1195-1240`) prevents another occurrence from being a capture; ordering cannot invent a missing
  access path. `D = src.Decl _; src.SearchByName {to = D, name = N}` is the 30s-to-2ms case at
  `fjord-viewer/src/query.rs:236-238`. Note the interaction with chasing: the chasable flag is
  what *releases* the claim and makes the good order legal, so removing chasing to simplify the DP
  would move cases into this section rather than out of it.
- A join whose inner side cannot seek on the schema's declared leading fields is a key-order
  problem. FINDINGS §2's case is already fixed at the schema, so §2 is not available as the
  witness; the corpus carries the key-order case as an **unchanged-plan negative control**.
- **Demand that magic cannot seed.** A use site binding nothing evaluates unseeded
  (`PLAN.md:3007`), and no ordering makes an unseeded closure cheap. The cost corpus carries this
  as a negative control too, so the optimiser is not credited with — or blamed for — what the
  transformation decides.

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

| Run | Focus |
|---|---|
| 0 | catalogue permutation, forced legacy/new resume, virtual fetch |
| 1 | admissible/`safe()` equivalence, strictness, coincidence, non-emptiness, seed mutations, adornment SIPS non-degeneracy, body flatness |
| 2 | cost arithmetic and monotonicity, reference evaluator over the admissible space, class tiers and store weights, amendment exhaustiveness and classification, hazard conservatism, non-retroactive pricing, DNF cross-check against item 10 |
| 3 | old/new emitter differential over every admissible order, trace/plan classifier, emission-inertness, interner stability, baseline snapshots, plan-fingerprint non-regression |
| 4 | selector properties, written-order replacements, budget boundaries including per-program, derived-tuple/canonical-id/round/attempt invariance, store-spy under every mode, compile-cost witnesses, native/wasm |
| 5 | F5-a/b/c, monotonicity, the validation ladder, routing agreement, generated-rule inheritance, ladder degradation under fault injection, DP compile-cost witnesses |
| 6 | identity/count model, structural count contract, sidecar compatibility, one-write seal |
| 7 | Stat row model, virtual-world pair property, server paging/fetch, CLI and shell |
| 8 | exact-count corpus, derived predicates stay nominal, equivalent builds, mode agreement, live finish publication, cursor precedence |

## Issue-level acceptance

Issue #18 closes only when all of the following are simultaneously true:

- Every corpus entry has reviewed baseline and selected snapshots, fingerprints, independent
  costs, ordered rows, semantic multiset, hazard classification, tier assignments and a selection
  mode; every diagnosed entry has its exact code and span.
- The census and mutation controls are green, including the admissibility, amendment, DP,
  compile-cost and **program** classes.
- **The admissible predicate is proved equal to `safe()`** on complete permutations, extended with
  the magic-literal pin, and every optimality claim is scoped to that space explicitly. No
  optimality assertion passes over an empty candidate set.
- **The amendment taxonomy is proved exhaustive**, the hazard test conservative, the `level_mut`
  call-site guard green, and **no rule generator introduces a hazard its source body lacked**.
- **Where both are reachable, the DP and the enumeration return the identical plan, fingerprint
  and cost breakdown**; Bellman locality, fetch-set determinacy, future-equivalence and
  monotonicity are green at every `n` the DP admits; adversarial sampling finds no cheaper order.
- **Reordering a body changes rows examined and nothing else** — the derived tuple set, every
  canonical id, every round count and every rule-output attempt tally are identical, asserted over
  generated programs under every search mode.
- **Movement 6's store-spy criterion holds under every search mode**, and the corpus contains the
  entry that fails it without the magic-literal pin.
- Baseline and selected plans agree on the semantic result multiset for every entry and for the
  generated model across every admissible forced order.
- `preserves_written_order` and its proptest are unchanged and green over the frozen `reorder`;
  the tie-break and greedy replacements are green over their modes; `reorder.rs:62-88` names its
  actual consumers.
- Named witnesses improve exact examined/store-operation counts with scale; the hostile nominal
  witness documents the absence of a universal runtime claim.
- **Optimised compilation stays within the stated multiple of baseline compilation per program**,
  in every mode, with the budgets and the measured per-transition constant in the audit table and
  the search budgets recorded in item 6's limit table.
- Native and wasm outputs are byte-identical for the complete nominal and exact-count corpus
  inputs, in every selection mode.
- Plan and program fingerprints move when an order moves; stale cursors refuse before a row
  descriptor; unchanged plans retain fingerprints and resume. Every plan that moves for a query
  never reordered before is a corpus entry with the movement stated.
- Exact counts are a validated pure function of sealed content; derived predicates remain on their
  declared tier in every statistics mode.
- The complete repository gate is green, and the book and `PLAN.md` state the shipped model,
  limits, fallbacks, statistics policy, search modes, predicate tiers, the store-weight limitation
  and the proof boundary exactly as the tests do.
