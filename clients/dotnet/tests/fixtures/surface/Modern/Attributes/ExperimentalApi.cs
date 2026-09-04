using System.Diagnostics.CodeAnalysis;

namespace Surface.Modern.Attributes;

/// <summary>
/// C# 12 — Experimental attribute, on a type. Every reference to this type is a diagnostic
/// with the identifier the attribute names, so a reference to it is only expressible from
/// inside a <c>#pragma warning disable</c> — which is what <see cref="ExperimentalUses"/>
/// does below.
/// </summary>
[Experimental("SURFACE001")]
public static class ExperimentalGauge
{
    /// <summary>The reading this gauge is experimenting with.</summary>
    public static double Reading => 1.5;
}

/// <summary>A stable type with one experimental member.</summary>
public static class MixedStability
{
    /// <summary>C# 12 — Experimental attribute, on a method rather than a type.</summary>
    [Experimental("SURFACE002", UrlFormat = "https://example.invalid/{0}")]
    public static double Provisional() => 2.5;

    /// <summary>Not experimental, in the same type.</summary>
    public static double Settled() => 3.5;
}

/// <summary>Reads both, with the diagnostics suppressed at the use site.</summary>
public static class ExperimentalUses
{
    /// <summary>Sums the experimental type's member and the experimental method.</summary>
    public static double Total()
    {
#pragma warning disable SURFACE001, SURFACE002
        return ExperimentalGauge.Reading + MixedStability.Provisional() + MixedStability.Settled();
#pragma warning restore SURFACE001, SURFACE002
    }
}
