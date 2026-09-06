# `refimpl` — one API, two assemblies of one identity

One library and two projects, there for one shape: **a reference assembly beside its
implementation**. `Widgets/ref/Widgets.csproj` restates the whole public API of
`Widgets/src/Widgets.csproj` — same assembly identity, every member declared, no bodies and
no documentation comments — which is what `ref/System.Collections.csproj` is to
`src/System.Collections.csproj`, and what every library in `dotnet/runtime`'s shared
framework ships.

**It killed the run.** Both projects mint the same `src.Symbol` for every member, correctly:
they are one symbol. But `codemarkup.SymbolInfo` is keyed `{symbol}` with
`{signature, doc, modifiers}` on the value side, and the two halves disagree on all three —
so two facts wanted one key with two values, ingest refused it (`ops-I4`), `FactSink` latched
the refusal, and the write stream died part-way through:

```
Unhandled exception. System.InvalidOperationException: the fact writer failed while
writing codemarkup.SymbolInfo
 ---> Boxops.Fjord.Client.FjordServerException: Conflict: predicate PredicateId(8)
      already holds a different fact under this key, as FactId(8796093022209)
EXIT=134
```

That is the same failure, on the same predicate, that a 100-project slice of
`src/libraries/sfx.slnx` produced — **no checkout shipping a reference pack could be indexed
at all.**

| Shape | What it is here for |
|---|---|
| two projects of one **file name**, in two solution folders | the identity collision itself: Buildalyzer names a compilation's assembly from the project file, so `ref/Widgets.csproj` and `src/Widgets.csproj` are one `nuget Widgets 1.0.0.0` — and `.slnx` refuses two projects of one name in one folder, which is why `sfx.slnx` files them under `/ref/` and `/src/` too |
| the documentation comments on the **implementation** only | `doc` is the field that always differs, and the one a consumer would lose to a first-wins rule — the ref half is listed **first** in the solution on purpose, so a rule that kept whichever came first would answer every documentation query with the empty string while still exiting 0 |
| `partial` on the ref half's types, absent on the implementation's | `modifiers` differs too, so the conflict does not depend on `--no-docs` being off |
| `Gadget`, a class with a constructor, a method and a property | the member kinds each take their own arm |
| `Bolt`, a struct | so the shape is not only a class |
| `Uses.Go` | a use of each, so every declaration has a reference that must still resolve once the ref half is unwalked |
| `[assembly: ReferenceAssemblyAttribute]` added by the **build**, not written in a source file | how `dotnet/runtime` marks one (`Directory.Build.props`), so the marker has to survive a design-time build to be read — a fixture that wrote it in C# would pass without proving that |

`ReferenceAssemblyTests` is what runs over this: the real program, a real socket, a real
database, and the exit code as the conflict assertion — the same reading `ledger`,
[`arity`](../arity/README.md), [`indexer`](../indexer/README.md) and
[`partial`](../partial/README.md) all give it. Then it asks the database for the shape of
what was written: one `codemarkup.SymbolInfo` carrying the **implementation's**
documentation, no `codemarkup.Definition` anywhere in the reference assembly's file, and the
reference assembly still a project in the build graph with its compilation beside it.

**A fixture of its own rather than an edit to `partial`**, which is the rule `ledger` and
`census` both carry: what a new test needs belongs in a new fixture. This and `partial` are
the same *symptom* — one symbol, two declarations, two values under one key — and different
causes: `partial` is two declarations inside one compilation, and this is two compilations
that agree on an identity. Keeping them apart is what makes a failure name which one.
