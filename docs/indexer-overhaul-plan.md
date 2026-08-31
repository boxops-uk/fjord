# The indexer overhaul — write path, seam, discrimination, and symbol identity (revision 2)

## What this plan is

Five issues (#28–#32) are open against `clients/dotnet`. This plan reads them together with
four decisions taken in discussion — that the .NET indexer stays **supported** as an
end-to-end example, that it grows a **seam** for swapping fact emission, that the producer
should stop carrying **conflict bookkeeping**, and that **SCIP** becomes the cross-language
symbol identity — and sequences the whole of it as one route.

**Revision 2 answers [#34](https://github.com/boxops-uk/fjord/issues/34)**, a review of revision 1
that raised 51 findings against it. Six were verified against the tree before being accepted here;
all six held, and two of them were errors in revision 1 rather than gaps. What changed is recorded
at the end, under *What review #34 changed*. Three of the review's asks were decisions rather than
corrections and are answered in *Decisions*.

---

## What was read, and what it found

Read in full: issues #28–#32 and #34; `clients/dotnet/Boxops.Fjord.Indexer/` (3,653 LOC across
nine files) and `Boxops.Fjord.Client/` (1,236 LOC); `schemas/code.sigla`; **`bench/FINDINGS.md`
§§12, 14, 15, 15a–c, 16, 16a–b, 17, 17a–b**; `docs/glean.md` §1 and §2; `docs/gitnexus.md`
§§16–17; `website/content/schema-language.md`; `crates/fjord-store-fjall/src/catalog.rs`;
`crates/fjord-client/src/address.rs`; `PLAN.md:3215-3240`; `.github/workflows/release.yml`.

| # | Coupling | Direction | Where |
|---|---|---|---|
| C1 | **The two write-path ceilings mask each other.** The gate throttles fact production below one writer's capacity, so `queueing` cannot be large, so "the writer was not the ceiling" is true by construction rather than by measurement. | Conflict — resolved by sequencing | #30, #31; `FINDINGS` §14 |
| C2 | **The gate contaminates the Fjord/Glean comparison, and `FINDINGS` §17b already measured it.** Not a new finding — a known one nothing has acted on. | Re-run §17, not §15 | `FINDINGS:1194-1207` |
| C3 | **The conflict rule is the one thing de-gating can change observably** — and a semantic key removes the hazard rather than making it thread-safe. | The schema fix de-risks the concurrency fix | #30 footgun 2 |
| C4 | **The five issues fall into four disjoint compartments** and none crosses the MSBuild ↔ fact-emission line. The seam is where the bugs already are. | Validates the seam's location | #28/#29 → `Loader`; #32 → `Projects`; #30 → `Indexer`; #31 → `FactSink` |
| C5 | **`ops-I5` is Glean's own rule, adopted** — Glean disables it on its batch paths (`ignoreRedef`, *"silently picking one of the two facts… That's bad"*). Conflict care is not a Fjord tax; it is a requirement Fjord reports and Glean swallows. **`_kinds`/`Declared.First` is already our own `ignoreRedef`.** | Deletes the rule rather than relocating it | `glean.md:54-59`, `:115` |
| C6 | **The repo already specified SCIP without naming it** — "a content-derived string identity with the repo as its first token". **But a SCIP symbol leads with a package triple, not an origin**, so the quote asks for something SCIP does not give. | Adopt the standard; restate the claim | `gitnexus.md:242-244` |
| C7 | **Schema imports exist, but two readers never call `resolve`.** `sample_schema.rs:53` parses and lowers `code.sigla` without following imports, as does the published `fjord-db` example. | `scip`/`config` are declared *in* `code.sigla` for now | `schema-language.md:185`, `sample_schema.rs:53` |
| C8 | **A fact file is portable by predicate *name***, so file ingestion is a read side rather than a format negotiation. | Run 9 emits blocks | `PLAN.md:3225` |
| C9 | **The producer already holds no ids.** What it still holds is *conflict* bookkeeping, which exists only because the key under-discriminates. | Narrows "remove dedup tracking" to one real change | `PLAN.md:3229-3232` |
| C10 | **§17a's "within 8%" rests on a 28 s margin measured gate-held**, and recoverable wait is 4–12× that. It is the number the gate fix most endangers. | §17a joins the re-run list | `FINDINGS:1170-1192` |
| C11 | **`src.Decl`'s key is read by 61 files**, including a hard `KEY_ORDER` assertion, the viewer's five queries and the CLI workload. | Run 4 carries a migration, not just a schema edit | `sample_schema.rs:247`, `viewer/query.rs`, `cli/workload.rs` |

---

## C2 — the gate contamination, and where the evidence actually is

`FINDINGS` §17b already measured this, and revision 1 missed it by stopping at §15c. The section
is titled *"Two things the comparison did not set out to measure"*, and its second is exactly the
finding:

> **The indexer's gate amplifies ~12×, and this run measured it by accident.** The same walk, two
> sinks:
>
> | | gate held | gate wait (8 walkers) | queueing |
> |---|---|---|---|
> | Glean sink (files) | 187.9 s | 928.1 s | 0.3 s |
> | Fjord sink (socket) | 335.9 s | 2,669.8 s | 70.3 s |
>
> **148 s more time holding the gate costs 1,742 s of summed walker wait.**

**This is the citation to use, not §15's.** Three reasons, and they matter:

- **§15 is superseded.** §17 is titled *"On equal footing the two write paths are within 8%, and
  §15's 3.5× was mostly memory pressure"*; `:1183` says *"§15's number was mostly the harness."*
- **§15's pair is confounded twice over.** Its runs differ in peak RSS (16.2 / 20.0 / 24.3 GB,
  `:983`) and `:1013` ties the 45% tail to heap pressure. §17's pair is clean — 9.4 against 9.1 GB.
- **Revision 1 mis-paired its own number.** It quoted §15's *"the last two rows"* sentence and then
  compared 1-writer against Glean for an 8.2×. The last two rows are 3.5×.

**The gate is a separate contaminant from the memory confound**, and only §17 controls for the
second. So the sequencing consequence stands unchanged — measure before claiming — but the
framing does not: this is a finding already in the tree that nothing has acted on, not one nobody
had read that way.

**And the number it most endangers is on no re-run list.** §17a prices the walk at ~502 s
(`:1172`) and reports 380 vs 352 s (*"within 8%"*) and 882 vs 854 s (3%). **Both margins are
28 s.** That 502 s reference was measured gate-held, and §17b gives ~116 s per walker Glean-side
against ~334 s Fjord-side — so recoverable wait is 4–12× the reported margin. §17a's conclusion is
not citable as settled until Run 7 re-runs it.

---

## The four theses

**1. The write path has two ceilings, and they must be lifted in order.** The gate
(`Indexer.cs:97`) is held across `Describe`'s Roslyn symbol queries — the expensive half — so
`--jobs N` parallelises the cheap half and serialises the costly one. Until it is gone, the writer
count cannot be measured, because production is throttled below one writer's capacity. #30 then
#31, never the reverse.

**2. The seam is where the bugs already are.** #28/#29 are `Loader` (MSBuild), #32 is `Projects`
(build facts), #30 is `Indexer` (the walk), #31 is `FactSink` (the sink). Four compartments, none
crossing the MSBuild ↔ fact-emission line.

**3. Discrimination, not bookkeeping.** The producer holds no ids already (`PLAN.md:3229`). What it
still holds is `_kinds`/`Declared.First` — conflict resolution that exists only because
`src.Decl`'s key cannot tell two overloads on one line apart. Make the key **semantic** and pin one
compiled target per database, and collisions become impossible rather than counted. It fixes a
second defect in the same move: `line` is in the key today, so reformatting a file changes the
identity of every declaration in it.

**4. SCIP fills a slot the repo already specified — and decision 5 makes it load-bearing.** Once a
database holds exactly one compiled target, a reference across a target boundary cannot nest a
local `Decl`. The symbol is what carries it. That turns SCIP from an added convenience into the
mechanism the fan-out depends on.

---

## The instrument that does not exist, and every issue asks for it

Four of the five issues name the same missing acceptance test — #29 and #30 want per-predicate
counts invariant across `--jobs`, #31 across `--writers`, #32 a count-vs-count class invariant.
**This is one artifact, and it is Run 0.** It is the issues' own gate rather than refactor
overhead, and it doubles as the seam's characterisation test.

Revision 1 got two things wrong about it, and both are load-bearing.

**It counted the wrong side of the wire.** `sink.Facts[predicate]` is incremented per `Add`
(`FactSink.cs:180-184`) — facts *queued*, not facts the database *holds*. De-gating makes duplicate
`Describe` calls possible, which the plan itself calls harmless because identical facts dedup — so
**a correct fix moves the queued count and would go red.** The converse also fails: two runs can
queue identical totals into different databases.

**The primary assertion is therefore the sealed identity**, which §15a already proved at scale:
25M facts, one writer and four, over real sockets, sealing to the same `0x462058b7b0671d29`
(`FINDINGS:993-996`) — *"`ops-I4` meaning what it says."* Revision 1 never used it. Per-predicate
**stored** counts queried back are the secondary; the queued table stays as diagnostics, and is
*expected* to move once the gate is gone.

**And the fixture was the source tree the plan edits.** `clients/dotnet` is modified by Runs 1, 2,
3, 5, 6 and 8; `src.Line` is one fact per source line, ~44% of a 14,040-fact baseline, and Run 1's
own work deletes ~40 lines. "Ledger unchanged" was false by construction. The fixture is now a
frozen solution under `clients/dotnet/tests/fixtures/`, **outside the indexed path**, which Runs 1,
3, 4 and 3.5 each need anyway for their own bespoke cases.

---

## The runs

Each run has one falsifiable claim and one mechanical gate.

### Run 0 — the ledger, database-side, on a frozen fixture

**Claim.** A frozen corpus, indexed at varying `--jobs` and `--writers`, seals to the same database
identity, and that is asserted in CI.

**Work.** A frozen fixture solution under `clients/dotnet/tests/fixtures/`, edited by no run.
Index it against a throwaway server, `finish` the database, and assert the **sealed identity** is
equal across `--jobs 1`/`--jobs 8` and `--writers 1`/`--writers 4`. Secondary: per-predicate
*stored* counts, queried back. Diagnostics: the queued table `Program.Report` already prints,
recorded but not asserted.

**Scoped to one target.** Run 0 pins `--framework` so it does not depend on Run 3.5; the
per-database fan-out assertion belongs to that run.

**Pin the SDK exactly for this job.** `global.json` uses `rollForward: latestFeature`, so a
10.0.2xx SDK brings different reference assemblies, and `src.TypeOf` stores
`type.ToDisplayString()` — a *value*, which a sealed identity hashes. A sealed identity is more
SDK-sensitive than counts, not less.

**Gate.** Identity equal across both axes; `Conflicts == 0`; `M == 0`; "no project compiles" == 0.

### Run 0.5 — somewhere for a gate to live

**Claim.** The .NET side has a test project, a CI job that runs it, and a stated way for a test to
acquire a server.

**Work.** `git grep -Ein "xunit|nunit|mstest|IsTestProject|dotnet test"` over `clients/` and
`.github/` returns **nothing**, and the `test` job installs no .NET. Six runs' gates have nowhere
to live. Add a test project to `Boxops.Fjord.slnx`, a `setup-dotnet` + `dotnet test` step, and
`InternalsVisibleTo` (or accept Decision 2's split, which removes the need for the write half).
State how a test starts and addresses a server — the socket-path length trap applies.

**Gate.** The job runs, and Run 0's assertions execute inside it.

### Run 1 — #28, workspace load without the second build

**Claim.** Adding built results with `addProjectReferences: false` and wiring the graph by
`ProjectId` gives the same cross-project resolution with no MSBuild process spawned during
workspace load.

**Work.** As #28's sketch; delete `Holds()` and the `ArgumentException` re-entry path.
**The path→id map is keyed `(path, tfm)`, not `path`.** Revision 1 followed #28 in skipping a path
seen twice — but the second sighting is a multi-targeted project's other target, which is exactly
what Run 3.5 consumes. Skipping it here would make fan-out impossible three runs later.

**Gate.** No process creation during workspace load; `A → B → C` (C outside the indexed set)
resolves A→B to source and C to metadata; **project count == successful builds**, the class
invariant, not only the fixture.

### Run 2 — #29, retry the transient case only

**Claim.** Distinguishing *a throw* (pipe race, retryable) from *a clean return with no usable
result* (deterministic) makes project-set completeness independent of `--jobs`.

**Work.** Retry the existing two-attempt pair as a unit, three attempts, escalating back-off. Do
not log inside `Attempt`. **Preserve the reason-picking**: `Loader.cs:238-256` picks the first
error across *both* attempts that is not "does not exist in the project", because one of the two is
always wrong about any given project by construction. A retry loop restructures exactly that code.

**Gate — a seam, not a race.** The fixture has three projects, so it cannot reach the concurrency
the race needs; a repro-based gate would be green with the bug fully present. Instead: an injected
attempt that throws once and succeeds on the second must be retried and counted `retried 1`; one
returning cleanly with no usable result must be attempted exactly twice and never retried; and on a
fixture with one genuinely-broken single-target project, the reported reason is the real one rather
than `MSB4057`.

### Run 3 — #32, a project that built belongs in the graph

**Claim.** Creating the `ProjectInfo` in `Refine` when the path resolves but the glob missed it
recovers the project, its source attribution and its reference edges, without duplicates.

**Work.** As #32. Keep the early return for `Paths.Relative` returning null. Keep path spelling
identical to `Discover`'s.

**Gate.** Both halves: the fixture (a solution referencing a project outside the globbed subtree),
**and the class invariant** — every design-time build result with a usable compilation has a
`Project` fact, one count against one count.

### Run 3.5 — decision 5: fan out, `--framework`, `--strict`

**Claim.** A multi-targeting solution produces one database per compiled target, each holding
exactly one target's facts, and no run is blocked by multi-targeting.

This run did not exist in revision 1 — decision 5 was closed without being scheduled, while Run 0
was specified in terms of a `--strict` flag it does not create.

**Work.** `--framework <tfm>` selects one target; the default fans out over every target present,
writing `code#net9.0`, `code#net8.0`, … . **A project is indexed under a target only if it compiles
as that target** — there is no nearest-compatible reduction (see Decisions). A project matching no
requested target is skipped and named; `--strict` promotes any skipped project *or* target to a run
failure, and CI sets it.

**The address is `#`, not `@`.** `catalog.rs:920-948` rejects a name containing `@` — *"`@`
separates a name from an instance, so a name may not contain one"* — so `code@net9.0` cannot be
created and would resolve as a ULID lookup. The full rules are: non-empty, no leading `.`, no `/`
or `\`, no `@`, no control characters, ≤255 bytes. `#` and `,` are clear. **Flavor sets are sorted
and deduped by this run**, because `check_name` does not do it and Buck's normalisation is Buck's.

**Fan-out costs N× the walk, not N× the build.** `Loader.cs:223` already asks for `CoreCompile`
"once per framework" and `Preferred` discards all but one; fan-out consumes what is thrown away.

**Gate.** A fixture whose projects target different frameworks produces one database per target;
each database's `config.Setting` names exactly one framework; a project targeting none of them is
skipped without `--strict` and fails with it; sealed identities differ between the fan-out
siblings; unqualified resolution finds a flavored name.

### Run 3.6 — delete `Declared.First`, alone

**Claim.** Removing the `First` gate changes `deduped` by a stated amount and the sealed identity
by nothing.

**Work.** `Indexer.cs:463` gates the whole of `Describe` on `First`. Because `_declarations` resets
per compilation while `_kinds` is run-global, `First` is the only thing stopping the expensive
symbol pass re-running for every declaration re-reached from another compilation. **That is a
semantic change, not a refactor** — it moves `deduped` and the wire volume, and burying it inside
Run 8 would invalidate Run 7's freshly published numbers with the run after them.

Keep a run-global **idempotence** guard — a set of described descriptors. That is caching, not
conflict bookkeeping, so C9 survives it.

**Also settle what emits `DeclSpan`.** It is emitted today only `if (first)` (`Indexer.cs:777`).
With `First` gone, say what emits it and whether a primary span is needed — `R.to.line` also
disappears with `line`, so `file_xrefs` (4.9M rows) would otherwise need a per-row join where a
point read sufficed.

**Gate.** Sealed identity unchanged; `deduped` delta stated and recorded in `FINDINGS`.

### Run 4.0 — the conflict census, as a gate

**Claim.** The number of declarations that would collide under Run 4's *candidate* key is known
before Run 4 moves the fingerprint.

**Work.** Revision 1 demoted this to a sizing number. That was wrong: `first` derives from `_kinds`
keyed `{module.Id, line, name}` (`Indexer.cs:744`), so it can only see collisions *on the same
line* — and every collision class #34 found lives on a different line or in a different
compilation. **The counter is blind to the collisions Run 4 introduces.**

So compute the candidate key `{module, descriptor}` alongside the old one, carry `(Kind, TypeOf,
Doc, Param)` against it, and report candidate keys reached with two different tuples **broken down
by predicate**. Run it on dotnet/runtime. "Two symbols differ" means a different declaration, not a
different `ISymbol` object — the memo is per-compilation, so those always differ.

**Gate.** Zero candidate-key conflicts on the fixture *including a conversion-operator pair*, and a
published count for dotnet/runtime. **This stays a gate**, because after Run 8 there is no other
detector.

### Run 4 — identity becomes semantic

**Claim.** A `src.Decl` key identifies a declaration by *what it is* rather than *where it sits*,
reformatting a file does not change what its declarations are, and every reader moves with it.

**4a — the key.**

```schema
predicate Decl     : { module : Module, name : string, descriptor : string } -> string
predicate DeclSpan : { decl : Decl, line : int, col : int, endLine : int, endCol : int }
```

`line` leaves the key; **`name` stays.** Revision 1 dropped both. Without `name` the only routes to
a short name are parsing a ~75-byte descriptor or reverse-joining `SearchByName` — which is keyed
name-first, so "the names of declarations in this file" becomes a scan, breaking three of the
viewer's five queries. Keeping it costs one string and buys direct projection, and
redundancy-for-seekability is this repository's own idiom (`SearchByName`/`SearchByLowerName`,
`Attribute`/`AttributeOf`, `Extends`/`DerivesFrom`).

**The descriptor's disambiguator is derived from `GetDocumentationCommentId()`.** Revision 1 had
this inverted — it made a SCIP descriptor the identity and the doc-comment id a cross-check, when
the cross-check is strictly more discriminating. **User-defined conversion operators overload on
return type**, and the indexer collapses both arms: `op_` stripped (`Indexer.cs:913`), one
`"operator"` kind for `UserDefinedOperator or Conversion` (`:1034`), return type emitted as the
*value* of `src.TypeOf`. So `implicit operator int` and `implicit operator long` on one type reach
one key with two values — distinct today **only because `line` is in the key**, the field this run
removes. `System.Decimal` ships ~20. `GetDocumentationCommentId()` emits `~System.Int32` for
exactly this reason, and a SCIP method descriptor reserves no place for one; a SCIP disambiguator
is indexer-chosen, so encoding the return type there is legal SCIP.

**4b — the symbol, and the cross-target reference.**

```schema
  predicate Symbol      : string
  predicate DeclSymbol  : { symbol : Symbol, to : Decl }
  predicate SymbolOf    : { decl : Decl, symbol : Symbol }
  predicate ExternalRef : { symbol : Symbol, file : File, at : { line : int, col : int, length : int } }
```

`ExternalRef` is new, and decision 5 requires it: with one compiled target per database, a
reference from `App` (net9.0) into `Lib` (netstandard2.0) cannot nest a local `Decl`, because
`Lib`'s facts belong to a different database. It carries the symbol instead, and a cross-database
tool joins it against the sibling's `DeclSymbol`. The target is a project in the solution, so the
build exists and the package triple can be formed.

**Declared inside `code.sigla`, not as separate files — for now.** C7: `sample_schema.rs:53`
`include_str!`s `code.sigla` and calls `parse` + `lower` with `assert!(diags.is_empty())`, never
`resolve` — the only entry point that follows imports. The published `fjord-db` example does the
same. Splitting into `scip.sigla`/`config.sigla` needs both readers converted to a resolving path
and an answer for a schema-path-less embedder; that is deferred, and the split is a later,
independently reviewable change.

**4c — the Glean union arm, in this run.** `GleanFacts.WriteValue` handles four type pairs and
`:217` is a throwing `default`. Every `Decl` reference nests via `WriteRef`→`WriteFact`, so a union
in the key throws on the first `Decl` fact — **disabling `--glean-out`, which Run 7 needs.**
Revision 1 put the fix in Run 8, *after* the run it breaks. Add a `(FjordType.Union,
FjordValue.Union)` arm emitting a single-key tagged object keyed by alternative name (reuse
`Values.cs:176`), and a sum type in `fjbench.angle` with alternatives in `code.sigla` order.

**4d — the Rust migration.** `git grep -l 'src\.Decl'` is **61 files**. The viewer's five queries,
the CLI workload, `sample_schema.rs`'s 27-predicate count and its `KEY_ORDER` assertion
(`:247` — `["module","name","line"]`), `fingerprint.rs`, `byte_identical_with_dotnet.rs`,
`golden/blocks.txt`. Run 9's gate — the viewer answering `/symbol/{name}` — is otherwise broken by
a run five steps earlier with no intervening work item.

**4e — the flag day, explicitly.** `CHANGELOG.md:217-219` declined exactly this once: *"a union
there would move its fingerprint and the constants two .NET clients carry, and that is a flag day."*
The order: edit `code.sigla` → `fjord schema fingerprint` → paste into `CodeIndex.cs` and
`Demo/Program.cs` → `emit-golden.sh` → `cargo test -p fjord-client byte_identical` →
`sample_schema.rs` (count, names, `KEY_ORDER`) → `fjbench.angle`. Confirm the gating CI job has
both toolchains: a Rust test asserts a fingerprint only a .NET run can produce.

**Gate.** Run 4.0's census is zero on a fixture containing two overloads on one line, a
conversion-operator pair, and a type with a same-line constructor — **asserted per predicate**, not
on `Decl`'s kind alone; reformatting the fixture produces an identical set of `src.Decl` keys;
`--glean-out` over the fixture loads and agrees stored-for-stored; `cargo test -p fjord-cli -p
fjord-viewer` green and the viewer serves `/symbol/…`; sealed identity recorded as the new baseline.

### Run 5 — #30, remove the gate

**Claim.** With shared state made safe per-mutation rather than per-declaration, walk throughput
scales with `--jobs`, and the sealed identity is unchanged.

**The shared state, item by item** — revision 1 listed three and there are more. `_declarations`,
`_files`, `_modules`, `_kinds` (gone by Run 4), `_imports`, `_walked`, the counters, the sink, the
progress callback, and `Uses`/`SampleName`/`SampleMethod`.

Two are not counter problems:

- **`Module.Id` is a dense identity.** `new Module(fact, _modules.Count)` (`Indexer.cs:844`) is
  packed into the import-edge dedup key (`:430`). Colliding ids make `_imports` dedup a *distinct*
  edge — **silently dropping `src.Import` facts**, which will read as "de-gating lost facts".
  Replace with `Interlocked.Increment`, or key `_imports` on `(path, namespace)` and drop the id.
- **The max-selection for `SampleName` has no cheap deterministic concurrent form.** Either accept
  first-reached (`Interlocked.CompareExchange`) and keep it out of the ledger, or say why not.

**The memo shape, spelled out** — both obvious readings break a gate. `ConcurrentDictionary.GetOrAdd`
runs its factory concurrently on losers, and the factory here *emits facts and increments
counters*; `Lazy<T>` serialises that but **throws when the factory re-enters itself**, which is what
write-before-recurse does on a cycle. So:

1. the memo value is identity-only, published with `TryAdd`;
2. only the winning thread emits and describes;
3. nothing that emits a fact runs inside a `GetOrAdd`/`Lazy` factory;
4. cycle termination is a per-thread in-progress set consulted *before* the memo;
5. if a cycle break emits a shallower value for one key, say so — that is a conflict source created
   here.

**Keep the instrumentation**, replacing `gate wait`/`gate held` with contended time on the striped
locks. **Do not use per-thread buffers**: they would multiply duplicate `File`/`Module` facts by
thread count and destroy §15a's check.

**Gate.** Sealed identity equal at `--jobs 1` and `--jobs 32`; contention a small fraction of the
run; `src.Import` count unchanged. **Scaling moves to the bench harness** — `Parallel.ForEach` runs
over trees within one compilation and compilations are serial, so on a three-project fixture in a
~2.6 s run "materially above `--jobs 1`" is a coin-flip on a shared runner.

### Run 6 — #31, the writer default, measured on an un-gated walk

**Claim.** The writer count that minimises end-to-end sealed wall clock is measured, and **sets**
the default.

**Work.** Sweep `--writers` across **more than one `--jobs`**, on **named corpora with stated
sizes** — §15c's crossover is a function of corpus size (0.76 at 4,000 files → 1.27 at 24,000), not
core count. The objective is end-to-end sealed wall clock, not `queueing` alone: §15c showed the
stall can be *moved* rather than removed (1,019 → 271 s while summed `writing` inflated 2.5×).
`--emit` must force one writer — **the forcing does not exist on `origin/main`**, and is latent only
because the default is 1.

**Gate.** The default is whatever this run measures, recorded with corpus, `--jobs` and figures.
Fact totals identical at 1 and 8 writers; `--emit` byte-identical across two runs.

### Run 7 — re-measure, and correct the record

**Claim.** Every published number the gate or the key move touches is re-run, and the record says
which of its predecessors were contaminated and by how much.

**Work.** Re-run **§12, §14, §15, §15a–c, §16, §16a–b, §17, §17a–b** on the write side, and **§1,
§2, §6, §11** on the read side — the key-order finding, the 67 q/s mix and the ~6,000 q/s figure
are all measured against a key Run 4 changes, which is the same defect C2 raises against §15. State
per omitted section why the key move cannot touch it.

Record in §17a **now**, before the re-run, that its 502 s reference is gate-inflated so 8%/3% are
not citable as settled. Update the indexer README's sample output.

**The corpus pins `--framework`** and names it, which §15's method needs anyway.

**Gate — the emit-priced walk.** Revision 1 gated on `gate wait` being comparable across sinks, but
Run 5 replaces that counter with striped-lock contention, a much smaller number that is comparable
almost regardless. §17a's construction is the right one: price the walk via the emit (60 s sink,
0.3 s queueing) and assert *the walk's own cost* matches across sinks.

### Run 8 — the seam

**Claim.** The fact-emitting half can be replaced without touching the MSBuild half, by a consumer
outside the assembly.

**8a — the write seam goes public, in `Boxops.Fjord.Client`.** `IBlockTarget`, `FactSink`,
`IFactWriter` — the batching, the bounded queue, the writer threads, the latched-failure rule. None
of it says anything about Roslyn; all of it is *generic write support*, which is the supported
surface. `FjordSchema` and `FjordFact` are already public there, so it is largely an accessibility
change. `FactSink` takes an `FjordSchema` rather than reaching `CodeIndex`; `GleanFacts` is the
precedent for deriving rather than tabulating.

**8b — build facts.** `ProjectInfo` becomes pure build data; `ProjectInfo.Fact`
(`Projects.cs:22`) and `ProjectIndex.Emit` (`:190`) move behind an emitter. The two halves meet at
`Indexer.cs:823`, so the build hook must expose a file → project-fact lookup the walk can reach.

**8c — the walk seam stays internal to the Indexer.** `IIndexPass` is indexer-shaped, and an
indexer is the thing this project does not support. The entity-model alternative is dropped: after
Runs 3.6, 4 and 5 a raw-writer consumer inherits almost nothing.

**No conflict rule, sink-side or otherwise** — Runs 4 and 3.5 remove everything it resolved. **But
keep a client-side conflict *diagnostic***: the server rejects the *fact* and cannot name the
declaration or its source location, and the state that could is what this run deletes. Without it
the failure mode is a multi-hour run, a rejected stream, no hatch and no diagnostic. Reporting is
not the covering-up the governing rule refuses — revision 1 conflated the two.

**Gate.** Sealed identity unchanged; the Glean target rides 8a unmodified; Run 9's converter
consumes the *published* write seam from outside the assembly.

### Run 9 — SCIP as an ingestion path

**Claim.** An off-the-shelf SCIP index loads into a database declaring the SCIP predicates, with no
per-language indexer written here.

**Work.** A converter: SCIP protobuf → facts, written against Run 8a's published write seam so it
is a genuine external consumer. It emits **blocks**, which is what `--emit` already writes, so it
becomes a file ingester for free when the read side lands.

**Two limits to state rather than discover.** Its gate is written against `src.*`-only viewer
routes, so the converter must synthesise the whole source layer — "no per-language work" holds as
"no per-language *indexer*", not "no work". And two producers writing descriptors into one key
field must agree on the descriptor form, or one database holds two spellings of one identity.

**Gate.** `scip-rust` over this repository, loaded, and the viewer's `/symbol/{name}` answering
against it — which is also the CI release artifact.

---

## Sequencing

```
Run 0    ledger — sealed identity, frozen fixture ────────────┐ gates everything
Run 0.5  .NET test project + CI plumbing                      │ nothing is gated without it
                                                              │
Run 1    #28 workspace load — (path, tfm) map                 │
Run 2    #29 retry — deterministic seam, not a race repro     │
Run 3    #32 project rescue + the class invariant             │
                                                              │
Run 3.5  decision 5: fan out, --framework, --strict           │ was unscheduled
Run 3.6  delete Declared.First, alone, deduped delta stated   │ before Run 7, not after
Run 4.0  candidate-key census on dotnet/runtime — a GATE      │
Run 4    semantic key + symbol + union arm + migration + flag day
                                                              │
Run 5    #30 de-gate — full state list, memo shape spelled out
Run 6    #31 writer default — the measurement sets it         │
Run 7    re-measure §12/§14/§15/§16/§17 + §1/§2/§6/§11        │
                                                              │
Run 8    the seam — write half public in Client               │
Run 9    SCIP ingestion — a consumer from outside the assembly
```

Five orderings are load-bearing:

- **Run 0.5 before every gate.** There is no .NET test project; six runs' gates have nowhere to run.
- **Run 1 before Run 2** — #28 deletes the code a retry loop would otherwise have to preserve.
- **Run 1's map is `(path, tfm)` because Run 3.5 needs it** — skipping a multi-targeted project's
  second result makes fan-out impossible three runs later.
- **Run 3.6 before Run 7** — deleting `First` moves `deduped`, so burying it in Run 8 would
  invalidate Run 7's numbers with the run after them.
- **Run 4 before Run 5**, **Run 5 before Run 6**, **Run 7 after both** — one fingerprint move and
  one de-gating absorbed by one re-measurement.

---

## What this plan does not prove

- **That §17's conclusion survives.** It establishes that §17a's margin is gate-inflated, not what
  the re-run will say. Glean's write path may still be cheaper.
- **That the descriptor is stable across producers.** SCIP's overload disambiguator is
  indexer-chosen; deriving it from `GetDocumentationCommentId()` fixes it for *this* indexer and
  says nothing about `scip-rust`'s. Run 9's second producer is where that is found out.
- **That `Local` descriptors are defined.** `Local { file, id }` still does not say what `id` is.
  Occurrence-ordered breaks the reformat-stability headline for exactly the declarations the union
  was added for; name-based collides. This needs settling inside Run 4, with two fixtures — two
  same-named local functions in one method, and a local function whose enclosing method is
  reformatted.
- **That a union in the key is the right cost.** Every future descriptor form is then a Breaking
  re-fingerprint of the 15 `Decl`-referencing predicates, where a reserved-prefix string would cost
  nothing.
- **Anything about cross-database *query*.** SCIP supplies a join key valid across databases; the
  engine has none. Decision 5 now *depends* on that key for cross-target references, which raises
  the stakes on `ExternalRef` without building the query side.

---

## Decisions

All six are settled. The governing call, taken once Runs 1–3 stabilise the project layer:

> **A run that cannot resolve assemblies or types fails, loudly, rather than emitting a degraded
> fact.** Correctness is the schema's business; compatibility with an awkward environment is the
> indexer's.

**Its granularity is per project, and `--strict` is the single switch.** Revision 1 left this
unstated while Run 2 adopted retry-then-skip and decision 5 skipped an incompatible target — both
emitting partial indexes. Per symbol, no real repository indexes; per run, #29's pipe race becomes
a run-killer. So: a project that cannot be resolved is skipped and named, `--strict` promotes any
skipped project or target to a run failure, CI sets `--strict`, and Run 4 refuses to key a
declaration whose disambiguator contains an error type.

**1 · Writer count — the measurement sets it.** Revision 1 fixed `min(8, ProcessorCount)` before
Run 6 measured it, which made Run 6 unable to falsify the decision it informed. Inverted: Run 6's
sweep sets the default, recorded with its corpora, sizes, `--jobs` values and figures.

**2 · The seam splits — write half public, walk half internal.** Revision 1 kept everything
`internal`, which made Run 9 a second entry point *in the same assembly* and so no test of an
extension surface at all. But publishing the whole seam overshoots. The cut that matches this
project's declared scope — *client libraries that speak the protocol, generic write support, not
baking in indexers* — puts `IBlockTarget`/`FactSink`/`IFactWriter` into `Boxops.Fjord.Client`,
which already ships, multi-targets and packs, and leaves `IIndexPass` inside the indexer.

**3 · Overload disambiguators — type-carrying, derived from the doc-comment id.** Under hard failure
a run that could not resolve a type does not produce facts, so the descriptor may carry parameter
types. Revision 1 then made a SCIP descriptor the identity and `GetDocumentationCommentId()` a
cross-check — the wrong way round, since only the latter carries `~ReturnType`, without which
conversion operators collide.

**4 · No first-party Rust indexer.** Folded into Run 9's gate.

**5 · Fan out by default; one compiled target per database; never block.**

| | Behaviour |
|---|---|
| **Default** | Fan out — one database per target present, `code#net9.0`, `code#net8.0`, … |
| `--framework <tfm>` | That target only, into one database |
| Per project | **Indexed under a target only if it compiles as that target.** No nearest-compatible reduction |
| No match | Skipped and named; `--strict` makes it a failure. CI sets `--strict` |

Revision 1 used NuGet's `GetNearest` so that a `netstandard2.0` project resolved *under* `net9.0`.
That pins the **resolution root, not the compiled target** — the project still compiles as
netstandard2.0, with its own defines, and `src.Module` is keyed by file, so one shared file under
two define sets reaches one key with two `TypeOf` values. Both `#if` branches resolve cleanly, so
hard failure never fires. **A coherent database is worth more than a complete reference graph**, so
strict matching it is, and the elimination table's second row becomes true.

**The cost, stated: the reference graph is cut at every target boundary.** `src.ExternalRef` carries
those references by symbol (Run 4b), and joining them is a cross-database client concern.

**6 · Configuration recorded as facts, named as a Buck-style flavor.** The resolution root is not
derivable from `src.Compilation`, so a name is not enough:

```schema
  predicate Setting : { dimension : string, value : string }
```

Dimension/value rather than named fields, because a configuration is not the same shape across
languages. It absorbs `#if DEBUG` as `{define, DEBUG}` — and **nothing pins or records the
*configuration* today**: `Loader` sets no `Configuration` and there is no `--configuration`. Run 3.5
adds one, or states that Debug is assumed.

Naming: `code#net9.0@01M0B3D` — flavor is what configuration, instance is which build, so they
never compete for one slot. **`#`, never `@`** (Run 3.5). Sorted and deduped, by us. The flavor is
derived from the facts, not the reverse.

*One sub-choice deferred with C7: whether these predicates eventually move to `scip.sigla` and
`config.sigla`, which needs two non-resolving readers converted first.*

---

## What review #34 changed

Six findings were verified against the tree before acceptance; all six held.

| Finding | Verified how | Effect |
|---|---|---|
| §16/§17 supersede §15 | `FINDINGS:1152`, `:1194-1207` | C2 rewritten; the framing demoted from "a finding nobody read that way" to "a finding nothing acted on" |
| `code@net9.0` uncreatable | `catalog.rs:920-948` | Revision 1 contradicted itself (`@` at one line, `#` at four); `#` everywhere, Run 3.5 owns it |
| Conversion operators overload on return type | `Indexer.cs:913`, `:1034` | **A factual error in revision 1**, with the elimination table built on it; disambiguator now derived from the doc-comment id |
| No union arm in the Glean writer | `GleanFacts.cs:217` | 4c moved into Run 4, which otherwise disabled Run 7 |
| 61 files read `src.Decl` | `git grep -l`, `sample_schema.rs:247` | 4d added; `name` kept in the key |
| No .NET test infrastructure | zero grep hits across `clients/`, `.github/` | Run 0.5 added |

Two of the review's findings were refuted by its own re-verification and are recorded so nobody
re-investigates: the writer default is not keyed on the wrong variable (the clamp is unexercised,
not wrong), and generated sources are already filtered (`Indexer.cs:862-867`,
`Projects.cs:445-452`) so the golden is not `obj/**/*.g.cs`-fragile — worth a guard test, since
removing the filter would silently make every future golden SDK-dependent.

**And two of revision 1's own claims were wrong in the same direction**: it cited `ops-I5` for
conflict rejection where `invariants.md:403-404` assigns order-independent rejection to **`ops-I4`**,
and `PLAN.md:3184` calls a winner-picking rule "the one thing `ops-I4` really forbids". Both
citations are corrected above.

---

## What this changes about scope

The opening question was whether to narrow declared support to the protocol and client libraries,
and demote the .NET indexer. **This plan assumes the opposite decision**, with one consequence
worth stating: the indexer cannot be demoted while it is the instrument that has to re-produce §17.

What it does adopt from the narrowing instinct is now sharper than revision 1's version. Decision 2
draws the supported line *through* the indexer rather than around it: the **write path** —
batching, backpressure, block targets — becomes published client surface, because that is generic
write support; the **walk** stays an unsupported example. And Run 9 inverts the rest: first-party
indexers emit SCIP symbols directly, everything else arrives as SCIP from indexers this project
does not write and does not support.
