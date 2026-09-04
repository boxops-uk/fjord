# `Ranges` — clauses 17 and 18 of the surface corpus

Arrays, and the extended indexing and slicing that grew on top of them. The slice is every
`POPULATION.tsv` row whose `area` is `ecma-17` (11 rows) or `ecma-18` (8 rows) — 19 rows, of
which 17 are marked `hazard`.

| File | Clauses |
|---|---|
| `ArrGeneral.cs` | 17, 17.1 — element type, rank, emptiness, jagged versus rectangular |
| `ArrTypeGrammar.cs` | 17.2, 17.2.1 — the array-type grammar, as the standard's own ten-declaration table |
| `ArrSystemArray.cs` | 17.2.2, 17.5 — `System.Array` as the base type, and the members every array inherits |
| `ArrCollectionInterfaces.cs` | 17.2.3 — `IList<T>`, `IReadOnlyList<T>` and their base interfaces |
| `ArrCreation.cs` | 17.3 — every array creation expression, and defaults |
| `ArrElementAccess.cs` | 17.4 — every index type and rank, the variable-reference forms, and the `Index`/`Range` forms |
| `ArrCovariance.cs` | 17.6 — array covariance and the run-time check it forces |
| `ArrInitializers.cs` | 17.7 — array initializers at every rank and in all three contexts |
| `ArrParams.cs` | 17.3's third route — parameter arrays, and the creation with no syntax |
| `RngExtendedIndexing.cs` | 18 — one countable, indexable, sliceable type and the accesses that reach all three members |
| `RngGeneral.cs` | 18.1 — the A/B/C inheritance chain, the `Count`/`Length` tie-break, a protected countable property |
| `RngIndexType.cs` | 18.2 — `System.Index`, and the indexer the clause requires be declarable |
| `RngRangeType.cs` | 18.3 — `System.Range`, all seven written forms of `..`, and the tuple it returns |
| `RngPatternBased.cs` | 18.4 — one declaration set conforming to both patterns, through `Count` |
| `RngFallthrough.cs` | 18.4.1 — the resolution order: array, string, indexer, then the pattern |
| `RngImplicitIndex.cs` | 18.4.2 — countable plus an `int` indexer, with both accessors |
| `RngImplicitRange.cs` | 18.4.3 — countable plus `Slice(int, int)`, and the overload set that moves its ordinal |

Every construct names the clause it comes from, in the file header or in its doc comment, so a
reader can walk from a census row to the code without a map.

## What was measured rather than reasoned about

The claims in the file headers are Roslyn's answers, not recollections. A probe compiled these
17 files in memory against the `net10.0` reference pack (0 errors) and ran the repository's own
`ScipSymbols` over the result — the real file, copied, not a re-implementation. It reported:

- **Every array element access has a null symbol.** `GetSymbolInfo` returns nothing for
  `Row[0]`, `Row[^1]` and `Row[1..^1]` alike; the operation is an `ArrayElementReference`. So
  the arms of 18.4.1 that go through an array cannot be written as a reference at all, and a
  producer that treats a null symbol as unresolved will count legal code as a failure.
- **`..` binds to four different declarations, chosen by which operand is absent.** Confirmed
  in `RngRangeType.Written()`, one expression: `0..4` and `^2..^0` to `Range..ctor(Index, Index)`,
  `..4` to `Range.EndAt`, `1..` and `^2..` to `Range.StartAt`, and bare `..` to `Range.All.get`
  — a property getter referenced by a use site with no identifier tokens in it.
- **Every `^n` is a constructor reference.** `System.Index..ctor(int, bool)`, at 39 sites; and
  every bare integer inside a range or index expression is a *user-defined conversion*,
  `System.Index.op_Implicit(int)`, bound at the literal.
- **The countable property is not in symbol info.** For `Sequence[^1]` the symbol is the
  indexer and nothing else; `RngSeq.Length` appears only as
  `IImplicitIndexerReferenceOperation.LengthSymbol`. `Length` wins over `Count` where both
  exist (`RngBothCounts`), a `Count`-only type is still countable (`RngCountedSequence`), and
  for the inheritance chain the property found is `RngCountableBase.Length` while the indexer
  found is `RngIndexableMiddle.this[int]` — one use site, two target types, neither of them
  the type named at the use site.
- **The pattern's `Slice` carries an ordinal that its use site cannot re-derive.**
  `RngSliceOverloads.Slice(int, int)` mints `Slice(+1).`, because
  `M:….Slice(System.Int32)` sorts before it; the only expression that binds it,
  `Overloaded[1..3]`, contains neither the name nor the arity.
- **Core-library targets are identified by a package coordinate that is not stable.**
  `Row.Length` is `System.Array.Length` in `System.Runtime 10.0.0.0` under this reference pack;
  the same source resolves it to `System.Private.CoreLib` when compiled against the runtime
  assemblies. No declaration in this corpus matches either string.
- **One tuple field, two spellings.** `range.GetOffsetAndLength(n).Offset` mints
  `System/ValueTuple#Offset.` and the same field read off an unnamed `(int, int)` mints
  `System/ValueTuple#Item1.`; both arities of `ValueTuple` collapse to one `ValueTuple#`
  descriptor. Metadata-only, so it merges silently instead of refusing a write — which is
  precisely why it belongs here.
- **`string` is the other silent arm.** `Text[^1]` is `string.this[int]` — the collapsing
  `this[]` descriptor, for a type nobody here declared — and `Text[1..^1]` is
  `System/String#Substring(+1).`, a name no character of the file contains.

## Properties this project is built to have

All four were checked mechanically over the compiled project, not by inspection:

- **No two declarations mint one identity string.** 295 declarations walked the way
  `Indexer.IndexTree` walks them, 294 distinct `ScipSymbols.Of` strings, **0 collisions** — the
  295th is the local function `Longest`, which is given no global name by design. This project
  cannot cause a refused write.
- **One type name at one arity.** 39 source types, no name declared at two arities.
- **One indexer per type.** Eleven types declare an indexer and each declares exactly one:
  `ArrParamsIndexed`, `RngSeq`, `RngIndexed`, `RngRanged`, `RngRangePrecedence`,
  `RngIndexableMiddle`, `RngBothCounts`, `RngHiddenCount`, `RngSequence`,
  `RngCountedSequence`, `RngPatternBoth`.
- **No `partial` anything, and no file-local type.** Zero of each; the word `partial` does not
  appear.

## What is deliberately absent, and where it went

Clause 18's sharpest hazard is that **every indexer in a type mints one identity string**: a
property descriptor is `Name(symbol) + '.'` with no disambiguator, and Roslyn's name for every
C# indexer is the literal `this[]`. Probed on the shape the clause invites — one type
declaring `this[int]`, `this[Index]` and `this[Range]` — that is three declarations and
**one** identity string, which is a refused write and the end of an indexing run. It is legal
C# (the probe compiled it with 0 errors), so it is quarantined rather than unwritable, and it
lives in a quarantine project indexed alone. Three things follow for the rows in this slice:

- 18.2 and 18.3 each need a declared indexer, so they get one **each, in their own type** —
  `RngIndexed.this[Index]` and `RngRanged.this[Range]`.
- 18.4.1's fallthrough order needs an indexer to *beat* a conforming pattern. On the index
  side that requires `this[Index]` beside `this[int]` in one type, which is the collision. On
  the range side it does not: `RngRangePrecedence` declares `this[Range]` while also being
  countable and sliceable, and `Precedence[1..3]` binds the indexer — verified — with `Slice`
  left live and unreachable from any element access. The rule is exercised; only the
  index-side spelling of it is quarantined.
- 18.4.2's own precondition, "`E[0]` is valid and uses the same indexer", is about two indexers
  competing at one span. Not written here, for the same reason.

Two more shapes are named in the file headers and written nowhere, because they do not compile:
a `params int[]` overload beside a plain `int[]` one is CS0111, and `RngHiddenCount`'s
`this[^1]` from outside the type is CS1503 — which is the point of that type, since it makes
"is a sequence" a property of a use site rather than of a declaration.
