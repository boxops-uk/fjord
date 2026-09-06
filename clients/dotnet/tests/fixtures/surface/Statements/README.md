# `Statements` — clause 13, where a walk has to descend

Twenty-eight files for ECMA-334 draft-v9 **clause 13**, statements. Every statement form the
language has, in every syntactic position it can stand in, plus the vocabulary the statements
bind to — because two of them, `foreach` and `using`, reach their targets *by pattern* and
write no name at all.

| File | Clauses | What it is for |
|---|---|---|
| `StatementGrammar.cs` | 13.1, 13.4 | All eight `embedded_statement` positions in the grammar, each holding one braceless statement, plus the two positions they are contrasted with; and the empty statement in the three places it is not merely legal |
| `Reachability.cs` | 13.2 | A declaration in unreachable code, a body with no reachable end point, a `switch` whose every section jumps |
| `Blocks.cs` | 13.3, 13.3.1, 13.3.2 | Sibling blocks that declare one name twice, a nesting that cannot, an empty statement list, and an iterator block |
| `LabeledStatements.cs` | 13.5 | Two labels named `Retry` in one member, a label carrying a declaration, a label jumped to from a nested block |
| `DeclarationStatements.cs` | 13.6.1 | All five declaration statement spellings, in one member, all called `payload` — and the same five with distinct names as a control |
| `LocalVariables.cs` | 13.6.2.1, 13.6.2.2, 13.6.2.3 | Declarator lists, a local that shadows a field, `var` inferring two types under one name, and every explicit type form |
| `VarAmbiguity.cs` | 13.6.2.1 | A type actually named `var`, in a namespace of its own so it cannot break the rest of the project |
| `RefLocals.cs` | 13.6.2.4 | `ref`, `ref readonly` and `scoped ref` locals, a re-aliasing assignment, and a `foreach` whose iteration variable is a ref local |
| `LocalConstants.cs` | 13.6.3 | `Limit` as a const field and as two local constants, every constant type, and a use that binds past both locals to the field |
| `LocalFunctions.cs` | 13.6.4 | `Fold` twice with two signatures, and every body form: static, generic with a constraint, iterator, async, nested, forward-referenced |
| `ExpressionStatements.cs` | 13.7 | Every expression on 13.7's list as a statement, one method called twice, and the `await` forms |
| `SelectionStatements.cs` | 13.8.1, 13.8.2 | Both selection statements in one member; `if` with no `else`, with one, as an `else if` chain, dangling, and declaring in its condition |
| `SwitchStatements.cs` | 13.8.3 | Three sections declaring `hit`, two labels on one section, `default` in the middle, `goto case`, every governing type, and the switch *expression* for contrast |
| `IterationStatements.cs` | 13.9.1, 13.9.2, 13.9.3, 13.9.4 | All four iteration statements in one member, then every shape of `while`, `do` and `for` — including two `for` loops declaring `i` |
| `ForeachCollections.cs` | 13.9.5 (vocabulary) | The five collection shapes `foreach` binds five different ways, and the `GetEnumerator`/`Current`/`MoveNext`/`Dispose` no `foreach` names |
| `ForeachStatements.cs` | 13.9.5.1, 13.9.5.2, 13.9.5.4 | Seven loops declaring `element`, the iteration variable's forms, deconstructing `foreach` four ways, and a body with every exit |
| `AsyncForeachStatements.cs` | 13.9.5.3 | Four `await foreach` loops declaring `tick`, bound through the interface, by pattern, and through `WithCancellation`/`ConfigureAwait` |
| `JumpStatements.cs` | 13.10.1, 13.10.2, 13.10.3, 13.10.5 | `break` and `continue` in every statement they can leave, every `return` form including `return ref`, and a `return` that runs a `finally` first |
| `GotoStatements.cs` | 13.10.4 | Two `goto Retry` that bind to two different labels, `goto case`/`goto default` naming targets by value, and a `goto` out of a `try` and two loops |
| `ThrowStatements.cs` | 13.10.6 | Two throws of one constructor, two bare rethrows, and the `throw` *expression* in every position that takes a value |
| `TryStatements.cs` | 13.11 | Two catch clauses declaring `error`, a catch with a type and no identifier, an exception filter, a general catch, `try`/`finally` alone, and `try` inside `catch` and inside `finally` |
| `CheckedStatements.cs` | 13.12 | A `checked` block and an `unchecked` block over the same arithmetic, nested, and a call that leaves the context behind |
| `LockStatements.cs` | 13.13 | Two locks on one monitor, a `lock` over `System.Threading.Lock` — which binds different members entirely — and every operand and body shape |
| `UsingStatements.cs` | 13.14.1 | Four declarations of `handle`, an expression resource with no name, a null resource, a `ref struct` disposed by pattern, `await using`, and every exit |
| `UsingDeclarations.cs` | 13.14.2 | `using var` at block and method scope, two declarators, a `ref struct`, `await using var`, and the `switch` section that needs braces round it |
| `YieldStatements.cs` | 13.15 | Six iterators — enumerable, enumerator, non-generic, property accessor, local function, async — each of which becomes a state machine that is not in the source |
| `Resources.cs`, `Exceptions.cs` | 13.13, 13.14, 13.10.6, 13.11 (vocabulary) | The `Dispose`, `DisposeAsync`, `Lock` and exception types the statements above reach without naming |

## Why clause 13 is worth a project of its own

Clause 13 is mostly `neither`: a `while` statement declares nothing and references nothing,
and there is no fact in an index whose subject is a `while`. What clause 13 decides is
**where a walk has to go**. Every one of the forms above is a different syntax position, and
a walk that handles blocks and statement lists but forgets, say, the `else` branch of an `if`
or the iterator clause of a `for` loses every declaration and every reference written there —
without dropping a *count* anywhere, because the container it skipped was never counted.
`StatementGrammar.WalkEveryPosition` is the smallest test of that: ten grammatical positions —
the grammar's eight `embedded_statement` positions plus the two it is contrasted with — one
method called in each, and a number at the end.

Three of its clauses are more than positions, and each of them is unusual:

- **A catch clause declares a variable whose value comes from the runtime.** `catch
  (StmtRuleError error)` is the only declaration in the language with no initializer and no
  assignment — and `catch (StmtRuleError)` is a *reference with no declaration beside it*,
  which no other form produces.
- **`foreach` and `using` bind members structurally.** Between them they reach
  `GetEnumerator`, `Current`, `MoveNext`, `Dispose`, `GetAsyncEnumerator`, `MoveNextAsync`,
  `DisposeAsync` and `Deconstruct`, and the source contains none of those eight identifiers at
  any use site in this project. Every one of them is declared in `ForeachCollections.cs` or
  `Resources.cs` so the edge has somewhere to land, and `lock` adds `Monitor.Enter`,
  `Monitor.Exit`, `Lock.EnterScope` and `Lock.Scope.Dispose` to the list — chosen by the
  *static type* of the expression in the parentheses, so one statement form has two disjoint
  sets of targets.
- **`yield` replaces the container.** A member containing `yield` is compiled into a nested
  state machine class whose fields are the member's locals. So every local in an iterator
  block has two forms, and an index built from source and an index built from metadata
  disagree about all of them by construction. `YieldStatements.cs` has six iterators in six
  different container shapes — method, accessor, local function, nested local function, async
  — so that a query can tell which side it is reading.

## The hazards, and what they have in common

Twenty-two rows of this slice are flagged as able to make two declarations want one identity
string, and in clause 13 they are all the same shape wearing different syntax: **a local's
scope is a block, and sibling scopes are disjoint, so one member may declare one name many
times legally.** The project writes that deliberately, once per declaring clause, with the two
declarations at *different types* wherever the language allows it — so that a merge is visible
in the answer rather than invisible in the count:

| Name | Where | How many | Told apart by |
|---|---|---|---|
| `slot` | `StmtBlocks.TwoSlots` | 2 | Two sibling blocks; `int` and `string` |
| `Retry` | `StmtLabels.TwoRetries`, `StmtGotos.TwoTargets` | 2 labels, 2 references each | Two sibling blocks, and nothing else — a label has no type |
| `payload` | `StmtDeclarationForms.FivePayloads` | 5 | Five declaration *forms*: variable, `var`, `ref`, `const`, local function |
| `inferred`, `typed` | `StmtLocalVariables` | 2 each | Sibling blocks; `string` against `List<int>`, `string` against `int?` |
| `tracked` | `StmtRefLocals.AliasAndCopy` | 2 | One is a `ref` alias, one is a copy. Same name, same type |
| `Limit` | `StmtLocalConstants` | 3 | A const field and two local constants, `int` and `string` |
| `Fold` | `StmtLocalFunctions.TwoFolds` | 2 | Two signatures that are not an overload set |
| `i` | `StmtIteration.TwoCounters` | 2 | Two `for` initializers; `int` and `long` |
| `hit` | `StmtSwitch.ThreeHits` | 3 | Three switch sections, which are three declaration spaces with no blocks |
| `element`, `tick` | `StmtForeach`, `StmtAsyncForeach` | 7 and 4 | Seven and four loops over seven and four collection shapes |
| `left`, `right` | `StmtForeach.EveryDeconstructingForm` | 2 each | Two deconstructing loops, one through tuple elements and one through `Deconstruct` |
| `error` | `StmtTry.TwoErrors` | 2 | Two catch clauses of *one* try statement; two exception types |
| `handle` | `StmtUsing`, `StmtUsingDeclarations` | 4 each | Declarator lists and sibling blocks |
| — | `StmtExpressionStatements.BumpTwice`, `StmtThrows.TwiceOverBudget`, `StmtLocks.TwiceOnOneMonitor` | 2 references each | Nothing but position: one source member, one target member, two occurrences |

## Four properties this project is built to have

- **It compiles, and every warning in it is deliberate.** `dotnet build` reports three:
  CS0642 on the empty statement of 13.4, CS0164 on the unreferenced label of 13.5, and CS8981
  on the type named `var` of 13.6.2.1. Each is the compiler agreeing that the specimen is the
  odd shape the clause is about; there are no others.
- **None of the five refused-write shapes appears.** No type is declared at two arities — no
  type here is generic at all — no type has an indexer, nothing is `partial`, and nothing is
  `file`-local. Clause 13 needs none of the five: everything it can collide on, it collides on
  with locals, which are scoped by blocks rather than named by containers.
- **Every pattern-bound member is declared inside the corpus.** `ForeachCollections.cs` and
  `Resources.cs` exist so that no `foreach`, `using` or `lock` in this project binds to
  something only the framework declares. The framework routes are exercised too — `List<T>`,
  `Span<T>`, `IAsyncEnumerable<T>` — but never *only* the framework routes.
- **`fixed` is not here.** The `fixed` statement belongs to the unsafe clause and to the
  Unsafe project; nothing in this project takes an address, declares a pointer, or needs
  `AllowUnsafeBlocks`. `checked` and `unchecked` are here as *statements* only — the
  `checked(e)` expression form is clause 12.8.19 and belongs to Expressions, and the boundary
  is marked in a comment at the place it would have gone.

## What the compiler refuses, recorded because it cannot be written

Nine shapes belong to clause 13 and are not in the corpus, because no compiling C# contains
them. Each is named in a comment where it would have gone:

- `if (gate) int x = 1;` — a declaration statement in an `embedded_statement` position. The
  grammar excludes it, which is why a label can carry a declaration and an `if` branch cannot.
- `counter.Count + 1;` — CS0201. Only 13.7's seven expression forms may stand as a statement.
- `var count = 1;` in `VarAmbiguity.cs` — CS0029 once a type named `var` is in scope. Implicit
  typing is simply gone from that namespace.
- A nested block redeclaring an enclosing block's local — CS0136. So a repeated local name is
  only ever a *sibling* shape, and every one in this project is written across siblings.
- Two sibling `if` statements both declaring `t` in their conditions — CS0128, because a
  pattern variable in an `if` condition is scoped to the enclosing block. Two switch sections
  can, which is why `hit` lives in a switch.
- `int.MaxValue + 1` inside a `checked` block — CS0220 at compile time. The overflow the clause
  is about can only be written over values the compiler cannot fold.
- `using var h = e;` directly in a switch section — CS8647. The section needs a block round it.
- `yield return` in a `try` with a `catch` (CS1626), in a `catch` or `finally` (CS1631,
  CS1625), and in a `lock` body — so the only guarded iterator in `YieldStatements.cs` is a
  `try`/`finally` with no `catch`.
- An iterator lambda — CS1621. `yield` may not appear in a lambda or an anonymous method,
  which is why both nested iterators in `StmtYield.FromNested` are local functions.
