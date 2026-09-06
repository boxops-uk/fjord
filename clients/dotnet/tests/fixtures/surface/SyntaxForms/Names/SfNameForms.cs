using System;
using System.Collections.Generic;
using Surface.SyntaxForms.Declarations;

// A using alias, so an alias-qualified name has something other than `global` to qualify
// with: `SfSys::Console` binds through a `SymbolKind.Alias` that this file declares.
using SfSys = System;

namespace Surface.SyntaxForms.Names;

// The five name forms a type or a member can be written as, gathered so the reference side
// of the walk can be read against them:
//
//   IdentifierName      a bare identifier                       — a `SimpleNameSyntax`
//   GenericName         `C<T>` at a use site                    — a `SimpleNameSyntax`
//   QualifiedName       `A.B`                                   — built from two of them
//   AliasQualifiedName  `global::A` and `alias::A`              — built from two of them
//   PredefinedType      `int`, `string`, `void`                 — *not* a name node at all
//
// The last line is the finding this file exists to make checkable. A reference dispatch that
// switches on `SimpleNameSyntax` sees the first two directly and the parts of the next two,
// and never sees a `PredefinedTypeSyntax` — so `int` in `int Count()` is a reference to
// `System.Int32` that the index does not hold, while `Int32 Count()` beside it is one it
// does.

/// <summary>Each name form, in a position where it is a reference to a declaration.</summary>
public sealed class SfNameForms
{
    /// <summary>IdentifierName — a bare identifier naming a type declared in this corpus.</summary>
    public SfLedgerClass Bare { get; } = new SfLedgerClass();

    /// <summary>GenericName — a constructed type written with its type argument list.</summary>
    public SfLedgerBox<string> Constructed { get; } = new SfLedgerBox<string>("held");

    /// <summary>GenericName nested inside a GenericName, so the arity is not the outer one.</summary>
    public Dictionary<string, List<int>> Nested { get; } = new Dictionary<string, List<int>>();

    /// <summary>QualifiedName — a nested type reached through its container.</summary>
    public SfLedgerClass.SfNestedEntry Qualified { get; } = new SfLedgerClass.SfNestedEntry();

    /// <summary>QualifiedName through namespaces as well as types.</summary>
    public System.Text.StringBuilder Deep { get; } = new System.Text.StringBuilder();

    /// <summary>
    /// AliasQualifiedName with the <c>global</c> alias — an <c>AliasQualifiedNameSyntax</c>
    /// whose alias identifier binds to no declaration at all.
    /// </summary>
    public global::System.Int32 GlobalQualified { get; } = 1;

    /// <summary>
    /// AliasQualifiedName with a <c>using</c> alias — the same syntax kind, but the alias
    /// identifier now binds to an <c>IAliasSymbol</c> the file declares.
    /// </summary>
    /// <returns>A rendering of the alias-qualified type.</returns>
    public string AliasQualified()
    {
        SfSys::Console.Out.Flush();
        return typeof(SfSys::Text.StringBuilder).Name;
    }

    /// <summary>PredefinedType in each of the positions a type can appear in.</summary>
    /// <param name="count">A predefined type as a parameter type.</param>
    /// <returns>A predefined type as a return type.</returns>
    public string Predefined(int count)
    {
        // `object`, `bool`, `char`, `double` and `decimal` as local types; `void` is the
        // return type of `Nothing` below. Each is a `PredefinedTypeSyntax`, and none of them
        // is a name.
        object boxed = count;
        bool positive = count > 0;
        char first = 'x';
        double scaled = count / 2.0;
        decimal exact = count;
        return $"{boxed}{positive}{first}{scaled}{exact}";
    }

    /// <summary>PredefinedType as a <c>void</c> return type.</summary>
    public void Nothing()
    {
    }

    /// <summary>
    /// The same reference written as a predefined type and as a name, so the two spellings
    /// sit next to each other.
    /// </summary>
    /// <returns>Both totals.</returns>
    public (int Predefined, Int32 Named) BothSpellings()
    {
        int predefined = 1;
        Int32 named = 2;
        return (predefined, named);
    }
}
