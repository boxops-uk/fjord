# The .NET indexer

A real indexer for a real language, writing into Fjord over the wire protocol:
**Buildalyzer** runs each project's design-time build out of process, **Roslyn** answers
what every name in the resulting compilation means, and the facts go straight down the
socket to a running server.

`Boxops.Fjord.Demo` shows the protocol works by writing six declarations somebody typed out.
This writes however many a checkout of .NET source contains, which is the other thing a
database needs shown: that it holds up when the facts were not chosen to be convenient.

```sh
# a fresh database, a server, and a checkout indexed into it
./clients/dotnet/index-repo.sh ~/src/OrchardCore

# or by hand, against a server already running
dotnet run --project clients/dotnet/Boxops.Fjord.Indexer -- \
    --source ~/src/OrchardCore --socket /tmp/fj-index/db/fjord.sock --database code
```

## What it is for

Four things, in the order they matter:

1. **Volume.** A million facts, written the way a producer really writes them, is the
   only way to find out what interning costs, what a scan costs when the predicate is
   not six rows, and whether a plan that looks fine on the fixture still looks fine when
   `codemarkup.FileXRef` has seven figures in it.
2. **A second implementation, doing something harder.** The demo proved the protocol is
   implementable from outside. This proves it is *usable* from outside — that a producer
   with a real workload, emitting in the order a syntax walk reaches things, needs
   nothing the protocol does not offer.
3. **Something to query.** An index of code someone knows is a database whose answers
   can be checked by opening the file.
4. **The rest of the schema.** Most of `schemas/dotnet.sigla`'s sixty-five predicates —
   the project graph and the C# entity model — cannot be answered by a syntax walk at
   all. This program is where they come from, which makes it part of the schema rather
   than a consumer of it.

## The shape of the run

**A producer that holds no fact ids.** Roslyn hands this program a symbol; it turns the
symbol into the entity fact that names it and nests *that whole fact* wherever a
reference to it goes — through the type that contains it, its full name, its namespace,
down to the interned identifier at the bottom. It keeps no map from entities to
identities, and it emits in whatever order the walk reaches things.

At six declarations that is an elegance argument. At a million facts it is the only
tractable option: the alternative is a second pass over an index that no longer fits in
memory, ordered so that every target is written before every reference to it.

The cost lands on the server, deliberately, and the run reports it:

```
  server                  18,176,899 created, 44,422,889 deduped
```

Five million references naming nine hundred thousand declarations *is* that dedup count.
It is interning working, and it is the number this whole exercise exists to measure.

## How C# maps onto the code index

The schema is the server's — sixty-five predicates, parsed from `schemas/dotnet.sigla`
rather than written in Rust — so the question is not what to declare but what to put in
it. `DotnetIndex.cs` states it independently, because that is what the handshake
fingerprint is for. **Declaration order is not part of that agreement**: the
fingerprint sorts by name on both sides, so this file may list predicates in whatever
order reads well.

**The source layer**, which every indexer fills whatever language it reads:

| predicate | what it holds | how it is decided |
|---|---|---|
| `src.File` | a path, relative to `--root` | every syntax tree with a file behind it, minus `bin/`, `obj/`, and anything outside the root — a path that climbs out is not a name two runs would agree on |
| `src.Symbol` | a SCIP symbol | the cross-language identity: `scip-csharp-2 nuget <assembly> <version> <descriptors>`, minted for everything with a global name and for nothing without one |
| `src.FileInfo` | `{file}` → bytes, lines, whether it ends in a newline | one per file, and what a consumer falls back to when an offset resolves past the last line |
| `src.FileLine` | `{file, line}` → the text, and three offsets | every line, blanks included. `start` is a UTF-8 byte offset and `cstart` a UTF-16 one — not the same number — so the line table is also the conversion table |
| `src.FileLineAt` | `{file, start, line}` | the inverse: which line an offset is on, as a seek rather than a scan |
| `src.FileLanguage` · `src.FileDigest` | `{file}` → the language · the content hash | what to highlight it as, and what tells two checkouts of one path apart |
| `src.FileLineStyles` | `{file, line}` → opaque bytes | syntax highlighting from Roslyn's own classifier, off unless `--styles`. fjord defines nothing about the bytes; `config.Setting {dimension = "style-encoding"}` names the format |
| `src.FileOrigin` | `{file}` → repo, revision | only where the run states them: provenance is not in the code |

**`config.Setting`** — what the database was built *against*: the target framework
(exactly one), the configuration, the index root, the position encoding, the symbol
scheme, the languages, and the producer. Every one of these was implicit once, and one of
them cost real time: an index whose paths are relative to a root nobody wrote down can only
be matched to a checkout by inference.

**The build layer** — what compiled a file, and into what:

| predicate | what it holds |
|---|---|
| `msbuild.Solution` | the solution this index was built from, where the run resolved one — see below |
| `msbuild.SolutionToProject` · `msbuild.ProjectToSolution` | its membership, both ways, one edge each per project the solution lists |
| `msbuild.Project` | a `.csproj`, with what MSBuild evaluated on the value side: SDK, output type, assembly name, root namespace, platform |
| `msbuild.Assembly` · `msbuild.Compilation` | the assembly a project **produces**, and the crossing of the two per target framework. Only a produced one: an assembly referenced from outside the graph is not named here at all |
| `msbuild.SourceFileToProject` · `msbuild.ProjectToSourceFile` | both directions, because neither is a seek from the other |
| `msbuild.ProjectReference` · `msbuild.ProjectReferencedBy` | the project graph, both ways: "what does this need" and "who needs this" |
| `msbuild.Package` · `msbuild.PackageReference` · `msbuild.PackageDependent` | `<PackageReference>`, with the version after central package management has had its say |

**The C# layer** — the entity model, which is what a syntax walk cannot see:

| predicate | what it holds |
|---|---|
| `csharp.Name` · `csharp.NameLowerCase` · `csharp.Namespace` | interned identifiers, and the search row that folds their case |
| `csharp.Class` · `Interface` · `Record` · `Struct` | the named types, each keyed on its full name and carrying its modifiers |
| `csharp.Method` · `Property` · `Field` · `Parameter` · `TypeParameter` | the members, keyed on what the compiler knows rather than on where they are written. `csharp.Local` is declared and deliberately not written — see below |
| `csharp.MethodParameter` · `MethodTypeParameter` · `TypeTypeParameter` · `PropertyParameter` | the ordered lists, one fact per position, because the type model has no arrays |
| `csharp.ArrayType` · `PointerType` | the type shapes a name alone cannot spell. `csharp.FunctionPointerType` cannot be written at all: its `signature` is a `csharp.Method`, whose key leads with a containing type, and a function pointer's signature symbol has none |
| `csharp.Implements` | **the closure**, not the list the declaration writes: a type that says `: List<T>` *is* an `IEnumerable`, and sigla has no recursion to close it at query time |
| `csharp.DefinitionLocation` · `EntityXRef` · `EntityRef` | where an entity is written, and every reference to it in both directions |
| `csharp.ObjectCreationLocation` · `MethodInvocationLocation` · `MemberAccessLocation` · `TypeLocation` | the same positions per *kind* — a construction and the constructor it calls, a call and the member access it went through, the field, property or method a `.` reaches, and every type spelled as a *name* |
| `csharp.SymbolOf` · `DefinitionBySymbol` | the crossing between an entity and its SCIP symbol |

**The `codemarkup` layer** — the same facts re-keyed for the questions a UI asks, with the
language taken out of them:

| predicate | the question it answers |
|---|---|
| `codemarkup.Definition` | "where is this symbol defined", keyed by symbol |
| `codemarkup.FileDefinition` | "what is defined in this file", keyed by file and position |
| `codemarkup.FileXRef` | "what does this file reference, in reading order" |
| `codemarkup.SymbolXRef` | "who references this", across every file |
| `codemarkup.FileLocalXRef` | the same, span to span, for a local that has no global name worth minting |
| `codemarkup.SearchEntry` · `SymbolByName` | prefix search on a case-folded name, and exact search on the written one |
| `codemarkup.Relation` · `RelationOf` | contains, extends, implements, overrides — both directions |
| `codemarkup.SymbolInfo` | the signature, the doc comment and the modifiers a hover card shows |

Several of those deserve their reasoning stated.

**`src.Symbol` is a string, and that is what makes a fan-out possible.** A `FactId` is a
predicate tag plus a per-predicate sequence, so it means nothing in another database — and
"who references this, anywhere" is a question across several. A SCIP symbol survives the
trip. It costs an interned string per reference and buys the ability to leave the database.

**A named type's descriptor carries its arity, and the scheme token says so.** C# overloads
a type name on arity — `class Result` beside `class Result<T>` — and Roslyn's `Name` strips
the count the metadata name spells, so a descriptor built from it minted one string for
both. That was not a conflation: `codemarkup.SymbolInfo` is keyed `{symbol}` with the
signature on the value side, so two facts wanted one key with two values, ingest refused
it, and **the run died part-way through a write** — no repository containing that pair
could be indexed. The arity goes inside the descriptor's name, `N/Result+1#`, which is the
only place the specification has for it: it has no arity provision and its one
disambiguator slot is a method's. `+` is a simple-identifier character there, so nothing is
escaped, and no C# identifier can contain one.

**Arity 0 keeps the bare name**, deliberately: `Result` is still `N/Result#`, so every
non-generic symbol in every index is the string it always was. The suffix sits on the
*containing type's* descriptor, so a member of `Result<T>`, its parameters and its type
parameters inherit it by construction rather than by a second rule.

**An overloaded indexer's descriptor carries a sibling ordinal, for the same reason in the
same place.** Roslyn names every indexer of a type `this[]` — an `IndexerName` attribute
moves the *metadata* name and not that one — so `this[int]` beside `this[int, int]` was one
string for two declarations and killed the run the same way, on `codemarkup.Definition`'s
`{symbol, file}` key. A term descriptor is `<name> '.'` and the grammar's one disambiguator
slot is a method's, so the ordinal goes inside the name too: `` N/Box+1#`this[]+1`. ``. It
is the ordinal methods already have, ordered by documentation id, rather than a count of
parameters — `this[int]` beside `this[string]` differ only in parameter *type*. And it is
**empty for the first or only sibling**, exactly as `M().` is for a method, so a type with
one indexer keeps the string it has: the same asymmetry that made arity 0 free.

C# permits two same-named members of one type only for methods and indexers, so nothing
else here can collide. That bound is a table rather than an assumption —
`ScipSymbolsTests.Only_a_method_or_an_indexer_can_be_two_same_named_members` compiles every
other property-shaped pair and asserts the compiler rejects it, including the two that look
like exceptions: an `IndexerName`'d indexer beside a property of that name, and two halves
of a partial class.

**Both halves of a partial member are one member, so they take one string and fill one
key.** `partial void Ping();` and `partial void Ping() { }` are two declarations of one
thing — one signature, one documentation comment, one `csharp.Method` — and a containing
type's `GetMembers()` lists only the declaring one. So the walk reduces every declaration
to that half before it spells or keys anything, and two things follow. The implementing
half spells the declaring half's symbol, rather than being looked for in a list it is not
in. And what a per-member fact carries is read from the declaring half, which matters
beyond tidiness: `GetDocumentationCommentXml` is **empty** on the implementing part, so a
walk that read it from whichever declaration it was standing on filled
`codemarkup.SymbolInfo`'s `{symbol}` key with a comment and without one, and the run died.

**`codemarkup.Definition`'s value is a function of its `{symbol, file}` key**, which is
the other half of the same rule and reaches further than partial members. It carries a
span, so two declarations of one thing in one file — both halves of a partial member, two
`partial class` parts — otherwise filled one key twice with two spans, and ingest refuses
one key with two values. The span is therefore the member's **first declaration in that
file**, asked of the member and the file rather than of whichever declaration the walk is
standing on. First in the file rather than the declaring half, because a partial *type* has
no declaring half and needs the same rule, and because the predicate is per file: the
declaring half's span is not in the implementing half's file. Position within a file is
fixed, so nothing here depends on the order the compiler was handed the files. Nothing is
lost either — `csharp.DefinitionLocation` and `codemarkup.FileDefinition` are keyed per
span and carry every declaration, which is how a partial type has answered "where is this
written" all along.

**An ordinal is counted on the canonical definition, which is what makes a reference's
equal its declaration's.** A reference arrives holding something `GetMembers()` may not
list — `M<int>` is not `Equals` to `M<T>`, nor is a reduced extension method — and, reached
through a *constructed* type, `GetMembers()` answers constructed members whose signatures
are the substituted ones. **Substitution can make two of those identical**:
`Box<T>.M(T)` and `Box<T>.M(int)` are one documentation id *and* one display string on
`Box<int>`, so sorting the constructed members left both sort keys tied, the stable sort
fell back to `GetMembers()` order — file order, for a partial class — and one of the two
file orders named the wrong overload. Reducing every sibling to its unreduced,
unconstructed definition before sorting removes the tie: the reference is then counted over
the same list its declaration is.

**The token moved with the format, because that check is all a fan-out has.**
`config.Setting {dimension = "symbol-scheme"}` is what a cross-repository join looks at
before it trusts a string match between two databases, so a format that moved under an
unchanged token would make it join identities that are no longer comparable and answer
wrongly, with nothing anywhere reporting an error. `scip-csharp` was revision 1 and this is
`scip-csharp-2`; the next move is `-3`. What ties the two is `golden/symbol-scheme.txt` —
representative symbols pinned *against* the token, regenerated with `FJORD_GOLDEN=update`,
and the regeneration refuses to write symbols that moved while the token did not.

**Identity and location are separate.** A `csharp.Method` is keyed on what the compiler
knows — its name, its containing type, its signature — and `csharp.DefinitionLocation`
says where it is written. Reformatting a file moves every location and no identity, which
is the property the old `{module, name, line}` key did not have: a blank line inserted at
the top of a file re-keyed every declaration below it.

**The location predicates are per *kind*, and each says what is at a position.**
`EntityXRef` answers "what does this file reference" over one union; the four beside it
answer the same positions by kind, which is the shape Glean's `csharp` schema has and this
one transcribes. `ObjectCreationLocation` carries the constructed type *and* the
constructor the compiler chose, which no other predicate here holds;
`MethodInvocationLocation` carries the invoked method and, where the call went through a
`.`, the member access it went through; `MemberAccessLocation` carries the field, property
or method a `.` reaches — the accessed member, not the expression it was reached through,
which is the reading the schema's own comment states and the only one that answers a field
or property read at all; `TypeLocation` carries every type spelled as a name — a keyword-spelled
one (`int`, `string`) is a `PredefinedTypeSyntax` and never reaches the walk. Every span is
the identifier's extent, converted to the UTF-8 bytes `config.Setting
{dimension = "position-encoding"}` declares — Roslyn counts UTF-16 code units, and the line
table is what converts.

**A predicate that is declared and not written is classified, with the reason.** Two are:
`csharp.Local`, deliberately — SCIP models a local as an occurrence ordinal that moves
whenever the file is edited, so a local gets no global name and `codemarkup.FileLocalXRef`
answers a file-local jump span to span instead — and `csharp.FunctionPointerType`, which
cannot be keyed, so a member typed as one is dropped and counted like a `dynamic` one.
**Nothing is owed any more.** Two predicates are impossible and say why above; three are
written by some runs and not others; every other one this client declares is written by any
run. The list is not prose: `PredicateCensusTests` holds it as a table and asserts every
entry over a run of the `census` fixture, so a predicate that stops being written fails, and
one that starts being written where the table excuses it fails too. That gate is what four
declared-and-empty location predicates got past, and what made the case for deleting the two
metadata-reference predicates rather than leaving them owed: a reference list from a
design-time build is every resolved DLL, and nothing in it separates a `<Reference>`
somebody wrote from the framework's own.

**A third classification is for predicates written by some runs and not others**, which the
three solution predicates are. Asserting one only over the run that fills it cannot tell it
from a predicate that is always written, so the table's `Conditional` entries are asserted
twice: rows after a run over `Census.slnx`, and none after a run over one of the same
fixture's `.csproj` files.

**The solution facts belong to the run that resolved a solution, and to no other.**
`--source` may name a `.slnx` or a `.sln`, or a directory the loader picks one out of — that
run has a solution and gets `msbuild.Solution` with both edges to every project the solution
lists. Point it at a `.csproj`, or at a directory with no solution in it, and the three are
**empty**: MSBuild's containment is one-way, so a project file names no solution, there is
nothing to resolve from one, and searching the disk for a solution that happens to list it
would put a claim in the database that the build system does not make. So the predicate reads
*the solution this index was built from*, which is a question a consumer can act on — and
"empty for a project-only run" is the answer rather than a gap. What decides it is what the
run **resolved**, not what was typed: `--source ~/src/repo` and `--source ~/src/repo/Repo.slnx`
are the same run and write the same facts.

**The solution file is interned as a path and gets none of the per-file source facts** — no
`src.FileLanguage`, no `src.FileDigest`, no `src.FileInfo`, no line table — which is exactly
how the `.csproj` in `msbuild.Project`'s key is interned. The source layer describes files the
run *read as source*: every offset in it is an offset into a file some compilation parsed, and
nothing here holds a position in a solution file. A `src.FileLanguage` of `xml` would also
contradict `config.Setting {dimension = "language"}`, which says what the semantic layers
cover; `--no-lines` and `--styles` are switches over that same table, and Roslyn's classifier
has no document for a file no compilation contains. What makes the fact readable is the two
edges, through which its `file` joins to exactly what a project's does.

**A project the solution lists that this index cannot key gets no edge, and the run says so.**
Both edges are references to an `msbuild.Project`, and a reference to a fact that does not
exist is not a fact — so the only such project an ordinary layout produces is one whose path
climbs out of `--root`, which has no name two runs would agree on. It is named where the build
layer names its other omissions and counted where the run reports its others, because a
database holding two thirds of a solution's membership looks exactly like one holding all of
it.

**`codemarkup` is redundant with `csharp` by construction, and deliberately.** Every fact
in it could be derived from the layer beside it — while `nyi/derivation` stands, a producer
is what states the second keying, and the query that *would* derive each one is a comment
in the schema. What it buys is a surface a UI reads without knowing C# exists, which is the
same surface a SCIP converter fills for TypeScript.

**`csharp.Implements` stores the closure, and that is a decision.** Someone asking for every
enumerable in a repository is asking the semantic question, not the syntactic one. More
facts on the way in, one seek on the way out.

**A declaration this layer cannot express is dropped and counted.** A signature mentioning
`dynamic`, or a name that did not resolve, has no `csharp.AType` alternative — and that type
sits in the key of `Method`, `Field` and `Parameter`. The other reason is a declaration kind
this layer has no entity for at all, which is a gap in the schema no checkout can fix: an
`event`, both forms of it, and an `extension` block, which takes the members declared inside
it down with it. Because `codemarkup` cross-references are written whether or not a
definition was, each of those is a symbol with uses and no definition. The run prints how
many and which of the two causes, because a layer that silently loses declarations is worse
than one that says how many it lost. On a checkout that declares neither it is zero, and a
number other than zero found a broken workspace once. Which kinds fall on which side is a
census — `DeclarationCensusTests` walks every declaration form Roslyn derives from the bases
the walk switches on, and refuses a form that is neither expressed nor counted.

**The build layer degrades rather than disappears.** A design-time build knows the resolved
framework, the assembly name MSBuild computed, versions after central package management,
and the exact source list. Without one, the project files are still on disk and still say
what they reference — so `ProjectIndex` reads their XML, attributes each source file to the
nearest enclosing project, and records an unexpanded `$(NetCoreAppCurrent)` as exactly that
rather than inventing a framework. Two things it will not do: guess a version a project
file does not state (the empty string means "not stated"), and attribute *shared* source —
`src/libraries/Common` in dotnet/runtime lives under no project and is compiled into a
hundred assemblies by explicit `<Compile Include>`, so it gets no edge at all rather than a
plausible one. The run says how many files that was.

## What it resolves, and what it does not

Everything here is a **symbol** question rather than a syntax question, which is the
whole difference between this and `example/index.py` — that one is honest about stopping
at the line where types would be needed, and this one is on the other side of it.
An extension method invoked as an instance method, a member reached through a type
inferred from a lambda's parameter, a partial class continued in another file: Roslyn has
already answered all of it, and the walk asks.

What it still does not do, each for a reason:

- **A reference to something outside the index is dropped**, not recorded. A symbol from
  a NuGet package or the framework has no source location to point at, and inventing a
  declaration for it would put file facts in the database naming paths that do not exist.
  The run reports how many: on a typical repository it is a third of all names.
- **A partial type or member is one entity, and its `codemarkup.Definition` is at its
  first declaration in each file.** One symbol, one hover card, one search-index name —
  with a `csharp.DefinitionLocation` and a `codemarkup.FileDefinition` for every
  declaration, so no half is unreachable.
- **A multi-targeted project is indexed once**, at the newest .NET it builds for. The
  other target frameworks are the same files and would dedup on the way in; the work
  would not.
- **Generic instantiations are collapsed to their definition** — `List<int>.Add` and
  `List<T>.Add` are one declaration — and a type parameter is not a declaration at all.
  The hierarchy itself *is* indexed, as `csharp.Implements` and `codemarkup.Relation`.
- **A type the compiler could not resolve is not recorded as one.** `csharp.AType` has no
  alternative for an error type or for `dynamic`, and that type sits in the *key* of
  `Method`, `Field` and `Parameter` — so the declaration is dropped rather than written
  under a fabricated one, and `Inexpressible` counts it. An unresolved type displays as
  whatever the source wrote, so recording it would mean the same declaration reached from
  a run that resolved it and one that did not were two different facts.
- **A symbol this producer cannot spell has no `src.Symbol`, and the run says how
  many.** Two things reach that: a member whose position among its type's same-named
  siblings could not be found, and a kind the descriptor switch has no arm for. Neither
  should happen, and neither is an exception any more — the declaration keeps its entity
  and its span and loses only the cross-database name, because a producer that dies on a
  shape nobody anticipated is the wrong failure mode for a tool pointed at code it did not
  write. Two rounds of the symbol work argued that branch unreachable and both were wrong
  about the same everyday shape, which is the argument for counting rather than throwing:
  a run prints `N symbol(s) not spelled` and somebody can go and look.
- **An indexer's *use* writes no reference.** A reference is collected from a
  `SimpleNameSyntax`, and `shelf[0]` is an element access with no name node of its own — so
  the declaration is in the database and the use is not, whatever the symbol says. That is
  a gap in the walk rather than in the format, and it is why the `indexer` fixture asserts
  only the declaration half: that a reference to an overloaded indexer is byte-identical to
  its declaration is asserted where the strings are minted, by `ScipSymbolsTests` and by
  `golden/symbol-scheme.txt`, both of which resolve an element access themselves.
- **An overload ordinal renumbers when an overload that sorts earlier is inserted.** That
  is inherent to the format — SCIP has an ordinal and no signature — and it is why nothing
  keys on a symbol across revisions: `csharp.Method` carries `docId` itself and is reached
  through `csharp.SymbolOf`. What *was* removed is the half that was not inherent: the
  ordinal no longer depends on the order the compiler was handed the files.
- **Two assemblies declaring one namespace-qualified type name are one `csharp` entity.**
  `csharp.FullName` carries no assembly, so `W.S` compiled twice is one row — and its
  members go with it, because they key on a containing type that fused. It has its own
  section below. The *arity* axis of the same key is closed: `csharp.FullName` carries an
  `arity`, so `Result` and `Result<T>` are two entities.
- **`csharp.Method` is only as fine as the `docId` it trails**, and the pinned compiler
  hands two C# 14 shapes one. A compound-assignment operator is `Name = "op_UnaryPlus"` and
  `M:…op_UnaryPlus(T)` whatever its token, so `MemLedger`'s `+=`, `checked +=` and `-=` are
  one `csharp.Method`; and two `extension` blocks over one receiver type both get
  `M:…#ctor(TReceiver)`, so their synthesised constructors are one. Four of the reference
  corpus's twelve merged entities are this, and it is a third axis — neither arity nor
  assembly — that a further disambiguator would have to close.

**One collision shape is left, and it is `file`-scoped types.** Two classes of it are
closed. *Two members of one type* are separated — a type name by its arity, a method and an
indexer by an ordinal, every other same-named pair a compile error. And *two declarations
of one thing in one file* now fill one key rather than two: both halves of a partial member
and two `partial class` parts, which is the paragraph above and was a dead run for the
partial type as well as the partial member. What is not closed is **two things in two files
whose one symbol is the same string**: C# 11's `file class Hidden` may be declared once per
file in one namespace, and the compiler mangles only the metadata name, so two of them mint
one `P/Hidden#` and so do their members. **That still kills a run** whenever the two differ in any member —
`codemarkup.SymbolInfo` is keyed `{symbol}` with the signature on the value side:

```
Unhandled exception. System.InvalidOperationException: the fact writer failed while
writing codemarkup.SymbolInfo
 ---> Boxops.Fjord.Client.FjordServerException: Conflict: predicate PredicateId(8)
      already holds a different fact under this key, as FactId(8796093022210)
EXIT=134
```

It is not folded in here because it is not a spelling. A `file` type is file-local by the
language's own word, so the question is whether it has a global name at all — the walk
already answers no for a local, a label, a range variable and anything a method body
introduces, and `codemarkup.FileLocalXRef` is where a span-to-span answer lives. Whether a
consumer may join on such a name across databases is a decision, and
`ScipSymbolsTests.A_file_local_type_still_takes_one_symbol_for_two_declarations` asserts
the collision as it stands so that closing it is deliberate.

Two further residues sit beside it and neither is a collision — the ordinal is not stable
across an edit (above), and the entity layer has no assembly axis (below). The `partial`
fixture and `PartialMemberTests` are the gates on the two shapes that *are* closed, so a
regression there is a failing run rather than a silently different index.

## The entity layer tells two arities apart, and does not tell two assemblies apart

`csharp.FullName` is `{name, containingNamespace, arity}`, and the four named types lead
their keys with it. So `Result` and `Result<T>` are **two** `csharp.Class` facts with a
`csharp.DefinitionLocation` each, `csharp.SymbolOf` crosses one symbol to each, and the C#
entity model now agrees with `src.Symbol` and with `codemarkup` rather than disagreeing.
`ArityPairTests.Each_arity_is_its_own_class_fact_with_its_own_location` is the gate, and
`EntityKeyCensusTests.Two_arities_are_two_entity_keys_and_a_partial_classs_halves_are_one`
holds it apart from the shape it used to be indistinguishable from: **a partial class is
still one fact with a location per part**, which is what a partial type has answered "where
is this written" with all along.

**What the key still has no field for is the assembly.** Two assemblies compiled in one
run may each declare `W.S`, and every field of the key agrees — same name, same namespace,
same arity, same modifiers — so they intern one row:

| | what a database holds for `W.S` declared in two assemblies |
|---|---|
| `csharp.Class` · `Interface` · `Record` · `Struct` · `csharp.FullName` | **one** fact for the two declarations |
| `csharp.DefinitionLocation` | **two** rows against it, one per assembly |
| `csharp.SymbolOf` · `DefinitionBySymbol` | two rows: the crossing is many-to-one, because `Package` puts the assembly identity in the symbol string |
| `csharp.Method` · `Field` · `Property` | fused as well wherever they agree, since each keys on a containing type that fused |
| `codemarkup.*` | **two** of each — the symbol is in every key there, so that half is correct |

Measured over the reference corpus, which carries `Assemblies.Left` and `Assemblies.Right`
for exactly this: **12** entities are reached by more than one declaration, **8** of them
this shape — two classes, three methods, one field and two properties — and the arity field
moves none of them, because the two sides agree at every arity.

The fix is a further key field and therefore another flag day;
`docs/unified-plan/15-retire-code-sigla.md` §S3 carries it as the open item, and
`Assemblies.Left/README.md` is the fixture's own account of the mechanism.

## Two things that had to be got right

**`Compile`, not `Build`.** Buildalyzer's default targets are `Clean;Build`, and both
delete things — `Build` because it depends on `IncrementalClean`, which removes what the
last build wrote and this one did not, and a design-time build writes nothing. Pointing
the default at a checkout empties every `bin` in it. It did that here, to this program's
own output, while it was running out of it.

**A key the compiler decides, not the formatting.** The old declaration key was
`{module, name, line}` — so a blank line inserted at the top of a file re-keyed every
declaration below it, two overloads written on one line collided, and the producer had to
carry conflict bookkeeping to notice. The entity key is what the compiler knows: a method
is its name, its containing type and its signature. Reformatting a file now moves every
`DefinitionLocation` and no identity, and the bookkeeping is gone because there is nothing
left for it to catch — a conflicting fact is refused by the server, by name (`ops-I4`),
and the run stops.

That rule now guards more than the kind. A declaration's type, its parameters' types and
its doc comment are values too, so **everything carrying one is emitted once per key**:
the first symbol to reach a key describes it and later ones do not. Two overloads written
on a single line therefore give one signature rather than a failed stream — which is the
right trade, because the alternative is losing the other eighteen million facts to a
collision nobody would have predicted.

## The flags

```
--source <path>       a .sln, .slnx, .csproj, or a directory holding one (required)
--root <path>         paths are reported relative to this (default: the solution's directory)
--dotnet <path>       the dotnet host to build with (default: <root>/.dotnet/dotnet if present)
--at <address>        where to write: [where//]name[@instance] (default: code, on
                      /tmp/fjord.sock)
--batch <n>           facts per block (default: 4096)
--max-files <n>       stop after n source files
--max-projects <n>    stop after n projects
--jobs <n>            builds, and files walked, at once (default: 4, or fewer cores)
--writers <n>         concurrent write streams, one connection each (default: 1)
--framework <tfm>     index only this target framework (default: one database per
                      framework the checkout compiles for, named <at>#<tfm>)
--configuration <c>   the configuration this index is resolved against, recorded as
                      config.Setting (default: Debug)
--strict              a project or target left out fails the run
--list-frameworks     print the frameworks this checkout compiles for, and stop
--no-refs             declarations only: no cross-references
--no-lines            do not write the line table (src.FileLine)
--styles              also write syntax highlighting (src.FileLineStyles)
--repo <id>           the repository this checkout is of, per file
--revision <rev>      the revision indexed (both, or neither: src.FileOrigin)
--no-docs             do not read doc comments (codemarkup.SymbolInfo.doc)
--no-restore          do not let the design-time build restore first
--dry-run             index and encode, but connect to nothing
--emit <path>         also write every block to a file
--no-smoke            do not query the index afterwards
--verbose             let MSBuild's output through
```

**`--batch` is a flag because finding out what it should be is the point of having
something to measure with.** A flush is a write stream, and the server interns a block
inside its per-database writer lock: bigger means fewer round trips and a longer hold.

**A checkout that compiles for two frameworks is indexed twice, into two databases.** A
project built for `net8.0` and one built for `net10.0` are different programs — different
preprocessor symbols, different references, often different members — and no key in the
schema can hold both. So the default fans out, writing `<at>#net8.0` and `<at>#net10.0`,
and each database says which framework it is through
`config.Setting {dimension = "framework"}`. A checkout with one framework writes one
database under the name it was given. `--framework` pins one; `--strict` refuses a run that
leaves a project out; `--list-frameworks` answers the caller who has to create the
databases first, which is a server operation this producer cannot do for itself.

**`--dry-run` is how to measure the volume**, since it encodes every block and writes
none. A connected run hands its facts to the client, which encodes them on the way out,
so the byte count is reported only when this program does the encoding itself.

**There is no degraded mode, and the numbers are why.** A walk that skipped MSBuild —
every `.cs` file under `--source`, parsed against the framework this program runs on, no
project graph and no NuGet — finds every declaration, because declarations are in the
syntax. It loses
references into a package's types, because the type is an error type and the member on it
binds to nothing. FluentValidation through the design-time build leaves **13** names
unresolved out of six and a half thousand; Roslyn's compilers parsed without MSBuild
leave **310,525** out of two and a half million. Different repositories, so not a
controlled comparison — but one name in five hundred against one in eight is the right
order of difference, and it is what asking MSBuild buys.

An index missing four fifths of its edges looks complete and answers wrongly, with
nothing in it to say so. **So a run whose projects all fail refuses**, naming what to fix,
rather than writing that index.

**`--emit <path>`** writes the same blocks to a file — sync marker, header, CRC and
payload, byte for byte what the wire carries. That is the fact-file format Phase 7b
ingests, so a large index can be captured once and replayed without Roslyn in the loop.

**A checkout too big for one machine is indexed per project, not per slice.** Holding
every tree of a large `--source` at once costs roughly 1.4 GB per ten thousand files
parsed, plus another 0.23 GB per thousand files *walked* as the symbol tables fill —
dotnet/runtime's `src/` is 32,710 files, which is more than most machines will give. The
answer is `--max-projects`, because a project is a compilation and a compilation is what
the memory is proportional to. Slicing a single compilation by *file* was the other
answer, and it dropped every reference that crossed a slice boundary — a cost paid in
missing edges rather than in time.

## Something big to point it at

Any .NET checkout will do, and the interesting ones are the ones nobody wrote for this.

```sh
git clone --depth 1 --filter=blob:limit=1m https://github.com/dotnet/roslyn.git
git clone --depth 1 https://github.com/OrchardCMS/OrchardCore.git
git clone --depth 1 https://github.com/JamesNK/Newtonsoft.Json.git
```

**A repository that pins its SDK will not design-time build here**, and that is not a bug
in either party: `global.json` names a version, `dotnet` refuses to substitute another,
and Roslyn's own repository pins an SDK preview that this machine does not have. Every
project then fails, and the loader falls back to parsing the `.cs` files — which for a
corpus that exists to be *large* costs the cross-assembly edges and nothing else. Delete
or relax the `global.json` in the checkout to get the full semantic index.

## What a run prints

This is a real one, and a small one: this repository's own solution, `--framework net10.0`,
against a release server.

```
indexed 55 file(s) in 10.8s
  src.File                                    55
  src.Symbol                              16,569
  src.FileLine                            16,801
  config.Setting                               7
  msbuild.Project                             20
  csharp.Name                              3,356
  csharp.Method                            2,005
  codemarkup.Definition                      958
  codemarkup.FileXRef                     14,658
  codemarkup.SearchEntry                     958
  …
  total                      152,173 facts in 82 blocks
  server                     133,860 created, 1,488,644 deduped
  contended                      0.1s  (364 of 152,173 facts waited for a batch)
  throughput                  14,057 facts/s

references: 18,247 resolved, 5,483 to declarations outside the index, 2 unresolved
```

Four of those numbers are worth reading twice.

**`created` counts every fact written, nested targets included; `deduped` those already
there.** A hundred and fifty-two thousand facts *sent* were 1,622,504 facts *touched* — a
factor of ten, which is what it costs to send each reference with its symbol and that
symbol's file nested inside it. Of those, 1,488,644 were already in the database and
133,860 were new. That number is interning working, and producing it is the whole point of the
exercise; `--dry-run` is the honest way to measure this side without one.

**`contended` is what the walk pays for sharing.** Several threads produce facts into
sixty-five per-predicate batches, and three hundred and sixty-four of a hundred and
fifty-two thousand found one already held — for a tenth of a second in total. It replaced a
single lock around the whole of fact production, and the number is here so the replacement
can be compared with what it replaced rather than assumed better.

**`5,483 to declarations outside the index` is the honest part.** Real code points at the
BCL and at packages; those references resolve to entities with no source location, and the
run counts them rather than dropping them or inventing targets. `2 unresolved` is a name
the compiler could not bind at all, which on a healthy checkout should be nearly zero.

**The line table is most of the bytes and none of the meaning.** A fact per line, blanks
included, because a table whose gaps mean "empty" cannot be told from one whose gaps mean
"not indexed" — and it is what lets a search hit be rendered with its context without
opening a file. `--no-lines` leaves it out when the semantic half is the point.

## What this does not tell you

**Nothing here is a measurement at scale, and the figures that were have been retired.**
This README used to carry a run over the whole of dotnet/runtime — 32,710 files, 18.2
million facts — and every number in it was taken over a schema that no longer exists, by a
mode that no longer exists, in a corpus nobody can rebuild. Annotating those figures would
have made them look current; they are gone instead, along with the register that held them.

[`bench/FINDINGS.md`](../../../bench/FINDINGS.md) is closed until a 1.0 pass for the same
reason, and it says what survives: the lessons, each banked in the tree with a guard. The
pass that re-takes the numbers owes a corpus first.

What is still true and worth knowing before pointing this at something large:

- **Memory is proportional to the compilation, not the checkout.** A project is a
  compilation and a compilation holds every symbol table it needs; `--max-projects` is the
  knob, because slicing a single compilation by *file* drops every reference that crosses a
  slice boundary — a cost paid in missing edges rather than in time.
- **A repository that pins its SDK will not design-time build here**, and that is not a bug
  in either party: `global.json` names a version and `dotnet` refuses to substitute another.
  Every project then fails to build, and this indexer refuses the run rather than writing a
  degraded index — which is the rule it exists to keep.
