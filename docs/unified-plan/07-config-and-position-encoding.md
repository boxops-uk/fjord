# W7 · `config.sigla` — what a database was built for, and which unit its spans are in

| | |
|---|---|
| **Issue** | [#42](https://github.com/boxops-uk/fjord/issues/42) items 2 and 3 — `[07]`; the predicate is revision 2's Decision 6, and appendix B of [#39](https://github.com/boxops-uk/fjord/issues/39) |
| **Area** | `schemas/`, `website/content/`, `clients/dotnet` (R3.5 writes it) |
| **Depends on** | W4 for the file split (fallback: declare inside `code.sigla`). **Not W6** — `config` imports nothing and stands alone, though its `position-encoding` dimension describes `src.ByteSpan`, so the two read best together |
| **Blocks** | **R3.5** (which must write these facts), W8 and W9 (which read `position-encoding`), W11 |
| **Invariants** | none |
| **Fingerprint** | a new file; `code.sigla` unaffected if `config` is its own namespace |

## Claim

A tool holding forty database handles can ask **which one this is** — what it was built for, what
its paths are relative to, which unit its spans count in, and which producers filled it — and get an
answer out of the database rather than out of its name.

## Why a name is not enough

Revision 2's Decision 6 is adopted verbatim and its argument is the one that matters: under
nearest-compatible target resolution a `netstandard2.0` project inside a `net9.0` index records
`netstandard2.0` in its own compilation facts — correctly, because that is what it compiled as — so
**the resolution root appears nowhere else in the index**. A tool asking which handle is the
`net9.0` index cannot compute it, and an instance name it can only string-match on is not an answer.

```schema
predicate Setting : { dimension : string, value : string }
```

Dimension-leading so "what framework is this database" is a seek; **key-only and multi-valued**, so
a dimension may hold several values (two defines, two languages) — which a record-shaped predicate
could not say.

## What this item adds to Decision 6: the reserved list

`{dimension : string, value : string}` with no registry means a **consumer** cannot know which
dimensions exist or what they mean. The plan itself needs two it never names (`framework`, `define`)
and notes a third is unrecorded. So the schema comment carries a **reserved-dimension list** — not a
union, which would defeat the point of dimension/value and make every new axis a Breaking edit:

| dimension | what a reader may assume |
|---|---|
| `repo`, `revision` | cross-database identity; a SCIP symbol carries a package coordinate, not an origin (C6). `src.FileOrigin` overrides per file where an index spans several |
| `index-root` | what every `src.File` path is relative to. The C# index's own provenance had to be *inferred* once, from which checkout was the only one in `$HOME`; this closes that |
| `position-encoding` | `utf8` or `utf16` — the unit of every `src.ByteSpan` in this database |
| `style-vocabulary` | which revision of the `src.FileLineStyles` kind table was written (`fjord-1`) |
| `symbol-scheme` | the scheme token(s) `src.Symbol` strings use here — what a fan-out checks before trusting a string match |
| `language` | multi-valued; what this database holds facts about |
| `producer` | one fact per producer and version, so a partial index says which layers exist |
| `framework`, `configuration`, `define`, `target-triple`, `feature` | the build axes the index was resolved against. **The facts are the truth and the flavor name is the convenience** |

**And one thing it is not**, stated in the comment because it is the misuse to expect: it is
**per-database**, so it cannot record a per-project or per-file axis. `{dimension = "define", value = "DEBUG"}`
says the run defined `DEBUG`, not which projects did. Where an axis genuinely varies within one
index, the fact that varies carries it — a compilation's own target framework on
`msbuild.Compilation`, a file's repository of origin on `src.FileOrigin`. Review #34 raised exactly
this ("`config.Setting` is per-database and multi-valued, so it cannot record the per-project axes
Decision 6 claims it absorbs"), and the answer is the comment plus the two per-fact escapes, not a
richer predicate.

## Position encoding — three units currently coexist in one namespace

Revision 2 has no occurrence of UTF-8, UTF-16 or "encoding" in a positional sense, and the tree has
three conventions:

- `src.Ref.at` and `src.FileXRef.at` are `{line, col, length}`, and the .NET indexer counts **UTF-16
  code units** — `GleanFacts.cs:297` is `text.ToCharArray()` over a .NET string, whose own doc says
  *"A .NET string is UTF-16"*.
- The `csharp` schema's `ByteSpan.start` is documented as a byte offset and **actually holds UTF-16
  code units**, for the same reason.
- `fjord_viewer::render::source` (`render.rs:113-164`) collects `text.chars()` and indexes by
  **Unicode scalar values**, which agrees with neither — one unit per codepoint where the producer
  counted two for anything above the BMP. Its own doc comment says *"1-based columns counted in
  characters, which is what the indexers emit"*, and that is the sentence that is wrong.

SCIP hit this and answered it with `Index.metadata.text_document_encoding` — **one declaration per
index, not per span**. So:

- `config.Setting {dimension = "position-encoding"}` with values `utf8` / `utf16`.
- `src.Ref.at`, `src.FileXRef.at`, `src.ByteSpan` and everything derived from them are in that unit.
- A database that does not state it is read as `utf16`, because that is what the compilers this set
  indexes actually count — **but stating it is the point**, and R3.5 states it.
- `src.FileLine` carries **both** offsets (`start` UTF-8 bytes, `cstart` UTF-16 code units), which
  makes the line table the conversion table: a consumer converts per line, against a row it has
  already fetched to render.
- The viewer is fixed in **W11**; a mixed-encoding database is deliberately not expressible.

## Acceptance criteria

1. **The file checks and is fingerprint-recorded**, and joins the golden table from W6's criterion 3.
2. **The reserved list is in the schema comment**, and every dimension in it is either written by a
   producer in this plan (R3.5) or has a named consumer (W8, W9, W11) — no dimension is reserved
   speculatively.
3. **A consumer can enumerate.** An integration test creates a database, writes the full reserved
   set, and asserts three queries: every setting (`config.Setting _`), one dimension's values
   (`{dimension = "language"}` returning two rows, proving multi-valued), and a seek on a dimension
   that is absent returning zero rows.
4. **`position-encoding` has a decoder-side test.** A fixture file containing a non-BMP character
   before a reference: with `utf16` declared, the span maps to the right characters through
   `src.FileLine.cstart`; with `utf8`, through `start`. This is the test that would have caught the
   viewer's `chars()` bug, and it belongs here rather than in W11 so the *schema's* claim is proven
   independently of any renderer.
5. **The book states the rule in one place** — the schema-set page (W10) — and the sentence *"a
   database that does not state its position encoding is read as `utf16`"* appears there and in the
   schema comment, identically.
6. **R3.5 writes them.** Cross-checked in W13: a fan-out database's `config.Setting` names exactly
   one framework, plus `repo`, `revision`, `index-root`, `position-encoding`, `symbol-scheme`,
   `producer` and at least one `language`. The gate is R3.5's, the vocabulary is this item's.
7. `python3 website/build.py --strict`, `cargo test`, clippy/fmt.

## Open sub-decision this item forces

**`--configuration` must be resolved either way in R3.5.** Nothing pins or records the configuration
today: `Loader` sets no `Configuration` and there is no `--configuration` flag. Recording
`{dimension = "configuration", value = "Debug"}` with a stated "Debug assumed" is acceptable and is
strictly better than silence. See W13, R3.5.
