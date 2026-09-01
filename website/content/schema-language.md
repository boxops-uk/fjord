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
  `src.Extends { base, type }`, and `type` is also how a named type is declared. That costs
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
`src.Ref {to = {decl = D}}` — and `X.to.decl?` selects one and binds its payload. Both are
seeks when the union leads the key. See [unions in the query
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
data is wanted in two orders, declare it twice — that is what `src.SearchByName`,
`src.FileXRef`, `src.DerivesFrom` and `src.AttributeOf` are, and each of them says so in a
comment.
:::

Two other placement rules worth internalising:

- **A field a query must match on belongs in the key**, not the value side: a value cannot
  be matched ([I6](invariants.html#i6)).
- **A trailing key field costs the seeks nothing.** `src.Ref` carries the reference's
  length in its key rather than its value, because a key field is already in the register
  the scan is holding while a value is a point read per row — and it trails, so every
  prefix above it still narrows exactly as it did.

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

`schemas/code.sigla` is a worked example rather than a default — `create` requires a schema and
there is nothing standing in for one. It is twenty-seven predicates in three layers, and the
joins between the layers are the point.

| Layer | Predicates | Answerable by |
|---|---|---|
| Source | `File`, `Module`, `Decl`, `DeclSpan`, `Ref`, `Import`, `Line`, plus `SearchByName`, `SearchByLowerName`, `FileXRef` | A syntax walk |
| Build | `Project`, `Assembly`, `Compilation`, `ProjectSource`, `ProjectRef`, `Package`, `PackageRef` | Something holding a build system |
| Declarations | `Member`, `Extends`, `Implements`, `Override`, `DerivesFrom`, `Param`, `TypeOf`, `Doc`, `Attribute`, `AttributeOf` | Something holding a compiler |

Read `schemas/code.sigla` itself if you are designing a schema: every predicate carries a
comment saying which question its key order answers, and four of them exist purely because
a derived predicate cannot yet be declared.

### `schemas/src.sigla` — the shared source layer

`code.sigla` imports it rather than declaring it, and that import is the point. Three
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

Three shapes in it are worth reading before designing anything similar:

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
`style-vocabulary`, `symbol-scheme`, `language`, `producer`, and the build axes.

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
