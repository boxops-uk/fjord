// Clause 16.4.3 — Inheritance. A struct implicitly inherits from System.ValueType, which
// inherits from object. A struct is implicitly sealed: it may not be a base type, and no
// struct member may be `abstract` or `virtual`. `override` is permitted on a struct member
// only when the member being overridden is inherited from System.ValueType or object —
// `Equals`, `GetHashCode` and `ToString`, and nothing else.
//
// The clause is `both`: the inheritance is a reference to a type no token names, and the
// overrides are declarations. The hazard is the override itself — `ToString` here and
// `object.ToString` in the framework want the same name, the same signature and different
// declaring types, so an identity that drops the declaring type merges a struct's override
// into the base member it overrides.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.3 — a struct that overrides all three overridable inherited members, and
/// implements the framework interfaces that make the overrides meaningful. There is no
/// base type clause anywhere in the declaration, and the type has a base type.
/// </summary>
public struct StOverridingPoint : IEquatable<StOverridingPoint>, IComparable<StOverridingPoint>, IFormattable
{
    /// <summary>The horizontal coordinate.</summary>
    public double X;

    /// <summary>The vertical coordinate.</summary>
    public double Y;

    /// <summary>Clause 16.4.9 — the declared constructor.</summary>
    public StOverridingPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Clause 16.4.3 — the override of <c>ValueType.Equals(object)</c>.</summary>
    public override bool Equals(object? obj) => obj is StOverridingPoint other && Equals(other);

    /// <summary>
    /// Clause 16.4.3 — a strongly typed <c>Equals</c> beside the override. Two members
    /// with one name in one type, told apart only by a parameter type, one of which is an
    /// override and one of which is not.
    /// </summary>
    public bool Equals(StOverridingPoint other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <summary>Clause 16.4.3 — the override of <c>ValueType.GetHashCode</c>.</summary>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>Clause 16.4.3 — the override of <c>object.ToString</c>.</summary>
    public override string ToString() => $"({X}, {Y})";

    /// <summary>
    /// Clause 16.4.3 — an interface method whose name is the name of the override above
    /// and whose signature is not. Three <c>ToString</c>s are now reachable on this type:
    /// this one, the override, and the inherited one the override replaced.
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        format == "x" ? X.ToString(formatProvider) : ToString();

    /// <summary>Clause 16.4.3 — ordering, so the struct is comparable as well as equatable.</summary>
    public int CompareTo(StOverridingPoint other)
    {
        int byX = X.CompareTo(other.X);
        return byX != 0 ? byX : Y.CompareTo(other.Y);
    }
}

/// <summary>
/// Clause 16.4.3 — a struct that overrides nothing, so all three members resolve to the
/// implementations in <c>System.ValueType</c>. It exists to be the other half of the
/// comparison: the same three calls, no declarations to find.
/// </summary>
public struct StInheritingPoint
{
    /// <summary>The horizontal coordinate.</summary>
    public double X;

    /// <summary>The vertical coordinate.</summary>
    public double Y;
}

/// <summary>
/// Clause 16.4.3 — the references. A struct's base type is reachable in four ways here and
/// written in none of them.
/// </summary>
public static class StInheritanceUse
{
    /// <summary>Clause 16.4.3 — the implicit conversion to the implicit base type.</summary>
    public static ValueType AsValueType(StInheritingPoint point) => point;

    /// <summary>Clause 16.4.3 — the implicit conversion to <c>object</c>, two links up.</summary>
    public static object AsObject(StInheritingPoint point) => point;

    /// <summary>Clause 16.4.3 — the overridden member, chosen by the static type.</summary>
    public static string OverriddenText(StOverridingPoint point) => point.ToString();

    /// <summary>Clause 16.4.3 — the inherited member on the struct that overrides nothing.</summary>
    public static string InheritedText(StInheritingPoint point) => point.ToString() ?? string.Empty;

    /// <summary>Clause 16.4.3 — the interface overload, told apart by its arguments.</summary>
    public static string FormattedText(StOverridingPoint point) =>
        point.ToString("x", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Clause 16.4.3 — a struct is implicitly sealed, so a generic constraint is the only
    /// way to write code over "some struct", and `struct` in the constraint is a keyword
    /// with no declaration behind it.
    /// </summary>
    public static bool SameShape<T>(T left, T right)
        where T : struct, IEquatable<T> => left.Equals(right);

    /// <summary>Clause 16.4.3 — the runtime base type, asserted rather than written.</summary>
    public static bool BaseIsValueType() => typeof(StInheritingPoint).BaseType == typeof(ValueType);
}
