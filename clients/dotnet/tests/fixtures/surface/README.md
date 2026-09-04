# `surface` — the reference corpus, and the census that says what it covers

Twenty-nine projects and 446 C# files whose job is to exercise **the entire C# language
surface**, so that this repository's indexer can be gated against the language rather than against
the shapes somebody remembered — and five more, held back under `quarantine/`, because they cannot
be indexed at all.

**The population is external, and that is the whole point.** `POPULATION.tsv` holds 1,129 rows:
one per numbered heading of ECMA-334 draft-v9 — clauses 1 to 24 and annexes A to E — plus one per
`SyntaxKind`, `SymbolKind`, `TypeKind`, `MethodKind` and `LanguageVersion` member of the pinned
compiler, read by reflection, plus one per language feature the standard predates (C# 10 to 14).
`SURFACE.tsv` is this corpus's answer to each of the 1,129, and every row is `exercised`,
`not-applicable`, `quarantined` or `unbuildable` with a reason a reader can check.

A list of features to cover is exhaustive only until the next thing nobody thought of, and this
repository has been bitten by that seven times — seven symbol collisions, each from a shape no
list held. So the population is read out of a document and a compiler, and its row count is
matched against the source's own structure: `grep '^#\+ '` per clause file, one entry per heading.

**The method caught a hole the first time it was applied.** Clause 18, *Extended indexing and
slicing*, had been read by nobody: the survey that produced this population said the standard had
19 clause files when it has 24, and the miscount was the gap restated as a fact. Clause 18 is
`Index`, `Range`, and the pattern-based binding of `x[^1]` and `x[1..2]` to a `Count`/`Length`
property, an indexer, and a `Slice` method whose name never appears at the use site. It carries
four defects that were confirmed by probe rather than argued:

- every indexer in a type mints one symbol string, because `Descriptor` routes `SymbolKind.Property`
  through the term arm with no disambiguator and Roslyn's `Name` for every indexer is `this[]`;
- **no element access writes a reference at all** — the walk dispatches on `SimpleNameSyntax`, and
  the only such node in `s[1..^1]` is the receiver, so both bound declarations read as dead;
- `..` binds to four different corelib members chosen by *which operand is absent*;
- a corelib target's symbol string carries the containing assembly's identity, which changes with
  the target framework — one unchanged source, three spellings of `System.Index`.

## Two halves, split on a property of the database rather than on taste

A key here holds one value. Two declarations that mint one identity string with different values
is a **refused write**: `FactSink` latches the refusal, the next flush throws, and the run dies
part-way through — so every fact after it in the walk is lost. One such shape anywhere in a
solution makes every *other* shape in that solution unmeasurable.

So the corpus is split:

- **`Surface.slnx` — 29 projects that index to completion.** Everything that answers *wrongly* or
  answers *nothing* lives here, because those are silent: they merge or they write nothing, and
  they cost the run nothing. They are most of what the corpus is for.
- **`quarantine/` — five projects, one run-killer each, excluded from the solution** and indexed
  one at a time, alone, so the refusal names itself instead of being masked by whichever fired
  first.

**Where a project boundary exists, a build property or an assembly boundary forced it**, and the
rest is organisation. Most projects set their own `Nullable` and several set `AllowUnsafeBlocks`,
`LangVersion` or `GenerateDocumentationFile` — those could have been folders. Three things could
not:

- **an entry point is one per assembly**, so `Entry`, `Lexical` and `SyntaxForms` each set
  `OutputType=Exe` and each needs its own project to hold a `Program`/`<Main>$` pair the compiler
  invents;
- **C# 14 needs `LangVersion=preview`** under the pinned Roslyn, which maps `latest` to C# 13, so
  `Preview` and `quarantine/Partial` hold the shapes the default version rejects;
- **`Assemblies.Left` and `Assemblies.Right` declare one type name twice with no reference between
  them**, which is the whole point of the pair — the entity layer's type identity carries no
  assembly, so two assemblies' `W.S` fuse into one row. A `ProjectReference` would make it
  CS0433 instead of a fusion.

## What it is built to have, and where each property is asserted

Nothing here is edited to make a test pass. Anything a new test needs belongs in a new file, and a
file changed for another reason moves every baseline recorded against it.

| Property | Asserted by |
| --- | --- |
| Every population id has exactly one verdict, and no verdict invents an id | `SurfaceCensusTests.The_census_answers_the_whole_population_and_nothing_else` |
| Nothing an index holds a fact about is answered `not-applicable` | `SurfaceCensusTests.A_member_an_index_holds_a_fact_about_is_never_answered_not_applicable` |
| Every `exercised` verdict names a file that exists | `SurfaceCensusTests.Every_verdict_is_one_of_the_four_and_names_evidence_that_exists` |
| Every project is in the solution, or deliberately out of it | `SurfaceCensusTests.The_solution_holds_every_project_except_the_quarantined_ones` |
| The whole corpus indexes to completion, and every file it checks in is in the database | `SurfaceCorpusTests.The_whole_corpus_indexes_to_completion` |
| Each quarantined shape refuses its write, on the predicate it is quarantined for | `SurfaceCorpusTests.A_quarantined_shape_still_refuses_its_write` |
| Every predicate this producer fills has rows over the corpus | `SurfaceCorpusTests.Every_predicate_this_producer_fills_has_rows_over_the_corpus` |

The second row is the one that refuses a lazy census. Once the population records that an index
holds a fact about a member, `not-applicable` is a contradiction — so a member this corpus does
not exercise has to be `quarantined` (exercising it kills the run) or `unbuildable` (no compiling
C# produces it), and both are claims a reader can check. Without it a census passes by writing
"not applicable" against everything hard.

## What it measured on arrival

`Surface.slnx`, indexed with `--framework net10.0 --styles` and a stated repo and revision:
**exit 0, 411,877 facts, 12,210 distinct `src.Symbol` strings, 481 `src.File` rows** — 446 C#
files, 34 project files and the solution, which is every file the corpus checks in outside
`quarantine/` and nothing else. All 34 project files appear even though the solution lists 29: the
build layer describes the projects it finds under the index root, so the five quarantined ones get
a `msbuild.Project` fact while their C# is never walked.

The flags matter and the gate passes them. Without `--styles` the per-line semantic-token pass
never runs and `src.FileLineStyles` is empty; without `--repo`/`--revision` there is no provenance
for `src.FileOrigin`, because a fixture copied out of the tree is not a checkout. A run without
them leaves two predicates empty that the audit classifies as written, so the corpus would measure
less than it appears to.

The five quarantined shapes, each indexed alone. **Four are repaired and one is not**, and the
table is the record of which — a commit that fixes one moves its row here, in the same commit, by
deleting it from `A_quarantined_shape_still_refuses_its_write` and asserting completion in its
place.

| Project | The shape | Was refused on | Now |
| --- | --- | --- | --- |
| `quarantine/Arity` | one type name at two arities | `codemarkup.Definition` | indexes, 30 symbols |
| `quarantine/Terms` | two indexers in one type | `codemarkup.Definition` | indexes, 20 symbols |
| `quarantine/Partial` | a partial member's two halves | `codemarkup.Definition` | indexes, 22 symbols |
| `quarantine/Ordinal` | a partial method beside a same-named overload | `codemarkup.SymbolInfo` | indexes, 17 symbols |
| `quarantine/FileLocal` | `file class C` in two files | `codemarkup.SymbolInfo` | **still refuses** |

**Which predicate refused it is the content of the claim.** `Definition` is keyed
`{symbol, file}`, so those three needed two declarations of one symbol in *one* file. `SymbolInfo`
is keyed on the symbol alone, which is why the other two fired *across* files — and why a fix that
deduplicated `Definition` per file would have left them live while looking like a repair.

**The symbol counts are a floor, and they are the other half of the claim.** A conflict can be
made to disappear by writing fewer facts: drop one of the two declarations that wanted one key and
the run completes, having lost exactly what the fix was for. So each repaired row records how many
distinct `src.Symbol` strings its project mints, and the gate asserts no fewer.

`FileLocal` remains because a file-scoped type's identity has no file segment: Roslyn keeps the
restriction only in `MetadataName` (`<F0>…__C`), while `Name`, `ToDisplayString()` and
`GetDocumentationCommentId()` are all plain `C`. The descriptor path has no segment for the file,
so two `file class C` in two files and any ordinary `C` beside them mint one string. The fix
borrows `MetadataName`'s per-file prefix, and it is not in this commit.

## What it deliberately does not reach

Three of the schema's predicates cannot be filled from here and the gate asserts they are
**empty** rather than skipping them: `msbuild.Package`, `msbuild.PackageReference` and
`msbuild.PackageDependent`. A corpus here takes no `PackageReference`, because CI has no network
and a fixture that reaches nuget.org is a test that fails on a train — the `census` fixture is
where those three are checked. Asserting the emptiness as an equality makes the pair
self-correcting: a fourth predicate falling empty fails the gate, and so does somebody adding a
package reference and leaving the list alone.

`CrossProject` is the corpus's **only** `ProjectReference`, and it is deliberate in both
directions. Every other project stands alone, and `Assemblies.Left`/`Assemblies.Right` must —
they declare one type name twice and a reference between them would not compile. But a use whose
declaration lives in another project's source is a different path through the producer: the bound
symbol arrives through a compilation reference rather than through this project's own syntax
trees, and a symbol string minted there has to equal the one minted where the declaration was
walked. That equality is the whole promise of a cross-database name, and a corpus of mutually
blind projects never tests it.

Two predicates are outside the coverage claim because the producer declines to write them:
`csharp.Local`, since a local gets no global name by decision, and `csharp.FunctionPointerType`,
which cannot be keyed — its `signature` is a `csharp.Method` whose key leads with a containing
type, and a function pointer's signature symbol has no containing type, namespace or symbol at
all. The corpus declares 34 function pointers and confirms it: the predicate is empty and the
drop is counted instead.

## Adding to it

The standard moves, so the population does. Re-derive a clause's rows from
`https://raw.githubusercontent.com/dotnet/csharpstandard/draft-v9/standard/`, match the entry
count against that file's heading tree, and add the rows to `POPULATION.tsv` — the census gate
will then fail until somebody has decided what the corpus does about each. That failure is the
feature.

Two rules for the C# itself: **no `PackageReference` and no `ProjectReference`**, because CI has no
network and a fixture that reaches nuget.org is a test that fails on a train; and **nothing that
kills the run** outside `quarantine/`, because one refused write hides everything behind it.
