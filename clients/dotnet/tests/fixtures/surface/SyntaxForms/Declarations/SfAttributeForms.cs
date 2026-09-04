using System;
using Surface.SyntaxForms.Directives;

// `Attribute` — one census row, and every target the grammar has a place for. The two
// declaration-level targets that can only be written at file scope go first, because an
// assembly or module attribute has to precede any namespace or type declaration.
//
// **An attribute is a reference to a constructor, written as a reference to a type.** The
// `SfMark` in `[SfMark(1)]` is an `IdentifierNameSyntax`, so a reference dispatch keyed on
// `SimpleNameSyntax` sees it — and what it binds to is the *type* `SfMarkAttribute`, found
// by the attribute name-lookup rule that appends `Attribute` to the spelling. The
// constructor the attribute actually invokes is reached by nothing:
// `AttributeSyntax` does not derive from `BaseObjectCreationExpressionSyntax`, so the walk's
// construction case never sees it. `[SfMark(1)]` and `new SfMarkAttribute(1)` are the same
// call, and only one of them is in the index.

[assembly: SfMark(10, Note = "assembly")]
[module: SfMark(11, Note = "module")]

namespace Surface.SyntaxForms.Declarations;

/// <summary>Every attribute target the grammar allows, on one type.</summary>
/// <typeparam name="TItem">A type parameter, so an attribute has a type parameter to sit on.</typeparam>
[SfMark(20)]
[SfMark(21, Note = "a second attribute in a second list")]
public sealed class SfAttributeForms<[SfMark(22)] TItem>
    where TItem : notnull
{
    /// <summary>A field with an attribute.</summary>
    [SfMark(23)]
    public int Marked;

    /// <summary>An event with an attribute on the event and on its backing field.</summary>
    [SfMark(24)]
    [field: SfMark(25)]
    public event EventHandler? Raised;

    /// <summary>A constructor with an attribute, whose parameter carries one too.</summary>
    /// <param name="held">What to hold.</param>
    [SfMark(26)]
    public SfAttributeForms([SfMark(27)] TItem held) => Held = held;

    /// <summary>A property with an attribute on the property and on one accessor.</summary>
    [SfMark(28)]
    public TItem Held
    {
        get;

        [SfMark(29)]
        private set;
    }

    /// <summary>An indexer with an attribute, and an attributed parameter.</summary>
    /// <param name="slot">Which slot.</param>
    /// <returns>The slot.</returns>
    [SfMark(30)]
    public int this[[SfMark(31)] int slot] => slot + Marked;

    /// <summary>
    /// A method with an attribute, an attributed return, an attributed parameter, and a
    /// local function and lambda inside it that carry their own.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <returns>Something derived from it.</returns>
    [SfMark(32)]
    [return: SfMark(33)]
    public int Every([SfMark(34)] int count)
    {
        // An attribute on a local function.
        [SfMark(35)]
        int Doubled(int value) => value * 2;

        // An attribute on a lambda, and on a lambda's parameter — two positions the C# 10
        // grammar added.
        Func<int, int> tripled = [SfMark(36)] ([SfMark(37)] int value) => value * 3;

        Raised?.Invoke(this, EventArgs.Empty);

        return Doubled(count) + tripled(count) + this[count];
    }

    /// <summary>An operator with an attribute, which is the last member kind that takes one.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Their marked sum.</returns>
    [SfMark(38)]
    public static int operator +(SfAttributeForms<TItem> left, SfAttributeForms<TItem> right) =>
        left.Marked + right.Marked;
}

/// <summary>An enum whose members carry attributes, which is a target with no other spelling.</summary>
[SfMark(40)]
public enum SfAttributedKind
{
    /// <summary>The zero value.</summary>
    [SfMark(41)]
    None = 0,

    /// <summary>The one value.</summary>
    [SfMark(42)]
    Some = 1,
}

/// <summary>A delegate whose declaration, return and parameter all carry attributes.</summary>
/// <param name="value">The value.</param>
/// <returns>The result.</returns>
[SfMark(43)]
[return: SfMark(44)]
public delegate int SfAttributedHandler([SfMark(45)] int value);
