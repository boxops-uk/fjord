namespace Surface.Modern.Patterns;

/// <summary>
/// C# 11 — Pattern match <c>Span&lt;char&gt;</c> or <c>ReadOnlySpan&lt;char&gt;</c> against a
/// constant string. The pattern's constant is a <c>string</c> and the input is a span, so the
/// match is neither a constant pattern in the standard's sense nor a conversion — the compiler
/// lowers it to a sequence comparison, and the string constant on the right is the only trace.
/// </summary>
public static class SpanConstantPattern
{
    /// <summary>C# 11 — <c>span is "123"</c> over a <c>ReadOnlySpan&lt;char&gt;</c>.</summary>
    public static bool IsOneTwoThree(ReadOnlySpan<char> text) => text is "123";

    /// <summary>C# 11 — the same over a writable <c>Span&lt;char&gt;</c>.</summary>
    public static bool IsWritableMatch(Span<char> text) => text is "abc";

    /// <summary>C# 11 — the span constant pattern as a <c>switch</c> arm, and with a constant.</summary>
    public static int Version(ReadOnlySpan<char> text) => text switch
    {
        "v1" => 1,
        "v2" => 2,
        Latest => 3,
        [] => 0,
        _ => -1,
    };

    /// <summary>The constant a <c>switch</c> arm above matches, to show a named constant works.</summary>
    private const string Latest = "v3";

    /// <summary>Calls the patterns, so each has a use.</summary>
    public static int All()
    {
        Span<char> writable = ['a', 'b', 'c'];

        return (IsOneTwoThree("123") ? 1 : 0)
            + (IsWritableMatch(writable) ? 2 : 0)
            + Version("v2");
    }
}
