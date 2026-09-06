namespace Surface.Modern.Members;

/// <summary>
/// C# 12 — <c>ref readonly</c> parameters. The language already had <c>in</c>, which is also
/// a read-only reference; <c>ref readonly</c> differs in what the *caller* must write — an
/// argument needs <c>ref</c> or <c>in</c> at the call site and may not be a value. So the
/// four parameter modifiers below are four distinct signatures with one runtime shape.
/// </summary>
public static class RefReadonlyParameters
{
    /// <summary>C# 12 — a <c>ref readonly</c> parameter.</summary>
    public static double Magnitude(ref readonly Ratio ratio) => Abs(ratio.Value);

    /// <summary>The <c>in</c> spelling of the same reference kind, for contrast.</summary>
    public static double MagnitudeIn(in Ratio ratio) => Abs(ratio.Value);

    /// <summary>The writable reference, which the other two forbid.</summary>
    public static void Negate(ref Ratio ratio) => ratio = new Ratio(-ratio.Numerator, ratio.Denominator);

    /// <summary>C# 12 — <c>ref readonly</c> beside <c>scoped</c>, which composes with it.</summary>
    public static double Scoped(scoped ref readonly Ratio ratio) => ratio.Value;

    /// <summary>
    /// The call sites: <c>ref</c> and <c>in</c> both satisfy a <c>ref readonly</c> parameter,
    /// and a value satisfies only <c>in</c>.
    /// </summary>
    public static double All()
    {
        var ratio = new Ratio(5, 8);

        return Magnitude(ref ratio)
            + Magnitude(in ratio)
            + MagnitudeIn(ratio)
            + Scoped(in ratio);
    }
}
