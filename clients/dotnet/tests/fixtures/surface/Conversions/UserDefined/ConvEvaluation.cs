using Surface.Conversions.ExplicitForms;

namespace Surface.Conversions.UserDefined;

/// <summary>
/// Clause 10.5.3 — the evaluation of a user-defined conversion, which is three steps: a
/// standard conversion in, the operator, then a standard conversion out. Nothing here
/// declares anything; every method is one call site whose whole content is a reference to
/// exactly one of the operators declared elsewhere in this project, and which one it names
/// is the question the clause exists to answer.
/// </summary>
public static class ConvEvaluation
{
    /// <summary>Clause 10.5.3 — names <c>ConvReading.op_Implicit</c> returning <see cref="ConvCelsius"/>.</summary>
    public static ConvCelsius AsCelsius(ConvReading reading) => reading;

    /// <summary>Clause 10.5.3 — the same syntax, naming the <see cref="ConvFahrenheit"/> operator.</summary>
    public static ConvFahrenheit AsFahrenheit(ConvReading reading) => reading;

    /// <summary>Clause 10.5.3 — names <c>ConvReading.op_Explicit</c> returning <see cref="ConvKelvin"/>.</summary>
    public static ConvKelvin AsKelvin(ConvReading reading) => (ConvKelvin)reading;

    /// <summary>Clause 10.5.3 — the same cast syntax, naming the <see cref="ConvRankine"/> operator.</summary>
    public static ConvRankine AsRankine(ConvReading reading) => (ConvRankine)reading;

    /// <summary>Clause 10.5.3 — the tuple-returning operator, which shares the explicit name.</summary>
    public static (double Celsius, double Fahrenheit) AsPair(ConvReading reading) =>
        ((double, double))reading;

    /// <summary>Clause 10.5.3 — the operator that converts into the enclosing type.</summary>
    public static ConvReading FromCelsius(ConvCelsius celsius) => celsius;

    /// <summary>Clause 10.5.3 — a standard implicit conversion in: <c>int</c> to <c>long</c> first.</summary>
    public static ConvLength FromInt(int millimetres) => millimetres;

    /// <summary>Clause 10.5.3 — a standard implicit conversion in from a narrower type still.</summary>
    public static ConvLength FromShort(short millimetres) => millimetres;

    /// <summary>Clause 10.5.3 — no conversion either side, the plain case.</summary>
    public static long AsLong(ConvLength length) => length;

    /// <summary>Clause 10.5.3 — a standard explicit conversion out: <c>int</c> then <c>short</c>.</summary>
    public static short AsShort(ConvLength length) => (short)(int)length;

    /// <summary>
    /// Clause 10.5.3, hazard — in an unchecked context this names
    /// <c>ConvLength.op_Explicit</c>.
    /// </summary>
    public static int AsIntUnchecked(ConvLength length) => unchecked((int)length);

    /// <summary>
    /// Clause 10.5.3, hazard — the identical cast in a checked context names
    /// <c>ConvLength.op_CheckedExplicit</c> instead. Same source, same target, different
    /// declaration.
    /// </summary>
    public static int AsIntChecked(ConvLength length) => checked((int)length);

    /// <summary>Clause 10.5.3 — the generic wrapper's inward operator, at <c>string</c>.</summary>
    public static ConvBoxed<string> Wrap(string value) => value;

    /// <summary>Clause 10.5.3 — and its outward one, at the same instantiation.</summary>
    public static string Unwrap(ConvBoxed<string> boxed) => (string)boxed;

    /// <summary>Clause 10.5.3 — the same two operators at a value-type instantiation.</summary>
    public static int Unwrap(ConvBoxed<int> boxed) => (int)boxed;

    /// <summary>Clause 10.5.3 — the explicit operators on <see cref="ConvSpan"/>, first target.</summary>
    public static ConvSpanStart StartOf(ConvSpan span) => (ConvSpanStart)span;

    /// <summary>Clause 10.5.3 — and the second, which the cast syntax cannot distinguish alone.</summary>
    public static ConvSpanEnd EndOf(ConvSpan span) => (ConvSpanEnd)span;
}
