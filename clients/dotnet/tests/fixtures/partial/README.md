# `partial` — one member, two declarations, two ways of dying

One project and four files, there for one shape: **a partial member**. `partial void Ping();`
beside `partial void Ping() { }` is two declarations of one thing, and a containing type's
`GetMembers()` lists only the first of them — so `GetDeclaredSymbol` on the second hands the walk
a symbol its own type does not list. It killed the run two different ways depending on how the
halves were written.

**Across two files**, the sibling search for the unlisted half missed and the ordinal refused to
guess an empty one, which was an exception:

```
Unhandled exception. System.ArgumentException: Part.Split.Ping() is not a member of
Part.Split (Parameter 'member')
   at Boxops.Fjord.Indexer.ScipSymbols.Ordinal(ISymbol member)
EXIT=134
```

**In one file** — which C# permits — both halves reached one `{symbol, file}` key of
`codemarkup.Definition` with two spans. It is keyed `{symbol, file}` with a span on the value side,
so two facts wanted one key with two values; ingest refused it (`ops-I4`), `FactSink` latched the
refusal, and the write stream died part-way through:

```
Unhandled exception. System.InvalidOperationException: the fact writer failed while
writing codemarkup.Definition
 ---> Boxops.Fjord.Client.FjordServerException: Conflict: predicate PredicateId(0)
      already holds a different fact under this key, as FactId(1)
EXIT=134
```

**And two `partial class` parts written in one file died the same way, with no partial member in
sight** — which is what says the defect was never about partial *members*: it was a value that
depended on which declaration the walk was standing on, under a key that names only the symbol and
the file.

| Shape | What it is here for |
|---|---|
| `Across.Ping`, declared in `Declaring.cs` and implemented in `Implementing.cs` | the shape itself: the implementing half spells the declaring half's symbol rather than being looked for in a list it is not in |
| `Across.Ping(int)` beside it, both halves | so the sibling **ordinal** is counted and not merely looked up — the implementing half has to land on `Ping(+1).` and not on the first overload's spelling |
| the documentation comment on the declaring half only | `GetDocumentationCommentXml` is **empty** on an implementing part, so a per-symbol fact read from the wrong half is `codemarkup.SymbolInfo`'s `{symbol}` key filled twice with two values — the conflict that has nothing to do with which file either half is in |
| `Across.Count`, a partial **property** | the arm beyond methods; a property has its own `PartialDefinitionPart` |
| `Across.this[int]` and `this[string]`, partial **indexers** | the intersection of the two hazards: an escaped name, an ordinal that has to land inside it, and a half the type does not list |
| `Across.Whole()`, ordinary | the control: a member of the same type, in the same file, spelled as it always was |
| `Together.Tick` and `Together.Beats`, both halves in `Together.cs` | the one-file layout, which is the `codemarkup.Definition` conflict rather than the sibling miss |
| a second `partial class Together` in that same file | the same conflict from a type rather than a member — a dead run before this fixture existed |
| `Uses.Go` | a use of each, so every declaration has a reference that must be byte-identical to the one string its halves mint |

`PartialMemberTests` is what runs over this: the real program, a real socket, a real database, and
the exit code as the conflict assertion — the same reading `ledger`, [`arity`](../arity/README.md)
and [`indexer`](../indexer/README.md) all give it. Then it asks the database for the shape of what
was written: one `codemarkup.Definition` per member per **file**, at that file's first declaration
of it, with a `csharp.DefinitionLocation` and a `codemarkup.FileDefinition` for **every**
declaration — one definition with two locations, which is what a partial type has answered "where
is this written" with all along.

**A fixture of its own rather than an edit to `arity` or `indexer`**, which is the rule `ledger` and
`census` both carry: what a new test needs belongs in a new fixture. The three are the same defect
class and different code, and keeping them apart is what makes a failure name which one.

**No `<LangVersion>`, on purpose.** Partial properties and partial indexers are C# 13 and the
project's `net10.0` default reaches them; a partial *constructor* is preview in this SDK and is
therefore not here, because a fixture that needs a preview switch is one whose failure a reader
has to diagnose before reading. It is covered by being an `IMethodSymbol` — the same arm the
methods above exercise.
