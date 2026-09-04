# `Expressions.Primary` — the primary expressions of ECMA-334 clause 12

One project, twelve source files, covering the *primary expression* half of clause 12: member
access, invocation, element access, `this` and `base` access, the three forms of `new`, the type
operators (`typeof`, `sizeof`, `default`, `nameof`, `stackalloc`, `checked`/`unchecked`),
interpolated strings, tuple literals, deconstruction, `await`, and the null-conditional forms.
The operator, assignment, lambda and query clauses of the same census area belong to the
sibling project and are not written here.

## What is where

| File | Clauses |
|---|---|
| `PxDeclarations.cs` | the declared vocabulary everything else binds to — 12.6.1, 12.6.4.8, 12.7, 12.8.3, 12.8.10.3, 12.8.12.4, 12.8.15, 12.8.17.2.2, 12.8.17.2.3, 12.9.9.2, 12.9.9.4 |
| `PxSimpleNames.cs` | 12.2.1, 12.2.2, 12.3.1, 12.3.2, 12.5.1, 12.6.5, 12.8.4, 12.8.7.2 |
| `PxMemberAccess.cs` | 12.5.2, 12.8.7.1, 12.8.14, 12.8.15 |
| `PxInvocations.cs` | 12.6.1, 12.6.2.*, 12.6.3.*, 12.6.4.*, 12.6.6.*, 12.8.10.* |
| `PxElementAccess.cs` | 12.8.12.1, 12.8.12.2, 12.8.12.3, 12.8.12.4 |
| `PxCreation.cs` | 12.8.17.2.1, 12.8.17.2.2, 12.8.17.2.3, 12.8.17.3, 12.8.17.4, 12.8.17.5, 12.8.24 |
| `PxNullConditional.cs` | 12.8.8, 12.8.9.1, 12.8.11, 12.8.13 |
| `PxInterpolation.cs` | 12.8.3 |
| `PxTypeOperators.cs` | 12.8.18, 12.8.19, 12.8.20, 12.8.21, 12.8.22, 12.8.23 |
| `PxAwait.cs` | 12.9.9.2, 12.9.9.3, 12.9.9.4 |
| `PxDeconstruction.cs` | 12.7, 12.8.6 |
| `PxCrossFileUses.cs` | a second use site for every declaration above |

## The properties it is built to have

- **Every declaration is used twice: once beside itself, once from another file.** Each
  declaring type carries a `UsedHere` (or an `Every…Form`) member whose references are
  *same-file*, and `PxCrossFileUses.cs` reaches the same declarations from across a file
  boundary. A same-file reference and a cross-file reference take different paths through the
  indexer, so a query that finds one and not the other has found a defect rather than an
  unusual fixture.
- **Every binding whose target is not spelled at the use site is written at least once.** That
  is what this half of clause 12 is *for*: an element access reaches an indexer with no name
  node (`PxGrid`, `PxRenamedIndexer`, `PxSliceable`), `new` reaches a constructor whose name is
  not the type's (`PxCreated`), an interpolated string reaches `AppendLiteral` and
  `AppendFormatted` overloads nobody wrote (`PxLogHandler`, `PxConditionalHandler`), `await`
  reaches four members of an awaiter (`PxAwaiter`, `PxCriticalAwaiter`), a range element access
  reaches `Slice` (`PxSliceable`), a collection initializer reaches `Add` (`PxBasket`), a
  deconstruction reaches `Deconstruct` — including one supplied by an extension method
  (`PxDeconstructExtensions`) — and a `checked` context reaches a *different* operator
  declaration from the one an unchecked context reaches (`PxCounter`).
- **A member access on a constructed generic type is written both ways.** `PxBox<T>` is used at
  `PxBox<int>`, `PxBox<string>`, `PxBox<PxPoint>` and `PxBox<PxBox<string>>`; the substituted
  member's spelling must equal its declaration's, or the reference lands nowhere.
- **`nameof` binds a symbol and produces a string.** `PxTypeOperators.NameOf` names a local, a
  parameter, a type parameter, a type, a constructed type, a namespace, a field, a member, an
  *overloaded* method (a method group, not one method), an enum member and an extension member.
- **None of the five quarantined shapes appears.** No type name at two arities — `PxBox<T>` and
  `PxGenericOverloads<T>` have no non-generic namesakes; no type declares two indexers (twelve
  types declare exactly one each); nothing is `partial`; no type is `file`-local. The two
  shapes this area *would* otherwise reach for — an overloaded indexer (the indexer context of
  12.6.4.1) and a partial method beside an overload — are left to the quarantine projects, and
  12.6.4.1 is exercised through its method and constructor contexts instead.
- **The identity hazards of the area are written deliberately.** Two same-named locals in
  disjoint blocks of one method (`PxSimpleNames.TwiceDeclared`); a property whose name is its
  own type's name (`PxPalette`); `Lookup()` beside `Lookup<T>()`, same name, same parameter
  list, different arity; `Accept(T)` beside `Accept(int)` in a generic class, which unify at
  `PxGenericOverloads<int>`; an indexer renamed with `IndexerName` beside a property genuinely
  called `Item`; and one anonymous type created at three sites in two files, which the compiler
  emits once.

Nothing in here is edited to make a query pass. It compiles with `dotnet build` against the
framework reference alone — no `PackageReference`, no `ProjectReference` — and it warns, because
a corpus of odd code warns.
