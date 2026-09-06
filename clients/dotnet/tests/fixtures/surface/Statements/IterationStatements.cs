// Clause 13.9 — iteration statements: 13.9.1 (there are four of them), 13.9.2 (`while`),
// 13.9.3 (`do`) and 13.9.4 (`for`). The fourth, `foreach`, is 13.9.5 and has three files of
// its own.
//
// 13.9.4 is the only one of the three here that *declares*: a `for` initializer may be a local
// variable declaration whose scope is the whole `for` statement — condition, iterator and
// embedded statement included, and nothing outside. That scope is the hazard: two `for` loops
// in one member each declaring `i` is the most common repeated-name shape in any C# codebase,
// and `TwoCounters` is it, written on purpose, with the two `i`s at different types so a merge
// is visible.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.9 — the three iteration statements that are not <c>foreach</c>.</summary>
public static class StmtIteration
{
    /// <summary>
    /// All four iteration statements, in one member.
    /// </summary>
    /// <remarks>
    /// 13.9.1. The clause's content is the list, and this is the list: `while` tests before,
    /// `do` tests after, `for` carries its own initializer and iterator, and `foreach` binds
    /// an enumerator. The differences are in *when the condition runs*, which is flow and not
    /// syntax — so the four forms below are deliberately equivalent, and the only thing
    /// separating them is which statement was written.
    /// </remarks>
    /// <param name="limit">How far each loop counts.</param>
    /// <returns>Four totals added together.</returns>
    public static int AllFour(int limit)
    {
        int total = 0;

        int spins = 0;
        while (spins < limit)
        {
            total += spins;
            spins++;
        }

        int rolls = 0;
        do
        {
            total += rolls;
            rolls++;
        }
        while (rolls < limit);

        for (int step = 0; step < limit; step++)
        {
            total += step;
        }

        foreach (int index in new int[limit])
        {
            total += index;
        }

        return total;
    }

    /// <summary>
    /// Every shape the <c>while</c> statement has.
    /// </summary>
    /// <remarks>
    /// 13.9.2. A condition that is a call; a condition that declares, through a pattern; a
    /// `while (true)` whose exit is a `break`, which is the shape that gives the statement no
    /// reachable end point (13.2); and a body that is a single embedded statement.
    /// </remarks>
    /// <param name="limit">How far to count.</param>
    /// <returns>A total over all the loops.</returns>
    public static int EveryWhileShape(int limit)
    {
        int total = 0;
        int spins = 0;

        // 13.9.2 — the ordinary shape.
        while (spins < limit)
        {
            total += 1;
            spins++;
        }

        // 13.9.2 — a condition whose value comes from a call, so the condition is a
        // reference and not just a comparison.
        while (StillGoing(ref spins, limit * 2))
        {
            total += 2;
        }

        // 13.9.2 — a condition that declares. The pattern variable is scoped to the enclosing
        // block, so `slice` is in scope after the loop as well as inside it.
        object? boxed = limit;
        while (boxed is int slice && slice > 0)
        {
            total += slice;
            boxed = slice - limit;
        }

        // 13.9.2 — `while (true)`, exited by a `break` (13.10.2).
        while (true)
        {
            total += 1;

            if (total > limit * 4)
            {
                break;
            }
        }

        return total;
    }

    /// <summary>
    /// Every shape the <c>do</c> statement has.
    /// </summary>
    /// <remarks>
    /// 13.9.3. The body runs before the condition is tested even once, which is the whole
    /// difference from `while` and is why `AtLeastOnce` returns 1 for a limit of 0. The
    /// trailing semicolon after the condition is part of the statement, unlike `while`'s.
    /// </remarks>
    /// <param name="limit">How far to count.</param>
    /// <returns>How many times the body ran.</returns>
    public static int AtLeastOnce(int limit)
    {
        int runs = 0;

        // 13.9.3 — the body runs, then the condition is tested.
        do
        {
            runs++;
        }
        while (runs < limit);

        // 13.9.3 — a `do` with an unbraced embedded statement and a declaring condition.
        object? boxed = limit;
        do
            runs++;
        while (boxed is int slice && runs < slice);

        return runs;
    }

    /// <summary>
    /// Two <c>for</c> loops, each declaring <c>i</c>.
    /// </summary>
    /// <remarks>
    /// 13.9.4, and its hazard. The scope of a `for` initializer's declaration is the `for`
    /// statement, so the two `i`s below are two declarations that never coexist — and they
    /// have different types, so an index that merges them has to pick one.
    /// </remarks>
    /// <param name="limit">How far each loop counts.</param>
    /// <returns>A total that touches both loops.</returns>
    public static int TwoCounters(int limit)
    {
        int total = 0;

        // 13.9.4 — `i` is an int here.
        for (int i = 0; i < limit; i++)
        {
            total += i;
        }

        // 13.9.4 — `i` is a long here. Same identifier, same containing member, same
        // statement form, and no scope in common.
        for (long i = 0; i < limit; i += 2)
        {
            total += (int)i;
        }

        return total;
    }

    /// <summary>
    /// Every part of a <c>for</c> statement, present and absent.
    /// </summary>
    /// <remarks>
    /// 13.9.4. The initializer may be a declaration *or* a statement-expression list, and the
    /// two are different productions: one declares and one does not. Every one of the three
    /// clauses may be omitted, and `for (;;)` with all three gone is the loop with no
    /// condition, which has no reachable end point in the sense of 13.2. A declaration
    /// initializer may declare more than one variable, and the iterator may be a list.
    /// </remarks>
    /// <param name="limit">How far to count.</param>
    /// <returns>A total over every loop.</returns>
    public static int EveryForShape(int limit)
    {
        int total = 0;

        // 13.9.4 — a declaration initializer with two declarators, and an iterator with two
        // expressions. `low` and `high` are one declaration statement and two declarations,
        // both scoped to this `for`.
        for (int low = 0, high = limit; low < high; low++, high--)
        {
            total += high - low;
        }

        // 13.9.4 — an initializer that is a statement-expression list rather than a
        // declaration. `outer` is declared before the loop, so this `for` declares nothing.
        int outer;
        for (outer = 0; outer < limit; outer++)
        {
            total += 1;
        }

        // 13.9.4 — no initializer and no iterator, only a condition.
        for (; outer > 0;)
        {
            outer--;
            total += 1;
        }

        // 13.9.4 — all three clauses omitted. The condition is taken to be `true`, so the
        // only way out is a jump.
        for (;;)
        {
            total += 1;

            if (total > limit * 4)
            {
                break;
            }
        }

        return total;
    }

    private static bool StillGoing(ref int spins, int limit)
    {
        spins++;
        return spins < limit;
    }
}
