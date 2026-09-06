# `Patterns` — clause 11, and the declarations nobody declares

Ten files for ECMA-334 draft-v9 **clause 11**, patterns and pattern matching. One file per
numbered subclause, plus the vocabulary the patterns are applied to and one file for the
pattern forms the language grew after the clause was written.

| File | Clause | What it is for |
|---|---|---|
| `PatternInputs.cs` | 11.1 | The pattern *input* side: the shapes, constants, properties, `Deconstruct` overloads, `Length`/indexer/`Slice`, `ITuple` and record the patterns bind. No patterns in it |
| `PatternForms.cs` | 11.2 | Every pattern form as arms of one `switch` expression, and again as case labels of one `switch` statement |
| `PatternGrammar.cs` | 11.2.1 | Every grammatical position that admits a pattern, and witnesses for "applicable to" and evaluation order |
| `DeclarationPatterns.cs` | 11.2.2 | `E is T t`, in every position, including one whose variable is never read |
| `ConstantPatterns.cs` | 11.2.3 | Literals, `null`, a `const` field, an enum member, a `const` local, and two constants with one simple name |
| `VarPatterns.cs` | 11.2.4 | `var v`, `var (a, b)`, `var _`, and a pattern variable that takes a field's name |
| `PositionalPatterns.cs` | 11.2.5 | Two `Deconstruct` overloads told apart by a comma, an extension one, a compiler-written one, `ITuple`, and a tuple |
| `PropertyPatterns.cs` | 11.2.6 | `{ M: p }` over a property, a field, an interface member, a nullable, a nesting, an extended `{ A.B: p }`, and two properties with one simple name |
| `DiscardPatterns.cs` | 11.2.7 | `_` in every position that admits it, and the discard designations beside it |
| `ExtendedPatternForms.cs` | 11.2, post-standard | The type, relational, logical, parenthesised, list and slice patterns — clause 11.2's grammar as the language has it |

## Why clause 11 is worth a project of its own

Every other clause that declares something declares it with syntax built for the purpose: a
member, a parameter list, a local declaration statement. **Clause 11 declares locals with an
identifier in the middle of an expression.** A pattern variable has no declaration
statement, no position in a member list, and no name anything outside the compilation can
hold — so the only trace it leaves is a *use*, and a pattern variable that is never read
leaves none at all. `DeclarationPatterns.IsBlob` and `VarPatterns.CountOf` are the two that
declare and are never read; they are here so that "nothing was recorded" is a claim about
named methods rather than a suspicion.

And **clause 11 references members without writing their names.** A positional pattern binds
a `Deconstruct` and writes no `Deconstruct`; a list pattern binds a `Length`, an indexer and
a `Slice` and writes none of the three; a property pattern does write its member name, but
with no receiver, so the name resolves against a static type that is somewhere else in the
expression. `PatListForms.Shape` is the widest gap in the language on this count: three
members reached, zero names at the use site.

## Three properties this project is built to have

- **Everything compiles, and nothing here is a compiler error.** `dotnet build` is clean —
  no errors and no warnings. That is worth stating for a pattern corpus, because the tempting
  specimens are illegal: a designation under `not` or `or`, a pattern not applicable to its
  input's type, a subsumed arm. Each of those is named in a comment at the place it would
  have gone, so the boundary is in the corpus even though the code cannot be.
- **None of the five refused-write shapes appears.** No type name at two arities (`PatBox<T>`
  is the only generic and has no non-generic twin), no type with two indexers (`PatRun` and
  `PatPair` have one each), no `partial` anything, and no `file` type. Clause 11 needs none
  of them.
- **Every same-name collision here is deliberate, and each is a pair a *static type* tells
  apart.** `Threshold` is two `const` fields, `Weight` is two properties, `Deconstruct` is
  two methods on one type, `form` is seven pattern variables in one member, and `_depth` is a
  field and a pattern variable. None of the five is ambiguous to the compiler; all five are
  ambiguous to anything that identifies a declaration by name and container.

## What the compiler refuses, recorded because it cannot be written

Four forms belong to clause 11 and are not in the corpus, because no compiling C# contains
them. Each is named in a comment where it would have gone:

- `input is _` — CS0246. As the whole pattern of an `is` expression, `_` is read as a type
  name; the discard pattern is not admitted there.
- `case _:` — CS0103. Nor as a `switch` *statement*'s case label. A `switch` expression's
  `_ =>` arm is the only place a bare `_` stands as a complete pattern.
- `is not [_, .. var tail]`, `is PatCircle c or PatSquare` — a `not` or an `or` pattern may
  not declare.
- `pair is (1, _) whole` on an `ITuple` implementer — CS8129. A designation takes the
  `ITuple` path away and demands a `Deconstruct`.

One more shape is legal and reads as though it should not be: two sibling `if` statements
cannot both declare `t`, because a pattern variable in an `if` condition is scoped to the
enclosing *block* — but two `switch` sections can, because a case label's variable is scoped
to its section. Every repeated designation name in this project is written across switch
sections or switch arms for that reason.
