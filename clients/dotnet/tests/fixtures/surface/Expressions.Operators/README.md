# `Expressions.Operators` — the operators, and the expressions built out of them

This project is the operator half of ECMA-334 draft-v9 clause 12. Its slice of
`POPULATION.tsv` is every `ecma-12` row about an operator or an expression form assembled
from operators: unary (12.9), arithmetic (12.12), shift (12.13), relational, equality and
type-testing (12.14), logical (12.15), conditional logical (12.16), null coalescing
(12.17), the throw expression (12.18), declaration expressions (12.19), the conditional
operator (12.20), anonymous functions (12.21 and 12.8.24), query expressions (12.22),
assignment in all its forms (12.23), constant expressions (12.25) and boolean expressions
(12.26) — plus the machinery clause 12.4 states them in terms of: operator overloading,
unary and binary overload resolution, candidate user-defined operators and lifted
operators. The primary-expression rows (12.8.1 through 12.8.23) and the classification,
binding, member-lookup and function-member rows (12.2, 12.3, 12.5, 12.6) belong to a
sibling project.

## Why the area is worth its own project

An operator use names nothing. `a + b` is a reference to a member called `op_Addition` and
the source contains no identifier at all; `a && b` is a reference to `op_False` and then to
`op_BitwiseAnd`; `window[1..^1]` is a reference to `Length` and `Slice`; `await x` is a
reference to `GetAwaiter`, `IsCompleted`, `OnCompleted` and `GetResult`; a query's `where`
is a reference to a `Where` method. Everywhere else in the language a reference is spelled
with the name of what it reaches. Here it is spelled with punctuation, and an index that
records references by matching identifiers records nothing.

So this project is arranged as two populations that meet:

| Directory | Holds |
|---|---|
| `Declarations/` | one of every overloadable operator, declared on a small coherent type: `OpMoney` (arithmetic, increment, relational, conversions, and the C# 11 `checked` variants), `OpBits` (logical and shift, including `>>>` and the C# 11 relaxed shift), `OpFlag` (`true`/`false`, and so `&&`, `\|\|` and every boolean-expression position), `OpTally` (the C# 14 instance `operator +=` and `operator ++()`), `OpBaseGauge`/`OpDerivedGauge` (the 12.4.6 candidate-set walk), `OpReading` (operators declared in a C# 14 extension block), `OpSpan` (the countable/sliceable shape `^` and `..` need), and the await pattern |
| `Uses/` | the expressions that bind to them, one method per clause, each written twice where the clause has a predefined form and a user-defined one — so the corpus holds both the case with a member reference behind the token and the case with none |
| `Anonymous/` | lambda and anonymous-method expressions: every signature form of 12.21.2, both body forms, overload resolution driven by an argument with no type of its own, and the outer-variable capture of 12.21.6 |
| `Query/` | the query-expression pattern of 12.22.4 declared from scratch, and every clause of the translation written against it |
| `Assignment/` | simple, compound, deconstructing, null-coalescing and `ref` assignment, deconstructors, and event assignment |

## Properties this project is built to have

- **Every query binds inside the corpus.** No file here imports `System.Linq`.
  `Query/OpQueryPattern.cs` declares `Select`, `Where`, `SelectMany` (both arities),
  `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `GroupBy` (both arities),
  `Join`, `GroupJoin` and `Cast` as extension methods on `OpQuerySource<T>`. A query that
  resolved to `Enumerable.Select` would not compile, so every clause keyword in
  `Query/OpQueryExpressions.cs` is a reference a gate can assert lands on a declaration in
  this project.

- **Every `checked` operator has its unchecked twin in the same type, and both are used.**
  `operator +` and `operator checked +` differ in source by one modifier and in metadata by
  one name (`op_Addition`, `op_CheckedAddition`). `Uses/OpOverloadResolutionUses.cs` writes
  the two uses character-for-character identically apart from the keyword that wraps them.
  This is the area's sharpest identity question and it is asked in one place on purpose.

- **The same token is used at two referents wherever the language allows it.** `+=` reaches
  a static `op_Addition` on `OpMoney` and an instance `op_AdditionAssignment` on `OpTally`;
  `++` reaches `op_Increment` on one and `op_IncrementAssignment` on the other; `-` reaches
  a unary and a binary declaration on `OpTemperature`; `<<` reaches two declarations on
  `OpBits` that differ only in an operand type; `==` reaches `op_Equality` on `OpMoney`,
  reference equality on `OpTag`, which declares none, and a per-element chain of
  `op_Equality` calls on a tuple of `OpMoney`.

- **Declarations inside expressions repeat their names.** Four lambdas in
  `OpLambdaExpressions.RepeatedParameterName` all declare a parameter called `value`; two
  queries in `OpQueryExpressions.RepeatedRangeVariableName` both declare a range variable
  called `part`; two blocks in `OpNullAndConditionalUses.DeclarationExpressions` both
  declare an `out` variable called `parsed`; two loops in
  `OpOuterVariables.RepeatedCapturedName` both declare and capture a `perIteration`. Each
  set is several declarations with one name inside one containing member, told apart only
  by scope.

- **None of the five known refused-write shapes appears here.** No type name at two
  arities, no type with two indexers (`OpTarget` and `OpSpan` have one each), no `partial`
  member or type, no `file`-local type. `grep` for `partial`, `dynamic` and `file class`
  over this directory returns nothing.

- **It compiles against the framework reference alone**, with no `PackageReference` and no
  `ProjectReference`. Two warnings are deliberate and are the only ones: `CS1718` on the
  NaN-unordered comparison in `Uses/OpRelationalUses.cs`, which is the point of that line,
  and `CS8714` in `Query/OpQueryPattern.cs`, where `GroupBy`'s key parameter is
  deliberately left unconstrained so the pattern's shape matches the clause rather than
  `Enumerable`'s.

## Reading from a census row to the code

Every construct carries the clause number it comes from in a comment, so a row of
`POPULATION.tsv` can be walked to the code that exercises it. Clause 12.4.7 (numeric
promotions) is the one clause here with no construct of its own: the promotions it
describes are conversions the compiler inserts into the mixed-type arithmetic in
`Uses/OpArithmeticUses.cs`, with no operator and no member behind them.
