# `Conversions` — ECMA-334 clause 10, and the one member overloadable on return type

This project is the corpus's slice of **clause 10, Conversions**: all 50 numbered clauses from
`10` to `10.8`, whether or not they declare anything. Twenty-one source files under six directories,
one directory per top-level subclause:

| Directory | Clauses | What is in it |
|---|---|---|
| `ImplicitForms/` | 10.2.2 – 10.2.18 | identity, numeric, enumeration, interpolated string, nullable, null literal, reference, boxing, dynamic, constant expression, type parameter, tuple, `default`, `throw` and switch expression conversions |
| `ExplicitForms/` | 10.3.1 – 10.3.8 | the general cast set, numeric and enum narrowing, nullable, reference downcasts with `as` and `is`, unboxing |
| `StandardForms/` | 10.4.2 – 10.4.3 | the standard conversions, each shown in the position that makes it standard — before or after a user-defined operator |
| `UserDefined/` | 10.2.14, 10.3.9, 10.5.1 – 10.5.5 | the conversion operators, what clause 10.5.2 permits, and one call site per operator |
| `NullableForms/` | 10.6.1 – 10.6.2 | the predefined nullable conversions, and the lifted form of a user-defined operator |
| `Functions/` | 10.2.15, 10.7.1 – 10.7.3, 10.8 | anonymous function conversions to delegate types and to expression trees, and method group conversions |

Every construct carries a comment naming the clause it comes from, so a reader can walk from a
row of `POPULATION.tsv` to the code and back.

## Why this clause is the awkward one

Clause 10 is mostly about **expressions that have no declaration**. An implicit numeric
conversion is a fact about a program that no name points at; a boxing conversion allocates and
is spelled with nothing at all. So most of this project is call sites, and the question a query
should ask of it is not "is this declared" but "does the reference at this position name the
right thing, exactly once".

The exception is clause 10.5, and it is why this project exists as its own project.

## The overload-on-return-type shape

A user-defined conversion operator is **the one member kind in C# that is overloadable on its
return type**. `UserDefined/ConvReading.cs` declares:

```csharp
public static implicit operator ConvCelsius(ConvReading reading);
public static implicit operator ConvFahrenheit(ConvReading reading);
public static explicit operator ConvKelvin(ConvReading reading);
public static explicit operator ConvRankine(ConvReading reading);
public static explicit operator (double Celsius, double Fahrenheit)(ConvReading reading);
```

Two members share the metadata name `op_Implicit` and three share `op_Explicit`. Within each
name the parameter lists are letter-for-letter identical — one parameter of type
`ConvReading`. **Nothing but the return type separates them.** Any identity scheme that
disambiguates members on containing type, name and parameter list alone mints one string for
two declarations, and does so with different values.

`ExplicitForms/ConvSpan.cs` carries a second, smaller instance of the same shape so that clause
10.3.1 owns one too, and `UserDefined/ConvEvaluation.cs` holds one call site per operator so
that each declaration has exactly one reference pointing at it and no more.

A cast expression cannot name which operator it wants — `(ConvKelvin)reading` and
`(ConvRankine)reading` are the same three tokens with the target type changed — so the
reference side of this shape is as tight as the declaration side.

## The other seven shapes written on purpose

Every row in this project's slice that the census marks `hazard=yes` has a shape written for
it. Beyond the two return-type pairs:

- **`ConvCoin.Box`** (10.2.9) — two interfaces declare the same member signature and one
  struct implements both explicitly. The two members agree in name, parameter list *and*
  return type; the only difference is which interface each names. This is the narrowest
  difference between two declarations that the language can state.
- **`ConvToken.Box`** (10.3.7) — a public member and an explicit interface implementation of
  the same name and signature in one struct, which is legal because their metadata names
  differ.
- **`ConvDoubleProducer.Produce`** (10.2.8) — one class, one interface at two type arguments,
  two explicit implementations. Same name, same empty parameter list, different return type.
- **`ConvLength`'s `checked` pair** (10.5.2) — `explicit operator int` beside
  `explicit operator checked int`. These agree in name, parameter list and return type, and
  differ by one keyword. In metadata they are `op_Explicit` and `op_CheckedExplicit`, so the
  collision is only for a scheme keyed on the C# spelling; the *reference* side is sharper —
  `AsIntUnchecked` and `AsIntChecked` in `ConvEvaluation` are the same cast in different
  contexts and must resolve to different declarations.
- **`ConvTypeParameters.Pass`** (10.2.12) — `Pass<T>(T) where T : class` beside `Pass(object)`.
  The two parameter types are the same once `T` is replaced by its effective base class.
- **`ConvKernel.Peel`** (10.3.5) — `new` hiding rather than overriding, so two members with
  the same name and signature exist in one hierarchy, and an explicit reference conversion at
  the call site is what picks between them.
- **`dynamic` and tuple element names** (10.2.2, 10.2.10, 10.2.13) — `dynamic` is `object`
  plus an attribute and tuple element names are not part of the type, so the field pairs at
  the bottom of `ConvIdentity`, `ConvDynamic` and `ConvTuples` declare one type two or three
  ways. These merge rather than conflict, which makes them the quiet ones: a merged answer
  still looks like an answer.
- **`ConvAnonymousFunctions.Several`** (10.7.1) — three anonymous functions and one local
  function in a single member body. Three of the four have no name in the source at all.

## Properties this project is built to have

- **No shape from the forbidden five.** No type name at two arities, no type with two
  indexers (none with any indexer at all), nothing `partial`, nothing `file`-local. This was
  checked against the emitted assembly and not only against the source: the collection
  expression in `ConvAnonymousFunctions.Several` returns an array rather than
  `IReadOnlyList<T>`, because the read-only interface target makes the compiler synthesise a
  helper type carrying three indexers.
- **Every operator has at least one call site, and every call site names one operator.**
  `ConvEvaluation` and `ConvLifted` exist for that and hold nothing else. A query over this
  project can therefore assert reference counts, not just declaration counts.
- **The clause's own negatives are written down where the compiler enforced them.**
  `ConvPermitted` names the four forms clause 10.5.2 forbids and the error each one raises;
  `ConvStandard` names `decimal` as the type that is *not* reachable through a user-defined
  operator in one cast, in either direction, because neither direction is a standard
  conversion. In both places the comment is what the compiler said, not what was remembered.
- **Warnings, deliberately.** The project compiles with `Nullable` enabled, and clauses 10.2.7
  and 10.6 are about the absent value, so `CS8629` fires three times. Those three warnings are
  the clauses being exercised.
