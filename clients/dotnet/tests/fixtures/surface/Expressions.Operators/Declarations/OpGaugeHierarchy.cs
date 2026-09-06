namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A base gauge that declares <c>operator +</c> and nothing else, so that a use on a
/// derived operand has to walk the base chain to find its candidate set.
/// </summary>
/// <remarks>
/// Clause 12.4.6 builds the candidate set per operator: for a class type it takes the
/// operators of that type and, only if the set is empty, repeats with the base class.
/// <c>derived + derived</c> therefore references a member declared here and returns a
/// <see cref="OpBaseGauge"/>, while <c>derived - derived</c> stops in the derived type.
/// Neither use writes the name of the type that declared the operator it reaches.
/// </remarks>
public class OpBaseGauge
{
    /// <summary>Constructs a gauge at a reading.</summary>
    public OpBaseGauge(int reading) => Reading = reading;

    /// <summary>The gauge's reading.</summary>
    public int Reading { get; }

    // 12.4.6 — declared here, reached from a derived operand.
    public static OpBaseGauge operator +(OpBaseGauge left, OpBaseGauge right) =>
        new(left.Reading + right.Reading);

    /// <inheritdoc/>
    public override string ToString() => $"gauge {Reading}";
}

/// <summary>
/// A derived gauge that declares only <c>operator -</c>. The candidate set for <c>-</c>
/// is non-empty here and so the walk stops; the set for <c>+</c> is empty and the walk
/// continues into <see cref="OpBaseGauge"/>.
/// </summary>
public sealed class OpDerivedGauge : OpBaseGauge
{
    /// <summary>Constructs a derived gauge at a reading.</summary>
    public OpDerivedGauge(int reading)
        : base(reading)
    {
    }

    // 12.4.6 — the derived type's own candidate, at a signature the base has no analogue of.
    public static OpDerivedGauge operator -(OpDerivedGauge left, OpDerivedGauge right) =>
        new(left.Reading - right.Reading);

    // 12.4.4 — a unary operator that shadows nothing, so unary resolution has exactly one
    // candidate to find in this type.
    public static OpDerivedGauge operator -(OpDerivedGauge value) => new(-value.Reading);
}
