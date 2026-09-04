# `Types` — clauses 8 and 9 of the surface corpus

Every type kind the standard names, and every category of variable, in sixteen files. The
slice is `POPULATION.tsv` rows whose `area` is `ecma-8` (types) or `ecma-9` (variables) —
123 rows, of which 61 are marked `hazard`.

| File | Clauses |
|---|---|
| `ReferenceTypes.cs` | 8.1 type categories; 8.2.2 class types; 8.2.3 `object`; 8.2.5 `string`; 8.2.6 interface types; 8.2.7 array types; 8.2.8 delegate types |
| `Homonyms.cs` | 8.2.1 — the general rules for reference types, as a naming hazard |
| `DynamicType.cs` | 8.2.4 and 8.7 — `dynamic` |
| `ValueTypes.cs` | 8.3.1–8.3.9 value, struct, simple, integral, floating-point, decimal and bool types; 8.3.13 boxing; 8.8 unmanaged types |
| `Enumerations.cs` | 8.3.10 — enumeration types |
| `TupleTypes.cs` | 8.3.11.1–8.3.11.3 — tuple types, elision, runtime representation |
| `NullableValueTypes.cs` | 8.3.12 — nullable value types and lifted operators |
| `ConstructedTypes.cs` | 8.4.1–8.4.5 constructed types and constraints; 8.5 type parameters |
| `ExpressionTreeTypes.cs` | 8.6 — expression tree types |
| `Nullability.cs` | 8.9.1–8.9.5.3 — nullable reference types, the nullable context, null states |
| `VariableCategories.cs` | 9.2.1–9.2.9.2 — every variable category the standard names |
| `DefaultValues.cs` | 9.3 — default values |
| `DefiniteAssignment.cs` | 9.4.1–9.4.4.21 — definite assignment, statement by statement |
| `DefiniteAssignmentExpressions.cs` | 9.4.4.22–9.4.4.34 — the expression forms, including the ones that declare |
| `VariableReferences.cs` | 9.5 variable references; 9.6 atomicity |
| `RefVariables.cs` | 9.7.1–9.7.2.8 — reference variables, returns, and ref safe contexts |

Every construct carries the clause number it comes from in its doc comment, so a reader can
walk from a census row to the code without a map.

## Properties this project is built to have

- **One type name at one arity.** All 101 type declarations were checked mechanically:
  no name appears at two arities anywhere in the project. `TyPair<TFirst, TSecond>` has no
  non-generic twin, and the arity pair that would provoke a refused write lives in a
  quarantine project instead. The only repeated simple name is `TyHomonym`, deliberately
  declared once in `Surface.Types.Alpha` and once in `Surface.Types.Beta` — that is 8.2.1's
  hazard, and the two differ in namespace, not in arity.
- **One indexer per type.** Two types declare a `this[int]`: `ITyMeasurable` and `TyRuler`.
  Neither declares a second.
- **No partial anything, and no file-local type.** Both shapes kill an indexing run, and
  neither appears here — nor does the word `partial`.
- **It compiles, and its warnings are deliberate.** Three warnings, each the observable
  effect of the clause that produces it: `CS0693` for 8.5's shadowed type parameter, and
  `CS0162` twice for 9.4.4.21's constant condition. Nothing else warns.
- **Names are unique across the corpus by prefix.** Clause 8's types are `Ty…` (interfaces
  `ITy…`), clause 9's are `Var…`. Twenty-two projects are indexed in one run and every one
  of them wants to call something `Point`.

## What the hazards are for

A hazard row was judged able to make two declarations want one identity string. The three
kinds here are worth naming, because a query over the indexed corpus has to distinguish
them:

1. **One type, two spellings.** `object`/`System.Object`, `string`/`System.String`,
   `int`/`System.Int32`, `nint`/`System.IntPtr`, `int?`/`System.Nullable<int>`,
   `(int, int)`/`(int A, int B)`/`System.ValueTuple<int, int>`, and an eight-element tuple
   against its nested `TRest` form. Each pair must **merge** to one type identity. `dynamic`
   is the same shape with the opposite answer: it erases to `object` in metadata, and an
   index that cannot express it should say so rather than quietly record `object`.
2. **Two declarations, one name.** Repeated deliberately in sibling scopes, because that is
   what an ordinal-numbered identity gets wrong: two locals named `x` in sibling blocks, two
   `for` loops each declaring `index`, three switch sections each declaring `value`, two
   catch clauses each declaring `error`, three `using` declarations each named `handle`,
   three `foreach` loops each declaring `item`, two lambdas each with a parameter `n`, two
   local functions named `Describe` beside a member method of that name, two labels named
   `Finish` in two methods, and — the one that needs no scope trick — `TyDoubleNamed`, which
   declares `Name()` and `ITyNamed.Name()` at once. Each must **separate**.
3. **A declaration written nowhere.** A struct's implicit parameterless constructor, its
   implicit `System.ValueType` base, an enum's `System.Enum` base, a record struct's
   generated properties, the `value` parameter of a property setter, the unnamed temporary
   an `in` argument makes from a value, and the constructor a target-typed `new()` calls.
   Whether these mint rows at all is the question; that they mint *one* row each is the
   claim.

## What is deliberately absent

- The **arity pair** (`TyPair` beside `TyPair<T>`) — quarantined, and clause 8.4 is fully
  exercised without it.
- A **nested type whose namespace-qualified name collides with a namespace-plus-type name**
  — `Surface.Types.Outer.Inner` reachable both as a nested type and as a type in a nested
  namespace. Roslyn's fully-qualified display string is identical for the two, so an
  identity built from that string would take a refused write with different values behind
  it, which kills the run. It is not one of the five known shapes; it is recorded here as a
  prediction instead of being written.
- **Pointer types**, which 8.1 names and clause 23 owns.
