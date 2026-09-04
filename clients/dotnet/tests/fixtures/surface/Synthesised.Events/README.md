# `Synthesised.Events` — the declaration kind the entity layer drops entirely

An event is the one member kind this indexer's entity layer was found to **drop**, not to
mis-name: `CsharpEntities.Build` ends its event arm at `IEventSymbol => Dropped()`, and `Declare`
returns on the null entity *before* it writes `SymbolOf`, `DefinitionBySymbol` or `Markup`. So no
event declaration in this project produces a `csharp` entity row, a `csharp.DefinitionLocation`,
or a `codemarkup` row of any kind — while `ScipSymbols.Of` spells every one of them perfectly
well, and `Reference` writes an xref from every `+=`, every `-=` and every read.

That asymmetry is the mechanism (`M20` in the corpus plan) and it is why this project is separate
from `Synthesised`, which holds the rest of the compiler's invented members: everything here is
**declared in source, spellable, and absent from the index**, which is the cheapest thing in the
whole corpus to gate — one query for a `SymbolXRef.target` that joins to no `Definition`.

## What is here, and where

| File | Holds |
|---|---|
| `Handlers.cs` | the two delegate types every event here is an event *of* (21.2): `EvReadingHandler` and the generic `EvPayloadHandler<TPayload>`. A delegate type **is** expressible, which is the contrast |
| `FieldLikeEvents.cs` | 15.8.2 field-like events: one declarator; **three declarators in one declaration** (`First, Second, Third`); a static one (15.8.4); one of a constructed generic delegate type; one in a struct (16.4.14); an abstract one and its field-like `override` (15.8.5) |
| `CustomEvents.cs` | 15.8.3 events with written accessors: expression-bodied `add`/`remove`, and a static pair with block bodies that compound-assign a delegate field |
| `InterfaceEvents.cs` | 19.4.5 an interface event; an interface event with a default implementation (C# 8); a `static abstract` one (C# 11); an implicit implementation; and an **explicit** implementation (19.6.2), whose name is a qualified name rather than an identifier |
| `EventUses.cs` | 12.23.6 event assignment — every event above subscribed and unsubscribed from *outside* its declaring type, plus `nameof` and a documentation `cref`, which are the two reference forms that name an event without using it |

## The census this project is built to be counted by

- **17 event symbols**, from **15 declaration nodes**: 11 `EventFieldDeclarationSyntax` carrying
  13 `VariableDeclarator`s, and 4 `EventDeclarationSyntax`. The two forms are reached by different
  routes — `GetDeclaredSymbol` on an `EventFieldDeclarationSyntax` returns null, so the field-like
  events are reachable *only* through their declarators, while the accessor form is reached from
  the declaration itself. `event EvReadingHandler? First, Second, Third;` is therefore the row
  that separates a walk that steps to `Declaration.Variables` (three events) from one that
  switches on the declaration (one event) from one that does both (six).
- **8 accessor declarations** (`add`/`remove` in the four accessor-formed events) and **26
  accessor symbols with no syntax at all** — 34 in total, two per event. An
  `AccessorDeclarationSyntax` is not in the declaration walk's switch, so `MethodKind.EventAdd`
  and `MethodKind.EventRemove` are a second, independent silence stacked on the first.
- **48 reference sites naming an event**: 26 `+=`/`-=` (15 and 11), 15 reads inside the declaring
  type (where a field-like event's name may also be assigned, 15.8.2), 3 `nameof`, and 4
  documentation `cref`s.
- **One of the 17 descriptors is backtick-quoted.** A SCIP descriptor for an event is
  `Name(symbol)` plus the term suffix `.` — the same suffix a field and a property get — and
  `Name` runs through `Escaped`, which quotes any name outside `[A-Za-z0-9_+$-]`.
  `EvExplicitNotifier`'s explicit implementation is the only event here whose `symbol.Name`
  is a dotted string, so it is the only one that reaches the escaping path, and the only
  event descriptor in the project that is not a bare identifier.
- **16 of the 17 events have at least one use.** The exception is `EvExplicitNotifier`'s explicit
  `IEvNotifier.Notified`, which **no expression in any program can name** (19.6.2) — every use of
  it goes through the interface and binds to `IEvNotifier.Notified`. So it is the one event here
  that is neither defined nor referenced, and the one whose absence a coverage query cannot
  distinguish from a walk that never visited the file.

## Properties, and what is deliberately absent

- **Every event is used, and every use is real code.** The uses are not `_ = E` fillers: handlers
  are recorded in a list, and each subscribe has a matching unsubscribe, so a reader can tell what
  the fixture would do if it ran.
- **Nothing here is one of the five run-killers.** No name at two arities (`EvReadingHandler` and
  `EvPayloadHandler<TPayload>` are different names), no type with two indexers (none has one), no
  partial member of any kind, and no `file` type. A project whose run dies proves nothing about a
  drop that is *silent*, which is the property being measured.
- **`<GenerateDocumentationFile>` is on** because a `cref` only binds when the parse runs in
  `DocumentationMode.Diagnose`. Without it, `<see cref="EvGauge.Read"/>` is trivia and that
  reference form does not exist in the tree at all.
