# `quarantine` — the five run-killers, each indexed alone

Five projects, one mechanism each. Every one of them is a **conforming C# program that this
indexer cannot index**: the walk mints one identity string for two declarations, an
immutable-database key that holds a value receives two different values, ingest raises
`Conflict`, `is_peers_fault()` fails the write stream, and the run dies mid-write. Every
fact after the refusal in that walk is lost.

**These projects are excluded from the corpus solution by design.** The assemble stage must
not add `quarantine/*/*.csproj` to `Surface.slnx`, and a whole-corpus run must not be given
this directory — one arity pair anywhere in the walk would take the other twenty-one
projects down with it, and every mechanism behind the refusal would become unmeasurable.
They are indexed **one at a time**, each on its own (`--source quarantine/Arity/Arity.csproj`
and so on), so that the conflict each provokes **names itself** instead of being masked by
whichever killer the walk happened to reach first.

That attribution is the entire value here, and it is why each project is the *minimum* that
provokes its own conflict and nothing else. There is no arity pair in `Partial`. There is no
overload beside the partial method in `Partial` — that belongs to `Ordinal`. There is no
arity stack in `FileLocal`, for the same reason, and the omission is stated below rather
than quietly made.

## What each provokes, and the predicate predicted to conflict

Only three predicates in the schema carry a value side, so only three can refuse a write:

| predicate | key | value |
| --- | --- | --- |
| `codemarkup.Definition` | `{symbol, file}` | `{span, kind, name, qualified}` |
| `codemarkup.SymbolInfo` | `{symbol}` | `{signature, doc, modifiers}` |
| `codemarkup.FileDefinition` | `{file, span, symbol}` | `{kind, name}` |

`FileDefinition` carries the span in its key, so it never conflicts. `Definition` conflicts
when two declarations of one symbol sit in **one file**. `SymbolInfo`, keyed on the symbol
alone, conflicts **across files too** — `signature` is a `Hover` display string with
`IncludeTypeParameters` and `IncludeName`, and `qualified` is `ToDisplayString()`, so *"the
two declarations disagree about their display string"* is exactly the conflict test.
Everything else in `codemarkup` and every `csharp.*` predicate is key-only, and a bad
identity there merges in silence.

### `Arity/` — M1: arity is absent from type identity, at every path segment

`Descriptor`'s `NamedType` arm is `Name(symbol) + '#'`, and `ISymbol.Name` carries no arity.
`ArityStore`, `ArityStore<T>` and `ArityStore<T, U>` are one string; because the path is
built by walking `ContainingSymbol`, `ArityOuter<T>.ArityInner<U>` and
`ArityOuter.ArityInner<U, V>` also merge, and in their **first** segment as well as their
last. Members, parameters and type parameters hanging off them inherit the merge.

- **Predicted conflict:** `codemarkup.SymbolInfo {symbol = …/ArityStore#}` receives
  `ArityStore`, `ArityStore<T>` and `ArityStore<T, U>` — and `codemarkup.Definition
  {symbol = …/ArityStore#, file = ArityStore.cs}` receives three spans, because all three
  declarations share a file. Both keys refuse.
- **The trap for a fix:** `typeof(ArityStore<>)` and `nameof(ArityStore<>)` write no type
  argument at all, yet Roslyn reports `Arity` 1 for them. Arity must come from
  `MetadataName`/`Arity` on the symbol, never from counting written type arguments.
- **Also merged, silently:** `[ArityMark]` and `[ArityMark<string>]` both reference a
  constructor spelled ``ArityMarkAttribute#`.ctor`().``, so two applied attributes are one
  reference target. First in the fix order — this is the highest-volume killer, firing on
  any repository that declares a name at two arities, and on `ValueTuple`'s nine.

### `Terms/` — M4: the term descriptor has no disambiguator

`Descriptor`'s Property/Field/Event arm is `Name(symbol) + '.'`, and the ordinal from
`Disambiguator` exists only on the Method arm. An indexer's `ISymbol.Name` is the literal
`this[]`, so a type's indexers are one string however many it has.

- **Predicted conflict:** ``codemarkup.Definition {symbol = …/TermsIndexed#`this[]`., file =
  TermsIndexed.cs}`` receives three spans — `Indexer.NameLocation` gives each declaration
  its own `ThisKeyword` token, so the values differ and it is a refusal rather than a
  dedupe — and `codemarkup.SymbolInfo` on the same symbol receives the three differing
  `Hover` signatures.
- **The second route to the same arm:** `TermsLookup`'s two explicit implementations of
  `ITermsMap<string, int>`'s overloaded indexer both spell
  ``TermsLookup#`Surface.Quarantine.Terms.ITermsMap<System.String,System.Int32>.this[]`.``,
  because for an explicit implementation `ISymbol.Name` is the interface's *display string*
  joined to the member's metadata name and the arm appends only `.`. Same refusal, and it
  is why the fix has to be a disambiguator on the term arm rather than a special case for
  element access.
- **The inversion to gate:** the accessors of those indexers reach the Method arm and *are*
  ordinal-separated (`get_Item()`, `get_Item(+1)`, `get_Item(+2)`), so the getters are
  distinguishable exactly where the properties owning them are not — and adding a fourth
  indexer renumbers the existing three. Second in the fix order; nothing gates the SCIP
  string at all, and no fixture elsewhere in this repository declares two indexers in one
  type.

### `Partial/` — M3: `Declare` runs per declaration syntax, identity comes from the symbol

Needs `<LangVersion>preview</LangVersion>`: partial properties and indexers are C# 13 and
need nothing, but partial constructors and partial events are C# 14 and are CS8703/CS0246
under a compiler whose `LanguageVersion.Default` is still C# 13.

- **Predicted conflict, one-file face:** `PartialSplit.cs` declares the type in two parts in
  one file and gives five member kinds two halves each, so
  `codemarkup.Definition {symbol, file = PartialSplit.cs}` receives two spans for
  `…/PartialSplit#`, for `…#Update().`, for `…#Label.`, for ``…#`this[]`.`` and for
  ``…#`.ctor`().`` The halves are two distinct `ISymbol`s that mint one string: same
  `Name`, same `ContainingType`, and `Disambiguator` gives both the bare form.
- **Predicted conflict, cross-file face:** `PartialHandler`'s halves sit in two files and
  name their parameters differently, which clause 15.6.9 explicitly permits (the compiler
  says so with CS8826, left un-suppressed as the evidence). `codemarkup.SymbolInfo {symbol =
  …/PartialHandler#Handle().}` receives `void PartialHandler.Handle<TItem>(TItem item)` and
  `void PartialHandler.Handle<TValue>(TValue value)`. This face does **not** need one file,
  and it is how a partial member is normally written. `doc` is not a second discriminator:
  Roslyn resolves the comment to the implementing part for both halves.
- **The residue after the identity fix:** `Edges` is called once per declaration and writes
  `csharp.MethodParameter {method, index, parameter}` — all key, no value — so both rows are
  accepted and the method has parameter `item` at index 0 *and* parameter `value` at index 0;
  `MethodTypeParameter` likewise holds `TItem` and `TValue` both at index 0. A wrong answer,
  not a refusal, so it needs its own count assertion.
- **The contrast row, which must not be broken by the fix:** the type `PartialHandler` has
  its two parts in two files, and `codemarkup.sigla`'s own comment blesses *"a symbol
  declared in two files — a C# partial class — is two facts"*. A gate here has to separate a
  member with two halves from a type with two parts. **Also silent:** the partial *event*'s
  two halves are two different node kinds and reach the markup layer not at all, because an
  event has no entity. Third in the fix order.

### `Ordinal/` — M2 (declaration face): a miss in the member list is spelled as ordinal zero

`Disambiguator` takes `ContainingType.GetMembers()`, filters to same-named methods, sorts by
documentation id, `FindIndex`es the symbol, and returns `index <= 0 ? "" : "+N"`.
`GetMembers()` returns only the **defining** part of a partial method, so `FindIndex` on the
implementing part answers **−1**, and the `index <= 0` guard spells that miss as *the first
overload*. The reference-side face of the same root lives in `hazards/Bindings`, because
that face completes.

- **Predicted conflict:** the documentation ids sort `Send(System.Int32)` before
  `Send(System.String)`, so ordinal zero belongs to `Send(int)`. The implementing half of
  `Send(string)` takes the bare form too, and `codemarkup.SymbolInfo {symbol =
  …/OrdinalCourier#Send().}` receives `void OrdinalCourier.Send(int count)` and `void
  OrdinalCourier.Send(string text)`. Two unrelated overloads, one symbol. Note that the
  *defining* half correctly spells `Send(+1).`, so the two halves of one member disagree with
  each other and one of them agrees with a method it has nothing to do with.
- The halves name their parameter identically on purpose: the display strings must differ
  because the two *methods* differ, not because 15.6.9 let the names drift — that is
  `Partial`'s mechanism, and mixing them in would make the refusal unattributable. The
  partial half is `public` (C# 9) only so the calls can live in `OrdinalUse.cs`.
- **Would-be wrong answer if the merge were silent:** `Send(1)` binds to a symbol whose
  `Definition` rows include `Send(string)`'s body, so go-to-definition on an `int` call lands
  in the `string` overload.

**Two stated `<Compile>` orders.** This project's `.csproj` sets
`EnableDefaultCompileItems=false` and lists `OrdinalPartA.cs`, then `OrdinalPartB.cs`, then
`OrdinalUse.cs`. **Reversing the first two lines is the second index of this project**, and
what the reversal is meant to show is that the overload-to-symbol mapping is a property of
the *code* and not of the item order. `OrdinalSub<T>` is the face that makes it bite:
`Take(T)` is declared in part A and `Take(int)` in part B, and while their ids differ on the
open type, on `OrdinalSub<int>` both members carry the byte-identical id
`M:….OrdinalSub{System.Int32}.Take(System.Int32)` — the sort ties, and `OrderBy`'s stability
hands the answer back to the order in which the compilation received its trees.
`OrdinalUse.cs` is listed last in both orders so that swapping A and B moves no call site.
`ScipSymbolsTests.A_partial_classs_overloads_do_not_depend_on_file_order` gates only the
non-generic case, where the ids differ and the sort cannot tie. Fourth in the fix order.

### `FileLocal/` — M5: a file-scoped name has no file in its descriptor path

`file class C` restricts a name to its declaring file, and Roslyn keeps that only in
`INamedTypeSymbol.MetadataName`, as a per-file prefix (`<F0>FD8A78B4…__C`). `ISymbol.Name`,
`ToDisplayString()` and `GetDocumentationCommentId()` are all plain `C`, and the descriptor
path has no segment for the file — so two `file class FileLocalHelper`es in two files and the
ordinary `class FileLocalHelper` beside them are three distinct types with one identity.

- **Predicted conflict:** each of the three types holds exactly one `Measure`, so each is the
  lone `Measure` of its own type and each takes the bare disambiguator
  `…/FileLocalHelper#Measure().`. `codemarkup.SymbolInfo` on that one key receives `int
  FileLocalHelper.Measure(int a)`, `…Measure(string a)` and `…Measure(double a)`. Three
  values, one key, **across files** — which is why the members are what this project is
  built around.
- **Silent at type level:** the three type declarations agree on `signature`, `modifiers` and
  `doc`, so `SymbolInfo` dedupes, and `Definition`'s key holds `file`, so those differ. The
  types merge into one entity with three declaration sites, `SearchEntry` and `SymbolByName`
  merged — a wrong answer, and the reason the type-level face alone would not fail the run.
- **Also wrong:** a cross-reference seek on the symbol declared in `FileLocalFirst.cs`
  returns the uses in all three files, so a rename driven off the index edits code in files
  that never mentioned the type.
- **Deliberately omitted, and predicted anyway:** `file class FileLocalHelper` beside `file
  class FileLocalHelper<T>` in one file would put two spans under one `{symbol, file}` key and
  two signatures under one `{symbol}` key, and the plan names it as a face of M5. It is not
  written here because it is an arity pair: it would refuse in the first file, before the walk
  reached the second, and the refusal would be attributable to `Arity`'s M1 rather than to
  file-locality. Index `Arity` for that shape; assert file-locality on `Measure`.
- Fifth in the fix order; the fix borrows `MetadataName`'s per-file prefix. `file`,
  `IsFileLocal` and `MetadataName`-as-a-discriminator appear nowhere in `clients/dotnet`
  today.

## Properties these five projects are built to have

- **Each compiles, with zero errors.** A run-killer that does not compile is not this
  corpus's business. `dotnet build` over each of the five exits 0; `Partial` warns CS8826
  and that warning is load-bearing evidence, not noise.
- **Each holds exactly one mechanism.** No project contains another's shape. When a run over
  one of them dies, the refused key names the mechanism with no further reasoning.
- **Every name is unique across the whole corpus**, prefixed by its project (`Arity…`,
  `Terms…`, `Partial…`, `Ordinal…`, `FileLocal…`) and namespaced
  `Surface.Quarantine.<Name>`, so that a fixed indexer can one day walk all twenty-seven
  projects in one run without any of these colliding with a sibling's types.
- **No `PackageReference`, no `ProjectReference`.** Everything comes from the framework
  reference alone, so CI needs no network.
- **The conflict is on a value-bearing predicate in every case**, which is what makes each of
  these fatal rather than silent. The silent faces are documented beside each one, because a
  fix that turns a refusal into a merge has not fixed anything.
