# W9 · The language layers, and `index.sigla` — the composite that proves the set resolves

| | |
|---|---|
| **Issue** | the five schemas posted as comments on [#40](https://github.com/boxops-uk/fjord/issues/40) — `[05]` in the batch, never filed as its own issue |
| **Area** | `schemas/` |
| **Depends on** | **W6** (all five import `src`), **W8** (the bridges target `codemarkup`), **W1** (the `csharp` bridges key on a union and are not joinable without it), W4 for the file layout |
| **Blocks** | nothing in this plan. **R9's re-pointed gate is W11's**, not this item's — a SCIP converter fills `codemarkup`, never a language layer |
| **Invariants** | I10 — every vocabulary in these files freezes its discriminants on landing |
| **Fingerprint** | five new namespaces; nothing existing moves |

## Claim

The set **composes**: nine files resolve into one schema, a database can be created from it, and the
two joins that today are written in JavaScript — a reference resolved to its definition across
languages, and a symbol joined to the project that compiled its file — typecheck, plan and run.

## What lands

| file | own predicates | imports |
|---|---|---|
| `csharp.sigla` | 31 | `src` |
| `msbuild.sigla` | 16 | `src` |
| `typescript.sigla` | 35 | `src`, `npm` |
| `npm.sigla` | 14 | `src` |
| `bundle.sigla` | 22 | `src`, `npm` |
| `index.sigla` | **0** — a composite declares no predicates of its own | all eight |

With `src` (9) and `config` (1) from W6/W7 and `codemarkup` (10) from W8, the composite is
**138 predicates in 9 files**, which is the issue's own figure and arithmetic that checks:
9+1+10+16+31+14+22+35 = 138.

Three structural moves come with them, and each is the same seam drawn twice:

- **The MSBuild project graph leaves `csharp`** into `msbuild.sigla` — a different producer fills
  it (revision 2's own Run 8b seam), it is not C# (a solution compiles F# and VB), and it was
  **missing every edge between two projects**. `ProjectReference`, `PackageReference`,
  `AssemblyReference` and the assembly crossing come back; the join from a declaration to the project
  that compiled it goes `src.Location` → `src.File` → `msbuild.SourceFileToProject`, and typechecks
  only because the file predicate is shared.
- **The package layer leaves `typescript`** into `npm.sigla`, modelled on Yarn's own vocabulary
  (locator / descriptor / resolution / workspace / project) so the two agree by construction rather
  than by translation.
- **The bundler layer leaves `typescript`** into `bundle.sigla`, whose shape is
  `getStats().toJson()` — webpack's as much as Rspack's, hence the namespace.

An MSBuild project's identity also changes from `csharp.Project`'s seven-field key to
`{ file : src.File }` with the evaluated attributes as values, on the same argument revision 2 makes
about `src.Decl`: **an identity must not carry an evaluation detail**, or re-evaluating under a
different SDK mints a different project and every reference edge points at whichever variant the
walk reached first.

## The scope decision, taken

These five files are **118 predicates that nothing in this repository populates**, and
`csharp.sigla` is a richer parallel model of the language `code.sigla` already models —
`index.sigla` acknowledges it by deliberately not importing `code.sigla`.

**Decision: ship all five as default schemas, and support the C# surface first.** So:

- All five land in `schemas/`. The cost is low: nothing in `schemas/` is embedded in a binary except
  by an explicit `include_str!`, and the release artifact ships **no** schema files at all — only the
  `fjord` and `fjord-viewer` binaries.
- **`csharp` and `msbuild` are the supported pair to begin with** — the ones a first-party producer
  writes and the ones the acceptance criteria below exercise end to end. `typescript`, `npm` and
  `bundle` ship beside them as declared, checked, fingerprint-recorded schemas whose producers are a
  consumer's.
- **Say what that means**, in one sentence per file and once in the book: fjord defines the shapes
  and the vocabularies' numbering. It does not promise to track LSP, SCIP, Yarn or webpack releases
  on any schedule; a vocabulary that has to grow does so through its `other : string = 0` valve
  rather than through a Breaking edit.
- **Do not embed them.** `code.sigla` and `demo.sigla` stay the only two schemas a binary carries.

## Acceptance criteria

1. **The set resolves.** `fjord --schema-path schemas schema check schemas/index.sigla` reports
   **138 predicate(s) in 9 file(s)** and a fingerprint, recorded in the golden table (W6 c3). Each
   of the five files also checks standalone against its own imports.
2. **The composite creates a database**, and `fjord describe index --schema` round-trips text that
   `create --schema` would accept.
3. **The two headline joins run.** Both queries in `index.sigla`'s header are integration tests
   against a database created from it and seeded with a hand-built fact set:
   - every reference in one file resolved to where its target is defined, **for any language**, from
     one query path;
   - a symbol, its declaration site, and the project that compiled the file it sits in — across
     three namespaces filled by three different producers.
   Each asserts rows, not merely that it typechecks. **These two are the whole claim of the set**;
   everything else in this item is in service of them.
4. **The `csharp` bridges are exercised, and they are W1's payoff.**
   `csharp.EntityXRef {file = F, use = U, target = D}; csharp.DefinitionLocation {definition = D,
   location = L}` — a union-typed variable shared by two generators — runs and returns rows. Before
   W1 this is `reject/type-mismatch`; a test asserting it runs is the end-to-end proof of that work
   item, and the narrowed eleven-query form is pinned beside it.
5. **Every vocabulary is transcribed, not remembered.** `Accessibility`, `MethodKind`, `RefKind`,
   `Variance`, `codemarkup.Kind`, `codemarkup.Role`, `src.Language` — each carries the source it was
   transcribed from in its comment, and a test asserts each union's discriminants are contiguous
   from their stated base and unique (a mechanical guard against a transcription slip, which I10
   makes permanent).
6. **A partial index is expressible and is tested.** A database created from `npm.sigla` alone —
   a repository with a lockfile and no TypeScript — creates, ingests and answers. This is the
   argument for the split, so it is a test.
7. **`msbuild.Project`'s new identity is exercised, and the conflict is a rejection.** Two
   evaluations of one `.csproj` under different SDKs reach **one** key with two different value
   sets, and the second is **rejected** — deterministically, whichever order they arrive in. That is
   `ops-I4`'s rule, not a choice this item gets to make: *"a conflict rule that picks a winner … is
   the one thing `ops-I4` really forbids"* (`PLAN.md:3184`), and `ops-I5`'s dedup covers only the
   identical case. Assert the rejection **and** that the two evaluations did not mint two projects,
   which is the defect the new identity closes.
8. **No `code.sigla` predicate is touched.** `schema diff` between shipped and new `code.sigla`
   reports only what W6 added.
9. **The book lists the set** with one paragraph per namespace and the "reference schemas" status
   sentence (W10); `website/build.py --strict` clean.
10. **The full gate**: `cargo test`, clippy/fmt, `check-guards.py`.

## Traps

- **Landing all five at once is not a reviewable diff.** AGENTS.md's dominant failure mode is *"a
  large, mostly-correct diff whose 10%-wrong part is expensive to find"*, and this is 118 predicates
  of hand-transcribed vocabulary. Land them one file per commit, in the order
  `msbuild → npm → bundle → csharp → typescript → index`, each with its own resolve-and-fingerprint
  test, and the composite last.
- **`typescript.sigla` imports `npm`, and `bundle` imports both.** The dependency points one way —
  a package graph is meaningful with no bundler; a bundle is not meaningful without packages — and
  reversing it would make a lockfile-only index undeclarable.
- **Two module graphs disagree on purpose.** `bundle.ModuleImport` is the bundler's post-resolution
  graph; `typescript.FileImport` is the source graph. A module in one and not the other is a
  tree-shaken module, and that is the interesting answer, not a bug.
