---
title: Getting started
description: Get the binaries, create a database, start a server, write some facts, and ask it a question — in about five minutes.
---

By the end of this page you will have a database on disk — files, declarations and the
references between them — a server in front of it, and answers to a few questions about it.
Then, if you want a real corpus rather than a worked example, a second database built by
pointing the .NET indexer at real source.

Everything below was run against the repository as it stands, and every block of output is
what it actually printed.

## Prerequisites

| You need | Why | Optional? |
|---|---|---|
| A Rust toolchain (edition 2024, stable) | Builds `fjord`, which is the CLI and the server | no |
| .NET SDK 10 | Runs the producers that fill a database with facts | only for steps 4 and 9 |
| `python3` | Serves these docs locally | yes |

There is nothing else — no database to install, no daemon to configure. The storage engine
([fjall](https://github.com/fjall-rs/fjall)) is a Rust dependency, and a database is a
directory.

## 1. Get the binaries

:::note Prebuilt binaries
Every [GitHub release](https://github.com/boxops-uk/fjord/releases/latest) carries `fjord` for
Linux x86_64, which skips this whole step:

```bash
curl -LO https://github.com/boxops-uk/fjord/releases/latest/download/fjord
chmod +x fjord
gh attestation verify ./fjord --repo boxops-uk/fjord   # what built it, and from what
```

That one needs glibc 2.34 or newer — Ubuntu 22.04, Debian 12, RHEL 9 and later. The
`-x86_64-linux-musl` builds beside it are the same code linked statically, for an older
distro, Alpine or a `scratch` container. Both carry the same allocator, but every number the
project publishes is measured on the first, so they are the second choice rather than the
first.
:::

```bash
cargo build --release --bin fjord
```

That gives you `target/release/fjord`, the one command-line tool. Use `--release`. A debug build of the executor is several times slower, and is not the
thing you want a first impression of. [Building from source](building.html) has the rest
of the workspace, if you want it.

## 2. Create a database

A database is created **against a schema**, which is then frozen and embedded in it for
life. There is no default, and that is deliberate: the schema is what says what every row
in the database means.

`schemas/demo.sigla` in the repository is the worked example — eleven predicates in a
namespace called `code`, describing files, declarations, the references between them, and a
kind per declaration. It is the **language** fixture: one predicate for every shape the type
model can hold, small enough to read in a sitting, and what the interactive site, the
benchmarks and the corpus all measure over. Start here, and see
[step 9](#9-index-real-source) for the schema a real indexer writes.

```bash
fjord --data-dir ./db create code --schema schemas/demo.sigla
```

```text
created code (01M1N3FDD16ZXDJRMM164TD1G4) against schemas/demo.sigla
```

The name is `code`; the ULID is the **instance**. `--data-dir` is the **store root** — the
directory databases live under, and the thing a server owns.

```bash
fjord --data-dir ./db list
```

```text
NAME  INSTANCE                    STATUS    SCHEMA        CONTENT  FACTS  BYTES  CREATED
code  01M1N3FDD16ZXDJRMM164TD1G4  writable  03678fcd1e79  -        -      -      2026-09-04 02:20:31Z
```

`list` reads sidecar files and never opens the storage engine, so it works while a server
holds every database under the root. The `SCHEMA` column is the leading half of
`demo.sigla`'s fingerprint, `0x03678fcd1e7924e3` — the number a client has to match to
connect at all.

## 3. Start a server

Readers do not open the directory themselves; they talk to a server. Locally that means a
Unix socket, and starting one is a single command.

```bash
fjord --data-dir ./db serve --ready-file ./ready &
while [ ! -e ./ready ]; do sleep 0.1; done
```

```text
fjord serve
  data dir   ./db
  socket     ./db/fjord.sock
  protocol   4
  connections 524288 at once  (half the descriptor limit; --max-connections sets it)
  databases  1
    code                 writable
```

`--ready-file` appears **after** the listener is accepting, so waiting on it is a signal
rather than a race. The socket path is derived from the store root, which is how a client
finds a server without being told where the data is.

:::warn Socket paths are short for a reason
A Unix socket path has a hard length limit of about 100 bytes. If your store root is deep,
pass `--socket /tmp/fjord.sock` explicitly and name it in the address —
`/tmp/fjord.sock//code`.
:::

## 4. Put some facts in it

Facts arrive over the wire, from a producer — there is no `fjord write` command yet
([file ingestion](status.html) is unbuilt). The producer for this schema is
`Boxops.Fjord.Demo`, a C# program that writes a handful of facts about a made-up Python
project and then asks a few questions back.

```bash
dotnet run --project clients/dotnet/Boxops.Fjord.Demo --configuration Release -- \
  --at ./db/fjord.sock//code
```

```text
connecting to ./db/fjord.sock//code
  our schema fingerprint 03678fcd1e7924e3
  connected: protocol 4, 13 predicates, schema 03678fcd1e7924e3

writing 6 declarations, every reference nested
  created 9, deduped 3 (of 12 facts touched)
  6 declarations + 3 files = 9 distinct facts
```

`13 predicates` is eleven declared plus the two the server reserves — `fjord.db.List` and
`fjord.db.Interning`, the catalogue answered as facts.

`deduped 3` is the number worth noticing, small as it is. The producer keeps track of **no
ids at all**. It sends each declaration with its file nested inside it; the server writes
each nested fact once and recognises it every time after, so twelve facts touched leaves
nine rows. The [guided tour](walkthrough.html#3-write-facts-holding-no-ids) shows what that
looks like on a corpus where the ratio is ten to one.

To write facts from your own program, see
[the client section](clients.html#writing-facts-from-rust).

## 5. Ask it something

```bash
fjord --data-dir ./db query code 'F where code.File F' --limit 3
```

```text
VALUE
query/plan.py
store/codec.py
store/keys.py
3 row(s)
fjord: stopped at 3 rows; raise or drop --limit to see the rest
```

A query is the shape you want back, the word `where`, and what to match. Name the fields
you care about, and the shape at the front decides the columns:

```bash
fjord --data-dir ./db query code \
  '{name = N, line = L} where code.Decl {file = F, name = N, line = L}' --limit 5
```

```text
LINE  NAME
12    key_of
48    key_prefix
77    key_successor
31    CodecError
7     encode_key
5 row(s)
fjord: stopped at 5 rows; raise or drop --limit to see the rest
```

Find-references — the question a code index exists to answer — is a join through a
reference. `code.Ref` holds two declarations, so "everything that refers to `key_of`" is one
statement matching the target and another naming the referrer:

```bash
fjord --data-dir ./db query code \
  '{f = F, l = L} where code.Ref {to = code.Decl {name = "key_of"},
                                  from = code.Decl {file = F, line = L}}'
```

```text
F     L
#4:3  5
1 row(s)
```

`F` printed as `#4:3` — a fact id, predicate 4 sequence 3, because that is what a reference
is once stored. Expansion is the client asking the server *what fact does this id name*, and
it is off unless you ask for it: it costs one point read per distinct reference.

```bash
fjord --data-dir ./db query code \
  '{f = F, l = L} where code.Ref {to = code.Decl {name = "key_of"},
                                  from = code.Decl {file = F, line = L}}' \
  --expand
```

```text
F              L
query/plan.py  5
1 row(s)
```

## 6. Use the shell

```bash
fjord --data-dir ./db shell code
```

```text
fjord shell — `code` on ./db/fjord.sock
  13 predicate(s) · rows print as jsonl · :help for commands
```

The shell compiles what you type **locally**, against the schema the server said it
serves — so a mistake is a caret under the word rather than a round trip, and `:plan` can
show you the plan without running anything.

```text
sigla> :limit 3
  3 row(s) per page
sigla> D.name where D = code.Decl _
  : str
"key_of"
"key_prefix"
"key_successor"
  :more for the next 3 — 3 so far
sigla> :more
"CodecError"
"encode_key"
"Plan"
  :more for the next 3 — 6 so far
```

`:more` holds a real resume token across a real round trip. Full command list:
[Shell reference](shell.html).

## 7. Seal it

A database is an **artifact**. Sealing flushes and merges every tree, hashes the content,
records the identity, and flips the status — after which every write is refused, forever.

```bash
fjord --data-dir ./db finish code
```

```text
sealing code — merging trees, then computing identity
sealed code: 11 facts, 68522 bytes, identity 0x0c60c170d491fc41
```

The identity is `hash(canonical schema, base facts)` — a content hash, so the same inputs
build a byte-identical answer whatever order they were written in. `list` now shows the
database as `complete`, with that number under `CONTENT`:

```text
NAME  INSTANCE                    STATUS    SCHEMA        CONTENT       FACTS  BYTES  CREATED
code  01M1N3FDD16ZXDJRMM164TD1G4  complete  03678fcd1e79  0c60c170d491  11     68522  2026-09-04 02:20:31Z
```

and any writer is refused at the handshake, before it sends a byte of data:

```text
Boxops.Fjord.Client.FjordServerException: ModeRefused: `code` is complete: it takes no more writes
```

Merging at `finish` is not cosmetic: an unmerged tree was measured seeking at up to 180×
a merged one, and the artifact roughly halves on disk. See
[Performance](performance.html).

## 8. From your own program

Everything above is a client of the same protocol, and so is your program. One dependency,
either language:

```bash
cargo add fjord-db                              # Rust
dotnet add package Boxops.Fjord.Client          # .NET — net8.0 or net10.0, no dependencies
```

`fjord-db` is a façade over the three crates that do the work — `fjord-client`, `fjord-schema`
and `fjord-wire` — so reading the database this tour just built is:

```rust
use std::{path::Path, sync::Arc};
use fjord_db::{Connection, Mode, Schema};

let mut connection = Connection::connect(
    Path::new("./db/fjord.sock"),
    "code",
    Arc::new(Schema::empty()),   // a reader has no claim to make
    Mode::ReadOnly,
    false,
)?;

let schema = Arc::new(connection.served_schema()?);   // the only way to be right about it

let mut rows = connection.query("F where code.File F")?;
for row in connection.take(&mut rows, 20)? {
    println!("{row:?}");
}
```

`take` reads *n* rows and leaves the stream open, because the server suspends holding a
bytes-only cursor and has already released its snapshot: a pause of an hour costs it what a
pause of a millisecond does.

The client must supply the schema — the value codec sends no field names and no type markers,
since both ends already have them — which is why a reader asks the database for its own.

## 9. Index real source

`demo.sigla` is the language fixture, and eleven predicates is not what indexing a real
repository needs. The set for that is `schemas/dotnet.sigla` — sixty-five predicates
composed by import from five files: `src` for files and lines, `config` for what the index
was built against, `msbuild` for the project graph, `csharp` for the semantic layer, and
`codemarkup` for the language-independent surface a UI reads.

**It is a different schema, so it is a different database.** A schema is frozen into a
database at `create`, and the two sets share no predicate, so nothing here writes into the
one above.

```bash
fjord --data-dir ./db2 --schema-path schemas create dotnet --schema schemas/dotnet.sigla
fjord --data-dir ./db2 serve --ready-file ./ready2 &
while [ ! -e ./ready2 ]; do sleep 0.1; done
```

```text
created dotnet (01M1PCCPWV76QCE9XCS7WG2WNF) against schemas/dotnet.sigla
fjord serve
  data dir   ./db2
  socket     ./db2/fjord.sock
  protocol   4
  connections 524288 at once  (half the descriptor limit; --max-connections sets it)
  databases  1
    dotnet               writable
```

`--schema-path` because `dotnet.sigla` is composed by import; an entry file's own directory
is always searched first, so a directory of schemas that import each other needs no more
than this.

The producer is the repository's own .NET indexer, pointed at the .NET code it is itself
part of: it builds each project, asks Roslyn what every name in the result means, and writes
the answers down the socket. `--framework` pins one target, because the client library
multi-targets and without it the run would fan out into one database per framework.

```bash
dotnet run --project clients/dotnet/Boxops.Fjord.Indexer --configuration Release -- \
  --sln clients/dotnet/Boxops.Fjord.slnx --root clients/dotnet \
  --framework net10.0 --at ./db2/fjord.sock//dotnet
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

connecting to ./db2/fjord.sock//dotnet, 1 writer(s)
  connected: protocol 4, 67 predicates, schema 4e90774b9a0814cc

       13 files        23,738 facts      9,144 facts/s  Boxops.Fjord.Demo
       25 files        84,036 facts     10,846 facts/s  Boxops.Fjord.Indexer
       53 files       163,014 facts     16,434 facts/s  Boxops.Fjord.Tests

indexed 61 file(s) in 12.2s
  …
  total                      179,214 facts in 92 blocks
  server                     158,242 created, 1,728,840 deduped
  writing                        4.9s  (summed over 1 writer(s), overlapped — not wall clock)
  queueing                       2.3s  (walk blocked on a full queue)
  contended                      2.0s  (241 of 179,214 facts waited for a batch)
  throughput                  14,639 facts/s

references: 21,361 resolved, 6,883 to declarations outside the index, 2 unresolved
```

**`1,728,840 deduped`** is the same trick as step 4 at scale: one and three-quarter million
facts touched, a hundred and fifty-eight thousand rows written, because every reference the
walk sends is the target fact nested inline and a file named a few thousand times is stored
once.

Find-references over that corpus is two seeks. `codemarkup.SearchEntry` finds the symbol by
name, and `codemarkup.SymbolXRef` leads with the target, so its uses are a range rather than
a scan:

```bash
fjord --data-dir ./db2 query dotnet \
  '{f = F, at = S} where
     codemarkup.SearchEntry
       {nameLowercase = _, name = "Crc32", kind = _, symbol = T, file = _, line = _};
     codemarkup.SymbolXRef {target = T, file = F, span = S}' --expand
```

```text
AT         F
{4808, 5}  Boxops.Fjord.Client/Blocks.cs
{4834, 5}  Boxops.Fjord.Client/Blocks.cs
{4847, 5}  Boxops.Fjord.Client/Blocks.cs
{4860, 5}  Boxops.Fjord.Client/Blocks.cs
{4873, 5}  Boxops.Fjord.Client/Blocks.cs
5 row(s)
```

[A guided tour](walkthrough.html) follows this database rather than the demo one, and reads
the plan behind that query.

## What to read next

- [A guided tour](walkthrough.html) — the same path over the indexed corpus, with more of the
  interesting corners.
- [Concepts](concepts.html) — facts, predicates, keys, values, lifecycle.
- [sigla query language](query-language.html) — the whole language, construct by construct.
- [CLI reference](cli.html) — every command, flag, address form and config key.
- [Clients](clients.html) — the .NET half, and what it takes to write a third client.
