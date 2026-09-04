# W15 · `code.sigla` retires, and the C# indexer writes the new set

| | |
|---|---|
| **Decision** | [D12](OPEN-QUESTIONS.md) — `code.sigla` retires. There are no consumers, so the freedom to break it exists now and will not later |
| **Area** | `clients/dotnet/Boxops.Fjord.Indexer`, `schemas/`, `crates/fjord-cli`, `crates/fjord-client`, `bench/` |
| **Depends on** | **W6, W7, W8, W9** — the set it moves onto. All landed |
| **Cancels** | **R4** and its sub-runs 4a/4b/4d/4e. R4.0 and R4c survive, re-aimed |
| **Fingerprint** | it *is* the last fingerprint move — after it, `code.sigla` has none |

## The finding that reorganises the rest of the programme

**Revision 2's Run 4 exists to give `src.Decl` a semantic key**, replacing `{module, name, line}`
with `{module, name, descriptor}` so that reformatting a file does not change what its declarations
are. Its analysis lands on the disambiguator: `GetDocumentationCommentId()`, because user-defined
conversion operators overload on return type and a SCIP method descriptor reserves no place for one.

`csharp.sigla` — landed in W9 — **already keys on exactly that**:

```
predicate Method : { name, containingType, returnType, isStatic, declaredAccessibility, docId : string }
```

So R4 would spend a flag day, a 71-file migration and a re-baselined identity to give a schema the
identity that its replacement already has, three weeks before that schema is deleted. **R4 is
cancelled.** What survives of it:

- **R4.0's conflict census**, re-aimed. The question is unchanged and still owed — *do two
  declarations reach one entity key* — but it is asked against `csharp.Method`'s real key rather
  than against a candidate one, and it is a gate for the same reason: after the walk is
  restructured there is no other detector.
- **R4c's Glean union arm.** `csharp.Definition` is a union in a key, so `GleanFacts.WriteValue`
  still throws on the first fact without it. It moves here rather than into a cancelled run.

## The ordering inverts

Revision 2 sequences the loader fixes first and the key move in the middle. That was right when the
key move was one predicate. It is wrong now, because the two halves of the programme have different
**clocks**:

| | needs the freedom window | can happen any time |
|---|---|---|
| | the schema move, `code.sigla`'s retirement, every Breaking edit | R0–R3 loader fixes, R5 concurrency, R6/R7 measurement, R8 the seam |

Nothing about a retry loop or a striped lock cares whether a consumer exists. Everything about
deleting a schema does. **So the schema move goes first**, and the runs that merely make the indexer
better follow it — which also means R7 re-measures once, against the schema that will still be there,
rather than twice.

## The five runs that replace R4

Each moves one slice of emission and is separately reviewable. The indexer writes **both** models
only between S3 and S5, and that window is deliberately short.

### S1 — the source layer

**Done.** The indexer writes all nine `src.*`, and the gate below is green: it wrote three when
this run opened — `File`, `FileLine` and `FileLineStyles`.

`FileInfo`, `FileLineAt`, `FileDigest` and `FileLanguage` are arithmetic and a hash over text the
walk already has. `FileOrigin` is per-file provenance, written only where an index spans several
repositories. `Symbol` is the SCIP-form symbol string, which R4b introduced and which `codemarkup`
joins on.

**`FileLineStyles` is done, and it is not what this section originally specified.** The run-length
encoder over a fjord kind table is deleted rather than deferred ([D13](OPEN-QUESTIONS.md)): the
declaration is `bytes`, the schema is silent about the contents, and the producer names the format.
`--styles` runs Roslyn's `Classifier` per document and writes LSP `SemanticTokens.data` — LEB128
varints, per line so `deltaLine` is always 0, over a fixed legend of Roslyn's own
`ClassificationTypeNames`. W6 c7's round-trip property went with the format: there is no fjord codec
to round-trip, and the cases that matter (overlapping spans, spans crossing a line boundary, no
token for whitespace) are asserted in `Boxops.Fjord.Tests.SemanticTokensTests`.

**`config.Setting` cannot land here, and that is a sequencing fact rather than a choice.** The
indexer holds `roslyn-lsp-1` as a constant and writes no `config.Setting` fact, so a consumer
receives style bytes with no declared way to read them. But `code.sigla` imports `src` and **not**
`config`, and the indexer writes against `code.sigla`'s fingerprint — so emitting the fact needs
either an `import config` in a schema that is being deleted (a flag day for nothing) or the move
onto the new set. It is therefore **S5's**, listed there, and until S5 the style payload is readable
only by knowing which indexer produced it. Recorded rather than left to be discovered by whoever
writes the first consumer.

**The line table is wrong today, and S1 starts by fixing it.** Roslyn's `SourceText.Lines` ends a
file that ends in a newline with an empty final line, and the walk writes a `FileLine` fact for it —
so nearly every file in every index carries a phantom last line. `src.sigla` says the opposite in as
many words (*"a\nb\n" is two lines, and so is "a\nb"*; `endsInNewline` is what tells them apart)
and `source_layer.rs` pins that reading with hand-built facts, which is why no test caught the
producer disagreeing with it. It is the off-by-one [risk 2](OPEN-QUESTIONS.md) predicted, in the
place it predicted, found by writing `FileInfo.lines` next to it — the two cannot both be right.

**Gate.** Every `src.*` predicate this schema can carry has facts after a fixture run, one query per
predicate asserting rows; the line-table arithmetic is a property over generated text rather than
three examples — a file with and without a trailing newline, CRLF, a real blank final line, non-BMP
text and a clipped line — and `FileLineAt` inverts `FileLine.start` for every row of it.

**How it landed.** The arithmetic is `SourceLayer`, separated from the walk
because a walk needs a workspace and a property does not: the line table with the phantom line gone,
`FileInfo`, `FileLineAt`, the extension-to-language mapping and the digest. Two test files, because
the two halves fail differently — `SourceLayerTests` for the arithmetic (a seeded corpus whose
population is asserted, since a generator that degenerates leaves its properties green and vacuous)
and `SourceWalkTests` for the wiring, taken at the `IBlockTarget` seam so it needs no server. Six of
them fail against the pre-fix line table, which is the check that they have teeth.

Three decisions inside it, each recorded where a second producer will meet it:

- **`FileInfo`, `FileLanguage` and `FileDigest` are not gated by `--no-lines`.** One fact each per
  file; the switch is about the size of the per-line table.
- **The language is by extension, not by the compiler that parsed it**, so the answer is the same
  for a file no compilation reached — and the discriminant is resolved through `CodeIndex`'s own
  vocabulary list rather than transcribed a second time, so a name this producer invents becomes an
  `other` fact rather than a wrong one. Visual Basic is the live case: no alternative exists for it.
- **The digest is SHA-256 over the UTF-8 encoding of the *decoded text***, not over the file's raw
  bytes, because every byte number in the database is an offset into exactly those bytes. A BOM is
  decoded away before any offset is counted, so hashing the file would make one file report two
  lengths. The cost is that `sha256sum` disagrees for a BOM'd file, which is why the value owed to
  `config.Setting {dimension = "digest"}` at S5 must name the input and not just the hash.

**`src.Symbol` is spec-conformant SCIP under this producer's own scheme token, `scip-csharp-2`.**
That is a decision, and it was taken against the reference indexer rather than against the
specification alone, because `scip-dotnet` — verified from its source and its checked-in snapshots —
does two things that a cross-database join key cannot survive:

| What it emits | Why it is unusable here |
|---|---|
| `Model/DiffPaneModel#` for `DiffPlex.DiffBuilder.Model.DiffPaneModel` — the innermost namespace only, because a namespace descriptor is attached to the package rather than to its parent | `A.B.T` and `C.B.T` collide inside one package |
| `nuget . .` as the package of the code being indexed, by default and on purpose | every repository's `Main/Program#` is the same string, and the fan-out joins on exactly this |
| `Overload1(+1).`, counting `ContainingType.GetMembers()` | for a partial class that order follows the order the compiler was handed the files, so one commit has two symbol sets and a sealed identity hashes the facts |

So this producer writes full qualification, the containing assembly's own identity as the package,
and the same `+N` disambiguator format counted over a `docId`-sorted order — sorted on each
sibling's **unreduced, unconstructed definition**, because substitution can make two constructed
overloads share one docId *and* one display string, and a tied sort then falls back to
`GetMembers()`, which is the order the files arrived in. **A different scheme token is the honest
part**: claiming `scip-dotnet` without matching it byte for byte would be worse
than either, and neither difference is repairable downstream — nothing can recover a namespace a
producer never wrote, or a signature from an ordinal. An ingested foreign index keeps its own token,
and a join is within one scheme.

The same ordinal carries an **overloaded indexer**, in a place the grammar does not have one.
Roslyn names every indexer of a type `this[]`, a term descriptor is `<name> '.'` and the
specification's one disambiguator slot is a method's — so `this[int]` beside `this[int, int]` minted
one string for two declarations and killed the run on `codemarkup.Definition`'s conflict, exactly as
two arities of a type name did. It goes inside the name, `` `this[]+1` ``, which is where the arity
went for the same reason; it is empty for the first or only sibling, so a type with one indexer
keeps the string it has. C# permits two same-named members of a type only for methods and indexers,
so *two members of one type* are now separated.

**A partial member is the other half of that, and it is one member rather than two.**
`partial void Ping();` and `partial void Ping() { }` are two declarations of one thing, and a
containing type's `GetMembers()` lists only the first — so the walk reduces every declaration to
that half before it spells or keys anything. Two declarations of one thing *in one file* therefore
fill one `{symbol, file}` key rather than two, which closes the shape for two `partial class` parts
in one file as well: `codemarkup.Definition` carries a span, and its value is now asked of the
member and the file — the member's first declaration in that file — rather than of whichever
declaration the walk was standing on. `csharp.DefinitionLocation` and `codemarkup.FileDefinition`
are keyed per span and still carry every declaration, so no half is unreachable. `PartialMemberTests`
over the `partial` fixture is the gate, through the real program into a real database.

**What is left is two things in two files that mint one string, and the shape is `file`-scoped
types.** `file class Hidden` may be declared once per file in one namespace and the compiler mangles
only the metadata name, so two of them mint one `P/Hidden#` — and the run still dies on
`codemarkup.SymbolInfo`, exit 134, whenever their members differ. It is left open on purpose rather
than patched: a `file` type is file-local by the language's own word, so what is owed is a decision
about whether it has a global name at all — the walk already answers no for everything else that is
file-local, and `codemarkup.FileLocalXRef` is the span-to-span channel.
`ScipSymbolsTests.A_file_local_type_still_takes_one_symbol_for_two_declarations` asserts the
collision as it stands, so closing it is deliberate.

**And nothing in the symbol walk throws any more.** Two rounds of this work argued that the branch
where an ordinal cannot be counted was unreachable — first from `ReducedFrom`, then from
canonicality — and an everyday partial member falsified both, turning `partial void Ping() { }` into
a dead run. A third argument would be worth less than a failure mode that cannot kill a run, so a
symbol this producer cannot spell has none: the declaration keeps its entity and its span, and the
run prints `N symbol(s) not spelled`. `ScipSymbolsTests.A_symbol_this_producer_cannot_spell_is_no_symbol_rather_than_an_exception`
provokes it with a built-in operator, which is a second shape that reached the same branch.

What survives of the instability is stated rather than hidden: an ordinal still renumbers when an
overload that sorts earlier is inserted, so nothing keys on a symbol across revisions —
`csharp.Method` carries `docId` itself and is reached through `csharp.SymbolOf`. What does *not*
survive is the half that was never inherent: an ordinal no longer depends on the order the compiler
was handed the files, for a reference through a constructed type as well as for a declaration. The
reasoning lives in `src.sigla`'s charter, `ScipSymbols`' own, and the tests, rather than in a
decision record.

**A third `config` dimension is now owed at S5.** `symbol-scheme` joins `style-encoding` and
`digest`: three things a consumer must read out of the database and cannot, until the indexer stops
writing a schema that imports no `config`.

**`FileOrigin` is stated by a run, not read from the code.** Which repository and which revision a
directory is a checkout of are facts about the checkout, so `--repo` and `--revision` say them or the
index does not carry them — guessing from a `.git` directory would put something in the index that
the next consumer has to distrust. They are refused singly: `FileOrigin` carries both fields on one
fact, so half a pair writes provenance asserting the other is the empty string, which no consumer
can tell from a gap. For a single checkout it is the same two strings on every file and the
per-database form belongs in `config.Setting` — one more thing S5 owes, and until then this is the
only channel provenance has at all.

**The gate is green, in two halves.** `SourceLayerDatabaseTests` starts a real server, walks a
fixture through the real `Indexer` into it over a socket, and asks one query per predicate against
what came back — a workspace rather than a bare compilation, so the classifier has a `Document` and
the style layer is covered too. It also asserts two answers rather than row counts: that
`FileInfo.lines` equals the number of `FileLine` rows across the whole round trip, and that an
overload's `+1` disambiguator survived being written and read back. The other half is
`source_layer.rs`, which asks the same questions of hand-built facts — the schema answering and the
producer filling it are different claims, and each needs its own test.

**One published number moves and is annotated rather than re-run.** The tour transcript in
`walkthrough.md` was taken before the source layer and is now stale in four ways — the predicate's
name, the predicate count, the fingerprint, and one fewer line fact per file plus the two new
predicates. It is re-run **with R7, after S5**, for D12's reason: the alternative is spending the
same afternoon twice on numbers that are about to move again. The scheme token in it is *not* one of
the stale things: it is a name rather than a measurement, so it was moved to `scip-csharp-2` in
place with the format.

### The order above is wrong past S1, and this is why

**S2, S3 and S4 write predicates the producer's schema cannot reach.** The indexer
handshakes on `code.sigla`'s fingerprint; `code.sigla` imports `src` and nothing else, and every
predicate `CodeIndex` carries is `src.*`. So `msbuild.*`, `csharp.*`, `codemarkup.*` and
`config.Setting` are all unreachable from it, and **S1 was the only S-run that could land against
it**. The handshake compares one whole-schema fingerprint ([D2](OPEN-QUESTIONS.md)), so a narrower
client schema is refused rather than accepted as a subset — there is no partial route.

This also explains a symptom rather than three: `style-encoding`, `digest` and `symbol-scheme` are
all owed to `config` and all blocked on the same switch, not on three separate oversights.

**So the retirement splits, and its first half moves to the front:**

```
S1   the source layer          done
S5a  the switch                the indexer targets the new set; `CodeIndex` becomes its
                               constants; the goldens and the independently-stated schemas
                               follow. This is the flag day, and it unblocks everything
S2 · S3 · S4                   msbuild, csharp, codemarkup — parallel, after S5a
S5b  the deletion              `schemas/code.sigla` goes when nothing references it
R7   re-measure                once, after S5b
```

**S5a targets `schemas/dotnet.sigla`** — `src` + `config` + `msbuild` + `csharp` + `codemarkup`,
**67 predicates**, `0xc20dfe719b04e025`. Not `index.sigla`, and the reason is not the flag day's
ceremony: a client handshakes on one whole-schema fingerprint, so a database created from the
everything-composite obliges every client to state all 138 predicates independently — and for the
.NET client 71 of those are `typescript`, `npm` and `bundle`, whose producer is a consumer's ([D1](OPEN-QUESTIONS.md)).
Hand-maintaining declarations with no producer behind them is a transcription surface, not coverage.
`index.sigla` stays what it is: the composite for an index that genuinely holds several languages,
and the proof that the set composes.

**The C# statement stays a transcription.** Generating it from the resolved Rust schema would make
the two agree by construction, which is the one thing the independent statement exists to prevent —
so the 67 are written from the `.sigla` files by hand.

**And it does not have to arrive all at once.** A client declares only the predicates it writes, and
that is the protocol's contract rather than a shortcut: predicate ids are the client's own, a block
header carries the predicate's **name**, and a nested reference takes its predicate from the field's
declared target — nothing positional crosses the wire. The fingerprint is the database's, carried,
and asserts provenance only. So `DotnetIndex` declares ten predicates against a 67-predicate
database today, and each later layer adds its own with its own gate. Verified rather than assumed:
`A_client_may_declare_only_the_predicates_it_writes`, and a stale fingerprint is still refused, so
declaring less asserts nothing less.

That also settles what checks a transcription, since the fingerprint cannot: **writing a fact of
every declared predicate through a real server and reading it back**. The server decodes against
its own statement, so a shape this side got wrong is refused at the write rather than discovered by
a consumer. Each layer's gate is that round trip.

### S2 — `msbuild.*`

Sixteen predicates, and the producer already reads the data: `Projects.cs` walks `ProjectReference`
and `PackageReference`, `Loader.cs` wires the reference graph by `ProjectId`. This is re-shaping
plus the new `{ file }` identity, and it is the natural companion to **R8b**, which moves
`ProjectInfo.Fact` and `ProjectIndex.Emit` behind an emitter.

**Landed with the switch.** Every reverse question is its own predicate and both project-graph
answers `code.sigla` could not give are asserted: an edge **between two projects**, in both
directions, and one project file reaching one project. `msbuild.Project`'s value side is six
optionals, so a field MSBuild left unset is `nothing` rather than the empty string — the two are
different answers, and a design-time build is what fills `sdk`, `outputType`, `platformTarget` and
`rootNamespace`.

One thing is approximated and says so at the code: **`Package.version` and `PackageReference.range`
are the same string.** The schema wants the resolved version in the identity and the declared range
beside it; this producer has one number, the declared version after central package management, and
the resolved one needs the assets file a design-time build does not read. Improving the identity
later will not disturb the range.

**Gate.** Two evaluations of one `.csproj` reach one project (already a test, W9); the reference
graph has edges between two projects, which `code.sigla`'s build layer never had.

### The switch — what it moved, and what it left

**The indexer targets `dotnet.sigla`, and `code.sigla`'s emission is gone**, both-models window
skipped. `CodeIndex` is deleted; the demo keeps `code.sigla` and its own statement of it, which is
what keeps `golden/blocks.txt` and `byte_identical_with_dotnet.rs` pinning the codec through the
move. The two clients are therefore written against *different* schemas now, and
`the_dotnet_clients_carry_the_fingerprint_the_schema_has` was re-cut to check each against its own —
which also gives `DotnetIndex`'s fingerprint its first mechanical guard.

**`--glean-out` refuses rather than lying.** `fjbench.angle` describes `code.sigla`'s shapes and the
walk now writes `dotnet.sigla`'s, so the translation would emit facts Glean cannot load, silently,
into a directory somebody would then measure. R7 re-establishes it against the new set; the
comparison it exists for is invalidated by the schema move regardless (risk 8).

**Still owed on `csharp`, and each for a stated reason:**

| Predicate | Why not yet |
|---|---|
| `Local` | deliberate. SCIP models a local as an occurrence ordinal that moves when the file is edited, and `codemarkup.FileLocalXRef` answers a file-local jump span to span — so a local needs no global identity. W15 asked whether one is worth emitting at all; this is the answer until a consumer wants otherwise |
| `FunctionPointerType` | **cannot be written**, and the reason is a key rather than a decision: `signature : Method` needs a `csharp.Method`, whose key leads with a containing type, and Roslyn gives a function pointer's signature symbol no containing type, namespace or symbol at all. A member typed as one is dropped and counted like a `dynamic` one; a producer could only fill this predicate if `Method`'s key stopped requiring a container, which is a schema change and so a flag day |
| `MethodInvocationLocation`, `MemberAccessLocation`, `ObjectCreationLocation`, `TypeLocation` | **no longer owed.** Glean's per-kind xref facts, beside the unified `EntityXRef`/`EntityRef` pair that predates them. They did not arrive with S4 as this said they would — they were declared and empty until criterion 7 below closed them, from the same visit and with no fingerprint moved |
| `Implements`, `TypeTypeParameter`, `MethodTypeParameter`, `PropertyParameter` | written by `CsharpEntities.Edges`, which the walk calls per declaration — so they have facts wherever the fixture has the shape |

`bench.sh` and `loadgen` are untouched: they generate synthetic `code.sigla` facts and never run the
indexer, so the benchmark corpus is re-shaped in S5b rather than here.

### S3 — `csharp.*`

Thirty-one predicates and the largest single piece of work in the programme — larger than R1–R3
together. The whole Roslyn entity model: namespaces, qualified names, the four named-type kinds,
methods, fields, properties, parameters, type parameters, locals, the `AType`/`NamedType`/
`Definition` unions, and the definition/reference/location tie-back.

It is not a translation of the existing emission. `src.Decl` is one predicate over every kind of
declaration; `csharp` has a predicate per kind and a union over them, so the walk's `Describe` half
is rewritten rather than adapted.

**Two things to settle inside it rather than discover.** What `EntityXRef`'s `use` span is measured
in — the declared `position-encoding`, which for Roslyn is `utf16`. And whether a `Local` is worth
emitting at all, given `codemarkup.FileLocalXRef` answers the jump span-to-span without one.

**Gate.** R4.0's census, re-aimed: zero entity-key conflicts on a fixture containing two overloads
on one line, a conversion-operator pair, and a type with a same-line constructor — asserted per
predicate. Reformatting the fixture produces an identical set of keys.

**And the census had to be sharpened before it would pass, which is worth recording because the
question as written reads as a defect.** `csharp.Parameter` and `csharp.TypeParameter` are
**structural** identities: neither key names its containing method — `TypeParameter`'s comment says
so in as many words, and `Parameter`'s key is name, type and modifiers — so `M(int a)` and
`SameLine(int a)` reach *one* `Parameter` fact by design, and the ordered edge
`MethodParameter {method, index, parameter}` is what ties one to a method. Run against every
predicate, the census reports those as conflicts. So it runs over the predicates that carry an
identity — the four named types, `Method`, `Field`, `Property`, `Namespace`, `FullName` — and the
sharing is asserted separately, as a claim rather than an excuse.

Both defects the old key had are asserted gone rather than argued: two overloads on one line are two
keys, and reformatting the fixture moves not a single one. The conversion-operator pair is its own
test, since it is the case that needs `docId` at all.

**Closed by a flag day: `csharp.FullName` carries an arity.** `csharp.Class`, `Interface`,
`Record`, `Struct` and `FunctionPointerType` all lead their keys with a `FullName`, and nothing else
in the schema references it — so it *is* a named type's identity, and one field fixes all of them.
It is now `{name, containingNamespace, arity}`, with `name` still leading and `arity` last, which
keeps "the names spelled `Foo`" and "`Foo` in `N` at any arity" ranges and makes "`Foo<T>` in `N`" an
exact seek. `class Result` beside `class Result<T>` is **two** `csharp.Class` facts with a
`csharp.DefinitionLocation` each, and `csharp.SymbolOf` crosses one symbol to each.

**The scalar completes the type-parameter decision rather than reversing it.** The named types carry
their parameters as `TypeTypeParameter` edges because a list cannot lead anything after it in a key.
An arity is that list's scalar summary, and a scalar *can* sit in a key: the edges keep the
parameters, the key gains the count.

`csharp.Name` was left holding the arity-stripped simple name, which is the other thing that could
have moved and the more expensive one — every consumer of a simple name would change meaning,
`NameLowerCase` and the two search predicates included. `csharp.Method` needed nothing either: it
keys on `Name` plus a `docId` that already encodes arity, which is why generic *method* overloads
never merged.

**What the key still has no field for is the assembly.** Two assemblies compiled in one run may each
declare `W.S` — same name, same namespace, same arity, same modifiers — and intern one row, with
their members going too because those key on a containing type that fused. Over the reference corpus
that is **8** merged entities across `Assemblies.Left`/`Assemblies.Right` (two classes, three
methods, one field, two properties), unmoved by the arity field. Closing it is a further key field
and therefore another flag day, and it is the open item this section now carries. The gates are
`ArityPairTests.Each_arity_is_its_own_class_fact_with_its_own_location` and
`EntityKeyCensusTests.Two_arities_are_two_entity_keys_and_a_partial_classs_halves_are_one`, the
second of which holds the fix apart from the regression it resembles: **a partial class is still one
fact with a location per part.**

### S4 — `codemarkup.*`

Ten predicates, and cheap once S3 exists because they are the same facts re-keyed for the question
a UI asks. Each carries the query that would derive it as a comment; while `nyi/derivation` stands,
that comment is the specification the producer is checked against, and a discrepancy is a diff.

**Gate.** The two headline joins in `index.sigla`'s header answer against an indexer-produced
database, not a hand-built one. **Green**, and widened to the questions the layer exists for: go to
definition, every reference in one file resolved to its definition, find-references keyed by the
target, a case-insensitive prefix search, containment in both directions, and the hover card — plus
completeness, because a UI surface with one empty predicate is a UI with one dead feature and the
joins each touch only three or four of the ten.

Two things the gate found, both in the fixture-and-wiring seam rather than the schema:

- **A fixture of declarations produces no cross-references at all.** A class with two members and no
  method bodies made every join pass on an empty collection. The fixture now has bodies, and the
  assertions are `NotEmpty` for exactly that reason.
- **The two layers must be written independently.** A local has no `csharp.Definition` — this
  producer mints none, deliberately — so hanging the `codemarkup` facts off the `csharp` one dropped
  every reference to a local, which is the majority of references in real code and the whole reason
  `FileLocalXRef` exists. They answer different questions and each is now written where it can be.

`nyi/value-field` shaped the queries: a value is fetched whole, so `D.value.span.start` is not
expressible and the assertions project `D.value`. That is the same constraint that makes
`FileXRef` all-key, met from the consumer's side.

### S5 — retire `code.sigla`

Delete it. What moves with it:

| Artifact | What happens |
|---|---|
| `schemas/code.sigla` | deleted |
| `clients/dotnet/.../CodeIndex.cs` | becomes the new set's constants, or is replaced by per-namespace files |
| `Boxops.Fjord.Demo/Program.cs` | its independent restatement follows |
| `clients/dotnet/golden/blocks.txt` | regenerated against the new set |
| `crates/fjord-client/tests/byte_identical_with_dotnet.rs` | its independent statement follows |
| `crates/fjord-cli/src/sample_schema.rs` | reads `index.sigla`, or the C# subset of it |
| `crates/fjord-cli/src/workload.rs`, `examples/loadgen.rs` | the benchmark corpus is re-shaped |
| `clients/dotnet/glean/fjbench.angle` | the Glean translation follows |
| the 71 files matching `src.Decl` | migrate or go |
| `bench/FINDINGS.md` | **every published read number is keyed to predicates that no longer exist** |

**Gate.** `git grep -l 'code\.sigla'` returns only history; the flag-day guards named in
`clients/dotnet/README.md` are green — the script this line named has since been deleted, and each
of its steps that was a correctness claim is now a test in the required job; the suite green.

## The one thing that gets worse

**Every published read measurement is invalidated**, not merely re-baselined. §1's key-order
finding, §2's join costs, §6's 67 q/s mix and §11's ~6,000 q/s are all measured over `src.Decl`,
`src.Ref` and `src.Line` in a corpus this deletes — and the replacement is not the same shape:
`csharp` splits one declaration predicate into a dozen, and `codemarkup` re-keys the same facts a
second time, so the fact count for one corpus goes up rather than staying still.

**Settled by closing the register rather than by annotating it.** The plan here was for `FINDINGS`
to say so at each invalidated entry and for R7 to re-run them after S5 — which is a state this
repository had been in once before (D9's `--syntax-only` deletion) and got through by writing it
down. What changed the answer is that the list kept growing: R3.7 deleted the mode that built the
corpus, D14 retired the comparison, and cost-based reordering and recursion are still ahead of the
read path. So `bench/FINDINGS.md` carries **one** banner rather than a caveat per section, the
whole register is closed, and R7 becomes a profiling pass nearer 1.0 instead of a repair.

## Sequencing

```
S1 src layer ──► S2 msbuild ──► S3 csharp ──► S4 codemarkup ──► S5 retire code.sigla
                                    │                                  │
                                 R4.0 census (re-aimed)                └──► R7 re-measure
                                 R4c  Glean union arm

R0.5 test project ──► R0 ledger ──► R1 · R2 · R3 · R3.6 · R5 · R6 · R8
R3.7 delete --syntax-only  (independent)
R9  SCIP converter  (needs R8a)
```

**R0.5 still comes first in practice**, because every gate above needs somewhere to live and the
.NET side has no test project at all. It is not on the schema clock, but nothing can be *verified*
without it.

## Acceptance criteria

Every sibling item carries this section and this one did not, which left the five runs above with
no list to check the tree against. It is written after the fact and its statuses are as of this
commit; a criterion is a test or a command, never prose.

1. **The source layer is written by a producer and read back over a socket.** All nine `src.*`
   predicates are written by the indexer, and the gate is a round trip rather than a row count:
   `SourceLayerDatabaseTests` walks a fixture, starts a real server and asserts *answers*, with
   the style layer covered in the same pass. The line-table arithmetic is separately pinned over
   hand-built facts in `crates/fjord-cli/tests/source_layer.rs`, because a schema holding a shape
   and a producer filling it are two claims. **Met.**
2. **The offset→line recipe's three cases are pinned, including the empty one.**
   `offset_to_line_has_three_cases_and_the_third_is_empty` asserts an offset at a line's start
   (including the last line's), an offset mid-line, and one strictly past the last line's start.
   **Met** — and it is what corrected the boundary this document and issue #39 both stated
   wrongly as "at *or* past".
3. **What is in a style payload is declared by the database, not by the schema.**
   `config.Setting {dimension = "style-encoding"}` names the format, the legend and the unit its
   columns count in; `SemanticTokensTests` asserts the payload and
   `crates/fjord-cli/tests/config_layer.rs` asserts the setting is readable as a fact. **Met.**
4. **`msbuild.*` answers what `code.sigla` could not.** An edge between two projects, asserted in
   both directions, and two evaluations of one `.csproj` reaching one project —
   `crates/fjord-cli/tests/msbuild_layer.rs` and W9's own gate. **Met.**
5. **The entity key has no conflict on the three cases that broke the old one.**
   `EntityKeyCensusTests` runs the census over the predicates that carry an identity — the four
   named types, `Method`, `Field`, `Property`, `Namespace`, `FullName` — and reports zero
   conflicts over a fixture holding two overloads on one line, a conversion-operator pair, a
   type with a same-line constructor, a type name at two arities and a partial class written
   twice; reformatting the fixture yields an identical key set. The
   structural identities `Parameter` and `TypeParameter` are excluded deliberately and their
   sharing is asserted as its own claim. Two of the three cases are their own tests —
   `Two_overloads_on_one_line_are_two_keys` and
   `Two_conversion_operators_differing_only_in_return_type_are_two_keys`, the second because a
   return-type overload is the case that needs `docId` at all. **Met** (the acceptance audit
   recorded the conversion-operator test as not its own; it is, and it goes red when the
   disambiguator is dropped).
6. **`codemarkup.*` is gated as a UI's surface, end to end.**
   `crates/fjord-cli/tests/codemarkup.rs` asks a real server the questions a UI asks, and
   `CsharpLayerTests`/`crates/fjord-cli/tests/csharp_bridge.rs` hold the layer below it. The two
   layers are written independently, which is asserted rather than assumed: a local has no
   `csharp.Definition`. **Met** — except the completeness half of this gate, which was named
   here and never written, and is what let criterion 7 sit empty through a release. It exists
   now: `PredicateCensusTests` classifies every predicate `DotnetIndex` declares as written,
   excused or owed, and asserts each behaves as classified over a run of the `census` fixture.
7. **The four `csharp` location predicates S4 promised are declared and written.** **Met.**
   They were declared and empty: `ObjectCreationLocation`, `MethodInvocationLocation`,
   `MemberAccessLocation` and `TypeLocation` had an id, a batch entry and a declared type in
   `DotnetIndex.cs`, and no emit site anywhere. `Indexer` writes all four from the visit that
   already writes `EntityXRef` — a creation from its `new`, an invocation from its call, a member
   access from its `.`, and a type from every name that binds to one — and
   `A_creation_an_invocation_a_member_access_and_a_type_are_located_where_written` asserts the
   targets and the spans, in the UTF-8 bytes `config.Setting {dimension = "position-encoding"}`
   declares. **This was not a flag day**, which is worth stating because this list said it would
   be: the schema declares those four already, so their types are in `csharp.sigla`'s fingerprint
   whether or not a producer writes a row. `every_shipped_schema_has_a_recorded_fingerprint`,
   `the_dotnet_clients_carry_the_fingerprint_the_schema_has` and
   `byte_identical_with_the_dotnet_client` are green with no constant re-pasted and no golden
   regenerated. What did move is `Interning_over_the_frozen_corpus_costs_what_it_says_it_costs`,
   by construction — 58 more facts created over the frozen corpus, which is what writing four
   predicates that were empty looks like.
8. **`schemas/code.sigla` is deleted and nothing describes it as live.** The file is gone;
   `git grep -l 'code\.sigla'` returns release notes and plan history — text that names the
   retirement in order to record it — and nothing that instructs a reader to use it.
   `python3 scripts/check-docs.py` is the mechanical half: it sweeps retired *names* as well as
   retired files, so a page that starts describing the deleted schema again fails the required
   job rather than waiting for the next hand sweep. **Met.**
9. **The flag day is walked by guards rather than by a script.** `clients/dotnet/README.md`
   carries the ordered checklist and names the test that checks each step —
   `every_shipped_schema_has_a_recorded_fingerprint`,
   `the_dotnet_clients_carry_the_fingerprint_the_schema_has` per schema and by name, and
   `byte_identical_with_the_dotnet_client` for the goldens — and each is in the required `test`
   job. **Met.**
