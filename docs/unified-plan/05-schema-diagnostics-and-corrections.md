# W5 · The multi-file schema's diagnostics, and three corrections the batch owes

| | |
|---|---|
| **Issue** | [#41](https://github.com/boxops-uk/fjord/issues/41) items 3–4 and its appendix — `[06]` |
| **Area** | `fjord-schema` (`syntax::diag`, `syntax::corpus`, `syntax::resolve`), `website/content/` |
| **Depends on** | **W4** — a multi-source corpus is what gives a cross-file diagnostic its coverage |
| **Blocks** | nothing, but every later schema file benefits from landing it early |
| **Invariants** | none |
| **Fingerprint** | unmoved |

## Claim

A schema whose file name does not match the namespace it declares is diagnosed **by name**; the
schema corpus can state a multi-file case, so every cross-file diagnostic is covered by the same
gate every single-file one is; and three statements in the book that are wrong about the tree are
corrected.

## The three corrections

**1 · Identical redeclaration across two files rejects, and the book says it does not.**
`website/content/schema-language.md:227-228`:

> - **The real error is genuine redeclaration** — two *different* definitions of one
>   fully-qualified name, as opposed to the same file reached twice.

`declare()` (`lower.rs:358-373`) checks `seen.contains_key(qualified)` and nothing else, so two
*identical* definitions in two different files reject too — reproduced against the binary in W4.
The behaviour looks right (a namespace split across files has exactly one declaration site per
predicate, which is what makes an import worth having); the sentence is what is wrong. Rewrite it to
say: **the same file reached twice is deduped by file identity; one name declared twice is a
rejection whether or not the two agree.**

**2 · A namespace/filename mismatch is silent, and costs a confused ten minutes.**
`resolve::find` (`resolve.rs:182-189`) locates a file from the **import name text** alone and never
inspects the `schema <name> { … }` head it finds there; the namespace comes only from
`lower.rs:120`. So `oa.sigla` importing `ob`, where `ob.sigla` declares `schema base`, resolves two
files and then fails `reject/unknown-name` on every reference — reporting the symptom at every use
site instead of the cause at the import.

**3 · Predicate ids are assigned by sorted qualified name, not by file position.** Issue #41's
appendix reports the numbering as positional with *"the entry file's own predicates first"*, and its
worked example (`app.A1, app.A2, base2.B1, base2.B2`) is consistent with that reading only because
`app` sorts before `base2`. The tree says otherwise — `lower.rs:155-171`, under
`Numbering::Assigned`:

```rust
predicates.sort_by(|a, b| {
    let key = |d: &Declared| (d.qualified.starts_with(RESERVED_NAMESPACE), d.qualified.clone());
    key(a).cmp(&key(b))
});
```

**Sorted by fully-qualified name, with the reserved `fjord.*` namespace last, then enumerated.**
The consequence is sharper than the issue's version and matters to W6: adding `src.FileDigest` to
`code.sigla` does not append an id — it **inserts** one between `src.File` and `src.FileXRef` and
renumbers every `src.*` predicate above it, *in databases created after the change*. Existing
databases are untouched: the map is assigned at create, embedded, and append-only for life
(`PLAN.md:3221-3226`).

That is safe on the wire — a block header carries the predicate **name**
(`clients/dotnet/Boxops.Fjord.Client/Blocks.cs:12`, and `WireFact.predicate` is *"carried for the
caller's benefit and is **not** encoded"*, `fjord-wire/src/value.rs:115-119`) — and it is safe for a
client's own tables, which index that client's own schema statement. The one place it is **not**
free is a `FactId`: its high three bytes are the *database's* predicate tag
(`fjord-schema/src/id.rs:34-57`), so a consumer that decodes a returned reference's tag against a
hardcoded table reads the wrong predicate the day the numbering moves. That is the hazard worth one
paragraph in the wire-protocol page, and it is a *consumer* hazard, not a format one.

## The work

1. A new diagnostic `Code::RejectNamespaceMismatch` (`reject/namespace-mismatch`), reported by
   `resolve` when a file reached through `import <name>` declares a namespace other than `<name>`.
   Message names both: *"`ob.sigla` declares no namespace `ob` — it declares `base`"*.
2. Extend `syntax::corpus`'s entry type to carry **additional sources**, so a case can be stated as
   an entry file plus imports. Today `entry(name, source, verdict)` holds one string
   (`corpus.rs:74`, `:191`), which is why no cross-file behaviour is in the corpus at all.
   `resolve_from` (W4) is what makes this cheap: the corpus needs no temp directories.
3. Corpus entries for the cross-file cases that have none: identical redeclaration across two files,
   a genuinely different redeclaration, a namespace/filename mismatch, an import that resolves to
   nothing, an import cycle, and the two fingerprint-identical splits from W4's evidence.
4. Rewrite `schema-language.md:227-228`, and add the id-assignment paragraph to the wire-protocol
   page.

## Acceptance criteria

1. **The mismatch is diagnosed by name.** A corpus entry classified
   `Diagnosed(Code::RejectNamespaceMismatch)`, and `every_entry_is_classified_as_the_table_says`
   green.
2. **The new code is reachable.** `every_code_is_reachable_from_the_corpus` (`corpus.rs`) asserts
   every `Code::ALL` variant has a `Diagnosed` entry — so the new code **cannot** be added without
   its case, and it must not be added to an excuse list instead. This is the criterion that makes
   item 2 of the work mandatory rather than convenient.
3. **The corpus can state a multi-file case**, and at least six entries do. Both existing gates
   (`every_entry_parses_as_classified`, `every_entry_is_classified_as_the_table_says`) run over them
   unchanged in shape.
4. **Identical cross-file redeclaration is pinned as a corpus entry**, so the behaviour the book now
   describes is the behaviour a test asserts.
5. **Id assignment has a test.** `predicate_ids_are_assigned_by_sorted_qualified_name` — a two-file
   schema whose *file* order and *sorted* order differ, asserting the ids follow the sort and that
   `fjord.*` sorts last. Today the rule is a `sort_by` with no test naming it, and W6 is about to
   rely on it.
6. **The book is correct.** `schema-language.md`'s redeclaration sentence rewritten; the
   wire-protocol page carries the id-assignment paragraph and the `FactId`-tag consumer hazard;
   `python3 website/build.py --strict` clean.
7. `cargo test`, clippy/fmt on 1.97.1, `check-guards.py`.

## Not in scope

- Changing how ids are assigned. The sorted rule is settled (`PLAN.md`, *Predicate ids belong to the
  database, not the schema text*) and this item documents it, tests it, and says who it can bite.
