using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The lifted operators of 12.4.8: a user-defined operator on a non-nullable value type
/// gains a lifted form over the corresponding nullable type, and the lifted form has no
/// declaration of its own.
/// </summary>
/// <remarks>
/// Every use below is a reference to a member declared on <see cref="OpMoney"/> or
/// <see cref="OpFlag"/> whose parameter types do not match the operand types at the use
/// site. An index that records the referenced declaration records the unlifted one; an
/// index that records a signature match records nothing.
/// </remarks>
public static class OpLiftedOperatorUses
{
    /// <summary>12.4.8 — the lifted unary operators.</summary>
    public static (OpMoney? Plus, OpMoney? Minus, OpFlag? Negated) LiftedUnary(
        OpMoney? money,
        OpFlag? flag) =>
        (+money, -money, !flag);

    /// <summary>12.4.8 — the lifted arithmetic operators.</summary>
    public static (OpMoney? Sum, OpMoney? Difference, OpMoney? Product) LiftedArithmetic(
        OpMoney? left,
        OpMoney? right) =>
        (left + right, left - right, left * 2L);

    /// <summary>
    /// 12.4.8 — the lifted increment operators, which act on a nullable variable and leave
    /// it null when it was null.
    /// </summary>
    public static OpMoney? LiftedIncrement(OpMoney? money)
    {
        money++;
        ++money;
        money--;

        return money;
    }

    /// <summary>
    /// 12.4.8 — the lifted relational operators, whose result is <c>bool</c> and not
    /// <c>bool?</c>: a null operand makes them false rather than null.
    /// </summary>
    public static (bool Less, bool Greater, bool AtMost, bool AtLeast) LiftedRelational(
        OpMoney? left,
        OpMoney? right) =>
        ((left < right), (left > right), (left <= right), (left >= right));

    /// <summary>
    /// 12.4.8 / 12.14.10 — the lifted equality operators, and the same token against the
    /// null literal, which 12.14.10 gives its own rule.
    /// </summary>
    public static (bool Equal, bool Unequal, bool IsNull) LiftedEquality(
        OpMoney? left,
        OpMoney? right) =>
        (left == right, left != right, left == null);

    /// <summary>
    /// 12.4.8 — one lifted shift, to show the lifting applies to the whole binary family
    /// and not only to arithmetic.
    /// </summary>
    public static OpBits? LiftedShift(OpBits? bits) => bits << 2;

    /// <summary>
    /// 12.4.8 — <c>operator true</c> and <c>operator false</c> are not lifted, so the
    /// nullable form has to be unwrapped before it can be a condition.
    /// </summary>
    public static string NotLifted(OpFlag? flag) =>
        flag.HasValue ? (flag.Value ? "yes" : "no") : "unknown";
}
