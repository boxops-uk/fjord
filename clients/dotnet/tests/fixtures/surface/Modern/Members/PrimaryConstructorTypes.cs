namespace Surface.Modern.Members;

/// <summary>
/// C# 12 — Primary constructors on any class or struct, not only records. The parameters are
/// in scope in the whole body, and each one is a *parameter* that behaves like a field: the
/// compiler captures it only where it is used, so <c>name</c> below is storage and
/// <c>scale</c> may not be. Nothing in source distinguishes the two.
/// </summary>
/// <param name="name">The gauge's name, read from an instance member.</param>
/// <param name="scale">The gauge's scale, read only by the initializer.</param>
public class Gauge(string name, double scale)
{
    /// <summary>A field initializer reading a primary constructor parameter.</summary>
    private readonly double _scale = scale;

    /// <summary>A property whose accessor reads a primary constructor parameter directly.</summary>
    public string Name => name;

    /// <summary>Applies the captured scale.</summary>
    public double Apply(double reading) => reading * _scale;

    /// <summary>A secondary constructor, which must chain to the primary one.</summary>
    public Gauge(string name)
        : this(name, 1.0)
    {
    }
}

/// <summary>
/// C# 12 — a primary constructor on a derived class, passing its parameters to
/// <c>base(...)</c> in the base list. This is the position that makes a primary constructor
/// unlike a field: the argument list sits in the type header.
/// </summary>
/// <param name="name">Passed through to the base.</param>
/// <param name="scale">Passed through to the base.</param>
/// <param name="unit">Kept by this type.</param>
public sealed class UnitGauge(string name, double scale, string unit) : Gauge(name, scale)
{
    /// <summary>The unit, captured from the primary constructor.</summary>
    public string Unit => unit;
}

/// <summary>C# 12 — a primary constructor on a <c>struct</c>.</summary>
/// <param name="numerator">The top.</param>
/// <param name="denominator">The bottom.</param>
public readonly struct Ratio(int numerator, int denominator)
{
    /// <summary>The top.</summary>
    public int Numerator { get; } = numerator;

    /// <summary>The bottom, defaulted when zero was passed.</summary>
    public int Denominator { get; } = denominator == 0 ? 1 : denominator;

    /// <summary>The quotient.</summary>
    public double Value => (double)Numerator / Denominator;
}

/// <summary>C# 12 — a primary constructor on a struct implementing an interface.</summary>
/// <param name="degrees">The angle.</param>
public readonly struct Angle(double degrees) : IComparable<Angle>
{
    /// <summary>The angle in degrees.</summary>
    public double Degrees => degrees;

    /// <inheritdoc/>
    public int CompareTo(Angle other) => degrees.CompareTo(other.Degrees);
}

/// <summary>Constructs each of the primary-constructor types.</summary>
public static class PrimaryConstructorUses
{
    /// <summary>Reads all four.</summary>
    public static string All()
    {
        var gauge = new Gauge("depth", 2.0);
        var chained = new Gauge("plain");
        var unit = new UnitGauge("temp", 1.5, "C");
        var ratio = new Ratio(3, 4);
        var angle = new Angle(90);

        return $"{gauge.Name}{gauge.Apply(1)}{chained.Name}{unit.Unit}{ratio.Value}{angle.Degrees}";
    }
}
