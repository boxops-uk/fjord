# W11 · The viewer — the right unit, the style layer, and a route that is not `src.Decl`

| | |
|---|---|
| **Issues** | [#36](https://github.com/boxops-uk/fjord/issues/36) Q4, [#39](https://github.com/boxops-uk/fjord/issues/39), [#42](https://github.com/boxops-uk/fjord/issues/42) item 3 |
| **Area** | `crates/fjord-viewer` (`query.rs`, `render.rs`) |
| **Depends on** | **W6** (`src.FileLine`, `src.FileLineStyles`), **W7** (`position-encoding`), **W8** (the routes) |
| **Blocks** | **R9** — whose gate is the viewer answering `/symbol/{name}` against a converted index, and which W13 re-points at `codemarkup` |
| **Invariants** | none. The viewer *"reaches nothing below `fjord_client`"* (`lib.rs:14-24`) and that stays true |

## Claim

The viewer renders a line at the column the producer meant, splices syntax runs and cross-reference
anchors as **one** nesting, and answers its five questions against `codemarkup` — so an index it can
serve no longer has to be a `code.sigla` index.

## Three defects and one addition

**1 · The column unit is wrong for non-BMP source, silently.** `render::source`
(`render.rs:113-164`) does:

```rust
let chars: Vec<char> = text.chars().collect();
…
out.push_str(&escape(&chars[cursor..from].iter().collect::<String>()));
```

`chars()` yields one element per **Unicode scalar value**. The producer counts **UTF-16 code
units** — `GleanFacts.cs:297` is `text.ToCharArray()` over a .NET string, whose own doc says *"A
.NET string is UTF-16"*. A character above the BMP costs two units and one `char`, so every anchor
after it on that line is off by one per such character. Bounds are checked, so the failure is a link
drawn over the wrong text or dropped — never a panic, which is why it has survived. The doc comment
at `render.rs:107` — *"1-based columns counted in characters, which is what the indexers emit"* — is
the sentence that is wrong.

**2 · Nothing declares the unit.** W7 adds `config.Setting {dimension = "position-encoding"}`; the
viewer reads it once per database and indexes in that unit, defaulting to `utf16` and saying so.

**3 · The line table it reads is being deleted.** `file_text` (`query.rs:187-197`) reads
`src.Line`, which W6 removes in favour of `src.FileLine` — so this is not an improvement the viewer
can decline: after W6 the query does not typecheck. `FileLine` carries `start`, `bytes` and `cstart`
beside the text, which is what makes offset↔line and unit conversion possible without a second
query. `tests/over_a_real_index.rs:199,209` moves with it.

**A question this item cannot answer for itself.** If `fjord-viewer` is being retired, most of what
follows moves rather than disappears: the position-encoding rule and the style-run merge belong to
*whatever renders a line*, the `codemarkup` routes belong to whatever serves a symbol, and **R9 needs
a different gate** — its acceptance is "the viewer answers `/symbol/{name}` against a converted
index". Nothing in the tree records a decision to retire it: it is a released binary
(`release.yml` stages `dist/fjord-viewer`), a workspace crate, and R9's stated gate. So this item
proceeds as written, and if the viewer goes, W11 and R9's gate are re-cut together — see
[`OPEN-QUESTIONS.md`](OPEN-QUESTIONS.md) D5.

**4 · The style layer.** `src.FileLineStyles` is a second set of ranges over the same line. Any
renderer must split at the union of both boundaries and emit one nesting — which two lists of
`(offset, length, kind)` do and a pre-baked `<span>` string does not, without parsing the HTML back
out. That is the argument this batch makes for the format, and the viewer is where it is proved.

## The work

- **A window is one prefix seek per layer**: `src.FileLine {file = F, line = a..b}` and
  `src.FileLineStyles {file = F, line = a..b}` — the same key shape, so a virtualised viewer costs
  two range seeks per window.
- **One merge, not two passes.** Split at the union of the style boundaries and the anchor
  boundaries; emit `<span class="…">` for kinds and `<a href>` for references, correctly nested.
- **Kind → CSS class is the viewer's business.** fjord defines the letters; the stylesheet is
  `fjord-viewer`'s and lives in its own file so it can change without re-indexing anything — which
  is the whole argument against baked HTML.
- **`codemarkup` routes.** The five queries move to `codemarkup.FileDefinition`,
  `codemarkup.FileXRef`, `codemarkup.SymbolXRef`, `codemarkup.SearchEntry` / `SymbolByName` and
  `codemarkup.Definition`. **The `src.*` routes stay**: the reference index is still a `code.sigla`
  index, and the viewer must serve both. Which set to use is decided per database from **the schema
  the server serves** — the viewer already fetches it on a probe connection (`lib.rs:107-116`,
  `served_schema()`) precisely because *"a schema belongs to the database (I13)"* and compiling one
  in *"was wrong twice"*. So the existing *"this database's schema is not a code index"* refusal
  (`lib.rs:105`) becomes a three-way choice: `codemarkup` routes, `src.*` routes, or a refusal that
  names what is missing. **No flag.**

## Acceptance criteria

1. **The non-BMP case is a test, not a review note.** `over_a_real_index`-style test: a fixture file
   whose line contains a character above the BMP before a reference; the anchor covers exactly the
   reference's text. It must fail against today's `chars()` indexing — a test that passes both ways
   proves nothing here.
2. **Both units are exercised.** The same fixture served from a database declaring `utf16` and one
   declaring `utf8`, each rendering correctly; a database declaring neither renders as `utf16` and
   the default is stated in the code and the book.
3. **Anchors and styles merge as one nesting.** A test over a line with overlapping style and anchor
   ranges asserts the output is well-formed and that no element crosses another's boundary — the
   property a baked-HTML field could not have satisfied.
4. **The style decoder round-trips**, including the omitted trailing `plain` run and an unrecognised
   kind letter reading as `plain` (W6 c7's home).
5. **A window is two seeks and no whole-file fetch.** A counted test — a store spy or the query
   profile — asserting a 100-line window on a large file reads the window, on both layers.
6. **The `codemarkup` routes answer.** Every viewer route answers against a `codemarkup`-only
   database (no `src.Decl` anywhere in it), and every route still answers against a `code.sigla`
   index. Two suites, one binary.
7. **R9's gate is reachable.** `/symbol/{name}` answers against a database holding only what a SCIP
   converter can fill — `src.File`, the line table, `codemarkup.Definition`, `FileXRef`,
   `SymbolXRef`, `SearchEntry`. This criterion is what makes W13's re-pointing of R9 real rather
   than a paragraph.
8. `cargo test -p fjord-viewer`, clippy/fmt, and the book's viewer section (W10).

## Traps

- **Do not read a style run's length in one unit and a column in another.** Style run lengths are in
  the database's declared position encoding, the same as every span; the merge is only correct if
  both range sets are in that unit before it starts.
- **`src.Line` and `src.FileLine` must not both be read for one file.** A producer fills one; the
  viewer picks per database, and a database holding both is a producer bug worth reporting rather
  than merging.
