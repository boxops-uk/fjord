using System;
using Surface.Expressions.Operators.Declarations;
using Surface.Expressions.Operators.Uses;

namespace Surface.Expressions.Operators.Assignment;

/// <summary>
/// The compound assignment of 12.23.5. <c>x op= y</c> is evaluated as <c>x = x op y</c>,
/// so the token names a binary operator and an assignment at once — and since C# 14 it may
/// instead name a single instance operator declared for the purpose.
/// </summary>
public static class OpCompoundAssignment
{
    /// <summary>
    /// 12.23.5 — every predefined compound assignment, over the operand types each one
    /// applies to.
    /// </summary>
    public static string EveryPredefinedForm(int number, string text, bool flag)
    {
        number += 1;
        number -= 2;
        number *= 3;
        number /= 4;
        number %= 5;
        number &= 6;
        number |= 7;
        number ^= 8;
        number <<= 1;
        number >>= 1;
        number >>>= 1;

        text += "-suffix";

        flag &= true;
        flag |= false;
        flag ^= true;

        return $"{number}{text}{flag}";
    }

    /// <summary>
    /// 12.23.5 — compound assignment on an enumeration, which is how a flag is added to or
    /// removed from a set.
    /// </summary>
    public static OpChannel OnAnEnumeration(OpChannel channel)
    {
        channel |= OpChannel.Left;
        channel &= ~OpChannel.Right;
        channel ^= OpChannel.Both;

        return channel;
    }

    /// <summary>
    /// 12.23.5 / 12.23.6's neighbour — compound assignment on a delegate variable, which
    /// combines and removes handlers. On a variable this is 12.23.5; on an event it is
    /// 12.23.6, and the source looks the same.
    /// </summary>
    public static Action? OnADelegate(Action first, Action second)
    {
        Action? combined = first;

        combined += second;
        combined -= first;

        return combined;
    }

    /// <summary>
    /// 12.23.5 — compound assignment on a type whose operator is a static binary one, so
    /// the token is rewritten into <c>op_Addition</c> plus a simple assignment.
    /// </summary>
    public static OpMoney ViaAStaticOperator(OpMoney money)
    {
        money += money;
        money -= new OpMoney(1);
        money *= 2L;
        money /= 2L;
        money %= 7L;

        return money;
    }

    /// <summary>
    /// 12.23.5 — the same three characters on a type that declares a C# 14 instance
    /// compound assignment operator, which is selected in preference to the static binary
    /// one the type also declares. Two declarations, one token, and the choice between
    /// them is invisible in the source.
    /// </summary>
    public static OpTally ViaAnInstanceOperator(OpTally tally, OpTally other)
    {
        tally += other;
        tally -= other;
        tally *= 3;

        return tally;
    }

    /// <summary>
    /// 12.23.5 — compound assignment on the user-defined shift operators, including the
    /// relaxed one whose right operand is a <c>string</c>.
    /// </summary>
    public static OpBits OnShiftOperators(OpBits bits)
    {
        bits <<= 2;
        bits >>= 1;
        bits >>>= 1;
        bits <<= "3";

        return bits;
    }

    /// <summary>
    /// 12.23.5 — compound assignment to targets that are not locals: a field, a property,
    /// an array element, an indexer element and a variable reached through a ref local. The
    /// target is evaluated once, which is the clause's substantive rule.
    /// </summary>
    public static int EveryTargetKind(OpTarget target, int[] items)
    {
        target.Field += 1;
        target.Property += 2;
        items[0] += 3;
        target["slot"] += 4;

        ref int alias = ref items[1];

        alias += 5;

        return target.Field + target.Property + items[0] + items[1] + target["slot"];
    }

    /// <summary>
    /// 12.23.5 — a compound assignment that needs a conversion of its result back to the
    /// target's type, which the clause allows only for the predefined operators.
    /// </summary>
    public static (byte Narrow, char Advanced) WithImplicitNarrowing(byte narrow, char letter)
    {
        narrow += 1;
        letter += (char)1;

        return (narrow, letter);
    }

    /// <summary>
    /// 12.23.5 — compound assignment inside a <c>checked</c> context, which selects the
    /// checked operator for the binary half of the rewrite.
    /// </summary>
    public static OpMoney CheckedCompound(OpMoney money)
    {
        checked
        {
            money += money;
        }

        return money;
    }
}
