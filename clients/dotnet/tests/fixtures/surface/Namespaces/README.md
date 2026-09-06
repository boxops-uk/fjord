# `Namespaces` — clause 14, and the declarations with nowhere to point

Nineteen source files for the fifteen census rows of ECMA-334 draft-v9 clause 14. The clause is
short and its subject is unusual: almost everything in it is a declaration that an index
cannot hold in the ordinary way. A namespace declaration has no location of its own — the
same namespace is declared by any number of files and no declaration is the definition. A
using alias declares a name that binds to a type nobody declared under that name. An extern
alias declares a name whose meaning is supplied by the build and appears in no source file at
all. So this project is deliberately weighted toward the shapes an index is most likely to
answer nothing for, and every one of them is written rather than described.

## What is where

| Directory | Clauses | What it holds |
|---|---|---|
| `Compilation/` | 14.2, 14.5.1 | A compilation unit with all four of its sections — extern aliases, usings, global attributes (`assembly` and `module` targets), namespace members. A second with no namespace declaration, whose types are members of the global namespace. A third that is nothing but three `global using` directives and declares no member at all. |
| `Declarations/` | 14, 14.3 | The file-scoped form, the block-scoped form, nesting, and a qualified identifier used at a nested position. Then `Surface.Namespaces.Shared` declared by three files in the three available spellings, each contributing a type that names the other two files' types unqualified. |
| `Members/` | 14.6, 14.7 | A namespace body holding both kinds of namespace member — a nested namespace and types — and every type-declaration form: class, abstract and sealed class, struct, interface, enum, delegate, record class, record struct, static class, and two generics. Plus the static classes the `using static` directives import. |
| `Using/` | 14.5, 14.5.1–14.5.4, 14.8.2 | All three alternatives of `using_directive` in one file, with every alias target form. Scope: three sibling namespace bodies that see three different sets of names. Ambiguity: one simple name declared in two imported namespaces. Uniqueness: one alias name declared four times against four targets. |
| `Qualified/` | 14.8, 14.8.1 | The `::` production under all three of its left-hand readings — a using alias, `global`, and an extern alias — arranged so that each is the *only* spelling that reaches what it reaches. |
| `Extern/` | 14.4 | `extern alias`, made buildable with no reference of any kind, and the negative half of the same fact. |

## Which alias forms are here

`using` aliases name, in `Using/NsUsingForms.cs`: a namespace, a plain type, a nested type, a
constructed generic, a constructed generic whose argument is itself constructed, a corpus
generic, a tuple type with element names, an array type, a multi-dimensional array type, a
nullable value type, a type reached through `global::`, a type reached through an extern
alias, and — with `AllowUnsafeBlocks` and `using unsafe` — a pointer type. One form is absent
because it does not exist: `using X = string?` is CS9132, "Using alias cannot be a nullable
reference type", and the census records it `unbuildable` rather than this file commenting it
out.

## `extern alias` with no reference

Clause 14.4 normally needs a referenced assembly carrying an alias, and the corpus may
reference nothing — CI has no network, and a `ProjectReference` would tie two fixtures
together. So `Namespaces.csproj` puts the alias on one assembly of the framework reference
that is already resolved: a target between `ResolveAssemblyReferences` and
`FindReferenceAssembliesForReferences` sets `Aliases` on the `ReferencePath` item for
`System.Net.Ping`.

The choice of assembly is the load-bearing part. Giving a reference an alias **removes it
from the global alias**, so the aliased assembly must be one whose types nothing here wants
unqualified. That makes clause 14.4 checkable from both sides, and both are asserted by the
build rather than by a comment:

- `NsPing::System.Net.NetworkInformation.Ping` resolves, in all six places the alias is
  declared — five compilation units and one namespace body.
- plain `System.Net.NetworkInformation.Ping` resolves **nowhere in this project**. It is
  CS1069 in every file, which is recorded in `Extern/NsExternAlias.cs` as the one construct
  here that is stated and not written.

## Properties this project is built to have

- **Every error code it names, it provoked.** Eight diagnostics are recorded in comments
  instead of compiled — CS9132 (a nullable-reference alias), CS8386 (`new` on an array alias),
  CS1955 (an alias hiding an imported method group), CS0104 (an ambiguous simple name), CS0103
  (twice, for what a parent namespace's import does not give), CS0246 (a relative using
  directive at compilation-unit level), CS0576 (an alias conflicting with a member of its own
  body), and CS1069 (the unaliased spelling of the aliased assembly's types). Each was
  produced by writing the offending line, reading the compiler's output, and deleting the
  line; none is recalled. Two of the eight corrected a claim this README would otherwise have
  made — a `using` of a parent namespace imports neither its nested namespaces nor their
  types, and CS0576 does not fire until the conflicting name is used.
- **No name is declared at two arities, and no shape in it can kill an indexing run.**
  `NsCell<TValue>` has no non-generic namesake and `NsPairOf<TFirst, TSecond>` has no
  one-parameter namesake; there is no indexer, no partial member, no partial type, and no
  `file` type. The corpus's coverage of those lives in the quarantine projects.
- **Every type name is unique across the whole corpus**, prefixed `Ns` — including the two in
  `Compilation/NsGlobalNamespaceUnit.cs`, which are members of the global namespace, the one
  declaration space all twenty-two projects share.
- **The same namespace is declared many times on purpose.** `Surface.Namespaces.Shared` by
  three files, `Surface.Namespaces.Members` by three, `Surface.Namespaces.Declarations` by two
  in two spellings, `Surface.Namespaces.Uniqueness` by two. Every one of those declarations
  mints the same name and carries no location that could distinguish it from its siblings —
  which is the whole of clause 14's hazard, and the reason a query over this project should
  find one namespace row per name and not one per file.
- **What clause 14 forbids is not writable, so it is written down.** A namespace member is a
  namespace or a type and nothing else: there is no grammar for a field, a method or a
  constant directly in a namespace, so 14.6's negative half appears in
  `Members/NsNamespaceMembers.cs` as prose. It is the only claim in the project with no code
  behind it, and it has none because no C# expresses it.
