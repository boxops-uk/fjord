# `Locals` — what a method body introduces, and the identities that keep none of it apart

Six source files for five mechanisms of the corpus plan: **M11** (a lambda parameter's
`csharp.Parameter` key has no container), **M12** (a local function's key is a display
string, which is `lambda expression` inside a lambda), **M13** (`this` and `@this` are both
named `this`), **M16** (`csharp.Local`'s key has no scope segment, latently), and **M23** (a
query range variable resolves in neither direction).

It is a **hazard project that must index to completion**. Everything here merges or writes
nothing; nothing conflicts. The reason is one fact about the producer: only `Declare` calls
`Markup`, and `Markup` writes the only two predicates with a value side
(`codemarkup.Definition` and `codemarkup.SymbolInfo`) — and it is not reached at all for a
local function, whose `ScipSymbols.Of` is null. So the fusions below land in all-key
predicates and dedupe in silence. A measured run encodes 2,604 facts, resolves 152 references and finishes.

## What is where

| File | Mechanism | What it holds |
|---|---|---|
| `Parameters/LocalsParameterFusion.cs` | M11 | Six parameters named `item` — one on a method, one on a lambda, one on a local function, then one each differing by type, `refKind`, `isOptional` and `isParams`. The key is exhibited by what does and does not separate. |
| `Parameters/LocalsThisSpelling.cs` | M13, M11 | Three parameters Roslyn names `this`: an extension receiver `@this`, an ordinary `@this`, and a lambda's `@this`. Plus the `this` keyword, which is in no row at all. |
| `Functions/LocalsFunctionKeys.cs` | M12 | Seven local functions named `Pick`: two in two methods (two keys, the gated case), two in two lambdas (one key), two in two sibling blocks of one method (one key), and one whose return type separates it. |
| `Scopes/LocalsShadowedScopes.cs` | M16 | Eleven locals reaching eight of the declared key's values, with the span-keyed layer beside them getting the same locals right. |
| `Query/LocalsQuerySource.cs` | M23 | The query pattern — `Select` and `Where` — declared in this project so the anti-join has a definition row to miss. |
| `Query/LocalsRangeVariables.cs` | M23 | Two query expressions and the hand-written translation of one of them, as a matched pair. |

## The measurements

Taken with `Boxops.Fjord.Indexer --dry-run`.

- **`csharp.Local` is 0.** `CsharpEntities.Build` has no `ILocalSymbol` arm, so a local has
  no entity, and `PredicateCensusTests` records the predicate as `Fill.Excused`. M16 is
  therefore *latent*: the corpus pins the count the key would collapse, so that filling the
  predicate is a decision rather than a regression.
- **`Unresolved` is 0, and `Inexpressible*` are both 0.** Every name here binds and every
  declaration is expressible — including the anonymous type a `let` clause's translation
  introduces.
- **`csharp.DefinitionLocation` is 47 and `codemarkup.Definition` is 39.** The gap of 8 is
  exactly the eight local functions: no SCIP symbol, so `Markup` is never reached, so no
  `Definition`, no `SymbolInfo`, no `SearchEntry` and no `SymbolByName`. Every local
  function in this project is invisible to the language-independent surface, which is *also*
  why M12's key fusion cannot kill the run.
- **`csharp.Parameter` emits 20 facts.** That is the number of distinct parameter *symbols*
  the walk reaches, since `CsharpEntities.Entity` memoises per symbol and emits once. After
  the store dedupes identical facts under identical keys the predicate should hold **13
  rows** — the fusions are `item:int` (5 declarations → 1), `this:string` with
  `isThis = false` (2 → 1) and `source:LocalsQuerySource<int>` (3 → 1). A run that reports
  20 has put a container in the key; a run that reports fewer than 13 has lost one of the
  separating fields.

## Properties this project is built to have

- **Every fusion is paired with a separation.** `item:long`, `ref int item`,
  `int item = 6` and `params int[] item` are in `LocalsParameterFusion.cs` so that the key
  is stated by exhibition; `long Pick()` is in `LocalsFunctionKeys.cs` for the same reason.
  A fixture that only fused would not say which field a fix must add.
- **The right layer is beside the wrong one, on the same source.**
  `Scopes/LocalsShadowedScopes.cs` is the clearest case: the four `step` locals get four
  correct `codemarkup.FileLocalXRef` rows, because `Indexer.Declared` walks
  `symbol.Locations` per symbol, in the same run in which a filled `csharp.Local` would
  hold one row for all four. `Query/LocalsRangeVariables.cs` does it as a matched pair —
  `Doubled` and `DoubledByHand` are the same computation, one written as a query and one as
  its translation, and the difference between their row counts is the whole of M23.
- **`LocalsQuerySource<TItem>` has one arity, deliberately.** A non-generic
  `LocalsQuerySource` beside it would be the type-name-at-two-arities collision that kills
  an indexing run. The same rule keeps every type here to a single arity.
- **No `System.Linq`.** A query expression is defined by translation onto method calls found
  by ordinary member lookup, so the pattern is declared here. If the queries called
  `Enumerable.Select` then "the method a query calls has no reference row" would be true and
  vacuous, because a declaration in a referenced assembly has no definition row to join to
  either.
