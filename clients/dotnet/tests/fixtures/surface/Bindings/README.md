# `Bindings` — the reference side: what the dispatch cannot see, and the edges nothing writes

Ten source files for seven mechanisms of the corpus plan: **M2** (reference face — the
overload ordinal counted on the raw bound symbol), **M8** (an attribute application binds to
a constructor), **M9** (the write role), **M24** (nothing links an implementation to the
interface member it implements), **M29** (the pattern-based bindings), **M30** (constructor
initializers), and **M4** (reference face — no indexer use is a row).

All of it completes. Every predicate involved is key-only — `codemarkup.FileXRef`,
`SymbolXRef`, `Relation`, `csharp.EntityXRef`, `EntityRef`, the location predicates — so a
wrong target merges into the right target's fan-out and a missing target writes nothing.
This is where the reference half of the ordinal drift lives, because only `Declare` writes
`codemarkup.SymbolInfo`, the one predicate whose value would disagree; the declaration half
of the same mechanism is a run-killer and is quarantined. A measured run encodes 5,779 facts
and finishes.

## What is where

| File | Mechanism | What it holds |
|---|---|---|
| `Overloads/BindOrdinalDrift.cs` | M2 | `BindPair<TItem>` with `Describe(TItem)` and `Describe(string)`, used on a `BindPair<int>` receiver — where substitution inverts the docId sort — and on a `BindPair<TAny>` receiver, where it does not. Two crossed spans and two correct ones. |
| `Overloads/BindMissedOrdinal.cs` | M2 | The two `FindIndex` misses: a constructed generic method, and a reduced extension method. With the unreduced spelling of the same call as the control. |
| `Attributes/BindMarkAttribute.cs` | M8 | `BindMarkAttribute`, two constructors and a `Topic` property. |
| `Attributes/BindAnnotated.cs` | M8 | Four application sites in four spellings — shortened with a named argument, shortened positional, shortened bare, and the full name. |
| `Roles/BindWriteRole.cs` | M9 | Seven ways to write one property, and the plain read beside them. |
| `Interfaces/BindInterfaceMembers.cs` | M24 | One interface member implemented implicitly and explicitly, used only through the interface — plus a `virtual`/`override` pair, which is the one member-level heritage edge that *is* written. |
| `Interfaces/BindStaticAbstract.cs` | M24 | A static abstract interface member, whose only legal call site is through a constrained type parameter, so its implementation is unreachable by construction. |
| `Patterns/BindPatternBindings.cs` | M29 | `foreach` (pattern-based and interface-based), `using` statement and declaration, a collection initializer, a deconstructing declaration, a positional pattern, a range index, and `await` — with a control method that calls every one of those members by name. |
| `Constructors/BindConstructorInitializers.cs` | M30 | `: base(...)`, `: this(...)`, `new T(x)` and a target-typed `new(x)`. |
| `Indexers/BindElementAccess.cs` | M4 | The project's one indexer, read through `[1]` and `[^1]` and written through `[2] = 3`, with a named property on the same type as the control. |

## The measurements

Taken with `Boxops.Fjord.Indexer --dry-run` for the fact counts, and with a Roslyn probe
over the same shapes for the bindings, since a fact count cannot say which declaration a
reference names.

- **`Unresolved` is 0**, `InexpressibleTypes` and `InexpressibleKinds` are both 0, and
  `csharp.DefinitionLocation` equals `codemarkup.Definition` at 124 — every declaration here
  reaches both layers. 252 references resolve.
- **M2 is measured on the disambiguator itself.** Running `ScipSymbols.Disambiguator`'s exact
  code over these shapes:

  | symbol | `FindIndex` | spelled |
  |---|---|---|
  | declaration `BindPair<TItem>.Describe(TItem)` | 1 | `Describe(+1).` |
  | declaration `BindPair<TItem>.Describe(string)` | 0 | `Describe().` |
  | use `pair.Describe(1)`, which binds to `Describe(TItem)` | **0** | `Describe().` |
  | use `pair.Describe("a")`, which binds to `Describe(string)` | **1** | `Describe(+1).` |
  | use `pair.Describe(item)` on `BindPair<TAny>` | 1 | `Describe(+1).` — correct |
  | use `words.Slice(1, 2)`, reduced | **-1** | `Slice().`, the one-argument overload |
  | use `BindSliceExtensions.Slice(words, 1, 2)` | 1 | `Slice(+1).` — correct |
  | use `Convert<int>(1)`, constructed | **-1** | `Convert().`, the `string` overload |

  The two uses in `Crossed()` each carry the other declaration's symbol; the reduced and
  constructed uses fall to `index <= 0` and carry the first-sorting overload's. `-1` and `0`
  are the same answer to `index <= 0` — that fold is the whole defect for the bottom half of
  the table.
- **The reduced extension's container is right**, measured: `ContainingType` is
  `BindSliceExtensions` and the `ContainingSymbol` chain runs through it, so the bad symbol
  joins to a real definition row for the wrong declaration. An anti-join for xref targets
  with no definition will not see it; only the two-layer comparison will.
- **M8's bindings, measured**: `[BindMark(Topic = "audit")]` → `BindMarkAttribute()`,
  `[BindMark("because")]` → `BindMarkAttribute(string)`, `[BindMark]` → `BindMarkAttribute()`,
  `[BindMarkAttribute]` → `BindMarkAttribute()`. The named argument `Topic` → the property.
  Not one of the four binds to the class.
- **M9's roles, measured** from the syntax shapes `CodeMarkup.Role` consults:
  `holder.Value = 1` write, `maybe?.Value = 1` **read**, `holder.Value++` **read**,
  `holder.Value += 1` write, `Take(ref holder.Slot)` **read**,
  `(holder.Value, _) = (1, 2)` **read**, `new BindHolder { Value = 1 }` write. Six writes,
  three of them keyed `write`.
- **M29 is measured by enumeration**: every `SimpleNameSyntax` in the ten use-site methods of
  `BindPatternBindings` is `var`, a receiver, a type name in a `new`, a local, or `Count`.
  None of `GetEnumerator`, `MoveNext`, `Current`, `Dispose`, `GetAwaiter`, `IsCompleted`,
  `GetResult`, `Deconstruct`, `Add`, `Length` or `Slice` appears, so none of them gets a row.
- **M30 is measured the same way**: `: base(weight)` contains one name node and it is the
  argument `weight`; `: this(1)` contains none; `new BindConstructed(2)` has one child name
  node and it is the type; `new(3)` has none.
- **M4's descriptor is `` `this[]`. ``, not `Item.`** Roslyn's `Name` for an indexer is
  `this[]`, which `ScipSymbols.Name` backtick-escapes. Verified against the emitted blocks:
  `BindBox#`this[]`.` appears and `BindBox#Item.` appears nowhere. A gate written against
  `Item.` would match nothing — the metadata name shows up only on the accessors,
  `get_Item` and `set_Item`.

## Properties this project is built to have

- **Every type declares at most one indexer, and at most one member per name per
  interface.** Two indexers in one type are two declarations spelling `` `this[]`. ``, and
  two explicit implementations of two constructed forms of one generic interface are the
  same shape — a term descriptor with no disambiguator, two `codemarkup.Definition` writes at
  one key in one file, a refused write, a dead run. There is exactly one indexer in this
  project and exactly one explicit interface implementation, of a non-generic interface.
  This is the constraint the project's value depends on: it has to complete.
- **Method overloads are safe and are used freely.** A method descriptor carries an ordinal,
  so two same-named methods get two symbols. What has no ordinal is a term, and what breaks
  the ordinal is a symbol absent from `GetMembers()` — a partial method's implementing half,
  which is quarantined and appears nowhere here.
- **Every mechanism has its control in the same file.** `Uncrossed` beside `Crossed`,
  `Unreduced` beside `ReducedTwoArguments`, `Unconstructed` beside `Constructed`, `Plain`
  beside the six broken roles, `ByName` beside the nine pattern-bound constructs, `Named`
  beside `Read`, a `virtual`/`override` pair beside the two interface implementations. The
  assertion is a subtraction, not a claim about one number.
- **`BindUnitValue.Weight()` is deliberately never called by name.** Writing that call
  would give the implementation a reference and destroy M24's total case. The same rule
  keeps every use in `BindInterfaceMembers.cs` routed through `IBindSink`.
- **One accidental correctness is recorded as accidental.** `words.Slice(1)` is filed
  correctly, because the overload it calls is the one that sorts first. It is in the fixture
  and labelled, so that a gate asserting "every reduced call is wrong" is not written.
