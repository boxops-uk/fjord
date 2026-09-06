// Clauses 15.10.1 (operators — general) and 15.10.2 (unary operators). An operator declaration
// is a function member whose *name is a token*: `public static MemTally operator -(MemTally t)`
// declares a method the source calls `-` and metadata calls `op_UnaryNegation`. So this is the
// one clause where the identifier an index holds and the identifier a reader types have nothing
// in common, and where a reference to the member contains no name at all — `-tally` is a call.
//
// Three hazards, all written below.
//
//   1. **One token, two members.** `operator -` is declared twice in `MemTally`: once unary and
//      once binary (in `MemBinaryOperators` the same pair appears again). The two are
//      `op_UnaryNegation` and `op_Subtraction`, so the emitted names differ — but the *source*
//      name is the same token, and an index keyed on what the source says has one name and two
//      declarations, separated by parameter count alone.
//   2. **`checked` is part of the name and not of the signature.** `operator -(MemTally)` and
//      `operator checked -(MemTally)` have identical signatures: same token, same parameter
//      list, same return type. Only the `checked` keyword separates them, and it separates them
//      into `op_UnaryNegation` and `op_CheckedUnaryNegation`.
//   3. **C# 14 instance increment beside the static one.** `MemTally` declares `operator ++`
//      twice: the classic `static MemTally operator ++(MemTally)` and the new
//      `void operator ++()`, which is an *instance* member. Two declarations, one token, and one
//      is static and one is not — `op_Increment` and `op_IncrementAssignment`.
//
// A `checked` operator is only ever selected inside a `checked` context, so `MemTallyUse` has
// both a checked and an unchecked reference to each pair: the two references differ in nothing
// that appears at the call site.

namespace Surface.Classes.Members.Operators;

/// <summary>15.10.2: every overloadable unary operator, on one value type.</summary>
public struct MemTally
{
    /// <summary>What is being tallied.</summary>
    public int Count;

    /// <summary>Starts a tally at a count.</summary>
    public MemTally(int count) => Count = count;

    /// <summary>15.10.2: unary plus, which is the identity and is still a declared
    /// member — `op_UnaryPlus`.</summary>
    public static MemTally operator +(MemTally tally) => tally;

    /// <summary>15.10.2: unary minus — `op_UnaryNegation`. Same token as the binary minus in
    /// <see cref="MemLedger"/>, and a different member.</summary>
    public static MemTally operator -(MemTally tally) => new MemTally(-tally.Count);

    /// <summary>15.10.2: the checked form of unary minus — `op_CheckedUnaryNegation`. Its
    /// signature is identical to the one above; the keyword is the whole difference.</summary>
    public static MemTally operator checked -(MemTally tally) => new MemTally(checked(-tally.Count));

    /// <summary>15.10.2: logical negation — `op_LogicalNot`. Its result need not be
    /// <c>bool</c>, and here it is not.</summary>
    public static MemTally operator !(MemTally tally) => new MemTally(tally.Count == 0 ? 1 : 0);

    /// <summary>15.10.2: bitwise complement — `op_OnesComplement`.</summary>
    public static MemTally operator ~(MemTally tally) => new MemTally(~tally.Count);

    /// <summary>15.10.2: the classic increment — `op_Increment`. Static, takes its operand and
    /// returns the new value, which is why `tally++` on a value type works at all.</summary>
    public static MemTally operator ++(MemTally tally) => new MemTally(tally.Count + 1);

    /// <summary>15.10.2: the classic decrement — `op_Decrement`.</summary>
    public static MemTally operator --(MemTally tally) => new MemTally(tally.Count - 1);

    /// <summary>15.10.2 in its C# 14 spelling: an *instance* increment —
    /// `op_IncrementAssignment`. It mutates in place and returns nothing, and it is preferred
    /// over the static one when the operand is a variable. Two members, one token.</summary>
    public void operator ++() => Count += 1;

    /// <summary>15.10.2 in its C# 14 spelling: the instance decrement —
    /// `op_DecrementAssignment`.</summary>
    public void operator --() => Count -= 1;

    /// <summary>15.10.2: the `true` operator — `op_True`. It may only be declared together with
    /// `false`, so this pair is the one place in the language where two members are required to
    /// arrive together.</summary>
    public static bool operator true(MemTally tally) => tally.Count > 0;

    /// <summary>15.10.2: the `false` operator — `op_False`.</summary>
    public static bool operator false(MemTally tally) => tally.Count <= 0;

    /// <summary>15.10.1: a readable form, so the type is not only operators.</summary>
    public override string ToString() => $"tally {Count}";
}

/// <summary>15.10.2: the references. Every one of these is a call to a member whose name is a
/// token, and the `checked` block selects a different member with the same call syntax.</summary>
public static class MemTallyUse
{
    /// <summary>15.10.2: the unchecked references — one per operator.</summary>
    public static string Unchecked()
    {
        var tally = new MemTally(3);
        var plus = +tally;
        var minus = -tally;
        var not = !tally;
        var complement = ~tally;
        var post = tally++;
        var pre = --tally;
        var truthy = tally ? 1 : 0;
        return $"{plus} {minus} {not} {complement} {post} {pre} {truthy} {tally}";
    }

    /// <summary>15.10.2: the same expressions in a `checked` context, which binds
    /// `-tally` to `op_CheckedUnaryNegation` instead. The two call sites are spelled
    /// identically.</summary>
    public static string Checked()
    {
        var tally = new MemTally(3);

        checked
        {
            var minus = -tally;
            return $"{minus}";
        }
    }

    /// <summary>15.10.2: the C# 14 instance increment, which is what `++` on a *variable* of
    /// this type actually calls. The static one is still reachable through a value that is not a
    /// variable.</summary>
    public static string InPlace()
    {
        var tally = new MemTally(1);
        tally++;
        tally--;
        ++tally;
        var fromValue = new MemTally(1) is var seed ? seed++ : seed;
        return $"{tally} {fromValue}";
    }
}
