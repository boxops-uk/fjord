using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// Every unary operator form of 12.9, used both on a predefined type — where the operator
/// resolves to nothing an index can name — and on a user-defined type, where the same
/// token is a reference to a declared member.
/// </summary>
public static class OpUnaryUses
{
    /// <summary>12.9.2 — unary plus, predefined and user-defined.</summary>
    public static (int Predefined, OpMoney UserDefined) UnaryPlus(int number, OpMoney money) =>
        (+number, +money);

    /// <summary>12.9.3 — unary minus, predefined and user-defined.</summary>
    public static (int Predefined, OpMoney UserDefined) UnaryMinus(int number, OpMoney money) =>
        (-number, -money);

    /// <summary>
    /// 12.9.3 — the same token inside a <c>checked</c> context, which selects
    /// <c>op_CheckedUnaryNegation</c> instead of <c>op_UnaryNegation</c>. The two uses are
    /// written identically; only the surrounding context tells them apart.
    /// </summary>
    public static OpMoney CheckedUnaryMinus(OpMoney money) => checked(-money);

    /// <summary>12.9.3 — and the explicitly unchecked form, for the contrast.</summary>
    public static OpMoney UncheckedUnaryMinus(OpMoney money) => unchecked(-money);

    /// <summary>12.9.4 — logical negation, predefined and user-defined.</summary>
    public static (bool Predefined, OpFlag UserDefined) LogicalNegation(bool flag, OpFlag tri) =>
        (!flag, !tri);

    /// <summary>12.9.5 — bitwise complement, predefined and user-defined.</summary>
    public static (int Predefined, OpBits UserDefined) BitwiseComplement(int mask, OpBits bits) =>
        (~mask, ~bits);

    /// <summary>
    /// 12.9.6 — the index-from-end operator. <c>^1</c> is a <see cref="Index"/>; on
    /// <see cref="OpSpan"/> the element access behind it references <c>Length</c> and the
    /// <c>int</c> indexer.
    /// </summary>
    public static (int FromArray, int FromSpan, Index Raw) IndexFromEnd(int[] items, OpSpan window)
    {
        Index last = ^1;

        return (items[^1], window[^2], last);
    }

    /// <summary>12.9.7 — prefix increment and decrement, predefined and user-defined.</summary>
    public static (int Number, OpMoney Money) PrefixIncrement(int number, OpMoney money)
    {
        ++number;
        --number;
        ++money;
        --money;

        return (number, money);
    }

    /// <summary>
    /// 12.8.16 — the postfix forms, which bind to the same <c>op_Increment</c> and
    /// <c>op_Decrement</c> declarations as the prefix forms above.
    /// </summary>
    public static (int Number, OpMoney Money) PostfixIncrement(int number, OpMoney money)
    {
        number++;
        number--;
        money++;
        money--;

        return (number, money);
    }

    /// <summary>
    /// 12.9.7 — increment inside a <c>checked</c> context, which reaches
    /// <c>op_CheckedIncrement</c>.
    /// </summary>
    public static OpMoney CheckedIncrement(OpMoney money) => checked(++money);

    /// <summary>
    /// 12.23.5 — increment on a type with a C# 14 instance increment operator, which
    /// selects <c>op_IncrementAssignment</c> over the static <c>op_Increment</c> that the
    /// same type also declares.
    /// </summary>
    public static OpTally InstanceIncrement(OpTally tally)
    {
        tally++;
        ++tally;
        tally--;

        return tally;
    }

    /// <summary>
    /// 12.9.8 — cast expressions: a predefined numeric conversion, a user-defined explicit
    /// conversion, its checked variant, a reference conversion, a boxing conversion and a
    /// cast that gives an anonymous function a target type.
    /// </summary>
    public static string Casts(double large, OpMoney money, object boxed)
    {
        int narrowed = (int)large;
        int fromMoney = (int)money;
        int checkedFromMoney = checked((int)money);
        string? text = (string?)boxed;
        object boxedNumber = (object)narrowed;
        Func<int> lambda = (Func<int>)(() => fromMoney);
        long? lifted = (long?)narrowed;

        return $"{narrowed}{fromMoney}{checkedFromMoney}{text}{boxedNumber}{lambda()}{lifted}";
    }
}
