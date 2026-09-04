namespace Surface.Modern.Collections;

/// <summary>
/// C# 13 — Better conversion from collection expression element, and C# 13 — Implicit indexer
/// access in object initializers. Both are overload- and target-resolution changes: the source
/// text of a call is legal in C# 12 and means something different, or nothing at all, there.
/// </summary>
public static class ElementConversions
{
    /// <summary>The overload C# 13 prefers for <c>[1, 2, 3]</c>, on the element type.</summary>
    public static string Render(ReadOnlySpan<int> values) => $"int:{values.Length}";

    /// <summary>The overload that wins for a collection of longs.</summary>
    public static string Render(ReadOnlySpan<long> values) => $"long:{values.Length}";

    /// <summary>
    /// C# 13 — the call whose resolution the feature changes. Both overloads are applicable
    /// to <c>[1, 2, 3]</c>; C# 13 compares the *element* conversions and picks <c>int</c>.
    /// </summary>
    public static string Preferred() => Render([1, 2, 3]);

    /// <summary>The same call with an explicit element type, which needs no new rule.</summary>
    public static string Explicit() => Render([1L, 2L, 3L]);
}

/// <summary>A fixed-length track with a length and an indexer, which is what an implicit index needs.</summary>
public sealed class Track
{
    private readonly int[] _slots = new int[4];

    /// <summary>How many slots the track has — the member an implicit index is measured from.</summary>
    public int Length => _slots.Length;

    /// <summary>The only indexer on this type.</summary>
    /// <param name="index">Which slot.</param>
    public int this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value;
    }
}

/// <summary>A type whose member is initialized through the nested-initializer form.</summary>
public sealed class Race
{
    /// <summary>The lanes, which the object initializer below writes into without creating.</summary>
    public Track Lanes { get; } = new();
}

/// <summary>C# 13 — Implicit indexer access in object initializers.</summary>
public static class ImplicitIndexerInitializers
{
    /// <summary>
    /// C# 13 — <c>[^1]</c> inside an object initializer. Before C# 13 a from-the-end index
    /// was not allowed in this position, because the initializer's receiver was evaluated
    /// once per element and <c>Length</c> could not be read. The index below is a read of
    /// <see cref="Track.Length"/> that appears nowhere in the source.
    /// </summary>
    public static Race Seeded() => new()
    {
        Lanes =
        {
            [0] = 1,
            [^1] = 4,
            [^2] = 3,
        },
    };

    /// <summary>Reads the seeded lanes back.</summary>
    public static int Total()
    {
        var race = Seeded();

        return race.Lanes[0] + race.Lanes[^1] + race.Lanes[^2];
    }
}
