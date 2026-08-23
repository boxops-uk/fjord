# Adversarial review: recursion implementation plan

**Reviewed:** 2026-08-22  
**Revised after code-level rebuttal:** 2026-08-22  
**Scope:** the amended recursion section of
[PLAN.md](../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation), checked
against the current compiler, executor, store seam, invariant guards, server catalogue, paging,
and count paths.  
**Verdict:** **the architecture is sound enough to begin Movement 1.** There is no
architecture-level blocker in the relation store or dispatching overlay. The plan does have a
load-bearing compiler gap that must gate Movement 3: it needs a program of named rules over the
existing `syntax::Ast`, including the answer goal as a distinguished rule, before semi-naive and
magic rewrites can be implemented or tested honestly. Recursive materialisation also requires an
I9 amendment and guard written before that materialisation path lands.

This revision corrects two severity errors in the first review: the existing syntax tree is
already the logical rule IR, and delta identity is mechanically unobservable if flatten refuses
four identity-bearing forms for local relations. It also replaces the proposed new executable
serialization with the repository's existing fingerprint-walk precedent, and records four gaps
the first review missed.

## Findings

| Priority | Finding | Required point of closure |
|---|---|---|
| Gate | Named logical rules and the distinguished answer goal are missing, although the rule IR and SIPS seam already exist | Before Movement 3 |
| High | Local row identity must be refused inside compilation, not merely stopped at the wire | Movement 0 item 3 |
| High | Magic demand derived from runtime answer bindings needs an explicit producer | Fold into the logical-program gate |
| Medium | Execution tags need one bound over the augmented catalogue count | Movement 0 / Movement 1 |
| Medium | SCC rounds should be simultaneous and non-selected recursive occurrences need a named snapshot | Movement 0 item 5b |
| High | Termination safety needs a small, decidable computed-value provenance rule | Before the naive recursive driver accepts source programs |
| High | The program fingerprint walk is missing recursion-specific semantic fields | Movement 4 |
| Gate | I9 must name recursive materialisation as an escape boundary and guard duplicate-heavy work | Before recursive materialisation lands |
| High | Limits must charge peak live representation and be enforced during generation | Movements 0–2 and 6 |
| Cost | Overlay execution adds executor monomorphisations, including to the WASM build | Cost table before implementation |
| High | The proposed I8 guard cannot observe a leaked derived-relation snapshot | Movement 4 |
| High | Existing virtual rows already disprove the claim that ephemeral ids never escape | Movement 0 item 3, with a decision covering both features |
| Medium | Recursive count is named as a consumer but has no acceptance criterion | Movement 8 |

## 1. Gate before Movement 3: a program of existing AST rules

The original review was right about the phase mismatch and wrong to call for a second query IR.
`syntax::Ast` already retains the head, ordered body, variables, fact applications, negation, and
source spans needed by a clause rewrite ([syntax.rs](../crates/fjord-engine/src/syntax.rs)). The
missing shape is a program containing several named `Query<NodeId>` rules over that syntax store,
plus a distinguished answer goal.

The SIPS seam also already exists. `flatten::Collected` is built before order is chosen and holds
parallel statements, `Deps`, and head reads; `Deps` is symbol-level captures and reads with no
`Plan` dependency ([flatten.rs](../crates/fjord-engine/src/flatten.rs),
[reorder.rs](../crates/fjord-engine/src/reorder.rs)). The required refactor is to make collection
runnable for an arbitrary rule body independently of plan emission. Adornment can then use the
collected statements plus the frontier order without reconstructing anything from `Plan`.

The phase order should be:

1. Parse and lower declarations into a program of named rules over the existing syntax tree.
2. Resolve and typecheck the program.
3. Run `collect` per rule to obtain logical statements and symbol dependencies.
4. Validate recursive safety and stratify the source program.
5. Generate magic rules and select transformed or unmagicked fallback.
6. Generate semi-naive variants.
7. Emit an ordinary `Plan` for each executable rule.

This gates Movement 3, not Movement 1. The relation store and overlay depend on predicate routing
and owned-scan representation, neither of which requires clause rewriting. Movement 1 can proceed
while this compiler seam is specified.

## 2. Local identity: refuse the four observable forms

The first review correctly found that accumulated and delta copies of one tuple normally receive
different predicate-tagged `FactId`s. It over-severed the issue because a local row's own identity
is unnecessary for the intended recursion feature and can be made mechanically unobservable.

Flatten must refuse:

- `Project::FactRef(address)` when `address` is bound from a local relation;
- `SeekKeyPart::RegisterFactId(address)` for such an address;
- `ResidualOp::EqRegisterFactId(address)` for such an address; and
- `Source::Fetch` when its declared target predicate is local.

A fetch *through a field of* a local row remains legal when that field's declared referent is a
base predicate. That is the worked `Reach` case: its tuple stores `src.Decl` ids as values, while
the identity of the `Reach` row itself is never observed.

With this rule, delta may remain a second local relation with its own ids. The semi-naive oracle
owes refusal tests for all four forms, not an identity-equivalence property. “Internal-only at the
wire boundary” in Movement 0 item 3 should be replaced with this flatten-level rule because a
local identity can affect another derived key long before a final row reaches the wire.

## 3. Magic demand needs an answer-goal producer

The worked query obtains `Seed` by executing `src.SearchByName`; it is not a compile-time magic
seed. Since strata are derived before the answer plan streams, a program without a distinguished
answer rule has no producer for `magic_Reach^bf(Seed)`.

The answer goal should therefore participate in the logical program. The first implementation can
generate a non-recursive rule equivalent to:

```text
magic_Reach^bf(Seed) :- src.SearchByName("encode", Seed)
```

and place it in `Stratum::Once`. The final answer plan may re-run the base prefix; retaining the
prefix as a supplementary relation is an optimization and should not enlarge the first cut.

Acceptance needs a store-spy guard where the seed comes from a multi-level base join and unrelated
graph components dominate the database. Result equality on a small fixture is insufficient: an
unseeded full closure can return the right rows.

This is a consequence of Finding 1 and belongs in the same Movement 3 gate, not as a separate
representation gate.

## 4. Execution tags: reuse the shipped dispatch mechanism, add the bound

Runtime routing is not an open design question. `fjord-server::catalogue::Catalogued` already
implements the intended mechanism: scan dispatches on the key's predicate prefix, point dispatches
on `id.predicate()`, and virtual rows receive ordinary predicate-tagged `FactId`s
([catalogue.rs](../crates/fjord-server/src/catalogue.rs)).

The recursion-specific obligation is the boundary of that mechanism. Virtual predicates are
appended to the schema visible to server compilation, so local execution tags must begin above the
**augmented** predicate count, not the database schema's stored predicate count. Before building
the executable:

```text
augmented_predicate_count + generated_local_count <= MAX_TAGGABLE_PREDICATE + 1
```

must hold, with overflow-safe arithmetic and a named diagnostic. Allocation is deterministic and
dense within the query. Guards should cover a server catalogue with virtual predicates, the exact
last usable tag, and one-past exhaustion. The cost is a bound and tests, not a new routing design.

## 5. Simultaneous SCC rounds: required for stable meaning, not least-fixpoint reachability

Per-rule freezing under a fixed rule order can still reach the least fixpoint for positive
Datalog. The reason to require simultaneous rounds is that the project gives rounds meaning:

- the deferred closure operator defines minimum BFS depth as the round of first derivation;
- work limits and profile counts should not change when source declarations are permuted; and
- a naive/semi-naive comparison at round granularity needs both evaluators to mean the same thing
  by a round.

Movement 0 item 5b should state the transition precisely:

1. Every rule in an SCC reads the same accumulated snapshot `A_r` and delta snapshot `Delta_r`.
2. A rule with `k` recursive occurrences produces one delta variant per occurrence.
3. The selected occurrence reads `Delta_r`; every non-selected recursive occurrence reads `A_r`.
4. Candidate tuples for every predicate are streamed into a shared next-delta deduplicator.
5. `Delta_(r+1) = candidates - A_r`, made visible to all rules only at the next round.
6. The SCC converges when every predicate's next delta is empty.

The focused guards are multiple recursive occurrences, mutual recursion, duplicate clauses, and
declaration-order permutation. No structural change to `Fixpoint { seed, step }` is required.

## 6. Termination safety: computed-value provenance, not a general domain analysis

The plan's “finite domain drawn from the base” is not a checkable contract, but the current
language does not need a general finite-domain analysis. Recursive value invention enters through
computed head leaves. Fixed signature shapes prevent record construction from growing in depth.

The static rule can be:

> A head leaf of a predicate in a recursive SCC may not be a `Project::Computed` whose transitive
> inputs include a variable bound by a recursive occurrence in that SCC.

Base-only computations and literals remain legal. The rule must walk through nested
`Project::Record` values and through `Computed::Register` chains. It rejects the hand-written
`depth : int` recurrence the deferred closure section already identifies as a trap, without
rejecting a finite computation over a base-bound value.

The local-reference-cycle analysis proposed by the first review is unnecessary once Finding 2
forbids local fact identity as a value. Acceptance requires the named diagnostic, a recursive
arithmetic case, a transitive computed-register case, and positive controls for literals and
base-only arithmetic.

## 7. Fingerprint: extend the walk and its mutation table

The coverage gap remains, but a canonical executable serialization is unnecessary. The repository
already has the correct guard shape: the hand-written plan walk is paired with
`every_part_of_a_plan_reaches_its_fingerprint`, a table of single-element mutations that must all
produce distinct fingerprints ([plan.rs](../crates/fjord-engine/src/plan.rs)).

Extend that walk and table to cover:

- relation declarations and physical field order;
- materialisation projections;
- stratum kind and order;
- rule target and order;
- deterministic execution-tag allocation;
- generated magic, supplementary, accumulated, and delta relations;
- the selected magic or unmagicked-fallback executable; and
- semantic limit values, while leaving deployment policy limits out.

The transformed-versus-fallback choice is especially load-bearing because one source program can
otherwise produce two cursor-incompatible executables. Rebuilding in a fresh process should yield
the same fingerprint, but no new serialized format is needed.

## 8. I9: recursive materialisation is a new escape boundary

The plan cannot preserve I9 by declaring fixpoint work outside the measured hot path. A rule's
materialisation callback runs per output attempt and must encode, deduplicate, and sometimes retain
the tuple. A duplicate-heavy join can allocate per attempt while the current scan-only guard stays
green.

The invariant registry should name retained derived tuples as a third escape boundary beside
suspend and string/bytes projection. Its guard should require reusable encoding scratch and no heap
allocation proportional to rejected or duplicate attempts; allocation may scale with bytes
actually retained under the memory budget.

The existing I9 caveat also becomes more important: the guard covers a single-level plan, while
opening a join level can allocate once per outer row. A fixpoint opens levels per rule per round,
multiplying that uncovered case. Add guards for:

- N versus 2N duplicate output attempts with constant retained tuples;
- N versus 2N distinct retained tuples, charging the expected retained bytes; and
- repeated level opening across rounds, with a positive allocator control.

The property and ignored guard belong up front, before recursive materialisation is implemented.

## 9. Limits must charge peak live representation

Fact and payload counts do not necessarily bound the simultaneously live representation:
accumulated and delta indexes, `Arc` snapshot arrays, candidate-dedup state, magic and
supplementary relations, and index metadata all contribute. Generated-program limits checked only
after adornment also do not prevent adornment itself from exhausting memory.

Required amendments:

- Charge peak live representation, or state and mechanically guard a strict multiplier from
  logical payload to peak bytes.
- Enforce generated relation/rule limits incrementally before each allocation.
- Stream candidates through deduplication and limit checks. An unbounded per-rule candidate buffer
  is both an availability hole and the materialised-result-set anti-pattern under another name.
- Test a tiny fixed point with a huge duplicate stream and exact-boundary rewrite growth.

## 10. Missing cost: executor monomorphisation

`Executor<S: FactStore>` is monomorphised ([iter.rs](../crates/fjord-engine/src/iter.rs)). The server
already instantiates it for the stored reader and for `Catalogued<Reader>`. Recursion adds overlay
forms for each store shape, and the browser adds the `MemStore` form. That increases compile time,
native code size, and the WASM bundle—the last one a stated product constraint.

This does not argue for dynamic dispatch; the existing code deliberately avoids a per-row virtual
call and allocation. It belongs in the cost table with a mechanical before/after size measurement
for the WASM module and, if material, release binaries.

## 11. I8 needs a relation-snapshot witness

Movement 4 proposes checking recursive I8 against fjall's open-snapshot count. That witness can
prove the base reader and its scans were dropped, but a derived relation snapshot is an engine
`Arc` with no fjall counterpart. The test can pass while the suspended program retains every
derived tuple.

Keep the fjall count for the base half and add a drop/liveness probe around the relation snapshot,
following the existing `DropProbe` pattern in `fjord_store::fixtures`
([fixtures.rs](../crates/fjord-store/src/fixtures.rs)). Assert both witnesses reach zero after:

- answer-page suspension;
- cancellation during a fixpoint;
- a materialisation or limit error; and
- normal completion.

Include positive controls showing both the fjall snapshot and relation snapshot are live during
execution. The two probes establish different halves of I8 and neither substitutes for the other.

## 12. Existing precedent: ephemeral virtual ids already escape

Movement 0 item 3 argues that a query-local id must not reach the wire because it has no durable
meaning and can alias another query. That is not a new state: `Catalogued` assigns virtual rows
ordinary `FactId`s, a whole-row `Project::FactRef` is accepted, and server row conversion writes
the id directly to the wire ([catalogue.rs](../crates/fjord-server/src/catalogue.rs),
[rows.rs](../crates/fjord-server/src/rows.rs)). The client expander keeps a cache keyed only by
`FactId`, including across pages ([expand.rs](../crates/fjord-client/src/expand.rs)).

This does not justify exposing recursive local ids—Finding 2 gives a cheap reason not to. It means
the rationale and guard must acknowledge the existing virtual-id contract. Movement 0 should
decide one of:

- virtual ids have a documented query/session lifetime and every cache is scoped or cleared to
  that lifetime; or
- the existing path is a latent identity-scope hole, fixed independently before its precedent is
  cited for recursion.

Add a test in which the virtual catalogue changes between requests while a client-side expander
exists, so aliasing or deliberate cache invalidation is observed rather than reasoned about.

## 13. Recursive count needs an acceptance criterion

The server's count path runs `Executor` in `CHUNK_ROWS` chunks and resumes between them
([session.rs](../crates/fjord-server/src/session.rs)). A recursive executable that re-derives on
every chunk therefore pays the complete fixpoint repeatedly merely to produce one number. Movement
8 names count as a consumer but proves nothing about its dispatch, correctness, cancellation,
resource charging, or repeated work.

Add acceptance criteria that:

- recursive count equals the cardinality of full enumeration;
- each count chunk validates the program fingerprint and charges/reports its re-derivation work;
- cancellation and every limit release both base and relation snapshots; and
- a multi-chunk count has a guard documenting the repeated-fixpoint cost, so a later optimization
  cannot silently change resume or accounting semantics.

## Revised movement recommendation

Movement 1 is unblocked and should start once its existing predicate-count and owned-scan decisions
are written. It is useful independent work and settles representation questions later movements
need.

Before Movement 3, add one logical-program item to Movement 0: a program of named rules over the
existing `syntax::Ast`, the answer goal as a distinguished rule, and `collect` callable per rule
before plan emission. Fold magic-seed production into that item. Also sharpen item 3 to the four
flatten refusals, item 5b to simultaneous SCC transitions, and item 6 to the computed-value rule.

Before recursive materialisation lands, amend I9 and write its allocation guard. Extend Movement
4 with the relation-snapshot drop probe and the existing fingerprint walk. Extend Movement 8 with
the count criteria. Record monomorphisation in the cost table, and resolve virtual-id lifetime as
an existing contract question rather than pretending recursion creates it.

The executor boundary, `Program` runtime shape, overlay dispatch, and bytes-only cursor all survive
this review. The necessary changes are compiler plumbing, sharper static refusals, and guards at
the actual new resource and lifetime boundaries—not a redesign of Movement 1.
