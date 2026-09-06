namespace Surface.Modern.Patterns;

/// <summary>
/// C# 11 — List patterns and the slice pattern. A list pattern matches through <c>Length</c>
/// or <c>Count</c> and an indexer; a slice pattern matches through a <c>Slice</c> method or a
/// <c>Range</c> indexer. Every one of those members is found by shape, so the pattern is a
/// reference to members nothing in the pattern's syntax names.
/// </summary>
public static class ListPatternForms
{
    /// <summary>C# 11 — a fixed-length list pattern over an array.</summary>
    public static bool IsOneTwo(int[] values) => values is [1, 2];

    /// <summary>C# 11 — a list pattern with a trailing slice, which makes the length open.</summary>
    public static bool StartsOneTwo(int[] values) => values is [1, 2, ..];

    /// <summary>C# 11 — a leading slice, so the pattern is about the end.</summary>
    public static bool EndsWithFour(int[] values) => values is [.., 4];

    /// <summary>C# 11 — a slice pattern that binds the slice, through <c>Slice</c>.</summary>
    public static int MiddleLength(int[] values) =>
        values is [_, .. var middle, _] ? middle.Length : 0;

    /// <summary>C# 11 — the empty list pattern, which is <c>Length is 0</c>.</summary>
    public static bool IsEmpty(int[] values) => values is [];

    /// <summary>C# 11 — a list pattern whose elements are themselves patterns that declare.</summary>
    public static string Describe(int[] values) => values switch
    {
        [] => "empty",
        [var single] => $"one: {single}",
        [var first, var second] => $"two: {first},{second}",
        [> 0, .. { Length: > 2 }] => "positive, then several",
        [_, .. var rest] => $"tail of {rest.Length}",
    };

    /// <summary>C# 11 — a list pattern over a <c>List&lt;T&gt;</c>, which matches on <c>Count</c>.</summary>
    public static bool CountedTwo(List<string> values) => values is [_, _];

    /// <summary>C# 11 — a list pattern over a string, whose elements are characters.</summary>
    public static bool LooksVersioned(string text) => text is ['v', >= '0' and <= '9', ..];

    /// <summary>C# 11 — list patterns nested inside a list pattern.</summary>
    public static bool IsIdentity(int[][] rows) => rows is [[1, 0], [0, 1]];

    /// <summary>C# 11 — a list pattern in an <c>if</c> that declares two variables.</summary>
    public static int FirstPlusLast(int[] values)
    {
        if (values is [var head, .., var tail])
        {
            return head + tail;
        }

        return values is [var only] ? only : 0;
    }
}
