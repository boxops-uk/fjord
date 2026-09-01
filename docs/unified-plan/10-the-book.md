# W10 · The book — every claim this batch changes, written where a reader will find it

| | |
|---|---|
| **Issues** | [#36](https://github.com/boxops-uk/fjord/issues/36) (the style format), [#39](https://github.com/boxops-uk/fjord/issues/39), [#41](https://github.com/boxops-uk/fjord/issues/41) item 4, [#42](https://github.com/boxops-uk/fjord/issues/42) item 3, [#43](https://github.com/boxops-uk/fjord/issues/43) |
| **Area** | `website/content/`, `website/nav.json`, `docs/glean.md`, `AGENTS.md` |
| **Depends on** | each item it documents; lands **with** that item, not after it |
| **Blocks** | nothing |

## Claim

Nothing in this batch ships with its rationale only in a GitHub issue: the format the style layer
defines, the unit a span is in, how a schema spans files, what a sealed artifact contains, and the
three sentences that are currently wrong about the tree, are all in the book — and the CI gate that
would catch a missing page is the one that runs.

## What the gate actually checks, so the work is scoped honestly

`python3 website/build.py --strict` turns exactly **two** warnings into failures: a nav-listed slug
with no `content/<slug>.md`, and a `content/*.md` with no `nav.json` entry. It does **not** validate
markdown links and does **not** compile `:::demo` blocks. Sigla samples are checked elsewhere:
`crates/fjord-inspect/tests/lowered.rs::every_sample_compiles_clean`, sourced from
`fjord_inspect::samples::SAMPLES`, plus `(cd web && npm run smoke)`, which compares the two
renderers page for page.

So: **a new page needs a nav entry, and any `:::demo` block on it needs an entry in `SAMPLES` or it
is checked by nothing.**

## The work, page by page

| Page | Change | From |
|---|---|---|
| **new — the schema set** | one page: what `src`, `config`, `codemarkup` and the five reference schemas are, the layering, the "reference schemas" status sentence, the `Kind`/`Role`/`Language` citation tables, and the two worked joins | W6, W7, W8, W9 |
| **new or a section — the style layer** | the run-length format in full: the grammar, the omitted trailing `plain` run, the unrecognised-letter rule, the LSP-derived kind table with its numbering, and the statement that **fjord defines the letters and no colours** | W6 |
| `schema-language.md` | the redeclaration sentence (`:227-228`) corrected; a section on splitting a schema across files, with `resolve_from` for an embedder; `code.sigla`'s "worked example rather than a default" sentence extended to say what the reference schemas are | W4, W5, W6 |
| the wire-protocol page | how predicate ids are assigned (sorted qualified name, `fjord.*` last, at create, append-only for life), that the wire carries names so the numbering never leaves the database, and the one consumer hazard: a `FactId`'s tag is the *database's* numbering | W5 |
| `storage.md` | `MARK_BYTES` in the marker table, why it sorts after unions (I3, not taste), and what `bytes` is for | W3 |
| `operations.md` | what a sealed directory contains after `finish`, and the artifact-size statement `FJORD_META.bytes` makes | W12 |
| `clients.md` | the viewer's style rendering and its position-encoding rule | W11 |
| `invariants.md` | no invariant moves. If W12 adds a guard for the sealed-artifact claim, the `ops-I*` table gains its first Guard column entry | W12 |
| `docs/glean.md` | `:57-58` still says `ops-I5` adopts the conflict-reject rule; `invariants.md:403-404` assigns order-independent rejection to **`ops-I4`**, and revision 2's own correction table says so. One-line fix, before anyone cites it forward | research |
| `AGENTS.md` | the build list gains `cargo check -p fjord-schema --no-default-features --target wasm32-unknown-unknown` (W4 c2) and the exhaustiveness recipe (W2 c3) | W2, W4 |

## Acceptance criteria

1. **`python3 website/build.py --strict` clean**, with every new page in `nav.json` under a group
   that reads sensibly in the reading order.
2. **Every `:::demo` block on a new page is in `fjord_inspect::samples::SAMPLES`**, so
   `every_sample_compiles_clean` covers it. A demo block that is not in `SAMPLES` is checked by
   nothing and must not ship.
3. **`(cd web && npm run smoke)` green** — the interactive site renders the new pages, and the
   page-for-page comparison between the two renderers passes.
4. **The three corrections are made and each is pinned by a test where one can be**: the
   redeclaration sentence (W5 pins the behaviour in the corpus), the `glean.md` invariant citation,
   and the viewer's "columns counted in characters" doc comment (W11 pins the behaviour).
5. **`python3 scripts/check-docs.py`** — the docs gate the `test` job already runs — stays green.
6. **No page describes something that does not exist.** Each new page lands in the same commit as
   the work item it documents, not ahead of it.
