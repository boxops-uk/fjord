// Clause 13.10.4 — the goto statement.
//
// `goto` is the only reference in the language whose target is a *statement*. Its three forms
// name their target three different ways: `goto identifier` by a name that a labeled statement
// declared (13.5), `goto case constant` by a *value* that appears in a case label, and `goto
// default` by a keyword. Only the first of the three writes a name a resolver could look up.
//
// The hazard is the pair in `TwoTargets`: two `goto Retry` statements in one member, binding
// to two different labels that are both called `Retry`. A reference edge here has to carry
// which block it was written in, because the name alone identifies two declarations. This is
// the reference-side twin of the declaration hazard in `LabeledStatements.cs`.

using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.10.4 — the three forms of <c>goto</c>.</summary>
public static class StmtGotos
{
    /// <summary>
    /// Two <c>goto Retry</c> statements, binding to two different labels.
    /// </summary>
    /// <remarks>
    /// 13.10.4, and its hazard. Each `goto` binds to the `Retry` declared in its own block —
    /// the labels are two declarations (13.5) and these are two references, and every one of
    /// the four is spelled `Retry`.
    /// </remarks>
    /// <param name="budget">How far to count in each block.</param>
    /// <returns>A trail naming which block each pass ran in.</returns>
    public static string TwoTargets(int budget)
    {
        List<string> trail = [];

        {
            Retry:
            trail.Add("first");

            if (trail.Count < budget)
            {
                // 13.10.4 — binds to this block's `Retry`, three lines up.
                goto Retry;
            }
        }

        {
            Retry:
            trail.Add("second");

            if (trail.Count < budget * 2)
            {
                // 13.10.4 — binds to *this* block's `Retry`, which is a different
                // declaration with the same name.
                goto Retry;
            }
        }

        return string.Join(",", trail);
    }

    /// <summary>
    /// <c>goto case</c> and <c>goto default</c>, which name their targets by value.
    /// </summary>
    /// <remarks>
    /// 13.10.4 with 13.8.3. `goto case 2;` does not refer to an identifier: it refers to
    /// whichever case label of the enclosing switch has the constant value 2, so the
    /// reference is resolved by *constant equality* and there is nothing at the use site for a
    /// name-based resolver to match. `goto default` refers to the default label the same way,
    /// by keyword. Both may jump backwards to a section already written, and `case 4` below is
    /// reached only that way.
    /// </remarks>
    /// <param name="gate">Which section to enter first.</param>
    /// <returns>A trail of the sections visited, in order.</returns>
    public static string ByValue(int gate)
    {
        List<string> trail = [];

        switch (gate)
        {
            case 1:
                trail.Add("one");
                goto case 2;

            case 2:
                trail.Add("two");
                goto case 4;

            case 3:
                trail.Add("three");
                goto default;

            case 4:
                trail.Add("four");
                break;

            default:
                trail.Add("default");
                goto case 4;
        }

        return string.Join(",", trail);
    }

    /// <summary>
    /// A <c>goto</c> that leaves a <c>try</c>, and one that leaves two loops.
    /// </summary>
    /// <remarks>
    /// 13.10.4 with 13.10.1. Jumping out of a `try` runs its `finally` on the way, exactly as
    /// a `return` does. Jumping *into* a block is not allowed — CS0159 — so a label's
    /// reachability from a `goto` is a real question with a real answer, and the direction
    /// that fails cannot be in the corpus. The two shapes below are the two that succeed:
    /// outward out of a `try`, and outward out of nested loops.
    /// </remarks>
    /// <param name="grid">Rows to search.</param>
    /// <param name="needle">What to look for.</param>
    /// <returns>A trail of what ran and what was found.</returns>
    public static string OutOfEverything(int[][] grid, int needle)
    {
        List<string> trail = [];

        try
        {
            trail.Add("try");

            for (int row = 0; row < grid.Length; row++)
            {
                for (int column = 0; column < grid[row].Length; column++)
                {
                    if (grid[row][column] == needle)
                    {
                        trail.Add($"found {row}");

                        // 13.10.4 — out of two loops and out of the `try`, in one statement.
                        // The `finally` runs before the label is reached.
                        goto Found;
                    }
                }
            }

            trail.Add("exhausted");
        }
        finally
        {
            trail.Add("finally");
        }

        Found:
        return string.Join(",", trail);
    }
}
