# W4 · One resolution algorithm, two source providers — a schema that spans files, embedded

| | |
|---|---|
| **Issue** | [#41](https://github.com/boxops-uk/fjord/issues/41) — `[06]` |
| **Area** | `fjord-schema` (`syntax::resolve`), `fjord-cli` (`sample_schema.rs`), `fjord-db`, `wasm/` |
| **Depends on** | nothing |
| **Blocks** | **W6, W7, W8, W9** as *files* (each can otherwise be declared inside `code.sigla` at identical fingerprints), W5's multi-source corpus, and the browser's ability to open any schema with an `import` |
| **Invariants** | none. I13 (the schema is embedded and frozen at create) is unaffected — this changes how a schema is *read*, never what is stored |
| **Fingerprint** | unmoved — proven, twice, below |
| **Format** | untouched |

## Claim

A schema that spans files can be resolved **without a filesystem**, by the same algorithm that
resolves one from disk; the CLI's fixture reader and the published `fjord-db` example both follow
imports; and a browser build can open a schema with an `import` in it.

## The defect, confirmed

Schema imports work. What does not work is *embedding* a schema that uses them.

- `crates/fjord-cli/src/sample_schema.rs:53` is `include_str!("../../../schemas/code.sigla")`, and
  `parse_or_panic` (`:132-149`) calls `syntax::parse::parse` then `syntax::lower::lower` with
  `assert!(diags.is_empty())`. It never calls `resolve`.
- The published `fjord-db` front-page example (`crates/fjord-db/src/lib.rs:56-63`, also in the
  crate's `README.md`) calls `read_schema` → `syntax::read`, which lowers **one block of source**.
  Its own doc says so at `:111-113`: *"Imports are not followed here … resolved by
  `fjord_schema::syntax::resolve`, which needs to know the directories to search and is therefore
  the caller's decision."*
- `syntax::resolve::resolve(entry: &Path, roots: &[PathBuf])` (`resolve.rs:64`) is the only entry
  point that follows imports, and it reads the filesystem directly: `fs::canonicalize` (`:85`, the
  dedup identity), `fs::read_to_string` (`:94`), `Path::is_file` inside `find` (`:182-189`). An
  import name maps to a path by `relative()` (`:172-179`) — `lang.rust` → `lang/rust.sigla`,
  `EXTENSION = "sigla"` (`:38`) — searching the entry file's own directory first, then the roots.
- `wasm/src/lib.rs:14-18` already states the consequence as a known absence: *"**schema `import`**,
  because resolution reads files — so a browser schema is single-file until a virtual resolver
  exists."*

**And "just declare it in both files" is not an escape.** `declare()` (`lower.rs:358-373`) checks
presence by name only:

```rust
if seen.contains_key(qualified) {
    diags.push(Code::RejectRedeclaration.at(span, format!("`{qualified}` is already declared in this schema")));
```

so two *byte-identical* declarations of one fully-qualified name in two files reject. Reproduced
against the real binary:

```
$ fjord schema check ./entry.sigla --schema-path .
fjord: error[reject/redeclaration]: `dup.File` is already declared in this schema
```

## What the split costs: nothing, measured twice

Both claims were reproduced in-tree with `target/debug/fjord`, on the exact sources from issue #39's
appendix C:

```
$ fjord schema check ./a.sigla                      # `type Bool` declared locally
fingerprint 0x53a9857a412b2e7b
$ fjord schema check ./b.sigla --schema-path .      # `import base; base.Bool`
fingerprint 0x53a9857a412b2e7b
$ fjord schema diff ./a.sigla ./b.sigla --schema-path .
Identical

$ fjord schema check ./one.sigla                    # predicate in the entry file
fingerprint 0xb5761dc1251dc713
$ fjord schema check ./p2.sigla --schema-path .     # …moved to an imported file
fingerprint 0xb5761dc1251dc713
```

The mechanism is in the tree, not in luck: an alias is *expanded* at lowering (`lower.rs:36`), so
its name never reaches `PredicateTy`; and `Predicate` (`schema.rs:94-99`) carries no file, so
`predicate_form`/`type_form` (`fingerprint.rs:292-364`) cannot read one.

## The work

**1 · Factor resolution over a source provider.** One algorithm, two providers — never two
algorithms:

```rust
pub trait SchemaSources {
    /// The source for an import name, and the name to attribute diagnostics to.
    fn find(&self, import: &str) -> Option<(String, String)>;
}

pub fn resolve_with(entry: (&str, &str), sources: &impl SchemaSources) -> Result<Resolved, String>;

/// The convenience an embedder wants: an ordered list, the entry first.
pub fn resolve_from<'a>(sources: impl IntoIterator<Item = (&'a str, &'a str)>) -> Result<Resolved, String>;
```

`resolve(entry: &Path, roots: &[PathBuf])` keeps its signature and becomes `resolve_with` over an
`FsSources { roots }`. The dedup identity differs per provider and must be stated: `canonicalize`
for the filesystem, the import name for an embedded set.

**2 · Put the filesystem provider behind a default-on cargo feature, `fs`.** This is what makes
"the embedded path touches no filesystem" **mechanical** rather than a promise: with
`--no-default-features` the provider is not compiled, so any call from the embedded path is a
compile error. It is also what the browser build needs.

**3 · Convert both readers.** `sample_schema.rs` gains a `const SOURCES: &[(&str, &str)]` of
`include_str!`s and asserts over the **resolved** diagnostics; the `fjord-db` example and its
`README.md` show `resolve_from` with two `include_str!`s, and the doc paragraph at `lib.rs:111-113`
is rewritten — it currently tells a reader that following imports *requires* directories, which
after this is false.

**4 · Delete the wasm caveat.** `wasm/src/lib.rs:14-18`'s "schema `import`" absence goes, and the
sentence that replaces it says what a browser embedder does instead.

## Acceptance criteria

1. **One algorithm, proven by a differential.** `resolving_from_memory_matches_resolving_from_disk`
   — for each multi-file case in the schema corpus (W5), resolve it twice, once through `FsSources`
   over a temp directory and once through `resolve_from`, and assert **equal predicate ids, equal
   names, equal per-predicate fingerprints, equal schema fingerprint, and equal diagnostics in the
   same order**. This is the criterion that stops the two paths drifting; a shared implementation is
   the means, and this test is the evidence.
2. **The embedded path compiles with no filesystem.**
   `cargo check -p fjord-schema --no-default-features --target wasm32-unknown-unknown` is added to
   the build list in `AGENTS.md` and to the `test` job. It must fail if `resolve_from` ever reaches
   `FsSources`.
3. **The CLI fixture follows imports.** `sample_schema.rs` calls the resolving path;
   `the_schema_declares_what_the_tree_names` (`sample_schema.rs:165`) and
   `every_record_lists_its_fields_in_the_intended_order` (`:291`) stay green; the
   `assert!(diags.is_empty())` is over resolved diagnostics. Proven non-vacuous by criterion 4.
4. **A two-file schema is the fixture, not a one-file one.** `schemas/` gains the split that W6
   lands (or, if W6 is not yet in, a two-file fixture under `crates/fjord-cli/tests/`), and
   `sample_schema.rs` reads it. A resolving reader that is only ever handed one file has not been
   tested.
5. **The published example is executed.** The `fjord-db` doctest runs under
   `cargo test -p fjord-db` and the crate's rustdoc still builds with `-D warnings` (it is a
   published crate; AGENTS.md).
6. **A browser can resolve an import.** `wasm/`'s exported schema entry point accepts a multi-file
   schema, exercised by a test in `crates/fjord-inspect` (which owns the embedded demo schema,
   `demo.rs:38`) and by `(cd web && npm run smoke)` if a demo page uses one. At minimum: the caveat
   in `wasm/src/lib.rs` is deleted **and** a test proves the capability it claimed was missing.
7. **No fingerprint moves anywhere.** `fjord schema fingerprint schemas/code.sigla` is unchanged by
   this work item; `cargo test -p fjord-client byte_identical` green without regenerating goldens.
8. **The full gate**: `cargo test`, clippy/fmt on 1.97.1, `check-guards.py`,
   `website/build.py --strict`.

## Traps

- **Resolution order is part of the contract.** `resolve` searches the entry file's own directory
  first, then the roots, first match wins (`resolve.rs:9-11`, `:67-73`). An embedded provider has no
  directories, so its order is the list's order — which must be documented, because it decides which
  of two files claiming one import name is used.
- **Diagnostics carry a source name.** They are attributed to `<resolved schema: ./x.sigla and N
  more>` today; an embedded set has no paths, so the attribution names must come from the provider
  or the errors become unreadable.
- **Do not make `resolve_from` a second lowering path.** `Numbering::Assigned` vs
  `Numbering::Declared` (`lower.rs:106`, `:155`) is a real fork in id assignment and only `resolve`'s
  path may assign; an embedded resolve is the *same* path with a different reader.

## Not in scope

- The namespace/filename mismatch diagnostic and the redeclaration documentation fix — **W5**, which
  is where the new multi-source corpus (unlocked by this item) gives them coverage.
