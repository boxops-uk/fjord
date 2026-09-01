# Changelog

Fjord DB. Dates are the release date; `0.x` is a pre-release series and the on-disk format is
not promised to be stable across its minor versions — a database written by one is read by the
version that wrote it. What *is* promised inside a series is the append-only discipline the
format stamp and the marker table enforce: nothing already written is renumbered.

## Unreleased

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

`bench/FINDINGS.md` §1's `src.Line` row is marked: the seek figures are about the key, which is
unchanged, but the byte totals are not, and the re-run is scheduled.

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

`predicate_ids_are_assigned_by_sorted_qualified_name` is the test that rule never had, and
every schema file added from here on relies on it.

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

Nothing existing moved: `schemas/code.sigla` is still `0xb08eea634e866a75`, the format stamp
is still codec 1 / storage 1, and the corpus's positional plan-fingerprint list took pure
insertions.

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
settled-decisions record inside it), and the two Glean documents merged into `docs/glean.md`.
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
