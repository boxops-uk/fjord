# `indexer` — the overloaded indexer no C# repository could be indexed with

One project and two files, the whole of it there for one shape: **an overloaded indexer**,
`this[int]` beside `this[int, int]`, which is an unremarkable C# idiom.

Roslyn names every indexer of a type `this[]`, and a term descriptor is `<name> '.'` — the SCIP
grammar's one disambiguator slot is a method's. So both minted `Slots/Shelf+1#`this[]`.`,
`codemarkup.Definition` is keyed `{symbol, file}` with a value side, and two facts wanted one key
with two values. Ingest refused it (`ops-I4`), `FactSink` latched the refusal, and the run died
part-way through a write. Indexing a repository containing such a type was not degraded, it was
impossible — the same failure, from the same cause, as `class Result` beside `class Result<T>`.

`OverloadedIndexerTests` is what runs over this: the real program, a real socket, a real
database, and the exit code as the conflict assertion — the same reading `ledger` and
[`arity`](../arity/README.md) both give it.

| Shape | What it is here for |
|---|---|
| `Shelf<T>.this[int]` beside `this[int, int]` | the pair itself: two symbols, `` `this[]` `` and `` `this[]+1` ``, where there was one and a dead run |
| a third, `Shelf<T>.this[string]` | three siblings rather than two, so the ordinal is shown to be a position in a total order and not a flag |
| `this[int]` beside `this[string]` | the two differ only in parameter **type**, which is why the ordinal is the `docId`-sorted one methods already use rather than a count of parameters |
| the indexers being on `Shelf<T>` | the containing type's arity suffix and the term's own ordinal in one symbol, on their own descriptors |
| `Single.this[int]`, alone | the control: the ordinal is empty for the only sibling, so a type with one indexer keeps the string it has — the asymmetry that keeps this off every property in every index |
| `Explicit`'s two implementations of `IShelf`'s indexers | Roslyn names each `Slots.IShelf.this[]`, escaped for the dots and the brackets, so the ordinal has to land **inside** the backticks |
| `Uses`' element accesses | a use of each indexer, so each declaration has a reference — see the limitation below |

**The reference half is not observable in this database, and that is a gap in the walk rather
than in the format.** A reference is collected from a `SimpleNameSyntax`, and an indexer's use is
an element access with no name node of its own — so `shelf[0]` writes no `codemarkup.FileXRef`
row, whatever the symbol says. It is recorded in [the indexer's
README](../../../Boxops.Fjord.Indexer/README.md); that a reference is byte-identical to the
declaration it points at is asserted where the strings are minted, by `ScipSymbolsTests` and by
`golden/symbol-scheme.txt`, both of which resolve an element access themselves.

**A fixture of its own rather than an edit to `arity`**, which is the rule `ledger` and `census`
both carry: what a new test needs belongs in a new fixture. The two shapes are the same defect
class and different code, and keeping them apart is what makes a failure name which one — `arity`
would otherwise go red for a reason its README does not describe.

**The entity layer does not merge these**, which is the contrast with `arity`: `csharp.Property`
trails `docId`, so eight indexers were eight entities all along. What could not tell them apart
was `src.Symbol`, and `csharp.SymbolOf` was therefore many-to-one — the mapping a
find-references answer is read through. `Every_indexer_reaches_a_symbol_of_its_own` is that
assertion.
