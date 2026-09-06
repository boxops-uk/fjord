namespace Surface.Modern.Structs;

/// <summary>
/// C# 10 — Improvements of structure types: a struct may declare an explicit parameterless
/// instance constructor, and may initialize its fields inline. Both were errors before C# 10,
/// and the pair interacts — the field initializers run only through a constructor, so
/// <c>default(Calibration)</c> and <c>new Calibration()</c> now differ in value.
/// </summary>
public struct Calibration
{
    /// <summary>C# 10 — a field initializer in a struct.</summary>
    public double Scale = 1.0;

    /// <summary>Another, so the constructor is not the only thing assigning.</summary>
    public double Offset = 0.5;

    /// <summary>C# 10 — an explicit parameterless instance constructor in a struct.</summary>
    public Calibration()
    {
        Scale = 2.0;
    }

    /// <summary>A second constructor, which chains to the parameterless one.</summary>
    public Calibration(double offset)
        : this() => Offset = offset;

    /// <summary>Applies the calibration.</summary>
    public double Apply(double reading) => (reading * Scale) + Offset;
}

/// <summary>C# 10 — the same, on a <c>readonly struct</c> with property initializers.</summary>
public readonly struct Tolerance
{
    /// <summary>C# 10 — a property initializer in a struct, run by the constructor below.</summary>
    public double Band { get; init; } = 0.25;

    /// <summary>C# 10 — an explicit parameterless constructor on a readonly struct.</summary>
    public Tolerance() => Band = 0.5;
}

/// <summary>Shows the two values a struct with an explicit parameterless constructor has.</summary>
public static class ParameterlessConstructors
{
    /// <summary>
    /// <c>default</c> skips the constructor and the field initializers; <c>new</c> runs both.
    /// The two calls below produce different numbers from the same type.
    /// </summary>
    public static bool DefaultDiffersFromNew()
    {
        var zeroed = default(Calibration);
        var constructed = new Calibration();

        return zeroed.Scale != constructed.Scale;
    }

    /// <summary>Reads the readonly struct's initialized property.</summary>
    public static double Band() => new Tolerance().Band;
}
