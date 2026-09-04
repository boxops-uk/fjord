---
title: Schema language
description: The .sigla schema DSL — blocks, predicates, types, imports, identity and compatibility. Field order is key order, so read that part twice.
---

A schema is a text file, conventionally `.sigla`. It declares namespaces and the predicates
in them. A database is created **against** a schema, embeds a canonical copy of it, and is
served from that copy for the rest of its life ([I13](invariants.html#i13)).

## A whole schema

```schema
# Comments start with `#`.
schema demo {

  # A scalar key: the whole key is one string.
  predicate Person : string

  # A record key. Field order is key order.
  predicate Knows : { from : Person, to : Person }

  # `-> T` is the value side: read on demand, never matched on.
  predicate Age : { person : Person } -> int

  # A nested record, and a reference into another predicate.
  predicate Sighting : { who : Person, at : { line : int, col : int } }
}
```

Here is one, live: the engine reads it, lowers it, and lists what it declares. Break it —
rename a type, drop a brace, give two alternatives the same tag — and the diagnostic is the
one the compiler emits, with the code a test asserts on.

:::demo schema
schema code {
  type What = { data : string = 5 | func : int = 2 }

  predicate File : string
  predicate Decl : { file : File, name : string, line : int } -> string
  predicate Kind : { decl : Decl, what : What }
}
:::

## Grammar

```text
file        ::= decl*
decl        ::= 'schema' ns '{' item* '}'
              | 'schema' ns 'evolves' ns            (parses, not available)

item        ::= 'import' ns
              | 'type' UpperName '=' type
              | 'predicate' UpperName ':' type [ '->' type ] [ 'stored' ]
              | 'derive' name [ 'stored' ]          (parses, not available)

type        ::= 'int' | 'string' | 'bytes'          builtin
              | UpperName | qualified.UpperName     a predicate or a named type
              | '{' fields '}'                      a record — or a sum, see below
              | '(' type ')'
              | '[' type ']'                        (parses, not available)
              | 'maybe' type                        (parses, not available)
              | 'set' type                          (parses, not available)
              | 'enum' '{' name ('|' name)* '}'     (parses, not available)

fields      ::= field (',' field)* [',']            a record
              | field ('|' field)+                  a sum: `a : t = 0 | b : t = 1`
field       ::= name [':' type] ['=' nat]           `= nat` is a discriminant
ns          ::= a dotted lowercase name — `src`, `lang.rust`
```

Three things about the shape:

- **`bytes` holds a run this language will not look inside.** Not validated as anything —
  that is what the type is — and ordered by `memcmp` over the payload, which the storage
  codec's escaping preserves ([storage](storage.html)). It is what a digest, a hash or a
  packed table belongs in, and it is written in a query as `0x…`. A `string` against a
  `bytes` field is a type error, so the two never meet in a comparison.
- **A record and a sum share their braces**, and are told apart by the separator after the
  first field: `,` continues a record, `|` starts a sum. That is one token of lookahead, and
  it is Angle's shape too.
- **A keyword may be a field name.** The sample schema has
  `code.Extends { type, base }`, and `type` is also how a named type is declared. That costs
  the grammar nothing, because a field name is never in a position where a keyword could
  start something.

:::note Permissive early, narrow later
Everything the grammar accepts and the type model cannot yet hold — arrays, `maybe`,
`set`, `enum`, `evolves`, a `stored` derivation — **parses**, and then draws one specific
`nyi/…` diagnostic naming it. A construct rejected by the *grammar* reports as a syntax
error pointing between two tokens, which tells a reader nothing about why the thing they
wrote is unavailable. See [what is not available yet](#what-is-not-available-yet).
:::

## Types

| Written | Means |
|---|---|
| `int` | A signed 64-bit integer. Negative values sort correctly — the codec gives them their own marker band |
| `string` | UTF-8. Order-preserving, so a prefix is a range |
| `Predicate` / `ns.Predicate` | A **reference** to a fact of that predicate. Stored as a `FactId`; type-checked against the predicate it names |
| `{ a : int, b : string }` | A record. Ordered fields, nesting allowed |
| `{ a : int = 0 \| b : string = 1 }` | A **union**. One of the alternatives, tagged by an explicit discriminant — see [unions](#unions) |
| `type Name = …` | A named type. Structural — it is inlined, and the name does not appear in the canonical form |

A **value side** is `-> T`, and `T` may be any of the above, including a record:

```schema
predicate Decl : { module : Module, name : string, line : int } -> string
predicate Boxed : { id : int } -> { lo : int, hi : int }
```

## Unions

A field may hold one of several **alternatives**, each carrying an explicit discriminant:

```schema
schema src {
  predicate File : string
  predicate Decl : { file : File, name : string }

  # One of three shapes a reference can resolve to. `missing` has no payload
  # type, which is the empty record.
  type Target = { decl : Decl = 0 | file : File = 1 | missing = 2 }

  predicate Ref : { at : int, to : Target }
}
```

- **The discriminant is written down, never inferred from position.**
  [I10](invariants.html#i10) requires tags to be stable and append-only, so the syntax has to
  give somewhere to write the number; positional numbering would silently re-tag every stored
  value the moment an alternative was inserted.
- **A record and a sum share their braces** — the separator after the first field decides,
  `,` for a record, `|` for a sum. A *single*-alternative union therefore needs a trailing
  `|` to be one at all: `{ only : string = 0 | }`.
- An alternative's payload may be any type — a scalar, a record, a reference. No payload
  written means the empty record.
- What lowering refuses, each by name: an alternative with no discriminant
  (`reject/missing-discriminant`), two alternatives sharing a tag
  (`reject/duplicate-discriminant`), and two sharing a name (`reject/duplicate-alternative`).

Once a union fact is written its discriminants are **frozen on disk**, and `schema diff`
reports **every** union edit as Breaking — appending an alternative included. A database is
served from its embedded schema and a client's per-predicate fingerprint has to match it, so
a union that grew is a different predicate to every client compiled against the old one.

On the query side, a one-field record against a union-typed field names an alternative —
`code.Resolves {to = {decl = D}}` — and `X.to.decl?` selects one and binds its payload. Both
are seeks when the union leads the key. See [unions in the query
language](query-language.html#unions).

## Field order is the index design

This is the single most consequential thing about writing a schema here.

A predicate's key is encoded field by field, in **declaration order**, and the encoding is
order-preserving. A query can therefore narrow the scan on a **leading run** of key fields
and can only *filter* on the rest.

```schema
# Fast at: "the declarations in this module", "…narrowed by name".
# Slow at:  "every declaration called X, anywhere".
predicate Decl : { module : Module, name : string, line : int } -> string

# Fast at: "everything called X" — the same data, keyed for the other question.
predicate SearchByName : { name : string, to : Decl }
```

Read each record as *what is this predicate fast at*, not as a list of attributes. Two
predicates in the sample schema were once declared alphabetically out of habit; it cost
**56,274 rows examined per row produced** on an ordinary join, and made find-references
unanswerable. The fix was to move a field.

:::warn Declaring a key alphabetically is a choice
It makes the index shape a consequence of what the fields happen to be called. If the same
data is wanted in two orders, declare it twice — that is what `codemarkup.SymbolByName`,
`codemarkup.FileXRef`, `codemarkup.RelationOf` and `csharp.DefinitionBySymbol` are, and each
of them says so in a comment.
:::

Two other placement rules worth internalising:

- **A field a query must match on belongs in the key**, not the value side: a value cannot
  be matched ([I6](invariants.html#i6)).
- **A trailing key field costs the seeks nothing.** `codemarkup.SearchEntry` carries the
  file and line a hit renders in its key rather than its value, because a key field is
  already in the register the scan is holding while a value is a point read per row — and
  they trail, so every prefix above them still narrows exactly as it did.

## Namespaces and imports

A namespace is a dotted lowercase name. Namespaces are **open across files** and a file may
hold several blocks, so nothing ties a namespace to a file.

An **import names a namespace, never a path**:

```schema
schema app {
  import base

  predicate Marker : { file : base.File, at : base.Span }
}
```

`base` resolves to `base.sigla`, and `lang.rust` to `lang/rust.sigla`, under a **root**. Roots
are the entry file's own directory first, then `--schema-path` (also
`FJORD_SCHEMA_PATH`, separated the way `PATH` is), first match wins.

```bash
fjord --schema-path ./schemas schema check ./app.sigla
```

```text
2 predicate(s) in 2 file(s)
  ./app.sigla
  ./base.sigla
fingerprint 0x72e0ddfeda09028f
```

:::note Write `./file.sigla`, not `file.sigla`
The entry file's *own directory* is the first root — and a bare relative filename has no
directory component, so a schema that imports a sibling will not resolve when named as
`app.sigla`. Use `./app.sigla` or an absolute path (or pass the directory as `--schema-path`).
:::

Resolution semantics, in four lines:

- **Edges with concatenation semantics.** Transitive closure, dedup by file identity, union
  the blocks. A namespace is open, so the union is the text put end to end.
- **Cycles are harmless by construction.** A file already read is not read again, so `a`
  importing `b` importing `a` terminates. Diamonds dedup for free.
- **One name declared twice is a rejection, whether or not the two agree.** The same
  *file* reached twice is deduped by file identity and costs nothing; two declarations of
  one fully-qualified name are `reject/redeclaration` even when they are byte-identical.
  That is the right behaviour rather than a wart: a namespace split across files has
  exactly one declaration site per predicate, which is what makes an import worth having.
- **The file found has to be the file meant.** A file is located from the import name
  alone — resolution never inspects the `schema <name>` head it finds there — so
  `import ob` answered by a file declaring `schema base` is `reject/namespace-mismatch`,
  reported *at the import*. Without it the two files resolve and then every reference into
  the namespace fails `reject/unknown-name`, which reports the symptom everywhere and the
  cause nowhere.
- **Transitive visibility is accepted, not fought.** An import is not an encapsulation
  boundary; what `a` imports, anything importing `a` can see.

### Resolving without a filesystem

Resolution is one algorithm over a **source provider**, and the filesystem is one
implementation of it. An embedder — a producer with its schema in `include_str!`, or a
browser, which has no filesystem at all — hands
`fjord_schema::syntax::resolve::resolve_from` an ordered list of `(name, text)`, the entry
first, and gets the same union back:

```rust
let schema = fjord_db::read_schema([
    ("app.sigla", include_str!("../schemas/app.sigla")),
    ("base.sigla", include_str!("../schemas/base.sigla")),
])?;
```

The list's **order is the search order**, as the roots are for a file: where two sources
claim one import name, the first wins. The two paths are held together by a differential —
every multi-file case in the corpus is resolved both ways and must agree on every
predicate, every fingerprint and every diagnostic — because two algorithms would drift.

## Identity: canonical form and fingerprints

A schema's identity is independent of file layout and declaration order **by construction**.

1. **Canonical form** — resolve every name to its fully-qualified form; strip comments,
   whitespace, file provenance and the order predicates happened to be declared in.
2. **Fingerprint** — a hash over that form. One per predicate, plus one for the whole
   schema.

```bash
fjord schema fingerprint ./app.sigla --canonical
```

```text
fjord-schema-v1
app.Marker:{file:@base.File#0beb86474c616b93,at:{line:int,col:int}}
base.File:string
```

Two details are load-bearing. A named type has been **inlined** — `base.Span` is gone and
its structure is there instead — so a type alias is not identity-bearing. And a reference is
spelled as the referent's fully-qualified name **plus the referent's own fingerprint**
(`@base.File#0beb…`), so changing a predicate changes the fingerprint of everything that
transitively references it. A position would have made identity depend on declaration order;
a bare name would not have propagated the change.

**A record's field order *is* identity-bearing**, because it is encoding order and it
decides the seek prefix. Permuting fields is a semantic change and must move the
fingerprint.

## Compatibility

Schema identity is the map `qualified_name → predicate_fingerprint`, and compatibility is
deliberately collapsed to subset containment:

```text
compatible(old → new)  ⇔  old_map ⊆ new_map
```

**The only compatible change is adding a predicate.** Any in-place modification of a key or
value — including reordering fields — is `Breaking`, because values are queryable and
positionally encoded, so a field change shifts stored bytes.

```bash
fjord schema diff people.sigla people2.sigla
fjord schema diff people.sigla people3.sigla
```

```text
Compatible (1 added)
  + demo.Employer

Breaking (1 predicate(s))
  ~ demo.Knows  (modified: 080f8e02ff957601 → c1779584fe40b587)
```

Either side of a `diff` may be a schema file **or the name of a database** — comparing what
a build would produce against what an artifact already holds is the question it exists for.

The migration path for a breaking change is the one the workflow already implies: build a
new artifact. There is no `evolves` and no query-time projection layer, which is why
subset containment is enough and cannot fail the way a richer compatibility check can.

:::note What subset containment costs
Adding an *optional* field is Breaking here. Under this rule the migration is a new
predicate name and a rewrite of every query that used the old one. That is a real cost, and
it is accepted deliberately: the alternative is a per-type default table and a projection
layer, and [I13](invariants.html#i13) freezes the schema at create so there is no second
schema for a projection to live between.
:::

## Where a schema is used

| Moment | What happens |
|---|---|
| `fjord schema check` | Resolve imports, union the blocks, lower — reports syntax errors, unresolved imports and redeclarations |
| `fjord create --schema F` | Resolve, canonicalise, fingerprint, and **embed** the result in the new database |
| A client connecting | The startup frame carries the predicates the client claims, each with its fingerprint; a claim that is not an exact match is checked by subset containment |
| A client asking `H` | The server answers with the schema **that database** is served with, as source — which is what lets a client compile locally |
| Every write | Validated against the embedded schema |

## What is not available yet

Each of these parses and then names itself in a diagnostic:

```schema
schema t {
  predicate A : [ int ]                          # nyi/array
  predicate B : maybe string                     # nyi/maybe
  predicate D : enum { x | y }                    # nyi/enum
  predicate E : set int                           # nyi/set
  derive t.A                                      # nyi/derivation
}
schema t evolves u                                # nyi/evolves
```

```text
error[nyi/maybe]: `maybe` is sugar over a union, and waits on a naming decision: the
                  alternative names it desugars to enter the fingerprint
  ┌─ /tmp/nyi.sigla:3:17
  │
3 │   predicate B : maybe string
  │                 ^^^^^^^^^^^^
```

Two of them are worth understanding rather than just noting:

- **A one-to-many is written as one fact per element.** That is the settled answer, not a
  workaround for missing arrays: an array cannot be prefix-matched (its length is at the
  front), so an array anywhere but the last key field permanently closes the seek prefix for
  every field after it. Edges — `predicate ProjectRef : { from : Project, to : Project }` —
  are how a many-to-many is said here.
- **`maybe` and `enum` are sugar over a union**, which exists — what neither has is its
  *naming* decision. Each desugars to alternative names and payload types that enter the
  canonical form, and the fingerprint freezes whatever is chosen, so the spelling has to be
  right the first time.

## The sample schema

`schemas/demo.sigla` is a worked example rather than a default — `create` requires a schema
and there is nothing standing in for one. It is a small code index, and it is **complete on
purpose**: eleven predicates, at least one for every construct the type model can hold.

| Construct | Where to see it |
|---|---|
| a scalar key, and a reference to one | `code.File`, and every predicate that names it |
| a record key with a value side | `code.Decl` — a reference leading, `line` trailing |
| a nested record in a key, and in a value | `code.Span`, `code.Extent` |
| a union in a key, both ways round | `code.Kind` and `code.KindOf` |
| a union payload of every kind — none, a reference, a record | `code.Resolves` |
| a single-alternative union | `code.Note` |
| `bytes` | `code.Digest` |
| a keyword as a field name | `code.Extends`, whose first field is `type` |

Read it if you are designing a schema: every predicate carries a comment saying which
question its key order answers, and the file states which construct it is there to show.
The set a real producer writes is `schemas/dotnet.sigla` — sixty-five predicates across
five layers — and it is the wrong thing to read first.

### `schemas/src.sigla` — the shared source layer

Every layer imports it rather than declaring its own, and that import is the point. Three
schemas used to declare **their own `File`**, each with a comment saying "owned here so the
schema resolves standalone" — and the cost of that thrift is that `content.File "x"` and
`csharp.File "x"` are *different types naming the same file*, so the join that renders a
search hit (a definition in one index, the bytes in another) cannot be written in sigla at
all. It gets written in the bridge, in JavaScript, and holds only because two producers
agree on a string by convention.

Nine predicates — `File`, `Symbol`, `FileLanguage`, `FileDigest`, `FileOrigin`, `FileInfo`,
`FileLine`, `FileLineAt`, `FileLineStyles` — plus the scalars every schema was copying.
Every one leads with `file`, so a file's line table, its styles and its digest are each one
prefix seek, and `line` trails so a **window is a range on the last key field**.

Four shapes in it are worth reading before designing anything similar:

- **`FileLine` carries both offsets.** `start` is a UTF-8 byte offset and `cstart` a UTF-16
  code-unit offset, and they are not the same number — a codepoint above the BMP is four
  bytes and two code units. Which one a span means is declared once per database by
  `config.Setting {dimension = "position-encoding"}`, and the line table is therefore the
  conversion table: a consumer converts against a row it has already fetched to render.
- **`FileLineAt` is the same table keyed by offset**, and it exists because a value can
  neither be matched ([I6](invariants.html#i6)) nor read field-wise — so "which line is byte
  12345 in" against `FileLine` alone is a scan. Keyed `{file, start}` it is one seek, and
  because sigla has no descending seek and no `LIMIT` the shape is a range upward with a
  client-side limit of 1. The third of its three cases is the one that bites: an offset
  **past the last line's start** returns nothing, and the answer is then `FileInfo.lines`.
  That is the common case for a reference in the last line of a file, not an edge.
- **Nothing presentational is on `FileLine`'s value.** Not taste — `nyi/value-field` means a
  query cannot project one field of a value, so *every consumer of any field pays for all of
  them*. A baked-HTML field measured 21.6 MB against 12.6 MB of text on a real corpus, so a
  viewer's window paid ~2.4× the bytes it needed. `FileLineStyles` is a separate predicate
  for that reason, keyed identically so a window is the same seek.
- **`FileLineStyles` holds bytes the schema refuses to describe.** A highlighter's
  vocabulary is a presentation concern with a lifecycle of its own, so freezing one into a
  predicate every published index carries would make each new token kind a Breaking edit
  ([I10](invariants.html#i10)) — and styles are the one thing here that is *regenerable*,
  where the facts beside them are not. So the type is `bytes` and the declaration says
  nothing about the contents: `config.Setting {dimension = "style-encoding"}` names the byte
  format and its token legend together, and a consumer that does not recognise the value
  renders those lines plain. Absent means "not tokenised", so an index with no highlighter
  is a complete index and needs no sentinel. **fjord ships no codec and defines no
  vocabulary** — a producer writes what its tokeniser already emits, and names it.

:::note The worked example is LSP, and none of it is fjord's
`Boxops.Fjord.Indexer --styles` runs Roslyn's `Classifier` — what Visual Studio colours
with — and writes LSP `SemanticTokens.data` under the `style-encoding` value `roslyn-lsp-1`:
five integers per token, `[deltaLine, deltaStart, length, tokenType, tokenModifiers]`, over a
legend of Roslyn's own `ClassificationTypeNames`. A browser decodes it to a `Uint32Array` and
hands it to Monaco unchanged.

Two details of that encoding matter. The integers are LEB128 varints rather than fixed 32-bit
words, because a delta, a length and a legend index are all small — about five bytes a token
instead of twenty. And the facts are **per line**, so `deltaLine` is always 0 and
`deltaStart` is relative to the previous token on that line: a deliberate divergence from
LSP, which encodes a whole file and would otherwise have to be fetched and decoded whole to
draw forty lines.

Three properties of a real tokeniser rule out a flat run list, each verified against Roslyn's
output rather than assumed: spans **overlap** (a static method's name comes back as both a
method name and a static symbol, which is LSP's `tokenModifiers` bitfield), spans **cross
lines** (an LSP token may not, so a block comment is split at each line boundary), and
**nothing is emitted for whitespace**, so gaps are absent and uncovered text renders plain.

The legend is fixed rather than discovered: one built from the names a run happened to meet
would differ between two indexes of the same repository, and a sealed identity hashes the
facts. Appending to it is safe and renumbering is not, exactly as for a schema union. A
producer for another language names its own encoding and its own legend, and this predicate
does not change.
:::

### `schemas/codemarkup.sigla` — one surface a UI can read

Ten predicates over `src`, and the whole layer rests on one decision: **the join key is a
string** — `src.Symbol` — and not a union over languages.

The alternative is what makes the case. Key a definition on `{ csharp : … | typescript : … }`
and a C#-only index and a C#-plus-TypeScript index carry *different* `Definition` predicates,
because appending a union alternative is Breaking ([I10](invariants.html#i10) freezes
discriminants, and `schema diff` reports even an append that way). One UI could then not read
both, which is the entire purpose of a language-independent layer. That is a live test rather
than a paragraph — appending one alternative to a union used in two keys breaks exactly those
two — and so is the property it buys: every `codemarkup` predicate has the same fingerprint
resolved alone or inside a composite holding two language layers.

The second reason is cross-database. A `FactId` is a predicate tag plus a per-predicate
sequence ([I11](invariants.html#i11)), so it means nothing in another database — and "who
references this, anywhere" is a fan-out. A string survives the trip; an id does not.

**Anything a consumer joins *through* is in the key**, and that is stricter than it looks: a
value can neither be matched ([I6](invariants.html#i6)) nor read field-wise
(`nyi/value-field`), so a reference behind the `->` is reachable only by projecting the whole
value and issuing a second query. So `Definition` keys on the file as well as the symbol, and
`FileXRef` is **all key** — a renderer wants every field of every row, and a value would be a
point read per reference. A trailing key field costs the seeks nothing, which is what makes
that free.

| Vocabulary | Transcribed from | The valve |
|---|---|---|
| `Kind` | LSP `SymbolKind`, 1–26 verbatim | `other : string = 0`, the slot LSP does not use |
| `Role` | SCIP `SymbolRole`, projected | `other : string = 0` |
| `RelationKind` | Glean `codemarkup` relations | `other : string = 0` |

All three are **citations rather than inventions**, and that is deliberate: they sit in keys,
so their discriminants froze the day the layer shipped and every future value has to arrive
through `other`. LSP's `SymbolKind` has not moved since 3.x.

:::warn A bitmask is lost, and it is stated rather than hidden
SCIP's `SymbolRole` is a bitmask — a reference can be a definition *and* an import at once. A
sigla union cannot express that, and an `int` of flags would be a field no query could usefully
match on. `Role` is therefore the mutually-exclusive projection a UI filters by, and a producer
needing the full mask loses information here.
:::

Every predicate is redundant with a language layer by construction — the same facts keyed for
the question a UI asks. In Glean these are `stored` derived predicates; here `nyi/derivation`
means a producer writes them, so each carries the query that *would* derive it as a comment.
While the population is by hand that comment is the specification the producer is checked
against.

### The five reference schemas, and `index.sigla`

`csharp` (31), `msbuild` (16), `typescript` (35), `npm` (14) and `bundle` (22) — 118
predicates that **nothing in this repository populates**. They ship as declared, checked,
fingerprint-recorded schemas because a schema file costs a binary nothing: nothing in
`schemas/` is embedded except by an explicit `include_str!`, and the release artifact
carries no schema files at all.

**What fjord promises about them, and what it does not.** fjord defines the shapes and the
vocabularies' numbering. It does not promise to track LSP, SCIP, Yarn or webpack releases on
any schedule; a vocabulary that has to grow does so through its `other : string = 0` valve
rather than a Breaking edit. `csharp` and `msbuild` are the supported pair — the ones a
first-party producer writes and the ones exercised end to end.

Three structural moves come with them, and each is the same seam drawn twice:

- **The MSBuild project graph left `csharp`.** A different producer fills it, an MSBuild
  solution compiles F# and VB, and it was missing every edge *between two projects*. A
  project is now identified by its `.csproj` with the evaluated attributes as values —
  because an identity must not carry an evaluation detail, or re-evaluating under a
  different SDK mints a second project and every reference edge points at whichever the
  walk reached first.
- **The package layer left `typescript`** into `npm`, modelled on Yarn's own vocabulary
  (locator / descriptor / resolution / workspace / project) so the two agree by construction.
  A repository with a lockfile and no TypeScript is then an `npm` index and nothing else,
  which is a test rather than a claim.
- **The bundler layer left `typescript`** into `bundle`, whose shape is
  `getStats().toJson()` — webpack's as much as Rspack's, hence the namespace.

`index.sigla` declares **no predicates of its own**: it imports the other eight and exists to
prove the set composes. It resolves to **136 predicates in 9 files**, and the two queries in
its header are integration tests rather than illustrations — every reference in one file
resolved to where its target is defined *for any language*, and a symbol joined to the
project that compiled the file it sits in, across three namespaces filled by three different
producers. Neither is expressible across three separate databases, because a `FactId` does
not leave the database that issued it ([I11](invariants.html#i11)).

:::note Two module graphs disagree on purpose
`bundle.ModuleImport` is the bundler's graph, read from stats after resolution and
tree-shaking; `typescript.FileImport` is the source graph, read from the import statements as
written. A module in one and not the other is a module that was shaken out — the interesting
answer, not a bug.
:::

### `schemas/config.sigla` — what a database was built for

One predicate, `config.Setting {dimension, value}`, and it answers the question a tool
holding forty handles has to ask: *which one is this*. An instance name it can only
string-match on is not an answer, and the sharp case is a build axis that appears nowhere
else in the index — under nearest-compatible target resolution a `netstandard2.0` project
inside a `net9.0` index records `netstandard2.0` in its own compilation facts, correctly,
so the resolution root is unrecoverable from the facts.

**Dimension-leading, key-only and multi-valued.** Dimension first so a lookup is a seek;
key-only so a dimension may hold several values, which `dimension -> value` could not say
because the second write would be a conflict rather than a second fact. Both fields are
strings and neither is a union, deliberately: a union would freeze the vocabulary's
discriminants on the day it shipped ([I10](invariants.html#i10)) and make every new axis a
Breaking edit to a predicate every published index carries. The schema comment carries the
reserved list instead — `repo`, `revision`, `index-root`, `position-encoding`,
`style-encoding`, `symbol-scheme`, `language`, `producer`, and the build axes.

It is **per-database**, so it cannot record a per-project or per-file axis:
`{dimension = "define", value = "DEBUG"}` says the run defined `DEBUG`, not which projects
did. Where an axis varies inside one index, the fact that varies carries it.

:::note A database that does not state its position encoding is read as `utf16`
`position-encoding` is `utf8` or `utf16`, and it is the unit **every** offset and column in
the database counts in — one declaration per database, as SCIP settled it, rather than a
unit per span. A mixed-encoding database is deliberately not expressible.

The default is `utf16` because that is what the compilers this set indexes actually count:
Roslyn and the TypeScript compiler both count UTF-16 code units. Stating it is still the
point. The unit that agrees with neither is a renderer walking `chars()`, which counts
Unicode scalar values — one per codepoint where the producer counted two for anything above
the BMP, and the symptom is a link drawn over the wrong text, which reads as a styling bug.
:::

There are also **virtual** predicates — `fjord.db.List` (the store root as rows) and
`fjord.db.Interning` (the write path's own counters, per database) — declared in
`crates/fjord-server/schemas/catalogue.sigla`, the crate that answers them. A virtual
predicate is answered by the server out of what it knows rather than read from a keyspace,
which is why it is a file of its own: it is deliberately absent from the handshake
fingerprint, from the copy embedded at create, and from every artifact's keyspaces, and the
whole reserved `fjord.` namespace is marked virtual so a stored predicate can never collide
with it. A client that has never heard of them connects exactly as before.
