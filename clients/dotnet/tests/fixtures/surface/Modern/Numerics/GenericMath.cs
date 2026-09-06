using System.Numerics;

namespace Surface.Modern.Numerics;

/// <summary>
/// C# 11 — Generic math support: static abstract and static virtual interface members. An
/// interface may declare <c>static abstract</c> members — including operators, properties and
/// conversions — and a type parameter constrained to it may then invoke them as
/// <c>T.Member</c>. That is a call whose receiver is a *type parameter*, resolved per
/// substitution, so the reference recorded at the call site cannot be a member of any one type.
/// </summary>
/// <typeparam name="TSelf">The implementing type, which the interface constrains to itself.</typeparam>
public interface IScalar<TSelf>
    where TSelf : IScalar<TSelf>
{
    /// <summary>C# 11 — a <c>static abstract</c> property.</summary>
    static abstract TSelf Zero { get; }

    /// <summary>C# 11 — a <c>static abstract</c> method.</summary>
    static abstract TSelf Create(double value);

    /// <summary>C# 11 — a <c>static abstract</c> operator.</summary>
    static abstract TSelf operator +(TSelf left, TSelf right);

    /// <summary>C# 11 — a <c>static abstract</c> conversion operator.</summary>
    static abstract explicit operator double(TSelf value);

    /// <summary>
    /// C# 11 — a <c>static virtual</c> member, which has a body and may be overridden. The
    /// body calls the <c>static abstract</c> operator above through the type parameter.
    /// </summary>
    static virtual TSelf Twice(TSelf value) => value + value;

    /// <summary>An ordinary instance member, so the interface is not all-static.</summary>
    string Describe();
}

/// <summary>C# 11 — a type implementing every static abstract member above.</summary>
public readonly struct Metres : IScalar<Metres>
{
    /// <summary>Creates a length.</summary>
    public Metres(double value) => Value = value;

    /// <summary>The length.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public static Metres Zero => new(0);

    /// <inheritdoc/>
    public static Metres Create(double value) => new(value);

    /// <inheritdoc/>
    public static Metres operator +(Metres left, Metres right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    public static explicit operator double(Metres value) => value.Value;

    /// <inheritdoc/>
    public string Describe() => $"{Value}m";
}

/// <summary>C# 11 — a second implementation, which overrides the <c>static virtual</c> member.</summary>
public readonly struct Feet : IScalar<Feet>
{
    /// <summary>Creates a length.</summary>
    public Feet(double value) => Value = value;

    /// <summary>The length.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public static Feet Zero => new(0);

    /// <inheritdoc/>
    public static Feet Create(double value) => new(value);

    /// <inheritdoc/>
    public static Feet operator +(Feet left, Feet right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    public static explicit operator double(Feet value) => value.Value;

    /// <summary>C# 11 — overriding a <c>static virtual</c> interface member.</summary>
    public static Feet Twice(Feet value) => new(value.Value * 2);

    /// <inheritdoc/>
    public string Describe() => $"{Value}ft";
}

/// <summary>C# 11 — the generic algorithms the static abstract members exist for.</summary>
public static class GenericMath
{
    /// <summary>
    /// C# 11 — <c>T.Zero</c> and <c>T.Twice</c>: static members invoked on a type parameter.
    /// </summary>
    /// <typeparam name="T">A scalar.</typeparam>
    public static T Sum<T>(IEnumerable<T> values)
        where T : IScalar<T>
    {
        var total = T.Zero;

        foreach (var value in values)
        {
            total += value;
        }

        return T.Twice(total);
    }

    /// <summary>C# 11 — the same over the framework's own generic-math interfaces.</summary>
    /// <typeparam name="T">Any number.</typeparam>
    public static T SumNumbers<T>(IEnumerable<T> values)
        where T : INumber<T>
    {
        var total = T.Zero;

        foreach (var value in values)
        {
            total = checked(total + value);
        }

        return total;
    }

    /// <summary>C# 11 — a constraint naming an operator interface rather than a number one.</summary>
    /// <typeparam name="T">Anything with a <c>&gt;</c>.</typeparam>
    public static T Larger<T>(T left, T right)
        where T : IComparisonOperators<T, T, bool>
        => left > right ? left : right;

    /// <summary>Substitutes both implementations, and two framework numbers.</summary>
    public static string All()
    {
        var metres = Sum<Metres>([new Metres(1), new Metres(2)]);
        var feet = Sum<Feet>([new Feet(3)]);

        return $"{metres.Describe()}{feet.Describe()}{SumNumbers<int>([1, 2, 3])}{Larger(2.5, 1.5)}";
    }
}
