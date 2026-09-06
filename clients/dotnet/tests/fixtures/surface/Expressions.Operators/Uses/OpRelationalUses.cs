using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>A class that declares no equality operator, so <c>==</c> on it is reference equality.</summary>
public sealed class OpTag
{
    /// <summary>Constructs a tag.</summary>
    public OpTag(string name) => Name = name;

    /// <summary>The tag's name.</summary>
    public string Name { get; }
}

/// <summary>
/// The relational, equality and type-testing operators of 12.14, one method per clause.
/// </summary>
public static class OpRelationalUses
{
    /// <summary>
    /// 12.14.1 — the general forms: <c>is</c>, <c>as</c>, and the <c>default</c> literal
    /// standing on one side of an equality.
    /// </summary>
    public static bool GeneralForms(object candidate, OpMoney money)
    {
        bool isType = candidate is string;
        string? asType = candidate as string;
        bool againstDefault = money == default;
        bool defaultOnLeft = default(OpMoney) == money;

        return isType && asType is not null && againstDefault && defaultOnLeft;
    }

    /// <summary>12.14.2 — integer comparison, over signed and unsigned operands.</summary>
    public static bool IntegerComparison(int i, long l, uint u, ulong ul) =>
        i < l && u > 1u && ul >= 1ul && i <= 2 && l != 0 && u == 1u;

    /// <summary>12.14.3 — floating-point comparison, including the NaN-unordered cases.</summary>
    public static bool FloatingPointComparison(float f, double d) =>
        f < d && d >= 0.0 && !(f > double.NaN) && f == f;

    /// <summary>12.14.4 — decimal comparison, which is its own set of operators.</summary>
    public static bool DecimalComparison(decimal left, decimal right) =>
        left < right || left >= right || left == right || left != right;

    /// <summary>12.14.5 — boolean equality.</summary>
    public static bool BooleanEquality(bool left, bool right) => left == right || left != right;

    /// <summary>12.14.6 — enumeration comparison, which runs on the underlying type.</summary>
    public static bool EnumerationComparison(OpChannel left, OpChannel right) =>
        left < right || left >= right || left == OpChannel.Both || right != OpChannel.None;

    /// <summary>
    /// 12.14.7 — reference type equality. The first comparison has no user-defined
    /// operator to find; the second forces the predefined one on a type that does declare
    /// <c>==</c>, by casting both operands to <c>object</c>. Two uses of the same token,
    /// one of which references <c>op_Equality</c> and one of which references nothing.
    /// </summary>
    public static bool ReferenceEquality(OpTag left, OpTag right, string? text)
    {
        bool byReference = left == right;
        bool againstNull = left != null;
        bool boxedPastTheOperator = (object?)text == (object?)"literal";

        return byReference || againstNull || boxedPastTheOperator;
    }

    /// <summary>12.14.8 — string equality, which is value equality on a reference type.</summary>
    public static bool StringEquality(string left, string? right) => left == right || left != "x";

    /// <summary>12.14.9 — delegate equality.</summary>
    public static bool DelegateEquality(Action left, Action? right) => left == right || left != null;

    /// <summary>
    /// 12.14.10 — equality between a nullable value type and the null literal, both for a
    /// predefined type and for a user-defined one whose <c>op_Equality</c> is lifted.
    /// </summary>
    public static bool NullableAgainstNull(int? number, OpMoney? money, OpFlag? flag) =>
        number == null || money != null || flag == null || number is null;

    /// <summary>
    /// 12.14.11 — tuple equality, which compares element by element. The second comparison
    /// runs the user-defined <c>op_Equality</c> of <see cref="OpMoney"/> once per element,
    /// from a single <c>==</c> token.
    /// </summary>
    public static bool TupleEquality((int Count, string Name) left, (int Count, string Name) right)
    {
        bool predefined = left == right;
        bool userDefinedElements = (new OpMoney(1), new OpMoney(2)) == (new OpMoney(1), new OpMoney(2));
        bool nested = (1, (2, 3)) != (1, (2, 4));

        return predefined && userDefinedElements && nested;
    }

    /// <summary>12.14.12.1 — the is-type operator, which declares nothing.</summary>
    public static bool IsType(object candidate) =>
        candidate is string || candidate is OpMoney || candidate is int[];

    /// <summary>
    /// 12.14.12.2 — the is-pattern operator, which declares the variables its patterns
    /// bind. Every name introduced here is a declaration inside an expression.
    /// </summary>
    public static string IsPattern(object candidate)
    {
        if (candidate is string text)
        {
            return text;
        }

        if (candidate is OpMoney { Minor: > 0 } positive)
        {
            return positive.ToString();
        }

        if (candidate is (int first, int second))
        {
            return $"{first}/{second}";
        }

        if (candidate is int[] { Length: > 1 } array)
        {
            if (array is [var head, .., var tail])
            {
                return $"{array.Length}:{head}:{tail}";
            }
        }

        return candidate is not null ? "other" : "none";
    }

    /// <summary>12.14.13 — the as operator, which never throws and yields null instead.</summary>
    public static (string? Text, OpTag? Tag, int? Boxed) AsOperator(object candidate) =>
        (candidate as string, candidate as OpTag, candidate as int?);
}
