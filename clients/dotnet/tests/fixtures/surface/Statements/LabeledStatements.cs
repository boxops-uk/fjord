// Clause 13.5 — labeled statements.
//
// A label is a declaration whose declaration space is a *block*, not a member: "the scope of
// a label declared in a labeled_statement is the block in which the label is declared". Two
// sibling blocks in one method may therefore declare the same label, and each `goto` in each
// block binds to its own. That is the hazard 13.5 is flagged for, and `TwoRetries` is it:
// one member, two labels named `Retry`, two references named `Retry`, and four things that
// answer to two strings.
//
// A label is also the one position in the grammar (13.1) whose statement is a full
// `statement` rather than an `embedded_statement`, so `LabeledDeclaration` can label a
// declaration statement where an `if` branch could not.

namespace Surface.Statements;

/// <summary>Clause 13.5 — labels, and the block that scopes them.</summary>
public static class StmtLabels
{
    /// <summary>
    /// Two labels named <c>Retry</c>, in two sibling blocks of one method.
    /// </summary>
    /// <remarks>
    /// 13.5, and the hazard: both declarations are legal, both are in
    /// <see cref="TwoRetries"/>, and both answer to the identifier <c>Retry</c>. The two
    /// <c>goto</c> statements are 13.10.4 and each binds to the label in its own block — so
    /// getting this wrong produces a reference edge that points across a scope boundary
    /// rather than an error anybody would see.
    /// </remarks>
    /// <param name="budget">How far to count in each block.</param>
    /// <returns>What was spent in both blocks together.</returns>
    public static int TwoRetries(int budget)
    {
        int spent = 0;

        {
            // 13.5 — a label. Its scope is this block and nothing outside it.
            Retry:
            spent++;

            if (spent < budget)
            {
                goto Retry;
            }
        }

        {
            // 13.5 — `Retry` again. A different declaration of the same name, in a sibling
            // block, and no error: the first one is not in scope here.
            Retry:
            spent += 2;

            if (spent < budget * 3)
            {
                goto Retry;
            }
        }

        return spent;
    }

    /// <summary>
    /// A label whose statement is a declaration statement.
    /// </summary>
    /// <remarks>
    /// 13.5 with 13.1. `labeled_statement` takes a `statement`, and a declaration statement
    /// is one — so a label may carry a declaration even though the embedded positions of
    /// 13.1 may not. Jumping backwards over it re-enters the declaration's scope, and
    /// <c>carried</c> is assigned again.
    /// </remarks>
    /// <param name="seed">A number to double.</param>
    /// <returns>The doubled, non-negative seed.</returns>
    public static int LabeledDeclaration(int seed)
    {
        Start:
        int carried = seed * 2;

        if (carried < 0)
        {
            seed = -seed;
            goto Start;
        }

        return carried;
    }

    /// <summary>
    /// A label declared in an enclosing block and jumped to from a nested one.
    /// </summary>
    /// <remarks>
    /// 13.5. This is the direction that works: a label is visible in the blocks nested inside
    /// the one declaring it, so <c>goto Finish</c> can leave two loops at once. The reverse —
    /// a label inside a block, jumped to from outside it — is CS0159, and is the reason a
    /// label is a declaration worth resolving rather than a name that always matches.
    /// </remarks>
    /// <param name="grid">Rows to search.</param>
    /// <param name="needle">What to look for.</param>
    /// <returns>The row the needle was found in, or -1.</returns>
    public static int FindRow(int[][] grid, int needle)
    {
        int found = -1;

        for (int row = 0; row < grid.Length; row++)
        {
            for (int column = 0; column < grid[row].Length; column++)
            {
                if (grid[row][column] == needle)
                {
                    found = row;
                    goto Finish;
                }
            }
        }

        Finish:
        return found;
    }
}
