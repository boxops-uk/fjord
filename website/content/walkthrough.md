---
title: A guided tour
description: One session from an empty directory to a sealed database — writing facts, asking questions, reading a plan, paging, and watching a finished database refuse a writer.
---

One session, start to finish: an empty directory in, a sealed database out, with a look at
the interesting things along the way. It takes about ten minutes to follow, and it assumes
you have read [Getting started](getting-started.html) — this is the same path with more of
the corners in it.

The data is a **real code index of real code**: the repository's own .NET solution — the
client library, the demo producer, and the indexer — indexed by that same indexer, with
Roslyn answering what every name means. Every command below was run, and every block of
output is what it printed.

Set up once (`FJ` is a Fjord checkout with `cargo build --release` done and the .NET SDK
on the path):

```bash
cd /tmp && mkdir fj-tour && cd fj-tour
FJ=/path/to/fjord
AP=$FJ/target/release/fjord
```

## 1. A schema you can read

The sample schema is a file, `schemas/demo.sigla`, and it parses like any other. Ask it
what it thinks it is:

```bash
$AP schema check $FJ/schemas/demo.sigla
```

```text
11 predicate(s) in 1 file(s)
  schemas/demo.sigla
fingerprint 0x03678fcd1e7924e3
```

Eleven predicates, and **one for each construct the language has** — that is what the file
is for. It is small enough to read in a sitting and complete enough that no shape goes
untested, which is why the instruments and the interactive site both measure over it.

The fingerprint is computed over the **canonical form** — fully-qualified names, no
comments, no whitespace, no declaration order. Two files that mean the same thing have the
same number. Per-predicate fingerprints come out too:

```bash
$AP schema fingerprint $FJ/schemas/demo.sigla
```

```text
ID  PREDICATE      TYPE                                                       FINGERPRINT
0   code.Decl      { file: code.File, name: string, line: int } -> string     755bfcc416fd
1   code.Digest    { file: code.File } -> { sha256: bytes }                   69de1231a679
2   code.Extends   { type: code.Decl, base: code.Decl }                       9590300c5a52
3   code.Extent    { decl: code.Decl } -> { from: { line: int, … }, … }       77a58f8ed3b6
4   code.File      string                                                     3ebcbfa8a901
5   code.Kind      { decl: code.Decl, what: { data: string = 5 | … } }        15350b25ba50
6   code.KindOf    { what: { data: string = 5 | … }, decl: code.Decl }        4a780f20a7cd
…
```

Two of those predicates are the same data twice: `code.Kind` leads with the declaration
and `code.KindOf` leads with the tag. That is not redundancy, it is the index design — and
[step 6](#6-read-the-plan) is where it becomes visible. The ids are **sorted by name**, not
by declaration order, which is where a position comes from.

## 2. Create, and serve

The database is created against `dotnet.sigla`, not the file above: `demo.sigla` is the
*language* fixture — one predicate per construct — and what the indexer writes is the
sixty-five-predicate set a real producer needs. `--schema-path` because that set is
composed by import from five files.

```bash
$AP --data-dir ./db --schema-path $FJ/schemas create code --schema $FJ/schemas/dotnet.sigla
$AP --data-dir ./db serve --ready-file ./ready &
while [ ! -e ./ready ]; do sleep 0.1; done
```

```text
created code (01M1PCG31908ZED3YT1YKQBT9D) against /path/to/fjord/schemas/dotnet.sigla
fjord serve
  data dir   ./db
  socket     ./db/fjord.sock
  protocol   4
  connections 524288 at once  (half the descriptor limit; --max-connections sets it)
  databases  1
    code                 writable
```

**No schema is printed, because a server has none of its own.** Each database is served with
its own embedded copy, so one store root can hold artifacts built from different declarations
— and one that embedded no copy is listed rather than served, since the only alternative is to
guess how its rows decode.

## 3. Write facts, holding no ids

The producer is `Boxops.Fjord.Indexer`, pointed at the .NET code it is itself part of.
`--framework` pins one target: the client library multi-targets, and without it the run
would fan out into one database per framework — which is the right default and one database
too many for a tour.

```bash
dotnet run --project $FJ/clients/dotnet/Boxops.Fjord.Indexer --configuration Release -- \
  --sln $FJ/clients/dotnet/Boxops.Fjord.slnx --root $FJ/clients/dotnet \
  --framework net10.0 --at ./db/fjord.sock//code
```

```text
indexing /path/to/fjord/clients/dotnet
  paths relative to /path/to/fjord/clients/dotnet
  schema fingerprint 4e90774b9a0814cc
  entry point /path/to/fjord/clients/dotnet/Boxops.Fjord.slnx
  5 C# project(s) in the solution
  built Boxops.Fjord.Demo.csproj (net10.0, 4 files, 3.1s)
  built Boxops.Fjord.Indexer.csproj (net10.0, 15 files, 3.1s)
  built Boxops.Fjord.Scip.csproj (net10.0, 11 files, 3.1s)
  built Boxops.Fjord.Client.csproj (net10.0, net8.0, 15 files, 5.3s)
  built Boxops.Fjord.Tests.csproj (net10.0, 32 files, 2.6s)
  build layer: 57 project(s), 5 from a design-time build, 61 file(s) attributed exactly
  solution Boxops.Fjord.slnx: 5 of 5 listed project(s) have a project fact to be an edge to
  1 target framework(s) — net10.0, loaded in 6.6s

connecting to ./db/fjord.sock//code, 1 writer(s)
  connected: protocol 4, 67 predicates, schema 4e90774b9a0814cc

       13 files        23,738 facts      9,144 facts/s  Boxops.Fjord.Demo
       25 files        84,036 facts     10,846 facts/s  Boxops.Fjord.Indexer
       53 files       163,014 facts     16,434 facts/s  Boxops.Fjord.Tests

indexed 61 file(s) in 12.2s
  src.File                                    61
  src.Symbol                              19,299
  src.FileLine                            20,805
  src.FileLineAt                          20,805
  config.Setting                               7
  msbuild.Project                             57
  csharp.Name                              3,591
  csharp.Class                               492
  csharp.Method                            2,193
  codemarkup.Definition                    1,093
  codemarkup.FileXRef                     17,120
  codemarkup.SearchEntry                   1,093
  …
  total                      179,214 facts in 92 blocks
  server                     158,242 created, 1,728,840 deduped
  writing                        4.9s  (summed over 1 writer(s), overlapped — not wall clock)
  queueing                       2.3s  (walk blocked on a full queue)
  contended                      2.0s  (241 of 179,214 facts waited for a batch)
  throughput                  14,639 facts/s

references: 21,361 resolved, 6,883 to declarations outside the index, 2 unresolved
```

**Sixty-five predicates in the schema, and the run filled fifty-eight of them.** The
handshake above says sixty-seven, which is those sixty-five plus the two virtual
`fjord.db.*` the server appends; the seven a run leaves empty are all in the schema.

**One of those seven is not empty in the database**, and the difference is worth knowing:
the tally counts what the producer wrote *top-level*, and `csharp.FullName` is only ever
written nested inside the entities that key on it — so the producer reports zero for it
while `X where X = csharp.FullName _` over this very index answers 402 rows. A predicate counted zero here means
nobody wrote it *as a fact of its own*, not that nothing in the database has one. The set is five schemas
composed: `src` for files and lines, `config` for what the index was built against,
`msbuild` for the project graph, `csharp` for the semantic layer, and `codemarkup` for the
surface a UI reads. A producer writes what it has, and a predicate nobody fills is a name in
a file rather than a hole in the database.

`contended` is what the walk paid for sharing: several threads producing facts into
sixty-five per-predicate batches, and a couple of hundred of the 179,214 found one already
held. That number is the one figure on this page that does not reproduce — it is how the
threads happened to interleave, and a second run of the same command gives a different one,
where the fact count beside it does not.

Read the two server counts together: **one and three-quarter million facts touched, a
hundred and fifty-eight thousand rows exist** — because every reference the walk wrote was
the target fact **nested inline** rather than an id:

```text
codemarkup.SymbolXRef {
  target = src.Symbol                                     ← a whole fact, not an id
    "scip-csharp-2 nuget Boxops.Fjord.Client 0.4.0.0
     Boxops/Fjord/Client/Crc32#",
  file = src.File "Boxops.Fjord.Client/Blocks.cs",        ← nested again
  span = { start = 4808, length = 5 }
}
```

The server interns each nested fact bottom-up — a parent's key has no bytes until its
children have ids — and substitutes the id. A file named a few thousand times is written
once and deduplicated the rest, which is where 1,728,840 of those facts went. That is why
an indexer needs no map from entities to identities and no emission order: it emits what it
holds where the syntax walk stands. (The `6,883 to declarations outside the index` are
references to the BCL and to packages — real code points at code nobody walked, and the
indexer says so rather than inventing targets.)

## 4. Ask the first questions

```bash
$AP --data-dir ./db query code 'F where src.File F' --limit 3
```

```text
VALUE
Boxops.Fjord.Client/BlockTarget.cs
Boxops.Fjord.Client/Blocks.cs
Boxops.Fjord.Client/Boxops.Fjord.Client.csproj
3 row(s)
fjord: stopped at 3 rows; raise or drop --limit to see the rest
```

`--limit` is **not** `LIMIT`: the query is unchanged, the server does the work up to the
point the in-band cancel lands, and what it bounds is what crosses the socket.

The `.csproj` in there is not a mistake. A project file is a `src.File` too — that is what
the build layer keys its facts on — so "every file this index holds" means every file,
including the ones nobody compiles.

A record head names the output fields, and a prefix is a range on the field the key leads
with:

```bash
$AP --data-dir ./db query code \
  '{name = N, line = L} where codemarkup.SearchEntry
     {nameLowercase = "b".., name = N, kind = _, symbol = _, file = _, line = L}' --limit 5
```

```text
LINE  NAME
27    BadFacts
29    BadQuery
36    Bare
548   BaseOfKind
84    Batch
5 row(s)
fjord: stopped at 5 rows; raise or drop --limit to see the rest
```

The columns came back alphabetically because a *query's* record fields are sorted by name
when it is lowered — so `{a = 1, b = 2}` and `{b = 2, a = 1}` are one type and one set of
bytes. A *schema's* fields are never sorted; that order is the key order, and it is why
`nameLowercase` leading the key is what makes the prefix above a seek.

## 5. A reference is an id, until you ask

```bash
$AP --data-dir ./db query code 'R where R = codemarkup.SymbolXRef _' --format jsonl --limit 2
```

```json
"#9:225"
"#9:227"
```

`#9:225` is a `FactId`: predicate 9, sequence 225. sigla cannot ask what it names — a query
names a fact by its key, never by its number, and putting an id in the language would put a
storage detail in a query. So the question goes to the **protocol**, and the client asks it:

```bash
$AP --data-dir ./db query code 'R where R = codemarkup.SymbolXRef _' \
  --format jsonl --limit 2 --expand
```

```json
{"target": "scip-csharp-2 nuget Boxops.Fjord.Client 0.4.0.0 Boxops/Fjord/Client/Crc32#", "file": "Boxops.Fjord.Client/Blocks.cs", "span": {"start": 4808, "length": 5}}
{"target": "scip-csharp-2 nuget Boxops.Fjord.Client 0.4.0.0 Boxops/Fjord/Client/Crc32#", "file": "Boxops.Fjord.Client/Blocks.cs", "span": {"start": 4834, "length": 5}}
```

That is the **logical form**: the same shape a producer sends, and the same shape the
content hash is computed over. The recursion, the depth bound and the cache are the
client's, because how deep to expand is a display decision. The server does one point read
per distinct id.

The target is a **string**, and deliberately: `src.Symbol` is a SCIP symbol, so a reference
can name a declaration in another database — which is what makes a fan-out across
repositories a join rather than a merge. A fact id would mean nothing outside the database
that issued it.

## 6. Read the plan

The shell holds the schema the server serves, so it compiles locally and can show a plan
without running anything.

```bash
$AP --data-dir ./db shell code
```

Find-references, which is the question this schema is shaped for:

```text
sigla> :plan {f = F, at = S} where
         codemarkup.SearchEntry
           {nameLowercase = _, name = "Crc32", kind = _, symbol = T, file = _, line = _};
         codemarkup.SymbolXRef {target = T, file = F, span = S}
  r0 <- codemarkup.SearchEntry scan
       where name == "Crc32"
  r1 <- codemarkup.SymbolXRef seek[target = r0.symbol, file = _, span = _]
  head {at = r1.span, f = r1.file}
```

Two levels, and the plan says exactly what each costs. `SearchEntry`'s key leads with
`nameLowercase`, so a constraint on `name` — the *second* field — cannot narrow the scan:
the leading field is open, and the name can only **filter** rows the scan already produced.
Then `SymbolXRef`'s key leads with `target`, so the symbol **seeks**: `r0.symbol` is spliced
into the seek key, and only the references to that symbol are read.

Ask for the outcome as well as the intent:

```bash
$AP --data-dir ./db query code \
  '{f = F, at = S} where
     codemarkup.SearchEntry
       {nameLowercase = _, name = "Crc32", kind = _, symbol = T, file = _, line = _};
     codemarkup.SymbolXRef {target = T, file = F, span = S}' --profile
```

```text
AT         F
{4808, 5}  #56:2
{4834, 5}  #56:2
{4847, 5}  #56:2
{4860, 5}  #56:2
{4873, 5}  #56:2
5 row(s)
STEP                    EXAMINED
codemarkup.SearchEntry  1093      full scan
codemarkup.SymbolXRef   5
1098 examined, 5 produced
```

A thousand and ninety-three search entries examined to find one, then exactly five rows for
its references. The fix is not a query change; it is asking the key the way it is keyed:

```text
sigla> :plan E where E = codemarkup.SearchEntry {nameLowercase = "crc"..}
  r0 <- codemarkup.SearchEntry seek[nameLowercase = "crc".., name = _, kind = _,
                                    symbol = _, file = _, line = _]
  head r0#
```

The same rows, entered by the field the key leads with, so a name prefix is a **range**
rather than a filter — and case-folded, because "find anything spelled like crc" and "find
exactly `Crc32`" are different questions and the schema keeps a row for each.

Every other shape the language compiles to — reading through a reference, arithmetic, a
negation, a denial, a disjunction — is laid out side by side in
[Executor & resume](executor.html#every-construct-as-a-plan).

## 7. Paging that holds a real cursor

```text
sigla> :limit 3
  3 row(s) per page
sigla> F where src.File F
  : str
"Boxops.Fjord.Client/BlockTarget.cs"
"Boxops.Fjord.Client/Blocks.cs"
"Boxops.Fjord.Client/Boxops.Fjord.Client.csproj"
  :more for the next 3 — 3 so far
sigla> :more
"Boxops.Fjord.Client/Buffers.cs"
"Boxops.Fjord.Client/Crc32.cs"
"Boxops.Fjord.Client/Errors.cs"
  :more for the next 3 — 6 so far
```

`:more` is not a re-run with an offset. The server suspended the query, encoded one
detached row per open loop level into a **bytes-only token**, and handed it over; the next
page resumes from those bytes. Nothing is held server-side between pages, which is what
makes paging stateless — a web tier can page without holding a connection.

## 8. A mistake is a caret, not a round trip

```bash
$AP --data-dir ./db query code 'X where src.Nope X'
```

```text
error[reject/unknown-predicate]: `src.Nope` is not a predicate in this schema
  ┌─ <input>:1:9
  │
1 │ X where src.Nope X
  │         ^^^^^^^^^^
```

The client compiled it against the schema the server serves, so the diagnostic arrived
without asking the server anything. Where the two compilers could disagree, the **server**
decides what runs.

## 9. Your own schema

A schema is a file, and creating a database against one freezes it there:

```schema
# people.sigla
schema demo {

  # A scalar key: the whole key is one string.
  predicate Person : string

  # A record key. Field order is key order, so this is fast at
  # "who does this person know" and only filters the other way.
  predicate Knows : { from : Person, to : Person }

  # A value side (`-> T`) is fetched only when a query asks for it.
  predicate Age : { person : Person } -> int
}
```

```bash
$AP schema fingerprint people.sigla
```

```text
ID  PREDICATE    TYPE                                    FINGERPRINT
0   demo.Age     { person: demo.Person } -> int          a3b1b02ea361
1   demo.Knows   { from: demo.Person, to: demo.Person }  080f8e02ff95
2   demo.Person  string                                  34b7f70464c8
```

Adding a predicate is compatible. Changing one — including **reordering its fields** — is
not, because field order is encoding order:

```bash
$AP schema diff people.sigla people2.sigla    # a fourth predicate added
$AP schema diff people.sigla people3.sigla    # `Knows` fields swapped
```

```text
Compatible (1 added)
  + demo.Employer

Breaking (1 predicate(s))
  ~ demo.Knows  (modified: 080f8e02ff957601 → c1779584fe40b587)
```

Create a database against it, through the running server:

```bash
$AP create './db/fjord.sock//people' --schema people.sigla
```

```text
created people (01M0BN8AG2APYZB3B5YXGY58VW) against people.sigla
```

The address is `[where//]name[@instance]`, and it is the same grammar every client takes —
the CLI, the viewer, and the .NET indexer. See [CLI reference](cli.html#addressing).

## 10. Seal it, and watch it refuse

```bash
$AP --data-dir ./db finish code
```

```text
sealing code — merging trees, then computing identity
sealed code: 158242 facts, 45963455 bytes, identity 0xc5a6a64833095a1a
```

`finish` makes the data durable, **merges every tree**, computes
`hash(canonical schema, base facts)`, records it, and flips the status as the last durable
act. Now the database is an artifact:

```text
NAME  INSTANCE                    STATUS    SCHEMA        CONTENT       FACTS   BYTES     CREATED
code  01M1QAWV1J9QZ7YJ9MTRZWHYKV  complete  4e90774b9a08  c5a6a6483309  158242  45963455  2026-09-04 23:08:43Z
```

and every writer is refused at the handshake, structurally rather than per fact — pointing
the indexer at it again says so and stops, rather than discovering it a fact at a time:

```text
connecting to ./db/fjord.sock//code, 1 writer(s)
could not write to ./db/fjord.sock//code: ModeRefused: `code` is complete: it takes no more writes
```

## What the tour showed

| You saw | The rule behind it |
|---|---|
| 1,728,840 facts deduped against 158,242 created | Interning **is** the dedup; a nested reference resolves to one row |
| A name that filtered and a symbol that seeked | Field order is key order, and key order is the index design |
| `#9:225` in a row, expanded on request | Stored, a reference is a `FactId`; expansion is a protocol question, not a query one |
| A scan, then a seek spliced with its id | A plan is a nested loop, and the order of its steps *is* the nesting |
| `:more` returning the next three | A resume token is bytes, so paging holds nothing open |
| A caret with no round trip | The client compiles; the server decides what runs |
| `complete`, and a refused writer | `Writable → Complete` is one way, and enforced at session establishment |
