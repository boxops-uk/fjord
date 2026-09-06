namespace Surface.Conversions.ImplicitForms;

/// <summary>
/// Clauses 10.2.12 and 10.3.8 — the conversions available to and from a type parameter.
/// What is permitted depends entirely on the constraints, so every method here states its
/// own, and the constraint is the only thing that makes the body legal.
///
/// The deliberate hazard is <see cref="Pass{T}(T)"/> beside <see cref="Pass(object)"/>: two
/// overloads whose parameter types are the same once <c>T</c> is replaced by its effective
/// base class.
/// </summary>
public static class ConvTypeParameters
{
    /// <summary>Clause 10.2.12 — <c>T</c> to <c>object</c>, which every type parameter has.</summary>
    public static object ToObject<T>(T value) => value!;

    /// <summary>Clause 10.2.12 — <c>T</c> to its effective base class.</summary>
    public static ConvNode ToEffectiveBase<T>(T value)
        where T : ConvNode
        => value;

    /// <summary>Clause 10.2.12 — <c>T</c> to an interface named in its constraints.</summary>
    public static IConvLabelled ToConstraintInterface<T>(T value)
        where T : IConvLabelled
        => value;

    /// <summary>Clause 10.2.12 — <c>T</c> to <c>U</c>, when <c>T</c> is constrained to <c>U</c>.</summary>
    public static TBase ToOtherTypeParameter<T, TBase>(T value)
        where T : TBase
        => value;

    /// <summary>Clause 10.2.12 — the null literal to a <c>class</c>-constrained parameter.</summary>
    public static T? NullOf<T>()
        where T : class
        => null;

    /// <summary>Clause 10.2.12 — <c>T?</c> to <c>object</c> for a <c>struct</c>-constrained parameter.</summary>
    public static object? NullableToObject<T>(T? value)
        where T : struct
        => value;

    /// <summary>Clause 10.2.12, hazard — the generic half of the overload pair.</summary>
    public static void Pass<T>(T value)
        where T : class
    {
    }

    /// <summary>Clause 10.2.12, hazard — the half whose parameter is <c>T</c>'s effective base.</summary>
    public static void Pass(object value)
    {
    }

    /// <summary>Clause 10.3.8 — <c>object</c> to <c>T</c>, the unconstrained downward cast.</summary>
    public static T FromObject<T>(object value) => (T)value;

    /// <summary>
    /// Clause 10.3.8 — <c>T</c> to a class derived from its effective base class. The clause
    /// does not list this conversion, so the cast has to go through <c>object</c>; the
    /// direct form is CS0030 and is the reason this method reads the way it does.
    /// </summary>
    public static ConvLeaf ToDerived<T>(T value)
        where T : ConvNode
        => (ConvLeaf)(object)value;

    /// <summary>Clause 10.3.8 — <c>T</c> to an interface it is not constrained to.</summary>
    public static IConvLabelled ToUnconstrainedInterface<T>(T value)
        where T : ConvNode
        => (IConvLabelled)value;

    /// <summary>Clause 10.3.8 — <c>T</c> to <c>U</c> through <c>object</c>, which always compiles.</summary>
    public static TOther Reinterpret<T, TOther>(T value) => (TOther)(object)value!;

    /// <summary>Clause 10.3.8 — <c>T</c> to a value type, unboxed through <c>object</c>.</summary>
    public static int Unwrap<T>(T value) => (int)(object)value!;
}
