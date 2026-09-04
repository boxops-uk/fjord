# `SyntaxForms` — every `SyntaxKind` that is a declaration or a reference to one

The census slice this project answers is not a clause range. It is Roslyn's own
`SyntaxKind`: the 115 rows of `POPULATION.tsv` whose `area` is `roslyn` and whose `id` names
a syntax kind, or a group of them (`AddExpression / the 22 binary kinds`). The sibling
`roslyn` project answers `SymbolKind`, `TypeKind`, `MethodKind` and `LanguageVersion` — the
same area from the symbol side.

Twenty-two files, one per group of kinds, each naming the census row a construct comes from
so a reader can walk from a row to the code and back.

## What it subsumes

`Boxops.Fjord.Tests/DeclarationCensusTests` walks Roslyn's type hierarchy for the seven bases
`Indexer.IndexTree` switches on and asserts a fixture holds every concrete form it finds —
nineteen of them. Every one of those nineteen is here, in `Declarations/`, and so is the
`extension` block the gate parses under `Preview`. What this project adds is the other side:
**the reference kinds**, which that gate does not model at all.

- Every name form: `IdentifierName`, `GenericName`, `QualifiedName`, `AliasQualifiedName`,
  `PredefinedType` — in `Names/SfNameForms.cs`.
- Every access form: `a.b`, `a?.b`, `a[i]`, `[i] = v`, `f(x)`, `this`, `base` — in
  `Expressions/SfAccessForms.cs`.
- Every operator form, as four census groups: 22 binary, 13 assignment, 9 prefix-unary, 3
  postfix-unary — in `Expressions/SfOperatorForms.cs`.
- The kinds that occupy identifier syntax without being a reference anyone wrote: `var`,
  `nameof`, `_`, a contextual keyword used as a name, and C# 14's `field` — in
  `Names/SfIdentifierOccupants.cs`.
- All six cref kinds, which are references written inside trivia — in
  `Names/SfCrefForms.cs`.

## The property it is built to have

**Every construct here is one an index either holds a fact about or provably does not, and
the file says which.** That is the whole design. The walk's declaration switch names six
node bases and its reference dispatch names `SimpleNameSyntax` plus three expression bases,
so for any syntax kind the answer is decidable by reading, and each file states the answer
for its kinds beside the code that provokes it. A query over the indexed corpus can then
assert *both* directions: that a class declaration has a definition, and that a `get`
accessor beside it does not.

The three claims a gate can hold against this project:

- **Every reference the walk can see, it sees twice where the schema says twice.** `a.b`
  produces a cross-reference *and* a member-access location; `a?.b` produces only the first,
  because `MemberBindingExpressionSyntax` does not derive from
  `MemberAccessExpressionSyntax`. Both spellings of the same member read are in
  `SfAccessForms.Reach`, so the row counts are comparable rather than argued.
- **Nothing here is a refused write.** No type name appears at two arities, no type has two
  indexers, nothing is `partial`, and no type is `file`-local. The one shape that could
  collide without being one of those five — two anonymous-object creations with the same
  member names, which are one symbol with two declaring syntaxes — is written as three
  *distinct* shapes, and the collision is recorded as a prediction in
  `SfCreationForms.Anonymous`.
- **Two kinds in the slice are unreachable from code that compiles, and two more are
  unreachable from code that is not a script.** `IncompleteMember` and
  `UnknownAccessorDeclaration` exist only in parser error recovery; `BadDirectiveTrivia` is
  CS1024 even inside an inactive `#if`, and `ShebangDirectiveTrivia` is CS9314 outside a
  script or file-based program. The other eighteen preprocessor-directive kinds are in
  `Directives/SfDirectiveForms.cs`, the script-only ones inside an inactive region where
  Roslyn parses them into directive nodes and binds none of them.

## What the project file has to say, and why

Five properties beyond the corpus shape, each because a census row needs it:

| Property | The row that needs it |
|---|---|
| `OutputType=Exe` | `CompilationUnit` and `GlobalStatement` — top-level statements are CS8805 in a library |
| `AllowUnsafeBlocks` | `PointerType`, `FunctionPointerType`, and the two prefix-unary kinds that only exist over pointers |
| `LangVersion=preview` | `ExtensionDeclaration` and the `field` keyword; the pinned Roslyn parses neither below C# 14 |
| `GenerateDocumentationFile` | the six cref kinds — a cref is only a `CrefSyntax` node when the documentation mode is on |
| `Nullable=enable` | `NullableType` over a type parameter, and `SuppressNullableWarningExpression` |

And one target, `SfAliasXDocument`, which attaches an alias to the resolved
`System.Xml.XDocument` reference. `ExternAliasDirective` is the one row in the slice that no
source file can reach alone: `extern alias xdoc;` is CS0430 unless a reference actually
carries that alias, and the SDK resolves the framework by targeting pack rather than by
`<Reference>` item. `System.Xml.XDocument` is chosen because nothing here needs it in the
global namespace — aliasing an assembly removes its types from `global::`, so aliasing
`System.Runtime` would make `object` itself unresolvable.

## One thing that is not under `Surface.SyntaxForms`

`SfEntryPoint.cs` is a file of top-level statements, so the compiler synthesises a `Program`
class in the **global** namespace and an entry point whose declaring syntax is the
`CompilationUnitSyntax` itself. Neither can be moved into a namespace; that is what the
`CompilationUnit` row is. Every other type in this project is under `Surface.SyntaxForms` and
prefixed `Sf`, so no name here collides with another project in the corpus.

`SfLiteralForms.CallVarargs` is the one member the entry point does not call.
`__arglist` — the `ArgListExpression` row — compiles to the vararg calling convention, which
CoreCLR refuses to JIT. It is here to be parsed, not run.
