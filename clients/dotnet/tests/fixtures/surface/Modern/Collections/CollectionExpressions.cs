using System.Runtime.CompilerServices;

namespace Surface.Modern.Collections;

/// <summary>
/// C# 12 — Collection expressions and the spread element. A collection expression has no type
/// of its own: <c>[1, 2, 3]</c> means a different construction for every target type, and for
/// a type marked <see cref="CollectionBuilderAttribute"/> it means a call to a method named
/// only in an attribute argument. So the interesting fact is what each <c>[…]</c> below
/// *resolved to*, none of which is written at the site.
/// </summary>
public static class CollectionExpressions
{
    /// <summary>C# 12 — a collection expression targeting an array.</summary>
    public static int[] Array() => [1, 2, 3];

    /// <summary>C# 12 — the same syntax targeting a <see cref="List{T}"/>.</summary>
    public static List<int> Listed() => [1, 2, 3];

    /// <summary>C# 12 — targeting a span, which is stack-allocated rather than built.</summary>
    public static int SpanTotal()
    {
        ReadOnlySpan<int> values = [4, 5, 6];

        return values[0] + values[^1];
    }

    /// <summary>C# 12 — targeting an interface, where the compiler chooses the implementation.</summary>
    public static IReadOnlyList<string> Interfaced() => ["a", "b"];

    /// <summary>C# 12 — the empty collection expression, whose element type comes from the target.</summary>
    public static IEnumerable<double> Empty() => [];

    /// <summary>
    /// C# 12 — the spread element <c>..e</c>. It is not a call to <c>AddRange</c>: the
    /// compiler enumerates the operand inline, so the only member reference it leaves is the
    /// enumerator's.
    /// </summary>
    public static int[] Spread(int[] head, IEnumerable<int> tail) => [0, .. head, .. tail, 9];

    /// <summary>C# 12 — a spread of a span into a list, mixing collection kinds.</summary>
    public static List<int> SpreadSpan(ReadOnlySpan<int> values) => [.. values];

    /// <summary>C# 12 — a nested collection expression, whose target is the element type.</summary>
    public static int[][] Nested() => [[1, 0], [0, 1]];

    /// <summary>C# 12 — a collection expression targeting a <see cref="CollectionBuilderAttribute"/> type.</summary>
    public static Trail Built() => [1.5, 2.5];

    /// <summary>Calls every one of them.</summary>
    public static int All()
    {
        var spread = Spread(Array(), Listed());
        var built = Built();

        return spread.Length + SpanTotal() + Interfaced().Count + SpreadSpan([1, 2]).Count
            + Nested().Length + built.Length + (Empty() is null ? 1 : 0);
    }
}

/// <summary>
/// C# 12 — a collection type built by a factory the compiler finds through an attribute. The
/// <c>Create</c> method is named by a string, so the reference from
/// <see cref="CollectionExpressions.Built"/> to <see cref="TrailBuilder.Create"/> passes
/// through two indirections nothing in the expression mentions.
/// </summary>
[CollectionBuilder(typeof(TrailBuilder), nameof(TrailBuilder.Create))]
public readonly struct Trail
{
    private readonly double[] _points;

    /// <summary>Wraps the points a builder collected.</summary>
    public Trail(double[] points) => _points = points;

    /// <summary>How many points the trail has.</summary>
    public int Length => _points?.Length ?? 0;

    /// <summary>Enumerates the points, which is what makes the type a collection.</summary>
    public IEnumerator<double> GetEnumerator()
    {
        foreach (var point in _points ?? [])
        {
            yield return point;
        }
    }
}

/// <summary>The factory <see cref="Trail"/>'s attribute names.</summary>
public static class TrailBuilder
{
    /// <summary>Builds a trail from the span the compiler fills in.</summary>
    public static Trail Create(ReadOnlySpan<double> points) => new(points.ToArray());
}
