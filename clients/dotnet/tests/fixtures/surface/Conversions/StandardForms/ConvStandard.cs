using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.StandardForms;

/// <summary>
/// A distance in metres.
///
/// Clause 10.4 defines the <em>standard</em> conversions: the subset of the implicit and
/// explicit conversions that may be composed with a user-defined operator, in or out. This
/// type's two operators exist so that the composition has something to compose with, and
/// <see cref="ConvStandard"/> is where each composition is written out.
/// </summary>
public readonly struct ConvMetres
{
    /// <summary>A distance of <paramref name="value"/> metres.</summary>
    public ConvMetres(double value) => Value = value;

    /// <summary>How many metres.</summary>
    public double Value { get; }

    /// <summary>The inward operator, from the type every standard numeric widening reaches.</summary>
    public static implicit operator ConvMetres(double value) => new(value);

    /// <summary>The outward one, which the standard explicit conversions continue from.</summary>
    public static explicit operator double(ConvMetres metres) => metres.Value;
}

/// <summary>
/// Clauses 10.4.2 and 10.4.3 — the standard conversions, each shown in the position that
/// makes it standard: before or after a user-defined operator. A conversion that is implicit
/// but not standard — a user-defined one, or the boxing of a type parameter — cannot appear
/// in these positions, which is what the clause is for.
/// </summary>
public static class ConvStandard
{
    /// <summary>Clause 10.4.2 — an implicit numeric conversion in: <c>int</c> to <c>double</c>.</summary>
    public static ConvMetres FromInt(int value) => value;

    /// <summary>Clause 10.4.2 — a narrower one still: <c>byte</c> to <c>double</c>.</summary>
    public static ConvMetres FromByte(byte value) => value;

    /// <summary>Clause 10.4.2 — an implicit constant expression conversion in.</summary>
    public static ConvMetres FromConstant() => 3;

    /// <summary>Clause 10.4.2 — a boxing conversion out, which is standard.</summary>
    public static object ToObject(ConvMetres metres) => metres;

    /// <summary>Clause 10.4.2 — an implicit nullable conversion out.</summary>
    public static ConvMetres? ToNullable(ConvMetres metres) => metres;

    /// <summary>Clause 10.4.2 — an implicit reference conversion out of an unrelated value.</summary>
    public static ConvNode ToNode(ConvLeaf leaf) => leaf;

    /// <summary>Clause 10.4.3 — a standard explicit conversion after the operator.</summary>
    public static int ToInt(ConvMetres metres) => (int)metres;

    /// <summary>Clause 10.4.3 — a longer chain out: operator, then <c>double</c> to <c>float</c>.</summary>
    public static float ToFloat(ConvMetres metres) => (float)metres;

    /// <summary>Clause 10.4.3 — and to <c>long</c>, whose opposite direction is implicit.</summary>
    public static long ToLong(ConvMetres metres) => (long)metres;

    /// <summary>
    /// Clause 10.4.3 — a standard explicit conversion in, from <c>double?</c> to <c>double</c>,
    /// which is standard because <c>double</c> to <c>double?</c> is standard implicit.
    /// </summary>
    public static ConvMetres FromNullableDouble(double? value) => (ConvMetres)value;

    /// <summary>
    /// Clause 10.4.3 — an unboxing conversion, written as two casts. The single cast
    /// <c>(ConvMetres)boxed</c> would be an unboxing of <see cref="ConvMetres"/> itself, not a
    /// standard conversion feeding the operator, so the intermediate type is spelled out.
    /// </summary>
    public static ConvMetres FromBoxedDouble(object boxed) => (double)boxed;

    // Clause 10.4.3, and the point of the clause: `decimal` is *not* reachable through this
    // type's operators in one cast, in either direction. `double` to `decimal` and `decimal`
    // to `double` are both explicit numeric conversions, so neither has a standard implicit
    // conversion running the other way, so neither is a *standard* explicit conversion, so
    // neither may be composed with a user-defined one. `(decimal)metres` and
    // `(ConvMetres)someDecimal` are both CS0030, and the two casts below are what it takes.

    /// <summary>Clause 10.4.3 — <c>decimal</c> in, spelled as the two conversions it really is.</summary>
    public static ConvMetres FromDecimal(decimal value) => (double)value;

    /// <summary>Clause 10.4.3 — and out again, likewise.</summary>
    public static decimal ToDecimal(ConvMetres metres) => (decimal)(double)metres;
}
