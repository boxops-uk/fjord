namespace Surface.Modern.Records;

/// <summary>
/// A plain struct — not a record — so that the <c>with</c> expression below is the C# 10
/// extension of <c>with</c> beyond record classes rather than a record's own member.
/// </summary>
public struct Extent
{
    /// <summary>The horizontal size.</summary>
    public int Width;

    /// <summary>The vertical size.</summary>
    public int Height;
}

/// <summary>C# 10 — with expression on structs and anonymous types.</summary>
public static class WithExpressions
{
    /// <summary>C# 10 — <c>with</c> applied to a non-record struct.</summary>
    public static Extent Widen(Extent extent, int extra) => extent with { Width = extent.Width + extra };

    /// <summary>C# 10 — <c>with</c> applied to an anonymous type.</summary>
    public static string Relabel()
    {
        var origin = new { Label = "origin", Depth = 0 };
        var deeper = origin with { Depth = 3 };

        return $"{deeper.Label}@{deeper.Depth}";
    }

    /// <summary>C# 10 — <c>with</c> applied to a record struct, for contrast with the two above.</summary>
    public static Sample Renumber(Sample sample, int index) => sample with { Index = index };
}
