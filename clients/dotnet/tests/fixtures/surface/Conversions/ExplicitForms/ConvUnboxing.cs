using System;
using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.ExplicitForms;

/// <summary>
/// A token identifier.
///
/// Clause 10.3.7, deliberate hazard: this struct declares a public <c>Box</c> and an explicit
/// implementation of <see cref="IConvBoxLeft.Box"/>. Both are named <c>Box</c>, both take no
/// arguments and both return <c>object</c>; nothing about the two declarations differs except
/// that one is qualified by an interface. Unboxing is how the second one is reached from the
/// first one's type.
/// </summary>
public readonly struct ConvToken : IConvBoxLeft
{
    /// <summary>A token with the given identifier.</summary>
    public ConvToken(int id) => Id = id;

    /// <summary>Which token this is.</summary>
    public int Id { get; }

    /// <summary>The public member, which boxes the whole struct.</summary>
    public object Box() => this;

    /// <summary>The interface member of the same name, which boxes only the identifier.</summary>
    object IConvBoxLeft.Box() => Id;
}

/// <summary>
/// Clause 10.3.7 — the unboxing conversions. Each one is a cast from a reference type back to
/// a value type, and each fails at run time unless the box holds exactly that type.
/// </summary>
public static class ConvUnboxing
{
    /// <summary>Clause 10.3.7 — <c>object</c> to a simple type.</summary>
    public static int ToInt(object boxed) => (int)boxed;

    /// <summary>Clause 10.3.7 — <c>object</c> to an enum type.</summary>
    public static ConvStroke ToStroke(object boxed) => (ConvStroke)boxed;

    /// <summary>Clause 10.3.7 — <c>object</c> to a nullable type, where null is permitted.</summary>
    public static int? ToNullableInt(object? boxed) => (int?)boxed;

    /// <summary>Clause 10.3.7 — <c>object</c> to a struct declared in this project.</summary>
    public static ConvCoin ToCoin(object boxed) => (ConvCoin)boxed;

    /// <summary>Clause 10.3.7 — an interface reference back to the struct behind it.</summary>
    public static ConvCoin CoinFromInterface(IConvBoxLeft left) => (ConvCoin)left;

    /// <summary>Clause 10.3.7 — <see cref="ValueType"/> to a value type.</summary>
    public static int FromValueType(ValueType boxed) => (int)boxed;

    /// <summary>Clause 10.3.7 — <see cref="Enum"/> to a specific enum type.</summary>
    public static ConvStroke FromEnumType(Enum boxed) => (ConvStroke)boxed;

    /// <summary>Clause 10.3.7 — to a type parameter constrained to be a value type.</summary>
    public static T FromObject<T>(object boxed)
        where T : struct
        => (T)boxed;

    /// <summary>Clause 10.3.7 — box and unbox in one expression, which is a no-op with a cost.</summary>
    public static int RoundTrip(int value) => (int)(object)value;

    /// <summary>Clause 10.3.7 — the boxed form of a tuple, unboxed whole.</summary>
    public static (int Left, int Right) ToPair(object boxed) => ((int, int))boxed;

    /// <summary>Clause 10.3.7, hazard — the public <c>Box</c>, then the value back out of it.</summary>
    public static ConvToken ThroughPublicBox(ConvToken token) => (ConvToken)token.Box();

    /// <summary>Clause 10.3.7, hazard — the interface <c>Box</c>, which boxes something else.</summary>
    public static int ThroughInterfaceBox(ConvToken token) => (int)((IConvBoxLeft)token).Box();
}
