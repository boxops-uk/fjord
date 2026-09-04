using System;
using System.Collections.Generic;

namespace Surface.Expressions.Operators.Anonymous;

/// <summary>
/// The outer variables of 12.21.6: which locals an anonymous function can see, which of
/// those it captures, and how many times the captured one is instantiated.
/// </summary>
public sealed class OpOuterVariables
{
    private int _field = 1;

    /// <summary>
    /// 12.21.6.1 — the outer variables of an anonymous function are the locals, parameters
    /// and <c>this</c> in scope where it is written. Each reference below crosses out of
    /// the lambda into the enclosing member.
    /// </summary>
    public Func<int> OuterVariablesInScope(int parameter)
    {
        int local = 2;

        return () => parameter + local + _field + this.Instance();
    }

    /// <summary>A member the lambda above reaches through a captured <c>this</c>.</summary>
    public int Instance() => _field * 10;

    /// <summary>
    /// 12.21.6.2 — a captured outer variable outlives the method that declared it, because
    /// the anonymous function holding it does. The local is read and written from inside.
    /// </summary>
    public static Func<int> CapturedAndMutated()
    {
        int counter = 0;

        return () =>
        {
            counter++;

            return counter;
        };
    }

    /// <summary>
    /// 12.21.6.2 — two anonymous functions capturing the same variable share one copy of
    /// it, so the two lambdas below observe each other's writes.
    /// </summary>
    public static (Action Increment, Func<int> Read) SharedCapture()
    {
        int shared = 0;

        return (() => shared++, () => shared);
    }

    /// <summary>
    /// 12.21.6.3 — a <c>for</c> loop declares its variable once, so every lambda made in
    /// the loop captures the same instantiation and they all see the final value.
    /// </summary>
    public static List<Func<int>> OneInstantiation()
    {
        var made = new List<Func<int>>();

        for (int index = 0; index < 3; index++)
        {
            made.Add(() => index);
        }

        return made;
    }

    /// <summary>
    /// 12.21.6.3 — a local declared inside the loop body is a fresh instantiation on every
    /// iteration, so each lambda captures its own.
    /// </summary>
    public static List<Func<int>> OneInstantiationPerIteration()
    {
        var made = new List<Func<int>>();

        for (int index = 0; index < 3; index++)
        {
            int perIteration = index;

            made.Add(() => perIteration);
        }

        return made;
    }

    /// <summary>
    /// 12.21.6.3 — the <c>foreach</c> iteration variable is itself a fresh instantiation
    /// per iteration, which is why this method needs no copy and the
    /// <see cref="OneInstantiation"/> loop does.
    /// </summary>
    public static List<Func<int>> ForeachInstantiation(IEnumerable<int> items)
    {
        var made = new List<Func<int>>();

        foreach (int item in items)
        {
            made.Add(() => item);
        }

        return made;
    }

    /// <summary>
    /// 12.21.6.3 — two loops in one method, each declaring a local named
    /// <c>perIteration</c> and each capturing it. Two declarations, one name, one
    /// containing member.
    /// </summary>
    public static List<Func<int>> RepeatedCapturedName()
    {
        var made = new List<Func<int>>();

        for (int index = 0; index < 2; index++)
        {
            int perIteration = index;

            made.Add(() => perIteration);
        }

        foreach (int index in new[] { 5, 6 })
        {
            int perIteration = index * 2;

            made.Add(() => perIteration);
        }

        return made;
    }

    /// <summary>
    /// 12.21.6.1 — a <c>static</c> lambda has no outer variables: it may not read the
    /// locals in scope where it is written, only its own parameters and static state.
    /// </summary>
    public static Func<int, int> NoOuterVariables()
    {
        int unreachableFromInside = 4;
        Func<int, int> pure = static value => value + 1;

        return value => pure(value) + unreachableFromInside;
    }
}
