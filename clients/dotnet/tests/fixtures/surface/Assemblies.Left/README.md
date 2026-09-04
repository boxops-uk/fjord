# `Assemblies.Left` — one type name, two assemblies (the pair's account)

This project and `Assemblies.Right` are **one subject in two directories**, and this file is the
account of both. They exist because a mechanism in the corpus plan needs *two assemblies* and
nothing else does: `M17`, the entity layer's missing assembly axis.

## The mechanism

`CsharpEntities.FullName` is `{Name(symbol.Name), Namespace(symbol.ContainingNamespace)}` — a
simple name and a namespace, and no third field. `csharp.Class` is keyed on that plus base type,
containing type, accessibility and the boolean modifiers. There is no assembly anywhere in the
key, so **two assemblies that declare the same namespace-qualified type name intern one row**.

`Assemblies.Left/PairedType.cs` and `Assemblies.Right/PairedType.cs` therefore declare, in the
namespace `Surface.Assemblies.Shared`:

| Type | Why |
|---|---|
| `AsmPairSignal` | the non-generic half: one `csharp.Class` row, two `csharp.DefinitionLocation` spans, one `csharp.SymbolOf` mapping to two symbol strings (M17) |
| `AsmPairSlot<T>` | the generic half, whose `T` is one `csharp.TypeParameter` fact in two assemblies (M10) |

Nothing else is shared. Everything a side owns is named for that side — `AsmLeftBackend`,
`AsmLeftBox<T>`, `AsmLeftMap<TKey, TValue>`, `IAsmLeftSource<out T>`, `AsmLeftUses`, and the
mirror image in `Assemblies.Right` — so the only fusions in these two projects are the two that
are on purpose.

The SCIP layer is expected to get this right, because `Package` writes the containing assembly's
own identity into the symbol string: `AsmPairSignal` should be two distinct SCIP symbols, one per
assembly, at the same time as it is one entity row. That disagreement between the two layers, at
one declaration, is what makes this pair worth checking in.

## The second mechanism, M10

`csharp.TypeParameter` is keyed `{name, variance, hasNotNullConstraint,
hasReferenceTypeConstraint, hasValueTypeConstraint}` — no declaring type, no declaring method, no
ordinal. So each side declares type parameters in four shapes, and the pair states what merges
with what:

- `AsmPairSlot<T>` in **both** assemblies, `AsmLeftBox<T>`, `AsmRightBox<T>` and the method type
  parameter of `AsmLeftUses.Convey<T>` / `AsmRightUses.Convey<T>` — six declarations of an
  unconstrained `T` across two assemblies, five key fields identical, **one fact**.
- `AsmLeftMap<TKey, TValue>`'s `TKey` is `where TKey : notnull`, so it sets one flag and is a
  different fact from its own `TValue`. That is the contrast which shows the key is made of
  *constraints*: edit the `where` clause and the type parameter moves to another entity while its
  SCIP symbol string does not change at all.
- `IAsmLeftSource<out T>` and `IAsmRightSource<out T>` carry `variance = Out`, so they fuse with
  each other and with nothing invariant.

## No reference between them, and there cannot be one

Neither csproj has a `ProjectReference` — and none is possible. The moment one compilation can see
the other's `Surface.Assemblies.Shared.AsmPairSignal`, every use site is CS0433 and the pair does
not compile. Both halves are *source in one indexing run*, which is what puts two declarations
under one entity key; asserting it needs no `extern alias` and no prebuilt DLL. (The metadata face
of the same mechanism — one half in a referenced assembly — is recorded `unbuildable` in the plan,
because it would need a checked-in DLL or a package reference, and the corpus takes neither.)

## Three properties this pair is built to have

- **The two `PairedType.cs` files are declaration-identical, doc comments included.** Exactly one
  line of code differs (the `Raise` body's per-side stamp) and one line of comment. This is a
  safety property, not tidiness: `codemarkup.SymbolInfo` is keyed on the symbol alone and carries
  `{signature, doc, modifiers}`, so if the assembly coordinate ever left the symbol string,
  identical values would dedupe in silence while a diverging signature or doc would be a *refused
  write* that kills the whole corpus run. This project's mechanism is a silent merge and must stay
  one.
- **The two csprojs are identical in every property that shapes a display string** — the reason
  `<Nullable>enable</Nullable>` appears in both. `Signature` is a Hover-format display string, and
  `AsmPairSlot<T>.Take()` hovers as `T?` only where annotations are on.
- **Each side uses the paired type itself.** `AsmLeftUses` and `AsmRightUses` each construct
  `AsmPairSignal` and `AsmPairSlot<T>`, so M17's damage is observable on the reference side too:
  `csharp.EntityXRef` cannot tell a use in one assembly from a use in the other, and
  find-references on either declaration answers with both.

Each type declares **at most one indexer** (`AsmLeftBox<T>`'s `this[int]`) and no partial member,
no `file` type and no name at two arities — the five shapes that kill an indexing run are absent
by construction, because a pair whose run dies proves nothing.
