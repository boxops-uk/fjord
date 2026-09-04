# `Structs` — clause 16, and the members nobody writes

Twenty-two files for ECMA-334 draft-v9 **clause 16**, structs. The slice is `POPULATION.tsv`
rows whose `area` is `ecma-16` — 36 rows, of which 26 are marked `hazard`. One file per
numbered subclause or small group of them, plus two files for the struct forms the language
grew after the clause was written.

| File | Clauses | What it is for |
|---|---|---|
| `General.cs` | 16, 16.1 | The smallest complete struct, and one declaration reached as a value, a box and a nullable |
| `StructDeclarations.cs` | 16.2, 16.2.1 | Every optional part of the `struct_declaration` production: attributes, a type parameter list, two constraints clauses, a trailing semicolon |
| `StructModifiers.cs` | 16.2.2 | `public`, `internal`, none, `private`, `protected`, `protected internal`, `private protected`, `readonly`, `unsafe`, and `new` over an inherited nested struct |
| `RefModifier.cs` | 16.2.3 | `ref struct` with a `ref` field and a `ref readonly` field, `readonly ref struct`, a `scoped` parameter, and the post-standard `allows ref struct` |
| `PartialModifier.cs`, `PartialModifierPart2.cs` | 16.2.4 | One partial struct in two files: fields and constructor in one, property, method and nested enum in the other |
| `StructInterfaces.cs` | 16.2.5 | A four-interface list, an explicit interface implementation beside a same-named public member, and a default interface implementation nothing declares |
| `StructMembers.cs` | 16.2.6, 16.3, 16.3.1 | One struct body with one of every member kind a struct may declare, and the inherited half of the member list |
| `ReadonlyMembers.cs` | 16.3.2 | `readonly` methods and properties in a mutable struct, `readonly get` beside `set`, and a `readonly struct` where all of it is implicit |
| `ValueSemantics.cs` | 16.4.2, 16.4.4, 16.4.5, 16.4.6 | Copy on assignment beside a class that does not, three spellings of a default value, boxing both ways, and `with` on a non-record struct |
| `Inheritance.cs` | 16.4.3 | The implicit base type, the three overrides a struct member may be, and a struct that overrides none of them |
| `MeaningOfThis.cs` | 16.4.7 | `this` assigned to, passed by value, passed by `ref`, passed by `in`, boxed, and read in a readonly member |
| `FieldInitializers.cs` | 16.4.8 | Post-standard struct field initializers, the constructor they require, and the `default` that skips them |
| `Constructors.cs` | 16.4.9 | The declared constructor beside the unwritten one, a declared parameterless constructor, `: this()` chains, and a primary constructor |
| `StaticConstructors.cs` | 16.4.10 | A static constructor, the references that trigger it, and the `default` and the array that do not |
| `StructProperties.cs` | 16.4.11 | Auto, get-only, `init`, full-body, expression-bodied, static, private-set and `required` properties |
| `StructMethods.cs` | 16.4.12 | One overload set of five, `params` both ways, a ref return, an iterator, an async method and a local function |
| `StructIndexers.cs` | 16.4.13 | Five structs with one indexer each, including one renamed by `IndexerName` beside a property called `Item` |
| `StructEvents.cs` | 16.4.14 | A field-like event, an event with accessors, a static event, and the subscription a copy loses |
| `SafeContext.cs` | 16.4.15.1–16.4.15.8 | Every parameter modifier, every `ref` and `scoped` local form, field safe context, operators on a ref struct, `stackalloc`, and constructor invocation with a `ref` argument |
| `RecordStructs.cs` | post-standard | `record struct`, `readonly record struct`, `with`, deconstruction, positional patterns and an overridden `PrintMembers` |
| `InlineArrays.cs` | post-standard | The C# 12 inline array: element access with no indexer, the span conversions, and one as a field of an ordinary struct |

Every construct carries the clause number it comes from in its doc comment, so a reader
can walk from a census row to the code without a map.

## Why clause 16 is worth a project of its own

**A struct has members that no file declares, and it always has, and clause 16 is where
they come from.** Every other clause that declares something declares it with tokens. Here
the compiler adds to the member list on its own:

- the **parameterless constructor** (16.4.9), which exists whether or not it is written, so
  a struct with one declared constructor has two;
- the **base type** `System.ValueType` (16.4.3), which no token names, and the `Equals`,
  `GetHashCode` and `ToString` inherited through it;
- an **auto-property's backing field** (16.4.11);
- a **field-like event's backing field** (16.4.14), which has *the event's own name*, in the
  event's own type;
- a positional **`record struct`'s** dozen-plus members (post-standard), of which the source
  writes none;
- an **inline array's** element access and span conversions (post-standard), which make an
  index expression resolve to a member that is in no file in the corpus.

`Constructors.StTwoConstructorsOneWritten` is the smallest case and the one worth gating on:
one constructor in the file, two in the type, and the unwritten one sorts *first*. Anything
that numbers members off the compiler's list — an ordinal, a position in a member array —
counts a member the index never saw, and every number after it in that type is off by one.
`StructMethods.StMethodSet` is the same drift with something behind it to move: five methods
called `Combine`, told apart by signature or by ordinal, sitting after an unwritten
constructor.

**And clause 16 is where a modifier changes meaning without changing a name.** `readonly`,
`ref`, `scoped`, `in`, `ref readonly` and `new` are, between them, half this project.
`ReadonlyMembers.StMutableGauge.Peek` and `.Take` have the same shape, the same return type
and different promises; `StParameterSafeContext.ByValue(int)` and
`StOtherParameterSafeContext.ByValue(ref int)` are one name and two signatures that differ
only by a modifier, and they are in two types because C# will not let them share one. If an
identity is minted from names, none of these pairs is distinguishable, and nothing about
that failure is loud.

## Four properties this project is built to have

- **It compiles, and every construct in it is legal C# 14 on `net10.0`.** `dotnet build`
  exits 0 with four `CS0649` warnings — fields that are never assigned, which is what a
  struct field on a type declared to demonstrate an accessibility modifier looks like. No
  file here is a compiler-error fixture.
- **None of the five refusing shapes is written.** No type name at two arities (checked
  mechanically over all 109 type declaration sites: no base name appears at two arities,
  here or anywhere else in the corpus, and no name in this project is used by another
  project), no type with two
  indexers — which is why the indexer variations are spread over five structs rather than
  overloaded in one — no partial member of any kind, no partial type with both parts in one
  file, and no `file` type. The partial struct's two parts are in two files on purpose.
- **Every declaration is reached from a use.** Each file ends with a `...Use` class that
  calls what the file declares, including the members that have no declaration: the
  unwritten constructor is invoked, the default interface implementation is called, the
  synthesized `Deconstruct` is bound by a positional pattern, and the inline array's
  element access is written five times. A row that says "the index records nothing here" is
  then a claim about a use site, not a suspicion about dead code.
- **Every construct names its clause.** The comment at the top of each file states the
  clause's rule in the standard's own terms and says where the post-standard language
  departs from it — 16.4.8 forbids struct field initializers and C# 10 allows them, 16.2.3
  forbids a ref struct implementing an interface and C# 13 allows it — so a reader can tell
  the standard's surface from the language's.
