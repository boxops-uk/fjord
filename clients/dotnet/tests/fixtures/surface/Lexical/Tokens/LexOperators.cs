// Clause 6.4.6 (operators and punctuators). The operator *tokens* are exercised by
// declaring every overloadable one, and the punctuator hazards are the token sequences a
// lexer has to split by context: `>>` closing two type argument lists, `?` before `[` or
// `.`, and `..`. The checked forms are the pair that matters most to an index: `operator +`
// and `operator checked +` have identical operator tokens and identical parameter lists,
// and differ only in a modifier.

using System.Collections.Generic;

namespace Surface.Lexical.Tokens;

/// <summary>
/// 6.4.6: every overloadable operator token, with the checked and unchecked forms of the
/// five that admit both. The two <c>+</c> declarations are the pair a query has to keep
/// apart: they differ by the <c>checked</c> modifier and by nothing else.
/// </summary>
public readonly struct LexOperatorTokens
{
    /// <summary>Builds one.</summary>
    public LexOperatorTokens(int value) => Value = value;

    /// <summary>What it holds.</summary>
    public int Value { get; }

    /// <summary>Binary <c>+</c>, which compiles to <c>op_Addition</c>.</summary>
    public static LexOperatorTokens operator +(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value + right.Value);

    /// <summary>
    /// The checked form of the same operator token and parameter list, which compiles to
    /// <c>op_CheckedAddition</c>.
    /// </summary>
    public static LexOperatorTokens operator checked +(LexOperatorTokens left, LexOperatorTokens right) =>
        new(checked(left.Value + right.Value));

    /// <summary>Binary <c>-</c>.</summary>
    public static LexOperatorTokens operator -(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value - right.Value);

    /// <summary>The checked form of binary <c>-</c>.</summary>
    public static LexOperatorTokens operator checked -(LexOperatorTokens left, LexOperatorTokens right) =>
        new(checked(left.Value - right.Value));

    /// <summary>Unary <c>+</c>, which shares its token with the binary form and not its name.</summary>
    public static LexOperatorTokens operator +(LexOperatorTokens value) => value;

    /// <summary>Unary <c>-</c>, likewise.</summary>
    public static LexOperatorTokens operator -(LexOperatorTokens value) => new(-value.Value);

    /// <summary>The checked form of unary <c>-</c>.</summary>
    public static LexOperatorTokens operator checked -(LexOperatorTokens value) =>
        new(checked(-value.Value));

    /// <summary>Logical negation.</summary>
    public static bool operator !(LexOperatorTokens value) => value.Value == 0;

    /// <summary>Bitwise complement.</summary>
    public static LexOperatorTokens operator ~(LexOperatorTokens value) => new(~value.Value);

    /// <summary>Increment.</summary>
    public static LexOperatorTokens operator ++(LexOperatorTokens value) => new(value.Value + 1);

    /// <summary>The checked form of increment.</summary>
    public static LexOperatorTokens operator checked ++(LexOperatorTokens value) =>
        new(checked(value.Value + 1));

    /// <summary>Decrement.</summary>
    public static LexOperatorTokens operator --(LexOperatorTokens value) => new(value.Value - 1);

    /// <summary>The checked form of decrement.</summary>
    public static LexOperatorTokens operator checked --(LexOperatorTokens value) =>
        new(checked(value.Value - 1));

    /// <summary>The <c>true</c> operator, which only exists paired with <c>false</c>.</summary>
    public static bool operator true(LexOperatorTokens value) => value.Value != 0;

    /// <summary>The <c>false</c> operator.</summary>
    public static bool operator false(LexOperatorTokens value) => value.Value == 0;

    /// <summary>Multiplication.</summary>
    public static LexOperatorTokens operator *(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value * right.Value);

    /// <summary>The checked form of multiplication.</summary>
    public static LexOperatorTokens operator checked *(LexOperatorTokens left, LexOperatorTokens right) =>
        new(checked(left.Value * right.Value));

    /// <summary>Division.</summary>
    public static LexOperatorTokens operator /(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value / right.Value);

    /// <summary>The checked form of division.</summary>
    public static LexOperatorTokens operator checked /(LexOperatorTokens left, LexOperatorTokens right) =>
        new(checked(left.Value / right.Value));

    /// <summary>Remainder.</summary>
    public static LexOperatorTokens operator %(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value % right.Value);

    /// <summary>Bitwise and.</summary>
    public static LexOperatorTokens operator &(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value & right.Value);

    /// <summary>Bitwise or.</summary>
    public static LexOperatorTokens operator |(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value | right.Value);

    /// <summary>Bitwise exclusive or.</summary>
    public static LexOperatorTokens operator ^(LexOperatorTokens left, LexOperatorTokens right) =>
        new(left.Value ^ right.Value);

    /// <summary>Left shift.</summary>
    public static LexOperatorTokens operator <<(LexOperatorTokens value, int count) =>
        new(value.Value << count);

    /// <summary>Signed right shift, whose token a nested generic also closes.</summary>
    public static LexOperatorTokens operator >>(LexOperatorTokens value, int count) =>
        new(value.Value >> count);

    /// <summary>Unsigned right shift, a three-character token.</summary>
    public static LexOperatorTokens operator >>>(LexOperatorTokens value, int count) =>
        new(value.Value >>> count);

    /// <summary>Equality, which the compiler requires be paired with inequality.</summary>
    public static bool operator ==(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value == right.Value;

    /// <summary>Inequality.</summary>
    public static bool operator !=(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value != right.Value;

    /// <summary>Less than, which the compiler requires be paired with greater than.</summary>
    public static bool operator <(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value < right.Value;

    /// <summary>Greater than.</summary>
    public static bool operator >(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value > right.Value;

    /// <summary>Less than or equal.</summary>
    public static bool operator <=(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value <= right.Value;

    /// <summary>Greater than or equal.</summary>
    public static bool operator >=(LexOperatorTokens left, LexOperatorTokens right) =>
        left.Value >= right.Value;

    /// <summary>A widening conversion, whose metadata name is <c>op_Implicit</c>.</summary>
    public static implicit operator int(LexOperatorTokens value) => value.Value;

    /// <summary>A narrowing conversion, whose metadata name is <c>op_Explicit</c>.</summary>
    public static explicit operator short(LexOperatorTokens value) => (short)value.Value;

    /// <summary>The checked form of the narrowing conversion.</summary>
    public static explicit operator checked short(LexOperatorTokens value) =>
        checked((short)value.Value);

    /// <summary>A conversion *into* the type, so the operator's direction is both ways.</summary>
    public static explicit operator LexOperatorTokens(int value) => new(value);

    /// <summary>Overriding these keeps CS0660 and CS0661 out of the build.</summary>
    public override bool Equals(object? other) => other is LexOperatorTokens o && o.Value == Value;

    /// <summary>Overriding these keeps CS0660 and CS0661 out of the build.</summary>
    public override int GetHashCode() => Value;
}

/// <summary>
/// 6.4.6: the punctuator sequences a lexer has to re-split by context. Every field's type
/// or initialiser holds one of them.
/// </summary>
public sealed class LexPunctuators
{
    /// <summary>Two closing angle brackets that are not a right-shift token.</summary>
    public readonly List<List<int>> DoubleClose = [];

    /// <summary>Three closing angle brackets, which are not an unsigned right shift.</summary>
    public readonly List<List<List<int>>> TripleClose = [];

    /// <summary>A <c>?</c> that marks nullability, next to a <c>[</c> that does not.</summary>
    public readonly int[]? NullableArray;

    /// <summary>The conditional-access <c>?.</c> and the null-coalescing <c>??</c>.</summary>
    public int Coalesced => NullableArray?.Length ?? 0;

    /// <summary>The conditional-element-access <c>?[</c>.</summary>
    public int FirstOrZero => NullableArray?[0] ?? 0;

    /// <summary>The range <c>..</c>, whose two dots are one token and not two.</summary>
    public int[] Tail => (NullableArray ?? [])[1..];

    /// <summary>The index-from-end <c>^</c>, which is also the exclusive-or token.</summary>
    public int Last => (NullableArray ?? [0])[^1];

    /// <summary>The lambda arrow, and a null-coalescing assignment.</summary>
    public int Assigned()
    {
        int? held = null;
        held ??= 7;
        System.Func<int, int> twice = value => value * 2;
        return twice(held.Value);
    }

    /// <summary>The namespace-alias qualifier <c>::</c>, which is two colons and one token.</summary>
    public global::System.Type Qualified => typeof(global::System.Int32);
}
