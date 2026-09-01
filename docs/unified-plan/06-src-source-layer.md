# W6 · `src.sigla` — one shared source layer

| | |
|---|---|
| **Issue** | [#39](https://github.com/boxops-uk/fjord/issues/39) — `[03]`, superseding [#36](https://github.com/boxops-uk/fjord/issues/36) |
| **Area** | `schemas/`, `crates/fjord-cli/src/sample_schema.rs`, `clients/dotnet`, `website/content/` |
| **Depends on** | **W4** for the file split (fallback: declare inside `code.sigla` at identical fingerprints). **W5** is desirable first, not required |
| **Blocks** | **W8, W9, W11**; coordinates with **R4b**. Not W7 — `config` imports nothing |
| **Invariants** | I13 (a database's schema is frozen at create — existing databases are untouched), I10 (the `Language` vocabulary's discriminants are frozen the day this lands) |
| **Fingerprint** | **Breaking**, and deliberately: `src.Line` is deleted in favour of `src.FileLine`. Eight predicates added, one removed, **every surviving predicate byte-identical**. `code.sigla`'s schema-level fingerprint moves from `0xb08eea634e866a75` |
| **Format** | untouched |

## Claim

One `src.File` means a C# definition, a TypeScript module, an MSBuild source item and a line of
text can be said to be *the same file* **in a query** rather than in a convention — and the whole
layer lands additively, with every predicate that exists today keeping its fingerprint byte for byte.

## What lands

`schemas/src.sigla` — nine predicates and the scalars four schemas were each copying. The file is
reproduced in full in issue #39's appendix A and is taken as written, with the three decisions below
applied:

```
File  Symbol  FileLanguage  FileDigest  FileOrigin  FileInfo  FileLine  FileLineAt  FileLineStyles
Bool  MaybeString  MaybeInt  MaybeFile  MaybeSymbol  ByteSpan  Location  Loc  Range  Language
```

and one edit to `schemas/code.sigla`: delete `predicate File : string`, add `import src`.

## The cost, exactly

Measured in-tree, with `target/debug/fjord`:

- **A named `type` moved into an imported file is fingerprint-identical.** `type Bool` local vs
  `base.Bool` imported: both `0x53a9857a412b2e7b`, `schema diff` says `Identical`. The mechanism is
  in the tree: an alias is *expanded* at lowering (`lower.rs:36`), so its name never reaches
  `PredicateTy`.
- **A predicate moved to another file is fingerprint-identical.** Both `0xb5761dc1251dc713`.
  `Predicate` (`schema.rs:94-99`) carries no file, so the canonical form cannot read one.
- Therefore every surviving `src.*` predicate — **`src.File` included, which physically moves into
  another file** — keeps its per-predicate fingerprint. Without the deletion below, `schema diff`
  reports `Compatible (8 added)` with **no `~` lines**; that was measured by the issue and is what
  makes the eight arrivals free.

So the **additive half is free at the predicate level**. What is not free is the deletion and the
schema-level number:

- **`src.Line` is deleted** (D2 below), which makes the change `Breaking` and names exactly one
  predicate.
- `fjord schema fingerprint schemas/code.sigla` is `0xb08eea634e866a75` today, byte-for-byte the
  constant both .NET clients carry (`CodeIndex.cs`'s `SchemaFingerprint`, restated independently in
  `Boxops.Fjord.Demo/Program.cs:71`). It moves.

**This is a flag day, and that is the accepted cost.** The protocol has an alternative — the
handshake falls back to per-predicate containment when a client sends its predicates instead of a
whole-schema number (`session.rs:350-380`) — and it is **declined** (W14): the client keeps its one
constant, and a schema change is a client version bump.

Two further consequences of adding predicates, both stated so nobody discovers them:

- **Predicate ids renumber for databases created after the change.** Ids are assigned by sorted
  qualified name (W5), so `src.FileDigest` inserts between `src.File` and `src.FileXRef` rather than
  appending. Existing databases keep their embedded map (I13). The wire carries names, so this
  never leaves the database — except in a `FactId`'s tag, which is a *consumer* hazard W5 documents.
- **`sample_schema.rs`'s `assert_eq!(schema.len(), 27)` (`:167-171`) becomes 35**, and `KEY_ORDER`
  (`:245`) gains the new predicates' key orders. Both are part of this work item, not surprises.

## Three decisions, one of which reverses the issue's recommendation

**D1 · `src.sigla` owns `predicate Symbol : string`, not `code.sigla`.** Issue #39 recommends R4b
own it, *"since that is the run that first populates it"*. Reversed here, for a structural reason
the issue could not see from outside: **`codemarkup.sigla` (W8) imports `src` and must not import
`code.sigla`** — `index.sigla`'s own header says `code.sigla` is deliberately not imported, because
its `src` namespace holds a second, older declaration layer the new set does not build on. If R4b
owned `Symbol`, every consumer of the language-independent layer would have to drag the reference
index in with it. R4b therefore declares only `DeclSymbol`, `SymbolOf` and (see W8) whatever
survives of `ExternalRef`, and adds `import src`. Identical redeclaration rejects, so exactly one
file may declare it and this is the one.

**D2 · `src.Line` is deleted; `src.FileLine` is the line table.** `code.sigla` has
`Line : { file, line } -> string`; this replaces it with
`FileLine : { file, line } -> { text, start, bytes, cstart }`.

Carrying both was the earlier recommendation, on the grounds that removing a predicate is Breaking
under subset containment. **That is no longer the trade being made**: there are no production
consumers to protect, so the schema gets the shape it should have rather than a permanent wart plus
a deprecation note nobody would act on. Two line tables in one namespace would mean a producer must
choose and a reader must ask which one it got, forever.

The migration is bounded and every site is known:

| Site | What changes |
|---|---|
| `clients/dotnet/.../Indexer.cs:324,331`, `CodeIndex.cs:378` | emits `FileLine`, and must now compute `start` (UTF-8 bytes), `bytes` and `cstart` (UTF-16 code units) per line — it already counts UTF-16 for columns (`GleanFacts.cs:297`) |
| `crates/fjord-viewer/src/query.rs:187-197` | `file_text` reads `FileLine` and projects `text` — W11 |
| `crates/fjord-viewer/tests/over_a_real_index.rs:199,209` | the fixture writes the richer value |
| `crates/fjord-cli/src/workload.rs:193,232` | two workload queries |
| `crates/fjord-cli/examples/loadgen.rs:272,347` | the generated corpus |
| `crates/fjord-client/tests/byte_identical_with_dotnet.rs:75` | the independently-stated schema |
| `bench/FINDINGS.md:70` | §1's `src.Line` row — 8,583,810 rows at 4 columns |

**And it re-baselines a published number.** `src.Line` is the largest predicate by bytes in §1's
corpus; `FileLine`'s value is four fields where it was one, so the corpus's size and its per-row
read cost both move. §1's line-table figures are re-run with R7's list (W13).

**D3 · `styles` ships as a `string` in v1.** The run-length encoding is ASCII — a decimal length
then a single-letter kind, `"2p6k1p12s"`, a trailing `plain` run omitted, an unrecognised letter read
as `plain`. That last rule is why the vocabulary is **not** a sigla union: inside an opaque payload
a new kind costs nothing, where a union alternative would be a Breaking edit to a predicate every
published index carries (I10). `config.Setting {dimension = "style-vocabulary"}` (W7) states which
revision was written. When W3 lands, `styles` becomes a packed varint table and **this one
predicate's fingerprint moves** — which is the third reason it is a predicate of its own.

## Two consumer recipes that must be written down, because the shapes are sharp

**`FileLineAt` — offset → line.** Keyed `{file, start, line}`, all key. There is no descending seek
and no `LIMIT` in sigla, so the shape is a range upward bounded by the client:
`{file = F, start = X..}` with a client-side limit of 1.

- Exact hit: the returned row **is** the answer.
- Mid-line `X`: the returned row is the line *after* the one containing `X`, so the answer is
  `line - 1` — arithmetic on a row already in hand, no second seek.
- **`X` at or past the last line's start: the range is empty.** The consumer must fall back to
  `FileInfo.lines`, and that fallback is not optional — it is the common case for a reference in the
  last line of a file. Issue #39 does not state it.

**A window.** `FileLine {file = F, line = a..b}` is a range on the last key field: 0.4–1.4 ms for a
100 line window at any offset in a 500,000 line file, which rests on the `SeekKeyPart::Range`
planner fix already in the tree.

## Acceptance criteria

1. **The file checks, and its fingerprint is recorded.** `fjord --schema-path schemas schema check
   schemas/src.sigla` succeeds, and the schema-level fingerprint is written into the file's header
   comment **and** into a Rust golden table (criterion 3).
2. **The shape of the change is asserted mechanically, not argued.** A test —
   `crates/fjord-cli/tests/schemas.rs::the_source_layer_breaks_exactly_one_predicate` — resolves the
   previous `code.sigla` (kept as a golden copy under `crates/fjord-cli/tests/schemas/`) and the new
   one, and asserts `Identity::compatibility` is `Breaking` whose `broken` list is **exactly
   `["src.Line"]`**, that **eight** predicates were added (`src.File` moves, it does not arrive),
   and that **every predicate name present in both has an equal per-predicate fingerprint**. A second broken name, or a `~` on a surviving
   predicate, is a failure — this is what keeps "additive apart from one deliberate deletion" a fact
   rather than a hope.
3. **Every file in `schemas/` has a recorded fingerprint under test.** A golden table
   (`name → 0x…`) asserted by a test, so an accidental edit to a shipped schema is a red suite
   rather than a surprise at someone's handshake.
4. **A database is created from it and queried.** An integration test creates a database from a
   composite that includes `src`, writes a handful of facts covering `FileInfo`, `FileLine`,
   `FileLineAt`, `FileLineStyles`, `FileLanguage`, `FileDigest`, `FileOrigin` and `Symbol`, and runs
   one query per predicate, asserting rows. An unexercised predicate is a name in a file, not a
   layer.
5. **The two recipes are corpus'd, edge cases included.** Three queries, each with expected rows:
   offset exactly at a line start; offset mid-line; **offset past the last line's start, asserting
   zero rows**, with the `FileInfo` fallback shown in the test and in the schema comment.
6. **The window's cost claim is measured, not asserted.** A bench or a counted test showing a
   100 line window on a large synthetic file reads the window and not the offset — the same
   construction `iter::a_bounded_seek_reads_the_window_and_not_the_offset` already uses. Recorded in
   `bench/FINDINGS.md` with the corpus size.
7. **The style encoding has a decoder and a property.** A round-trip test over generated
   `(length, kind)` run lists: encode → decode → equal, including the omitted trailing `plain` run
   and an unrecognised letter reading as `plain`. It lives wherever the first consumer does (W11);
   the *format* is specified in the schema comment and the book.
8. **`sample_schema.rs` moves with it**: the predicate count assertion, the `KEY_ORDER` table, and
   the resolving reader from W4. `cargo test -p fjord-cli` green.
9. **The .NET side and the goldens.** The re-pasted constant in both C# files, the indexer emitting
   `FileLine` with all four value fields, regenerated `golden/blocks.txt`/`unions.txt`, and a client
   version bump (W14) — `cargo test -p fjord-client byte_identical` green. **This criterion is not
   met by editing the schema and leaving the client to be found out.**
10. **The book carries the layer.** A page for the schema set (W10) and the `nav.json` entry;
    `python3 website/build.py --strict` clean.
11. **The full gate**: `cargo test`, clippy/fmt on 1.97.1, `check-guards.py`, the wasm check.

## Traps

- **Exactly one file may declare a name.** Identical redeclaration rejects (W5), so the
  `code.sigla` edit is not optional and cannot be softened into "declare it in both for a while".
- **`Language`'s discriminants are frozen the day this lands** (I10). `other : string = 0` is the
  valve, and it is the reason the vocabulary can be closed at all.
- **Do not fold `styles` into `FileLine`'s value.** A value is fetched whole and cannot be projected
  field-wise (`nyi/value-field`, `flatten.rs:4818`), so a presentational field on the line table is
  charged to every consumer of any other field — measured at ~2.4× the bytes a viewer window needed.
- **`ByteSpan`'s unit is not a byte.** Roslyn and the TypeScript compiler count UTF-16 code units.
  The unit is declared once per database in W7 and this schema's comments must not say "byte" as
  though it were settled.
