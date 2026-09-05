# The .NET client

A C# implementation of Fjord's wire protocol, and a console program that uses it to
write facts into a real database and query them back.

It exists to answer a question the Rust tests cannot: **is the protocol implementable
from outside?** A client written in the same repository, against the same types, can
agree with the server by accident — sharing a constant, sharing an enum, sharing an
assumption nobody wrote down. This one shares nothing but the specification, and it
found two things the Rust side had not: a block header length stated in one place and
miscounted in another, and a fingerprint that would have depended on the client's byte
order.

| Project | What it is |
|---|---|
| `Boxops.Fjord.Client` | the library: varints, CRC-32, the value codec, blocks, frames, the handshake, a connection. **The one packable project** — published as `Boxops.Fjord.Client`, targeting `net8.0` and `net10.0`, with no dependencies |
| `Boxops.Fjord.Demo` | a console program that writes a small code index and queries it |
| `Boxops.Fjord.Indexer` | a **real** indexer — Buildalyzer and Roslyn over a .NET checkout, at whatever size the checkout is ([its README](Boxops.Fjord.Indexer/README.md)) |

## As a package

```bash
dotnet add package Boxops.Fjord.Client
```

`Boxops.Fjord.Client/README.md` is the package's front page, and its example is compiled against
this library rather than written next to it. Shared metadata — version, licence, repository,
Source Link, deterministic build — is in `Directory.Build.props`, and nothing there is
packable unless it says so, which is how the two console programs stay out of the feed.

```bash
dotnet pack clients/dotnet/Boxops.Fjord.Client -c Release -o ./nupkg
```

That produces the `.nupkg` and a `.snupkg` of symbols. CI runs the same two commands, so a
package that would not build is a red branch rather than a discovery at publish time.

## Running it

From the repository root:

```sh
cargo build --bin fjord

rm -rf /tmp/fj-demo && mkdir -p /tmp/fj-demo
./target/debug/fjord --data-dir /tmp/fj-demo/db create code
./target/debug/fjord --data-dir /tmp/fj-demo/db serve \
    --socket /tmp/fj-demo/fjord.sock \
    --ready-file /tmp/fj-demo/ready &

# `--ready-file` appears only once the listener is accepting, so waiting on it is a
# signal rather than a race.
while [ ! -e /tmp/fj-demo/ready ]; do sleep 0.1; done

dotnet run --project clients/dotnet/Boxops.Fjord.Demo -- --socket /tmp/fj-demo/fjord.sock
```

Or `./clients/dotnet/run-demo.sh`, which is the above.

## What the demo shows

**A producer that holds no fact ids.** Every reference it writes is the target fact
itself, nested inline, two levels deep — a declaration names its module, which names
its file. It keeps no map from entities to identities and emits in whatever order it
likes. The server interns each target and substitutes the id.

That is the whole reason a reference on the way in is a fact rather than an id. An
indexer walking a syntax tree knows the file when it reaches the declaration; every
id-based scheme would make it remember what the server called things.

The counts show it working:

```
writing 6 declarations, every reference nested
  created 12, deduped 6 (of 18 facts touched)
  6 declarations + 3 modules + 3 files = 12 distinct facts

a reference with a nested-record key: created 1, deduped 4
the same declarations again: created 0, deduped 18
```

Six declarations name three modules and three files, so eighteen facts are *touched*
and twelve are *written*. The reference that follows carries a nested record in its key
and points at a declaration and a file that already exist, so one fact is created and
four dedup. Sending the same declarations again writes nothing at all — interning is
idempotent, which is what makes retrying after a dropped connection safe.

## What a client has to know, and what it does not

**It has to have the schema.** The value codec sends no field names, no type markers
and no record arities: the server has them and so does the client, and sending what the
reader already has is what a transmission-shaped format declines to do. `Schema.cs` is
that, written down — mirroring `fjord::code_index` on the Rust side, deliberately,
because two independent statements of one schema is what the fingerprint is *for*.

It has caught that too. When the server moved from a cut-down three-predicate schema to
the real code index, the demo was refused at the handshake with both fingerprints named,
before a byte of data flowed — the whole mechanism working in the one situation it exists
for. When the schema later grew from six predicates to twenty-two it was the *golden*
that spoke first — `byte_identical_with_the_dotnet_client` compares fingerprints before
it compares a single byte, and said so in one line rather than as a hex diff. That is
also why this program states all twenty-two and writes facts for six: the fingerprint is
over the schema, not over the part of it a client happens to use.

Until schemas are parsed (PLAN Phase 8) a client writes the schema out by hand and
asserts it at the handshake with a fingerprint, which is what turns "we disagree about
the schema" from a corrupted database into a refused connection. Passing
`assertSchema: false` sends `0`, meaning *do not check* — right for a reader, wrong for
a producer.

**It does not have to know how facts are stored.** The storage codec is
order-preserving, self-delimiting and frozen on disk; none of that is on the wire, and
a client never sees it.

## The flag day: what to do when a shipped schema moves

A client sends **one** whole-schema fingerprint and the server checks it for equality, so
any edit to that file refuses every client until each one is rebuilt. The protocol carries
an alternative — per-predicate containment, which would make an *additive* change free —
and it is deliberately not used: the default schema is not expected to move often, and a
version bump is the accepted cost of the simpler client. That decision is the reason this
section exists.

**There was a script for this, and it is gone.** `scripts/flag-day.sh` walked nine steps
in order and stopped at the first stale one, because the expensive failure was discovering
step 3 after step 7. Every step that was a *correctness* claim is a test now, and a test is
a required check rather than a thing somebody remembers to run:

| what the script checked | what checks it now |
|---|---|
| the schema resolves, and its number | `every_shipped_schema_has_a_recorded_fingerprint` |
| each client's constant | `the_dotnet_clients_carry_the_fingerprint_the_schema_has`, per schema and by name |
| the Rust side agrees byte for byte | `byte_identical_with_the_dotnet_client` |
| the fixture reader's counts and key order | `sample_schema`'s four tests |
| the suite and the lint gate | the ordinary gate |

What went with it is one nudge rather than one guarantee: it noticed goldens that had
been regenerated and not committed, which is a `git status` away.

So the order below is for a person, and nothing enforces it:

1. **Edit the schema** — `schemas/demo.sigla` for the demo and the fixture,
   `schemas/dotnet.sigla` for what the indexer writes. **A shared layer moves more than
   itself**: `csharp.sigla` is imported by `dotnet.sigla` and by `index.sigla`, so editing
   it moves three numbers, and which three is a thing to read off the imports rather than
   remember.
2. **Read the new number** — `fjord --schema-path ./schemas schema check <file>`, over
   **every** shipped schema rather than the one you edited, so the set that moved is
   measured and not guessed.
3. **Paste it into the constant that states *that* schema.** The clients are written
   against different schemas: `Boxops.Fjord.Demo/Program.cs` states `demo.sigla`,
   `Boxops.Fjord.Indexer/DotnetIndex.cs` states `dotnet.sigla`, and
   `Boxops.Fjord.Scip/ScipFacts.cs` states `index.sigla` — three, each restated
   independently on purpose. The test above checks each against its own, so a missed one
   is a red suite rather than a refused handshake at somebody's site.
4. **Regenerate the goldens** — `./clients/dotnet/emit-golden.sh`. This needs a .NET SDK,
   and it is the step most often forgotten because the Rust test that depends on it
   *looks* like a Rust problem.
5. **Check the Rust side still agrees** — `cargo test -p fjord-client byte_identical`. Its
   corpus is stated independently, so a block added on one side and not the other fails
   here by count before it fails by bytes.
6. **Update `crates/fjord-cli/src/sample_schema.rs`** if the fixture moved: the predicate
   count, `KEY_ORDER`, `VALUE_ORDER` and the name lookups.
7. **Bump the .NET package version** in the same commit that re-pastes the constant. A
   moved fingerprint *is* a client release, and an un-upgraded client's refusal is the
   designed failure — `a_schema_mismatch_is_refused_at_the_handshake` asserts it names
   both numbers, so an operator can tell a stale client from one pointed at the wrong
   database.
8. **`cargo test` and the pinned lint gate.**

A schema *re-keying* — changing a predicate's key rather than adding one — is a bigger
job than this: every query and every producer that reads the predicate moves with it, and
no script can find them. Rehearse it end to end on a branch first.

## What is not implemented

The client mirrors the server, so it stops where the server does. Streams are issued
sequentially — the ids are real and the server tags every reply with one, but this
client sends a stream's frames and reads its replies before starting the next, so one
result is open at a time and `FjordConnection.Rows` refuses a second while one is. There
is no per-stream flow control, which is
[deferred](../../website/content/operations.md) on the server too.

**Paging, counting and cancellation are implemented, and they travel together.**
`FjordConnection.Rows` pulls a page at a time and yields rows, so `Take(n)` costs one
page rather than the whole result; `Page` is the single exchange under it, and
`CountRows` asks for the total without a row being encoded. Cancellation is what makes
the first of those safe rather than a hazard: a caller that stops mid-page leaves that
page's remaining rows on a socket every stream shares, so disposing the enumerator sends
`CANCEL` on the open stream and reads to its `Complete` before handing the connection
back. `Query` still collects the whole result and still cannot be stopped — it is the
right thing for a result whose size the caller already knows, and the wrong thing for
one it does not.

There is no test project: the console program *is* the test, and it is a better one
than a unit suite would be, because it runs against the real server over a real socket.
A unit test of this codec against constants copied from the Rust would prove the
constants were copied.

## The indexer

`Boxops.Fjord.Indexer` is the demo's argument made at scale: the same client library, the
same nested references and the same handshake, driven by **Buildalyzer** (a design-time
build per project, out of process) and **Roslyn** (what every name in the result means)
over a checkout of somebody's real .NET source.

```sh
./clients/dotnet/index-repo.sh ~/src/SomeSolution
```

The demo answers *is the protocol implementable from outside*. The indexer answers the
two questions after it: is it **usable** from outside by a producer with a real workload,
and does the database hold up when the facts were not chosen to be convenient. It has its
own [README](Boxops.Fjord.Indexer/README.md) — what it maps onto the twenty-two predicates,
what it resolves, and what the numbers it prints mean.

