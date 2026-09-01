# W1 · `unify` grows a `Ty::Union` arm

| | |
|---|---|
| **Issue** | [#37](https://github.com/boxops-uk/fjord/issues/37) — `[01]` in the issue batch |
| **Area** | `fjord-engine` (`ty.rs`, `corpus.rs`) |
| **Depends on** | nothing |
| **Blocks** | W9 (`csharp.EntityXRef`/`EntityRef`, `SymbolOf`/`DefinitionBySymbol`) and R4's descriptor decision. **Not W8** — `codemarkup` keys on a string and needs no arm |
| **Invariants** | none moved. I10 is *read* by the new arm (discriminants are the identity), not touched |
| **Fingerprint** | unmoved — no schema, no plan, no cursor change |
| **Format** | untouched |

## Claim

A variable bound to a union-typed field can be shared by two generators: the query plans, runs,
and answers what the alternative-narrowed form answers. Two unions that genuinely differ are
still rejected, and the diagnostic names the alternative that differs instead of printing one
type twice.

## The defect, confirmed

`Ty` is `crates/fjord-engine/src/syntax.rs:93-113` — seven variants, `Union(Arc<[(Symbol, u32, Ty)]>)`
among them, carrying *(name, discriminant, payload)* in **schema declaration order**.

`Checker::unify` is `ty.rs:822-877`. After the `Var`-chain strip and the `Ty::Error` poison check
(`:839-847`) it matches on the pair, and the arms are `Var`/`Var`, `Var`/anything, `Int`/`Int` and
`String`/`String`, `Fact`/`Fact`, `Record`/`Record` — and then, at `ty.rs:875`:

```rust
(got, expected) => Err(TyError::Mismatch { expected, got }),
```

There is no `Union`/`Union` arm, so **two structurally identical unions never compare equal**. The
first mention of a variable takes the `Var` arm and binds it; the second reaches the catch-all, and
the error is built from the two sides it just failed to compare — which are equal, hence the same
type printed twice.

The asymmetry is real and local: `zonk` (`ty.rs:925-951`), `occurs` (`:953-960`), `Checker::render`
(`:1106-1143`) and `schema_ty` (`:1147-1164`) are all exhaustive over `Ty` with an explicit `Union`
arm — the compiler required it, because none of them has a catch-all. `unify`'s catch-all is why
nothing asked.

## The work

**1 · The arm.** Compare **by discriminant**, never by position. Union alternatives are held in
declaration order (`fjord_schema::schema::PredicateTyNamed::Union`, `schema.rs:70-80`: *"Held in
declaration order … permuting the declaration changes no stored byte"*), and only the fingerprint's
canonical form sorts them (`fingerprint.rs:345-360`). So a zip is wrong, and so is the `Record`
arm's model (`ty.rs:856-873`), which finds by **name** alone:

```rust
let Some((_, y)) = ys.iter().find(|(n, _)| n == name) else { … };
```

The union arm must match on the discriminant *and* check the name, because the canonical form
carries both (`fingerprint.rs:355-361` writes `name:type=disc`), so two unions differing only in an
alternative's spelling are different types on disk and must be different types here.

Required behaviour, in one sentence: **equal iff the alternative sets are equal as sets of
`(name, discriminant, payload)`, compared in both directions.** Arity, a renamed alternative, a
renumbered discriminant and a changed payload are each a mismatch; a permuted *declaration order* is
not.

**2 · The diagnostic.** Add `TyError::UnionMismatch { alternative, expected, got }` beside
`Mismatch` (`ty.rs:84-88`) and report it through the existing `Code::RejectTypeMismatch`
(`Checker::report`, `ty.rs:1054-1081`) — **not** a new code, so the corpus's
`every_code_is_accounted_for` module needs no new excuse and the taxonomy does not grow for a
message improvement. The message names the first alternative on which the two sides disagree, in
discriminant order, and says how: missing on one side, a different name at that discriminant, or a
payload that does not unify.

**3 · Nothing else.** `Checker::compare` (`ty.rs:179-210`) is unrelated — it runs *after* `unify`
has succeeded and decides whether a type may be ordered; a union may not, and that stays.

## Acceptance criteria

Each is a named test that fails before the change and passes after.

1. **The shared variable plans and runs.** A new `fjord_engine::corpus` entry:

   ```rust
   entry(
       "X where test.Tagged {what = W, id = X}; test.Label {id = _, what = W}",
       Supported(<the four ids, in the order the runner reports>),
       "a **union-typed variable shared by two generators** — the first mention binds a \
        variable to a union, the second asks unify to compare a union with a union",
   )
   ```

   The fixture already carries what this needs and **no fixture change is required**:
   `fjord_store::fixture` declares the same two-alternative union twice, in two separately
   allocated `Arc`s — `test.Tagged : { what : union, id : int }` and
   `test.Label : { id : int, what : union }`, `union = { num : int = 3 | text : string = 0 }`
   (`crates/fjord-store/src/fixture.rs:27-30`, built by `tagged()` at `:259-273` and called at
   `:230` and `:241`). Both predicates hold the same four values against ids 10/20/30/40
   (`fixture.rs:385-405`), so the join is non-empty and one-to-one.

   **The claim is proved, not predicted.** A throwaway spike — the arm below, plus a scratch test
   over `MemStore` seeded from `fixture::facts()` — was run against this tree and then reverted:

   ```
   X where test.Tagged {what = W, id = X}; test.Label {id = _, what = W}
       rows  = [20, 40, 10, 30]        diagnostics = []
   X where test.Tagged {what = W, id = X}; test.Label {id = 10, what = W}
       rows  = [10]                    diagnostics = []
   X where test.Tagged {what = {num = X}, id = _}
       rows  = [1, 2]                  diagnostics = []      (unchanged)
   ```

   So: **the arm alone is sufficient — `flatten` needs no work.** A union-typed register splices
   into a key position as `SeekKeyPart::RegisterField` like any other field
   (`flatten.rs:3353-3376`), and both sides encode a union identically, so the bytes match. And
   `cargo test -p fjord-engine` was **496 passed, 0 failed** with the arm in place, so no existing
   test depends on union/union failing to unify.

   The row order `20; 40; 10; 30` is the scan order of `test.Tagged`, whose leading key field is the
   union: `text` is discriminant 0 and `num` is 3, so the two `text` rows come first. Take the
   string from the gate anyway — if it differs, the plan changed and that is worth knowing.

2. **Every corpus gate stays green**, including the two that a new `Supported` entry moves:
   `every_supported_entrys_plan_fingerprint_is_stable` (`corpus.rs:1339`) holds a *positional*
   `Vec` of hashes — it is regenerated, not edited by hand — and
   `every_supported_entry_returns_its_rows` (`:1525`) / `every_supported_entry_resumes_to_the_same_rows`
   (`:1696`) iterate generically. `cargo test -p fjord-engine` green.

3. **The narrowed form is unregressed.** A second corpus entry pins the shape consumers use today
   — the join through one alternative (`{what = {num = X}, …}`) — classified `Supported`. This is
   the eleven-query C# workaround's building block; W8 and W9 assume it keeps working.

4. **A permuted declaration order still unifies.** Unit test in `ty.rs`,
   `a_union_unifies_with_the_same_alternatives_declared_in_another_order`: build
   `{num : int = 3 | text : string = 0}` and `{text : string = 0 | num : int = 3}` as two `Ty::Union`
   values and assert `unify` returns `Ok`. This is the criterion that rules out a zip.

5. **Four negatives still reject.** Table-driven unit test
   `a_union_does_not_unify_with_a_union_that_differs`, one case each: a missing alternative
   (arity), a renamed alternative at the same discriminant, a renumbered discriminant, and a
   changed payload type. Each must be `Err`, and each must report `reject/type-mismatch`.

6. **No diagnostic prints one type twice.** Unit test
   `a_union_mismatch_names_the_alternative_that_differs`: for each case in (5), the rendered
   message names the offending alternative, and the two rendered type strings are not equal to
   each other. (Before this work the message is literally *"expected {…}, found {…}"* with the
   two identical.)

7. **Nested and recursive shapes terminate.** Unit test
   `a_union_inside_a_record_inside_a_union_unifies`: a union whose payload is a record whose field
   is a union unifies with an independently built equal copy; and a union of `Ty::Fact` payloads
   compares by `PredicateId` without descending into the predicate. A fact-typed payload is the
   only cycle-breaker the type model has, so this is what keeps the arm total.

8. **The clean-build gates.** `cargo +1.97.1 clippy --all-targets --workspace -- -D warnings` and
   `cargo +1.97.1 fmt --all` clean; `python3 scripts/check-guards.py` unchanged (this work adds no
   ignored guard).

## Traps

- **The `Var` arm runs first and must keep running first.** `(Ty::Var(var), ty) | (ty, Ty::Var(var))`
  is symmetric; a union arm placed above it would capture a bound variable's union before the
  binding is resolved.
- **`Ty::Error` is poison and is already handled at `:839-847`.** A union carrying an `Error`
  payload must not be reported as a union mismatch — that would report the same defect twice and
  break `checking_keeps_going_after_an_error` (`ty.rs:1319`).
- **Do not "simplify" the alternatives into a `HashMap` to compare them.** Record fields are
  ordered slices everywhere by codec requirement (AGENTS.md), and union alternatives are the same
  data; the comparison is a small-N scan and belongs as one.

## Not in scope

- Making `unify` exhaustive over `Ty` so that a *new variant* is a compile error — that is **W2**,
  and it is what stops the next instance of this defect rather than this one.
- Anything about a union in a *key* being the right modelling choice; W8 argues the opposite for
  `codemarkup`, and both positions survive this arm landing.
