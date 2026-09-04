// Clause 8.6 — expression tree types. An anonymous function converted to
// System.Linq.Expressions.Expression<TDelegate> is data rather than code: its parameters are
// still declarations and the members it names are still references, but nothing in it is
// ever called from the emitted body.

namespace Surface.Types;

/// <summary>
/// 8.6 — expression trees over each shape of delegate the clause admits.
/// </summary>
public static class TyExpressionTrees
{
    /// <summary>8.6 — the constructed expression tree type, from a lambda with one parameter.</summary>
    public static readonly System.Linq.Expressions.Expression<System.Func<int, int>> Increment =
        x => x + 1;

    /// <summary>
    /// 8.6 hazard — a second tree in the same type whose lambda parameter has the same name
    /// as the first's. Two parameter declarations named <c>x</c>, each inside an unnamed
    /// container.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<System.Func<int, int>> Decrement =
        x => x - 1;

    /// <summary>8.6 — a tree over a delegate with no parameters and no result.</summary>
    public static readonly System.Linq.Expressions.Expression<System.Action> DoNothing =
        () => System.GC.KeepAlive(null);

    /// <summary>8.6 — a tree over a delegate declared in this project (8.2.8).</summary>
    public static readonly System.Linq.Expressions.Expression<TyAdder> AddInTree =
        (left, right) => left + right;

    /// <summary>
    /// 8.6 — a tree that names a member of a corpus type. The reference to
    /// <see cref="TyAccount.Describe"/> is real, and the call it stands for never happens.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<System.Func<TyAccount, string>>
        DescribeInTree = account => account.Describe();

    /// <summary>8.6 — the non-generic base type, which every tree above converts to.</summary>
    public static System.Linq.Expressions.Expression AsBase => Increment;

    /// <summary>8.6 — a tree built by hand rather than from a lambda, so the same type is
    /// reached without any anonymous function at all.</summary>
    public static System.Linq.Expressions.Expression<System.Func<int, int>> BuiltByHand()
    {
        System.Linq.Expressions.ParameterExpression parameter =
            System.Linq.Expressions.Expression.Parameter(typeof(int), "x");
        System.Linq.Expressions.BinaryExpression body =
            System.Linq.Expressions.Expression.Multiply(
                parameter,
                System.Linq.Expressions.Expression.Constant(2));
        return System.Linq.Expressions.Expression.Lambda<System.Func<int, int>>(body, parameter);
    }

    /// <summary>8.6 — compiling a tree, which turns the data back into a delegate.</summary>
    public static int RunIncrement(int seed) => Increment.Compile()(seed);

    /// <summary>
    /// 8.6 — the same lambda body in both forms: as a delegate, where the body is code, and
    /// as an expression tree, where it is data. The two conversions are the clause's point.
    /// </summary>
    public static (int AsCode, string AsData) BothWays(int seed)
    {
        System.Func<int, int> asCode = x => x * 3;
        System.Linq.Expressions.Expression<System.Func<int, int>> asData = x => x * 3;
        return (asCode(seed), asData.Body.ToString());
    }
}
