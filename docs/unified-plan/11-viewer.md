# W11 · The viewer is retired, and what its replacement needs from this side

| | |
|---|---|
| **Issues** | [#36](https://github.com/boxops-uk/fjord/issues/36) Q4, [#39](https://github.com/boxops-uk/fjord/issues/39), [#42](https://github.com/boxops-uk/fjord/issues/42) item 3 |
| **Area** | `crates/fjord-viewer` (deleted), `fjord-server` (a WebSocket listener — **still unbuilt**, see criterion 4), and a new browser application |
| **Depends on** | **W7** (`position-encoding`) for the unit rule. **W6** and **W8** for the data the new viewer reads, not for the retirement |
| **Blocks** | **R9** — whose gate was "the viewer answers `/symbol/{name}`" and now needs another |
| **Invariants** | `ops-I10` — a new listener is default-closed, as TCP is |

> **This item was re-cut.** It used to be four defects in `crates/fjord-viewer`. [D11](OPEN-QUESTIONS.md)
> retires that crate, so three of the four move to whatever renders a line next and the fourth —
> "the line table it reads is being deleted" — stops existing. What is left is a deletion that
> happened, and a specification for the side of the boundary this repository owns.

## What happened

`fjord-viewer` is deleted: 1,950 lines across six files, a released binary, and nothing in the
workspace depended on it. It proved what it was built to prove — a viewer is an ordinary consumer
of the protocol, needing no privileged access to a database — and building it is what found the
two predicates the schema was missing, `src.FileXRef` and the case-folded search index, because
the questions a UI asks turned out not to be the questions the schema answered.

**Retired before W6, not migrated through it.** Two of W6's seven migration sites were in this
crate (`query.rs:187-197` reading `src.Line`, and `tests/over_a_real_index.rs:199,209`), so the
flag day is smaller by exactly those, and no work is spent on code that is going.

## Why a browser application rather than a better Rust one

The rendering, not the taste. A source view is a **merge of two independent sets of ranges over
one line** — syntax runs from `src.FileLineStyles`, cross-reference anchors from
`codemarkup.FileXRef` — split at the union of both boundaries and emitted as one correct nesting.
Server-rendered HTML can do that once; a virtualised scroll over a 50,000-line file then cannot
reuse any of it, and neither can a hover, a filter or a selection.

That is also the argument the style layer already makes for two lists of `(offset, length, kind)`
over a pre-baked `<span>` string: the merge is only expressible if both range sets are still
ranges when the renderer sees them.

## What this repository owes it

### 1 · A transport a browser can open

`fjord_client`'s `Transport` is a Unix socket or TCP, and a browser can open neither. **The answer
is a WebSocket listener on `fjord-server` carrying the same frames** — one protocol, one codec,
one set of goldens, and in particular the .NET golden keeps meaning something. The alternative
considered and rejected is a JSON/HTTP surface, which would be a second serialisation of every row
and contradicts a recorded decision: *"the server never produces JSON — a decision from the
original brief"* (`fjord_cli::rows`).

Three things it inherits rather than invents:

- **Default-closed**, as TCP is (`ops-I10`). Binding is an operator's decision and access control
  is the transport's job.
- **The same frames**, so `session.rs` is unchanged below the listener and the admission control,
  the fair writer and the stream multiplexing all apply as they are.
- **The same handshake**, so a browser client claims a schema fingerprint and is refused by the
  same path everything else is.

### 2 · The unit its columns count in

`config.Setting {dimension = "position-encoding"}` — `utf8` or `utf16`, declared once per database
(W7, landed). A database that does not state it is read as `utf16`.

The retired viewer got this wrong and it is worth writing down as the thing not to repeat:
`render::source` indexed by `str::chars()`, one unit per Unicode scalar value, where the producer
counted **UTF-16 code units**. A codepoint above the BMP costs two units and one `char`, so every
anchor after it on that line was off by one per such character. Bounds were checked, so the
failure was a link drawn over the wrong text — never a panic, which is why it survived.
`the_position_encodings_disagree_exactly_where_it_matters` (W7) pins the arithmetic.

### 3 · Routes that do not require a `code.sigla` index

`codemarkup` (W8): `Definition`, `FileDefinition`, `FileXRef`, `SymbolXRef`, `SearchEntry` /
`SymbolByName`. The point is that an index the viewer can serve no longer has to be one this
repository's own indexer produced — which is what makes a SCIP-converted index viewable, and what
lets R9's converter fill six predicates rather than synthesise a whole source layer.

## R9's gate, re-cut

Revision 2's R9 accepts on *"the viewer answers `/symbol/{name}` against a converted index"*. There
is no viewer to answer it, and waiting for the browser one would make a converter's acceptance
depend on an unrelated project's schedule.

**The replacement gate is the converter's output, asserted directly**: a database built by the SCIP
converter answers a fixed set of `codemarkup` queries — go-to-definition, every reference in a
file in position order, find-references across files, and a prefix search — with stated rows. That
is a better gate than the old one on its own terms: it tests the converter rather than a UI, it
fails in the converter's own test suite rather than through a web request, and it does not go red
when somebody changes a stylesheet.

## Acceptance criteria

1. **The crate is gone**, and with it every reference outside release history: `release.yml`'s
   build, staging, `SHA256SUMS`, attestation and upload lists; `AGENTS.md`'s module map;
   `building.md`, `getting-started.md`, `index.md`, `clients.md`; `README.md`. `cargo test` green,
   `website/build.py --strict` clean, `check-docs.py` clean.
2. **The book says it is retired and why**, in `clients.md`, along with the three things its
   replacement needs — so a reader who knew the viewer is not left wondering where it went.
3. **The release drops from four binaries to two**, and the notes stop naming it.
4. **The WebSocket listener** carries the same frames, is default-closed, and is covered by the
   existing socket battery run over the new transport — not by a second battery, which would be
   two statements of one protocol.
   **Not met: the listener was not built.** `grep -rni 'websocket|tungstenite|ws://'` over
   `crates/`, `wasm/`, `web/` and `clients/` returns prose and nothing else, so no battery can run
   over it and no test can close this. The battery half *is* done — it is transport-generic now and
   runs over Unix and TCP, so a third door is an arm rather than a second battery, and the listener
   inherits this coverage the day it lands. What is left is four decisions rather than one
   implementation, and they are recorded in [`PLAN.md`](../../PLAN.md#a-transport-a-browser-can-open).
5. **R9's gate is the converter's own**, stated as queries and rows in `docs/unified-plan/13-indexer-runs-amended.md`.

## Not in scope

- The browser application itself. It is a project rather than a work item, and it wants a plan of
  its own beside this one.
- Anything about *what* the new viewer looks like. This file is the boundary, not the product.
