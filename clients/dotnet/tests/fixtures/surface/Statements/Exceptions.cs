// The exception vocabulary clauses 13.10.6 and 13.11 name.
//
// A `throw` is a reference to a constructor and a `catch` is a reference to a type, so both
// clauses need types declared inside the corpus for their edges to land on. Two are declared
// so that a `catch` list can have more than one specific clause and a filter can have
// something to test.

using System;

namespace Surface.Statements;

/// <summary>Thrown when a budget clause 13.10.6 is throwing about runs out.</summary>
public sealed class StmtBudgetError : Exception
{
    /// <summary>Reports a budget that was exceeded.</summary>
    /// <param name="spent">What was spent.</param>
    /// <param name="budget">What was allowed.</param>
    public StmtBudgetError(int spent, int budget)
        : base($"spent {spent} of {budget}")
    {
        Spent = spent;
        Budget = budget;
    }

    /// <summary>What was spent.</summary>
    public int Spent { get; }

    /// <summary>What was allowed.</summary>
    public int Budget { get; }
}

/// <summary>
/// Thrown when a rule is broken. Carries a severity so a clause 13.11 exception filter has a
/// property to read — a filter is an expression, and the only interesting filters read the
/// caught exception.
/// </summary>
public sealed class StmtRuleError : Exception
{
    /// <summary>Reports a broken rule.</summary>
    /// <param name="rule">Which rule.</param>
    /// <param name="severity">How badly, from 0 upwards.</param>
    public StmtRuleError(string rule, int severity)
        : base($"rule {rule} broken")
    {
        Rule = rule;
        Severity = severity;
    }

    /// <summary>Which rule was broken.</summary>
    public string Rule { get; }

    /// <summary>How badly it was broken.</summary>
    public int Severity { get; }
}
