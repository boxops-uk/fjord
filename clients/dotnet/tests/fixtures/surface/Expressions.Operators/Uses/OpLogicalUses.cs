using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The logical operators of 12.15 and the conditional logical operators of 12.16.
/// </summary>
public static class OpLogicalUses
{
    /// <summary>12.15.2 — the integer logical operators.</summary>
    public static (int, uint, long) IntegerLogical(int i, uint u, long l) =>
        (i & 0xFF, u | 1u, l ^ 0xFFL);

    /// <summary>12.15.3 — the enumeration logical operators, on a flags enumeration.</summary>
    public static OpChannel EnumerationLogical(OpChannel left, OpChannel right) =>
        (left | right) & ~OpChannel.None ^ OpChannel.None;

    /// <summary>12.15.4 — the boolean logical operators, which do not short-circuit.</summary>
    public static bool BooleanLogical(bool left, bool right) => (left & right) | (left ^ right);

    /// <summary>
    /// 12.15.5 — the nullable boolean <c>&amp;</c> and <c>|</c>, which are three-valued
    /// and are not the lifted forms of the boolean operators.
    /// </summary>
    public static (bool?, bool?, bool?) NullableBooleanLogical(bool? left, bool? right) =>
        (left & right, left | right, left ^ right);

    /// <summary>12.15.2 — the user-defined logical operators of <see cref="OpBits"/>.</summary>
    public static (OpBits, OpBits, OpBits) UserDefinedLogical(OpBits left, OpBits right) =>
        (left & right, left | right, left ^ right);

    /// <summary>
    /// 12.4.8 — the lifted forms of those same operators, selected by operands of type
    /// <c>OpBits?</c>. The declaration referenced is the unlifted one; the lifting is the
    /// compiler's, and there is no second declaration for an index to point at.
    /// </summary>
    public static (OpBits?, OpBits?, OpBits?) LiftedUserDefinedLogical(OpBits? left, OpBits? right) =>
        (left & right, left | right, left ^ right);

    /// <summary>12.16.1 / 12.16.2 — the boolean conditional logical operators.</summary>
    public static bool BooleanConditionalLogical(bool left, bool right) =>
        (left && right) || (!left && !right);

    /// <summary>
    /// 12.16.3 — the user-defined conditional logical operators. <c>a &amp;&amp; b</c> on
    /// <see cref="OpFlag"/> is a reference to <c>op_False</c> and then to
    /// <c>op_BitwiseAnd</c>; <c>a || b</c> is <c>op_True</c> and then <c>op_BitwiseOr</c>.
    /// Four member references, from four characters that name none of them.
    /// </summary>
    public static (OpFlag And, OpFlag Or) UserDefinedConditionalLogical(OpFlag left, OpFlag right) =>
        (left && right, left || right);

    /// <summary>
    /// 12.16.1 — short-circuiting means the right operand is an expression that may not be
    /// evaluated. The call in it is still a reference the index must hold.
    /// </summary>
    public static bool ShortCircuitedOperand(string? text) =>
        text is not null && text.StartsWith("x");
}
