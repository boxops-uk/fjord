# W2 · Close the exhaustiveness gap — a new scalar family is a compile error, everywhere

| | |
|---|---|
| **Issue** | [#38](https://github.com/boxops-uk/fjord/issues/38) Part 0 — `[02]` in the issue batch |
| **Area** | `fjord-schema`, `fjord-encoding`, `fjord-wire`, `fjord-store`, `fjord-ingest`, `fjord-server`, `fjord-engine`, `clients/dotnet` |
| **Depends on** | W1 (so the type-path experiment has a real arm to protect) |
| **Blocks** | W3 (`bytes`) — the issue's own ordering, and the reason Part 0 is worth doing even if `bytes` is refused |
| **Invariants** | none moved. I2 (`skip` walks any typed value) is the property the codec half must keep |
| **Fingerprint** | unmoved |
| **Format** | untouched |

## Claim

Adding a variant to `PredicateTyNamed`, or to `fjord_engine::syntax::Ty`, produces a **compile
error at every site that must handle it**, before any test runs — and the experiment that proves
this is written down and repeatable.

## What is actually wrong, and three corrections to the issue

The issue reports five `_ =>` arms that absorb a new scalar family and fail at runtime. Verified
against the tree, with three differences that change the work:

| # | Site | Wildcard | What it reports | Provoked by a test today? |
|---|---|---|---|---|
| 1 | `fjord_schema::syntax::print::same_ty` (`print.rs:166`) | `print.rs:201` `_ => false` | two schemas compare unequal | **no** |
| 2 | `fjord_wire::value::encode_value` (`value.rs:135`) | `value.rs:193` | `TypeMismatch("value does not fit this type")` | **no** |
| 3 | `fjord_store::fact::checked` (`fact.rs:190`) | `fact.rs:286` | `FactError::TypeMismatch` | yes — `a_field_of_the_wrong_type_is_reported` (`fact.rs:479`) |
| 4 | `fjord_ingest::intern::resolve` (`intern.rs:155`) | `intern.rs:253` | *"does not fit the type the schema declares for it"* | **no** |
| 5 | `fjord_server::rows::to_wire` (`rows.rs:121`) | `rows.rs:165` | `Unprojectable("a row that does not fit the type its head produced")` | **no** |
| 6 | **`fjord_encoding::tuple::encode_typed_at` (`tuple.rs:758`)** | `tuple.rs:824` | `StoreCodecError::BadRecord` | **no** |

**Correction 1 — `same_ty` is not dead code, and the issue's retraction of its own claim was
premature.** The issue names `syntax::print::compatible` as the only caller and reports it
unreachable. There is no `fn compatible` in the tree. `same_ty`'s caller is
`syntax::print::equivalent` (`print.rs:136`), and it has **exactly one production call site**:
`recoverable()` (`catalog.rs:875-885`), which `Catalog::create` runs before anything exists. Every
other call is a test (`schema_doc.rs:139`, `tests/catalog.rs:93`, `print.rs`'s own battery).

That one site is enough, and it is worth being precise about what it costs, because the issue first
claimed *"`same_ty` would make `create --schema` reject a valid schema"* and then withdrew it as
*"inferred and … wrong"*. **The withdrawal was too broad.** `recoverable()`'s own doc comment says
what it is for — a schema *"built rather than parsed"*, because *"`Schema` is a public type, and the
failure it prevents is silent"* — and *"everything that came from a `.sigla` file passes by
construction"*. So on the day a scalar family is added: a schema parsed from a file still creates,
and a schema **built programmatically** is rejected by `create`, with a message saying the two
schemas disagree and nothing saying which family it could not compare. Narrower than the issue's
first claim, real, and not what its retraction implies.

**Correction 2 — there are six sites, not five.** `encode_typed_at` is the bottom of the storage
encoder that `fjord_store::fact::encode` reaches through, and its wildcard reuses
`StoreCodecError::BadRecord` — so a scalar-family mismatch there reports as *"bad record"*, which
is the one message in the set that actively misdirects. Its decode-side sibling `decode_typed_at`
(`tuple.rs:1381`) matches over `ty` **alone** and is exhaustive, which is the pattern this work item
generalises: the gap is exactly the sites that match a `PredicateTy` **jointly** with an
already-built value.

**Correction 3 — `#[deny(clippy::wildcard_enum_match_arm)]` would catch none of them.** The lint
fires on `match <single-enum-expr>` and never on `match (a, b)`. All six sites are tuple matches.
Measured: the lint reports **160 sites workspace-wide, 120 hand-written after excluding the 40 in
lelwel's `generated.rs`, 85 of them in `fjord-engine`** — and **not one** of the six appears among
them. So the lint is not "optional regression insurance" for this defect; it is a *consequence* of
the restructure, and it only becomes able to see these sites once the outer match is over one enum.

`impl Serialize for Value` (`tuple.rs:1555`) **is** dead as the issue says — exhaustive over all six
`Value` variants, and no caller anywhere in `crates/`, `wasm/`, `web/` or `clients/`. Both live JSON
renderers hand-roll their own match instead.

## The work

**1 · Restructure the six joint matches to dispatch on `ty` first.** The outer match becomes
exhaustive over the thing that grows; the inner wildcard covers only a genuine value/type mismatch:

```rust
Ok(match ty {
    PredicateTy::Str => match value {
        WireValue::Str(s) => …,
        _ => return Err(mismatch()),
    },
    // …one arm per family, and no `_` on the outer match
})
```

**2 · Add `#[deny(clippy::wildcard_enum_match_arm)]` at those six functions only** — now that the
outer scrutinee is a single enum, the lint can see them, and it is what stops the restructure being
undone. Targeted, never blanket: there are 120 legitimate hand-written wildcard sites, 85 of them in
`fjord-engine` matching over AST node kinds.

**3 · The same restructure on the type path.** `Checker::unify` (`ty.rs:822`) matches
`(a, b)` jointly, so a new `Ty` variant is silently absorbed there exactly as a new
`PredicateTy` variant is absorbed by the six. Dispatch on one side after the `Var` and `Ty::Error`
cases are handled (they are symmetric and must stay first, see W1). `zonk`, `occurs`,
`Checker::render` and `schema_ty` are already exhaustive and stay so.

**4 · Close the `TySpec` parity gap.** `fjord_encoding::tuple::proptest::TySpec` (`tuple.rs:1607-1618`)
is a *parallel* enum with exactly `PredicateTyNamed`'s five constructors. A new family that does not
grow a `TySpec` arm is never drawn by `arb_typed_pair` (`tuple.rs:1828`), so every law over it —
order-matches-oracle against `cmp_typed` (`:1741`), round-trip, `skip_walks_any_typed_value`
(`:3235`) — **keeps passing vacuously**. Either derive one enum from the other, or add a census
assertion per family (see criterion 5).

**5 · Drive the exhaustive generator through the rest of the stack.** `fjord_wire::value`'s
generator (`Tape::value`, `value.rs:672`) is exhaustive over `PredicateTy` and is behind the
`proptest` feature, which **only `fjord-ingest` enables**. Extend it to `fjord_server::rows::to_wire`
and to both live JSON renderers, so a family that reaches storage also reaches the wire and the two
renderings.

**6 · Delete `impl Serialize for Value`** (`tuple.rs:1555`), with its three now-unused imports. It
sits against `fjord-inspect`'s own stated design — *"the internals do not derive `Serialize`, and
that is the design"* — and the letter of that comment holds today only because this one is
hand-written rather than derived.

**7 · The .NET client has the same shape and is in scope for the *statement*, not the fix.**
`ValueCodec.WriteValue` (`clients/dotnet/Boxops.Fjord.Client/Values.cs:118`) is a
`switch (type, value)` with a `default:` throw at `:165-167`, and `GleanFacts.WriteValue`
(`Boxops.Fjord.Indexer/GleanFacts.cs:167-221`) is the same shape with four arms — the latter is
already scheduled as **R4c**. Record here that C# has no analyser equivalent of the restructure, so
the mechanism on that side is the flag-day checklist (W14) plus R4c's arm, not a lint.

## Acceptance criteria

1. **The type-model experiment, run and recorded.** Add a throwaway variant to
   `PredicateTyNamed` on a scratch branch. Every site that must handle it is a **compile error
   before any test runs**, and the recorded transcript names all six sites above plus the
   compiler-guided ones. `cargo build --workspace` must fail; `cargo test` must never be reached.
   The transcript goes in the commit message or `bench/FINDINGS.md`, not into a test.
2. **The type-path experiment, run and recorded.** The same, for a throwaway variant of
   `fjord_engine::syntax::Ty`: compile errors in `unify`, `zonk`, `occurs`, `render` and
   `schema_ty`, and in nothing that merely passes a `Ty` through.
3. **Both experiments are repeatable by a script.** `scripts/check-exhaustive.sh` (or a documented
   two-line recipe in `AGENTS.md`'s build section) applies the variant, builds, and asserts a
   non-empty compile-error set — so the next person does not have to reconstruct the method. It is
   **not** wired into CI: it fails the build by design.
4. **The six wildcards are gone**, and each of the six functions carries
   `#[deny(clippy::wildcard_enum_match_arm)]`. Mechanical: `cargo +1.97.1 clippy --all-targets
   --workspace -- -D warnings` clean, and a grep for `_ =>` inside those six functions returns
   nothing.
5. **No law is vacuous.** A census test asserts that `arb_typed_pair` draws **every**
   `PredicateTy` family at least once in a run of the standard case count — the shape
   `the_generator_reaches_every_wire_shape` (`value.rs:1037`) already uses. It must fail if a family
   is added to `PredicateTy` without a `TySpec` arm.
6. **The two live renderers are proven to agree.** A new test — `fjord-cli` or a shared
   integration test — renders the same value through `fjord_cli::rows::json` (a `WireValue` + `Desc`)
   and `fjord_inspect::value::json` (a storage `Value` + `Schema`) for every family, and asserts the
   two JSON shapes are equal. Today nothing enforces this, and both independently chose
   `{"alt": payload}` for a union; a family tagged by one and bare by the other would make the shape
   of a row depend on which endpoint served it.
7. **The five unprovoked sites gain the test that provokes them.** Sites 1, 2, 4, 5 and 6 have no
   test that reaches their mismatch arm today (site 3 does — `a_field_of_the_wrong_type_is_reported`,
   `fact.rs:479`). Each gains one, per AGENTS.md's rule that *"every error state is demonstrated by
   a test that provokes it"*.
8. **Site 1 is provoked through its real caller.** `a_built_schema_with_an_uncomparable_field_is_refused`
   — a `Schema` built programmatically whose two sides differ only in the new family must be refused
   by `Catalog::create` (through `recoverable()` → `equivalent` → `same_ty`), and after the
   restructure must be refused for the *right* reason. Without this, every other criterion here can
   pass while the site Correction 1 argues is dangerous stays unverified.
9. **`impl Serialize for Value` is gone** and the workspace plus test suite compile with no unused
   imports.
10. **Nothing else moved.** `cargo test` green; `python3 scripts/check-guards.py` unchanged; no
   schema fingerprint, plan fingerprint or golden byte moves — this work item is a refactor and its
   proof is that every golden is untouched.

## Traps

- **The restructure must not change any error's identity.** The variants and messages are the
  contract layer for six error states; rewriting the match must keep each mismatch reporting the
  same variant. Site 6 is the exception worth taking deliberately: `BadRecord` is the wrong variant
  for a scalar mismatch and this is the moment to give it the right one — which means a new
  `StoreCodecError` variant and a test that provokes it.
- **`same_ty` fails closed, and must keep failing closed.** Its restructure must not turn a
  schema disagreement into a schema agreement.
- **Do not blanket-deny the lint.** 120 hand-written sites, 85 in `fjord-engine`; a blanket deny is
  a week of churn over matches on AST node kinds where a wildcard is correct.

## Not in scope

- `bytes` itself — W3.
- The C# side's own exhaustiveness — recorded above, fixed at R4c (the Glean writer) and W3 (the
  client codec, if `bytes` lands).
