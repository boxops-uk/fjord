# `Assemblies.Right` — the other half of the pair

This project is one half of a two-directory subject. **The account of the mechanism, the
properties and the rules is [`../Assemblies.Left/README.md`](../Assemblies.Left/README.md)** —
read that first.

What is here:

- `PairedType.cs` — `Surface.Assemblies.Shared.AsmPairSignal` and `AsmPairSlot<T>`, **declaration-
  identical with `Assemblies.Left/PairedType.cs`**, doc comments included. Exactly one line of
  code differs: `Raise` returns this side's stamp. Keeping them identical is what makes M17 a
  silent merge rather than a refused write, so a change to one file is a change to both.
- `AsmRight.cs` — everything this side owns, all of it named `AsmRight*` / `IAsmRightSource` so
  that the pair's only shared names are the two shared on purpose: `AsmRightBackend`,
  `AsmRightBox<T>`, `AsmRightMap<TKey, TValue>`, `IAsmRightSource<out T>` and `AsmRightUses`.

There is **no `ProjectReference` to `Assemblies.Left`, and there cannot be one**: one compilation
seeing the other's `AsmPairSignal` is CS0433 at every use site. The two are source in one indexing
run, which is the whole point.
