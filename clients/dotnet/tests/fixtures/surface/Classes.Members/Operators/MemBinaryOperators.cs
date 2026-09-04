// Clause 15.10.3 (binary operators). Every overloadable binary token, plus the two families
// that make the clause interesting for identity: the `checked` forms, whose signature is
// identical to the unchecked one, and C# 14's user-defined compound assignment operators, which
// are *instance* members with no static counterpart in the same shape.
//
// The comparison operators are the other reason this file exists as a pair of types. `==` and
// `!=` must be declared together, and so must `<`/`>` and `<=`/`>=`, so clause 15.10.3 has
// three sets of members that the compiler refuses to accept singly (CS0216) — the only
// co-declaration requirement in the member surface besides `true`/`false`. Declaring `==` also
// obliges `Equals` and `GetHashCode` (CS0660, CS0661), so one operator declaration drags two
// overrides in with it.
//
// The hazard, stated as a pair of rows a query must separate:
//
//   * `operator +(MemLedger, MemLedger)` and `operator checked +(MemLedger, MemLedger)` —
//     same token, same parameters, same return type, two members.
//   * `operator +(MemLedger, MemLedger)` and `void operator +=(MemLedger)` — one is static and
//     binary, the other is an instance member with one parameter, and `a += b` may bind to
//     either depending on whether the left operand is a variable.

namespace Surface.Classes.Members.Operators;

/// <summary>15.10.3: the arithmetic, bitwise and shift operators, with their checked forms and
/// their C# 14 compound-assignment forms.</summary>
public struct MemLedger
{
    /// <summary>The running total.</summary>
    public int Total;

    /// <summary>Opens a ledger at a total.</summary>
    public MemLedger(int total) => Total = total;

    /// <summary>15.10.3: addition — `op_Addition`.</summary>
    public static MemLedger operator +(MemLedger left, MemLedger right)
        => new MemLedger(left.Total + right.Total);

    /// <summary>15.10.3: the checked form of addition — `op_CheckedAddition`. Identical
    /// signature to the above.</summary>
    public static MemLedger operator checked +(MemLedger left, MemLedger right)
        => new MemLedger(checked(left.Total + right.Total));

    /// <summary>15.10.3: subtraction — `op_Subtraction`. The same token as the unary minus on
    /// <see cref="MemTally"/>, one parameter longer.</summary>
    public static MemLedger operator -(MemLedger left, MemLedger right)
        => new MemLedger(left.Total - right.Total);

    /// <summary>15.10.3: multiplication — `op_Multiply`.</summary>
    public static MemLedger operator *(MemLedger left, int factor)
        => new MemLedger(left.Total * factor);

    /// <summary>15.10.3: division — `op_Division`.</summary>
    public static MemLedger operator /(MemLedger left, int divisor)
        => new MemLedger(left.Total / divisor);

    /// <summary>15.10.3: remainder — `op_Modulus`.</summary>
    public static MemLedger operator %(MemLedger left, int divisor)
        => new MemLedger(left.Total % divisor);

    /// <summary>15.10.3: bitwise and — `op_BitwiseAnd`.</summary>
    public static MemLedger operator &(MemLedger left, MemLedger right)
        => new MemLedger(left.Total & right.Total);

    /// <summary>15.10.3: bitwise or — `op_BitwiseOr`.</summary>
    public static MemLedger operator |(MemLedger left, MemLedger right)
        => new MemLedger(left.Total | right.Total);

    /// <summary>15.10.3: exclusive or — `op_ExclusiveOr`.</summary>
    public static MemLedger operator ^(MemLedger left, MemLedger right)
        => new MemLedger(left.Total ^ right.Total);

    /// <summary>15.10.3: left shift — `op_LeftShift`. The right operand of a shift need not be
    /// <c>int</c> any more, and here it is.</summary>
    public static MemLedger operator <<(MemLedger left, int places)
        => new MemLedger(left.Total << places);

    /// <summary>15.10.3: right shift — `op_RightShift`.</summary>
    public static MemLedger operator >>(MemLedger left, int places)
        => new MemLedger(left.Total >> places);

    /// <summary>15.10.3: unsigned right shift — `op_UnsignedRightShift`, a third shift token
    /// that shares nothing with the other two but its shape.</summary>
    public static MemLedger operator >>>(MemLedger left, int places)
        => new MemLedger(left.Total >>> places);

    /// <summary>15.10.3 in its C# 14 spelling: compound addition —
    /// `op_AdditionAssignment`. An instance member with one parameter and no return, chosen over
    /// `op_Addition` when the target is a variable.</summary>
    public void operator +=(MemLedger right) => Total += right.Total;

    /// <summary>15.10.3: the checked form of compound addition —
    /// `op_CheckedAdditionAssignment`.</summary>
    public void operator checked +=(MemLedger right) => Total = checked(Total + right.Total);

    /// <summary>15.10.3: compound subtraction — `op_SubtractionAssignment`.</summary>
    public void operator -=(MemLedger right) => Total -= right.Total;

    /// <summary>15.10.1: a readable form.</summary>
    public override string ToString() => $"ledger {Total}";
}

/// <summary>15.10.3: the comparison operators, which the language requires in pairs. Splitting
/// them off keeps `MemLedger` free of the `Equals`/`GetHashCode` overrides that `==` forces.</summary>
public readonly struct MemRank
{
    /// <summary>The position.</summary>
    public int Place { get; }

    /// <summary>Fixes a rank at a place.</summary>
    public MemRank(int place) => Place = place;

    /// <summary>15.10.3: equality — `op_Equality`. Declaring it without `!=` is CS0216.</summary>
    public static bool operator ==(MemRank left, MemRank right) => left.Place == right.Place;

    /// <summary>15.10.3: inequality — `op_Inequality`.</summary>
    public static bool operator !=(MemRank left, MemRank right) => left.Place != right.Place;

    /// <summary>15.10.3: less than — `op_LessThan`. Its pair is `&gt;`.</summary>
    public static bool operator <(MemRank left, MemRank right) => left.Place < right.Place;

    /// <summary>15.10.3: greater than — `op_GreaterThan`.</summary>
    public static bool operator >(MemRank left, MemRank right) => left.Place > right.Place;

    /// <summary>15.10.3: less than or equal — `op_LessThanOrEqual`. Its pair is `&gt;=`.</summary>
    public static bool operator <=(MemRank left, MemRank right) => left.Place <= right.Place;

    /// <summary>15.10.3: greater than or equal — `op_GreaterThanOrEqual`.</summary>
    public static bool operator >=(MemRank left, MemRank right) => left.Place >= right.Place;

    /// <summary>15.10.3: the override `==` obliges (CS0660), which is a member the operator
    /// declaration pulled into this type.</summary>
    public override bool Equals(object? other) => other is MemRank rank && rank.Place == Place;

    /// <summary>15.10.3: the override `==` also obliges (CS0661).</summary>
    public override int GetHashCode() => Place;
}

/// <summary>15.10.3: the references. Every binary operator above is invoked here, the `checked`
/// ones twice — once in each context, with the same spelling.</summary>
public static class MemBinaryOperatorUse
{
    /// <summary>15.10.3: one reference per token.</summary>
    public static string Arithmetic()
    {
        var left = new MemLedger(6);
        var right = new MemLedger(3);
        return $"{left + right} {left - right} {left * 2} {left / 2} {left % 4} " +
               $"{left & right} {left | right} {left ^ right} {left << 1} {left >> 1} {left >>> 1}";
    }

    /// <summary>15.10.3: the compound forms, which bind to the instance members because the
    /// target is a variable.</summary>
    public static string Compound()
    {
        var ledger = new MemLedger(1);
        ledger += new MemLedger(2);
        ledger -= new MemLedger(1);

        checked
        {
            ledger += new MemLedger(4);
        }

        return $"{ledger}";
    }

    /// <summary>15.10.3: the checked binary operator, selected by context alone.</summary>
    public static string CheckedArithmetic()
    {
        var left = new MemLedger(6);
        var right = new MemLedger(3);

        checked
        {
            return $"{left + right}";
        }
    }

    /// <summary>15.10.3: all six comparisons, and the two overrides they obliged.</summary>
    public static string Compare()
    {
        var first = new MemRank(1);
        var second = new MemRank(2);
        return $"{first == second} {first != second} {first < second} {first > second} " +
               $"{first <= second} {first >= second} {first.Equals(second)} {first.GetHashCode()}";
    }
}
