# `Modern` — C# 10 through 13, on the default language version

Thirty-nine source files for the `post-standard` rows of `POPULATION.tsv` that name a C# 10,
11, 12 or 13 feature. The slice is the language the standard does not describe: ECMA-334
draft-v9 was written against C# 6, and every construct here postdates it, so no clause number
covers any of them. The row's `id` — `C# 11 — Required members` and the like — is the citation,
and every construct names its row in a comment.

`Modern` is the plain one of the three projects this slice needed. Its sibling `Preview`
carries the C# 14 rows, because the pinned compiler needs `LangVersion=preview` for them, and
`Entry` carries top-level statements, because they need an `OutputType` of `Exe`. Three
MSBuild property sets cannot coexist in one project, which is the whole reason for the split.
**`Modern` states no `LangVersion` on purpose**: everything here is accepted by the *default*
version for `net10.0`, and that is part of what the project asserts. Pinning a version would
turn a compiler regression into a fixture edit.

## What is where

| Directory | Rows |
|---|---|
| `GlobalUsings.cs` | global using directives — plain, alias and static, in a file that declares no type |
| `Records/` | record structs, `readonly record struct`, `record class`, sealed `ToString`, `with` on structs and anonymous types |
| `Deconstruction/` | assignment and declaration in one deconstruction |
| `Lambdas/` | lambda natural type, explicit return type, attributes on lambdas, default lambda parameters |
| `Attributes/` | `[AsyncMethodBuilder]` on a method, `[CallerArgumentExpression]`, generic attributes, `[Experimental]`, `[OverloadResolutionPriority]` |
| `Strings/` | constant interpolated strings, raw string literals, newlines in interpolation holes, UTF-8 literals, `\e`, interpolated string handlers |
| `Directives/` | the enhanced `#line` pragma, beside the whole-line form it extends |
| `Structs/` | parameterless struct constructors and field initializers, inline arrays, `ref` fields, `scoped`, `[UnscopedRef]`, `ref struct` interfaces, `allows ref struct` |
| `Patterns/` | extended property patterns, list and slice patterns, a span matched against a constant string |
| `Members/` | `required` members, primary constructors, `ref readonly` parameters, `params` collections, `ref` and `unsafe` in iterators and async methods |
| `Operators/` | `checked` user-defined operators, relaxed shift operands, `>>>` and `>>>=` |
| `Numerics/` | `static abstract` and `static virtual` interface members, generic math, `nint` and `nuint` |
| `Aliases/` | `using` aliases for a tuple, an array, a jagged array, a delegate, a pointer and a function pointer |
| `Collections/` | collection expressions, the spread element, `[CollectionBuilder]`, better conversion from a collection expression's element, an implicit index in an object initializer |
| `Names/` | extended `nameof` scope, and `nameof` reaching a member of an instance member |
| `MethodGroups/` | the C# 13 method group natural type improvements — arity, static-ness and the scope-by-scope half, which needs the second namespace in `InnerScopePruning.cs` |
| `Concurrency/` | `lock` over a `System.Threading.Lock`, beside `lock` over an object |
| `Analysis/` | the four rows that change no declaration: improved definite assignment, auto-default structs, cached method group conversions, warning wave 7 |
| `Interceptors/` | a call replaced at compile time by an attribute in another file |

## What the project file has to say, and why

- **`AllowUnsafeBlocks`** — a `using` alias for a pointer or a function pointer must be
  `using unsafe`, and C# 13's "ref and unsafe in iterators and async" is about an `unsafe`
  context inside a state machine. Neither row has a safe spelling that still exercises it.
- **`Nullable=enable`** — `required string` and `required string?` are the same declaration to
  a reader and to an index with nullability off, and the definite-assignment rows are about
  null-state analysis.
- **`InterceptorsPreviewNamespaces`** — interceptors are opted into per namespace by an
  MSBuild property, not by anything in source. Without it `[InterceptsLocation]` is an error
  and the row cannot be exercised at all.

## The properties this project is built to have

- **Every declaration has a use, in this project.** No `ProjectReference` and no
  `PackageReference` — CI has no network — so every reference is either into the framework or
  into a file beside it. Each file ends with a member that reads what the file declares, which
  is what makes a *missing* reference distinguishable from a construct nobody exercised.
- **Each row's construct sits beside the construct it replaced.** The pre-feature spelling is
  in the same file as the feature: nested property patterns beside the extended path, `in`
  beside `ref readonly`, a hand-written backing field beside a primary constructor parameter,
  `Monitor` beside `Lock`, a string literal beside a raw one. A query that cannot tell the two
  apart has found something, and a query that finds only one of them has found something else.
- **None of the five refused-write shapes appears here.** No type name at two arities, no type
  with two indexers, no partial member of any kind, no `file`-local type. Three rows in the
  slice can only be exercised by one of those shapes — partial properties and indexers,
  partial methods with signature differences (warning wave 6), and file-local types — and they
  are recorded in the census as `quarantined` rather than written.
- **Two constructs here are deliberately fragile, and say so.**
  `Interceptors/InterceptedCall.cs` is pointed at by a literal line and character offset, so
  inserting a line in it breaks the build with CS9141; `Directives/EnhancedLinePragma.cs`
  declares a method the compiler reports as living in a file that does not exist. Both are
  cases where an index's answer and the compiler's answer can differ without either being
  wrong, which is why the corpus has to contain them rather than describe them.
