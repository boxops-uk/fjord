// Clause 16 — Structs — and clause 16.1, General. A struct is a value type that can
// declare constants, fields, methods, properties, events, indexers, operators,
// constructors and nested types; unlike a class it does not require heap allocation, it
// derives implicitly from System.ValueType, and it is implicitly sealed.
//
// The whole of clause 16 hangs on two things written nowhere in the source: the base type
// and the parameterless constructor. Both are here, both unwritten.

namespace Surface.Structs;

/// <summary>
/// Clause 16 / 16.1 — the smallest complete struct: a value type with two instance
/// fields and one declared constructor. What the compiler knows about it and what this
/// file says differ in three ways, all of them by design: there is a second constructor
/// (16.4.9) that is written nowhere, there is a base type <c>System.ValueType</c>
/// (16.4.3) that no token names, and the members inherited through it are part of the
/// type's member list and part of no file.
/// </summary>
public struct StCoin
{
    /// <summary>Clause 16.3.1 — an instance field, in minor units.</summary>
    public long Minor;

    /// <summary>Clause 16.3.1 — a second instance field, the ISO 4217 code.</summary>
    public string Code;

    /// <summary>
    /// Clause 16.4.9 — the declared constructor. Declaring it does not remove the
    /// parameterless one: <c>default(StCoin)</c> and <c>new StCoin()</c> both stay legal.
    /// </summary>
    public StCoin(long minor, string code)
    {
        Minor = minor;
        Code = code;
    }

    /// <summary>Clause 16.3.1 — a method whose receiver is a variable, not a reference.</summary>
    public string Format() => $"{Minor} {Code}";
}

/// <summary>
/// Clause 16.1 hazard — one struct declaration reached three ways. The bare value type,
/// the boxed form, and the nullable form are three static types over a single
/// declaration; an identity that keeps only the unqualified name merges the references.
/// </summary>
public static class StCoinUse
{
    /// <summary>Clause 16.1 — the value type itself, at its default value.</summary>
    public static StCoin Zero = default;

    /// <summary>Clause 16.1 / 16.4.6 — the same declaration behind a reference type.</summary>
    public static object BoxedZero = default(StCoin);

    /// <summary>Clause 16.1 — the nullable form, a distinct constructed type.</summary>
    public static StCoin? Missing = null;

    /// <summary>
    /// Clause 16.1 — construction with arguments and construction without, in one method,
    /// so both constructors of the member list are invoked from source.
    /// </summary>
    public static string Both()
    {
        StCoin declared = new StCoin(250, "GBP");
        StCoin implicitly = new StCoin();
        return declared.Format() + "/" + implicitly.Format();
    }
}
