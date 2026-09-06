// Clause 13.1 — the statement grammar — and clause 13.4 — the empty statement.
//
// 13.1's content is the `statement` production, and a list of the forms clauses 13.3 to 13.15
// then define. Every one of those forms has a file of its own here, so what is left for 13.1
// is the thing the production itself says and no individual form does: `embedded_statement`
// is a *different* production from `statement`. Eight syntactic positions take an
// embedded_statement rather than a statement list, and a walk that descends into blocks and
// forgets the single-statement positions loses everything written in them without dropping a
// declaration anywhere a count would notice.
//
// So each of those eight appears below exactly once, with no braces, holding a call to
// `Mark` — and so do the two positions the grammar contrasts them with, a label's statement
// and a `checked` block. Ten references to one method, each in a different grammatical
// position, and one more statement that is the empty one.

using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>
/// Every position clause 13.1's <c>embedded_statement</c> may stand in, each holding one
/// braceless statement.
/// </summary>
public sealed class StmtGrammarPositions
{
    private readonly List<string> _marks = [];

    /// <summary>What every embedded statement in this type was written to hold.</summary>
    /// <param name="where">The position the call was written in.</param>
    public void Mark(string where) => _marks.Add(where);

    /// <summary>The positions marked so far, in the order they were reached.</summary>
    public IReadOnlyList<string> Marks => _marks;

    /// <summary>
    /// Walks every embedded statement position in the grammar.
    /// </summary>
    /// <remarks>
    /// An <c>embedded_statement</c> may not be a declaration statement or a labeled
    /// statement — the grammar excludes both, which is why <c>if (gate) int x = 1;</c> is a
    /// syntax error while <c>Written: int x = 1;</c> is legal. The labeled form is in
    /// <c>LabeledStatements.cs</c>; the position taken here is the one a label's own
    /// statement occupies.
    /// </remarks>
    /// <param name="gate">Chooses the <c>if</c> branch.</param>
    /// <param name="numbers">Something for <c>foreach</c> to walk.</param>
    /// <returns>How many positions were reached.</returns>
    public int WalkEveryPosition(bool gate, int[] numbers)
    {
        // 13.8.2 — position 1, the `if` branch, unbraced.
        if (gate)
            Mark("if-then");
        else
            // 13.8.2 — position 2, the `else` branch. A distinct grammar position, not a
            // second use of the first one.
            Mark("if-else");

        int spins = 0;

        // 13.9.2 — position 3, the `while` body.
        while (spins < 1)
            Mark($"while-body {spins++}");

        // 13.9.3 — position 4, the `do` body.
        do
            Mark("do-body");
        while (spins > 9);

        // 13.9.4 — position 5, the `for` body.
        for (int i = 0; i < 1; i++)
            Mark("for-body");

        // 13.9.5 — position 6, the `foreach` body.
        foreach (int number in numbers)
            Mark($"foreach-body {number}");

        // 13.13 — position 7, the `lock` body.
        lock (_marks)
            Mark("lock-body");

        // 13.14.1 — position 8, the `using` body.
        using (StmtHandle handle = new("using-body"))
            Mark(handle.Name);

        // 13.5 — position 9, and not an embedded one: the statement a label labels. The
        // grammar is
        // `labeled_statement : identifier ':' statement`, so this position takes a
        // *statement* and admits forms the eight above do not.
        Written:
            Mark("label-body");

        // 13.12 — position 10, and the other counterexample: `checked` takes a *block* and
        // not an embedded statement, as do the three parts of 13.11's `try`. This is what
        // makes the distinction worth writing down — `checked Mark("x");` is a syntax error.
        checked
        {
            Mark("checked-block");
        }

        // 13.4 — an `if` position again, this time holding the only statement form that can
        // stand in any of these positions and contain nothing at all.
        if (numbers.Length < 0)
            ;

        return _marks.Count;
    }
}

/// <summary>Clause 13.4 — the empty statement, in the three places it is not merely legal.</summary>
public static class StmtEmptyStatements
{
    /// <summary>
    /// Spins until a counter runs out, doing all the work in the loop conditions.
    /// </summary>
    /// <param name="limit">How far to count.</param>
    /// <returns>How many steps were taken.</returns>
    public static int SpinTo(int limit)
    {
        int seen = 0;

        // 13.4 — the body of a `while` whose whole work is its condition. The empty
        // statement is what makes a loop with no body writable.
        while (Advance(ref seen, limit)) ;

        // 13.4 — a `for` with an empty body, which is the same statement in a different
        // embedded position.
        for (int i = 0; i < limit; i++) ;

        if (limit > 0)
        {
            // 13.10.4 — jumping to the label below, whose statement is the empty one.
            goto Done;
        }

        seen = -seen;

        // 13.4 — a label at the end of a block still needs a statement to label, and the
        // empty statement is the one that says "and then nothing".
        Done: ;

        return seen;
    }

    private static bool Advance(ref int seen, int limit)
    {
        seen++;
        return seen < limit;
    }
}
