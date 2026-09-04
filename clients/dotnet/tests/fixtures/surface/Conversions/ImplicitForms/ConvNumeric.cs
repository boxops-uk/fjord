namespace Surface.Conversions.ImplicitForms;

/// <summary>How a line is drawn. The enum clause 10.2.4 converts constant zero into.</summary>
public enum ConvStroke
{
    /// <summary>No stroke — the zero member, which is what a constant zero lands on.</summary>
    None = 0,

    /// <summary>A hairline.</summary>
    Thin = 1,

    /// <summary>A heavy line.</summary>
    Thick = 2,
}

/// <summary>
/// Clauses 10.2.3, 10.2.4 and 10.2.11 — the implicit conversions that need no cast and no
/// declaration: widening numerics, constant zero to an enum, and a constant whose value the
/// compiler can see fits the narrower type.
/// </summary>
public static class ConvNumeric
{
    /// <summary>Clause 10.2.3 — <c>byte</c> to <c>long</c>.</summary>
    public static long ByteToLong(byte value) => value;

    /// <summary>Clause 10.2.3 — <c>int</c> to <c>double</c>.</summary>
    public static double IntToDouble(int value) => value;

    /// <summary>Clause 10.2.3 — <c>long</c> to <c>decimal</c>, the one with no precision loss.</summary>
    public static decimal LongToDecimal(long value) => value;

    /// <summary>Clause 10.2.3 — <c>char</c> to <c>float</c>, via the integral chain.</summary>
    public static float CharToFloat(char value) => value;

    /// <summary>Clause 10.2.3 — <c>uint</c> to <c>ulong</c>.</summary>
    public static ulong UIntToULong(uint value) => value;

    /// <summary>Clause 10.2.3 — <c>sbyte</c> to <c>short</c>, both signed.</summary>
    public static short SByteToShort(sbyte value) => value;

    /// <summary>Clause 10.2.3 — <c>float</c> to <c>double</c>.</summary>
    public static double FloatToDouble(float value) => value;

    /// <summary>Clause 10.2.4 — a constant zero is an <see cref="ConvStroke"/>.</summary>
    public const ConvStroke ZeroStroke = 0;

    /// <summary>Clause 10.2.4 — constant zero converted to an enum type at a local.</summary>
    public static ConvStroke NoneFromZero()
    {
        ConvStroke stroke = 0;
        return stroke;
    }

    /// <summary>Clause 10.2.4 — and to a nullable enum type, which the clause also permits.</summary>
    public static ConvStroke? NullableNoneFromZero()
    {
        ConvStroke? stroke = 0;
        return stroke;
    }

    /// <summary>A constant whose value clause 10.2.11 lets the compiler narrow.</summary>
    public const int SmallConstant = 42;

    /// <summary>Clause 10.2.11 — a named <c>int</c> constant in <c>byte</c> range.</summary>
    public static byte NarrowedByte() => SmallConstant;

    /// <summary>Clause 10.2.11 — a negative literal in <c>sbyte</c> range.</summary>
    public static sbyte NarrowedSByte() => -7;

    /// <summary>Clause 10.2.11 — a literal in <c>ushort</c> range.</summary>
    public static ushort NarrowedUShort() => 1000;

    /// <summary>Clause 10.2.11 — constant zero to <c>ulong</c>, which no numeric rule allows.</summary>
    public static ulong ZeroAsULong() => 0;

    /// <summary>
    /// Clause 10.2.11 — a <c>long</c> constant to <c>ulong</c>, the only narrowing the clause
    /// permits from <c>long</c>, and only because the value is not negative.
    /// </summary>
    public static ulong NarrowedFromLong() => 9L;
}
