# `Docs` — Annex D of the surface corpus

Documentation comments, in eight files. The slice is `POPULATION.tsv` rows whose `area` is
`annex-d` — 30 rows, of which 14 are marked `hazard`.

Annex D is the load-bearing annex for this indexer, because the indexer uses the
**documentation identifier** — D.4.2's ID string — as the sort key that assigns overload
ordinals. So the annex is not documentation here: it is identity. Every ID string quoted in
these files was read out of the `Docs.xml` this project emits, not recalled, and the
`GenerateDocumentationFile` property in the `.csproj` is what makes that file exist.

| File | Clauses |
|---|---|
| `DocComments.cs` | D.1 the mechanism and the two lexical forms; D.2 the introduction, and where a comment may sit |
| `DocTags.cs` | D.3.1 the tag list is open; D.3.2 `<c>`; D.3.3 `<code>`; D.3.4 `<example>`; D.3.7 `<list>`; D.3.8 `<para>`; D.3.12 `<remarks>`; D.3.13 `<returns>`; D.3.16 `<summary>`; D.3.19 `<value>` |
| `DocIncluded.cs`, `DocIncluded.xml` | D.3.6 `<include>`, resolved, file-missing and no-match |
| `DocCrefs.cs` | D.3.5 `<exception>`; D.3.11 `<permission>`; D.3.14 `<see>`; D.3.15 `<seealso>` — and every form a `cref` can take |
| `DocParams.cs` | D.3.9 `<param>`; D.3.10 `<paramref>`; D.3.17 `<typeparam>`; D.3.18 `<typeparamref>`, well formed and malformed |
| `DocIdStrings.cs` | D.4.1 processing; D.4.2 the ID string format, one declaration per rule; D.4.3 the examples |
| `DocIdHazards.cs` | D.4.2 and D.4.3 again, as the shapes that make two declarations want one string |
| `DocAnnexExample.cs` | D.5 and D.5.1 the annex's worked example, transliterated; D.5.2 the XML this compiler emitted for it |

Every construct carries the clause number it comes from in its doc comment, and every
declaration in the project carries a documentation comment — which is not house style here
but a consequence of the subject: with `GenerateDocumentationFile` on, an undocumented public
member is CS1591, and there are none.

`DocIncluded.xml` is **source**, not build output. The compiler reads it while compiling
`DocIncluded.cs`, which points four `<include>` tags at it; deleting it turns all four into
CS1589 warnings and empties three members' documentation.

## Properties this project is built to have

- **It compiles.** `dotnet build Docs/Docs.csproj --nologo -m:1 -nodeReuse:false` exits 0:
  `Build succeeded. 0 Error(s), 13 Warning(s)`.
- **Its thirteen warnings are the evidence, not noise.** Every one is the observable effect of
  a clause, and there is nothing else:
  - CS1587 once — D.2's misplaced comment, in `DocMisplacedComment.Count`.
  - CS1589 once — D.3.6's missing include file, in `DocIncludedText.FromMissingFile`.
  - CS0419 once — D.3.14's unqualified cref to an overload set, in `DocCrefForms.OverloadSet`.
  - CS1574 twice — D.4.2's `!:` error form: a name with no declaration, and a declaration with
    no cref syntax (the explicit interface implementation).
  - CS1572, CS1571, CS1573 once each — D.3.9's param tag for a non-parameter, duplicate tag,
    and undocumented sibling, all in `DocParamMistakes.Wrong`.
  - CS1734 and CS1735 once each — D.3.10's and D.3.18's references to nothing, in
    `DocParamMistakes.Ghosts`.
  - CS0693 once — D.3.17's method type parameter shadowing its type's, in
    `DocParamShadow<T>.Where<T>`.
  - CS0659 and CS0661 once each — D.5.1's example overrides `Equals` and defines `==` without
    overriding `GetHashCode`, because the annex's example does not. Transliterating it
    faithfully is the point of that file.
- **One type name at one arity.** All 37 type declarations were checked mechanically: no name
  appears at two arities anywhere. `DocCache<TItem>`, `DocIdGenerics<TFirst, TSecond>`,
  `DocTypeParamTags<TItem>`, `DocParamShadow<T>`, `DocIdGenericsInner<TThird>` and
  `DocIdPayloadHandler<TPayload>` have no non-generic twins.
- **One indexer per type.** Four types declare a `this[int]` — `DocCatalog`, `DocIdShapes`,
  `DocParamTags` and `DocIndexerNamed`. None declares a second.
- **No partial anything, and no file-local type.** The word `partial` does not appear in this
  project at all, and `file` appears only as `<include>`'s attribute and in prose about the
  documentation file — never as a modifier. Both shapes kill an indexing run.
- **No two declarations mint the same ID string.** The 185 `<member>` entries in `Docs.xml`
  were extracted and checked for duplicates: there are none. That is the invariant the run
  depends on, and it is stated as a fact about the emitted file rather than as an intention.
- **Names are unique across the corpus by prefix.** Every type is `Doc…` (interfaces
  `IDoc…`), and each was grepped against every other `.cs` file under `surface/`: no name
  appears outside this project. Namespaces are `Surface.Docs` and five children of it, and no
  other project declares any of them.

## What the hazards are for

A hazard row was judged able to make two declarations want one identity string. In this area
they fall into four kinds, and a query over the indexed corpus has to answer each differently.

### 1. The ID string is the identity, and it is coarser than the language in five places

Each of these is an encoding that is **not injective**. Three of them cannot be provoked as an
overload pair, because C# refuses the declaration before the annex gets its chance — which
means the annex is rescued by the language rather than by the format, and that is worth
recording as much as a live collision is:

| Encoding | Collides | Provokable in one type? |
|---|---|---|
| `params int[]` and `int[]` are both `System.Int32[]` | yes | no — CS0111. `WithArray` and `WithParams` carry it under two names |
| `ref`, `out`, `in`, `ref readonly` are all `@` | yes | no — CS0663. `WithRef`, `WithOut`, `WithIn`, `WithRefReadonly` carry it under four names |
| `(int, int)`, `(int a, int b)`, `ValueTuple<int, int>` all become `System.ValueTuple{System.Int32,System.Int32}`; element names vanish | yes | no — CS0111. `WithTuple` carries the one form |
| `int?` and `System.Nullable<int>` both become `System.Nullable{System.Int32}` | yes | no — one type, two spellings |
| A finalizer's member name is `Finalize`, which a method could also be called | yes | no — CS0111, verified |

### 2. A conversion operator is the one member the descriptor cannot express

`DocConvertible` declares four conversions in two pairs. `op_Implicit(DocConvertible)` appears
twice and `op_Explicit(DocConvertible)` twice; within each pair the containing type, the member
name and the **entire argument list** are identical, and the `~` return type is the only
difference:

```
M:Surface.Docs.Ids.DocConvertible.op_Implicit(Surface.Docs.Ids.DocConvertible)~System.Int32
M:Surface.Docs.Ids.DocConvertible.op_Implicit(Surface.Docs.Ids.DocConvertible)~System.Int64
```

This is the only member kind in C# that can do it: CS0111 stops every other same-signature
pair, and CS0557 stops only a conversion pair with the same source *and* target. So a
descriptor of (type, name, argument types) mints one string for two members, and the ordinal
assigned by sorting ID strings is the only thing left. `op_CheckedExplicit` is a fifth member
whose ID differs from the `short` conversion in the member name alone.

### 3. Two references, one string — and one of them means something wider

Verified in the emitted file: `M:Surface.Docs.Crefs.DocOverloadSet.Emit` is

- the ID string the **nullary overload declares for itself**, and
- what `<see cref="DocOverloadSet.Emit()"/>` resolves to, and
- what `<see cref="DocOverloadSet.Emit"/>` resolves to — a cref to the whole set of four,
  which the compiler warns about (CS0419), picks an overload for, and writes the same string
  for.

Three occurrences of that string in `Docs.xml`, meaning two different things, plus a
declaration. That is D.3.14's hazard and it is not a name clash in C# at all.

### 4. A name whose scope the tag does not carry

`<param>`, `<paramref>`, `<typeparam>` and `<typeparamref>` reference a declaration by
**name**, and a parameter has no ID string of its own — so the target is (this member, this
name) and the member half comes from outside the tag.

- `DocParamTags` declares three `Store` overloads, each with a parameter named `value`, each
  documented `<param name="value">`. Three tags, one name, three targets; only the ordinal
  separating the overloads separates them.
- `DocParamShadow<T>.Where<T>` documents a `<typeparam name="T">` that is not the type's `T`.
  The ID strings do separate them — `` `0 `` against ``` ``0 ``` — but the display string "T"
  does not, and neither does the tag's `name` attribute. A `<typeparamref name="T"/>` inside
  that method resolves to the method's parameter, shadowing the type's.
- `DocExplicitSink` declares three members spelled `Accept`: two explicit interface
  implementations and one ordinary method. Their ID strings differ; their simple names do not.
- `DocSignal.Fired` is a field-like event, which declares **two** members from one line — the
  event and a compiler-named backing field. Only the event has an ID string, so the annex
  offers nothing to tell them apart with. Writing the field by hand beside the event is
  CS0102, verified.
- `DocIdAccess` declares four `Emit` overloads at four accessibilities. Nothing in any of the
  four ID strings records which is private and which is public: D.4.1's flat `<members>` list
  holds ID strings and nothing else.

## What the annex and C# cannot both say

Three findings, each checked against the emitted file rather than assumed, and each a place
where an index will answer one of two ways:

1. **A resolved `cref` cannot name a constructed generic type.** `DocCache{string}` and
   `DocCache<string>` are both CS1584 — after a cref name, braces hold a type-parameter
   *declaration* list, so only an identifier fits — and `DocCache{TItem}` resolves to the
   **unbound** `T:…DocCache`1`. A constructed type reaches the file only as a method cref's
   parameter, where `<int>` does bind and becomes `{System.Int32}`, or as a verbatim
   prefixed cref nobody resolved. All three forms are in `DocCrefForms.Constructed`.
2. **A resolved `cref` cannot name an explicit interface implementation.** The ID string
   exists — `M:…DocExplicitSink.Surface#Docs#Crefs#IDocSink#Accept(System.String)` — and the
   only way to write it is verbatim, which means the compiler never checks it. Written as C#
   it is CS1574, and `DocCrefBroken.UnnameableMember` carries that case. Both are in the file:
   one as a resolved reference the compiler did not verify, one as `!:` text.
3. **No C# construct emits an `N:` entry.** A documentation comment on a namespace declaration
   is CS1587 and reaches the file as nothing. The `N:` prefix appears only as a cref target,
   four times in this project.

## What is deliberately absent

- **A nested type beside a same-named namespace.** `Surface.Docs.Ids.DocIdShapes.DocIdInner`
  is a nested type, and a type `DocIdInner` in a namespace `Surface.Docs.Ids.DocIdShapes`
  would mint the **same ID string** — D.4.2 separates a nested type from its container with
  the same period a namespace uses. C# permits both to exist. It is the sharpest collision in
  the annex and it is not written here: it is not one of the five known quarantine shapes, and
  a refused write would kill the whole-corpus run. `Types/README.md` predicts the same shape
  from Roslyn's display string; this is the same collision reached from the annex's side.
- **A `<permission>` cref to `System.Security.Permissions.SecurityPermission`**, which is what
  the annex's era would have written. That type is not in the framework reference for
  `net10.0` and CI has no network, so `DocContract.Read` documents
  `System.Security.SecurityException` instead. The tag, its resolution and its ID string are
  the same shape.
- **`<include>` reaching a second assembly's documentation.** `<include>` reads an XML file,
  not a compiled reference, so this needs no `ProjectReference` — and there are none.
