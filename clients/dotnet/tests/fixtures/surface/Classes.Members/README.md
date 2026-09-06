# `Classes.Members` — clause 15's function members

Twenty-five source files covering the 74 census rows of `ecma-15` that are about **function
members**: methods, properties, events, indexers, operators, instance constructors, static
constructors, finalizers, accessors, async functions and iterators. 70 exercised, 3 recorded
`not-applicable`, 1 quarantined. The class declaration itself, fields, constants and nested
types (rows `15`, `15.1`, `15.2.*`, `15.3` through `15.3.9.7`, `15.4` and `15.5.*`) belong to
the sibling project for this area and are not covered here.

Every construct names its clause in a comment, and every file opens with a paragraph saying
which identity collision it was written to provoke, so a reader can walk from a census row to
the code and from the code to the claim a query has to settle.

## What is where

| Directory | Clauses | Holds |
|---|---|---|
| `Methods/` | 15.6.1–15.6.11 | every method shape: parameter modifiers, the overload matrix, the four virtuality modifiers, `extern`, extension methods in both spellings, and every body form including local functions |
| `Members/` | 15.3.10.1–15.3.10.6 | the six reserved-name families, each written in the one form the compiler allows |
| `Properties/` | 15.7.1–15.7.6 | every accessor form — `get`, `set`, `init`, automatic, expression-bodied, narrowed, and the C# 14 `field` keyword — plus a three-deep accessor override chain |
| `Events/` | 15.8.1–15.8.5 | field-like and custom events, static and instance, and an abstract/virtual/sealed chain where an override swaps a generated accessor pair for a written one |
| `Indexers/` | 15.9.1, 15.9.2 | fourteen types with **one indexer each**, spanning every key type, `params`, `init`, `ref` return, `Index`, `Range` and an override chain |
| `Operators/` | 15.10.1–15.10.4 | every overloadable token, the `checked` forms, the C# 14 compound-assignment and instance-increment forms, and eight conversion operators of which five are called `op_Explicit` or `op_Implicit` |
| `Constructors/` | 15.11.1–15.11.5, 15.12, 15.13 | five `.ctor`s in one type, a primary constructor, a generated default one, an absent one, a `.cctor` beside a parameterless `.ctor`, and three finalizers |
| `Async/` | 15.14.1, 15.14.2 | every async return type, and the task-type builder pattern written twice by hand — sixteen members bound by name and referenced by nothing |
| `Iterators/` | 15.15.1–15.15.6.2 | iterator methods, an iterator property, an iterator indexer and an iterator operator; hand-written enumerators and enumerables including the three-`GetEnumerator` shape |

## The properties this project is built to have

- **It compiles, and it produces exactly one warning.** `dotnet build` exits 0 with
  `1 Warning(s), 0 Error(s)`: CS0465 on `MemReservedNamesFree.Finalize()`, which is clause
  15.3.10.5 doing precisely what the clause says it does. Any other diagnostic is a
  regression.
- **Every declaration has a reference.** Each file ends with a `UseAll` or `Use*` member that
  reaches every declaration in it, so no member here is declared and never reached. The three
  deliberate exceptions are named below, and each is a fact rather than an oversight.
- **Nothing here is one of the five quarantined shapes.** Checked mechanically as well as by
  eye: 111 declared type names, none at two arities; 14 types with an indexer, each with
  exactly one; no `partial` anywhere in the project; no `file` type. The one row that can only
  be exercised by such a shape — 15.6.9, partial methods — is recorded `quarantined` and is
  not written.
- **Every hazard is a pair of declarations, not a pair of clauses.** Where the census marks a
  row `hazard=yes`, some type in this project holds two declarations that a plausible identity
  string cannot separate, and that type's file comment says which two and what separates them
  in fact.

### The three members with no callers, on purpose

- **Every member of `MemFutureBuilder` and `MemFutureOfBuilder<TResult>`** (clause 15.14.2).
  The compiler finds them by well-known name; nothing in the source references them. A query
  for their callers must answer *none*.
- **Every finalizer** (clause 15.13). There is no syntax for calling one.
- **Every static constructor** (clause 15.12). There is no syntax for calling one either.

## What the compiler actually does, which is not always what clause 15 says

Each of these was found by building the shape rather than by reading, and each is a verdict a
query over the index has to match rather than a curiosity. The exact diagnostic is quoted so a
reader can re-provoke it.

- **The `ref`/`out` overload pair is not expressible anywhere.** Inside one type it is CS0663,
  *"cannot define an overloaded method that differs only on parameter modifiers 'out' and
  'ref'"* — and `in` versus `ref` is the same error. Across an inheritance boundary it
  compiles, and then the derived declaration **hides** the base one, silently: no CS0108, no
  CS0114, even though the base member is `virtual`. So `Blend(ref slot)` on a
  `MemModifierOverloadDerived` receiver is CS1620 and the base member is reachable only through
  `base.` or an upcast. `Methods/MemOverloads.cs` writes both halves and both spellings of the
  recovery. The consequence for the index is that the two members are never in one overload
  set, so an ordinal counted off a flattened member list would place them in an order no call
  site can observe.
- **A reserved member name is reserved by name *and parameter list*.** CS0082 says *"type
  'PropRes' already reserves a member called 'get_Weight' with the same parameter types"* — so
  `int Weight { get; }` beside `int get_Weight()` is an error and beside
  `int get_Weight(int scale)` is clean, with no warning at all. Five types in
  `Members/MemReservedNames.cs` therefore hold **two members with the same emitted name**,
  separated by nothing but a parameter list: `get_Weight`, `add_Tick`, `get_Item`, `Finalize`
  and `op_Addition`.
- **An operator is not a reserved name but a definition.** `operator +` beside a method called
  `op_Addition` with the same parameter types is CS0111 (*"already defines a member called
  'op_Addition'"*), not CS0082 — because the operator *is* that method. Likewise `~C()` beside
  `void Finalize()`. So clause 15.3.10.6 and 15.3.10.5 fail differently from 15.3.10.2 through
  15.3.10.4, and an index that models operators as methods gets the right answer for the wrong
  reason.
- **`~C()` beside `void Finalize(int)` is completely clean.** Not even CS0465, which a plain
  `Finalize` method does get. `MemReservedFinalizer` has two members whose emitted names are
  both `Finalize` and whose source names are `~MemReservedFinalizer` and `Finalize`.
- **`IndexerName` moves the reservation rather than adding one.** With
  `[IndexerName("Element")]`, the indexer emits `get_Element` and the name `get_Item` is free —
  so `MemRenamedIndexer` declares an indexer *and* a method called `get_Item(int)` with exactly
  the signature that would otherwise have clashed.
- **A property called `Item` and an indexer cannot coexist.** CS0102, *"the type already
  contains a definition for 'Item'"*. So `MemItemProperty` has the name and no indexer, and
  every indexer in the file has the emitted name and no source name.
- **A custom event cannot be read, even by its own declaring type.** CS0079, *"can only appear
  on the left hand side of += or -="*, applies inside the class as well as outside it — a
  field-like event is readable there and a custom one is not. So the two event forms differ in
  which *references* are legal, not only in what the compiler generates.
- **An `in` argument admits no implicit conversion.** `Blend(in slot)` with an `int` will not
  reach `Blend(in long)`; it binds to the `out` overload and is CS1620. Every by-reference
  argument in this project therefore names a variable of exactly the parameter's type.
- **`[LibraryImport]` cannot appear in this project at all.** The source-generated P/Invoke
  requires `static partial`, which is a quarantined shape, and refuses four ways without it:
  CS8795, CS0751, SYSLIB1050 and SYSLIB1062. Clause 15.6.8 is therefore exercised by `extern`
  and `[DllImport]` alone, and its modern spelling belongs to the quarantine project that owns
  partial members.
- **C# 14 needs no `LangVersion` here.** The pinned SDK is 10.0.101, whose default for
  `net10.0` is C# 14, and all four of the features this project wanted compile unmodified: the
  `field` keyword (`Properties/MemAccessors.cs`), user-defined compound assignment and instance
  increment operators (`Operators/`), and extension blocks
  (`Methods/MemExtensionMethods.cs`). None of them is deferred to the Preview project.
- **An iterator block may be an operator, a property getter or an indexer getter.** All three
  compile, and all three are in `Iterators/MemIterators.cs`. The generated state machines'
  demangled names are then `op_Addition`, `get_Rows` and `get_Item` — names clause 15.3.10
  reserves as *members* and clause 15.15 turns into *types*.

## The five collisions worth stating as claims

1. **Three members named `GetEnumerator`, no parameters on any of them** — `MemRange`, clause
   15.15.6.2. A public one returning a struct, and two explicit interface implementations
   returning `IEnumerator<int>` and `IEnumerator`. They differ only in return type, which is
   not part of a C# signature. `foreach` binds the one that implements no interface.
2. **Two members named `Current`, no parameters on either** — `MemCursor`, clause 15.15.5.3,
   and again in `MemExplicitCursor` where both are explicit. This is the collision every
   hand-written enumerator in every codebase has.
3. **Five members called `op_Explicit` and `op_Implicit`** — `MemMeasure`, clause 15.10.4.
   Three `op_Explicit`s taking one `MemMeasure` and returning `int`, `long` and `string`; two
   `op_Implicit`s, one out and one in. Return type is the only separator for three of them.
4. **A `.cctor` spelled like a `.ctor`** — `MemRegistry`, clause 15.12. A static constructor
   and a parameterless instance constructor: same source name, same empty parameter list, one
   keyword between them, and neither may be given a parameter to tell them apart.
5. **Two local functions called `Helper` in one type, neither of them a member of it** —
   `MemBodyLocals`, clause 15.6.11. One in the body of `Route(int)` and one in the body of
   `Route(string)`, with a third and a fourth nested inside `Nested`. Separating them needs the
   enclosing *overload's* ordinal, which is not in any member list a local function appears in.
