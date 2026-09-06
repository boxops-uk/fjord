# `Names` — spelling and lexical identity, and the tokens nobody wrote

Nine source files for four mechanisms of the corpus plan: **M15** (two names that render
identically are two symbols), **M14** (the contextual keywords — `var`, `nameof`, a using
alias's own name, and a user type named `var`), **M27** (a type spelled as a keyword has no
name node and is therefore no reference), and **M35** (whether `G < A , B > (7)` is a
generic call or two comparisons).

It is a **hazard project that must index to completion**. Every mechanism here produces a
wrong or a missing row rather than a refused write: no type is declared at two arities, no
member is partial, no type is `file`-local, and no type declares two indexers or two
same-named terms. A measured run over it encodes 2,374 facts, writes 49 declarations,
resolves 97 references and finishes.

## What is where

| Directory | Mechanism | What it holds |
|---|---|---|
| `Spelling/NamesRenderedAlike.cs` | M15 | Two constants named `Café` and two classes named `NamesCafé`, one of each pair composed (U+00E9) and one decomposed (`e` + U+0301). |
| `Spelling/NamesPredefinedSpelling.cs` | M27 | Three methods whose every type is a `PredefinedTypeSyntax` keyword. |
| `Spelling/NamesInferredSpelling.cs` | M27, M14 | The same three methods with every local spelled `var`. A matched pair with the file above — nothing else differs. |
| `Spelling/NamesFrameworkSpelling.cs` | M27 | The same types again as `System.Int32` / `System.String`, the third of three spellings. |
| `Keywords/NamesNameOf.cs` | M14 | Three `nameof` expressions, each an identifier that binds to nothing. |
| `Keywords/NamesAliasDirective.cs` | M14 | Two using-alias directives, whose alias names bind to nothing. |
| `Keywords/NamesInferredEdges.cs` | M14 | The two `var`s whose inferred type is not an ordinary named type — an anonymous type, and a `foreach` element. Kept out of the matched pair on purpose. |
| `Keywords/NamesVarTaken.cs` | M14 | `class var`, in a sibling namespace, taking the token back from the language. |
| `Ambiguity/NamesGenericOrComparison.cs` | M35 | The three readings the lookahead rule of 6.2.5 produces from one token shape. |

## The measurements

Taken with `Boxops.Fjord.Indexer --dry-run`, which encodes every fact and connects to
nothing. They are the assertions, not illustrations.

- **`Unresolved` is 5, by construction**: one per `nameof` (three) and one per using-alias
  directive (two). `ledger` asserts a global zero and this project cannot be added to it —
  `LedgerTests` pins those counts. The corpus therefore asserts a number *per project*.
- **`InexpressibleTypes` and `InexpressibleKinds` are both 0.** Nothing here is dropped.
- **The M27 inversion, per file, indexed alone**:

  | file | types named | `csharp.TypeLocation` | `codemarkup.FileXRef` | references |
  |---|---|---|---|---|
  | `NamesPredefinedSpelling.cs` | `int`, `string`, `bool`, `double`, `object` | **0** | 2 | 8 |
  | `NamesInferredSpelling.cs` | none — every local is `var` | **5** | 7 | 13 |
  | `NamesFrameworkSpelling.cs` | `System.Int32`, `System.String` | 6 | 6 | 11 |

  The file that names five types writes no type reference at all; the file that names none
  writes five. That inversion is the cheapest single assertion in the corpus, and the two
  files are line-for-line identical apart from the spelling of their locals.
- **M15 is two symbols, measured in the emitted blocks**: the composed byte sequence
  `Caf\xc3\xa9` and the decomposed `Cafe\xcc\x81` both appear (119 and 118 times), so
  Roslyn hands the walk two `Name` strings where 6.4.3 says an identifier is compared after
  conversion to normalization form C. Nothing is normalised anywhere between the source and
  the descriptor.

## Properties this project is built to have

- **Every mechanism is written, not described.** The one construct that is stated and not
  compiled is `var n = 3;` inside `Surface.Names.VarTaken`, which is
  `error CS0029: Cannot implicitly convert type 'int' to 'Surface.Names.VarTaken.var'` —
  the proof that a user type named `var` takes the token back rather than sharing it. It is
  recorded in `Keywords/NamesVarTaken.cs` with its error code, because a fixture has to
  compile to be a gate.
- **`class var` is a sibling of the inference files, never a parent.** A type named `var`
  in scope removes inference from that scope. Declared in `Surface.Names`, it would rewrite
  `Spelling/NamesInferredSpelling.cs` into a build error and silently delete M14's positive
  half. The namespace separation is this project's only structural requirement.
- **The M27 pair stays a pair.** Any member added to one of the two spelling files must be
  added to the other, or the reference-count inversion stops being a measurement. The two
  `var`s that do not have a keyword twin live in `Keywords/NamesInferredEdges.cs` for that
  reason.
- **One warning, and it is evidence.** CS8981 on `class var` is not suppressed: the build
  log carrying it is what says the construct compiled as written.
