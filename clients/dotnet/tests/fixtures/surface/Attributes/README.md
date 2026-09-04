# `Attributes` — clause 22 (exceptions) and clause 23 (attributes)

Eighteen source files covering the 48 census rows whose `area` is `ecma-22` or `ecma-23`:
34 exercised, 14 recorded `not-applicable`, none quarantined. The split is not a judgement
call — the 34 exercised rows are exactly the 34 the census marked `hazard=yes`, and the 14
not-applicable ones are exactly the 14 whose `declares` column says `neither`. Every one of
those fourteen is a clause heading, a general introduction, or a statement about run-time
behaviour, so there is no row here where the census and the code disagree about whether an
index holds a fact.

Every construct names its clause in a comment, so a reader can walk from a census row to
the code that produces it.

The project is a library with two settings that the clauses require rather than prefer.
`Nullable` is `enable`, because outside an enabled nullable context the twelve 23.5.7
attributes parse and then check nothing — the applications would be inert and a query
asserting what they annotate would be asserting nothing. `DefineConstants` adds
`SURFACE_TRACE` and never defines `SURFACE_AUDIT`, because 23.5.3 is only observable as a
*difference*: one conditional symbol defined and one not, in one compilation.

## What is where

| Directory | Clauses | Holds |
|---|---|---|
| `Exceptions/` | 22.2–22.5 | a custom exception hierarchy, the two causes of an exception, and every part of the handler search |
| `Classes/` | 23.2.1–23.2.4 | the attribute class declarations, the positional/named parameter split, every legal parameter type, and a generic attribute class |
| `Specification/` | 23.3, 23.4.2, 23.4.3 | every attribute target including the two global ones, what an application compiles to, and reading applications back |
| `Reserved/` | 23.5.2–23.6 | `Conditional`, `Obsolete`, `AsyncMethodBuilder`, the three caller-info attributes, the twelve code-analysis attributes, `EnumeratorCancellation`, and the interoperation set |

## Why clause 23 is the interesting half

An attribute is a **declaration** — a class — and an application is a **reference to its
constructor**, not to the class. Clause 23.4.2 says so outright, and it is the reason this
project's shape is what it is: `AttrAuditAttribute` declares four constructors and the
applications below it select all four, so a query for "what does this application refer to"
has an answer (`.ctor(String, String[])`) that is not the attribute class and is not
distinguishable from its three siblings by name.

The only place in the project where an attribute class is referenced the way an ordinary
class is, is `Specification/AttrRetrieval.cs` — a `typeof`, a cast, a property read. The gap
between that count and the number of applications is the whole subject of the clause.

## The properties this project is built to have

- **Every declaration has a reference.** Every attribute class declared here is applied
  somewhere, every exception type is thrown or caught, and every method is called from
  something that compiles — so a reference count of zero means the resolution broke, not
  that the fixture is unusual. There are exactly three deliberate exceptions, each named
  below: `AttrTwin`, `AttrRetired.Removed`, and the members of both async method builders.
- **Nothing here is one of the five quarantined shapes.** No type name at two arities, no
  type with two `this[...]`, no partial member or partial type, no `file` type. Checked
  mechanically as well as by eye: reflecting over the built assembly finds 67 declared
  types, 67 distinct simple names, none at two arities and not one compiler-generated name
  among them that a source declaration also spells. The two indexers in the project are in
  two different types.
- **Every attribute target the clause lists is applied at least once**, and every target
  that has a *default* spelling is applied both ways — `[AttrDetail(...)]` and
  `[field: AttrDetail(...)]` on the same field, twice on the same parameter.
- **Every claim in a comment about metadata was checked against the built assembly**, by
  reflecting over `bin/Debug/net10.0/Attributes.dll`, not by reasoning. The section below is
  what that turned up; five comments were rewritten because the compiler disagreed with
  them.
- **Nothing in this project runs at run time by accident.** The interoperation declarations
  in `Reserved/AttrInterop.cs` name `kernel32.dll`, which does not exist on the machine that
  builds this. They are declarations, they are never called, and `AttrInteropUse` reaches
  them through `nameof` and `typeof` only.

## What the compiler actually does, which is not always what clause 23 says

Each of these is a verdict a query over the index has to match rather than a curiosity, and
each was found by inspecting the built assembly.

- **A field-like event's backing field carries the event's own name.**
  `public event EventHandler? Changed;` produces `event Changed` *and* a private field
  `Changed` in one type — unlike an auto-property, whose backing field is
  `<Name>k__BackingField`. So one source line declares two members with one name in one
  type, and an identity string of (type, member name) is one string for an event and a
  field. This is the closest this project comes to a refused write without writing any of
  the five quarantined shapes.
- **`[method: ...]` on a field-like event applies to both accessors.** One
  `[AttrRepeatable(3)]` section in `AttrEveryTarget<TItem>` is present on `add_Changed` and
  on `remove_Changed`: one application in the source, two in the assembly, with no syntax
  separating them.
- **A named constructor argument is stored positionally.** `[AttrAudit(note: "x")]` records
  one positional argument and no named arguments, exactly as `[AttrAudit("x")]` does. The
  parameter name survives only in the source, so the reference from an application to a
  *constructor parameter* is visible to a syntactic index and to nothing else.
  `[AttrAudit("x", Note = "y")]` is the one that really does record a named argument,
  because that one is a property assignment.
- **An enum argument loses the member name.** `AttrSeverity.Stop` is stored as `2`, and
  `new[] { AttrSeverity.Note, AttrSeverity.Stop }` as `{ 0, 2 }`. The enum *type* is
  retained. `nameof(Counted)` likewise becomes `"Counted"`, and `Level = 1 + 2 * 3` becomes
  `7`. A `typeof` argument is the exception — it survives as a type reference.
- **`[Conditional]` removes calls, never declarations.** All three local functions named
  `Trace` are emitted, including the one whose symbol is undefined and which therefore has
  no caller anywhere: `<Logs>g__Trace|1_0`, `<Logs>g__Trace|1_1` and
  `<LogsAgain>g__Trace|2_0`. The mangled name carries the enclosing method and two
  ordinals, so metadata separates three declarations that the source spells once — and the
  ordinals depend on declaration order.
- **A conditional attribute *class* really does vanish from its targets.**
  `AttrConditionalApplications` and its `Counted` property each carry two applications in
  the source and exactly one in the assembly. The class `AttrAuditedAttribute` is still
  emitted; only its applications are gone.
- **`MemberNotNull` diagnoses a name that resolves to nothing and `NotNullIfNotNull` does
  not.** `[MemberNotNull("_absent")]` is CS8776, a warning — so the dangling name still
  reaches metadata. `[return: NotNullIfNotNull("absent")]` draws no diagnostic at all.
  Both are in `Reserved/AttrNullabilityPostconditions.cs`, deliberately.
- **`CallerMemberName` reports the member, not the method.** A call inside `Weight`'s getter
  reports `Weight`, not `get_Weight`; inside an indexer, `Item`; inside an event accessor,
  `Changed`; in a constructor, `.ctor`; in the static constructor, `.cctor`; and inside a
  lambda or a local function, the enclosing member two levels up. Four of those six are not
  legal identifiers or are names the source never writes.
- **Two `DllImport` declarations can be one imported function.**
  `AttrNativeMethods.GetTickCount64` and `AttrNativeMethods.TicksRenamed` both resolve to
  `kernel32.dll!GetTickCount64`; the second says so through `EntryPoint`. A many-to-one an
  index has nothing to key on.

## The three declarations here that are deliberately unreferenced

A corpus whose every symbol is reached is easier to gate, so each exception is recorded
rather than left to be discovered.

- **`Classes.AttrTwin`** is an attribute class that no application in this corpus can name.
  `AttrTwinAttribute` sits beside it, so the short spelling `[AttrTwin]` is CS1614 —
  ambiguous — and the long spelling reaches the twin instead. Both types are in the
  assembly; one of them has no application, and the reason is a name, not an omission.
- **`Reserved.AttrRetired.Removed`** is marked `[Obsolete(..., true)]`, so every reference
  to it is CS0619, an *error*, which no `#pragma warning disable` can suppress. The only
  reference to it in this project is inside an `#if SURFACE_AUDIT` region that no
  compilation includes. Defining `SURFACE_AUDIT` makes this project fail to build, on
  purpose: that is what `error: true` means. The member itself is in metadata, flag and all.
- **Every member of `AttrTicketBuilder` and `AttrReceiptBuilder<TResult>`** is called by the
  assembly and by nothing an index can see. `[AsyncMethodBuilder(typeof(...))]` makes the
  compiler bind eight members by name and signature from generated state-machine code, so
  the single `typeof` in the attribute argument is the whole visible edge between an
  `async AttrTicket` method and the eight members it uses.

## Nothing here needs a quarantine project

None of the 48 rows requires one of the five refused-write shapes, and none of the shapes
appears. Two rows came close enough to be worth stating:

- **23.2.1** would reach a refused write if this project declared a generic attribute class
  beside a non-generic one of the same name. `AttrTypedAttribute<T>` is therefore at one
  arity only, with no `AttrTypedAttribute` anywhere in the corpus.
- **23.6** puts an ordinal on the members of a `ComImport` interface, which is the same
  property a partial member's two halves have and the reason the ordinal is worth asserting
  where it is safe. `IAttrComLedger` declares its two slots in one interface in one file,
  which is an ordinary declaration and not a partial anything. `LibraryImport` — the modern
  spelling of `DllImport` — is deliberately *not* used, because it requires
  `static partial`, which is the partial-member shape.
