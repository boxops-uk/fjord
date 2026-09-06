// Clause 13.10 — jump statements: 13.10.1 (what they have in common), 13.10.2 (`break`),
// 13.10.3 (`continue`) and 13.10.5 (`return`). `goto` is 13.10.4 and `throw` is 13.10.6; each
// has a file of its own, because each is a *reference* row and these four are not.
//
// None of these four is flagged as a hazard and none of them declares anything, which is the
// point: a jump statement is a syntax node with no name, no type and nothing for an index to
// hold except the fact that a walk descended into the statement containing it. What makes
// them worth writing is 13.10.1's rule about `finally`: a jump that leaves a `try` runs the
// `finally` blocks between it and its target *before* it goes, so the order in which a
// `return` and a `finally` execute is the reverse of the order they are written in.
// `ReturnThroughFinally` records that with a trail a reader can check.

using System;
using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.10 — the jumps that have no name to resolve.</summary>
public static class StmtJumps
{
    /// <summary>
    /// <c>break</c> in every statement it can leave.
    /// </summary>
    /// <remarks>
    /// 13.10.2. `break` leaves the nearest enclosing `switch`, `while`, `do`, `for` or
    /// `foreach` — and *only* the nearest, which is why leaving two loops at once needs the
    /// `goto` in `LabeledStatements.FindRow`. A `break` in a `switch` inside a loop leaves
    /// the switch and not the loop, which is the mistake the clause exists to forbid guessing
    /// about.
    /// </remarks>
    /// <param name="limit">How far to count.</param>
    /// <returns>A trail of which loops were left early.</returns>
    public static string EveryBreak(int limit)
    {
        List<string> trail = [];

        // 13.10.2 — out of a `while`.
        int spins = 0;
        while (true)
        {
            spins++;

            if (spins > limit)
            {
                trail.Add("while");
                break;
            }
        }

        // 13.10.2 — out of a `do`.
        int rolls = 0;
        do
        {
            rolls++;

            if (rolls > limit)
            {
                trail.Add("do");
                break;
            }
        }
        while (true);

        // 13.10.2 — out of a `for`.
        for (int step = 0; ; step++)
        {
            if (step > limit)
            {
                trail.Add("for");
                break;
            }
        }

        // 13.10.2 — out of a `foreach`.
        foreach (int index in new int[limit + 1])
        {
            if (index == limit)
            {
                trail.Add("foreach");
                break;
            }
        }

        // 13.10.2 — out of a `switch`, from inside a loop. This `break` ends the switch
        // section; the loop keeps going, and the `continue` below is what skips the rest of
        // the iteration.
        for (int step = 0; step <= limit; step++)
        {
            switch (step)
            {
                case 0:
                    trail.Add("switch");
                    break;

                default:
                    break;
            }
        }

        return string.Join(",", trail);
    }

    /// <summary>
    /// <c>continue</c> in every statement it can restart.
    /// </summary>
    /// <remarks>
    /// 13.10.3. `continue` goes to the *end point* of the nearest enclosing loop body, which
    /// in a `for` means the iterator still runs and in a `while` means the condition is
    /// tested again — so a `continue` in a `while` whose counter is incremented at the bottom
    /// of the body is an infinite loop, and the shapes below are all written to avoid being
    /// one.
    /// </remarks>
    /// <param name="limit">How far to count.</param>
    /// <returns>How many iterations were skipped.</returns>
    public static int EveryContinue(int limit)
    {
        int skipped = 0;

        // 13.10.3 — in a `while`, with the counter advanced before the `continue`.
        int spins = 0;
        while (spins < limit)
        {
            spins++;

            if (spins % 2 == 0)
            {
                skipped++;
                continue;
            }
        }

        // 13.10.3 — in a `do`, likewise.
        int rolls = 0;
        do
        {
            rolls++;

            if (rolls % 3 == 0)
            {
                skipped++;
                continue;
            }
        }
        while (rolls < limit);

        // 13.10.3 — in a `for`, where the iterator runs on the way out.
        for (int step = 0; step < limit; step++)
        {
            if (step % 2 == 1)
            {
                skipped++;
                continue;
            }
        }

        // 13.10.3 — in a `foreach`, which advances the enumerator on the way round.
        foreach (int index in new int[limit])
        {
            if (index == 0)
            {
                skipped++;
                continue;
            }
        }

        return skipped;
    }

    /// <summary>
    /// Every form <c>return</c> has.
    /// </summary>
    /// <remarks>
    /// 13.10.5. A `return` with no expression, in a void member; a `return` with one, in a
    /// value-returning member; and `return ref`, which returns a *variable* rather than a
    /// value and is the only form whose result can be assigned through by the caller. An
    /// iterator's `return` may not carry an expression at all — that is `yield break`, in
    /// `YieldStatements.cs`.
    /// </remarks>
    /// <param name="cells">Storage to return a reference into.</param>
    /// <param name="index">Which cell.</param>
    /// <returns>An alias for the chosen cell.</returns>
    public static ref int PickCell(int[] cells, int index)
    {
        // 13.10.5 — `return ref`, twice, so that the form appears in a branch as well as at
        // the end of a body.
        if (index < 0)
        {
            return ref cells[0];
        }

        return ref cells[index % cells.Length];
    }

    /// <summary>
    /// A void member whose <c>return</c> carries nothing.
    /// </summary>
    /// <remarks>
    /// 13.10.5. An early `return;` in a void member is a jump to the end point of the body,
    /// and the only jump statement that is spelled with a keyword and nothing else.
    /// </remarks>
    /// <param name="counter">Something to bump.</param>
    /// <param name="gate">Whether to bump it.</param>
    public static void ReturnNothing(StmtCounter counter, bool gate)
    {
        if (!gate)
        {
            return;
        }

        counter.Bump();
    }

    /// <summary>
    /// A <c>return</c> that leaves a <c>try</c>, and the <c>finally</c> that runs first.
    /// </summary>
    /// <remarks>
    /// 13.10.1, and the rule that makes jump statements more than syntax: the `finally` runs
    /// *between* the `return` and the caller, so the trail this method returns has "finally"
    /// in it even though the `return` is written above the `finally` block. The returned value
    /// is computed before the `finally` runs, which is why appending to the trail there is
    /// visible and reassigning a local would not be.
    /// </remarks>
    /// <param name="gate">Whether to return from inside the <c>try</c>.</param>
    /// <returns>A trail of what ran, in the order it ran.</returns>
    public static string ReturnThroughFinally(bool gate)
    {
        List<string> trail = [];

        try
        {
            trail.Add("try");

            if (gate)
            {
                // 13.10.1 — this `return` transfers control to the `finally` below, and only
                // then out of the method.
                return string.Join(",", trail) + "|then-finally-ran";
            }

            trail.Add("no-return");
        }
        finally
        {
            trail.Add("finally");
        }

        return string.Join(",", trail);
    }
}
