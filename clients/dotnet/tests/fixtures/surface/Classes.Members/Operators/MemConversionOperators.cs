// Clause 15.10.4 (conversion operators). A conversion operator's emitted name is `op_Implicit`
// or `op_Explicit` and carries **no trace of what it converts to**, so this is the only member
// in the language whose overloads differ *only in return type* — which is not part of a C#
// signature anywhere else, and here is the only thing separating two members.
//
// That is the hazard, and it is the strongest one in clause 15.10:
//
//   * `MemMeasure` declares `explicit operator int`, `explicit operator long` and
//     `explicit operator string`. Three members named `op_Explicit`, all three taking one
//     `MemMeasure` parameter. Only the return type differs. An identity of
//     (type, name, parameter types) mints **one** string for all three.
//   * `explicit operator checked int` sits beside `explicit operator int` — same source token,
//     same parameter, same return type — separated only by the `checked` keyword, into
//     `op_CheckedExplicit`.
//   * A conversion in and a conversion out are both `op_Implicit` here: `MemMeasure -> double`
//     and `int -> MemMeasure`. Same name, same arity, and the parameter type is the only
//     difference — the reverse of the case above.
//
// A conversion operator is also the one member whose *references* are invisible: `(int)measure`
// names no member, and `double weight = measure;` names nothing at all.

namespace Surface.Classes.Members.Operators;

/// <summary>15.10.4: every conversion-operator form, on one type.</summary>
public readonly struct MemMeasure
{
    /// <summary>The measured amount.</summary>
    public int Amount { get; }

    /// <summary>Fixes a measure at an amount.</summary>
    public MemMeasure(int amount) => Amount = amount;

    /// <summary>15.10.4: an explicit conversion out, to <c>int</c> — `op_Explicit`.</summary>
    public static explicit operator int(MemMeasure measure) => measure.Amount;

    /// <summary>15.10.4: the checked form of that conversion — `op_CheckedExplicit`. Identical
    /// in every part of the signature to the one above.</summary>
    public static explicit operator checked int(MemMeasure measure) => checked(measure.Amount);

    /// <summary>15.10.4: an explicit conversion out, to <c>long</c> — `op_Explicit` again.
    /// Second member of that name; only the return type differs.</summary>
    public static explicit operator long(MemMeasure measure) => measure.Amount;

    /// <summary>15.10.4: an explicit conversion out, to <c>string</c> — `op_Explicit` a third
    /// time.</summary>
    public static explicit operator string(MemMeasure measure) => measure.Amount.ToString();

    /// <summary>15.10.4: an implicit conversion out, to <c>double</c> — `op_Implicit`. It is
    /// implicit because it cannot lose information, which is the rule the clause states and the
    /// compiler does not check.</summary>
    public static implicit operator double(MemMeasure measure) => measure.Amount;

    /// <summary>15.10.4: an implicit conversion *in*, from <c>int</c> — `op_Implicit` again.
    /// Same name as the one above, and this time it is the parameter type that differs and the
    /// return type that is this member's own type.</summary>
    public static implicit operator MemMeasure(int amount) => new MemMeasure(amount);

    /// <summary>15.10.4: an explicit conversion in, from <c>double</c>, so both directions exist
    /// at both explicitness levels.</summary>
    public static explicit operator MemMeasure(double amount) => new MemMeasure((int)amount);

    /// <summary>15.10.1: a readable form.</summary>
    public override string ToString() => $"measure {Amount}";
}

/// <summary>15.10.4: a second type, so a conversion between two user-defined types exists and is
/// not a conversion to a framework one.</summary>
public readonly struct MemWeight
{
    /// <summary>The weight in whatever unit the caller had in mind.</summary>
    public int Value { get; }

    /// <summary>Fixes a weight.</summary>
    public MemWeight(int value) => Value = value;

    /// <summary>15.10.4: user-defined type to user-defined type, implicitly. Declared on the
    /// source type; it could equally have been declared on the target, and an index has to hold
    /// it wherever it is.</summary>
    public static implicit operator MemMeasure(MemWeight weight) => new MemMeasure(weight.Value);

    /// <summary>15.10.4: and back, explicitly, declared on the same side.</summary>
    public static explicit operator MemWeight(MemMeasure measure) => new MemWeight(measure.Amount);
}

/// <summary>15.10.4: the references. A conversion reference is a cast, an assignment, or
/// nothing visible at all — no member name appears at any of these call sites.</summary>
public static class MemConversionUse
{
    /// <summary>15.10.4: the explicit conversions out, each written as a cast whose target type
    /// is what selects the member.</summary>
    public static string ExplicitOut()
    {
        var measure = new MemMeasure(7);
        var asInt = (int)measure;
        var asLong = (long)measure;
        var asString = (string)measure;
        return $"{asInt} {asLong} {asString}";
    }

    /// <summary>15.10.4: the same cast in a `checked` context, which selects
    /// `op_CheckedExplicit` instead. The two call sites are spelled identically.</summary>
    public static int CheckedOut()
    {
        var measure = new MemMeasure(7);

        checked
        {
            return (int)measure;
        }
    }

    /// <summary>15.10.4: the implicit conversions, whose call sites name nothing — no cast, no
    /// member, no parentheses.</summary>
    public static string Implicit()
    {
        MemMeasure fromInt = 3;
        double asDouble = fromInt;
        MemMeasure fromWeight = new MemWeight(4);
        return $"{fromInt} {asDouble} {fromWeight}";
    }

    /// <summary>15.10.4: the explicit conversions in, and the one between two user-defined
    /// types.</summary>
    public static string ExplicitIn()
    {
        var fromDouble = (MemMeasure)2.5;
        var backToWeight = (MemWeight)fromDouble;
        return $"{fromDouble} {backToWeight.Value}";
    }
}
