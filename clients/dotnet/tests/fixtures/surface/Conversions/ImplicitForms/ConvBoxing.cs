using System;

namespace Surface.Conversions.ImplicitForms;

/// <summary>One of two interfaces declaring the same member signature.</summary>
public interface IConvBoxLeft
{
    /// <summary>Boxes the receiver.</summary>
    object Box();
}

/// <summary>The other. Its <c>Box</c> is indistinguishable from the left one's.</summary>
public interface IConvBoxRight
{
    /// <summary>Boxes the receiver.</summary>
    object Box();
}

/// <summary>
/// Clause 10.2.9, deliberate hazard: a struct implementing both <see cref="IConvBoxLeft"/>
/// and <see cref="IConvBoxRight"/> explicitly. The two members agree in name, parameter list
/// and return type, and differ only in which interface each one names — the narrowest
/// difference between two declarations that this project can state.
/// </summary>
public readonly struct ConvCoin : IConvBoxLeft, IConvBoxRight
{
    /// <summary>A coin worth <paramref name="pence"/>.</summary>
    public ConvCoin(int pence) => Pence = pence;

    /// <summary>What it is worth.</summary>
    public int Pence { get; }

    /// <summary>The left interface's member. Boxing <c>this</c> is the clause's conversion.</summary>
    object IConvBoxLeft.Box() => this;

    /// <summary>The right interface's member, identical in every part but the qualifier.</summary>
    object IConvBoxRight.Box() => this;
}

/// <summary>
/// Clause 10.2.9 — the boxing conversions. A value type becomes a reference, which is the
/// one implicit conversion that allocates.
/// </summary>
public static class ConvBoxing
{
    /// <summary>Clause 10.2.9 — a simple type to <c>object</c>.</summary>
    public static object BoxInt(int value) => value;

    /// <summary>Clause 10.2.9 — an enum to <c>object</c>.</summary>
    public static object BoxStroke(ConvStroke stroke) => stroke;

    /// <summary>Clause 10.2.9 — a nullable, which boxes the value or gives null.</summary>
    public static object? BoxNullableInt(int? value) => value;

    /// <summary>Clause 10.2.9 — a struct declared in this project.</summary>
    public static object BoxCoin(ConvCoin coin) => coin;

    /// <summary>Clause 10.2.9 — a struct to an interface it implements.</summary>
    public static IConvBoxLeft CoinToLeft(ConvCoin coin) => coin;

    /// <summary>Clause 10.2.9 — a struct to <see cref="ValueType"/>.</summary>
    public static ValueType CoinToValueType(ConvCoin coin) => coin;

    /// <summary>Clause 10.2.9 — an enum to <see cref="Enum"/>, not to <see cref="ValueType"/> first.</summary>
    public static Enum StrokeToEnum(ConvStroke stroke) => stroke;

    /// <summary>Clause 10.2.9 — a nullable struct to the struct's interface.</summary>
    public static IConvBoxLeft? NullableCoinToLeft(ConvCoin? coin) => coin;

    /// <summary>Clause 10.2.9 — boxing a type parameter known to be a value type.</summary>
    public static object BoxTypeParameter<T>(T value)
        where T : struct
        => value;

    /// <summary>Clause 10.2.9 — a tuple, whose elements box with it.</summary>
    public static object BoxTuple((int Left, int Right) pair) => pair;
}
