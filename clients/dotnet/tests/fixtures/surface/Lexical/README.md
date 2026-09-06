# `Lexical` — clause 6 (lexical structure) and clause 7 (basic concepts)

Thirty-two source files covering the 70 census rows whose `area` is `ecma-6` or `ecma-7`: 44
exercised, 26 recorded `not-applicable`, none quarantined. Every construct names its clause
in a comment, so a reader can walk from a census row to the code that produces it.

The project is an **application**, not a library — `OutputType` is `Exe` and
`StartupObject` names `Surface.Lexical.Concepts.LexEntryPoint`. Clause 7.1 is a
*declaration* row, and a class library would index the same `Main` with nothing marking it
as the one the runtime calls. `Nullable` is `disable` for the same kind of reason: clause
6.5.9's `#nullable` regions are only observable against an off baseline.

## What is where

| Directory | Clauses | Holds |
|---|---|---|
| `Programs/` | 6.1 | one namespace declared by two files, one spelling each |
| `Trivia/` | 6.3.2–6.3.4 | every line terminator, every kind of comment, every white-space character |
| `Tokens/` | 6.2.5, 6.4.2–6.4.6 | the grammar ambiguities, identifiers, keywords, every literal form, every operator |
| `Directives/` | 6.5.1–6.5.10 | definition, conditional, diagnostic, region, line, nullable and pragma directives |
| `Concepts/` | 7.1, 7.3, 7.7.1 | the entry points, and every declaration space with one name declared in six of them |
| `Members/` | 7.4.1–7.4.8 | one file per kind of container, each declaring every member kind it may |
| `Access/` | 7.5.2, 7.5.4 | the six declared accessibilities, and the four spellings of a protected access |
| `Overloads/` | 7.6 | overloads across every part of a signature, and the five parts that are not in one |
| `Hiding/` | 7.7.2.2, 7.7.2.3 | hiding through nesting and through inheritance, with the references that select |
| `Names/` | 7.8.1–7.8.3 | twelve spellings of one type name, one unqualified name in three namespaces |

## The properties this project is built to have

- **Every declaration has a reference.** Each file ends with a member that reads the
  others, so no symbol here is declared and never reached — a reference count of zero means
  the resolution broke, not that the fixture is unusual.
- **Nothing here is one of the five quarantined shapes.** No type name at two arities, no
  type with two `this[...]`, no partial member or partial type, no `file` type. Checked
  mechanically as well as by eye: 126 declared type names, none at two arities, at most one
  indexer per type.
- **Every name that is repeated is repeated on purpose.** `LexBranch` three times (6.5.5,
  one compiled), `LexShared` three times (7.8.2, three namespaces), `Leaf` twice (7.8.3,
  once nested and once namespaced), `LexHidden` twice (7.7.2.2), `Nested` five times (five
  containers). Nothing else collides.
- **Each hazard is a pair of spellings, not a pair of clauses.** Where the census marked a
  row `hazard=yes`, the file holds two declarations or two references that a plausible
  identity string cannot separate, and the file's own comment says which.

## What the compiler actually does, which is not always what clause 6 says

Four of these were found by building the shape rather than by reading, and each one is a
verdict a query over the index has to match rather than a curiosity:

- **Formatting characters are stripped from the emitted name.** `zero<U+200D>width` and
  `zerowidth` are one identifier (CS0102 if both are declared), and the name in metadata is
  `zerowidth` — the U+200D is in the source and not in the symbol. So the declaration's
  identifier as written is three bytes longer than the name it declares.
- **Canonically equivalent identifiers are two identifiers.** `LexCanonicalPair` declares
  `café` twice, once with U+00E9 and once with `e` + U+0301, and both spellings are present
  in the built assembly once each. An index that normalises to NFC mints one name for two
  fields.
- **A supplementary-plane letter cannot be an identifier character.** U+1D504 is class Lu
  and 6.4.3 admits it, but Roslyn lexes identifiers over UTF-16 code units and reports
  CS1056 for both the literal and the `\U0001D504` spelling. `LexNonAsciiIdentifiers` uses
  U+FF21, U+0130 and U+FB01 instead — one code unit each, and each one a folding hazard.
- **`(x)(y)` and `(x)-y` are settled by the token, not by the name.** A parenthesised name
  followed by `(` is a cast whatever the name denotes: `(negate)(-x)` with `negate` a
  delegate-typed local is CS0118 and `(Holder.Negate)(-x)` is CS0426, both looking for a
  *type*. And `-` is not one of the tokens that keeps a parenthesised name a cast, so
  `(T)-x` is a subtraction even when `T` is a type (CS0075). `LexCastOrInvoke` holds all
  three readings, the reachable ones written and the unreachable one named.

Three shapes the clauses reach that no compiling C# can express, recorded here rather than
written: a type named `file`, `required` or `scoped` (CS9056, CS9029, CS9062 — all three are
legal *member* names and are in `LexContextualKeywordMembers`); a protected member read
through a base-typed receiver from a derived class (CS1540, named in
`LexProtectedAccess.ThroughABaseInstance`); and a namespace and a type of one name in one
enclosing namespace (CS0101), which is the only way one fully qualified name could be
reached two ways and is why 7.8.3's exact collision is unbuildable.

## Bytes that matter

`Trivia/LexTerminators.cs` and `Trivia/LexWhiteSpace.cs` carry characters an editor may
silently rewrite. The terminators file uses U+000D alone, U+0085, U+2028 and U+2029 as line
terminators; `LexTerminators.LastMember` is on **line 29** counting the way 6.3.2 counts and
on line 23 counting U+000A alone. Reformatting either file to LF changes what it tests.
`Tokens/LexIdentifiers.cs` carries five Cf characters and a decomposed `é`, none of which
renders.
