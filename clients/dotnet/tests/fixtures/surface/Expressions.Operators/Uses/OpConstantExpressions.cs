using System;

namespace Surface.Expressions.Operators.Uses;

/// <summary>Constants another type's constant expressions refer to.</summary>
public static class OpConstantSource
{
    /// <summary>A constant whose initializer is itself an operator expression.</summary>
    public const int Base = 2 * 3 + 1;

    /// <summary>A constant string, built by the one operator that works on strings.</summary>
    public const string Prefix = "op" + "-" + "const";

    /// <summary>A constant of enumeration type, built with a logical operator.</summary>
    public const OpChannel Channel = OpChannel.Left | OpChannel.Right;
}

/// <summary>An attribute whose argument must be a constant expression.</summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class OpWeightAttribute : Attribute
{
    /// <summary>Constructs the attribute.</summary>
    public OpWeightAttribute(int weight) => Weight = weight;

    /// <summary>The weight.</summary>
    public int Weight { get; }
}

/// <summary>
/// The constant expressions of 12.25 — expressions the compiler must evaluate, in each
/// position the language demands one.
/// </summary>
/// <remarks>
/// Every initializer here is an operator expression, and several of them refer to a
/// constant declared in another type. The reference is a real one — an index that folds
/// constants at index time records the value and loses the edge.
/// </remarks>
public static class OpConstantExpressions
{
    /// <summary>12.25 — a constant whose initializer refers to a constant in another type.</summary>
    public const int Derived = OpConstantSource.Base * 10;

    /// <summary>12.25 — a constant folded from a division and a remainder.</summary>
    public const int Folded = (OpConstantSource.Base * 100) / 7 % 13;

    /// <summary>
    /// 12.25 — an <c>unchecked</c> constant expression that overflows. Without the
    /// <c>unchecked</c> the compiler would refuse it, because a constant expression is
    /// evaluated in a checked context by default.
    /// </summary>
    public const int Overflowing = unchecked(int.MaxValue + 1);

    /// <summary>12.25 — a constant expression using the conditional operator.</summary>
    public const string Chosen = OpConstantSource.Base > 5 ? "big" : "small";

    /// <summary>12.25 — a constant expression using shift and complement.</summary>
    public const int Mask = ~(1 << 4) & 0xFF;

    /// <summary>12.25 — a constant of nullable type, whose only legal value is null.</summary>
    public const string? Absent = null;

    /// <summary>12.25 — a constant expression in a default parameter value.</summary>
    public static int WithDefault(int scale = OpConstantSource.Base + 1) => scale * Derived;

    /// <summary>12.25 — a constant expression as an attribute argument.</summary>
    [OpWeight(OpConstantSource.Base << 2)]
    public static int Weighted() => Derived;

    /// <summary>
    /// 12.25 — a constant expression as a <c>case</c> label and as an array size, and a
    /// <c>goto case</c> naming one.
    /// </summary>
    public static string CaseLabels(int value)
    {
        var buffer = new int[OpConstantSource.Base * 2];

        switch (value)
        {
            case OpConstantSource.Base:
                return "base";
            case OpConstantSource.Base + 1:
                goto case OpConstantSource.Base;
            case Folded:
                return $"folded {buffer.Length}";
            default:
                return OpConstantSource.Prefix;
        }
    }
}
