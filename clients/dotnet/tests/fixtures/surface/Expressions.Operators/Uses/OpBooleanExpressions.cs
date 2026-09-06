using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The boolean expressions of 12.26: every place the language requires a value of type
/// <c>bool</c> also accepts a value of a type that declares <c>operator true</c>.
/// </summary>
/// <remarks>
/// Each condition written on an <see cref="OpFlag"/> below is a reference to
/// <c>op_True</c>, and the source contains no name at all at that position — only the
/// expression whose value is tested.
/// </remarks>
public static class OpBooleanExpressions
{
    /// <summary>12.26 — the condition of an <c>if</c> statement.</summary>
    public static string IfCondition(OpFlag flag)
    {
        if (flag)
        {
            return "yes";
        }

        return "no";
    }

    /// <summary>12.26 — the condition of a <c>while</c> and of a <c>do</c>.</summary>
    public static int LoopConditions(OpFlag flag, int limit)
    {
        int count = 0;

        while (flag)
        {
            count++;

            if (count >= limit)
            {
                break;
            }
        }

        do
        {
            count--;

            if (count <= 0)
            {
                break;
            }
        }
        while (!flag);

        return count;
    }

    /// <summary>12.26 — the condition of a <c>for</c> statement.</summary>
    public static int ForCondition(OpFlag flag)
    {
        int total = 0;

        for (int i = 0; flag; i++)
        {
            total += i;

            if (i > 2)
            {
                break;
            }
        }

        return total;
    }

    /// <summary>12.26 / 12.20 — the condition of a conditional operator.</summary>
    public static int ConditionalCondition(OpFlag flag) => flag ? 1 : 0;

    /// <summary>
    /// 12.26 — a plain <c>bool</c> in the same positions, so the corpus holds both the
    /// case with a member reference behind it and the case with none.
    /// </summary>
    public static int PredefinedConditions(bool flag)
    {
        int total = flag ? 1 : 0;

        if (flag)
        {
            total++;
        }

        while (total < 3)
        {
            total++;
        }

        return total;
    }
}
