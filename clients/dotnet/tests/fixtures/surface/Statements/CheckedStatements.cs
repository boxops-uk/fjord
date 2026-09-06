// Clause 13.12 — the checked and unchecked statements.
//
// The row is marked `neither`, and it is the clearest case of that in clause 13: a `checked`
// block declares nothing and references nothing. What it does is change the *meaning* of the
// arithmetic operators inside it, which is a fact about every expression in a region and about
// no declaration anywhere. There is nothing in an index to hold it against — and that is
// exactly why the block has to be in the corpus: an index that silently drops the block's
// contents along with the block loses eight statements and no declaration, so nothing counts
// down.
//
// The clause covers only the *statement* forms. `checked(e)` and `unchecked(e)` are
// expressions, clause 12.8.19, and belong to the Expressions project; one of each is written
// below in a comment so the boundary is visible from here.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.12 — the two blocks that change what arithmetic means.</summary>
public static class StmtChecked
{
    /// <summary>
    /// A <c>checked</c> block, an <c>unchecked</c> block, and the same arithmetic in both.
    /// </summary>
    /// <remarks>
    /// 13.12. The operands are locals rather than literals on purpose: `int.MaxValue + 1` as
    /// a *constant* expression inside a `checked` block is CS0220 at compile time, so the
    /// overflow the clause is about can only be written with values the compiler cannot fold.
    /// That constraint is the reason this file has a `Widen` helper instead of literals.
    /// </remarks>
    /// <param name="seed">A number to overflow from.</param>
    /// <returns>A label saying which block did what.</returns>
    public static string BothBlocks(int seed)
    {
        int big = int.MaxValue - seed;
        string label;

        // 13.12 — an `unchecked` block. The addition wraps round silently, which is the
        // default in a project that has not set CheckForOverflowUnderflow, and is stated here
        // anyway so the region is in the source.
        unchecked
        {
            int wrapped = big + seed + 1;
            label = $"unchecked {wrapped < 0}";
        }

        // 13.12 — a `checked` block. The same addition throws, and the `try` around it is
        // the only reason this method returns.
        try
        {
            checked
            {
                int guarded = big + seed + 1;
                label += $" checked {guarded}";
            }
        }
        catch (OverflowException)
        {
            label += " checked threw";
        }

        return label;
    }

    /// <summary>
    /// A <c>checked</c> block nested in an <c>unchecked</c> one, and a call out of both.
    /// </summary>
    /// <remarks>
    /// 13.12. The context is lexical and it does not follow a call: `Widen` below is compiled
    /// in the *default* context whatever block calls it, so the innermost `checked` has no
    /// effect on the arithmetic inside it. An index that models the block as containing the
    /// arithmetic it affects would have to stop at the call boundary, and this is the shape
    /// that says where.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total, wrapped or not.</returns>
    public static int Nested(int seed)
    {
        int total = 0;

        unchecked
        {
            total += seed * 2;

            checked
            {
                total += seed;

                // 13.12 — the arithmetic in `Widen` is not in this `checked` context. The
                // context is a property of the source region, not of the call graph.
                total += Widen(seed);
            }

            total += seed * 3;
        }

        // Clause 12.8.19, not 13.12, and here to mark the boundary:
        //   int folded = unchecked(int.MaxValue + 1);
        // is the *expression* form, which is legal where the constant is not, and lives in
        // the Expressions project.

        return total;
    }

    private static int Widen(int value) => value * 1_000;
}
