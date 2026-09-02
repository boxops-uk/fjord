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

**`src.Symbol` is spec-conformant SCIP under this producer's own scheme token, `scip-csharp`.**
That is a decision, and it was taken against the reference indexer rather than against the
specification alone, because `scip-dotnet` — verified from its source and its checked-in snapshots —
does two things that a cross-database join key cannot survive:

| What it emits | Why it is unusable here |
|---|---|
| `Model/DiffPaneModel#` for `DiffPlex.DiffBuilder.Model.DiffPaneModel` — the innermost namespace only, because a namespace descriptor is attached to the package rather than to its parent | `A.B.T` and `C.B.T` collide inside one package |
| `nuget . .` as the package of the code being indexed, by default and on purpose | every repository's `Main/Program#` is the same string, and the fan-out joins on exactly this |
| `Overload1(+1).`, counting `ContainingType.GetMembers()` | for a partial class that order follows the order the compiler was handed the files, so one commit has two symbol sets and a sealed identity hashes the facts |

So this producer writes full qualification, the containing assembly's own identity as the package,
and the same `+N` disambiguator format counted over a `docId`-sorted order. **A different scheme
token is the honest part**: claiming `scip-dotnet` without matching it byte for byte would be worse
than either, and neither difference is repairable downstream — nothing can recover a namespace a
producer never wrote, or a signature from an ordinal. An ingested foreign index keeps its own token,
and a join is within one scheme.

What survives of the instability is stated rather than hidden: an ordinal still renumbers when an
overload that sorts earlier is inserted, so nothing keys on a symbol across revisions —
`csharp.Method` carries `docId` itself and is reached through `csharp.SymbolOf`. The reasoning lives
in `src.sigla`'s charter, `ScipSymbols`' own, and twelve tests, rather than in a decision record.

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
same afternoon twice on numbers that are about to move again.

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

**Blocked on S5a**, like S3 and S4: `msbuild` is not in the producer's schema until the switch.

**Gate.** Two evaluations of one `.csproj` reach one project (already a test, W9); the reference
graph has edges between two projects, which `code.sigla`'s build layer never had.

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

### S4 — `codemarkup.*`

Ten predicates, and cheap once S3 exists because they are the same facts re-keyed for the question
a UI asks. Each carries the query that would derive it as a comment; while `nyi/derivation` stands,
that comment is the specification the producer is checked against, and a discrepancy is a diff.

**Gate.** The two headline joins in `index.sigla`'s header answer against an indexer-produced
database, not a hand-built one.

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

**Gate.** `git grep -l 'code\.sigla'` returns only history; `scripts/flag-day.sh` walks clean; the
suite green.

## The one thing that gets worse

**Every published read measurement is invalidated**, not merely re-baselined. §1's key-order
finding, §2's join costs, §6's 67 q/s mix and §11's ~6,000 q/s are all measured over `src.Decl`,
`src.Ref` and `src.Line` in a corpus this deletes — and the replacement is not the same shape:
`csharp` splits one declaration predicate into a dozen, and `codemarkup` re-keys the same facts a
second time, so the fact count for one corpus goes up rather than staying still.

R7 re-runs them, and it must run **after** S5 rather than after R4. Until then the record carries
numbers no command can reproduce, and `FINDINGS` says so at each of them — which is a state this
repository has already been in once (D9's `--syntax-only` deletion) and got through by writing it
down rather than by pretending.

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
