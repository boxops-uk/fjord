// Clause 8.3.12 — nullable value types. T? is System.Nullable<T>, so every spelling below
// names one type, and the lifted operators are the reason a query about them is worth
// asking: the operator applied to int? is declared on neither int nor Nullable<int>.

namespace Surface.Types;

/// <summary>
/// 8.3.12 hazard — one type, three spellings: the <c>?</c> shorthand, the framework
/// generic, and the framework generic over the framework name of the element type.
/// </summary>
public static class TyNullableSpellings
{
    /// <summary>8.3.12 — the shorthand.</summary>
    public static int? ByShorthand = 1;

    /// <summary>8.3.12 hazard — the constructed generic it stands for.</summary>
    public static System.Nullable<int> ByGeneric = 2;

    /// <summary>8.3.12 hazard — and with the element type spelled out too.</summary>
    public static System.Nullable<System.Int32> ByFullGeneric = 3;

    /// <summary>8.3.12 — assignment among the three, which needs no conversion.</summary>
    public static int? Align()
    {
        ByGeneric = ByShorthand;
        ByFullGeneric = ByGeneric;
        return ByFullGeneric;
    }

    /// <summary>8.3.12 — a nullable form of a struct declared in this project.</summary>
    public static TyPoint2D? MaybePoint = new TyPoint2D(1, 2);

    /// <summary>8.3.12 — a nullable enum, which is a nullable value type like any other.</summary>
    public static TyStroke? MaybeStroke = TyStroke.Thick;

    /// <summary>8.3.12 — the members of System.Nullable&lt;T&gt; the language exposes.</summary>
    public static double PointOrZero() =>
        MaybePoint.HasValue ? MaybePoint.Value.X : MaybePoint.GetValueOrDefault().X;

    /// <summary>8.3.12 — the implicit conversion up and the explicit conversion down.</summary>
    public static int Narrow(int? value) => (int)value!;

    public static int? Widen(int value) => value;

    /// <summary>8.3.12 — the null literal is a value of every nullable value type.</summary>
    public static int? Nothing = null;

    /// <summary>8.3.12 — and `default` for a nullable value type is that same null.</summary>
    public static int? DefaultIsNull = default;
}

/// <summary>
/// 8.3.12 — lifted operators, null propagation, and the overload pair that <c>int</c> and
/// <c>int?</c> can legally form. <c>Double(int)</c> and <c>Double(int?)</c> are distinct
/// signatures, so an identity that strips nullability merges two callable methods.
/// </summary>
public static class TyNullableOperators
{
    /// <summary>8.3.12 hazard — the non-nullable overload.</summary>
    public static int Double(int value) => value * 2;

    /// <summary>8.3.12 hazard — the nullable overload, which differs only by the `?`.</summary>
    public static int? Double(int? value) => value * 2;

    /// <summary>8.3.12 — a lifted arithmetic operator: `+` over two int? operands.</summary>
    public static int? Add(int? left, int? right) => left + right;

    /// <summary>8.3.12 — a lifted comparison, which yields false when either side is null.</summary>
    public static bool Less(int? left, int? right) => left < right;

    /// <summary>8.3.12 — a lifted equality, which is true when both sides are null.</summary>
    public static bool Same(int? left, int? right) => left == right;

    /// <summary>8.3.12 / 9.4.4.29 — the null-coalescing operator over a nullable value type.</summary>
    public static int OrZero(int? value) => value ?? 0;

    /// <summary>8.3.12 — null-conditional member access, whose result is itself nullable.</summary>
    public static int? LengthOf(string? text) => text?.Length;

    /// <summary>8.3.12 — a nullable value type as a switch subject, with a null pattern arm.</summary>
    public static string Classify(int? value) => value switch
    {
        null => "absent",
        0 => "zero",
        > 0 => "positive",
        _ => "negative",
    };
}
