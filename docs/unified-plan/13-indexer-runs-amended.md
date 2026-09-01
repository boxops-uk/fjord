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

### R0 — the ledger

**+ A re-baseline is a reviewed change with a stated cause.** The primary assertion is the sealed
identity, and the identity hashes values — so **every schema change moves it**. Runs that move it and
do not currently say so: **3.5** (fan-out changes which facts exist per database), **4**, and
anything landing from **W6/W9**. **3.6** claims the identity is *unchanged*, which is the claim worth
keeping — if it moves, something else broke. Add one line to Run 0: a moved baseline is accepted with
a written cause, never as a diff someone approves.

**+ The SDK pin applies to every later baseline, not only Run 0's.** `global.json` is `10.0.100`
with `rollForward: latestFeature`, so a 10.0.2xx SDK brings different reference assemblies and
`src.TypeOf` stores `type.ToDisplayString()` — a *value*, which a sealed identity hashes.

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

### R5, R6 — unchanged

### R7 — re-measure

**+ W12 may add to the re-run list.** If the benchmark databases were sealed before W12's flush fix,
their read numbers were measured against a partly-unmerged tree, which `compact`'s own doc prices at
up to 180× on a re-seek. W12 criterion 7 establishes whether they were; if so, `§1`, `§2`, `§6` and
`§11` join R7's list for that reason as well as for the key move.

**+ Two more sections join the list, for reasons that are not the key move.** §1, §14 and §15 were
measured with `--syntax-only` (`FINDINGS:20`, `:932`, `:965`), which R3.7 deletes — so they are
re-run against a semantic walk on a named corpus, and the old figures are marked as produced by a
mode that no longer exists. §1's `src.Line` row (`FINDINGS:70` — 8,583,810 rows) is re-run again for
a second reason: W6 replaces that predicate with `src.FileLine`, whose value is four fields where it
was one, so both the corpus's size and its per-row read cost move.

**+ The three still-open items whose window is Runs 6–7** are carried here so they are not lost:
`serve --commit-per-block` measured, the write rung over a real corpus, and the `--json` baseline.

### R8 — the seam

**+ 8a names the real surface**: `IBlockTarget` and `FactSink` become public in
`Boxops.Fjord.Client` (an accessibility change plus a move); an `IFactWriter` abstraction over the
`Thread[]` writer pool is **new work**, not a re-marking, and is priced as such or dropped.

### R9 — SCIP as an ingestion path

**+ Re-pointed at `codemarkup` routes.** Revision 2's stated limit is that R9's gate is written
against `src.*`-only viewer routes, *"so the converter must synthesise the whole source layer"* —
inventing `src.Decl`, `src.Module` and `src.SearchByName` for a language it has no compiler for.
With W11's `codemarkup` routes, the converter fills only what SCIP actually contains: `src.File`,
the line table, `codemarkup.Definition`, `FileXRef`, `SymbolXRef`, `SearchEntry` — each a direct
transcription of a SCIP `Occurrence` or `SymbolInformation`. **This is a reduction in R9's scope**,
and it makes R9 depend on **W11**.

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
