namespace Surface.Modern.Directives;

/// <summary>
/// C# 10 — Enhanced <c>#line</c> pragma. The pre-C# 10 form remaps a whole line; the enhanced
/// form remaps a *span*, column to column, with an offset — which is what a source generator
/// needs to point a diagnostic at the exact characters of the original document.
/// </summary>
/// <remarks>
/// The declarations between the directives below are real declarations in this file, and the
/// compiler reports them as living somewhere else. That is the trap the row is here to expose:
/// an index that records a mapped span attributes them to a file that does not exist, and an
/// index that records the raw span disagrees with every diagnostic the compiler emits about
/// them. Both are defensible and they are not the same answer, so the corpus has to contain
/// the case rather than describe it.
/// </remarks>
public static class EnhancedLinePragma
{
    /// <summary>Declared before any remapping, as the control.</summary>
    public static int Unmapped() => 1;

#line (12, 5) - (12, 34) 6 "Generated/Modern.g.surface"

    /// <summary>Declared inside an enhanced <c>#line</c> span, so its reported position moves.</summary>
    public static int Mapped() => 2;

#line default

    /// <summary>Declared after <c>#line default</c>, which restores the real positions.</summary>
    public static int Restored() => 3;

#line 400 "Generated/Modern.legacy.surface"

    /// <summary>The pre-C# 10 whole-line form, for contrast with the span form above.</summary>
    public static int Legacy() => 4;

#line default

    /// <summary>The hidden form, which asks the debugger to step over the region.</summary>
    public static int Sum() => Unmapped() + Mapped() + Restored() + Legacy();
}
