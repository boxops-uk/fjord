# `EnumsDelegates` — clauses 20 and 21 of the surface corpus

Enums and delegates, in fifteen files. The slice is `POPULATION.tsv` rows whose `area` is
`ecma-20` (enums) or `ecma-21` (delegates) — 14 rows, and **every one of them is marked
`hazard`**, which is why this project is mostly hazards and only incidentally coverage.

| File | Clauses |
|---|---|
| `EnumGeneral.cs` | 20 and 20.1 — an enum is a distinct value type whose default is zero |
| `EnumDeclarations.cs` | 20.2 — the enum-base, all eight underlying types, an empty body, a trailing semicolon, and enums nested in a class, a struct, an interface and a nested namespace |
| `EnumModifiers.cs` | 20.3 — every accessibility a top-level and a nested enum can be declared with, and `new` |
| `EnumMembers.cs` | 20.4 — explicit, implicit, forward-referenced and composite member values; attributes and verbatim identifiers on members |
| `EnumSystemType.cs` | 20.5 — `System.Enum` as base, as constraint, and as the container of every member an enum value inherits |
| `EnumValuesAndOperations.cs` | 20.6 — the fourteen predefined operators, the conversions, and the three constant positions an enum member is folded into |
| `EnumUnqualifiedUse.cs` | 20.4 and 20.6 at use sites that do not name the enum: `using static` (14.5.5) and a using alias (14.5.2) |
| `DelegateGeneral.cs` | 21 and 21.1 — static and instance targets, and the immutability of an invocation list |
| `DelegateDeclarations.cs` | 21.2 — generic, variant, variadic, `ref`/`out`/`in`, ref-returning, defaulted, constrained, attributed and nested delegate declarations |
| `DelegateMembers.cs` | 21.3 — `Invoke`, `BeginInvoke`, `EndInvoke`, the constructor, and everything inherited from `System.Delegate` |
| `DelegateCompatibility.cs` | 21.4 — parameter contravariance, return covariance, exact by-reference matching, and variance conversions between constructed forms |
| `DelegateInstantiation.cs` | 21.5 — a method group, an overloaded group, an instance group, a generic group, a lambda, a typed lambda, a static lambda, an anonymous method, a parameterless anonymous method, a local function, another delegate, and a natural type |
| `DelegateInvocation.cs` | 21.6 — every receiver an invocation can have, and the argument forms `Invoke` is reached through |
| `DelegateEvents.cs` | 21.2 and 21.6 through clause 15.8 — events of a custom delegate type, field-like, accessor-bearing, static and explicitly implemented |
| `FrameworkDelegates.cs` | 21.2 and 21.5 over `System.Action`, `System.Func` and their relatives — the delegate types the corpus uses but does not declare |

Every construct carries the clause number it comes from in its doc comment, so a reader can
walk from a census row to the code without a map.

## Properties this project is built to have

- **One type name at one arity.** All 95 type declarations — 39 enums, 28 delegates, 23
  classes, 3 interfaces, 2 structs — were checked mechanically: no name appears at two
  arities. The generic delegates are `EdMapper<TIn, TOut>`, `EdVariantMapper<TIn, TOut>`,
  `EdFactory<T>`, `EdConstrained<T>`, `EdSink<T>` and `EdSource<T>`, six distinct names, and
  not one of them has a twin at another arity. Exactly two simple names repeat, both
  deliberately and both at arity zero: `EdModHidden` and `EdHiddenDelegate`, each declared
  once in a base class and once in a derived class with `new`, which is clause 20.3's and
  21.2's hiding hazard and not an arity pair.
- **No indexer anywhere.** The two-indexers-in-one-type shape cannot arise here, because no
  type in the project declares `this[...]` at all.
- **No partial anything, and no file-local type.** Both shapes kill an indexing run. The word
  `partial` does not occur in the project, and neither does `file` as a modifier.
- **It compiles, and its three warnings are deliberate.** `CS1718` in
  `EnumUnqualifiedUse.cs` is the compiler stating the alias merge claim itself — it can see
  that `EdShade.Red` and `EdColor.Red` are one member and says so. `CS8601` and `CS8603` in
  `DelegateInstantiation.cs` are clause 21.5's removal semantics: `-` on delegates is
  `Delegate.Remove`, so it can empty a list and yield null where `+` never does. Nothing else
  warns.
- **Names are unique across the corpus by prefix.** Every type here is `Ed…` (interfaces
  `IEd…`), enums and delegates alike. Twenty-two projects are indexed in one run, and enums
  called `Color` and delegates called `Handler` are what every one of them wants.

## What the hazards are for

All 14 rows in this slice are hazard rows, and they resolve into three shapes. A query over
the indexed corpus has to distinguish them, because the right answer is different for each.

1. **Two declarations that want one identity — these must separate.** `EdStrokeWeight.Thin`
   and `EdStrokeWeight.Slim` are both the constant 1, and `EdAccess.All` and
   `EdAccess.Everything` are both 7 — one written as a literal, one as an expression over
   other members. `EdBearing.North` and `EdCompass.North` share a name and not a value.
   `EdDeclaredLeft` and `EdDeclaredRight` are member-for-member identical. `EdNotifyLeft` and
   `EdNotifyRight` are two delegate types with one signature, `void(string)`, and the language
   keeps them so far apart that no implicit conversion exists between them — while one method,
   `EdCompatibility.Announce`, is the target of both. `EdEcho.EdEcho` is a member whose name is
   its own container's. `EdModHidden` and `EdHiddenDelegate` are each two declarations of one
   name in a base and a derived type. And `TwoLambdasOneParameterName` declares two lambdas
   whose parameters are both called `n` in one method, where the synthesized methods differ
   only by an ordinal the source does not contain.
2. **A declaration written nowhere — the claim is that each mints one row, or none, but never
   a second value under a first row's key.** Every one of the 39 enums has `System.Enum` as
   its base and a `value__` instance field, and no token in the project writes either;
   `System.Enum` is written once, as a constraint in `EnumSystemType.IsZero`, and that
   occurrence must name the same type the 39 implicit heritage edges point at. Every one of
   the 28 delegates has a constructor, an `Invoke`, a `BeginInvoke` and an `EndInvoke`
   synthesized from its single declaration line, plus a base of `System.MulticastDelegate`.
   `EdWatched.Changed` is a field-like event, so its declaration also makes an `add`
   accessor, a `remove` accessor and a backing field. `default(EdVoltage)` is a value of an
   enum none of whose members is zero. And the predefined enum operators — all fourteen the
   clause lists, plus `sizeof`, exercised across `EnumValuesAndOperations.cs` — are declared
   by nothing at all: the language supplies them per enum type, so every operator use site in
   that file has no declaration to point at.
3. **A reference whose target is not named at the site — these must resolve, and to the right
   declaration.** This is what clause 21.6 is: `combine(2, 3)` calls `EdCombine.Invoke`, and
   the only identifier at the site is `combine`. The wrong answers are both available and both
   plausible — attributing the call to `EdCalculator.Add`, the method the delegate was made
   from, or attributing it to nothing and losing the only call there is. Beside it:
   `combine.Invoke(2, 3)` writes the name out, and the two must be one member;
   `chooser(true)(4, 5)` calls two `Invoke`s of two delegate types in one expression;
   `Read | Write` names two members of an enum that appears only in a `using static`
   directive; `EdShade.Red` reaches a member through an alias for its type; `EdMark(...)`
   applies a type spelled `EdMarkAttribute`; `FromOverloadedGroup` resolves the identifier
   `Print` to one of three declarations; `FromGenericMethodGroup` references
   `Describe<int>` with neither the brackets nor the `int` written; `FromNaturalType` gives a
   local the type `System.Action<string>`, which no token names; and `Narrow(EdSink<object>)`
   converts between two constructed delegate types on the strength of one `in` keyword on a
   declaration in another file.

## What is deliberately absent

- **A generic delegate at two arities** — `EdMapper<T>` beside `EdMapper<T, TOut>` would be the
  most natural thing in the world to write in clause 21.2, and it is the first of the five
  shapes that kill an indexing run. It is quarantined, and 21.2 is fully exercised without it:
  variance, constraints, variadics, by-reference parameters and ref returns are all shown on
  delegates whose names are used once.
- **`System.Action` and `System.Func`, however, are that shape** — `Action` and `Action<T>` are
  one name at two arities, and `Func` has seventeen. They arrive from metadata rather than
  from source, and `FrameworkDelegates.cs` references them on purpose, because a corpus that
  never mentioned them would not resemble any C# anyone writes. If an index mints identities
  for referenced external types on a name that omits the arity, this file is where the run
  dies, and that is a prediction worth having recorded.
- **A `value__` member declaration.** Every enum has an instance field called `value__` that
  holds its underlying value, and the compiler refuses to let a member declaration shadow it
  (CS0076). So the implicit field can be predicted and not provoked: no source in this project
  can make a second declaration want its identity.
- **Function pointer types** (`delegate*`), which look like clause 21 and belong to clause 23.
- **A nested type whose namespace-qualified name matches a namespace-plus-type name.**
  `Surface.EnumsDelegates.Deep` is a namespace and no type in the project is called `Deep`, so
  the collision the `Types` project predicts is not reachable from here.
