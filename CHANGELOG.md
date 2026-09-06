# Changelog

Fjord DB. Dates are the release date; `0.x` is a pre-release series and the on-disk format is
not promised to be stable across its minor versions — a database written by one is read by the
version that wrote it. What *is* promised inside a series is the append-only discipline the
format stamp and the marker table enforce: nothing already written is renumbered.

## 0.2.0 — 2026-09-06

**Breaking on both sides of the wire, and on disk.** The protocol is 4, the shipped schemas
are a new set, and several entity identities moved — so a client built against 0.1.0 is
refused at the handshake, and a database written by 0.1.0 is not one this reads. Rebuild the
clients and recreate the indexes; there is no migration and the series does not promise one.

The round's shape: `schemas/code.sigla` is replaced by a composable set — a shared source
layer, a UI surface, the project graph, the package layer, two language layers and the
composite that imports them — and the indexer that writes it is told what to index rather
than guessing, and creates the databases it writes to. Four query defects are fixed, two of
which answered *wrong rows* rather than failing. `fjord-viewer` and the Glean comparison are
retired.

### `--exclude` is deleted, having done nothing since `--syntax-only` went

The flag was parsed and never read. Its implementation lived inside the syntax-only
file-discovery walk and went with it when that was deleted, leaving the option, its parse
arm and a doc paragraph describing behaviour the tool no longer had — so
`--exclude src/tests` silently indexed `src/tests`.

**It is not coming back in that form.** The walk indexes what a design-time build says a
project compiles; dropping a subtree from that would index a program different from the one
that builds, which is the thing this indexer refuses to do everywhere else. A corpus
decision belongs in what you point it at — name the solutions or projects you want with
`--sln` and `--project`.

`bench/FINDINGS.md` §16 and §17 record measurements taken with it while it worked; those
figures stand as history, and the register is closed until a 1.0 pass either way.

### The indexer is told what to index · **change your invocations**

`--source` is deleted. Name what to index with `--sln` and `--project`, both repeatable and
unioned into one index:

    fjord-indexer --sln ~/src/Repo/Repo.slnx --project ~/src/Repo/tools/Gen.csproj \
        --root ~/src/Repo --at /run/fjord.sock//code

**It used to guess.** A directory was searched for the first `.slnx`, then `.sln`, then
`.csproj` by ordinal sort, and a repository with two solutions got whichever sorted first
without saying so: `ShareX.ImageEditor.sln` beside `ShareX.sln` indexed **three of thirteen
projects and exited 0 under `--strict`**, because "the index is complete" could only ever
mean "complete for the entry point I chose". Two of twelve repositories in a compatibility
sweep also kept their solution below the top directory and could not be indexed at all.

`--root` is one value — every `src.File` is a path relative to it, and
`config.Setting {dimension = "index-root"}` records which — so with more than one input it
must be given rather than guessed. Two roots mean two databases, which is a second run.
Solution membership is per solution, so a project two of them list is named by both.

### The indexer creates the databases it writes to · `--schema`

A checkout compiling for several frameworks is several databases, `<name>#<tfm>`, and the
names are not known until the design-time build has run. Discovering them ahead of time
meant `--list-frameworks` — a full load of the whole solution — and a 213-project
repository spent **482 seconds** on it before failing with `UnknownDatabase`, having
computed the very names it needed a moment earlier.

Pass `--schema` a composed schema and the run creates what it does not find:

    fjord --schema-path ./schemas schema compose ./schemas/dotnet.sigla > /tmp/dotnet.sigla
    fjord-indexer --sln ~/src/Repo/Repo.slnx --schema /tmp/dotnet.sigla --at …//code

Optional and additive: without it a missing database is the error it always was. Bake the
schema per run and never commit it — it is derived from the files beside it, so a copy in a
repository is the one that goes stale quietly.

### `fjord schema compose` prints a schema with its imports inlined

The two steps `create` already took, named so a caller who is not `create` can take them.
What comes back carries no `import`, so it needs no search path to be read again — which is
what lets a client that ships a known schema hand it to a server it cannot share a
filesystem with. Following an import is resolution, and neither a client without a parser
nor a server without the caller's filesystem can do it.

### `create` refuses a schema that declares what a server answers

A database declaring `fjord.db.List` composed to two of them when served and could not be
opened — and `create` wrote the artifact first and discovered that second, leaving a
database no listing could explain. The check now runs before anything is written, beside
the schema round-trip check already there, and names the predicate.

Reachable from outside: `SCHEMA` answers with the schema being *served*, virtuals included,
so a client that asked a database what it held and handed the answer back walked into it.

### The .NET client reads a page at a time, counts without rows, and manages databases

`Rows(sigla, pageSize)` is a lazy `IEnumerable`, so `Take(n)` costs one page rather than the
whole result; `Page` is the exchange under it and `CountRows` asks for a total with no row
encoded. Disposing an enumerator early sends `CANCEL` and drains to `Complete`, which is
what makes stopping early safe on a socket every stream shares. `CreateDatabase`,
`SchemaSource` and `ConnectUnbound` cover the lifecycle frames the protocol has always
carried and this client did not implement.

**The defect behind it**: an indexer was OOM-killed at 20.6 GB after its walk had finished,
while printing five rows. One smoke query asked for the whole line table of every file a
definition was in — **109,720,432 rows** — and `Query` collects every row before its caller
sees the first. That query is fixed (46,820 rows, one per declaration) and every smoke query
now runs against a real server in the suite, which nothing did before.

### A reference assembly is not walked, and a second implementation of one assembly is named

`dotnet/runtime` could not be indexed at all. Every library ships a `ref/` project beside its
`src/` one, restating the whole public API under the same assembly identity, so both minted
one `src.Symbol` per member while `codemarkup.SymbolInfo` — keyed `{symbol}`, with the
documentation on the value side — wanted one key with two values. Ingest refused it
(`ops-I4`) and the write stream died part-way through. A compilation carrying
`ReferenceAssemblyAttribute` is now left unwalked and counted; the project stays in the build
graph.

`src/coreclr/System.Private.CoreLib` beside `src/mono/System.Private.CoreLib` is the same
collision without the same answer — two real programs, one identity — so the second is left
out **by name** and `--strict` fails the run over it. Which is kept is the solution's order:
arbitrary between the two and stable across runs, because nothing in a build graph says which
implementation a reader meant.

### `csharp.FullName` carries an arity, so `Result` and `Result<T>` are two entities · **rebuild your clients, recreate your databases**

C# holds a type name at two arities in one declaration space, and `class Result` beside
`class Result<T>` is the everyday idiom. `csharp.FullName` was `{name, containingNamespace}` — a
name with the arity stripped, because that is what Roslyn's `ISymbol.Name` answers — and the four
named types key on it. So the pair was **one** `csharp.Class` with **two**
`csharp.DefinitionLocation` rows: the shape a partial class has, never an error, and
`csharp.TypeTypeParameter` hung `T` off the merged entity. A commit had already made the two mint
distinct SCIP symbols, so this was newly *visible* rather than newly *made*: before that the run
died on a `codemarkup.SymbolInfo` conflict before anything could merge.

It is `{name, containingNamespace, arity}` now, `arity` last. Over the `arity` fixture, indexed
through a real server, `csharp.Class` answers two rows named `Result` — one at arity 0 and one at
arity 1 — with a definition location each, where it answered one row with two.

**Field order is the design, not a formality.** `name` still leads, so "the fully-qualified names
spelled `Foo`" is a range; `{name, containingNamespace}` is a range over the arities; and
`{name, containingNamespace, arity}` is an **exact seek** — the query that could not be asked
before. `:plan` shows all three.

**And it completes the type-parameter decision rather than reversing it.** The named types carry
their parameters as `TypeTypeParameter` edges because a list cannot lead anything after it in a
key. An arity is that list's scalar summary, and a scalar can sit in a key where a list cannot:
the edges keep the parameters, the key gains the count.

`csharp.Name` is untouched and still holds the arity-stripped simple name, so `NameLowerCase` and
the two search predicates mean what they meant. `csharp.Method` is untouched too: it keys on
`Name` plus a `docId` that already encodes arity, which is why generic *method* overloads never
merged.

**Adding a field changes a predicate's type, not the predicate set, so no predicate id
renumbers** — the expensive half of the last flag day, and this one does not have it. `describe`
over a database created before and after shows every id paired with the same name; what moved is
the per-predicate fingerprint of the 27 `csharp.*` predicates whose type reaches `FullName`
through the `AType`/`NamedType`/`Definition` unions. `csharp.Name`, `NameLowerCase`, `Namespace`
and `TypeParameter` did not move.

**Three whole-schema fingerprints move, and eight do not:**

| schema | was | is | predicates, imports resolved |
|---|---|---|---|
| `csharp.sigla` | `0xcd1ded4ad8d8b187` | `0x13b475c0b02c4228` | 40 → 40 |
| `dotnet.sigla` | `0x32c681adade8a5f7` | `0x4e90774b9a0814cc` | 65 → 65 |
| `index.sigla` | `0x49cbd96832c1ae21` | `0x48932239a227a7f8` | 136 → 136 |

Every client carrying an old constant is refused at the handshake until it is rebuilt, which is
the designed failure. Two constants were re-pasted — the indexer's (`dotnet.sigla`) and the SCIP
converter's (`index.sigla`); the demo's is over `demo.sigla` and unmoved. The .NET package goes to
**0.4.0** in this same change, because a moved fingerprint *is* a client release — and the bump is
visible in the book's expanded rows, where a symbol reads
`nuget Boxops.Fjord.Client 0.4.0.0 …` rather than `0.3.0.0`.

`clients/dotnet/golden/` came back byte-identical, established by running `emit-golden.sh` and
hashing rather than by reasoning: all three goldens are over `demo.sigla` and two throwaway
schemas, none of which imports `csharp`.

**Measured over the reference corpus.** `Surface.slnx` indexed before and after: 40 more distinct
facts, from 12 `csharp.FullName` rows that were holding 31 different named types between them and
now hold one row each — `System.Action` at three arities, `System.Func` at three, `ValueTuple` at
seven, and `Task`/`ValueTask`/`EventHandler`/`Expression`/five `CompilerServices` builders at two
apiece. `csharp.Class` goes 1,421 → 1,426 and `csharp.Struct` 241 → 253. `quarantine/Arity`,
indexed alone, goes from 10 declarations on **5** entities to 10 on **10**, the nested pair
`ArityOuter<T>.ArityInner<U>` / `ArityOuter.ArityInner<U, V>` included.

**What the key still has no field for is the assembly**, and the corpus measures that too:
`Assemblies.Left` and `Assemblies.Right` declare one namespace-qualified name each, agreeing at
every arity, so **8** entities there are still reached by two declarations — two classes, three
methods, one field and two properties. It is unmoved by this change and is now the open item in
`docs/unified-plan/15-retire-code-sigla.md` §S3.

The gates are `ArityPairTests.Each_arity_is_its_own_class_fact_with_its_own_location` and
`EntityKeyCensusTests.Two_arities_are_two_entity_keys_and_a_partial_classs_halves_are_one`, and
the second is the one that matters: it asserts the arity pair is two keys **and** that a partial
class's two halves are one, because a fix that keyed on something per-declaration would satisfy
the first and break the second. `PartialMemberTests` holds the same contrast in a database.

The book's transcripts are re-taken from a real run rather than edited, which also picked up drift
nobody had: the run now sees 57 projects under `clients/dotnet` rather than 20, because the
reference corpus's fixture projects live there.

### `msbuild.AssemblyReference` and `msbuild.AssemblyDependent` are deleted · **rebuild your clients, recreate your databases**

Both were declared, given ids, listed in the .NET client's batch set — and **never written by
anything**. A new completeness census made that visible: `PredicateCensusTests` classifies every
predicate the client declares and asserts it behaves as classified, and this pair sat there as
`Owed` with the reason it could not be filled. The reason is why deleting beat filling: the
reference list a design-time build hands back is every resolved DLL, some hundreds of framework
assemblies per project, and nothing in it tells a `<Reference>` somebody wrote from the
framework's own. Keying an assembly on a file name would have invented an identity two producers
would spell differently, so the schema was declaring a question no consumer could get an answer
to.

`msbuild.Assembly` **stays** and its charter narrows. It is written from each project's own
output name and still joined by `Compilation` and `ProjectCompilation`, so the deletion orphans
nothing — but an `Assembly` fact can now only be one a project *in the graph produces*, never
one referenced from outside, and the schema's comment says so.

**Removing a predicate is Breaking under subset containment, so three fingerprints move:**

| schema | was | is | predicates, imports resolved |
|---|---|---|---|
| `msbuild.sigla` | `0xd97f69e6c42cf593` | `0xfad0dcb5ca7f6cd9` | 25 → 23 |
| `dotnet.sigla` | `0xc20dfe719b04e025` | `0x32c681adade8a5f7` | 67 → 65 |
| `index.sigla` | `0xea69e11d083ae95f` | `0x49cbd96832c1ae21` | 138 → 136 |

**Every client carrying an old constant is refused at the handshake until it is rebuilt** — the
designed failure, and the refusal names both numbers. The .NET package goes to **0.3.0** in this
same change, because a moved fingerprint *is* a client release. Three constants were re-pasted:
the indexer's (`dotnet.sigla`), the SCIP converter's (`index.sigla`) and — unmoved —
the demo's. The converter's had been sitting inline as a positional argument, named nowhere, so
`the_dotnet_clients_carry_the_fingerprint_the_schema_has` never saw it and its own handshake was
what noticed; it is a named `SchemaFingerprint` constant now and that gate checks all three.

**The predicate numbering moves, and that is what a recreated database contains.** Ids are
assigned by sorted fully-qualified name at create and are append-only for the life of a
database, so deleting two shifts every predicate that sorts after them down by two: in
`dotnet.sigla`, `msbuild.*` ends at 55 rather than 57 and the whole `src.*` block moves — `src.File`
is **56**, not 58, which is visible in a row printed as `#56:2` where it used to read `#58:3`. A
`FactId` packs its predicate's id in its high bits, so a consumer decoding a returned reference
against a hardcoded table would read the wrong predicate. Nothing has to detect that: every
existing database is refused at the handshake for the moved fingerprint, so a stale table cannot
meet new bytes. The .NET client's own hand-written ids move the same way — 43 constants after
the deleted pair — and `PredicateCensusTests` asserts that array is exactly its declaration's
ids.

The `census` run's audit table loses two rows. Three `Owed` entries remain, all about
solutions, and they are a separate open question. The book's transcripts are re-taken from a
real run rather than edited: ten empty predicates out of sixty-five now, where there were
twelve out of sixty-seven.

`clients/dotnet/golden/` did **not** need regenerating, which is the right answer rather than a
skipped step: all three goldens are over `demo.sigla` and two throwaway schemas of their own,
none of which imports `msbuild`. `byte_identical_with_the_dotnet_client` compares fingerprints
before bytes, and it is green untouched.

### An equality on a `string` or `bytes` key field answered rows of other values

`X = "a"` answered three rows over a store holding `"a"`, `"a\0"` and `"a\0z"` — and a join
narrowed by such a field matched the same way, since a splice is the same seek with someone
else's bytes in it. `int`, `Fact` and record key fields were never affected, and neither was the
prefix pattern `X = "a"..`, which wants those rows.

This is the sibling of the excluding-bound defect below, and the same arithmetic: a terminated
field encodes as `MARK ++ escaped(payload) ++ 0x00`, so `enc(v)` is a proper byte prefix of
`enc(w)` exactly when `w` is `v` with a NUL and more after it. A seek opened
`[prefix, strinc(prefix))` — the range of everything sharing its bytes — which is right for a
byte-prefix pattern and one value too wide for a complete field encoding. Silent, because a
constant bind **folds**: the equality is not in the residual list, so nothing behind the seek
re-checked what it let through. The same off-by-one closed the open end of a one-sided bound
behind a splice (`file = F, line >= 1000` over a `bytes` file field).

The fix is that the plan now **says which of the two it holds**. A byte-prefix pattern is
`SeekKey::PrefixRange`, a variant of its own beside the parts because nothing may follow a
partial field encoding — the rule a bounded seek's edges already followed — and a
`SeekKey::Prefix` or `Composite` is a run of complete field encodings. The first ends at
`strinc`, the second at `above_field`. Read off the byte string the two are indistinguishable,
and whichever end is picked is wrong for the other.

**A resume token for a query holding a prefix pattern is refused after this and must be
re-issued.** `PrefixRange` takes its own fingerprint tag, which is the point of the variant: two
plans holding the same bytes as the two readings open different ranges and must not accept each
other's cursors. Eight of the corpus's plan fingerprints move, all of them prefix patterns; no
other plan shape changes. The cursor **layout** is unmoved, so `CURSOR_VERSION` stays at 3, and a
folded equality's fingerprint is unmoved too — a token saved against one by an older build is
refused by the range check instead, `BadResumeKey` rather than an answer from the wrong place.

Nothing on disk moved: no marker, no encoding, no format stamp. The end of a range is query-time
arithmetic.

### The protocol is 4, which is what `bytes` always said it was · **rebuild your clients**

The `bytes` family was described as a protocol bump in three places — the doc comment on the
wire descriptor's own `TAG_BYTES`, the release note below, and the decision that accepted it —
and `fjord_wire::protocol::VERSION` stayed at 3. It is 4 now, and the .NET client's
`ProtocolVersion` with it. **A client built before this is refused at the handshake until it is
rebuilt**, which is the designed failure and the reason the field exists.

The schema fingerprint does not cover this, which is the part worth understanding. A peer may
open a connection *without* asserting a schema and then ask the server to describe its own —
that is how a client discovers what a database holds. Such a peer, built before `bytes`, agreed
about the protocol at the handshake and then met descriptor tag 5 with no case for it, failing
mid-stream on `UnknownRefForm`. Version 2 was minted for a change of exactly this shape, for
exactly this reason: an older peer should be told it speaks a different protocol rather than left
to fail a comparison it cannot interpret.

### An excluding bound on a `string` or `bytes` field answered the wrong rows

`X > v` dropped rows that satisfy it and `X <= v` returned rows that do not, whenever the
comparison folded into a seek on a `string` or `bytes` key field and the stored values differ by
a trailing NUL. `>= v` and `< v` were always right, and `int` was never affected.

The cause is the storage codec's escape scheme meeting a byte-prefix successor. A terminated
field encodes as `MARK ++ escaped(payload) ++ 0x00`, and `escaped` writes a payload NUL as
`0x00 0xFF` — so `enc(v)` is a proper **byte** prefix of `enc(w)` exactly when `w = v ++ 0x00 ++ …`,
and every such `w` is strictly greater than `v`. The excluding edge was `strinc(prefix ++ enc(v))`,
the successor of everything sharing that prefix, which cuts those greater values off with the
value's own rows.

It was silent, and that is the part worth stating: folding a comparison into the seek **removes it
from the residual list** — the range is meant to be the exact answer rather than a superset — so
nothing behind the seek re-checked the boundary.

The edge is now `above_field(k) = k ++ 0xFF`, which lands between one value's keys and the next
value's rather than at the end of a byte prefix. Every field mark is at or below `MARK_BYTES`
(`0x53`) and a group terminator is `0x00`, so a key that continues *at* `v` continues with a byte
below `0xFF`, while a NUL-extension of `v` continues with `0xFF` itself. It is also total where
`strinc` is not — an all-`0xFF` prefix has no successor.

**Live since 0.1.0 for `string`**, where it needed a NUL inside a stored string to show. The
`bytes` family made it ordinary rather than exotic, which is how it was found.

### The exhaustiveness probe no longer eats the edit it refuses to touch

`scripts/check-exhaustive.sh` armed `trap restore EXIT` — a `git checkout` of the file it is
about to edit — *above* its dirty-tree guard. So the guard's own `exit 2` fired the trap: the
script printed "commit or stash them first" and then discarded exactly the unstaged changes it
had just declined to touch, with no stash and no reflog entry behind it.

The window is the one the script is for. `AGENTS.md` says to run it "when adding one, or when
changing a match that dispatches on a type" — which is to say while `schema.rs` or `syntax.rs`
is open and half-written.

The trap is armed below the guard now, and `scripts/test_check_exhaustive.py` holds the order
against a throwaway repository rather than this one, because a control for *does it destroy
uncommitted work* must not be able to destroy any. Three exits are provoked: a dirty target is
refused with its edit byte-for-byte intact, an unknown argument touches neither file, and the
throwaway `Probe` variant is still gone after a run that ends in failure — so the restore is
not conditional on success. It joins the required `test` job; the probe it guards stays out of
CI, because that one fails the build by design.

### A SCIP index is an ingestion path — `scip2fjord`

Every language with a SCIP indexer reaches a Fjord database through one program:
TypeScript, Java, Scala, Rust, Python, Go, Ruby. It reads the index, converts the positions,
projects the kinds, and writes the same `codemarkup` surface a C# index has — so a UI reads
one shape whatever produced the facts.

It references the client library and **no indexer**, which is what proves the write seam was
really published rather than merely renamed. No protobuf package either: four messages and
eleven fields, in a format that is varints and length-delimited bytes.

**Positions are the work.** SCIP counts characters — UTF-8, UTF-16 or UTF-32 code units
depending on what the indexer was written in — and this schema counts UTF-8 bytes from the
start of the file. The fixture index has an `é` in it for that reason: an implementation
right about ASCII and wrong about everything else passes every other assertion.

What it does not write is a declaration layer or a type graph. A SCIP index contains
neither, and inventing them per occurrence would put facts in a database nothing could stand
behind.

### The write path is `Boxops.Fjord.Client`'s, not the indexer's

`IBlockTarget`, `BlockWritten`, `FjordTarget` and `FactSink` are public in the client
library. None of them says anything about Roslyn: the batching, the bounded queue, the
writer threads and the latched-failure rule are generic write support, and the only reason
they lived in the indexer is that the indexer is where they were written.

`FactSink` takes a **schema**, not a producer. It needs to know how many predicates exist
and how to encode one, and nothing else.

A refusal now names the predicate whose block was in the writer's hands. A database rejects
a *fact* and can say which predicate; it has never heard of the declaration behind it — so
without this the answer to a multi-hour run that ended in a refusal was "one of eighteen
million facts".

### One database per target framework

A project compiled for `net8.0` and one compiled for `net10.0` are different programs, and
no key in the schema can hold both. The indexer used to keep the newest and throw the rest
away; it now writes `code#net8.0` and `code#net10.0`, and each says which framework it holds.

**Compiled as, not compatible with.** There is no nearest-compatible reduction: a project
with no `net8.0` result is absent from the `net8.0` index rather than present under a target
MSBuild never built it for. `--framework` selects one, the run names every project it leaves
out, and `--strict` makes that a failure.

`config.Setting` has a producer at last — nine dimensions, `framework` exactly once. Every
axis a database was resolved against used to be implicit, and one of them cost real time:
an index whose paths are relative to a root nobody wrote down can only be matched to a
checkout by inference.

### A checkout that had been built indexed as nothing at all

`CoreCompile` is incremental, so after any ordinary `dotnet build` MSBuild skipped it — and
the compiler command line it would have logged is the entire content of a design-time build.
Every project came back succeeded-with-no-result, which the loader reported as a failed
build, naming a target the project was never going to have. **A run over a repository
anybody had built wrote nothing.**

Four more defects in the same area, each invisible from inside the code:

- **Adding a project pulled in projects nobody asked for**, building them during what is
  supposed to be pure bookkeeping — and left the compilation holding each referenced project
  twice, once as a project and once as its assembly, so every type in it was ambiguous and
  Roslyn answered `null` rather than choosing.
- **A solution naming a project through `..`** produced no results for it: the path is
  carried unnormalised while MSBuild reports the normalised one.
- **A project the glob missed and the build found** contributed nothing, and the reference
  edge pointing at it was dropped for want of a target.
- **A file outside the index root** was named `../../../.nuget/packages/…`, which depends on
  where the root happens to be.

A transient MSBuild failure is now retried and a deterministic one is not — the distinction
being that a throw says nothing about the project while a clean answer with no compiler
invocation has told the truth about it. Retrying both is how a broken repository takes three
times as long to say so; retrying neither is how the project set depends on `--jobs`.

### The walk has no gate, and the index does not depend on how many threads walked it

One lock stood around everything downstream of a symbol — the memos, the counters, the sink
— so eight walker threads took turns to write. It is striped per predicate now, and the
memos follow the rule that makes concurrency safe here: built outside, published with
`TryAdd`, emitted by the winner, and **a cycle broken on the thread's own stack** rather than
on a shared marker, which would have had two threads writing facts of different depths under
one key.

Over this repository: 103,106 facts at one job and 103,106 at eight, 8,161 facts/s against
12,282, and 523 of those facts waited for a batch for under a twentieth of a second.

The writer default was measured rather than assumed and stays at 1: more writers remove the
stall and the walk gets slower anyway, which is the crossover being a property of corpus size
rather than core count.

### The measurement register is closed until a 1.0 pass

`bench/FINDINGS.md` carries one banner saying every number in it is superseded, and the
twenty sections below are left exactly as they were taken. Four things happened underneath
it and any one would have been enough: the schema every figure was measured over is
deleted, the mode that built the corpus is deleted, the Glean comparison the write-path
entries turn on is retired, and cost-based reordering and recursion are still ahead of the
read path.

**Closed rather than amended, and that is the decision.** A measurement is only worth
reading against the tree that produced it, so rewriting these to look current would destroy
the one thing they are still good for. What survives is stated instead: five lessons, each
banked in the tree with a guard — key field order decides a join's cost (§2), interning is
most of what ingest spends (§12, §13), the plateau was the connect and the allocator (§18),
two defects found and fixed (§10, §20), and a new scalar family is a compile error rather
than a corrupt row (§19). The instruments survive too, and *"what is still open"* is kept
whole as the next pass's inbox: an unmeasured question is worth more than a stale answer.

Four run gates recorded into the register and are re-cut with it. One of them, R0's, had
been unrunnable for longer than that: it asserted `Conflicts == 0` against a counter the
semantic key removed, and `M == 0` against a counter that never existed in the tree or the
plan.

### `--syntax-only` is deleted, and a run that cannot resolve refuses

The mode skipped semantic resolution to index faster. The governing rule is that *a run
that cannot resolve assemblies or types fails, loudly, rather than emitting a degraded
fact*, and a mode whose whole purpose is to skip resolution is the exception that rule
cannot survive. `--skip-files` goes with it, having existed only to make syntax-only runs
finish.

An indexer that builds no project now throws and says which of the two happened — nothing
found under `--source`, or every project failed — rather than writing a small index that
looks like a working one.

### The Glean comparison is retired; Fjord stands on its own

The capability ledger, the read-path comparison plan, the Angle translation of the
benchmark schema, the script that ran both systems, `GleanFacts.cs` and `--glean-out` are
all deleted. Every number the comparison produced was already void — measured over
predicates a later change deleted — and maintaining a translation of somebody else's schema
to keep a void comparison runnable is a cost with nothing on the other side.

**Provenance stays, because deleting a citation loses the reasoning rather than the
comparison.** `csharp.sigla` still says it is a translation, and `codemarkup`'s
vocabularies still name their sources — their discriminants are frozen *because* they are
citations, and a reader who thought we invented the numbering would feel free to renumber
it.

### `schemas/code.sigla` is deleted; the .NET indexer writes `schemas/dotnet.sigla`

The worked-example schema and the real producer's schema were one file, and it could not be
both. It is now two.

**`dotnet.sigla` (`0xc20dfe719b04e025`) is what the indexer writes** — 67 predicates,
declaring none of its own, importing `src`, `config`, `msbuild`, `csharp` and `codemarkup`.
One declaration predicate keyed `{module, name, line}` becomes a per-kind entity layer with
a symbol identity that does not move when a file is reformatted: **`src.Symbol` is a
spec-conformant SCIP symbol** under this producer's own scheme token, `scip-csharp-2`, with
the database naming the scheme it holds in `config.Setting {dimension = "symbol-scheme"}`.
A symbol with no global name — a local, a label, a member inside a method body — gets no
string rather than a synthesised one that would have to be invalidated;
`codemarkup.FileLocalXRef` answers those span to span.

**`demo.sigla` (`0x03678fcd1e7924e3`) takes the fixture role and grows to earn it** — eleven
predicates covering every construct the type model can hold: the three scalars, records flat
and nested, a value side that is a scalar and one that is a record, references, unions with
payloads of every kind including empty, a single-alternative union, a keyword as a field
name, and a named type. It is what the interactive site lowers, what the benchmarks measure
over, and what the byte-identical test states independently in Rust.

**A client may declare only what it writes**, and that turned the switch from a 67-predicate
big bang with no gate until the end into ten predicates against a database of sixty-seven,
then a layer at a time. Predicate ids are the client's own, a block header carries the
predicate's *name*, and a nested reference takes its predicate from the field's declared
target, so nothing positional crosses the wire. Declaring less asserts nothing less: the
fingerprint is carried rather than computed, so a stale one is still refused — and since it
therefore says nothing about the shapes, what checks a transcription is a fact of every
declared predicate written and read back.

Two things went with the file. `flag-day.sh` is retired: every step it walked is a test now,
which is where a checklist belongs. And the Roslyn side gained an `Inexpressible` counter —
a signature this layer cannot key is dropped and *counted*, never hidden — which promptly
caught a test workspace built without metadata references that was indexing one type out of
nine.

### `src.FileLineStyles` holds opaque bytes, and fjord ships no codec

Syntax highlighting is a payload fjord stores and does not interpret. There is no fjord
format for it, no vocabulary of token types, and no codec in any crate: the schema says
`bytes`, `config.Setting {dimension = "style-encoding"}` names the format and its legend
together, an unrecognised value renders those lines plain, and absent means "not
tokenised".

The alternative was fjord defining a token vocabulary, which would have made every
producer's classifier translate into ours and every consumer's renderer translate back out
— two lossy conversions to reach a format neither end wanted. The .NET indexer carries LSP
semantic tokens as **the producer's choice**, marked as such.

### The .NET side has a test project, in the job that is already required

`Boxops.Fjord.Tests` runs on every pull request, added to the existing `test` job rather
than to a new one. A new `dotnet-test` job would not be a required check until a repository
ruleset was edited — an admin action, audit-logged, easy to forget, and invisible when
forgotten — so the gate for six runs' worth of work lives behind a check that is already
enforced.

### Five reference schemas, and `index.sigla` — the set composes

`csharp` (31 predicates), `msbuild` (14), `typescript` (35), `npm` (14) and `bundle` (22), plus
a composite that declares none of its own and imports the other eight. **136 predicates in 9
files**, and `schemas/` goes from two files to eleven.

These 118 predicates are **not populated by anything in this repository**. They ship as
declared, checked, fingerprint-recorded schemas because a schema file costs a binary nothing —
nothing in `schemas/` is embedded except by an explicit `include_str!`, and a release carries
no schema files at all. What fjord promises is the shapes and the vocabularies' numbering, not
tracking LSP, SCIP, Yarn or webpack releases on a schedule; a vocabulary that has to grow does
so through its `other : string = 0` valve.

**The two queries in the composite's header are integration tests rather than illustrations.**
Every reference in one file resolved to where its target is defined *for any language*, and a
symbol joined to the project that compiled the file it sits in — across three namespaces filled
by three different producers. Neither is expressible across three separate databases, because a
`FactId` does not leave the database that issued it (I11). The fixture is a TypeScript file
referencing a C# symbol defined in a C# file compiled by an MSBuild project, and nothing in
either query says which language anything is.

`csharp.EntityXRef` joined to `csharp.DefinitionLocation` through a shared union-typed variable
is the end-to-end payoff of the `unify` fix: a seven-alternative union in two key positions,
which was `reject/type-mismatch` and no plan at all until that arm existed.

`every_vocabulary_is_contiguous_and_unique` walks every union in all 136 predicates and asserts
the discriminants are unique and contiguous from their lowest. These sit in keys, so I10 froze
them on landing and a transcription slip in a twenty-one-line table is permanent — which no
reviewer reliably catches and no other test would.

### `schemas/codemarkup.sigla` — one surface a UI can read

Ten predicates over `src`, and the whole layer rests on one decision: **the join key is a
string**, `src.Symbol`, and not a union over languages.

Key a definition on `{ csharp : … | typescript : … }` and a C#-only index and a
C#-plus-TypeScript index carry *different* `Definition` predicates, because appending a union
alternative is Breaking — I10 freezes discriminants and `schema diff` reports even an append
that way. One UI could then not read both, which is the entire purpose of a
language-independent layer. Both halves of that are tests rather than paragraphs: appending one
alternative to a union used in two keys breaks exactly those two, and every `codemarkup`
predicate has the same fingerprint resolved alone or inside a composite holding two other
language layers.

The second reason is cross-database. A `FactId` is a predicate tag plus a per-predicate
sequence (I11), so it means nothing in another database — and "who references this, anywhere"
is a fan-out across several. A string survives the trip; an id does not.

`Kind`, `Role` and `RelationKind` are **citations rather than inventions** — LSP's `SymbolKind`
1–26 verbatim, SCIP's `SymbolRole` projected, Glean's relation kinds — because they sit in keys
and their discriminants froze the day this shipped. `other : string = 0` is the only valve.
One loss is stated rather than hidden: SCIP's `SymbolRole` is a *bitmask*, a reference can be a
definition and an import at once, and this stores the mutually-exclusive projection a UI
filters by.

Nothing existing moves — a new namespace in a file of its own.

### `schemas/src.sigla` — one shared source layer · **breaking, and a client rebuild**

`schemas/code.sigla`'s fingerprint moves from `0xb08eea634e866a75` to `0xe044df7620885507`.
(That file is **deleted** later in this same release, above; the flag day below is what it cost
while it existed, and the layer it moved to is what survives.)
**Every client carrying the old constant is refused at the handshake until it is rebuilt** —
that is the designed failure, and the refusal names both numbers so an operator can tell a
stale client from one pointed at the wrong database. The .NET package goes to 0.2.0 in the
same change.

`code.sigla` now **imports** `src` rather than declaring its own file layer. Three schemas
used to declare their own `File`, each "owned here so the schema resolves standalone", and the
cost of that came due: `content.File "x"` and `csharp.File "x"` are different types naming the
same file, so the join that renders a search hit — a definition in one index, the bytes in
another — could not be written in sigla at all. It was written in JavaScript, and it held only
because two producers agreed on a string by convention.

**The change is exactly eight predicates added and one removed, with every survivor
byte-identical**, and that is asserted rather than described:
`the_source_layer_breaks_exactly_one_predicate` fails on a second broken name or on any
survivor whose fingerprint moved. Eight and not nine because `src.File` *moves* into
`src.sigla` rather than arriving — a `Predicate` carries no file for the canonical form to
read.

**`src.Line` is deleted** in favour of `src.FileLine`, whose value is the line's text plus
three offsets rather than a bare string. `start` is a UTF-8 byte offset and `cstart` a UTF-16
code-unit offset — not the same number, because a codepoint above the BMP is four bytes and
two code units — and which one a span means is declared once per database by
`config.Setting {dimension = "position-encoding"}`. The line table is therefore the conversion
table.

Two errors inherited from the proposal are corrected here rather than shipped. An integer
range is a **comparison statement** (`S >= X`) and not `start = X..`; `..` is the string-prefix
operator. And an offset *at* the last line's start is an exact hit — only an offset **past** it
returns nothing, which is when a consumer falls back to `FileInfo.lines`. That fallback is the
common case for a reference in the last line of a file, so a consumer reading empty as "not
found" fails on every one of them.

`bench/FINDINGS.md` §1's `src.Line` row was marked for a re-run: the seek figures are about the
key, which is unchanged, but the byte totals are not. The register has since been closed whole,
above, so the re-run is the 1.0 pass rather than a scheduled repair.

### `fjord-viewer` is retired

The code-search site is gone, and a release now carries **two binaries rather than four**: `fjord`
and `fjord-x86_64-linux-musl`. What replaces it is a browser application — React and Vite, with a
WebAssembly client — and the reason is the rendering rather than the taste.

A source view is a **merge of two independent sets of ranges over one line**: syntax runs and
cross-reference anchors, split at the union of both boundaries and emitted as one correct nesting.
Server-rendered HTML does that once, and then a virtualised scroll over a 50,000-line file cannot
reuse any of it, and neither can a hover, a filter or a selection.

It proved what it was built to prove — a viewer is an ordinary consumer of the protocol, needing no
privileged access to a database — and building it is what found the two predicates the schema was
missing, a file's cross-references keyed by file and a case-folded search index, because the
questions a UI asks turned out not to be the questions the schema answered. Nothing in the
workspace depended on it.

**If you were running it**, there is no drop-in replacement yet. The last release that carries it
stays on the releases page and keeps working against any database it worked against before; a
database's schema is embedded and frozen at create, so nothing about an existing index changes
underneath it.

One thing this repository now owes the replacement, recorded so it is not discovered late: a
browser can open neither a Unix socket nor raw TCP, and those are the whole of the client's
transport today. The answer is a **WebSocket listener carrying the same frames** — one protocol,
one codec, one set of goldens — rather than a second, JSON-shaped surface. It will be
default-closed, as TCP is.

### `schemas/config.sigla` — a database says what it was built for

One predicate, `config.Setting {dimension, value}`, for the question a tool holding forty
handles has to ask: *which one is this*. An instance name it can only string-match on is not an
answer, and the sharp case is a build axis that appears nowhere else in the index — under
nearest-compatible target resolution a `netstandard2.0` project inside a `net9.0` index records
`netstandard2.0` in its own compilation facts, correctly, so the resolution root is
unrecoverable from the facts.

Dimension-leading so a lookup is a seek; **key-only** so a dimension may hold several values,
which `dimension -> value` could not say because the second write would be a conflict rather
than a second fact. Both fields are strings and neither is a union: a union would freeze the
vocabulary on the day it shipped (I10) and make every new axis a Breaking edit to a predicate
every published index carries. The reserved list lives in the schema comment instead.

**`position-encoding` is the one worth reading.** `utf8` or `utf16`, declared once per database
and the unit of every offset and column in it — SCIP's answer rather than a unit per span, and a
mixed-encoding database is deliberately not expressible. A database that does not state it is
read as `utf16`, because that is what Roslyn and the TypeScript compiler actually count. The
unit that agrees with neither is a renderer walking `chars()`: one per codepoint where the
producer counted two for anything above the BMP, in range and pointing at the wrong text, which
reads as a styling bug rather than an off-by-one.

Nothing existing moves — a new namespace in a file of its own.

### `finish` writes the data to tables, which it did not

`Catalog::seal` called `persist` then `compact`. `persist` fsyncs the write-ahead journal;
`compact` merges already-flushed segments. **Neither touches a memtable** — so whatever ingest
left resident when a database was sealed was written to no table at all. It stayed in the
journal, invisible to the merge that followed, and every read of it came from a memtable
recovered at open rather than from the merged tables sealing exists to leave behind. `compact`'s
own doc prices a re-seek into an unmerged tree at up to 180×.

Measured on the same corpus both ways — 520,000 facts:

| | without the flush | with it |
|---|---|---|
| tables after `finish` | **1,176 kB** | **17,120 kB** |
| journal after `finish` | 73,376 kB | 73,376 kB |
| the instance on disk | 74,572 kB | 90,516 kB |
| `finish` | 4.97 s | 9.26 s |

Read the first row: half a million facts came to 1.2 MB of tables.

**Two consequences worth knowing before upgrading.** `finish` costs 1.86× more, which is the
flush doing real work — it is the operation whose whole job is to say *this is finished*. And a
sealed directory is now **21% larger**, because fjall reclaims a sealed journal only inside its
own flush worker, above a threshold of its own, with no public API to ask; so the tables are
written *in addition to* the journal rather than instead of it. The trade is a table-backed read
path, paid for on every query forever, against a one-time larger artifact.

`FJORD_META.bytes` is documented rather than changed: it is the on-disk size of the instance
directory at the moment of sealing, journals included. Not a logical fact count.

No identity moves — `ops-I4` hashes the facts, and the test corpus seals to
`0xbd38b7d3971a1c5d` on both paths, which is what makes a flush a flush.

The `ops-I*` table in the invariants registry gains its first Guard column entries.

### `import ob` answered by a file declaring `schema base` says so

A file is located from the import name alone — resolution never inspects the `schema <name>`
head it finds there — so a mismatch used to resolve both files and then fail
`reject/unknown-name` at every reference into the namespace, reporting the symptom everywhere
and the cause nowhere. It is now `reject/namespace-mismatch`, reported **at the import**, and
it names both: *"`ob.sigla` declares no namespace `ob` — it declares `base`"*.

The schema corpus can state a **cross-file** case, which it could not before: doing so needed a
filesystem, and a corpus that writes temp directories is a corpus nobody runs. `resolve_from`
made them cheap, and seven arrived at once — an import that brings in what it names, a named
type moved into an imported file, identical *and* differing redeclaration across two files, a
namespace mismatch, a cycle, and a diamond.

**Two corrections to what the book said.** Identical redeclaration across two files rejects —
the book said it did not — and that is the right behaviour rather than a wart, because a
namespace split across files has exactly one declaration site per predicate. And the
wire-protocol page now says where a predicate id comes from: sorted fully-qualified name,
`fjord.*` last, assigned at create and append-only for life. A predicate whose name sorts early
therefore *inserts* an id rather than appending one. That never leaves the database — a block
header carries the name — with one exception, now written down: a `FactId` packs its predicate's
id in its high bits, so a consumer decoding a returned reference's tag against a hardcoded table
reads the wrong predicate the day the numbering moves.

`predicate_ids_are_assigned_by_sorted_qualified_name` gates that rule across a schema that
**spans files**, which is what is new here; `ids_are_assigned_by_sorted_qualified_name` has
gated it within one block since before this release. The reserved half — that `fjord.*`
numbers last — is gated by the same test's `fjord.db` block, and dropping the reserved key
from `lower`'s sort makes it fail. Every schema file added from here on relies on both.

### A schema that spans files, resolved without a filesystem

`fjord_db::read_schema` now takes the **set** of sources, the entry first, and follows the
imports through it — so a producer embeds its schema with `include_str!` and a browser build
can open one with an `import` in it. It touches no filesystem, and that is mechanical rather
than a promise: the filesystem provider sits behind a default-on `fs` feature, so
`cargo check -p fjord-schema --no-default-features` is a compile error the day the embedded
path reaches for it. CI runs it against `wasm32-unknown-unknown`.

**This is a breaking change to `read_schema`'s signature.** It was `(name, source)` and lowered
one block of source; a schema with an `import` came back silently missing everything it
imported, which is a database full of rows nobody can read back. The old shape had no way to be
right about a multi-file schema.

One algorithm, two providers, and a differential says so: every multi-file case in the corpus
is resolved twice — once from a real directory, once from the same text in memory — and the two
must agree on every predicate, every per-predicate fingerprint, the schema fingerprint, and the
rendered diagnostics. Two clauses are licensed to differ because they are each provider's own:
how it names a source, and its account of where it looked.

`Resolved::files` is `Vec<String>` where it was `Vec<PathBuf>`, since an embedded set has no
paths. `fjord schema check` prints the same thing.

`schemas/` gains a recorded-fingerprint test — a shipped schema's number is what a client
carries as a constant, so an accidental edit to one should be a red suite rather than a refused
handshake at somebody's site.

### A `bytes` scalar family, and the `0x…` literal

A schema can declare a field holding **uninterpreted bytes**: `predicate FileDigest : { file :
src.File, digest : bytes }`. Not validated as anything — that is the whole of the type — and
ordered by `memcmp` over the payload, which is what the storage codec's escaping already
bought and what a length prefix would have thrown away.

Written in a query as `0x…`, lowercase, two hex digits a byte. Its own literal rather than a
widening of the string rule, which would have made every existing string literal ambiguous,
and lexed permissively so `0xzz` is one token with a named diagnostic — `lit/bytes-digit`,
beside `lit/bytes-empty` and `lit/bytes-odd-digits` — rather than a caret between two tokens.
The literal lands with the family rather than later because a digest lookup is the reason
anybody wants the type, and because `print::literal` emits `0x…`: without the lexer rule that
would be the one place the printer produces text sigla cannot read back.

**`MARK_BYTES` is `0x53`, appended, so `bytes` sorts after a union rather than beside a
string.** That reads oddly and is not taste: [I3](website/content/invariants.md#i3) freezes the
marker table and I15 checks the format stamp for *equality* at open, so renumbering is a
`codec` bump and a `codec` bump makes every database written by 0.1.0 unopenable. The wart is
taken, and the ordering is unobservable — a field has one declared type, a union discriminates
by tag before any payload is compared, and a record's fields are positional, so no query can
put a `bytes` and a `string` on the two sides of one comparison.

Four tag tables each took their own next free number, none derived from another and none
shared with `string`: the wire descriptor 5, the content identity 9, and the plan fingerprint
5 for a type and 6 for a value. Sharing the identity hash's number would fold `"ab"` and
`0x6162` together, so two databases holding different data would hash alike.

**It is a protocol bump.** A peer built before this meets descriptor tag 5, has no case for
it, and refuses the stream rather than reading the field as a `string` and handing its caller
bytes that are not text. The .NET client ships the type in the same change, with a third
golden corpus of its own — and `blocks.txt` and `unions.txt` came back byte-identical from a
full regeneration, so nothing already on the wire moved.

In JSON it is a bare lowercase hex string, **untagged**. Both live renderers hold the type
when they render, so every consumer that can interpret the field has the schema too; the one
that loses is a reader of detached JSON text with no schema, for whom `"00ff"` is
indistinguishable from a string whose content happens to be hex. Stated, rather than paid for
by every consumer on every row.

Nothing existing moved **for `bytes` itself**: `schemas/code.sigla` was still
`0xb08eea634e866a75` when this landed, the format stamp is still codec 1 / storage 1, and the
corpus's positional plan-fingerprint list took pure insertions. (That fingerprint moves later
in this same release and the file is then **deleted** — see the source layer above. The number
here is what `bytes` cost, not where the release left it.)

### A union-typed variable can be shared by two generators

`X where test.Tagged {what = W, id = X}; test.Label {id = _, what = W}` plans and runs. Two
structurally identical unions never compared equal: `unify` had an arm for every other shape
and a catch-all underneath, so the second mention of a union-typed variable reached the
catch-all and the error was built from the two sides it had just failed to compare — which are
equal, hence *"expected {…}, found {…}"* with one type printed twice. The workaround was one
query per alternative.

Alternatives are compared **as a set** of *(name, discriminant, payload)*, by discriminant and
never zipped: they are held in declaration order and permuting a declaration moves no stored
byte, so two orderings are one type. The name is checked as well as the discriminant, because
the fingerprint's canonical form writes `name:type=disc` — two unions differing only in an
alternative's spelling are different types on disk, and must be different types here.

A genuine mismatch now names the alternative the two sides first differ on, in discriminant
order, and how: an alternative only one of them declares, one discriminant with two spellings,
or a payload that does not unify. It reports through `reject/type-mismatch` as before — a
message improvement is not a reason for the taxonomy to grow.

Nothing else moved. No schema fingerprint, no cursor, no stored byte, and no plan fingerprint
of any query that already planned — the corpus's positional hash list gained two entries and
changed none. `flatten` needed no work: a union-typed register splices into a key position as
any other field does, and both sides encode a union identically.

### An order comparison on the seek-terminal field is a range, not a filter

`X < 7`, and its three siblings, against a constant on the key field that **ends a seek
prefix** now fold into the seek as a bounded range instead of filtering rows the scan already
read. `{n = Ln} where F = content.File "…"; L = content.Line {file = F, line = Ln}; Ln >= 1000;
Ln < 1200` opens the scan at line 1000 rather than at line 1 — the cost of a window stops being
the cost of its offset. Measured on the fixture shape rather than argued: the folded plan reads
its ten rows where the filtered one reads a hundred to answer the same ten.

It is [I1](website/content/invariants.md#i1) being spent, and the half of it that had never
been: the encoding is order-preserving, so one contiguous run of the *value* order is one
contiguous run of the *key* order, which is what makes the range the exact answer rather than a
superset. The registry said as much and said no query lowered one; now one does.

Four rules keep it honest, each of them a wrong answer if got wrong. The bound is **terminal**
— every field before it fixed, nothing after it in the seek — and that is structural: the edges
are fields of `SeekKey::Bounded`, not entries in a parts list something could append to. It is
against a **constant** only; `A.x < B.y` and `N < "a".."` still filter or are refused by name.
The **first bound of each sense** takes the seek and a second of that sense stays a residual.
And a folded bound **leaves** the residual list, because the range is exact — but only where
every branch of a level folded it, since two alternatives are two key layouts and a field
terminal in one can sit behind a capture in the other.

A range is in the plan fingerprint, both edges and their inclusivity, so a resume cursor issued
against one window is never accepted by a plan with another — and the scan's resume position is
now checked against the range rather than against the prefix, or a bound spent by the seek
would be given back by the resume. No `CURSOR_VERSION` bump: a fingerprint that moves is the
mechanism, not a casualty.

### A fuzzy **prefix** match: `"parse"~<2`

`~` measures the edit distance to the whole stored string, which is the wrong question for a
search box: a five-character term is never within three edits of a fifteen-character
identifier, however well it prefixes it. `~<` asks whether some **prefix** of the stored string
is within the distance instead. Over 148,809 identifier-shaped names, `"parse_node"~1` answers
5 rows and the misspelt `"parsr"~<1` answers 7,416.

It is anchored, not a substring search — `"parsr"~<1` reaches `parser_function` and not
`my_parser_function` — which is what keeps it a set of ranges the automaton seeks between. `~`
is unchanged in meaning, in rows and in fingerprint; the two share one machine, one pair of
bounds (`1`–`3` edits, 63 characters) and one deferral for `!=`. Either may guide a seek or run
as a filter, and where a level carries both, `~` takes the guide because it is the one whose
states go dead on a long key.

A term no longer than its distance matches every stored string through the empty prefix. That
is left legal and documented rather than refused: it is what a search box does on the first
keystroke.

Anchoring is a **field on the plan**, folded into the residual and guide fingerprint tags, so a
resume cursor taken under one question is never accepted by the other — no `CURSOR_VERSION`
bump was needed. Two guards are new to it: an accepted row is not decoded past its accepting
prefix, and a band of keys sharing one accepting prefix is walked once rather than per row.

### `fjord` allocates from mimalloc

The tool links mimalloc as its `#[global_allocator]`, which is a whole-program choice and so
belongs in the binary rather than in a library every consumer would inherit it through. Under
concurrent scans glibc serialises the scan path's per-chunk allocations on its per-arena
mutexes; mimalloc's per-thread caches do not. The four workload medians improved 5–10% on an
8-core box with the load generator resident on it; individual round-pairs ranged from 4.7–14.5%,
and the gain was +38% at core saturation on a 14-core box —
[`bench/FINDINGS.md` §18](bench/FINDINGS.md). `the_global_allocator_is_mimalloc` is the guard
that catches the attribute going missing, since dropping it leaves a binary that still builds
and passes, only slower.

Both published binaries carry it, the statically linked one included, so the allocator is no
longer what separates them — what separates them is musl's libc, and nothing is measured on
that build.

### `Connection::discard`

A result read to its end with **no row decoded** — the server does the whole query, and the
client stops short of the one cost that is only ever the client's. It is what a load generator
should consume with, and it is not a cancel: nothing is cut short, so what it reports is the
server's own count. Decoding rows in a co-resident generator took ~40% of the box, which made
every throughput number partly a measurement of the generator.

## 0.1.0 — 2026-08-21

The first published artifact. Everything below is *what is there*, and the
[gaps](#what-is-not-in-it) are as much of the release as the features.

### Install

```bash
cargo add fjord-db                              # the Rust client
dotnet add package Boxops.Fjord.Client          # the .NET client
```

Or take the binary — `fjord`, the tool, and `fjord-viewer`, the code-search site — from the
release, which carries SLSA provenance naming the workflow that built it:

```bash
curl -LO https://github.com/boxops-uk/fjord/releases/latest/download/fjord
chmod +x fjord
gh attestation verify ./fjord --repo boxops-uk/fjord
./fjord --help
```

Each release also carries `fjord-x86_64-linux-musl` and `fjord-viewer-x86_64-linux-musl`: the
same code linked statically, with no glibc floor at all, for an older distro, Alpine or a
`scratch` container. Take those only if the one above will not run — musl's allocator is
slower under the load a server puts on it.

**Linux x86_64**, dynamically linked, needing **glibc 2.34 or newer** — Ubuntu 22.04, Debian
12, RHEL 9 and anything later. That is measured on the binary CI produces rather than inferred
from the runner it is built on. The store root's lock is POSIX `flock` and the default
transport is a Unix socket, so Windows is out of scope rather than untested; other Unix targets
are expected to work and are not built or tested by CI.

### The engine runs in a browser, and the design book runs it

The storage seam split from its backends — `fjord-store` is the `FactStore` trait and the
shared fixtures, `fjord-store-mem` and `fjord-store-fjall` are the two implementations — which
is what lets the engine compile to `wasm32-unknown-unknown` with no filesystem under it.
`fjord-inspect` is the JSON view of every construct (tokens, the parse tree, the lowered tree,
the plan, a run's steps, the stored rows), and `wasm/` is the module that carries them into a
page: 366 KB, 151 KB over the wire.

So [the design book](https://boxops-uk.github.io/fjord/) *is* the engine. A `:::demo` block in
a page is a running lexer, parser, typechecker, planner, executor or database table, editable
in place, and `/playground` is every view of one query at once with the executor steppable over
real rows. The pages are `website/content/`, parsed by both renderers rather than copied, and
the smoke check compares them page for page — a dialect that drifts is a page that reads
differently depending on which copy you found. `python3 website/serve.py` is still the copy
that reads with no toolchain.

### The documentation is one body, and it is tested

The design book is the website, verified claim by claim against the tree; its
invariants page is the canonical registry. `AGENTS.md` is the working contract for
contributors, `PLAN.md` is a roadmap rather than a phase tree (with the auth design and the
settled-decisions record inside it), and the two Glean documents merged into one (retired whole
in a later release, when the comparison was).
CI builds the site strictly and runs `scripts/check-docs.py`, which fails on a broken link, an
invariant citation the registry does not declare, a reference to a retired document, or a
build-plan phase number in code — each a way the documentation actually went stale once.

**And it is published by the same pipeline that ships the binaries.** Every push to main
deploys the interactive site to <https://boxops-uk.github.io/fjord/> — after the suite, the
drift gate, and the bundle being driven in a real browser — and every release carries that
bundle as an attested `fjord-docs-site.tar.gz`. Every page in it is a file rather than a
fallback, so a link into the middle of the book is a page and not a 404.

**Every error state is demonstrated by a test** that provokes it and asserts it at its
contract layer, fjall/OS bubbles excepted; the engine's corpus gate now covers every
diagnostic code, not only the deferrals. Comments across the workspace state the risk they
guard rather than the history of how the code got there.

### What is in it

- **An immutable fact database.** A database is created against a schema, written to, sealed,
  and thereafter only read. Facts are typed records identified by a `FactId`, grouped by
  predicate, stored in an LSM.
- **sigla**, a typed Datalog-flavoured query language: generators, joins, records, field
  access, constants and folding, aliases, constraints, denials, four comparisons, integer
  arithmetic, negation, disjunction, `never`, subqueries, and references followed in both
  directions.
- **A suspendable executor.** A query suspends to a bytes-only cursor and resumes exactly,
  releasing its snapshot at every chunk boundary — so a page held for an hour costs what one
  held for a millisecond does.
- **A schema language.** Files, namespaces, imports, a canonical form, per-predicate and
  whole-schema fingerprints, subset-containment compatibility, and `schema check` /
  `fingerprint` / `diff`.
- **Union types**, with **explicit append-only discriminants** — `{ num : int = 3 | text :
  string = 0 }`. A tag is written down rather than taken from the position, because a derived
  one renumbers the moment an alternative is inserted and every value already written then
  reads as a different alternative. Written and matched as `{alt = p}`, selected as `X.alt?`,
  and where the union is a leading key field, matching an alternative is a **seek** rather than
  a filter.
- **A wire protocol**, with a second implementation in C# that shares no code with the Rust
  one and a byte-for-byte golden test between the two encoders.
- **Parallel ingestion.** Many writers per database, behind per-key exclusion striped 64 ways.
- **A server**, a **client**, a **command-line tool**, and a **code-search site** built on
  nothing but the client.

### What is not in it

Stated because a missing feature discovered by a user is worse than one written down.

| Missing | What it means for you |
|---|---|
| **Authentication** | None, by design at this stage. The transport is the trust boundary: the server binds a Unix socket, TCP is opt-in per invocation, and access control belongs to a gateway in front |
| **`maybe` and `enum`** | Both are sugar over a union, which *is* there — but each needs a naming decision that enters the schema fingerprint, so both still parse and report themselves. Write the union out |
| **Stored derivation** | A derived predicate cannot be *declared*. Derived data is written by hand, which is what four predicates in the sample schema are |
| **Ingestion from files** | Facts arrive over the wire from a producer. The file format is defined and the pipeline is not wired to a command |
| **Arrays and sets** | A one-to-many is one fact per element |
| **`fjord write`, `db backup`/`restore`/`verify`, `completions`** | Named in the design, absent from the binary. A sealed database is a directory, so `tar` is the backup |
| **Per-predicate statistics** | Nothing feeds a selectivity heuristic, which is why the reorderer has none |
| **Per-stream flow control** | Bounded queues and per-connection backpressure in the meantime |
| **A resumable deadline** | A timeout unwinds terminally rather than handing back a cursor |

Two operational facts that are easy to meet and are not in that table because they are
properties of what *is* built:

- **A `Writable` database is never merged.** Trees are compacted at `finish`, so a long-lived
  ingest-then-query workflow pays unmerged-LSM seek cost until it is sealed — up to two orders
  of magnitude on a page seek. Seal before you measure read performance.
- **The interning lookup cache is a fixed budget per open database** (~256 MiB, two
  generations of 128 MiB) with no operator dial. It is measured at its ceiling at 18.3M facts
  and untested above that.

### Notes for anyone who has been tracking `main`

- **Unions landed (8.6), and nothing else moved with them.** The marker table gained `0x52`,
  *appended* — the eleven markers below it are unchanged, so every database already written is
  read by exactly the bytes that wrote it. The wire's descriptor and value tables gained a tag
  each, also appended, so an older peer meets one and says so rather than mis-reading what
  follows. `schemas/code.sigla` is deliberately untouched: a union there would move its
  fingerprint and the constants two .NET clients carry, and that is a flag day with nothing to
  do with unions working.
- **There is no built-in schema.** `fjord create` requires `--schema <file>`; a server carries
  no data schema of its own, and a database that embeds no schema copy is listed rather than
  served. `schemas/code.sigla` is a sample rather than a default.
- **`fjord shell` requires a database.** The embedded demo, and the `example/` corpus it was
  seeded from, are gone.
- **`fjord finish` used to seal against the tool's built-in schema** regardless of what the
  database embedded, which computed the content identity over misread rows for any database
  built against another schema. It reads the embedded copy now.
- The command-line tool moved to `crates/fjord-cli`, and `Connection::control` no longer takes
  a schema.
- **The .NET namespace is `Boxops.Fjord.Client`**, matching the package id — so
  `dotnet add package Boxops.Fjord.Client` is followed by `using Boxops.Fjord.Client;` and
  there is one name rather than two. The projects and the solution are renamed to match.
