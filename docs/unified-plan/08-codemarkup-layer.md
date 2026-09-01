# W8 · `codemarkup.sigla` — one language-independent surface for a code-search UI

| | |
|---|---|
| **Issue** | [#40](https://github.com/boxops-uk/fjord/issues/40) — `[04]` |
| **Area** | `schemas/`, and R4b's `src.ExternalRef` |
| **Depends on** | **W6** (imports `src`), **W7** (`position-encoding`). **W1** for the language-side bridges in W9, not for this file |
| **Blocks** | **W9**, **W11**, and it changes **R4b** and **R9** |
| **Invariants** | I10 (the two vocabularies' discriminants are frozen the day this lands), I11 (why the join key is a string) |
| **Fingerprint** | a new namespace; nothing existing moves |

## Claim

A C# index, a TypeScript index, a Rust index converted from SCIP, and a composite of all three serve
the **same** `codemarkup` predicates, and one client reads every one of them — because the join key
is a string, so the layer's fingerprint does not depend on which languages the database happens to
hold.

## Ten predicates

```
Definition  SymbolInfo  FileDefinition  FileXRef  SymbolXRef  FileLocalXRef
SearchEntry  SymbolByName  Relation  RelationOf
```

plus two frozen vocabularies: `Kind` (LSP's `SymbolKind`, 1–26 verbatim, with `other : string = 0`
in the slot LSP does not use) and `Role` (the mutually-exclusive projection a UI filters on, with the
mapping from SCIP's `SymbolRole` **bitmask** stated in the comment — a bitmask is not expressible as
a sigla union, and that loss is stated rather than hidden), and `RelationKind`. Taken as written from
the issue's appendix A.

## The two arguments, and both survive W1

**1 · A union over languages makes this layer's fingerprint depend on which languages the database
holds.** Schema identity is per-predicate and a union edit is Breaking — I10 freezes discriminants
and `schema diff` reports even an append that way. So a C#-only index and a C#-plus-TypeScript index
would carry *different* `codemarkup.Definition` predicates, and one UI could not read both, which is
the entire purpose of a language-independent layer. Measured by the issue on exactly that pair:
`Breaking (2 predicate(s))` for one appended alternative.

**2 · A `FactId` is per-database by construction** (I11 — the id is a predicate tag plus a
per-predicate sequence, `fjord-schema/src/id.rs:34-57`). "Who references this, anywhere" is a fan-out
across databases, and a fan-out needs a key that survives the trip. R3.5's fan-out makes that
load-bearing rather than aspirational.

W1 makes Glean's union shape *available* here; it stays the wrong one, for reasons that have nothing
to do with what the engine can express. **Where the union shape is right is the in-database path**,
and that is where W9 puts it: `csharp.EntityXRef`/`EntityRef` target a language entity, so following
one is a `FactId` hop and costs no interned string. Both exist deliberately; the duplication is what
portability costs, and Glean pays it too.

## The placement rule, and it is stricter than it looks

**Anything a consumer joins *through* is in the key.** A value cannot be matched (I6) *and* a field
of a value cannot be projected — `X.value.file` is `error[nyi/value-field]`
(`flatten.rs:4818-4826`), with a corpus entry at `corpus.rs:142-150`. So a reference behind the `->`
is reachable only by projecting the whole value and issuing a second query.

This layer exists to be joined through, so its keys are wide and its values narrow: a definition's
`file` is in the key because that is how the bytes (`src.FileLine`) and the project
(`msbuild.SourceFileToProject`) are reached, and `FileXRef` is **all key** because a renderer wants
every field of every row and a value would be a point read per reference (I6). Both are free — a
*trailing* key field costs the seeks nothing, which is the argument `src.Ref` already makes for
carrying a reference's length in its key.

## What this changes about revision 2 — `src.ExternalRef`

R4b adds `ExternalRef : { symbol, file, at : {line, col, length} }` because with one compiled target
per database (Decision 5) a reference from `App` (net9.0) into `Lib` (netstandard2.0) cannot nest a
local `Decl`. **That is `codemarkup.SymbolXRef`, arrived at from the other direction** — and it
leaves three things to settle. The plan settles them:

1. **`ExternalRef` becomes `codemarkup.SymbolXRef` + `codemarkup.FileXRef`.** R4b declares neither.
   The file-keyed twin is not optional and `code.sigla` already knows why: `src.FileXRef`'s own
   comment records that against `src.Ref`, "which references are in this file" was *"a scan of the
   largest relational predicate in the index: 4.9M rows to find the few hundred in one file"*. A
   renderer asking that of `ExternalRef` is that scan again.
2. **Units.** `ExternalRef.at` was `{line, col, length}`; this set standardises `src.ByteSpan
   {start, length}` with the encoding declared once per database (W7). One namespace with two
   conventions makes a consumer ask which predicate uses which, so: **offsets, in the declared
   encoding**, everywhere in the new set. `src.Ref`/`src.FileXRef` keep `{line, col, length}`
   because they are shipped and moving them is Breaking; the difference is documented, not smoothed
   over.
3. **If W8 does not land, R4b still owes the file-keyed twin.** Recorded so the dependency is not
   silently one-way.

## Hand-populated now, derived later — and the derivation is the specification

Every predicate here is redundant with the language layers by construction: the same facts keyed for
the question a UI asks. In Glean these are `stored` derived predicates; fjord has `nyi/derivation`,
so a producer writes them — the pattern `code.sigla` already uses for `src.SearchByName`.

**So every predicate carries the sigla query that would derive it, as a comment.** While the
population is by hand that comment is the specification the producer is checked against, and a
discrepancy is a diff rather than an argument. When `derive` lands, the comment becomes the body.

## Acceptance criteria

1. **The file checks against `src`, and its fingerprint is recorded** in W6's golden table.
   `fjord --schema-path schemas schema check schemas/codemarkup.sigla`.
2. **The fingerprint-invariance claim is asserted mechanically.** A test resolves
   `codemarkup.sigla` alone and then inside a composite that also holds two language layers, and
   asserts **every `codemarkup.*` per-predicate fingerprint is equal in both** — the property that a
   union-keyed design would not have. This is the single most important criterion in this item,
   because it is the whole argument.
3. **The union counter-example is pinned.** The issue's `cm_union1`/`cm_union2` pair is added as a
   test fixture asserting `Breaking (2)` for one appended alternative, so the reason for the string
   key is a live test rather than a paragraph.
4. **Every predicate is exercised.** An integration test writes a small hand-built index — two
   files, two languages, a definition, a local, three references, one relation — and runs **one
   query per predicate**, asserting rows: go-to-definition, the file outline in position order,
   every reference in a file in position order, find-references across files, a file-local jump, a
   case-insensitive prefix search, an exact-name search, and both relation directions.
5. **Each "would derive as" comment is a query that compiles.** A test collects them and asserts
   each parses and typechecks against the composite schema (execution not required — the language
   layers are not populated by this item). A specification nothing checks is a comment.
6. **The `FileLocalXRef` claim is exercised**: a local variable's jump-to-declaration resolves
   span-to-span inside one file, costing no `src.Symbol` — proving the argument that file-local
   references need no global name, which is what makes SCIP's occurrence-ordered `local0` unnecessary
   (and settles half of #42 item 1; see W13).
7. **The book carries the layer** and the `Kind`/`Role` citation table (W10);
   `website/build.py --strict` clean.
8. **The full gate**: `cargo test`, clippy/fmt, `check-guards.py`.

## Traps

- **`Kind` and `Role` are in keys**, so their discriminants are frozen on the day this lands (I10)
  and every future value has to arrive through `other : string = 0`. That is why both are
  *citations* of a published enum rather than inventions — LSP's `SymbolKind` has not moved since
  3.x.
- **A bitmask is lost.** SCIP's `SymbolRole` is a bitmask; a reference can be a definition *and* an
  import at once. This layer stores the mutually-exclusive projection a UI filters by, and a
  producer needing the full mask loses information. Stated in the comment.
- **Do not add a `container` field.** Containment is
  `Relation {from = C, kind = {contains}, to = S}`, joinable in both directions and not stored twice.
