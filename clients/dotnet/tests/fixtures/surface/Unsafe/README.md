# `Unsafe` — clause 24 of the surface corpus

Unsafe code: pointer types of every pointee, function pointer types in every position a
member can put one, the eight pointer operators, the `fixed` statement, fixed-size buffers
and `stackalloc`. The slice is every `POPULATION.tsv` row whose `area` is `ecma-24` — 25
rows, of which 16 are marked `hazard`.

Clause 24 is where a managed-only indexer runs out of arms. Two of the type kinds here are
not `INamedTypeSymbol`, one of them cannot be given a name at all, and one of them cannot be
keyed at all — so most of this project is written to make a *silent* loss visible by being
counted, rather than to be answered correctly.

| File | Clauses |
|---|---|
| `UnsGeneral.cs` | 24, 24.1, 24.2 — the `unsafe` modifier in every place its grammar admits, and an `unsafe` block |
| `UnsPointerTypes.cs` | 24.3 — one field per pointee kind: predefined, struct, nested struct, enum, tuple, framework, pointer, `void`, and a type parameter |
| `UnsFunctionPointers.cs` | 24.3's post-standard extension — `delegate*<…>` as field, parameter, return type, local, property and array element, in four calling conventions |
| `UnsFixedMoveable.cs` | 24.4 — a fixed variable and a moveable one, distinguished by whether `&` needs a `fixed` statement |
| `UnsPointerConversions.cs` | 24.5, 24.5.1, 24.5.2 — every pointer conversion the clause lists, and arrays of pointers at three ranks |
| `UnsIndirection.cs` | 24.6.2 — `*P` as a value, as an assignment target, doubled, and over a type parameter |
| `UnsMemberAccess.cs` | 24.6.3 — `P->M` to a field, a method, through two arrows, and off pointer arithmetic |
| `UnsElementAccess.cs` | 24.6.4 — `P[E]` at four index types, doubled, and negative |
| `UnsAddressOf.cs` | 24.6.5 — `&V` over each kind of fixed variable, and `&M` over a method |
| `UnsIncrementDecrement.cs` | 24.6.6 — prefix and postfix, forwards and backwards, over three pointee widths |
| `UnsArithmetic.cs` | 24.6.7 — `P + N`, `N + P`, `P - N`, `P - Q`, compound, and inside `checked` |
| `UnsComparison.cs` | 24.6.8 — all six operators, `null`, and two different pointer types compared through `void*` |
| `UnsSizeOf.cs` | 24.6.9 — `sizeof` over predefined, struct, enum, pointer, function pointer, buffer-bearing struct and type parameter operands |
| `UnsFixedStatement.cs` | 24.7 — every initializer form: array, string, `&variable`, two declarators, nested, pattern-based, `Span<T>`, pointer array |
| `UnsFixedBuffers.cs` | 24.8, 24.8.1, 24.8.2, 24.8.3, 24.8.4 — six buffers by element type, and the expressions that reach them from inside, through a pointer, and through a local |
| `UnsStackAlloc.cs` | 24.9 — eleven `stackalloc` forms, to a pointer and to a `Span<T>` |

Every construct names the clause it comes from, in the file header or in its doc comment, so
a reader can walk from a census row to the code without a map.

## What was measured rather than reasoned about

The claims in the file headers are Roslyn's answers. A probe compiled these 16 files in
memory against the `net10.0` reference pack (**16 files, 0 errors**), walked declarations
through the same `switch` as `Indexer.IndexTree`, and ran the repository's own
`ScipSymbols` — the real file, copied, not a re-implementation. It reported:

- **A pointer type has no name, so it has no symbol.** Every one of the 28 pointer-typed
  fields here has `Type.Kind == PointerType` with `Type.Name.Length == 0`, and
  `ScipSymbols.Of` returns null for all 28 — its first guard rejects an empty name, because
  a backtick-escaped empty string is no name at all. The type is still *keyed*, structurally:
  `csharp.PointerType`'s only key field is the pointee, so those 28 fields mention **19
  distinct pointees** and a query must find 19 rows, not 28. `int*` written eleven times is
  one row.
- **A function pointer cannot be keyed, and 19 declarations here disappear because of it.**
  `IFunctionPointerTypeSymbol.Signature` has `Name == ""`, `ContainingType == null`,
  `ContainingSymbol == null` and `ContainingNamespace == null`, with
  `MethodKind.FunctionPointerSignature` — so there is neither a `csharp.Method` for
  `FunctionPointerType.signature` to point at nor a `FullName` to ask for, and
  `CsharpEntities.Type` counts the drop instead. Twelve fields, one property and six methods
  in this project have a function pointer in the position their key needs, and all 19 are
  dropped whole. `ScipSymbols.Of` on the signature symbol returns null rather than throwing,
  which is why this is a silent loss and not a crash.
- **A dropped parameter leaves its ordinal behind.** `CsharpEntities.Ordered` increments the
  index whether or not the entity came back, so `UnsFunctionPointers.Apply(delegate*<int,
  int>, int)` writes one `csharp.MethodParameter` edge, at **index 1**, with no index 0 — the
  gap is the only evidence the first parameter existed. `RoundTrip` has one parameter and
  writes no edge at all.
- **`->` is indistinguishable from `.` in the index.** All 12 arrow accesses here parse as
  `MemberAccessExpressionSyntax` of kind `PointerMemberAccessExpression`, so they fall into
  the same arm of the walk as the project's 25 dot accesses and mint
  `csharp.MemberAccessLocation` over the name alone. Every one binds a fully-keyed target:
  `at->X` to `Surface/Unsafe/UnsPoint#X.`, `at->Sum()` to `Surface/Unsafe/UnsPoint#Sum().`,
  `host->Cells` to `Surface/Unsafe/UnsFixedBufferHost#Cells.`.
- **A `fixed` statement declares 22 locals that the index does not hold.** None of the 22 is
  reached by the declaration walk — its only variable-declarator arm requires a
  `BaseFieldDeclarationSyntax` parent — and `ScipSymbols.Of` would refuse them anyway, since
  SCIP gives nothing a method body introduces a global name.
- **The pattern-based `fixed` binds a method that appears nowhere in the source.**
  `GetPinnableReference` occurs as an identifier token **0 times** in these 16 files, while
  `fixed (int* at = source)` and the two `Span` forms bind it. So `UnsPinnable.GetPinnableReference`
  has a declaration and no reference, and reads as dead code to anything querying the index.
- **`stackalloc` is not an object creation.** All 15 `stackalloc` nodes are
  `StackAllocArrayCreationExpressionSyntax` or its implicit form, and **none** is a
  `BaseObjectCreationExpressionSyntax` — so no `csharp.ObjectCreationLocation` row exists for
  any of them, where `new UnsPoint { … }` in the same file mints one.
- **`sizeof` is the only pointer-adjacent operator that leaves a name.** Of 16 `sizeof`
  expressions, **6** contain an identifier for the walk to resolve (`UnsPoint`,
  `UnsPoint.Corner`, `UnsFlag`, `UnsFixedBufferHost`, `T`, and `UnsPoint` again in an offset
  computation). The other ten are spelled entirely in keywords — `sizeof(int)`,
  `sizeof(void*)`, `sizeof(delegate*<int, int>)` — and mint nothing at all.
- **A fixed-size buffer is a pointer field, and its length is not recorded.** All six buffers
  have `IsFixedSizeBuffer == true` and a `Type` that is already a pointer (`int*`, `byte*`,
  `char*`, `bool*`, `double*`, `long*`), so `csharp.Field.type` is the `pointerType`
  alternative and the element type written in the source is what the index does *not* hold.
  The lengths — 8, 16, 4, 2, 3, 1 — appear in no field of any predicate, and neither does the
  `<Cells>e__FixedBuffer` struct Roslyn synthesises to hold the storage: it has no
  declaration node, and the walk reaches declarations through syntax.
- **The `unsafe` modifier is recorded nowhere.** `CodeMarkup.Modifiers` builds its word list
  from accessibility, `IsStatic`, `IsAbstract`, `IsVirtual`, `IsOverride` and `IsSealed`, and
  has no `unsafe` arm; no field of `csharp.Method`, `csharp.Field`, `csharp.Property` or
  `csharp.Class` holds it either. Every declaration in this project is therefore written
  under a symbol and a `modifiers` string indistinguishable from a safe one's.
- **An event is dropped for its kind, not its type.** `UnsSafeHost.Read` is the one
  declaration here that `CsharpEntities.Build` refuses because there is no `csharp` event
  entity at all — `_inexpressibleKinds`, not `_inexpressibleTypes`. It is in this project by
  accident of clause 24.2 listing `unsafe` among the event modifiers, and it is worth a
  number of its own.

One warning is expected and is not a defect to be fixed: CS0169 says
`UnsFixedBufferUse._host` is never used, while `PinnedFromField` reads it as a `fixed`
statement's initializer. Roslyn's unused-field analysis does not count a fixed-size-buffer
pin as a use, which is itself a small instance of the same theme — the buffer access is
compiled through a synthesised member, and the field it hangs off falls out of the analysis.

## Properties this project is built to have

All four were checked mechanically over the compiled project, not by inspection:

- **No two declarations mint one identity string.** 245 declarations walked the way
  `Indexer.IndexTree` walks them, **245 distinct** `ScipSymbols.Of` strings, **0 collisions**
  whole-project and 0 within any one file. This project cannot cause a refused write.
- **One type name at one arity.** 28 source types, **0** names declared at two arities, and
  no generic type at all — the only type parameters here belong to methods.
- **One indexer per type.** The maximum over all 28 types is **1**: `UnsOperatorHost.this[int]`,
  which is there because clause 24.2 lists `unsafe` among the indexer modifiers.
- **No `partial` anything, and no file-local type.** Zero of each; the word `partial` does
  not appear, and `IsFileLocal` is false for all 28.

## What is deliberately absent, and where it went

Nothing in clause 24 needs one of the five quarantined shapes, so **no row in this slice is
quarantined** — the clause's hazards are all of the merge-or-drop kind rather than the
refused-write kind. What is missing instead is code that does not compile, named in the file
that would have held it:

- `&_cell` and `&Cells[1]` outside a `fixed` statement are CS0212 (`UnsFixedMoveable.cs`),
  and `fixed` over an *already* fixed buffer is CS0213 — which is why `PinnedFromField`
  pins a field of a class rather than a local.
- `*` and `++` on a `void*` are CS0242 (`UnsIndirection.cs`, `UnsIncrementDecrement.cs`):
  both are defined in terms of the pointee's size, and `void` has none.
- Assigning the pointer a `fixed` statement declares is CS1656 (`UnsFixedStatement.cs`).
- A buffer reached through a moveable receiver is CS1666, a buffer outside a struct is
  CS1642, a non-constant length is CS0150 and a managed element type is CS1663
  (`UnsFixedBuffers.cs`).
- `stackalloc` of a managed element type is CS0208 (`UnsStackAlloc.cs`).

Two rows are `not-applicable` for a reason worth stating rather than for being headings.
Clause 24.4's fixed/moveable split and clause 24.8.4's exemption from definite-assignment
checking are both *compile-time classifications*: no predicate in this schema records
either, and the compiler enforces both. `UnsFixedMoveable.cs` and
`UnsFixedBufferUse.ReadUnassigned` are written anyway, so a reader can see what the rows are
about, but a query has nothing to ask.
