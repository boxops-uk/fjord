using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The null coalescing operator (12.17), the throw expression (12.18), declaration
/// expressions (12.19) and the conditional operator (12.20).
/// </summary>
public static class OpNullAndConditionalUses
{
    /// <summary>
    /// 12.17 — null coalescing over a reference type, a nullable value type and a chain,
    /// plus the null-coalescing assignment form.
    /// </summary>
    public static (string Text, int Number, string Chained) NullCoalescing(
        string? first,
        string? second,
        int? maybe)
    {
        string text = first ?? "fallback";
        int number = maybe ?? -1;
        string chained = first ?? second ?? "fallback";

        // 12.17 / 12.23.1 — null-coalescing assignment, which assigns only when null.
        first ??= "assigned";
        maybe ??= 0;

        return (text + first, number + maybe.Value, chained);
    }

    /// <summary>
    /// 12.18 — the throw expression, in each position the clause allows: the right operand
    /// of <c>??</c>, an arm of <c>?:</c>, and the body of an expression-bodied member.
    /// </summary>
    public static string ThrowExpression(string? text, bool ok) =>
        ok
            ? text ?? throw new ArgumentNullException(nameof(text))
            : throw new InvalidOperationException("not ok");

    /// <summary>12.18 — a lambda whose whole body is a throw expression.</summary>
    public static Func<int, int> ThrowingLambda() => value => throw new NotSupportedException();

    /// <summary>
    /// 12.19 — declaration expressions: an <c>out</c> argument that declares its own
    /// variable, both explicitly typed and with <c>var</c>, and a discard in the same
    /// position.
    /// </summary>
    /// <remarks>
    /// The two blocks below each declare a variable named <c>parsed</c>. They are two
    /// declarations with one name in one method body, distinguished only by scope, which is
    /// the shape an index keyed on (containing method, name) collapses.
    /// </remarks>
    public static int DeclarationExpressions(string first, string second)
    {
        int total = 0;

        {
            if (int.TryParse(first, out int parsed))
            {
                total += parsed;
            }
        }

        {
            if (int.TryParse(second, out var parsed))
            {
                total += parsed;
            }
        }

        if (int.TryParse(first, out _))
        {
            total++;
        }

        return total;
    }

    /// <summary>
    /// 12.20 — the conditional operator: a boolean condition, a target-typed conditional
    /// whose arms have no common type of their own, and a condition of a user-defined type
    /// that supplies <c>operator true</c>.
    /// </summary>
    public static (int Simple, long TargetTyped, string UserDefinedCondition) ConditionalOperator(
        bool flag,
        OpFlag tri,
        int small)
    {
        int simple = flag ? small : -small;
        long targetTyped = flag ? 1 : small;
        string userDefinedCondition = tri ? "yes" : "no";

        return (simple, targetTyped, userDefinedCondition);
    }

    /// <summary>
    /// 12.20 — the ref conditional, whose arms are variables rather than values, so the
    /// whole expression is a variable and can be the target of an assignment.
    /// </summary>
    public static int RefConditional(ref int left, ref int right, bool takeLeft)
    {
        ref int chosen = ref takeLeft ? ref left : ref right;
        chosen = 99;

        return left + right;
    }

    /// <summary>
    /// 12.20 — a conditional whose arms are themselves operator expressions on a
    /// user-defined type, so one <c>?:</c> guards two <c>op_Addition</c> references.
    /// </summary>
    public static OpMoney NestedConditional(OpMoney money, bool grow) =>
        grow ? money + money : money - money;
}
