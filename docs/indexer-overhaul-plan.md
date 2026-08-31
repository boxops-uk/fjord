# The indexer overhaul — write path, seam, discrimination, and symbol identity (revision 1)

## What this plan is

Five issues (#28–#32) are open against `clients/dotnet`. This plan reads them together with
four decisions taken in discussion — that the .NET indexer stays **supported** as an
end-to-end example, that it grows a **seam** for swapping fact emission, that the producer
should stop carrying **conflict bookkeeping**, and that **SCIP** becomes the cross-language
symbol identity — and sequences the whole of it as one route.

The finding that reorders everything: **two of the five issues invalidate published
measurements**, including the Fjord-versus-Glean comparison in `bench/FINDINGS.md` §15. That
is not a side effect to clean up afterwards. It decides what must be measured before anything
is claimed, and it is why the harness is Run 0 rather than an afterthought.

---

## What was read, and what it found

Read in full: issues #28–#32; `clients/dotnet/Boxops.Fjord.Indexer/` (3,653 LOC across nine
files) and `Boxops.Fjord.Client/` (1,236 LOC); `schemas/code.sigla`; `bench/FINDINGS.md`
§§12, 14, 15, 15a–c; `docs/glean.md` §1 and §2; `docs/gitnexus.md` §§16–17 and the finding
table; `website/content/schema-language.md` §Namespaces and imports; `PLAN.md:3215-3240`;
`.github/workflows/release.yml`.

| # | Coupling | Direction | Where |
|---|---|---|---|
| C1 | **The two write-path ceilings mask each other.** The gate throttles fact production below one writer's capacity, so `queueing` cannot be large, so "the writer was not the ceiling" is true by construction rather than by measurement. | Conflict — resolved by sequencing | #30, #31; `FINDINGS` §14 |
| C2 | **§15's core claim is contaminated.** "One walk, two sinks, so what differs is the database and not the indexer" requires the shared producer not to be a binding constraint. Its own table shows `gate wait` differing **8.2×** between the runs compared. | Published result must be re-run | `FINDINGS` §15, `gate wait` row |
| C3 | **The conflict rule is the one thing de-gating can change observably** — and a semantic `Decl` key removes the hazard rather than making it thread-safe. | The schema fix strictly de-risks the concurrency fix | #30 footgun 2 |
| C4 | **The five issues fall into four disjoint compartments** and none crosses the MSBuild ↔ fact-emission line. The seam is where the bugs already are. | Validates the seam's location | #28/#29 → `Loader`; #32 → `Projects`; #30 → `Indexer`; #31 → `FactSink` |
| C5 | **`ops-I5` is Glean's own rule, adopted** — Glean disables it on its batch paths (`ignoreRedef`, *"silently picking one of the two facts… That's bad"*). So conflict care is not a Fjord tax; it is a correctness requirement Fjord reports and Glean swallows. | Places the rule sink-side, above both targets | `glean.md:54-59`, `:115` |
| C6 | **The repo already specified SCIP without naming it** — "a content-derived string identity with the repo as its first token, reversible by searching rather than by dereferencing". | Adopt the standard into a designed slot | `gitnexus.md` §16/17 |
| C7 | **Schema imports already exist** and resolve by namespace under a schema path. The "importable default schema" half needs no engine work. | Cheap; land early | `schema-language.md:185-224` |
| C8 | **A fact file is portable by predicate *name*** — "the wire carries names (once per block), so the numbering never leaves the database and a fact file is portable to any database declaring those names." | File ingestion is a read side, not a format negotiation | `PLAN.md:3225` |
| C9 | **The producer already holds no ids** — that is settled and needs no work. What it still holds is *conflict* bookkeeping, which exists only because the key under-discriminates. | Narrows "remove dedup tracking" to one real change | `PLAN.md:3229-3232` |

---

## C2 first, because it is the one that changes what may be published

`FINDINGS` §15 compares Fjord and Glean over one corpus, and rests its method on one sentence:

> One walk, two sinks, so what differs between the last two rows is the database and not the
> indexer.

Its own table carries this row:

```
gate wait / held | — | 7,608 / 1,282 s | 3,256 / 644 s | 926 / 284 s
                      1 writer          4 writers       Glean
```

The producer's gate wait differs by **8.2×** across the runs being compared. If the walk were
genuinely identical across sinks, that number would be comparable. It is not — because
`sink.Add` is called with the gate held (`FactSink.cs:180`, reached under `Indexer._gate`,
`Indexer.cs:97`) and its flush blocks under it. Each sink's speed feeds back into the shared
producer.

**So the two sinks perturbed the thing that was supposed to be constant, by different amounts,
and the evidence was in a published row.** §15's headline is not safe to cite until it is
re-run on an un-gated walk.

Two riders:

- **Every published indexer number is `--syntax-only`** — the mode where 3.13M names go
  unresolved and `Describe` therefore does the least symbol work. #30's ceiling is worst in the
  mode nobody benchmarked, which is also why measured `gate held` (1,282 s of 3,977 s, 32%) is
  milder than #30's predicted "≈ wall clock".
- **§15c already concluded the writer count "wants to be adaptive rather than a flag."** #31
  proposes a measured constant, `min(8, ProcessorCount)`. Those are different answers and one
  must be chosen (see Open decisions).

---

## The four theses

**1. The write path has two ceilings, and they must be lifted in order.** The gate
(`Indexer.cs:97`) is held across `Describe`'s Roslyn symbol queries — the expensive half — so
`--jobs N` parallelises the cheap half and serialises the costly one. Until it is gone, the
writer count cannot be measured, because production is throttled below one writer's capacity.
#30 then #31, never the reverse.

**2. The seam is where the bugs already are.** #28/#29 are `Loader` (MSBuild), #32 is
`Projects` (build facts), #30 is `Indexer` (the walk), #31 is `FactSink` (the sink). Four
compartments, none crossing the MSBuild ↔ fact-emission line. The seam does not have to be
argued for; it has to be written down.

**3. Discrimination, not bookkeeping.** The producer holds no ids already (`PLAN.md:3229`).
What it still holds is `_kinds`/`Declared.First` — conflict resolution that exists only because
`src.Decl`'s key `{module, name, line}` cannot tell two overloads on one line apart. Make the
key **semantic** — a descriptor rather than a position — and the conflict half disappears
because collisions become impossible rather than counted; what remains is caching, which can be
made concurrent without correctness risk. **This is also the cheapest de-risking available for
#30**, whose worst footgun is that de-gating makes the conflict winner racy. It fixes a second
defect in the same move: `line` is in the key today, so reformatting a file currently changes
the identity of every declaration in it. With decision 5 pinning the target as well, every
conflict source is eliminated at its source and the bookkeeping is **deleted rather than
narrowed**.

**4. SCIP fills a slot the repo already specified.** `gitnexus.md` says cross-repo links want
"a content-derived string identity with the repo as its first token — reversible by searching
rather than by dereferencing", and that "none of it is built; all of it is designed." SCIP is
that, standardised. Schema imports (C7) make the shipped schema a file rather than a feature,
and fact-file portability by name (C8) means an off-the-shelf SCIP indexer's output works
against any database declaring the namespace.

---

## The instrument that does not exist, and every issue asks for it

Four of the five issues name the same missing acceptance test:

| Issue | What it asks for |
|---|---|
| #29 | "Index the same fixture solution twice at high `--jobs`; assert identical project counts *and* identical per-predicate fact totals." |
| #30 | "Index one corpus at `--jobs 1` and at `--jobs 32`; assert identical *per-predicate* fact counts." |
| #31 | "Assert writer-count invariance of output: fact totals at 1 writer and 8 writers must be identical." |
| #32 | "Every design-time build result that produced a usable compilation has a corresponding `Project` fact." — one count-vs-count comparison. |

**This is one artifact, and it is Run 0.** It is not refactor overhead borrowed against the
issues; it is the issues' own stated gate. It also happens to be the characterization test the
seam refactor needs, and the CI smoke check the release artifact needs. It pays three times.

**The fixture is this repository's own `clients/dotnet`** — already the corpus §15's
cross-check used (20 files, 14,040 facts), small enough for CI, and real code rather than a
constructed tree.

---

## The runs

Each run has one falsifiable claim and a mechanical gate. Runs 1–3 touch no schema and no
concurrency; Run 4 moves the schema; Run 5 removes the gate; Run 6 tunes what Run 5 exposed.

### Run 0 — the fixture ledger

**Claim.** A fixed corpus, indexed at varying `--jobs` and `--writers`, produces identical
per-predicate fact counts, and that is asserted in CI.

**Work.** A script that indexes `clients/dotnet` against a throwaway server, captures the
per-predicate table the run already prints (`Program.Report`), and diffs it against a checked-in
golden. Assert: per-predicate counts, `Conflicts`, the project count, and the "no project
compiles" count. Add to the `test` job.

**The fixture already multi-targets**, which is convenient: `Boxops.Fjord.Client` is
`net8.0;net10.0`, so a default run fans out to two databases and the ledger exercises decision 5's
default path for free. **Counts are therefore asserted per database, not per run** — a ledger that
summed across the fan-out could not see one target losing facts while another gained them. CI
passes `--strict`.

**Gate.** The ledger is green at `--jobs 1` and `--jobs 8`, and at `--writers 1` and 4, and the
per-database counts are identical across both.

**Note.** `--dry-run` covers the encode path without a server if the CI server proves awkward;
prefer a real server so `created`/`deduped` are exercised.

### Run 1 — #28, workspace load without the second build

**Claim.** Adding built results with `addProjectReferences: false` and wiring the reference
graph by `ProjectId` in a second pass produces the same cross-project symbol resolution with
no MSBuild process spawned during workspace load.

**Work.** As #28's sketch. Delete `Holds()` and the `ArgumentException` re-entry path — #28
notes they can now only miscount.

**Gate.** No process creation during the workspace-load phase; a fixture with `A → B → C`
(C outside the indexed set) resolves A→B to source and C to metadata; ledger unchanged.

### Run 2 — #29, retry the transient case only

**Claim.** Distinguishing *a throw* (pipe race, retryable) from *a clean return with no usable
result* (deterministic) makes project-set completeness independent of `--jobs`.

**Work.** As #29. Retry the existing two-attempt pair as a unit, three attempts, escalating
back-off. Do not log inside `Attempt`. Report `built N, failed M, retried K`.

**Gate.** The same fixture indexed twice at high `--jobs` gives identical project counts and
per-predicate totals; CI fails if `M > 0` on the known-good fixture.

**Ordering.** After Run 1, which deletes the code a retry loop would otherwise have to preserve.

### Run 3 — #32, a project that built belongs in the graph

**Claim.** Creating the `ProjectInfo` in `Refine` when the path resolves but the glob missed it
recovers the project, its source attribution and its reference edges, without creating
duplicates.

**Work.** As #32. Keep the early return for `Paths.Relative` returning null — genuinely outside
the index root. Keep path spelling identical to `Discover`'s.

**Gate.** Fixture with a solution referencing a project outside the globbed subtree: assert the
`Project` fact exists, its sources are attributed, the reference edge is emitted, and the "no
project compiles" count is 0 on a fixture where every file belongs to one.

### Run 4 — discrimination: identity becomes semantic, and `scip` arrives

**Claim.** A `src.Decl` key identifies a declaration by *what it is* rather than by *where it
sits*, so two declarations cannot collide, `_kinds` loses its conflict role, and reformatting a
file does not change what its declarations are.

This run is two changes that share one fingerprint move, and therefore one re-measurement.

**The objection that shaped it.** An earlier revision of this plan added `col` to the key. That
is syntax standing in for semantics, and it is fragile in a way that matters: a formatter moving
two overloads onto separate lines would change both their identities and every reference to
them. **The key already has this defect** — `line` is in it, so inserting a blank line at the
top of a file changes the identity of every declaration below. `col` would not have introduced
positional fragility; it would have made an existing one finer-grained and far likelier to trip.
The fix is to take position *out*, not to add more of it.

**4a — the key.**

```schema
predicate Decl     : { module : Module, descriptor : string } -> string
predicate DeclSpan : { decl : Decl, line : int, col : int, endLine : int, endCol : int }
```

`name` and `line` both leave the key. The descriptor already carries the qualified name, and the
line was only ever there to tell two declarations apart — which is now the descriptor's job.
Position becomes wholly an attribute of a declaration, which is what `src.DeclSpan` is for.
Roughly size-neutral: one longer string in, one string and one int out. **Collisions become
impossible by construction rather than by counting.**

`schemas/code.sigla`'s existing warning is right and now applies in the other direction:

> widening an identity to carry a **rendering detail** is how a schema acquires fields nobody
> can explain later.

A line number is a rendering detail. It was in the identity because nothing better was
available; the descriptor is what was missing.

**What the descriptor is.** A SCIP **descriptor** — a namespace/type/member path with kind
suffixes and an overload disambiguator — *not* a full SCIP symbol. The distinction is the whole
of why this works:

```
scip-dotnet nuget System.Text.Json 8.0.0  System/Text/Json/Utf8JsonReader#Read().
└─ scheme ─┘└──── needs the build ──────┘  └────────── purely semantic ─────────┘
```

The package triple needs a resolved build; the descriptor needs a compiler. So a descriptor is
available in every mode this indexer has, and the full symbol is not. .NET has a native
equivalent — `ISymbol.GetDocumentationCommentId()`, the ECMA-spec'd `M:Foo.Bar(System.Int32)`,
one call and formatter-stable — but the schema field should mean the same thing whichever
language produced it, so the descriptor is the stored form and the doc-comment id is the
cross-check. `scip-dotnet` is a reference implementation for deriving one from Roslyn.

**4b — the symbol.** A new `schemas/scip.sigla`, importable per `schema-language.md:185`:

```schema
schema scip {
  # The full SCIP symbol string, interned once. Bare-string predicate because a
  # ~75-byte identity nested into every referencing fact is the wrong shape — the
  # pattern src.File and src.Project already use.
  predicate Symbol : string
}
```

and in `src`:

```schema
  import scip

  # Symbol-leading: "which declaration is this symbol" is the cross-database
  # question, and a leading key field is what makes it a seek. Same argument as
  # src.SearchByName.
  predicate DeclSymbol : { symbol : scip.Symbol, to : Decl }
  predicate SymbolOf   : { decl : Decl, symbol : scip.Symbol }
```

**Descriptor and symbol are not the same field, and that is the point.** The descriptor is the
*identity* and is always available; the full symbol is the *global name* and requires the
package triple, so it is a side predicate that a degraded run simply does not populate. Identity
must not depend on whether the producer could resolve packages — that is what keeps
`--syntax-only` ("no MSBuild, no NuGet, no project graph") able to produce an index at all,
which the README states is the reason that mode exists.

**A reference, not a value** — I6 means a value cannot be matched on, and joining on the symbol
is the entire point.

**Two gaps this shape has, and neither is optional.**

- **Symbols with no descriptor.** Local functions — which the indexer declares
  (`LocalFunctionStatementSyntax`) — and anything SCIP models as `local`, which is
  document-scoped rather than global. `GetDocumentationCommentId()` returns null for some of
  these. Unions landed in 8.6, so the honest shape is
  `descriptor : Global string | Local { file : File, id : string }`.
- **Unresolved parameter types — settled by hard failure.** The disambiguator carries parameter
  types. The objection was that a run which resolved a type and one which did not would produce
  *different* descriptors for one declaration, which `--skip-files` slicing cannot tolerate;
  under the governing rule (see Decisions) the second run fails instead of producing facts, so
  the divergence has no way to occur. **The cost is `--syntax-only`**, which can no longer emit
  the semantic layer at all — see Decisions for what replaces it, and for the corpus Run 7 needs
  as a consequence.

**Before this run, one cheap measurement (Run 4.0).** Make the conflict counter honest: count
every `first == false` where the two symbols differ, not only where the *kinds* differ. Today
two `method` overloads on one line collide on every value-carrying predicate and `Conflicts`
stays zero — **the counter is blind to the most common instance of the bug it exists to watch.**
A few lines, no fingerprint move. It is now a **sizing** number rather than a gate: the
disambiguator question it was going to settle is closed, so this says how much the run buys, not
whether it is safe.

**Gate.** `Conflicts` is 0 on a fixture containing two overloads on one line and a type with a
same-line constructor; reformatting that fixture (blank line inserted at the top, overloads
split across lines) produces an **identical** set of `src.Decl` keys — that is the property
`col` could not have had, and it is the acceptance test for the whole run; the ledger's
per-predicate counts move only where predicted; the golden blocks and `fjbench.angle` move
together with the fingerprint; `fjord --schema-path ./schemas schema check ./schemas/code.sigla`
resolves the imports and reports every file (`code`, `scip`, `config`).

### Run 5 — #30, remove the gate

**Claim.** With shared state made safe per-mutation rather than per-declaration, walk
throughput scales with `--jobs`, and per-predicate fact counts are unchanged.

**Work.** As #30's fix direction: symbol memo → concurrent map preserving **write-before-recurse**
(the entry is published before `Describe` walks, which is what stops a cycle in code that does
not compile from recursing until the stack runs out); counters → `Interlocked`; sink → one lock
per predicate, detaching the full batch under the lock and enqueuing **outside** it; progress
callback → its own lock.

**Do not use per-thread buffers.** They would multiply duplicate `File`/`Module` facts by thread
count and destroy §15a's load-bearing check — 25,046,499 queued, 25,012,490 stored, 34,009
duplicates, "both systems deduplicated exactly those, without either being told which." The
striped approach keeps the memo shared and mirrors the server's own per-key striping.

**Run 4 has already removed this run's worst hazard.** #30's second footgun is that de-gating
makes the conflict winner racy and non-deterministic. With a descriptor key there is no conflict
to race on at all; what remains is a cache whose entries agree.

**Keep the instrumentation.** `gate wait`/`gate held` are the only evidence the fix worked;
replace them with contended time on the striped locks rather than deleting them.

**Gate.** Ledger identical at `--jobs 1` and `--jobs 32`; walk throughput at `--jobs 8`
materially above `--jobs 1`; contention a small fraction of the run; `queueing` becomes large
— **that is the fix working**, and it is why Run 6 must follow immediately.

### Run 6 — #31, the writer default, measured on an un-gated walk

**Claim.** With the walk parallel, the writer count that minimises `queueing` without costing
throughput can be measured, and the default set from that measurement.

**Work.** Sweep `--writers` at `--jobs 32` over a light and a dense corpus. `--emit` must force
one writer (its block file is a deterministic run). Do not tie writers to `--jobs` — the doc
comment already records that coupling as the original mistake.

**Gate.** `queueing` a small fraction of the run on the CI fixture; fact totals identical at 1
and 8 writers; `--emit` byte-identical across two runs. The default's rationale carries the
corpus, the `--jobs`, and the `queueing` figures that chose it.

**Open decision.** #31 says a measured constant; §15c says adaptive. See below.

### Run 7 — re-measure, and correct the record

**Claim.** §15's comparison, re-run on an un-gated walk, supports or revises its published
conclusion — and either way the number in the tree is one that was measured under conditions
its own method requires.

**Work.** Re-run §1, §12, §15, §15a–c on the current tree. Update
`clients/dotnet/Boxops.Fjord.Indexer/README.md`'s sample output. Add a note to §15 recording
that the prior figure was producer-contaminated and by how much — the repo's convention is that
a superseded measurement says what moved it.

**The corpus definition now names a target.** A benchmark pins `--framework` rather than fanning
out — one database, one target, stated. That is a gain rather than a chore: §15's method requires
the two runs to differ only in their sink, and a corpus that says which target it indexed is one
fewer way for them to differ.

**Gate.** `gate wait` (or its replacement) is comparable across the Fjord and Glean runs, which
is the condition §15's method actually requires.

### Run 8 — the seam

**Claim.** The fact-emitting half can be replaced without touching the MSBuild half, and the
Glean target still rides the same path.

Three seams, smallest first.

**8a — schema.** `FactSink` takes an `FjordSchema` rather than reaching `CodeIndex`. Its only
couplings are `CodeIndex.Predicates.Length` (×3) and `Block.Encode(CodeIndex.Schema, …)`. The
batching, bounded queue, writer threads and latched-failure rule are hard-won and say nothing
about `src.*`. **Nearly free, and both other seams need it.** `GleanFacts` is the precedent —
its Angle names are derived from the schema, not tabulated, "so a second hand-written name table
would be a second thing to keep in step".

**8b — build facts.** `ProjectInfo` becomes pure build data; `ProjectInfo.Fact` (`Projects.cs:22`,
a `src.Project` fact built in the constructor) and `ProjectIndex.Emit` (`Projects.cs:190`) move
behind an emitter interface. **The two halves meet at one line** — `Indexer.cs:823` emits
`ProjectSourceFact(fact, project.Fact)` after `projects.Owners(path)` — so the build hook must
expose a file → project-fact lookup the walk can reach. Design it, do not discover it.

**8c — the walk.** `IIndexPass { void Visit(SemanticModel, SyntaxTree, IFactWriter) }`.

**The entity-model alternative is dropped.** It was justified by how much a raw-writer consumer
would inherit; after Run 4 and Run 5 that is almost nothing — cycle termination in their own
walk (a property of recursive symbol walks, not of Fjord) and the striped-sink contract, which
the framework owns. An entity model would bake in an opinion about what code indexing means,
which is the scope trap this whole exercise is avoiding.

**The client-side conflict rule is deleted, not relocated.** Earlier revisions moved it
sink-side; decisions 3 and 5 remove everything it had to resolve, so the correct end state is
that it does not exist. `_kinds` and `Declared.First` go.

**Why deleting beats keeping it as an escape hatch.** Suppressing a conflict is precisely the
covering-up the governing rule refuses. A same-key-different-value conflict now means a
consumer's schema under-discriminates or their target is not pinned, and `ops-I5`'s server
reject is the mechanism that tells them so — by name, deterministically. A first-wins hatch in
the framework would re-import Glean's `ignoreRedef` (C5) through the back door, into the one
system that declined it.

**What survives is about walking a graph, not about Fjord**: `_declarations` as a
per-compilation **cycle guard** — the write-before-recurse rule Run 5 must preserve — and
`_walked` as file dedup. That is the whole of the producer's remaining bookkeeping, and it
completes "the producer holds no ids and tracks no dedup" (C9).

**Gate.** Ledger unchanged across the refactor; the Glean target rides 8a unmodified; a second
schema (the SCIP-only pass of Run 9) exercises 8a and 8c end to end.

### Run 9 — SCIP as an ingestion path

**Claim.** An off-the-shelf SCIP index loads into a database declaring `schemas/scip.sigla`,
with no per-language work on our side.

**Work.** A converter: SCIP protobuf → facts, written against the Run 8 seam so it is a second
consumer of it rather than a second program. It emits **blocks** — which is what `--emit`
already writes, and per `PLAN.md:3225` a fact file is portable to any database declaring those
predicate names.

**So this run is deliberately half a feature.** File ingestion is not built (README lists it
under *Not built*; `--emit`'s format is what Phase 7b would read). The converter therefore
writes over the wire today, and becomes a file ingester for free when the read side lands — no
format negotiation, because the format is already the one the wire carries.

**Gate.** `scip-rust` (or rust-analyzer's SCIP output) over this repository, loaded, and the
viewer's `/symbol/{name}` answering against it. That is also the CI artifact from the earlier
discussion — **and it is a Rust reference index with no Rust indexer written.**

---

## Sequencing

```
Run 0  ledger ─────────────────────────────────────────────┐  gates everything
                                                            │
Run 1  #28 workspace load ──┐                               │
Run 2  #29 transient retry ─┤ no schema, no concurrency     │
Run 3  #32 project rescue ──┘                               │
                                                            │
Run 4.0 honest conflict counter  ── cheap, decides 4a       │
Run 4  semantic key (descriptor) + scip.sigla  ── moves fingerprint
                                                            │
Run 5  #30 de-gate  ── de-risked by Run 4                   │
Run 6  #31 writer default  ── blocked on Run 5              │
Run 7  re-measure §12/§15  ── one re-measurement, not two   │
                                                            │
Run 8  the seam  ── shape settled by Run 5's striping       │
Run 9  SCIP ingestion  ── second consumer of Run 8          │
```

Three orderings are load-bearing:

- **Run 1 before Run 2.** #28 deletes `Holds()` and the `ArgumentException` re-entry path that
  #29's retry restructuring would otherwise have to preserve.
- **Run 4 before Run 5.** A semantic key removes #30's worst footgun instead of requiring it to
  be made thread-safe and deterministic.
- **Run 5 before Run 6**, stated in both issues, and Run 7 after both so the fingerprint move
  and the gate removal are absorbed by **one** re-measurement.

Run 3 is independent after Run 0; it is grouped with 1–2 because its "if `Refine` is ever called
in parallel" footgun sits in their territory.

---

## What this plan does not prove

- **That §15's conclusion survives.** It establishes that the measurement is contaminated and
  must be re-run, not what the re-run will say. Glean's write path may still be cheaper.
- **That SCIP disambiguators are cross-indexer stable for overloads.** Verify against the spec
  before publishing any cross-database claim that depends on it.
- **That a descriptor is derivable in every mode.** The unresolved-parameter-type question in
  Run 4 is open, and if type-carrying disambiguators turn out to be slice-fragile, arity-only
  disambiguation is weaker than the run claims.
- **That the seam is right for a second language.** Run 9's SCIP converter is the first real
  test; a Roslyn-shaped seam that only Roslyn fits is a failure this plan cannot detect earlier.
- **Anything about cross-database *query*.** SCIP supplies a join key valid across databases;
  the engine still has none (`gitnexus` gap 3, `ops-I9` untouched, "every mechanism sits above
  the executor"). The honest claim is that this makes cross-DB tools **buildable by consumers**.
  Worth fixing the wording before it ships — `gitnexus.md` is careful about this line.

---

## Decisions — closed

All four are settled. The governing call, taken after Runs 1–3 stabilise the project layer:

> **A run that cannot resolve assemblies or types fails, loudly, rather than emitting a
> degraded fact. Correctness is the schema's business; compatibility with an awkward
> environment is the indexer's.**

**1 · Writer count — #31's measured constant.** `min(8, ProcessorCount)`, with §15c's adaptive
target recorded as the follow-on. A measured default is falsifiable in CI; a controller is not.
The default's rationale carries the corpus, the `--jobs`, and the `queueing` figures that chose
it.

**2 · Extension model — internal seam, not a package.** Types stay `internal`; no second NuGet
package. Run 9's SCIP converter is the first genuine second consumer, and publishing an API
before it exists is guessing at the shape. Revisit after Run 9. "Compatibility is the client's
problem" is consistent with a fork owning its own drift.

**3 · Overload disambiguators — type-carrying.** The objection to carrying parameter types was
entirely that a run which resolved a type and one which did not would produce different
descriptors for one declaration, which `--skip-files` slicing cannot tolerate. Under hard
failure the second run does not exist. So the descriptor carries parameter types and `F(int)`
separates from `F(string)`. **Run 4.0's counter is now a sizing number, not a gate** — it says
how much the run buys, not whether it is safe.

**4 · No first-party Rust indexer.** Folded into Run 9's gate rather than standing as a
decision: if a SCIP-ingested Rust index answers the viewer's queries, the ecosystem's indexers
plus one converter cover more languages than any first-party walk would.

**5 · Fan out by default; narrow by flag; never block.** Indexing for different runtimes or
target platforms is either **separate databases** or something a client **accounts for in its own
schema**. The reference indexer's default is the first, because it is the one that guesses
nothing:

| | Behaviour |
|---|---|
| **Default** | **Fan out** — one database per resolved target, written to `code@net9.0`, `code@net8.0`, … using the `[where//]name[@instance]` address grammar `--at` already accepts. Nothing dropped, nothing chosen on the operator's behalf |
| `--framework <tfm>` | Resolve **only** that target, into one database. What a benchmark run and a single-target consumer use |
| Per project | **Nearest-compatible** resolution, NuGet's `GetNearest` — so a `netstandard2.0` project resolves *under* `net9.0` rather than being skipped, which is what a real build does |
| No compatible target | **Skipped, counted and named**; `--strict` turns it into a failure. CI sets `--strict`; a person exploring a repository does not |

What this replaces is today's rule — "a multi-targeted project is indexed once, at the newest
.NET it builds for" — whose defect was never that it chose, but that it chose **silently and per
project**, so one index could mix targets. Fan-out does not choose; `--framework` chooses once,
for the whole run, on the record.

Our reference schema does **not** carry the target in `src.Decl`'s key. A consumer wanting several
targets in one database adds that discrimination themselves — the same division of labour as the
governing rule.

**Fan-out costs N× the walk, not N× the build.** `Loader.cs:223` already asks for `CoreCompile`
"once per framework", and `Preferred` then picks a single result to walk and discards the rest.
The expensive MSBuild half is therefore *already being paid* on every multi-targeted project in
every run today; fan-out consumes what is currently thrown away. The README's note that "the
other target frameworks are the same files and would dedup on the way in; the work would not"
remains true of the walk and is wrong about the build.

**6 · The configuration is recorded as facts, and named as a Buck-style flavor.**

**Facts, because a name is not queryable — and because it cannot be derived.** Under decision 5's
nearest-compatible resolution a `netstandard2.0` project inside `code#net9.0` records
`netstandard2.0` in its `src.Compilation`, correctly, because that is what it compiled as. **The
resolution root therefore appears nowhere in the existing facts.** A cross-database tool asking
which of several databases is the `net9.0` index cannot compute it, and an instance name it can
only string-match on is not an answer. So a new importable namespace, following the `scip`
precedent so that a Rust or TypeScript indexer means the same thing by it:

```schema
schema config {
  # What this database was built for: the resolution root every src.Compilation
  # was reduced against, which is *not* derivable from those facts.
  #
  # Dimension-leading, so "what framework is this database" is a seek.
  predicate Setting : { dimension : string, value : string }
}
```

**Dimension/value rather than named fields**, because a configuration is not the same shape across
languages — `{framework, net9.0}`, `{target-triple, x86_64-unknown-linux-gnu}`, `{feature, serde}`.
It is also this repository's own idiom: a fact per element, keyed for the question, exactly as
`src.Line` and `src.Param` do it. It absorbs conditional compilation's other axis for free —
`#if DEBUG` makes Debug and Release genuinely different indexes, and that is
`{dimension = "define", value = "DEBUG"}` with no new mechanism.

*Naming tension to settle:* `src` already calls Project/Assembly/Compilation "the build layer", so
a `build` namespace would collide conceptually. `config` separates *what we built under* from
*what the build graph is*.

**Naming: flavors, from Buck.** `address.rs:46` records that "the selector — `name@instance` — is
passed through as a string. Resolving which instance of a name is meant belongs to the store's
catalog, and the server does it." **The client does not parse the selector**, so this is a catalog
change rather than a wire or grammar one.

```text
code#net9.0@01M0B3D
└┬─┘└─┬───┘└───┬───┘
 │    │        └─ instance — which build (a ULID, per address.rs's own example)
 │    └────────── flavor — what configuration
 └─────────────── name — the logical index
```

Buck1 spells a build variant `//lib:foo#shared,linux-x86_64` — a base target plus a
comma-separated flavor set — and Fjord's `where//name` already echoes Buck's `cell//path:target`.
Two properties come with the syntax and both are wanted here: a flavor set is **sorted and
deduped**, so one configuration has exactly one name; and multiple dimensions compose —
`code#net9.0,linux-x64` — without new grammar.

**This dissolves the `--at code@nightly` composition question** rather than answering it: flavor
and instance are different axes, so they never compete for one slot. Overloading `@` would have
conflated *which build* with *what was built*.

One constraint from the address module: a database name may not contain `/`, since the split is at
the last `//`. `#` and `,` are clear.

**The flavor is derived from the facts, not the reverse.** The name is a convenience for humans and
for `fjord list`; `config.Setting` is the truth. A tool that trusts the name over the facts has the
same defect as one that trusts a file path over its contents.

---

## What these decisions cost, and what they close

**`--syntax-only` cannot survive as a semantic mode.** It is defined as "no MSBuild, no NuGet,
no project graph", and it exists "so that a repository which will not restore on this machine
still produces an index". Under hard failure it fails on essentially every real repository — the
dotnet/runtime run left **3,130,214 names unresolved, one in three**. Two honest options, and
one must be chosen as part of Run 4:

- **Remove it.** The mode's whole purpose was degradation, and degradation is now refused.
- **Redefine it as a source-layer-only mode** emitting `File`, `Module` and `Line` — all
  genuinely derivable from syntax — and refusing the semantic layer by name. This keeps a volume
  instrument (`src.Line` alone was 8.6M of the 18.2M facts) without claiming a semantic index.

*Recommendation: redefine rather than remove.* A mode that says which predicates it can fill is
consistent with the rule; a mode that quietly fills them badly is what the rule forbids.

**Run 7 loses its corpus, and that is a scheduling fact.** §1, §12 and §15 were all measured
`--syntax-only` on dotnet/runtime. §15's comparison specifically needs all 27 predicates to
agree stored-for-stored, which a source-layer-only run cannot produce. dotnet/runtime pins an
SDK that will not design-time build here — the README's own instruction is to "delete or relax
the `global.json` in the checkout to get the full semantic index". So Run 7 needs either that
relaxation, recorded as part of the corpus definition, or a corpus that builds unmodified
(OrchardCore is the README's suggestion). **Pick it before Run 4 lands**, because Run 4 moves the
fingerprint and Run 7 is the re-measurement that absorbs it.

**Conditional compilation — closed by decision 5.** A method under `#if NET8_0` with a different
return type has the *same* descriptor, because C# does not overload on return type, and a
different `src.TypeOf` value. A semantic descriptor cannot separate those. What separates them is
the target being **named rather than inferred**: one run indexes one target, so only one of the
two values exists to be written.

**With that, every conflict source is eliminated at its source rather than resolved after the
fact:**

| Source | Eliminated by |
|---|---|
| Two overloads on one line | the type-carrying descriptor (Run 4) |
| Conditional compilation across targets | required target specification (decision 5) |
| A declaration re-reached from another compilation | the values agree — that is dedup, not conflict |

**So the client-side conflict rule is deleted, not relocated** (see Run 8), and `_kinds` goes with
it.

**What decision 5 left open is closed by decision 6** — the configuration is recorded as
`config.Setting` facts rather than carried by a naming convention, and flavor/instance are separate
axes so `--at code@nightly` composes as `code#net9.0@nightly` with nothing to arbitrate. One
sub-choice remains inside decision 6: whether the namespace is called `config` or `build`.

---

## What this changes about scope

The opening question was whether to narrow declared support to the protocol and client
libraries, and demote the .NET indexer. **This plan assumes the opposite decision**, and one
consequence is worth stating: the indexer cannot be demoted while it is the instrument that has
to re-produce §15 (C2). Supporting it and fixing it are the same commitment.

What the plan does adopt from the narrowing instinct is **Run 9's inversion** — first-party
indexers reference SCIP symbols and emit them directly; everything else arrives as SCIP from
indexers we do not write and do not support. That is the scope limit, achieved by consuming a
standard rather than by declining to help.
