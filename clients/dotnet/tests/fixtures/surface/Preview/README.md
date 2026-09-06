# `Preview` — C# 14, which the pinned compiler only parses at `preview`

Ten source files for the `post-standard` rows of `POPULATION.tsv` that name a C# 14 feature.
Every construct names its row in a comment; the row's `id` is the citation, since no clause of
ECMA-334 draft-v9 describes any of this.

**The project exists because of one property.** `LangVersion=preview` is what the pinned
Roslyn needs to parse an `extension` block, and a language version is a per-project setting —
so these rows cannot live beside `Modern`'s, whose claim is that C# 10-13 needs *no* opt-in.
Splitting them keeps both claims checkable.

## What is where

| File | Rows |
|---|---|
| `ExtensionMembers.cs` | extension blocks: instance methods and properties, static members, a generic block with a constraint, and a block over a narrower receiver |
| `ExtensionOperators.cs` | user-defined operators inside an extension block, including a `checked` one and two in-place ones over a `ref` receiver |
| `FieldKeywordPreview.cs` | the `field` keyword in the shape C# 13 previewed — both accessors written out — and the breaking change: a member actually named `field` |
| `FieldBackedProperties.cs` | the C# 14 shapes: one auto accessor beside one written accessor, an initializer, a nullable backing field, a static and a record property |
| `NullConditionalAssignment.cs` | `?.` and `?[]` on the left of an assignment and a compound assignment, including an event and an array |
| `LambdaParameterModifiers.cs` | `out`, `ref`, `in` and `ref readonly` on lambda parameters whose types are inferred |
| `SpanConversions.cs` | first-class span types: a span-receiver extension method called on a string and on an array, and the variance conversion |
| `ExpressionTreeArguments.cs` | named and omitted arguments inside an `Expression<T>` lambda |
| `CompoundAssignmentOperators.cs` | instance `operator +=`, `-=`, `*=`, `++` and `--`, beside the static binary operator the compound form used to be derived from |
| `UnboundGenericNameof.cs` | `nameof(List<>)`, `nameof(Dictionary<,>)`, and an unbound nested generic |

## The property this project is built to have

**Extension blocks are the reason this project is more than a language-version formality.**
This indexer was found to drop an `extension` block *with every member inside it*. A block
declares its receiver in the block header rather than on each member, so a walk that expects a
method's first parameter to be its receiver finds nothing to attach and the members disappear
— silently, because a dropped declaration writes no fact and fails nothing.

So every extension member declared here has a call site in the same file
(`ExtensionMemberUses`, `ExtensionOperatorUses`). If the declarations are dropped, the
references remain, and the index then holds calls with nothing to resolve to. That
disagreement is measurable; a missing declaration on its own is not.

Two smaller cases are recorded the same way:

- **An extension *indexer* is not a thing.** `this[…]` inside an extension block is CS9282,
  so the corpus cannot hold one — a comment in `ExtensionMembers.cs` says so at the position
  where a reader will look for it. That is a limit of the language, not of the quarantine.
- **`field` shadows a member named `field`.** `FieldKeywordPreview.ShadowedField` declares a
  field with that name and a property whose accessor uses the keyword; the compiler warns
  CS9258 and binds the two spellings to two different variables. One name, two storages, in
  one type.

**Partial constructors and partial events are `quarantined`, not written.** Both are the
"partial member's two halves" shape, which is a refused write in the whole-corpus run; they
are recorded in the census and belong to a quarantine project.
