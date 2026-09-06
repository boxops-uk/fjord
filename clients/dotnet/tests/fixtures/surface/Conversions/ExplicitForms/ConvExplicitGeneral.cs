using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.ExplicitForms;

/// <summary>The low end of a span. One of two targets <see cref="ConvSpan"/> converts to.</summary>
public readonly struct ConvSpanStart
{
    /// <summary>A start at <paramref name="offset"/>.</summary>
    public ConvSpanStart(int offset) => Offset = offset;

    /// <summary>Where it starts.</summary>
    public int Offset { get; }
}

/// <summary>The high end of a span, and the other target.</summary>
public readonly struct ConvSpanEnd
{
    /// <summary>An end at <paramref name="offset"/>.</summary>
    public ConvSpanEnd(int offset) => Offset = offset;

    /// <summary>Where it ends.</summary>
    public int Offset { get; }
}

/// <summary>
/// A half-open range of offsets.
///
/// Clause 10.3.1, deliberate hazard: the two <c>explicit</c> operators below take the same
/// single parameter of the enclosing type and differ only in what they return. They are the
/// explicit counterpart of the pair in <see cref="Surface.Conversions.UserDefined.ConvReading"/>,
/// and they exist separately so that the general clause owns a shape of its own.
/// </summary>
public readonly struct ConvSpan
{
    /// <summary>A span from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public ConvSpan(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>The first offset in the span.</summary>
    public int Start { get; }

    /// <summary>The first offset after the span.</summary>
    public int End { get; }

    /// <summary>Clause 10.3.1 — one of the two same-parameter operators.</summary>
    public static explicit operator ConvSpanStart(ConvSpan span) => new(span.Start);

    /// <summary>Clause 10.3.1 — the other, distinguishable only by its return type.</summary>
    public static explicit operator ConvSpanEnd(ConvSpan span) => new(span.End);
}

/// <summary>
/// Clause 10.3.1 — the set of explicit conversions, which is every implicit conversion plus
/// the ones that can lose information or fail. Each method here is a cast expression of one
/// listed kind, so that the clause's list has one call site per entry.
/// </summary>
public static class ConvExplicitGeneral
{
    /// <summary>Clause 10.3.1 — an explicit cast that is really an implicit conversion.</summary>
    public static long RedundantCast(int value) => (long)value;

    /// <summary>Clause 10.3.1 — an explicit numeric conversion (see 10.3.2).</summary>
    public static byte Numeric(long value) => (byte)value;

    /// <summary>Clause 10.3.1 — an explicit enumeration conversion (see 10.3.3).</summary>
    public static ConvStroke Enumeration(int value) => (ConvStroke)value;

    /// <summary>Clause 10.3.1 — an explicit nullable conversion (see 10.3.4).</summary>
    public static int? Nullable(long? value) => (int?)value;

    /// <summary>Clause 10.3.1 — an explicit reference conversion (see 10.3.5).</summary>
    public static ConvLeaf Reference(object value) => (ConvLeaf)value;

    /// <summary>Clause 10.3.1 — an unboxing conversion (see 10.3.7).</summary>
    public static int Unboxing(object value) => (int)value;

    /// <summary>Clause 10.3.1 — an explicit tuple conversion (see 10.3.6).</summary>
    public static (int, int) Tuple((long, long) pair) => ((int, int))pair;

    /// <summary>Clause 10.3.1 — a user-defined explicit conversion, to the first target.</summary>
    public static ConvSpanStart ToStart(ConvSpan span) => (ConvSpanStart)span;

    /// <summary>Clause 10.3.1 — the same syntax, resolving to the second operator.</summary>
    public static ConvSpanEnd ToEnd(ConvSpan span) => (ConvSpanEnd)span;

    /// <summary>Clause 10.3.1 — a cast whose target is a type parameter (see 10.3.8).</summary>
    public static T TypeParameter<T>(object value) => (T)value;
}
