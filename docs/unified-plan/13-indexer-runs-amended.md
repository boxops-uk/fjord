# W13 · The indexer runs, amended

| | |
|---|---|
| **Issues** | [#28](https://github.com/boxops-uk/fjord/issues/28)–[#32](https://github.com/boxops-uk/fjord/issues/32), review [#34](https://github.com/boxops-uk/fjord/issues/34), follow-ups [#42](https://github.com/boxops-uk/fjord/issues/42) |
| **Specification** | **[`docs/indexer-overhaul-plan.md`](../indexer-overhaul-plan.md) (revision 2) remains the body of Runs 0–9.** This file amends it. Where the two disagree, **this file wins** |
| **Area** | `clients/dotnet`, `.github/workflows/release.yml`, `schemas/code.sigla`, `bench/FINDINGS.md` |

Revision 2 is sound and its sequencing is right; nothing here reopens a decision. What follows is
what the rest of this batch changes about it, what review #34 asked for that revision 2 does not
carry, and the anchors that were checked and found to have drifted.

---

## A · Anchors checked against the tree

Every substantive anchor in revision 2 holds. Four citations land 1–4 lines off, one LOC figure is
wrong, and one names a type that does not exist.

| Cited | Actual | Note |
|---|---|---|
| `Indexer.cs:744` (`_kinds` key) | **`:746`** | `var key = $"{module.Id}\0{line}\0{name}";` |
| `Indexer.cs:862-867` (generated filter) | **`:865-870`** | the cited range is the doc comment |
| `Indexer.cs:913` (`op_` stripped) | **`:914`** | `:913` is the switch-arm header |
| `Projects.cs:445-452` (`IsBuildOutput`) | **`:449-452`** | ditto |
| "3,653 LOC / 1,236 LOC" | **4,116 / 1,486** | not drift — the files are untouched since 20 Aug; the count was wrong |

Exact and confirmed: `Indexer.cs:97`, `:463`, `:777`, `:823`, `:844`, `:430`, `:1034`;
`Loader.cs:119`, `:203`, `:223`, `:238-256`; `Projects.cs:22`, `:190`; `FactSink.cs:180-184`;
`GleanFacts.cs:217`; `catalog.rs:920-948` (including the `@` sentence verbatim at `:936`);
`CHANGELOG.md:217-219`; `sample_schema.rs:53`, `:169`, `:247`; `PLAN.md:3184`; all seven
`bench/FINDINGS.md` citations; and `git grep -l 'src\.Decl'` = **61**.

**`IFactWriter` does not exist.** Run 8a and Decision 2 both name `IBlockTarget`, `FactSink`,
`IFactWriter` as the seam to publish. The tree has `IBlockTarget` (`internal interface`,
`BlockTarget.cs:25`) and `FactSink` (`internal sealed class`, `FactSink.cs:47`), both in
`Boxops.Fjord.Indexer`; writer concurrency is a raw `Thread[] _writers` (`FactSink.cs:62`). So Run 8a
is **not** "largely an accessibility change" for all three: it is an accessibility change for two and
an **extraction** for the third. Amend 8a to say which, and price it accordingly.

---

## B · Run-by-run amendments

> **Landed: R0.5, R1, R2, R3, R3.5, R3.7, R4.0's census, R5.** What they found is recorded
> in each run below and in the commits. Four defects turned up that no issue had named, and
> each is worth more than the run that found it:
>
> - **A checkout that had been built indexed as nothing at all.** `CoreCompile` is
>   incremental, so after any ordinary `dotnet build` MSBuild skipped it — and the compiler
>   command line it would have logged is the whole of what a design-time build reads. Every
>   project came back succeeded-with-no-result, reported as a failed build. `$(NonExistentFile)`
>   is the target's own escape hatch.
> - **A solution naming a project through `..` produced no results for it.** The path is
>   carried unnormalised while MSBuild reports the normalised one, and Buildalyzer pairs
>   them by string. R3's own fixture had nothing to rescue until this was fixed.
> - **A cross-project type was ambiguous on any checkout that had been built.**
>   `addProjectReferences: true` left the referenced project in the compilation twice, once
>   as a project and once as its assembly, and Roslyn answers `null` for an ambiguous type
>   rather than choosing.
> - **`--emit` would have had two targets overwrite one file** the day the fan-out landed.
>
> **Still open from these runs:** R3.5's gate also asks that unqualified resolution find a
> flavoured name — `code` resolving to `code#net10.0`. No such thing exists in the
> catalogue, and adding it is an engine change with its own naming rules and its own
> answer for what happens when two flavours match. It is not the producer's to do.

### R0 — the ledger

**+ A re-baseline is a reviewed change with a stated cause.** The primary assertion is the sealed
identity, and the identity hashes values — so **every schema change moves it**. Runs that move it and
do not currently say so: **3.5** (fan-out changes which facts exist per database), **4**, and
anything landing from **W6/W9**. **3.6** claims the identity is *unchanged*, which is the claim worth
keeping — if it moves, something else broke. Add one line to Run 0: a moved baseline is accepted with
a written cause, never as a diff someone approves.

**+ The SDK pin applies to every later baseline, not only Run 0's.** `global.json` is `10.0.100`
with `rollForward: latestFeature`, so a 10.0.2xx SDK brings different reference assemblies, and a
display string is stored as a *value*, which a sealed identity hashes. **Re-pointed:** it was
`src.TypeOf` storing `type.ToDisplayString()`, and that predicate is gone. The exposure is not —
`codemarkup.Definition` carries the hover signature (`CodeMarkup.cs:132`) and two `csharp`
predicates fall back to `ToDisplayString()` when a symbol has no documentation comment id
(`CsharpEntities.cs:422`, `:461`). Same argument, three sites instead of one: a sealed identity is
more SDK-sensitive than counts, not less.

**Re-cut: the gate names two things that no longer exist, and one that never existed.** Revision 2
gates R0 on *"Identity equal across both axes; `Conflicts == 0`; `M == 0`; \"no project compiles\"
== 0"*, and three quarters of that cannot be executed:

- **`Conflicts == 0`** — there is no `Conflicts` counter. The producer's conflict bookkeeping is
  gone, which was C9's point and D12's consequence: the key discriminates, so the hazard was
  removed rather than made observable. Nothing replaces the assertion because nothing can conflict
  quietly any more — `ops-I4` makes a conflicting fact a **rejected** fact, and `FactSink`'s
  latched-failure rule makes a rejected fact a failed run. *The run completing* is the assertion.
- **`M == 0`** names no counter this plan or the tree defines, here or anywhere else in revision 2.
  A gate has to be executable by someone who did not write it, so it goes rather than being guessed
  at.
- **`"no project compiles" == 0`** survives, and is `indexer.Unattributed` — printed as *"N file(s)
  no project compiles"* (`Program.cs:269-277`).

**The replacement gate.** Sealed identity equal across `--jobs 1`/`--jobs 8` and
`--writers 1`/`--writers 4`; the run completes, which is the conflict assertion; `Unattributed ==
0` and **`Inexpressible == 0`** on a fixture built to make both true — the second is the counter
that caught a fixture whose project had no metadata references and therefore indexed one type out
of nine, so it is the gate that stops R0 baselining a nearly-empty database and calling it stable.
Per-predicate stored counts queried back, as before. Recorded in the run's own commit message and
in the .NET test project R0.5 built, **not** in `bench/FINDINGS.md`, which is closed.

### R0.5 — somewhere for a gate to live

**+ The CI facts, and the required-check trap.** There is exactly one workflow,
`.github/workflows/release.yml`, with seven jobs: `test`, `package`, `build`, `site`, `pages`,
`attest`, `release`. `test` installs **no .NET**. `package` **does** — `actions/setup-dotnet@v4` with
`global-json-file: clients/dotnet/global.json`, then `dotnet build … -warnaserror` and `dotnet pack`
— but runs no tests, because there are none.

The trap: repository rulesets gate `main` and `release/*` on the **`test`** and **`build`** checks by
name. A new `dotnet-test` job is not a required check until a ruleset is edited, which is an
admin action and audit-logged. **Recommendation: add `setup-dotnet` + `dotnet test` to the existing
`test` job**, so six runs' gates are protected by a check that is already required. Cost: the `test`
job grows a .NET toolchain install on every PR.

### R3.5 — fan out, `--framework`, `--strict`

**+ It writes `config.Setting`, and W7 says which dimensions.** A fan-out database must record at
minimum `framework` (exactly one), `configuration`, `repo`, `revision`, `index-root`,
`position-encoding`, `symbol-scheme`, `producer`, and `language`. **`index-root` is the one that
closes a real defect**: the C# index's own provenance had to be *inferred* once, from which checkout
was the only one in `$HOME`.

**+ `--configuration` is resolved, either way.** Nothing pins or records it today (`Loader` sets no
`Configuration`; there is no flag). Adding the flag, or recording `{configuration, Debug}` with
"Debug assumed", both close it. Silence does not.

### R3.7 (new) — **delete `--syntax-only`**

Revision 2 does not mention `--syntax-only` anywhere, and review #34 asked for it to be specified —
which namespace derivation fills `src.Module`, and what a syntax-only database may claim.

**Decision: it is removed, not specified.** The indexer requires successful resolution. That is the
governing rule already written down — *"a run that cannot resolve assemblies or types fails, loudly,
rather than emitting a degraded fact"* — and a mode whose whole purpose is to skip resolution is the
exception that rule cannot survive.

**The surface, exactly** — 40 occurrences across seven files:

| File | What goes |
|---|---|
| `Options.cs:186, 253, 306, 379` | the `SyntaxOnly` property, its parse arm and its default |
| `Options.cs:124-129, 207` | `--skip-files`, which exists **only** for a syntax-only run |
| `Loader.cs:62, 443-534` | the branch and the whole `SyntaxOnly(...)` walk |
| `Indexer.cs`, `Projects.cs:39` | the reads and the doc comments that explain the mode |
| `index-repo.sh`, `README.md`, `Boxops.Fjord.Indexer/README.md` | the invocations and the prose |

**One consequence to record rather than discover: three published measurements were taken with it.**
`bench/FINDINGS.md:20` (§1's corpus — *"`--syntax-only --jobs 8`: 32,710 files, 18,176,899 facts"*),
`:932` (§14's 16-file, 12,382-fact run) and `:965` (§15's producer). Deleting the flag makes those
corpora **unreproducible**, not merely stale: a semantic walk over the same tree is a different and
much larger workload.

**Gate.** `git grep -in "syntax-only\|syntaxOnly\|skip-files"` over `clients/` returns nothing; the
indexer refuses, by name, a run whose resolution failed; and §1/§14/§15 are marked in `FINDINGS`
as measured by a mode that no longer exists, with their re-run scheduled in R7 against a semantic
walk on a named corpus.

**Done, and the gate's first clause cannot be taken literally** — a test that proves the flags are
refused has to name them, so the grep finds its own guard. The honest reading is *nothing outside
the test that refuses them*, and that is what holds: `Options`, `Loader`'s branch and its whole
walk, the `--skip-files` slicing that existed only for it, and the prose are gone.

**What replaced the fallback is the point of the run.** The loader fell back to the syntax walk
when every project failed; it now throws, naming what to fix. An index missing four fifths of its
edges looks complete and answers wrongly with nothing in it to say so — one name in five hundred
unresolved through a design-time build against one in eight without, which is the measurement the
README keeps.

`--max-projects` replaces `--skip-files` for a checkout too big for one machine, and that is a
better answer rather than an equal one: a project is a compilation and a compilation is what the
memory is proportional to, where slicing one compilation by *file* dropped every reference that
crossed a slice boundary.

### R4a — the descriptor is a **string**, and this settles #42 item 1

Revision 2's 4a declares `descriptor : string`, while three other passages only make sense if it is a
union. **It is a string.** The reasons, in the order they decide it:

1. Revision 2's own *"does not prove"* list already prices the alternative: *"a union in the key is
   … every future descriptor form is then a Breaking re-fingerprint of the 15 `Decl`-referencing
   predicates, where a reserved-prefix string would cost nothing."* W8 makes the identical argument
   one layer up and it is the reason `codemarkup` keys on a string.
2. **`Local` descriptors stop being a blocker.** SCIP's `local0`/`local1` are occurrence-ordered, so
   they change when a file is edited — the exact instability Run 4 exists to remove. W8's
   `codemarkup.FileLocalXRef` resolves a file-local reference **span to span within one file**,
   costing no string and stable under any edit that does not move the target. So the `Local`
   alternative is not undefined; it is **unnecessary**. A local that genuinely needs a name takes a
   reserved-prefix string (`local:<scope-path>`), and the two fixtures #34 asked for still land: two
   same-named local functions in one method, and a local function whose enclosing method is
   reformatted.
3. Consequently **two of revision 2's "does not prove" items are struck** — *"that `Local`
   descriptors are defined"* and *"that a union in the key is the right cost"* — and the third,
   about cross-producer descriptor stability, stands untouched.

**4c stays, and is re-justified.** `GleanFacts.WriteValue` (`GleanFacts.cs:167-221`, quoted in full
in the research) handles four type pairs with a throwing `default:` at `:217`. With no union in
`Decl`'s key it is not *required* by Run 4 — and it is still in Run 4, because it is one instance of
the class W2 closes on the Rust side, and finding it in Run 4 rather than Run 8 was luck plus a
reviewer. Note that `ValueCodec.WriteValue` (`Boxops.Fjord.Client/Values.cs:118`, `default:` at
`:165-167`) has the identical shape and is the client's, not the Glean writer's.

### R4b — `src.Symbol` is declared by `src.sigla`

Reversing issue #39's recommendation, for the reason in W6 D1: **`codemarkup.sigla` imports `src` and
must not import `code.sigla`**. So R4b declares `DeclSymbol` and `SymbolOf` in `code.sigla`, adds
`import src`, and declares **no `Symbol` and no `ExternalRef`**:

- **`ExternalRef` becomes `codemarkup.SymbolXRef` + `codemarkup.FileXRef`** (W8). Its file-keyed twin
  is not optional — `src.FileXRef`'s own comment records that the file question against the
  symbol-keyed predicate was *"a scan of the largest relational predicate in the index: 4.9M rows to
  find the few hundred in one file."*
- **If W8 slips past R4**, R4b keeps `ExternalRef` **and adds the file-keyed twin**, in
  `src.ByteSpan` offsets rather than `{line, col, length}`, with the encoding declared per database
  (W7).

### R4d / R4e — the migration and the flag day

Unchanged in substance; the inventory and the order move to **[W14](14-flag-day-inventory.md)**,
which R4e and W6 share.

**+ The line table moves with it.** W6 deletes `src.Line` in favour of `src.FileLine`, so the
indexer's line emission (`Indexer.cs:324,331`, `CodeIndex.cs:378`) must produce the richer value —
`text`, `start` (UTF-8 bytes), `bytes`, `cstart` (UTF-16 code units). It already counts UTF-16 for
columns (`GleanFacts.cs:297`), so the new field is the cheap one and the UTF-8 offset is the new
work. Whether this rides R4's flag day or W6's is a scheduling choice; it must ride one of them,
because a producer emitting `src.Line` against a schema that no longer declares it is refused at
the first block. Two additions there: the schema-level fingerprint is
`0xb08eea634e866a75` today and is carried in **two** independently-pasted C# constants, and the
per-predicate handshake claim is the alternative that would make W6 cost the clients nothing.

### R3.6 — delete `Declared.First`

**Re-cut: the target is already gone, and it went as a side effect rather than as this run.**
`Indexer.cs:463` gated the whole of `Describe` on `First`, and `_kinds` was the run-global map it
read; the S-runs' entity rewrite deleted both along with the predicate they served. `git grep
'_kinds\|Declared\.First'` over `clients/dotnet` returns nothing.

**So the claim is unproven rather than satisfied.** R3.6's whole point was that removing the gate
*"is a semantic change, not a refactor"* — it moves `deduped` and the wire volume — and that
burying it inside a larger run would hide the movement. It was buried inside a larger run. What is
still owed is therefore the measurement the run existed to force, and it is now cheap: the fixture
indexes in seconds and `deduped` is on the report.

**And the successor has the same shape, one level down.** `CsharpEntities._entities` is
`Dictionary<ISymbol, FjordFact?>` under `SymbolEqualityComparer.Default` (`CsharpEntities.cs:36`),
built once per run (`Indexer.cs:181`) but keyed on `ISymbol` — which is **per-compilation**. A
declaration re-reached from another compilation misses the memo, is rebuilt, and is re-emitted;
the server interns it. That is exactly the cost `First` was suppressing, moved rather than removed,
and the class comment says so — *"the memo exists to stop rebuilding it, not to remember an
identity"*.

**Gate.** `deduped` on the frozen fixture stated as a number, in the commit message and asserted in
the .NET suite so it cannot drift silently; sealed identity unchanged by any change this run makes.
The cross-compilation miss is **stated, not fixed** — a run-global key would be a descriptor
string, which is R4's key by another name and was cancelled with it, so the question belongs to
whoever prices `src.Symbol` as a memo key.

### R5 — unchanged

### R6 — the writer default

**+ The `--emit` forcing landed.** Revision 2 records that *"the forcing does not exist on
`origin/main`, and is latent only because the default is 1"*. It exists now: `Program.cs:195` is
`options.Emit is null ? options.Writers : 1`, and `:196` says so on the console rather than
silently overriding what was asked for. That was the one prerequisite in the run that was code
rather than measurement.

**Re-cut: the figure has nowhere to be recorded, so the run's output is a default and a test.**
Revision 2's gate is *"the default is whatever this run measures, recorded with corpus, `--jobs`
and figures"*, and `bench/FINDINGS.md` is closed — a single fresh entry in a closed register would
be the only number in it a reader could mistake for current, which is worse than no entry.

**Where each half goes instead.** The **measurement** is a decision record: the sweep's figures,
the corpus and its size in the commit message that changes the default, which is where a reviewer
can argue with them and where `git log` keeps them attached to the line of code they justify. The
**invariants** are tests, and they are the half that can rot: fact totals identical at
`--writers 1` and `--writers 8`, and `--emit` byte-identical across two runs. R7's pass re-derives
the default if the crossover has moved — it is a function of corpus size, not core count
(0.76 at 4,000 files, 1.27 at 24,000), so the number this run picks is right for a stated corpus
and no other.

### R7 — re-measure

**Re-cut: the register is closed, and R7 is what re-opens it.** The run was a list of sections to
re-run — §1 and §2 for the key move, §1/§14/§15 for `--syntax-only`, §1/§2/§6/§11 again if the
benchmark databases predated W12's flush fix. Chasing that list entry by entry is the wrong shape
now, and it was becoming a longer list with every run: the schema the corpus was built over is
deleted, the mode that built it is deleted, the Glean comparison is retired, and **cost-based
reordering ([#18](https://github.com/boxops-uk/fjord/issues/18)) and recursion will change how a
query is planned and what the language can express** — which is most of what the read-path entries
measure.

So `bench/FINDINGS.md` carries one banner saying the whole register is superseded, and R7 is a
**profiling pass nearer 1.0** rather than a repair of the old numbers. What it owes:

- **A corpus.** Named, rebuildable, and produced by the semantic walk — which means it is a
  different and much larger workload than the one every figure was taken over.
- **A question set.** The workload catalogue states one access class per entry; what it does not
  have is a fixture shaped to ask them. `demo.sigla` is the *language* fixture — no search index,
  nothing per line — so two retired workloads have no equivalent and `codesearch` refuses at
  startup for want of one.
- **The three still-open items whose window this is:** `serve --commit-per-block` measured, the
  write rung over a real corpus, and the `--json` baseline (`bench/baselines/<host>.json`) so a
  number can be re-run rather than re-argued.

**What is deliberately not owed: amending the old entries to look current.** A measurement is only
worth reading against the tree that produced it, and rewriting them would destroy the one thing
they are still good for — the lessons the banner lists, each of which is banked in the tree with a
guard.

### R8 — the seam

**+ 8a names the real surface**: `IBlockTarget` and `FactSink` become public in
`Boxops.Fjord.Client` (an accessibility change plus a move); an `IFactWriter` abstraction over the
`Thread[]` writer pool is **new work**, not a re-marking, and is priced as such or dropped.

**Re-cut: the gate's first clause is retired and its third is the only one that tested anything.**
Revision 2 accepts R8 on *"Sealed identity unchanged; the Glean target rides 8a unmodified; Run 9's
converter consumes the published write seam from outside the assembly"*. The Glean target is gone
(D14), and it was carrying more weight than it looked: it was the run's only *existing* external
consumer, so "the seam is usable from outside" had something to hold it up on the day R8 landed
rather than on the day R9 did.

**The replacement is a consumer this repository keeps.** `Boxops.Fjord.Tests` is outside
`Boxops.Fjord.Indexer` and R0.5 built it: a test that writes a small block set through the
published seam alone — no `InternalsVisibleTo`, no type from the indexer assembly — is the same
claim, checked on every run instead of once. If that test needs an internal, the seam is not
published yet, which is the finding rather than a nuisance.

**Gate.** Sealed identity unchanged; a fact stream written through the published seam from
`Boxops.Fjord.Tests` with no access to indexer internals; and R9's converter consumes that same
surface from its own assembly, which remains the strongest form of the claim and is no longer the
only one.

### R9 — SCIP as an ingestion path

**+ Re-pointed at `codemarkup`, and its gate is re-cut.** Revision 2's stated limit is that R9's
gate is written against `src.*`-only viewer routes, *"so the converter must synthesise the whole
source layer"* — inventing `src.Decl`, `src.Module` and `src.SearchByName` for a language it has
no compiler for. With `codemarkup`, the converter fills only what SCIP actually contains:
`src.File`, the line table, `codemarkup.Definition`, `FileXRef`, `SymbolXRef`, `SearchEntry` —
each a direct transcription of an `Occurrence` or a `SymbolInformation`. **This is a reduction in
R9's scope.**

**And the gate itself has to move**, because `fjord-viewer` is retired
([D11](OPEN-QUESTIONS.md)). Revision 2 accepts R9 on *"the viewer answers `/symbol/{name}` against
a converted index"*; there is no viewer to answer it, and hanging a converter's acceptance on the
browser replacement's schedule would be a dependency nobody chose.

**The replacement gate is the converter's output, asserted directly.** A database built by the
converter answers a fixed set of `codemarkup` queries with stated rows: go-to-definition, every
reference in one file in position order, find-references across files, and a prefix search. Better
than the old gate on its own terms — it tests the converter rather than a UI, it fails inside the
converter's own suite rather than through a web request, and it does not go red because somebody
changed a stylesheet. R9 therefore depends on **W8**, and no longer on W11.

**+ A SCIP converter fills the style layer for free.** A SCIP `Occurrence` carries a symbol *and* a
syntax kind over one span, so one pass fills both the cross-reference layer and
`src.FileLineStyles`.

---

## C · What review #34 asked for that revision 2 still does not carry

Recorded so the ledger is complete; each is either amended above or is a decision for the morning.

| #34's ask | Status |
|---|---|
| `--syntax-only`'s namespace derivation and rollback story | **moot — R3.7 deletes the mode** |
| `config.Setting` cannot record a per-project axis | **W7** states it in the schema comment, with the two per-fact escapes |
| Run 9's two producers must agree on a descriptor form | **accepted as unproven (D6)** — W8 narrows it (a SCIP converter need not write descriptors into `src.Decl` at all) but does not close it; it stays in revision 2's *"does not prove"* list |
| SCIP symbols are package-scoped, not repo-scoped (C6) | **answered and accepted (D6)** — by **W7**'s `repo`/`revision` and `src.FileOrigin`, not by keying the symbol |
| the `obj/` filter deserves a guard test | still open, still cheap: removing the filter would silently make every future golden SDK-dependent |
