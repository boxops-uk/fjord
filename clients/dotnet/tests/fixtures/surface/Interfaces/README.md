# `Interfaces` — clause 19 of the surface corpus

Clause 19 of ECMA-334 draft-v9, in twelve files: what an interface may declare, and every way a
class, struct or other interface may implement it. The slice is `POPULATION.tsv` rows whose
`area` is `ecma-19` — 33 rows, of which 27 are marked `hazard`, which is the highest density of
identity hazards in the corpus after clause 15.

The clause earns that density from one syntactic fact: **the name of an explicit interface member
implementation is not an identifier.** It is an interface type, a dot, and a member name — and the
interface type may be generic, may be constructed, may be spelled five different ways, and may be
reached by two paths through an inheritance graph. Every hazard here is a variation on it.

| File | Clauses |
|---|---|
| `Contract.cs` | 19, 19.1 — an interface as a contract, implemented by a class, a struct, a record and a record struct |
| `Declarations.cs` | 19.2, 19.2.1 declaration form; 19.2.2 modifiers; 19.3 the body, including an empty one |
| `Variance.cs` | 19.2.3.1 variant type parameter lists; 19.2.3.2 the positions variance safety permits; 19.2.3.3 variance conversions |
| `BaseInterfaces.cs` | 19.2.4 — base interfaces, a diamond, a hidden inherited member, a constructed base |
| `Members.cs` | 19.4, 19.4.1 the member kinds; 19.4.2 fields; 19.4.3 methods; 19.4.4 properties, including one a derived interface implements explicitly; 19.4.5 events; 19.4.6 indexers |
| `StaticMembers.cs` | 19.4.7 operators, including `static abstract` and `static virtual`; 19.4.8 static constructors |
| `NestedTypes.cs` | 19.4.9 — every type kind an interface may nest, and a nested type of a generic interface |
| `MostSpecific.cs` | 19.4.10 — default implementations, and the most specific one of several |
| `MemberAccess.cs` | 19.4.11 — a member reached through the interface rather than through the class |
| `QualifiedNames.cs` | 19.5 — five spellings of one interface in one class's qualified member names |
| `Implementations.cs` | 19.6, 19.6.1 base lists; 19.6.2 explicit implementations; 19.6.4 generic methods |
| `Mapping.cs` | 19.6.3 uniqueness; 19.6.5 mapping; 19.6.6 inheritance; 19.6.7 re-implementation; 19.6.8 abstract classes |

Every construct carries the clause number it comes from in its doc comment, so a reader can walk
from a census row to the code without a map.

## Properties this project is built to have

- **One type name at one arity.** All 119 type declarations were checked mechanically: 118
  distinct names, no name at two arities. The only repeated simple name is `IfaceHidden`,
  declared once in `IfaceHidingBase` and once in `IfaceHidingDerived`, which is 19.2.2's `new`
  modifier and is separated by its containing type rather than by its arity.
- **One indexer per type.** Eight types declare a `this[…]` — `IfaceEveryMember`,
  `IfaceEveryMemberBox`, `IfaceDefaults`, `IfaceExplicit`, `IfaceExplicitBox`, `IfaceMap<TKey,
  TValue>`, `IfaceStringIntMap` and `IfaceQualifiedMap` — and none declares two. This is
  what constrains the explicit-implementation cases: a class implementing an interface indexer
  explicitly declares no indexer of its own, because the two would be one type with two
  `this[…]`.
- **One member per name per interface.** No interface here declares two members of one name. The
  same-name hazards are built across *two* interfaces instead — `IfaceAlphaNamed.Name` beside
  `IfaceBetaNamed.Name`, `IfaceCountAlpha.Count` beside `IfaceCountBeta.Count` — where the
  qualified names differ and an index that keys on the simple name does not.
- **No partial anything, and no file-local type.** The word `partial` appears in this project
  only in comments.
- **It compiles with no warnings.** `dotnet build Interfaces/Interfaces.csproj` reports
  `0 Warning(s) 0 Error(s)`, which is unusual for a corpus project and is a property of this
  clause: clause 19's oddities are all in what a name means, not in what the compiler thinks of
  it.
- **Every behavioural claim in a comment was run.** The comments assert which member executes —
  7 through the interface and 9 through the class for `IfaceOrderHides`, 11 for
  `IfaceOrderReimplements`, `IfacePoliteGreeter`'s greeting for `IfaceGreeterHost`, the class's
  own for `IfaceAmbiguousHost`. Each was checked by running the code in a throwaway console
  project outside the fixture, because a comment about run-time behaviour that nobody executed is
  a guess.
- **Names are unique across the corpus by prefix.** Every type declared here is named `Iface…`,
  nested types included, and no other project in the corpus uses that prefix.

## What the hazards are for

Twenty-seven of the thirty-two rows were judged able to make two declarations want one identity
string. They come in five kinds here, and a query over the indexed corpus has to tell them apart:

1. **A member whose name is a qualified name.** `IfaceQualifiedBox` implements five members of
   one interface under five spellings of that interface's name — simple, namespace-qualified, a
   namespace alias, a type alias, and `global::`-qualified. Five member identities, and *one*
   owning interface: an identity built from the written qualifier would find five.
2. **A qualifier that carries a substitution.** `IfaceStringIntMap` explicitly implements
   `IfaceMap<string, int>.Get`, `.Put` and `.this[…]`. The member being implemented takes a
   `TKey` and returns a `TValue`; the member implementing it takes a `string` and returns an
   `int`. The identity of the implementation has to carry `<string, int>`, or every construction
   of `IfaceMap` mints the same three names.
3. **One name, two members.** Two interfaces declare `Name()` with identical signatures;
   `IfaceTwoNames` implements both explicitly, so one class holds two members whose simple names
   and signatures are identical and whose qualifiers are not. Its mirror is `IfaceOneName`, where
   one class member is the implementation of both — two mapping edges into one declaration.
   `IfaceCountAlpha.Count` and `IfaceCountBeta.Count`, joined by `IfaceCountJoin`, are the same
   shape one level up: two members of one name are members of one interface, and a reference to
   the simple name through it does not compile.
4. **A member declared in one place and executing in another.** `IfaceDefaultsBox` declares one
   member and has five; `IfaceMapDerived` declares none at all and implements
   `IfaceBaseAlpha` entirely from a base class that never heard of it; `IfaceOverrideOrder`'s
   mapping names its base class's property while its own override is what runs; `IfaceGreeterHost`
   answers `Greet()` with a member declared in an interface. Four cases where the declaration a
   reference resolves to and the declaration that executes are different rows.
5. **One declaration, several types.** `IfaceAcceptsBoth` implements `IfaceAccepts<int>` and
   `IfaceAccepts<string>`: two entries in the interface set, one declaration, two base-type edges.
   `IfaceRepeatedBase` names `IfaceBaseAlpha` in its base list and inherits it through a diamond
   as well, so three paths reach one interface which is in the set once. `IfaceHostOf<TItem>`'s
   nested `IfaceHostOfCursor` declares no type parameter and is a different type per substitution.

## What is deliberately absent, and why

Two shapes clause 19 contains are not written here. Both are predictions about what would kill an
indexing run, and a prediction recorded is worth more than a dead run.

- **Two explicit implementations of two same-named members of one generic interface.** A class
  implementing `IfaceTagged<int>` and `IfaceTagged<string>`, where `IfaceTagged<TTag>` declares a
  property `TTag Tag`, has no choice: a property cannot be overloaded, so both implementations
  must be explicit, and the class then declares `IfaceTagged<int>.Tag` beside
  `IfaceTagged<string>.Tag`. If a member's identity is built from the qualifier's *declaration*
  rather than from the constructed type, both mint one string with two values behind it — a
  refused write, which fails the write stream and loses every fact after it in the walk. 19.6.3
  is exercised in the shape that has an escape instead: `IfaceAccepts<TItem>` declares a *method*
  taking a `TItem`, so `IfaceAcceptsBoth` implements both constructions as ordinary overloads.
- **The `partial` modifier on an interface.** 19.2.2 lists it. A partial type whose parts sit in
  one file is a known killer, and splitting one across two files to dodge that is a bet on how a
  type declaration's identity is keyed — if it is keyed by name and namespace, two parts in two
  files mint one string with two locations behind it, which is the same refused write with a
  wider blast radius. The rest of 19.2.2's modifier set is here: all six declared accessibilities
  and `new`.

## Where the clause and the compiler differ

Draft-v9 was written before `static abstract` interface members, and one sentence of clause 19 is
no longer enforced. It is exercised in the form the language now accepts and said out loud in the
code, because a reader checking the corpus against the standard would otherwise take the
construct for a mistake.

- **19.4.7** says it is a compile-time error for an interface to declare a conversion, equality
  or inequality operator. `IfaceAddable<TSelf>` declares `static abstract explicit operator
  int(TSelf)` and it compiles. Equality and inequality are absent — a `static abstract` pair of
  them belongs to whichever project owns clause 15.10, and neither is needed to reach 19.4.7.
- **19.4.2** still holds: an interface may not declare an instance field. What the clause's own
  example shows, and what `IfaceEveryMember` declares, is the four kinds of static field it may
  declare instead — `const`, `static`, `static readonly` and `private static`.

One shape in the clause is **unbuildable** rather than merely quarantined, and is named here for
the same reason: 19.6.3's counter-example, `class X<U, V> : I<U>, I<V>`, is CS0695. No compiling
C# holds it, so `IfaceUniqueAccepts<TItem>` exercises the rule from the permitted side —
`IfaceAccepts<TItem[]>` beside `IfaceAccepts<int>`, which no substitution can make equal.
