# `arity` — the pair no C# repository could be indexed with

One project and two files, the whole of it there for one shape: **a type name overloaded on
arity**, `class Result` beside `class Result<T>`, which is the everyday idiom.

Roslyn's `symbol.Name` strips the arity the metadata name spells, so a type descriptor built
from it minted `Arity/Pair/Result#` for both. `codemarkup.SymbolInfo` is keyed `{symbol}` with
the signature on the value side, and the signature always differs — so two facts wanted one key
with two values, ingest refused it (`ops-I4`), `FactSink` latched the refusal, and the run died
part-way through a write. Indexing a repository containing such a pair was not degraded, it was
impossible.

`ArityPairTests` is what runs over this: the real program, a real socket, a real database, and
the exit code as the conflict assertion — the same reading `ledger` gives it.

| Shape | What it is here for |
|---|---|
| `class Result` beside `class Result<T>` | the pair itself: two symbols, `Result#` and `Result+1#`, where there was one |
| `Ok` on both halves | a member of each arity, so the suffix is asserted where it is inherited and where it must not appear |
| `Result<T>.Value`, `Result(T value)`, `T` | a member, a constructor's parameter and a type parameter under a generic type — the descriptors that inherit the parent's arity by construction |
| `Result` and `Result<int>` in signatures | a use of each arity, and the constructed one is the arm where the reference has to spell the *definition's* descriptor |
| `Overloads.M()` beside `Overloads.M<T>()`, both called | the other defect in the same unit: a reference to a constructed generic method was not `Equals` to its definition, so the ordinal search missed and the call was filed under the plain overload — a wrong edge, not a missing one |

**Nothing here is edited to make a test pass**, the rule `ledger` and `census` both carry: what a
new test needs belongs in a new fixture. This is that new fixture, and it is deliberately *not*
`ledger` — the frozen corpus's interning figures are a measurement, and moving them for a shape
that has nothing to do with interning would conflate the two. It is deliberately not `census`
either: that one's edits are for a *shape* filling a predicate its audit table excuses, and this
fills none.

**The entity layer keeps the pair apart too, and that is the fixture's other claim.**
`csharp.FullName` is `{name, containingNamespace, arity}` and `csharp.Class` leads its key with
it, so this fixture produces **two** classes named `Result` with **one**
`csharp.DefinitionLocation` each. It produced one with two rows until the arity went into the key
— indistinguishable from a partial class, and never an error — which became observable here for
the first time, because until the symbol string distinguished the arities the run died before
anything could merge. `Each_arity_is_its_own_class_fact_with_its_own_location` is the gate.

**The partial class is the claim next to it and is deliberately not in this fixture.** One entity
with two definition locations is exactly what a partial class *should* produce, so a fix that
split the arities by keying on something per-declaration would break it while passing everything
here. That contrast is asserted over [`partial`](../partial/README.md), by
`PartialMemberTests.One_definition_per_member_per_file_and_a_location_for_each_half`, and at the
key level by `EntityKeyCensusTests`, which holds both shapes in one compilation.
