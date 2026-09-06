// Clause 13.10.6 — the throw statement, and the throw *expression* the language grew later.
//
// A `throw` is a reference row: `throw new StmtBudgetError(spent, budget)` reaches a
// constructor, and a bare `throw;` reaches nothing at all — it rethrows the exception the
// enclosing catch clause caught, so its "operand" is a variable the source does not write and
// may not even have a name.
//
// The hazard is the pair in `TwiceOverBudget`: one member, two `throw new StmtBudgetError(…)`
// statements, one constructor. Two occurrences and one (source, target) pair, which is the
// same shape as the expression-statement hazard in `ExpressionStatements.cs` and is worth
// writing again here because a `throw` is not an expression statement — it is its own
// production, so a walk can descend into one and not the other.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.10.6 — <c>throw</c>, as a statement and as an expression.</summary>
public static class StmtThrows
{
    /// <summary>
    /// Two <c>throw</c> statements reaching one constructor.
    /// </summary>
    /// <remarks>
    /// 13.10.6, and its hazard. Both statements construct a
    /// <see cref="StmtBudgetError"/> with two arguments, from two places in one member.
    /// </remarks>
    /// <param name="spent">What was spent.</param>
    /// <param name="budget">What was allowed.</param>
    /// <returns>The remaining budget, when there is one.</returns>
    /// <exception cref="StmtBudgetError">When the budget is exceeded, from either check.</exception>
    public static int TwiceOverBudget(int spent, int budget)
    {
        if (spent > budget)
        {
            throw new StmtBudgetError(spent, budget);
        }

        int doubled = spent * 2;

        if (doubled > budget)
        {
            throw new StmtBudgetError(doubled, budget);
        }

        return budget - doubled;
    }

    /// <summary>
    /// A bare <c>throw</c>, which references nothing the source names.
    /// </summary>
    /// <remarks>
    /// 13.10.6. `throw;` is only legal in a catch block, and it rethrows the caught
    /// exception preserving its stack trace — where `throw error;` would reset it. The first
    /// catch below names its exception and does not use the name; the second does not name
    /// one at all, and still rethrows the same way. So one of the two `throw;` statements
    /// has a variable in scope that could have been its operand and the other has none, and
    /// neither writes anything.
    /// </remarks>
    /// <param name="spent">What was spent.</param>
    /// <param name="budget">What was allowed.</param>
    /// <returns>The remaining budget.</returns>
    public static int Rethrown(int spent, int budget)
    {
        try
        {
            return TwiceOverBudget(spent, budget);
        }
        catch (StmtBudgetError error) when (error.Spent > 1_000)
        {
            // 13.10.6 — a rethrow with a named exception in scope, unused.
            throw;
        }
        catch
        {
            // 13.10.6 — a rethrow from a general catch, where there is no name at all.
            throw;
        }
    }

    /// <summary>
    /// Every position a <c>throw</c> <em>expression</em> may stand in.
    /// </summary>
    /// <remarks>
    /// Post-standard, and the reason clause 13.10.6 is not the whole story: `throw` became an
    /// expression, so it now appears in positions that take a value and never took a
    /// statement. Four of them are below — the right of `??`, an arm of `?:`, an
    /// expression-bodied member and a lambda body — and each is a reference to a constructor
    /// from a syntax position that clause 13 does not describe at all.
    /// </remarks>
    /// <param name="name">A name that may be null.</param>
    /// <param name="severity">How bad a rule break to report.</param>
    /// <returns>The name, when there is one.</returns>
    /// <exception cref="StmtRuleError">When there is not.</exception>
    public static string EveryExpressionPosition(string? name, int severity)
    {
        // Post-standard — the right operand of `??`, which is the position the feature was
        // added for.
        string checkedName = name ?? throw new StmtRuleError("name", severity);

        // Post-standard — an arm of a conditional expression. Both arms have to have a type
        // for the conditional to have one, and a `throw` arm is exempt from that.
        int length = checkedName.Length > 0
            ? checkedName.Length
            : throw new StmtRuleError("empty", severity);

        // Post-standard — a lambda body that is a single `throw`.
        Func<int, int> refuse = value => value >= 0
            ? value
            : throw new StmtRuleError("negative", severity);

        return $"{checkedName}:{length}:{refuse(length)}";
    }

    /// <summary>
    /// An expression-bodied member whose whole body is a <c>throw</c> expression.
    /// </summary>
    /// <remarks>
    /// Post-standard. The member has a return type and no `return` statement anywhere,
    /// because a `throw` expression is the body — a shape that has no reachable end point
    /// (13.2) and never had a statement in it.
    /// </remarks>
    /// <param name="rule">Which rule to complain about.</param>
    /// <returns>Never.</returns>
    /// <exception cref="StmtRuleError">Always.</exception>
    public static int NeverAnswers(string rule) => throw new StmtRuleError(rule, 9);

    /// <summary>
    /// A <c>throw</c> in a switch expression arm, and a <c>throw</c> statement in a switch
    /// section, so the two forms sit side by side.
    /// </summary>
    /// <param name="gate">What to switch on.</param>
    /// <returns>A label for the gate.</returns>
    /// <exception cref="StmtRuleError">For a gate with no label.</exception>
    public static string BothInOneSwitch(int gate)
    {
        // Post-standard — a `throw` expression as a switch arm.
        string byExpression = gate switch
        {
            0 => "zero",
            > 0 => "positive",
            _ => throw new StmtRuleError("negative", 1),
        };

        // 13.10.6 — a `throw` statement as the whole of a switch section.
        switch (gate)
        {
            case 0:
                return byExpression;

            case < 0:
                throw new StmtRuleError("negative", 2);

            default:
                return $"{byExpression}:{gate}";
        }
    }
}
