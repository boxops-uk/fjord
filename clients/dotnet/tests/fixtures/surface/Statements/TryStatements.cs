// Clause 13.11 — the try statement.
//
// The row is marked `both`, and both halves are unusual. A catch clause *declares*: `catch
// (StmtRuleError error)` declares a local whose scope is the catch block, and it is the only
// declaration in the language whose value comes from the runtime rather than from an
// initializer. A catch clause also *references* a type, in a position where nothing else can
// appear — a catch's type is not a variable's type, it is a filter, and `catch (Exception)`
// with no identifier references a type and declares nothing at all.
//
// The hazard is the pair of catch clauses in `TwoErrors`. Each catch clause is its own
// declaration space, so two clauses of one try statement may both declare `error`, at two
// different types. One member, one try statement, two declarations, one name — and unlike the
// switch-section hazard, there is not even a statement list between them.

using System;
using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.11 — <c>try</c>, with every clause it admits.</summary>
public static class StmtTry
{
    /// <summary>
    /// Two catch clauses of one <c>try</c>, both declaring <c>error</c>.
    /// </summary>
    /// <remarks>
    /// 13.11, and its hazard. `error` is a <see cref="StmtBudgetError"/> in the first clause
    /// and a <see cref="StmtRuleError"/> in the second. Both are declarations, neither is
    /// visible to the other, and the order of the clauses is what decides which one runs.
    /// </remarks>
    /// <param name="spent">What was spent.</param>
    /// <param name="budget">What was allowed.</param>
    /// <returns>A label for what happened.</returns>
    public static string TwoErrors(int spent, int budget)
    {
        try
        {
            if (spent > budget)
            {
                throw new StmtBudgetError(spent, budget);
            }

            if (budget < 0)
            {
                throw new StmtRuleError("budget", 1);
            }

            return "within";
        }
        catch (StmtBudgetError error)
        {
            return $"budget {error.Spent}/{error.Budget}";
        }
        catch (StmtRuleError error)
        {
            return $"rule {error.Rule}";
        }
    }

    /// <summary>
    /// Every clause form a <c>try</c> statement has.
    /// </summary>
    /// <remarks>
    /// 13.11. A specific catch with an identifier; a specific catch *without* one, which
    /// references a type and declares nothing; a catch with an exception filter, whose `when`
    /// expression runs before any `finally` between the throw and here — the trail below is
    /// the evidence, because "filter" is appended before "finally"; and a general `catch`,
    /// which must be last and catches what the framework wraps as well as what it does not.
    /// </remarks>
    /// <param name="which">Which failure to provoke.</param>
    /// <returns>A trail of what ran, in the order it ran.</returns>
    public static string EveryClause(int which)
    {
        List<string> trail = [];

        try
        {
            trail.Add("outer-try");

            try
            {
                trail.Add("inner-try");

                switch (which)
                {
                    case 1:
                        throw new StmtBudgetError(2, 1);

                    case 2:
                        throw new StmtRuleError("filtered", 5);

                    case 3:
                        throw new StmtRuleError("unfiltered", 0);

                    default:
                        throw new InvalidOperationException("unknown");
                }
            }
            finally
            {
                // 13.11 — an inner `finally`, which runs *after* the outer filters have been
                // evaluated. That ordering is the one thing about exception filters that
                // cannot be seen from the syntax, and this trail is what makes it visible.
                trail.Add("inner-finally");
            }
        }
        catch (StmtBudgetError error) when (Record(trail, $"filter {error.Spent}"))
        {
            // 13.11 — a catch with an identifier and a filter. The filter is an expression,
            // and the only expression in the language that runs before the stack unwinds.
            trail.Add("caught-budget");
        }
        catch (StmtRuleError error) when (error.Severity > 1)
        {
            trail.Add($"caught-severe-{error.Rule}");
        }
        catch (StmtRuleError)
        {
            // 13.11 — a catch clause with a type and no identifier. This is a reference with
            // no declaration beside it, which no other statement form produces.
            trail.Add("caught-rule");
        }
        catch
        {
            // 13.11 — the general catch clause, which has neither a type nor an identifier
            // and therefore neither a reference nor a declaration. It is the only clause in
            // the language that is pure syntax.
            trail.Add("caught-anything");
        }
        finally
        {
            trail.Add("outer-finally");
        }

        return string.Join(",", trail);
    }

    /// <summary>
    /// A <c>try</c> with a <c>finally</c> and no <c>catch</c>.
    /// </summary>
    /// <remarks>
    /// 13.11. `try`/`finally` handles nothing: the exception carries on past it, and the
    /// `finally` runs on the way. This is the shape a `using` statement is defined to be
    /// equivalent to (13.14.1), which is why it is worth having written out once by hand
    /// right next to the file that never writes it.
    /// </remarks>
    /// <param name="handle">A resource to close by hand.</param>
    /// <param name="fail">Whether to throw on the way.</param>
    /// <returns>What the handle measured.</returns>
    /// <exception cref="StmtRuleError">When asked to fail.</exception>
    public static int FinallyOnly(StmtHandle handle, bool fail)
    {
        try
        {
            if (fail)
            {
                throw new StmtRuleError("asked to fail", 1);
            }

            return handle.Measure();
        }
        finally
        {
            handle.Dispose();
        }
    }

    /// <summary>
    /// A <c>try</c> nested in a <c>catch</c>, and a <c>try</c> nested in a <c>finally</c>.
    /// </summary>
    /// <remarks>
    /// 13.11. Both positions are blocks, so both admit a whole `try` statement — which makes
    /// the nesting of `try` statements unbounded in three directions rather than one. A
    /// `throw` inside a `finally` replaces the exception that was in flight, which is the one
    /// case where a `finally` changes what the caller sees; `Swallowed` below catches it
    /// locally rather than doing that.
    /// </remarks>
    /// <param name="which">Which failure to provoke.</param>
    /// <returns>A trail of what ran.</returns>
    public static string Swallowed(int which)
    {
        List<string> trail = [];

        try
        {
            throw new StmtRuleError("first", which);
        }
        catch (StmtRuleError first)
        {
            trail.Add($"caught {first.Rule}");

            // 13.11 — a `try` inside a `catch` block.
            try
            {
                throw new StmtBudgetError(which, 0);
            }
            catch (StmtBudgetError second)
            {
                trail.Add($"caught {second.Spent}");
            }
        }
        finally
        {
            // 13.11 — a `try` inside a `finally` block. The exception it throws is caught
            // here, so it never replaces anything in flight.
            try
            {
                throw new StmtRuleError("in-finally", which);
            }
            catch (StmtRuleError third)
            {
                trail.Add($"caught {third.Rule}");
            }
        }

        return string.Join(",", trail);
    }

    private static bool Record(List<string> trail, string what)
    {
        trail.Add(what);
        return true;
    }
}
