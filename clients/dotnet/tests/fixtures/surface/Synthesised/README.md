# `Synthesised` — the members no source declares, and the compiler's own enumerations

This project's slice of `POPULATION.tsv` is the pinned compiler's enumerations rather than a
clause of the standard: every row whose id is a member of `SymbolKind`, `TypeKind`,
`MethodKind` or `LanguageVersion`. (The `SyntaxKind` rows of the same `roslyn` area belong to
a sibling project — a syntax kind is a node you can point at, and everything here is chosen
for the opposite reason.)

So the subject is **what a C# compilation contains that no file declares**. A record
declaration of one line produces nineteen members; a class with no constructor has one; an
auto-property is three symbols at one span; a field-like event's backing field has the
event's own name. None of it has a `DeclaringSyntaxReference`, all of it has the declaring
type's identifier as its `Location`, and an index built by walking declaration nodes holds
none of it. That is not a defect to fix here — it is the fact the corpus exists to state,
and it is stated by writing the source that provokes each one.

## What is here, and where

| File | Clauses / kinds |
|---|---|
| `Records.cs` | 15.x records — positional, derived, `record struct`, one with a hand-declared positional member, one that declares every synthesisable member. `Equals`/`GetHashCode`/`ToString`/`Deconstruct`/`PrintMembers`/`<Clone>$`/copy constructor/`op_Equality` |
| `PrimaryConstructors.cs` | C# 12 primary constructors on a class, a derived class, a struct and a generic class; captured versus uncaptured parameters; the struct's synthesised parameterless constructor beside the declared-in-the-header one |
| `ImplicitMembers.cs` | 15.11.5 implicit constructor, 15.12 static constructor (declared and synthesised), 15.13 finalizer, 15.7.4 auto-properties and the `field` keyword, 15.8.2 field-like events, 19.4 an enum's `value__`, 21.2 a delegate's four synthesised members, 15.9 one indexer |
| `StateMachines.cs` | 13.15 iterators, 15.15 async methods, async iterators, lambdas, anonymous methods, local functions — six methods that make the compiler emit types with names no identifier can spell |
| `MethodKinds.cs` | `Ordinary` (a six-way overload set), `UserDefinedOperator` (including `checked` and `>>>`), `Conversion`, `ExplicitInterfaceImplementation`, `EventAdd`/`EventRemove`, `DelegateInvoke`, `BuiltinOperator`, `ReducedExtension`, and the `extern`/`DllImport` shape that stands in for the unreachable `DeclareMethod` |
| `SymbolKinds.cs` | `Local`, `Label`, `Discard`, `RangeVariable`, `Parameter` (including a setter's undeclared `value`), `Field`, `TypeParameter` — each written in the shape where its name repeats |
| `TypeKinds.cs` | `Class`, `Interface`, `Struct`, `Enum`, `Delegate`, and the five that appear only as references: `Array`, `Pointer`, `FunctionPointer`, `Dynamic`, `TypeParameter` |
| `ExtensionBlock.cs` | `TypeKind.Extension` — C# 14 extension blocks, alone in their own file on purpose |
| `Aliases.cs` | `SymbolKind.Alias`: a using alias of a type, a constructed type, a namespace, a tuple, an array and a pointer; a `global using` alias; and `extern alias`, whose `/reference` option the csproj supplies |
| `Namespaces.cs` | `SymbolKind.Namespace` declared three times, a nested namespace, and one type name declared in two namespaces |
| `Preprocessing.cs` | `SymbolKind.Preprocessing` — a file-scoped `#define`, an `#undef`, disabled branches, a `[Conditional]` method |
| `ErrorTypes.cs` | `SymbolKind.ErrorType` / `TypeKind.Error` — unresolved names in a program that compiles |
| `AssemblyAndModule.cs` | `SymbolKind.Assembly` and `SymbolKind.NetModule`, reached the only way C# names them: an attribute target |
| `PartialSplitA.cs`, `PartialSplitB.cs` | a partial type across two files — the safe half of the shape whose one-file form is quarantined |
| `VersionLadder.cs` | one nested type per numbered `LanguageVersion`, each holding a construct that first became legal at that version, C# 1 through C# 14 |

## Four properties this project is built to have

- **Every synthesised member has a declared twin somewhere in the project.** `SynEntry`
  synthesises `ToString`, `Equals`, `GetHashCode`, `Deconstruct` and `PrintMembers`;
  `SynHandWritten` declares all five. `SynFieldEvent.Changed` has synthesised accessors and
  `SynFieldEvent.Reset` has declared ones. `SynImplicitStaticInit` has a synthesised static
  constructor and `SynStaticInit` a declared one. So a query does not have to know what
  synthesis is: it can *subtract*, and the difference is the answer.

- **Every kind that repeats a name, repeats it.** Six `Measure` overloads in one type; two
  locals named `item` in one method; two labels named `Retry` in one type; two range
  variables named `entry` in one method; six discards named `_` in one method; `SynTwice`
  in two namespaces; `SynAliasedInt` in two files; a declared parameter named `value` beside
  two synthesised ones; a field named `Changed` beside the event `Changed`. Every one of
  these is legal C#, and every one of them is a pair of declarations that an index keyed on
  anything less than the full context mints one identity for. They are here because that is
  the census's `hazard` column, and a hazard nobody wrote is a hazard nobody measured.

- **None of the five quarantined shapes is written.** No type name at two arities (checked
  mechanically, 93 distinct names, none repeated at a differing arity); exactly two
  indexers, in two different types; no partial member of any kind — the C# 3 partial method,
  the C# 13 partial property and the C# 14 partial constructor and event are named in the
  version ladder's comments and deliberately not declared; no partial type with both parts
  in one file; no `file` type. Where a `LanguageVersion` rung's marquee feature *is* one of
  the five, the rung is filled by another feature of the same version and the comment says
  which one was skipped.

- **The build's own properties are part of the fixture.** Four rows in this slice are
  reachable only through the csproj, and each is commented there against its row:
  `LangVersion=preview` is `LanguageVersion.Preview` and is what makes C# 14 parse under a
  pinned Roslyn 4.14 (`14.0` there is CS1617, and would fail the project rather than one
  construct); `AllowUnsafeBlocks` is what the pointer and function-pointer kinds need;
  `GenerateDocumentationFile` is what turns the unresolved crefs in `ErrorTypes.cs` into
  CS1574 warnings, so "these names bind to nothing" is checkable in the build log rather
  than asserted; and a target adding `Aliases="global,SynRuntime"` to `System.Runtime` is
  the `/reference` option `extern alias` binds to, without adding a reference.

## What warns, and why that is the point

`dotnet build Synthesised/Synthesised.csproj` exits 0 with **13 warnings and 0 errors**, and
the warnings are the evidence rather than noise:

| Warning | Count | What it confirms |
|---|---|---|
| CS1574 | 5 | five `cref`s in `ErrorTypes.cs` bind to nothing — the project's only error types, in a program that compiles |
| CS1572, CS1573 | 1 each | a `param` tag naming a parameter that does not exist, and the parameter that therefore has none |
| CS9124 | 2 | which primary-constructor parameters of `SynSpan2` are *captured* into a synthesised field — a distinction the source cannot show |
| CS9107 | 1 | `SynDerivedAnchor`'s parameter is captured *and* passed to the base constructor, so two synthesised fields may hold it |
| CS0693 | 1 | `SynConstrained.Nested`'s type parameter shadows its type's — two symbols, one name, one lexical region |
| CS9099 | 1 | a lambda's default parameter value that its target delegate type does not carry |
| CA2255 | 1 | the C# 9 module initializer in the version ladder, which an analyzer reserves for application code — kept, because the feature is a population row and this is not an application |

A corpus of deliberately odd code warns. A run over this project that reports *no* warnings
has lost the files those warnings came from, and the CS1574 count is the cheapest check that
`ErrorTypes.cs` was compiled at all.
