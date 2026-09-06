using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.NullableForms;

/// <summary>
/// A ratio.
///
/// Clause 10.6.2 says that a user-defined conversion between two non-nullable value types
/// gets a <em>lifted</em> form between their nullable versions, for free. Nothing in the
/// source declares the lifted operator; there is one declaration and two shapes of use, which
/// is why this type exists on its own.
/// </summary>
public readonly struct ConvRatio
{
    /// <summary>A ratio of <paramref name="value"/>.</summary>
    public ConvRatio(double value) => Value = value;

    /// <summary>The ratio itself.</summary>
    public double Value { get; }

    /// <summary>Clause 10.6.2 — the declaration whose lifted form is used below.</summary>
    public static implicit operator double(ConvRatio ratio) => ratio.Value;

    /// <summary>Clause 10.6.2 — and the explicit one, whose lifted form is also used below.</summary>
    public static explicit operator ConvRatio(double value) => new(value);
}

/// <summary>
/// Clauses 10.6.1 and 10.6.2 — the conversions that involve nullable types. The first clause
/// is the predefined lifting of the built-in conversions; the second is the lifting of a
/// user-defined operator, which is the same rule applied to a declaration the corpus owns.
/// </summary>
public static class ConvLifted
{
    /// <summary>Clause 10.6.1 — <c>S</c> to <c>S?</c>, the wrapping conversion.</summary>
    public static int? Wrap(int value) => value;

    /// <summary>Clause 10.6.1 — <c>S?</c> to <c>S</c>, the unwrapping one, which can throw.</summary>
    public static int Unwrap(int? value) => (int)value;

    /// <summary>Clause 10.6.1 — <c>S?</c> to <c>T?</c> over an implicit numeric conversion.</summary>
    public static long? WidenNullable(int? value) => value;

    /// <summary>Clause 10.6.1 — <c>S?</c> to <c>T?</c> over an explicit one.</summary>
    public static int? NarrowNullable(long? value) => (int?)value;

    /// <summary>Clause 10.6.1 — the lifted enumeration conversion.</summary>
    public static ConvStroke? ToNullableStroke(int? value) => (ConvStroke?)value;

    /// <summary>Clause 10.6.1 — a nullable of a struct this corpus declares.</summary>
    public static ConvSlot? WrapSlot(ConvSlot slot) => slot;

    /// <summary>Clause 10.6.1 — the null value surviving a lifted conversion.</summary>
    public static long? WidenNothing() => WidenNullable(null);

    /// <summary>
    /// Clause 10.6.2 — the lifted implicit operator. The reference here is to the one
    /// <c>implicit operator double</c> on <see cref="ConvRatio"/>; the lifting is the
    /// compiler's, and there is nothing in any file to point a lifted reference at.
    /// </summary>
    public static double? LiftedImplicit(ConvRatio? ratio) => ratio;

    /// <summary>Clause 10.6.2 — the lifted explicit operator, from the same one declaration.</summary>
    public static ConvRatio? LiftedExplicit(double? value) => (ConvRatio?)value;

    /// <summary>Clause 10.6.2 — the non-lifted use of the implicit operator, as a control.</summary>
    public static double NonLiftedImplicit(ConvRatio ratio) => ratio;

    /// <summary>Clause 10.6.2 — the non-lifted use of the explicit operator.</summary>
    public static ConvRatio NonLiftedExplicit(double value) => (ConvRatio)value;

    /// <summary>Clause 10.6.2 — a lifted conversion whose operand is the null literal.</summary>
    public static double? LiftedFromNothing() => LiftedImplicit(null);

    /// <summary>Clause 10.6.2 — a lifted conversion followed by a lifted numeric widening.</summary>
    public static decimal? LiftedThenWidened(ConvRatio? ratio) => (decimal?)(double?)ratio;
}
