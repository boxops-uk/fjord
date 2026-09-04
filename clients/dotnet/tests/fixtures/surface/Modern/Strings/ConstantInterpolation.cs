namespace Surface.Modern.Strings;

/// <summary>C# 10 — Constant interpolated strings.</summary>
public static class ConstantInterpolation
{
    /// <summary>An ordinary string constant, which the interpolations below consume.</summary>
    public const string Product = "surface";

    /// <summary>Another one, so a hole can hold more than one constant.</summary>
    public const string Area = "modern";

    /// <summary>
    /// C# 10 — Constant interpolated strings: a <c>const string</c> whose initializer is an
    /// interpolation. Every hole has to be a constant string itself, and the result is a
    /// compile-time constant — so this is a *constant* whose value an index can only know by
    /// folding the interpolation.
    /// </summary>
    public const string Label = $"{Product}/{Area}";

    /// <summary>A constant interpolation over a constant interpolation.</summary>
    public const string Qualified = $"{Label}:v1";

    /// <summary>The same text, spelled by concatenation, as the thing to compare against.</summary>
    public const string Concatenated = Product + "/" + Area;

    /// <summary>A constant interpolation used where a constant is required — an attribute argument.</summary>
    [Obsolete($"replaced in {Label}")]
    public static string Retired() => Qualified;
}
