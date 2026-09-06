# `census` — one solution rich enough to fill the schema

The corpus behind `PredicateCensusTests`, whose claim is **completeness**: every predicate
`DotnetIndex` declares either has rows after a run over this fixture, or is classified in the
audit table with the reason it cannot. So this fixture is not chosen to be small — it is
chosen to reach every shape a producer can write, and each file below is here for named
predicates.

| Shape | What it fills |
|---|---|
| `Census.slnx` itself, as the entry point | `msbuild.Solution` and both membership edges — written because this run resolved a solution, and asserted empty over a run entered at `Core/Core.csproj` instead |
| two projects, one referencing the other | `msbuild.ProjectReference` and its reverse, and cross-project references |
| `IShape` + `Rectangle : IShape` | `csharp.Interface`, `csharp.Class`, `csharp.Implements`, `codemarkup.Relation` |
| `record Entry` · `readonly struct Corner` | `csharp.Record`, `csharp.Struct` |
| `double[] _sides` | `csharp.ArrayType` |
| `static unsafe double Peek(double*)` | `csharp.PointerType`, which exists only in unsafe code — hence `AllowUnsafeBlocks` in `Core.csproj` |
| `Store<T>` with `this[int]` and `First<TShape>` | `csharp.TypeTypeParameter`, `csharp.PropertyParameter`, `csharp.MethodTypeParameter` |
| `new Rectangle(2, 3)` · `new()` | `csharp.ObjectCreationLocation` |
| `_store.Add(wide)` | `csharp.MethodInvocationLocation`, with the member access it was invoked through |
| `_store.Count` · `entry.Key` · `Rectangle.Unit` | `csharp.MemberAccessLocation` over a property and a field |
| `Store<Rectangle>`, `Entry`, `Corner` in signatures | `csharp.TypeLocation` |
| `var wide`, `var index` and their uses | `codemarkup.FileLocalXRef` — a local has no global name |
| `Vendored/`, which the solution does not name | `msbuild.Package` and both package edges, read from XML because nothing builds it and so nothing restores it |

**Nothing here is edited to make a test pass**, the same rule the `ledger` fixture carries:
what a new test needs belongs in a new fixture. What this one may gain is a *shape* that
fills a predicate the audit table currently excuses — that is the only edit it is for, and it
comes with the table entry that changes with it.

**Nothing here is dropped**, which is why the table above can be read as a claim about the
producer rather than about the corpus: every declaration is expressible and every name binds,
so a predicate with no rows is a producer that does not write it. That is also why there is no
`delegate*<…>` here — a function pointer's signature has no containing type, so it cannot be
keyed as a `csharp.Method` and the member typed as one is dropped.
`SourceWalkTests.A_function_pointer_in_a_signature_is_dropped_and_counted` provokes that on its
own source instead, where the drop is the assertion. The three zeros a run reports —
unattributed files, dropped declarations, unbound names — are asserted over the `ledger` fixture,
which exists for exactly that.
