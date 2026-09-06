using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>A flags enumeration, so the enum arithmetic and logical forms have a subject.</summary>
[Flags]
public enum OpChannel
{
    /// <summary>No channel.</summary>
    None = 0,

    /// <summary>The left channel.</summary>
    Left = 1,

    /// <summary>The right channel.</summary>
    Right = 2,

    /// <summary>Both channels.</summary>
    Both = Left | Right,
}

/// <summary>
/// The arithmetic operators of 12.12, in each predefined form the clause enumerates and in
/// the user-defined form 12.4.5 resolves.
/// </summary>
/// <remarks>
/// The mixed-type expressions here are also where the numeric promotions of 12.4.7 happen:
/// they are conversions the compiler inserts, with no operator and no member behind them,
/// which is why that clause has no construct of its own in this corpus.
/// </remarks>
public static class OpArithmeticUses
{
    /// <summary>12.12.2 — multiplication, over each predefined numeric shape.</summary>
    public static (int, long, float, double, decimal, OpMoney, OpReading) Multiplication(
        int i,
        long l,
        float f,
        double d,
        decimal m,
        OpMoney money,
        OpReading reading) =>
        (i * i, i * l, f * f, d * d, m * m, money * 3L, reading * reading);

    /// <summary>
    /// 12.12.2 — the same token in a <c>checked</c> context selects
    /// <c>op_CheckedMultiply</c> on the user-defined operand.
    /// </summary>
    public static OpMoney CheckedMultiplication(OpMoney money, long factor) =>
        checked(money * factor);

    /// <summary>12.12.3 — division, including the decimal and user-defined forms.</summary>
    public static (int, double, decimal, OpMoney) Division(int i, double d, decimal m, OpMoney money) =>
        (i / 2, d / 2.0, m / 2m, money / 2L);

    /// <summary>12.12.4 — remainder, which has no checked form.</summary>
    public static (int, double, decimal, OpMoney) Remainder(int i, double d, decimal m, OpMoney money) =>
        (i % 3, d % 3.0, m % 3m, money % 3L);

    /// <summary>
    /// 12.12.5 — addition in every form the clause lists: numeric, enumeration, string
    /// concatenation, delegate combination and user-defined.
    /// </summary>
    public static (int, OpChannel, string, Action?, OpMoney) Addition(
        int i,
        OpChannel channel,
        string text,
        Action first,
        Action second,
        OpMoney money) =>
        (i + i, channel + 1, text + i, first + second, money + money);

    /// <summary>
    /// 12.4.5 — the three <c>op_Addition</c> declarations of <see cref="OpMoney"/>, each
    /// selected by one of these three uses. The token is the same in all three.
    /// </summary>
    public static (OpMoney Homogeneous, OpMoney RightScalar, OpMoney LeftScalar) AdditionOverloads(
        OpMoney money) =>
        (money + money, money + 7L, 7L + money);

    /// <summary>
    /// 12.12.5 — addition in a <c>checked</c> context, which selects
    /// <c>op_CheckedAddition</c> where 12.4.5 above selected <c>op_Addition</c>.
    /// </summary>
    public static OpMoney CheckedAddition(OpMoney left, OpMoney right) => checked(left + right);

    /// <summary>
    /// 12.12.6 — subtraction in every form: numeric, enumeration minus underlying type,
    /// enumeration minus enumeration, delegate removal and user-defined.
    /// </summary>
    public static (int, OpChannel, int, Action?, OpMoney) Subtraction(
        int i,
        OpChannel channel,
        Action first,
        Action second,
        OpMoney money) =>
        (i - i, channel - 1, OpChannel.Both - OpChannel.Left, first - second, money - money);

    /// <summary>12.12.6 — subtraction in a <c>checked</c> context.</summary>
    public static OpMoney CheckedSubtraction(OpMoney left, OpMoney right) => checked(left - right);

    /// <summary>
    /// 12.4.6 — the candidate-set walk: <c>+</c> finds nothing in
    /// <see cref="OpDerivedGauge"/> and continues into <see cref="OpBaseGauge"/>, while
    /// <c>-</c> stops in the derived type. Neither use names the declaring type.
    /// </summary>
    public static (OpBaseGauge FromBase, OpDerivedGauge FromDerived, OpDerivedGauge Unary) CandidateWalk(
        OpDerivedGauge left,
        OpDerivedGauge right) =>
        (left + right, left - right, -left);

    /// <summary>
    /// 12.4.6 / 12.12.2 — operators reached through a C# 14 extension block, where the
    /// declaring type is neither operand's.
    /// </summary>
    public static (OpReading Product, OpReading Sum, OpReading Negated, double Halved) ExtensionOperators(
        OpReading left,
        OpReading right) =>
        (left * right, left + right, -left, left.Halved);
}
