# The unified plan — the schema set, the engine gaps it needs, the indexer runs, and one storage defect

**What this is.** Eight issues were filed against this repository between 31 August and 1 September
2026 — [#36](https://github.com/boxops-uk/fjord/issues/36) through
[#43](https://github.com/boxops-uk/fjord/issues/43) — by a consumer running a federated code search
on three fjord databases. They arrive as a set: five are schema proposals that stand on each other,
two are engine defects the schemas hit, and one is an unrelated storage observation. They also amend
[`docs/indexer-overhaul-plan.md`](../indexer-overhaul-plan.md) (revision 2), which answers
[#28](https://github.com/boxops-uk/fjord/issues/28)–[#32](https://github.com/boxops-uk/fjord/issues/32)
and review [#34](https://github.com/boxops-uk/fjord/issues/34).

This directory is one route through all of it: **fourteen work items**, each with one falsifiable
claim and acceptance criteria that are tests and commands rather than intentions.

**What is authoritative.** Revision 2 remains the specification of Runs 0–9; **[W13](13-indexer-runs-amended.md)
amends it**, and where the two disagree W13 wins. Everything else here is new and this directory is
its specification.

**[`OPEN-QUESTIONS.md`](OPEN-QUESTIONS.md) carries the decisions** — ten put for review and all ten
answered, with the consequence of each — plus ten corrections the plan makes to the issues and eight
risks it carries. **One question is still open**: whether `fjord-viewer` is being retired, which
would re-cut W11 and R9's gate.

---

## The issues, and where each one lands

| Issue | Batch label | Work items |
|---|---|---|
| [#37](https://github.com/boxops-uk/fjord/issues/37) — no `Ty::Union` arm in `unify` | `[01]` | **W1** |
| [#38](https://github.com/boxops-uk/fjord/issues/38) — exhaustiveness, then `bytes` | `[02]` | **W2**, **W3** |
| [#39](https://github.com/boxops-uk/fjord/issues/39) — one shared `src` source layer | `[03]` | **W6**, and W7/W10/W11 |
| [#40](https://github.com/boxops-uk/fjord/issues/40) — `codemarkup`, plus five language schemas in its comments | `[04]`, `[05]` | **W8**, **W9** |
| [#41](https://github.com/boxops-uk/fjord/issues/41) — embedded readers never `resolve` | `[06]` | **W4**, **W5** |
| [#42](https://github.com/boxops-uk/fjord/issues/42) — revision 2 follow-ups | `[07]` | **W7** (items 2–3), **W13** (items 1, 4) |
| [#43](https://github.com/boxops-uk/fjord/issues/43) — a sealed database keeps its journals | `[08]` | **W12** |
| [#36](https://github.com/boxops-uk/fjord/issues/36) — the content schema and style layer | superseded by `[03]` | **W6** (the style layer is adopted whole), **W11** |
| [#28](https://github.com/boxops-uk/fjord/issues/28)–[#32](https://github.com/boxops-uk/fjord/issues/32), [#34](https://github.com/boxops-uk/fjord/issues/34) | — | **W13** (Runs 0–9, amended), **W14** |

Not in this plan: [#18](https://github.com/boxops-uk/fjord/issues/18) (cost-based reorder), which has
its own plan in `scratchpad/`. It touches the planner and nothing here; the only interaction worth
knowing is that W6/W8/W9 add predicates whose cardinalities a cost model would read.

---

## The fourteen work items

| # | Item | Issue | Depends on | Moves a fingerprint? | Size |
|---|---|---|---|---|---|
| **W1** | [`unify` grows a `Ty::Union` arm](01-unify-union-arm.md) | #37 | — | no | S |
| **W2** | [Close the exhaustiveness gap](02-exhaustiveness-gap.md) | #38 | W1 | no | M |
| **W3** | [A `bytes` scalar family](03-bytes-scalar-family.md) | #38 | W2 | new only | M |
| **W4** | [Embedded schema resolution](04-embedded-schema-resolution.md) | #41 | — | no | M |
| **W5** | [Schema diagnostics and three corrections](05-schema-diagnostics-and-corrections.md) | #41 | W4 | no | S |
| **W6** | [`src.sigla` — one shared source layer](06-src-source-layer.md) | #39 | W4 | **yes — `code.sigla`, Breaking** | M |
| **W7** | [`config.sigla` and position encoding](07-config-and-position-encoding.md) | #42, #39 | W4 | new only | S |
| **W8** | [`codemarkup.sigla`](08-codemarkup-layer.md) | #40 | W6, W7 | new only | M |
| **W9** | [The language layers and `index.sigla`](09-language-layers.md) | #40's comments | W1, W6, W8 | new only | **L** |
| **W10** | [The book](10-the-book.md) | all | each item | no | S per item |
| **W11** | [The viewer](11-viewer.md) | #36, #39, #42 | W6, W7, W8 | no | M |
| **W12** | [A sealed database is its tables](12-sealed-database-journals.md) | #43 | — | no | M |
| **W13** | [The indexer runs, amended](13-indexer-runs-amended.md) | #28–#34, #42 | W6, W7, W8, W11 | **yes — R4** | **XL** |
| **W14** | [The flag-day inventory](14-flag-day-inventory.md) | R4e, #39 | — | it *is* the fingerprint move | S |

**W13 is a tier of its own.** It is not one work item: it carries amendments to all ten of
revision 2's runs, a new sub-run (R3.7), an edit to the required CI job, a 61-file migration, and one
piece of work it explicitly declines to price (extracting an `IFactWriter` from a raw `Thread[]`).
Its acceptance criteria are the runs' own gates, which is why it contributes none of the 106 below.

**W1 has been spiked and reverted.** The arm was written, the shared-union query planned and ran
against the existing fixture (`20; 40; 10; 30`), the alternative-narrowed form was unchanged, and
`cargo test -p fjord-engine` stayed green at **496 passed, 0 failed**. It is the smallest item here,
it needs no fixture change and no `flatten` work, and it unblocks the most — start there.

---

## Sequencing

Seven orderings are load-bearing. Everything else is preference.

```
W12  sealed database is its tables ───────────┐  independent; before R7 re-measures anything
W1   the union arm ───────────────────────────┤  smallest item, unblocks W9 and R4's descriptor
                                              │
W4   embedded resolution ──► W5 diagnostics   │  W4 is the gate on every schema being a *file*
       │                                      │
       ├──► W7 config (stands alone)          │
       │                                      │
       └──► W6 src ─┬─► W8 codemarkup ─► W9 language layers + index
                    │                         │  W6 is a flag day: Breaking, `src.Line` goes
                    └─► W11 viewer ───────────┼──► gates R9
                                              │
W2   exhaustiveness ──► W3 bytes ─────────────┘  parallel to the schema track throughout

R0   ledger                   R3.5 fan-out (needs W7)              R6  writer default
R0.5 .NET test job            R3.6 delete Declared.First           R7  re-measure (reads W12)
R1   #28 workspace load       R3.7 delete --syntax-only            R8  the seam
R2   #29 retry                R4.0 conflict census                 R9  SCIP (needs W11)
R3   #32 project rescue       R4   semantic key (needs W6, W8, W14)
                              R5   de-gate
```

1. **W12 before R7, and before anyone records an artifact size.** It does *not* disturb R0: a
   sealed identity is a hash over the facts, so flushing memtables moves no fact and no identity —
   which is W12's own first criterion. What it does move is `finish`'s cost and the artifact's size
   on disk, so it belongs ahead of the run that re-measures and ahead of any sizing estimate.
2. **W4 before W6/W7/W8/W9** if they are to be files. They can otherwise be declared inside
   `code.sigla` at *identical* fingerprints — verified — and split later as a no-op edit.
3. **W6 before R4b**, because `src.sigla` owns `src.Symbol` and identical redeclaration rejects.
4. **W8 before R4b** if `src.ExternalRef` is to be dropped in favour of `codemarkup.SymbolXRef`;
   otherwise R4b keeps `ExternalRef` **and owes it a file-keyed twin**.
5. **W11 before R9**, since R9's gate is the viewer answering against a converted index and this plan
   re-points it at `codemarkup` routes.
6. **W1 before W2**, so W2's type-path experiment has a real arm to protect; and W2 before W3, which
   is the issue's own ordering and the reason Part 0 is worth doing even if `bytes` is refused.
7. **The two flag days stay separate, W6 first** ([W14](14-flag-day-inventory.md)). Both are
   Breaking now — W6 deletes `src.Line`, R4 re-keys `src.Decl` — and combining them halves the
   ceremony while producing a diff nobody can review. Each is rehearsed end to end on a branch
   before it is landed.

---

## What this adds up to

**108 acceptance criteria across thirteen items** (W13 carries amendments to revision 2's own run
gates rather than criteria of its own), plus revision 2's Runs 0–9.

New on disk: **nine new schema files** — `src`, `config`, `codemarkup`, the five language schemas
and the composite, so eleven in `schemas/` where there are two today. The composite resolves to
**138 predicates** (9 `src` + 1 `config` + 10 `codemarkup` + 118 across the five language layers),
all but one of them new — `src.File` moves rather than arrives. Plus **one new scalar family** with
its `0x…` literal, **one new diagnostic code**, **one new marker byte**, **one protocol bump**, and
**two flag days**.

Removed: `src.Line`, `--syntax-only`, six silent wildcard arms, one dead `Serialize` impl, one
missing memtable flush, and three sentences in the book that are wrong about the tree.

---

## What "done" means for every item here

The repository's rules, restated because each work item's criteria assume them:

- **A guard is written first and lands green.** No work item may leave a new `#[ignore]`d test.
  `scripts/check-guards.py` accepts exactly `guard: <claim>, owned by Movement <N>` with `N` in
  0–8, and Movement 0 has closed — so an ignored guard from this plan would need the ledger's owner
  vocabulary extended **and** `scripts/test_check_guards.py` updated with it. Prefer green.
- **A new diagnostic code needs a corpus entry.** `every_code_is_reachable_from_the_corpus` asserts
  every `Code::ALL` variant is reachable; an excuse list entry is not a substitute.
- **A new `Supported` corpus entry moves a positional hash list.**
  `every_supported_entrys_plan_fingerprint_is_stable` holds a `Vec` keyed to `CORPUS` order —
  regenerate it, never hand-edit.
- **A property before an example.** Generators live in the canonical `proptest` support modules and
  are imported, never defined inline; a generator's population is asserted, because a strategy that
  degenerates leaves its property green and vacuous.
- **The gate, in full:**

  ```bash
  cargo build && cargo test
  cargo test -- --ignored --list
  python3 scripts/check-guards.py
  python3 -m unittest scripts/test_check_guards.py
  cargo +1.97.1 clippy --all-targets --workspace -- -D warnings
  cargo +1.97.1 fmt --all
  python3 website/build.py --strict
  cargo check -p fjord-engine --target wasm32-unknown-unknown
  ./scripts/build-wasm.sh && (cd web && npm run smoke)
  ```

  W4 adds `cargo check -p fjord-schema --no-default-features --target wasm32-unknown-unknown`.

---

## How to burn it down

Each work item is a branch and a PR. Within an item, the order is always the same: **write the
failing test, watch it fail, fill the implementation, then the book**. Two items (W9, W13) are
explicitly *not* one diff — W9 lands one schema file per commit in the stated order, and W13's runs
are revision 2's, one at a time.

The two flag days (W6, R4) are rehearsed end to end on a branch before either is landed, because
the expensive failure there is discovering step 4 of nine after step 8.
