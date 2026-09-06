// Clause 13.2 — end points and reachability.
//
// The clause is about flow analysis, and flow analysis is the compiler's business rather than
// an index's. What makes the clause worth a file anyway is that a *declaration* can sit in
// unreachable code. The compiler warns (CS0162) and then compiles it: it is in the syntax
// tree, it binds, and it has a type. Whether it reaches the index is therefore a real
// question with two possible answers, and `Stranded` below is where a query goes to ask it.
//
// The other half of the clause — a statement with no reachable end point — decides whether a
// `return` is *required*, and `NeverReturns` compiles with no trailing return only because
// `while (true)` has no reachable end point. That is a fact about the source's shape, not
// about the index, and it is here so that a reader looking for 13.2 finds both halves.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.2 — reachability, from both ends.</summary>
public static class StmtReachability
{
    /// <summary>
    /// Declares a local that no execution can reach.
    /// </summary>
    /// <param name="seed">A number to return.</param>
    /// <returns>The seed, always.</returns>
    public static int Stranded(int seed)
    {
        return seed;

        // 13.2 — an unreachable statement, and a declaration inside it. CS0162 says the
        // statement cannot be reached; nothing says it was not compiled. `stranded` is the
        // one local in this project whose presence or absence in the index is a fact about
        // whether the walk trusts reachability.
#pragma warning disable CS0162
        int stranded = seed + 1;
#pragma warning restore CS0162
    }

    /// <summary>
    /// A method with no <c>return</c> statement at all, which is legal because the end point
    /// of its body is not reachable.
    /// </summary>
    /// <param name="budget">A budget to exceed.</param>
    /// <returns>Never; the method always throws.</returns>
    /// <exception cref="StmtBudgetError">Always.</exception>
    public static int NeverReturns(int budget)
    {
        int spent = 0;

        // 13.2 — a `while (true)` has no reachable end point, so neither has the block
        // containing it, so no `return` is required after it. Removing the `true` makes this
        // method CS0161.
        while (true)
        {
            spent += 1;

            if (spent > budget)
            {
                throw new StmtBudgetError(spent, budget);
            }
        }
    }

    /// <summary>
    /// A <c>switch</c> in which every section ends in a jump, so the switch itself has no
    /// reachable end point either.
    /// </summary>
    /// <param name="gate">Which section to run.</param>
    /// <returns>A label for the section reached.</returns>
    public static string EveryArmJumps(int gate)
    {
        switch (gate)
        {
            case 0:
                return "zero";

            case 1:
                throw new InvalidOperationException("one is not allowed");

            default:
                return "many";
        }

        // 13.2 — nothing may be written here and no `return` is needed: the end point of
        // the switch statement above is unreachable, because the end point of every one of
        // its sections is.
    }
}
