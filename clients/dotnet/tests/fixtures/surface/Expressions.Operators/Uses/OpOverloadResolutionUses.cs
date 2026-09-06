using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// A temperature whose operator set exists to be resolved against: three
/// <c>op_Addition</c> declarations at arity two, and a unary <c>op_UnaryNegation</c>
/// beside a binary <c>op_Subtraction</c> written with the same character.
/// </summary>
public readonly struct OpTemperature
{
    /// <summary>Constructs a temperature.</summary>
    public OpTemperature(double degrees) => Degrees = degrees;

    /// <summary>The temperature in degrees.</summary>
    public double Degrees { get; }

    // 12.4.5 — three candidates, all at arity two, distinguished only by operand types.
    public static OpTemperature operator +(OpTemperature left, OpTemperature right) =>
        new(left.Degrees + right.Degrees);

    public static OpTemperature operator +(OpTemperature left, double right) =>
        new(left.Degrees + right);

    public static OpTemperature operator +(OpTemperature left, int right) =>
        new(left.Degrees + right);

    // 12.4.4 vs 12.4.5 — one character, two declarations, told apart only by arity.
    public static OpTemperature operator -(OpTemperature value) => new(-value.Degrees);

    public static OpTemperature operator -(OpTemperature left, OpTemperature right) =>
        new(left.Degrees - right.Degrees);

    /// <inheritdoc/>
    public override string ToString() => $"{Degrees:0.#}deg";
}

/// <summary>
/// The operator overloading and overload resolution clauses of 12.4, made explicit: which
/// declaration each token selects, and what tells two candidates apart when the source
/// says the same thing at both use sites.
/// </summary>
public static class OpOverloadResolutionUses
{
    /// <summary>
    /// 12.4.3 — the same token bound to a user-defined operator on the left and to a
    /// predefined one on the right. Only one of these two is a member reference.
    /// </summary>
    public static (OpTemperature UserDefined, double Predefined) OperatorOverloading(
        OpTemperature temperature,
        double degrees) =>
        (temperature + temperature, degrees + degrees);

    /// <summary>
    /// 12.4.4 — unary operator overload resolution. The candidate set for <c>-</c> in this
    /// type has two members and only the arity of the use picks between them.
    /// </summary>
    public static OpTemperature UnaryResolution(OpTemperature temperature) => -temperature;

    /// <summary>
    /// 12.4.5 — binary operator overload resolution across the three <c>op_Addition</c>
    /// declarations: an exact match, a match after an implicit numeric conversion of the
    /// argument, and a match that requires no conversion of a narrower literal.
    /// </summary>
    public static (OpTemperature Exact, OpTemperature Promoted, OpTemperature Integral) BinaryResolution(
        OpTemperature temperature,
        float degrees) =>
        (temperature + temperature, temperature + degrees, temperature + 3);

    /// <summary>
    /// 12.4.5 / 12.4.6 — the same <c>-</c> token in its binary form, which reaches the
    /// other member of the pair <see cref="UnaryResolution"/> reaches.
    /// </summary>
    public static OpTemperature BinaryMinus(OpTemperature left, OpTemperature right) => left - right;

    /// <summary>
    /// 12.4.3 — the checked/unchecked pair. These two expressions are character-for-
    /// character identical apart from the keyword that wraps them, and they select
    /// different declarations: <c>op_Addition</c> and <c>op_CheckedAddition</c>.
    /// </summary>
    public static (OpMoney Unchecked, OpMoney Checked) CheckedAndUncheckedSelectDifferently(
        OpMoney left,
        OpMoney right) =>
        (unchecked(left + right), checked(left + right));

    /// <summary>
    /// 12.4.3 — the same contrast for the whole checked family: unary minus, increment,
    /// multiplication, division and the explicit conversion.
    /// </summary>
    public static string WholeCheckedFamily(OpMoney money)
    {
        OpMoney negated = checked(-money);
        // `checked(money + 1L)` would *not* reach a checked operator: the better
        // candidate for (OpMoney, long) has no checked variant, so the homogeneous
        // operand is what makes the checked form reachable here.
        OpMoney incremented = checked(money + new OpMoney(1));
        OpMoney scaled = checked(money * 2L);
        OpMoney divided = checked(money / 2L);
        int narrowed = checked((int)money);

        OpMoney negatedUnchecked = unchecked(-money);
        int narrowedUnchecked = unchecked((int)money);

        return $"{negated}{incremented}{scaled}{divided}{narrowed}{negatedUnchecked}{narrowedUnchecked}";
    }

    /// <summary>
    /// 12.4.6 — a use whose candidate set comes from neither operand's own declarations:
    /// the operator is in an extension block, and the walk of 12.4.6 has to consider
    /// extension members to find it.
    /// </summary>
    public static OpReading CandidatesFromAnExtension(OpReading left, OpReading right) => left * right;

    /// <summary>
    /// 12.4.6 — and one whose candidate set comes from the operand's base class, because
    /// the operand's own type declares no <c>+</c> at all.
    /// </summary>
    public static OpBaseGauge CandidatesFromABaseClass(OpDerivedGauge left, OpDerivedGauge right) =>
        left + right;
}
