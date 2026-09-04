# `Classes` — clause 15's class declaration and its non-function members

Everything clause 15 says about a class *as a declaration*: its modifiers, its type parameters
and their constraints, its base class and interface list, its partial parts, its nested types,
and the two member kinds that are not function members — constants and fields. The slice is the
`POPULATION.tsv` rows whose `area` is `ecma-15`, up to and including 15.5.6.3, less 15.3.10.2–.6
— 52 rows, of which 39 are marked `hazard`. Everything from 15.6 (methods) onward, and the five
reserved-name subclauses that each need a function member to reserve anything, belong to the
sibling project `Classes.Members`.

| File | Clauses |
|---|---|
| `ClassDeclarations.cs` | 15, 15.2, 15.2.1 — `class_declaration` bare and fully dressed, and clause 15's homonym hazard |
| `ClassModifiers.cs` | 15.2.2, 15.2.2.1–15.2.2.3 — the modifier list, `abstract`, `sealed`, `unsafe`, `private` |
| `StaticClasses.cs` | 15.2.2.4, 15.2.2.4.1, 15.2.2.4.2 — a static class, and the only three ways to name one |
| `TypeParameters.cs` | 15.2.3 — arities one to three, a shadowed parameter, a parameter named after a type, and the variant interfaces a class may implement but not declare |
| `Constraints.cs` | 15.2.5 — every constraint form, one declaration each |
| `BaseSpecification.cs` | 15.2.4, 15.2.4.1–15.2.4.3 — base classes written, implied, constructed, nested and self-referential; interface lists that repeat, inherit and duplicate |
| `PartialLedger.cs`, `PartialLedger.Part2.cs` | 15.2.7 — two partial types, each split across the two files |
| `ClassMembers.cs` | 15.3, 15.3.1–15.3.3, 15.3.7, 15.3.8 — a member of every non-function kind, the instance type, members of constructed types, constituent types, static against instance |
| `Inheritance.cs` | 15.3.4, 15.3.5 — a three-deep chain, and hiding with `new` and without it |
| `MemberAccess.cs` | 15.3.6, 15.3.9.3 — all six declared accessibilities, on fields, constants and nested types |
| `NestedTypes.cs` | 15.3.9, 15.3.9.1–15.3.9.7 — nesting four deep, hiding, the absent `this`, reaching the container's private members, and nesting inside a generic |
| `ReservedNames.cs` | 15.3.10, 15.3.10.1 — members spelled exactly as a reserved accessor name |
| `Constants.cs` | 15.4 — every type a constant may have, a three-declarator constant, a hidden constant, and two constants with one value |
| `Fields.cs` | 15.5, 15.5.1–15.5.6.3 — every field modifier, `volatile`'s permitted types, the versioning pair, and every initializer form |
| `RefFields.cs` | 15.5.1 — `ref`, `ref readonly`, `readonly ref` and `readonly ref readonly` fields, in the `ref struct` that is the only place they are legal |

Every construct carries the clause number it comes from in its doc comment, so a reader can walk
from a census row to the code without a map.

## Properties this project is built to have

- **One type name at one arity.** All 127 type declarations were checked mechanically: no simple
  name in this project appears at two arities. The four repeated simple names are each
  deliberate and each separated by something other than arity — `ClsHomonym` by namespace,
  `ClsPartialLedger` and `ClsPartialStore` by being two parts of one type, `Inner` and `Marker`
  by container. The arity pair that provokes a refused write lives in a quarantine project.
- **No indexer anywhere, and no partial member.** This project declares no `this[…]` at all, so
  it cannot contribute the two-indexers shape; `partial` appears on two type declarations and on
  no member, and each type's two parts are in two different files.
- **No file-local type.** The word `file` does not appear as a modifier here.
- **No function members.** There is not one method, property, event, indexer, operator,
  constructor or finalizer in the project — which is what keeps it disjoint from
  `Classes.Members`, and is why every *reference* here is carried by a field initializer. Where a
  clause needs an expression (15.2.2.4.2's `typeof`, 15.3.4's read through a derived type,
  15.3.9.6's read of a container's private field), the expression is a static field's initializer.
- **It compiles, and its 21 warnings are accounted for.** `dotnet build Classes/Classes.csproj`
  exits 0 with 0 errors. Nine `CS0169` and nine `CS0649` are the shape of the project — a corpus
  of declarations declares storage nothing writes or reads. The other three are each the
  observable effect of the clause that produces them: `CS0108` for 15.3.5's hiding *without*
  `new`, `CS0693` for 15.2.3's shadowed type parameter, and `CS8714` for the flipped instance
  type `ClsMapEntry<TValue, TKey>`, whose `TValue` does not satisfy the `notnull` its position
  demands.
- **Names are unique across the corpus by prefix.** Every top-level type is `Cls…` (interfaces
  `ICls…`); nested types are short and unqualified on purpose, because their identity has to
  come from their container. The sibling clause-15 project prefixes `Mem…` and namespaces under
  `Surface.Classes.Members`, and this project declares no type named `Members`, so no nested
  type's fully qualified name can collide with that namespace.

## What the hazards are for

A hazard row was judged able to make two declarations want one identity string. The kinds here
are worth naming, because a query over the indexed corpus has to keep them apart:

1. **One name, two containers.** `ClsHomonym` in two namespaces; `IntLimit` in
   `ClsConstantTypes` and in its nested `Inner`; `Marker` in `ClsNestBase` and in
   `ClsNestDerived`; `Inner` under `ClsOuterHost.Middle` and under `ClsGenericHost<TPayload>`;
   `T` on `ClsBox<T>` and on its nested `ClsBoxSlot<T>`. Each must **separate**, and in every
   case the two differ in the type of something they hold, so a merge produces a member that
   exists nowhere.
2. **One name, two declarations related by hiding.** `Version`, `Depth` (twice), `RootCeiling`,
   `Ceiling`, `OwnTotal`, `Grade` and the nested `Marker`. Hiding is not overriding: both
   declarations survive, and which one a reference means depends on the static type it is
   reached through. `ClsInheritanceReads` reads both halves of the `Depth` pair, one through the
   hiding class and one through a cast to the base.
3. **One declaration, several members.** `public int First, Second, Third;`,
   `public int Alpha = 1, Beta = 2, Gamma;` and `public const int First = 1, Second = 2,
   Third = 3;` — one `field_declaration` node and three members, two of the three with their own
   initializer and one with none. An identity minted per declaration collapses them.
4. **One declaration, several types.** `ClsInstanceTyped<int>.Seed` against
   `ClsInstanceTyped<string>.Seed`; `ClsGenericHost<int>.Inner` against
   `ClsGenericHost<string>.Inner`; `IClsSink<int>` and `IClsSink<string>` in one interface list.
   Each reference must resolve back to the single declaration while the constructed types stay
   distinguishable.
5. **A declaration written nowhere.** `ClsPlain`'s `object` base against
   `ClsExplicitObjectBase : object`, which writes it; the interface set `ClsInheritedInterfaces`
   acquires through `IClsTaggedAndNamed` without naming either member of it; the implied
   `private` on `ClsAccessDeclarations._impliedPrivateField` and the implied `internal` on
   `ClsImpliedAccess`; the `abstract sealed` metadata gives `ClsStaticUtility` for the single
   `static` the source writes. Whether these mint rows at all is the question; that they mint
   *one* row each is the claim.
6. **A name that is not a type.** `ClsParamNameShadow<ClsPlain>` declares a type parameter whose
   name is a class in the same namespace, and `where T : U` names a type parameter where a
   constraint usually names a type. Both are edges that must not land on a type row.

## What is deliberately absent

- The **arity pair** (`ClsPlain` beside `ClsPlain<T>`) — quarantined, and 15.2.3 is fully
  exercised without it.
- **Two partial parts in one file**, and a **partial member's two halves** — both kill an
  indexing run, both are 15.2.7's neighbours, and both are quarantined. This project's partial
  types are split across two files, which is the shape the standard is actually about.
- A **nested type whose fully qualified name is also a namespace-plus-type name** — a namespace
  `Surface.Classes.X` holding a type `Y` beside a class `X` holding a nested `Y`. The two
  display identically, so an identity built from the qualified name alone would take a refused
  write; it is recorded as a prediction here rather than written.
- **Pointer-typed and `fixed`-size-buffer fields**, which 15.5.1's modifier list touches and
  clause 23 owns. `ClsUnsafeMarked` carries the `unsafe` modifier and no pointer.
- **A constant of a nullable value type**, which no C# compiles; `Constants.cs` says so in a
  comment where the declaration would be.
