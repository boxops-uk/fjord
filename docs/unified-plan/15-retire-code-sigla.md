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

The indexer writes all nine `src.*`. It writes **three** today — `File`, `FileLine` and
`FileLineStyles`.

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

**What that leaves owed here.** Nothing consumes `style-encoding` yet, and the indexer holds
`roslyn-lsp-1` as a constant without writing the `config.Setting` fact that publishes it — so a
consumer today receives bytes it has no declared way to read. Emitting `config.Setting` is not on
any run's list and belongs on this one, with the six remaining `src.*`.

**Gate.** Every `src.*` predicate has facts after a fixture run, one query per predicate asserting
rows; and a database the indexer wrote with `--styles` carries
`config.Setting {dimension = "style-encoding", value = "roslyn-lsp-1"}`, so the payload is readable
by the contract rather than by knowing which indexer produced it.

### S2 — `msbuild.*`

Sixteen predicates, and the producer already reads the data: `Projects.cs` walks `ProjectReference`
and `PackageReference`, `Loader.cs` wires the reference graph by `ProjectId`. This is re-shaping
plus the new `{ file }` identity, and it is the natural companion to **R8b**, which moves
`ProjectInfo.Fact` and `ProjectIndex.Emit` behind an emitter.

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
