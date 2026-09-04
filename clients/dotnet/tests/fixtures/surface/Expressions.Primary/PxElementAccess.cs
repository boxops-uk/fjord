using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.12.4 — an indexer with two parameters. An element access on it writes no name at all,
/// so the only thing tying <c>grid[1, 2]</c> to this declaration is the argument list.
/// </summary>
public sealed class PxGrid
{
    private readonly int[,] _cells = new int[3, 3];

    /// <summary>The one indexer of this type, taking a row and a column.</summary>
    public int this[int row, int column]
    {
        get => _cells[row, column];
        set => _cells[row, column] = value;
    }

    /// <summary>Same-file uses: the setter and then the getter, both without a name node.</summary>
    public int UsedHere()
    {
        this[1, 2] = 5;
        return this[1, 2];
    }
}

/// <summary>
/// 12.8.12.4 — an indexer whose metadata name is *not* <c>Item</c>, declared beside a property
/// that is called <c>Item</c>. Only one of the two is reachable by an element access, and only
/// the other is reachable by name.
/// </summary>
public sealed class PxRenamedIndexer
{
    private readonly string[] _elements = ["a", "b", "c"];

    /// <summary>A property genuinely named <c>Item</c>, which an element access can never reach.</summary>
    public string Item => "the property";

    /// <summary>The one indexer, renamed, so the property above does not collide with it.</summary>
    [IndexerName("Element")]
    public string this[int index] => _elements[index];

    /// <summary>Same-file uses: the element access reaches the indexer, the name reaches the property.</summary>
    public string UsedHere() => this[0] + Item;
}

/// <summary>12.8.12.4 — an indexer declared on an interface, so a mapping has to carry it.</summary>
public interface IPxIndexed
{
    /// <summary>The interface's one indexer.</summary>
    int this[int index] { get; }

    /// <summary>How many elements there are.</summary>
    int Length { get; }
}

/// <summary>
/// 12.8.12.4 — the implementing side, whose one indexer is the interface's mapping target. An
/// element access through the interface and through the class reach the same declaration by
/// two different routes.
/// </summary>
public sealed class PxIndexedList : IPxIndexed
{
    private readonly List<int> _values = [7, 8, 9];

    /// <inheritdoc />
    public int this[int index] => _values[index];

    /// <inheritdoc />
    public int Length => _values.Count;

    /// <summary>Same-file uses: directly, and through the interface.</summary>
    public int UsedHere()
    {
        IPxIndexed asInterface = this;
        return this[0] + asInterface[1];
    }
}

/// <summary>
/// 12.8.12 with the post-standard index and range support: a type with <c>Length</c>, an
/// <c>int</c> indexer and a <c>Slice</c> method is indexable by <c>^</c> and sliceable by
/// <c>..</c> — and neither <c>Slice</c> nor the <c>Index</c> arithmetic is written at the use site.
/// </summary>
public sealed class PxSliceable
{
    private readonly int[] _values = [1, 2, 3, 4, 5];

    /// <summary>Read by the compiler to turn <c>^1</c> into an <c>int</c>.</summary>
    public int Length => _values.Length;

    /// <summary>The one indexer; a from-end index lands here too.</summary>
    public int this[int index] => _values[index];

    /// <summary>The method a range element access binds to, spelled at no call site.</summary>
    public PxSliceable Slice(int start, int length)
    {
        var taken = new int[length];
        Array.Copy(_values, start, taken, 0, length);
        return new PxSliceable(taken);
    }

    /// <summary>Builds one over the given values.</summary>
    public PxSliceable(int[] values) => _values = values;

    /// <summary>Builds the default five.</summary>
    public PxSliceable()
    {
    }

    /// <summary>Same-file uses: an ordinary index, a from-end index, and a range.</summary>
    public int UsedHere()
    {
        var first = this[0];
        var last = this[^1];
        var middle = this[1..3];
        return first + last + middle.Length;
    }
}

/// <summary>
/// 12.8.12 — element access in every flavour the clause distinguishes: an array (12.8.12.2),
/// a string (12.8.12.3), and an indexer (12.8.12.4), including indexers on constructed
/// generic types where the member the access reaches is a substituted one.
/// </summary>
public static class PxElementAccess
{
    /// <summary>
    /// 12.8.12.2 — array access, which binds to no member at all: one dimension, several
    /// dimensions, jagged, and with the post-standard index/range forms.
    /// </summary>
    public static int ArrayAccess()
    {
        var single = new[] { 1, 2, 3 };
        var rectangular = new int[2, 3];
        var jagged = new int[2][];
        jagged[0] = [4, 5];
        jagged[1] = [6];
        rectangular[1, 2] = 7;

        var fromSingle = single[0];
        var fromRectangular = rectangular[1, 2];
        var fromJagged = jagged[0][1];
        var fromEnd = single[^1];
        var sliced = single[1..].Length;

        return fromSingle + fromRectangular + fromJagged + fromEnd + sliced;
    }

    /// <summary>
    /// 12.8.12.3 — string access, which does bind to a member: <c>string</c>'s own indexer,
    /// declared in the framework and named at no use site.
    /// </summary>
    public static int StringAccess()
    {
        const string text = "primary";
        var first = text[0];
        var last = text[^1];
        var slice = text[1..3].Length;
        return first + last + slice;
    }

    /// <summary>
    /// 12.8.12.4 — indexer access across the shapes: cross-file, two-argument, renamed,
    /// through an interface, and on constructed generic framework types.
    /// </summary>
    public static string IndexerAccess()
    {
        // Cross-file: the indexer is declared in PxDeclarations.cs.
        var target = new PxTarget(1);
        var fromTarget = target[2];

        // Two arguments, and a setter reached by an element access on the left of an assignment.
        var grid = new PxGrid();
        grid[0, 0] = 3;
        var fromGrid = grid[0, 0];

        // A renamed indexer, and the property that shares its default name.
        var renamed = new PxRenamedIndexer();
        var fromRenamed = renamed[1] + renamed.Item;

        // Through an interface, so the access goes via the mapping.
        IPxIndexed indexed = new PxIndexedList();
        var fromInterface = indexed[2];

        // On constructed generic types: the substituted indexer of Dictionary and of List.
        var lookup = new Dictionary<string, int> { ["a"] = 1 };
        lookup["b"] = 2;
        var fromDictionary = lookup["a"] + lookup["b"];
        var fromList = new List<PxPoint> { new(1, 1) }[0].Measure();

        // On a base type's indexer, through a derived instance.
        var fromDerived = new PxDerivedCounter()[1];

        return $"{fromTarget} {fromGrid} {fromRenamed} {fromInterface} {fromDictionary} {fromList} {fromDerived}";
    }

    /// <summary>
    /// 12.8.12.1 — the dispatch itself: three element accesses written identically, reaching an
    /// array, a string and an indexer respectively.
    /// </summary>
    public static string SameSyntaxThreeTargets()
    {
        var array = new[] { 10, 20 };
        const string text = "xy";
        var indexed = new PxIndexedList();

        var a = array[1];
        var b = text[1];
        var c = indexed[1];

        return $"{a} {b} {c}";
    }
}
