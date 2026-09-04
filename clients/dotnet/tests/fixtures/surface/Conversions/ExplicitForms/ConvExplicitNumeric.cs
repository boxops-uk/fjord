using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.ExplicitForms;

/// <summary>How urgent something is. A second enum, so enum-to-enum has two ends.</summary>
public enum ConvPriority
{
    /// <summary>Can wait.</summary>
    Low = 0,

    /// <summary>Cannot.</summary>
    High = 1,

    /// <summary>Should already have happened.</summary>
    Critical = 2,
}

/// <summary>
/// Clauses 10.3.2, 10.3.3 and 10.3.4 — the explicit conversions between value types: the
/// numeric narrowings, the enum conversions in both directions and between enums, and the
/// nullable forms of all of them.
/// </summary>
public static class ConvExplicitNumeric
{
    /// <summary>Clause 10.3.2 — a narrowing integral conversion.</summary>
    public static byte LongToByte(long value) => (byte)value;

    /// <summary>Clause 10.3.2 — floating point to integral, which truncates.</summary>
    public static int DoubleToInt(double value) => (int)value;

    /// <summary>Clause 10.3.2 — integral to <c>char</c>, which is always explicit.</summary>
    public static char IntToChar(int value) => (char)value;

    /// <summary>Clause 10.3.2 — <c>char</c> to a narrower integral type.</summary>
    public static byte CharToByte(char value) => (byte)value;

    /// <summary>Clause 10.3.2 — floating point to <c>decimal</c>.</summary>
    public static decimal DoubleToDecimal(double value) => (decimal)value;

    /// <summary>Clause 10.3.2 — <c>decimal</c> to floating point, which is also explicit.</summary>
    public static float DecimalToFloat(decimal value) => (float)value;

    /// <summary>Clause 10.3.2 — signed to unsigned of the same width.</summary>
    public static uint IntToUInt(int value) => (uint)value;

    /// <summary>Clause 10.3.2 — the same conversion in a <c>checked</c> context, which throws.</summary>
    public static sbyte CheckedNarrow(int value) => checked((sbyte)value);

    /// <summary>Clause 10.3.2 — and in an <c>unchecked</c> context, which wraps.</summary>
    public static sbyte UncheckedNarrow(int value) => unchecked((sbyte)value);

    /// <summary>Clause 10.3.3 — an enum to its underlying type.</summary>
    public static int StrokeToInt(ConvStroke stroke) => (int)stroke;

    /// <summary>Clause 10.3.3 — an enum to a type its underlying type does not fit in.</summary>
    public static byte StrokeToByte(ConvStroke stroke) => (byte)stroke;

    /// <summary>Clause 10.3.3 — an integral type to an enum.</summary>
    public static ConvStroke IntToStroke(int value) => (ConvStroke)value;

    /// <summary>Clause 10.3.3 — floating point to an enum, through the underlying type.</summary>
    public static ConvStroke DoubleToStroke(double value) => (ConvStroke)value;

    /// <summary>Clause 10.3.3 — one enum type to another, which is two conversions in one cast.</summary>
    public static ConvPriority StrokeToPriority(ConvStroke stroke) => (ConvPriority)stroke;

    /// <summary>Clause 10.3.3 — <c>decimal</c> to an enum, the one that goes through a call.</summary>
    public static ConvPriority DecimalToPriority(decimal value) => (ConvPriority)value;

    /// <summary>Clause 10.3.4 — <c>S?</c> to <c>T?</c>.</summary>
    public static int? NullableLongToNullableInt(long? value) => (int?)value;

    /// <summary>Clause 10.3.4 — <c>S?</c> to <c>T</c>, which throws when there is no value.</summary>
    public static int NullableToInt(int? value) => (int)value;

    /// <summary>Clause 10.3.4 — <c>S</c> to <c>T?</c>, narrowing and lifting at once.</summary>
    public static int? LongToNullableInt(long value) => (int?)value;

    /// <summary>Clause 10.3.4 — the nullable form of an enum conversion.</summary>
    public static ConvStroke? NullableIntToNullableStroke(int? value) => (ConvStroke?)value;

    /// <summary>Clause 10.3.4 — enum to underlying, lifted.</summary>
    public static int? NullableStrokeToNullableInt(ConvStroke? stroke) => (int?)stroke;
}
