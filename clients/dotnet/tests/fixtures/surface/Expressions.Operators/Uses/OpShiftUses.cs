using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>The shift operators of 12.13, predefined and user-defined.</summary>
/// <remarks>
/// The two <c>&lt;&lt;</c> uses on <see cref="OpBits"/> select different declarations —
/// one takes an <c>int</c> right operand, the other a <c>string</c>, which C# 11's
/// relaxed shift-operator requirements allow. Both uses are the same two characters.
/// </remarks>
public static class OpShiftUses
{
    /// <summary>12.13 — left shift over each predefined operand type.</summary>
    public static (int, uint, long, ulong) PredefinedLeftShift(int i, uint u, long l, ulong ul) =>
        (i << 1, u << 1, l << 1, ul << 1);

    /// <summary>12.13 — arithmetic right shift, which keeps the sign bit.</summary>
    public static (int, uint, long, ulong) PredefinedRightShift(int i, uint u, long l, ulong ul) =>
        (i >> 1, u >> 1, l >> 1, ul >> 1);

    /// <summary>
    /// 12.13 — the unsigned right shift added in C# 11, which is a distinct operator and
    /// not a spelling of <c>&gt;&gt;</c>.
    /// </summary>
    public static (int, long) UnsignedRightShift(int i, long l) => (i >>> 1, l >>> 1);

    /// <summary>12.13 — the shift count is promoted, so the right operand may be narrower.</summary>
    public static int PromotedShiftCount(int value, byte count) => value << count;

    /// <summary>
    /// 12.13 / 12.4.5 — the two user-defined <c>op_LeftShift</c> declarations, chosen by
    /// the type of an operand the token does not name.
    /// </summary>
    public static (OpBits ByInt, OpBits ByString) UserDefinedLeftShift(OpBits bits) =>
        (bits << 3, bits << "3");

    /// <summary>12.13 — the user-defined right shifts, ordinary and unsigned.</summary>
    public static (OpBits Arithmetic, OpBits Unsigned) UserDefinedRightShift(OpBits bits) =>
        (bits >> 3, bits >>> 3);
}
