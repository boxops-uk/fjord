// Clause 16.4.13 — Indexers. An indexer of a struct follows the rules for an indexer of a
// class. Its declared name is the keyword `this`, and its name in metadata is `Item`
// unless an IndexerName attribute says otherwise — so an indexer is the one member of a
// C# type whose name appears nowhere in its own declaration.
//
// ONE INDEXER PER TYPE, THROUGHOUT. A type with two `this[...]` declarations is one of the
// five shapes that refuse a write in this indexer, so the parameter-list variations that
// would ordinarily be overloads of one indexer are spread over four structs here. The
// two-indexer variant is left to the quarantine project that owns it; the row itself is
// exercised here, by everything else an indexer has.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.13 — an indexer with one integer parameter, get and set. Its metadata name
/// is <c>Item</c>, which is written nowhere below.
/// </summary>
public struct StIndexedByInt
{
    private int _a;
    private int _b;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StIndexedByInt(int a, int b)
    {
        _a = a;
        _b = b;
    }

    /// <summary>Clause 16.4.13 — the one indexer of this type.</summary>
    public int this[int index]
    {
        get => index == 0 ? _a : _b;
        set
        {
            if (index == 0)
            {
                _a = value;
            }
            else
            {
                _b = value;
            }
        }
    }
}

/// <summary>
/// Clause 16.4.13 — an indexer keyed by a string, in its own type because it would
/// otherwise be a second indexer.
/// </summary>
public struct StIndexedByName
{
    private double _width;
    private double _height;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StIndexedByName(double width, double height)
    {
        _width = width;
        _height = height;
    }

    /// <summary>Clause 16.4.13 — get-only, keyed by name.</summary>
    public double this[string key] => key == "width" ? _width : _height;
}

/// <summary>
/// Clause 16.4.13 — an indexer with two parameters, again in its own type. This is the
/// declaration that would collide with <c>StIndexedByInt</c>'s if the two sat together.
/// </summary>
public struct StIndexedByPair
{
    private readonly int _stride;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StIndexedByPair(int stride) => _stride = stride;

    /// <summary>Clause 16.4.13 — two parameters, one indexer.</summary>
    public int this[int row, int column] => (row * _stride) + column;
}

/// <summary>
/// Clause 16.4.13 hazard — an indexer whose metadata name is not <c>Item</c>, beside a
/// property that IS called <c>Item</c>. Legal only because the attribute moved the
/// indexer out of the way: source name `this`, metadata name `Slot`, and a real property
/// holding the name the indexer would otherwise have taken.
/// </summary>
public struct StRenamedIndexer
{
    private int _held;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StRenamedIndexer(int held) => _held = held;

    /// <summary>Clause 16.4.13 — the renamed indexer.</summary>
    [IndexerName("Slot")]
    public int this[int index]
    {
        get => _held + index;
        set => _held = value - index;
    }

    /// <summary>
    /// Clause 16.4.11 — a property called <c>Item</c>, which is the name the indexer above
    /// gave up. Without the attribute this declaration is a compile error.
    /// </summary>
    public int Item => _held;
}

/// <summary>
/// Clause 16.4.13 / 16.3.2 — a readonly struct's indexer, implicitly a readonly member, and
/// one that takes a System.Index so the post-standard `^` syntax reaches it.
/// </summary>
public readonly struct StFrozenIndexed
{
    private readonly int[] _values;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StFrozenIndexed(int[] values) => _values = values;

    /// <summary>Clause 16.4.11 — the length, which is what makes an implicit Index work.</summary>
    public int Length => _values.Length;

    /// <summary>Clause 16.4.13 — the one indexer, keyed by System.Index.</summary>
    public int this[Index index] => _values[index.GetOffset(_values.Length)];
}

/// <summary>
/// Clause 16.4.13 — the element accesses. An element access names no member, so every line
/// here is a reference to a declaration whose name the line does not contain.
/// </summary>
public static class StIndexerUse
{
    /// <summary>Clause 16.4.13 — a get accessor and a set accessor through one syntax.</summary>
    public static int RoundTrip()
    {
        StIndexedByInt pair = new StIndexedByInt(1, 2);
        pair[0] = 5;
        return pair[0] + pair[1];
    }

    /// <summary>Clause 16.4.13 — the string-keyed indexer.</summary>
    public static double Width(StIndexedByName box) => box["width"];

    /// <summary>Clause 16.4.13 — the two-parameter indexer.</summary>
    public static int Cell(StIndexedByPair grid) => grid[2, 3];

    /// <summary>Clause 16.4.13 — the renamed indexer and the property that took its old name.</summary>
    public static int RenamedBoth()
    {
        StRenamedIndexer renamed = new StRenamedIndexer(1);
        renamed[2] = 9;
        return renamed[0] + renamed.Item;
    }

    /// <summary>Clause 16.4.13 — an Index-keyed indexer reached with the `^` operator.</summary>
    public static int Last(StFrozenIndexed values) => values[^1];

    /// <summary>Clause 16.4.13 — a compound assignment, which calls get and then set.</summary>
    public static int Compound()
    {
        StIndexedByInt pair = new StIndexedByInt(1, 2);
        pair[1] += 3;
        return pair[1];
    }
}
