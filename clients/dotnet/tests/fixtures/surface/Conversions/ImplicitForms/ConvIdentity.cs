using System.Collections.Generic;

namespace Surface.Conversions.ImplicitForms;

/// <summary>
/// A single-value box. It exists to be instantiated at two type arguments that clause 10.2.2
/// declares identity-convertible, so that two declarations spell one type two ways.
/// </summary>
/// <typeparam name="T">Anything at all, including <c>dynamic</c>.</typeparam>
public sealed class ConvIdentityBox<T>
{
    /// <summary>Boxes <paramref name="value"/>.</summary>
    public ConvIdentityBox(T value) => Value = value;

    /// <summary>The boxed value.</summary>
    public T Value { get; }
}

/// <summary>
/// Clause 10.2.2 — the identity conversion. It converts between types the language calls
/// the same type but the source spells differently: <c>object</c> and <c>dynamic</c>, and
/// two tuple types whose element names differ.
/// </summary>
public static class ConvIdentity
{
    /// <summary>Clause 10.2.2 — <c>dynamic</c> to <c>object</c>, which emits no code.</summary>
    public static object FromDynamic(dynamic value) => value;

    /// <summary>Clause 10.2.2 — <c>object</c> to <c>dynamic</c>, the same conversion backwards.</summary>
    public static dynamic ToDynamic(object value) => value;

    /// <summary>Clause 10.2.2 — a named tuple type to the unnamed one it is identical to.</summary>
    public static (int, int) DropNames((int Left, int Right) pair) => pair;

    /// <summary>Clause 10.2.2 — and back, gaining names that exist only in the source.</summary>
    public static (int Left, int Right) AddNames((int, int) pair) => pair;

    /// <summary>Clause 10.2.2 — identity applied under a constructed type.</summary>
    public static List<object> ForgetDynamic(List<dynamic> values) => values;

    // Clause 10.2.2, deliberate hazard: four declarations, two distinct declared types.
    // `ConvIdentityBox<object>` and `ConvIdentityBox<dynamic>` are one type to the language
    // and to metadata, where `dynamic` survives only as an attribute; the same holds for the
    // two tuple spellings below.

    /// <summary>A box of <c>object</c>.</summary>
    public static readonly ConvIdentityBox<object> Objects = new(1);

    /// <summary>A box of <c>dynamic</c> — the same constructed type as <see cref="Objects"/>.</summary>
    public static readonly ConvIdentityBox<dynamic> Dynamics = new(1);

    /// <summary>A list of a named tuple type.</summary>
    public static readonly List<(int Left, int Right)> NamedPairs = [];

    /// <summary>The same list, under the unnamed spelling of the same type.</summary>
    public static readonly List<(int, int)> UnnamedPairs = NamedPairs;
}
