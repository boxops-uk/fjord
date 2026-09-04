using System;
using System.Collections.Generic;
using System.Linq;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Statements;

// The ten query-expression kinds.
//
// **Every one of them is both a declaration and a reference, and the index holds neither.**
// `from entry in entries` declares a `SymbolKind.RangeVariable` and calls nothing;
// `where`, `select`, `orderby`, `join` and `group` each call a method — `Where`, `Select`,
// `OrderBy`, `ThenByDescending`, `Join`, `GroupJoin`, `GroupBy`, `SelectMany` — that no
// identifier in the query names. The declaration switch reaches no query clause, and the
// walk drops a `SymbolKind.RangeVariable` reference before writing anything, so a query
// expression contributes references to its *sources* and nothing else.
//
// The method-syntax equivalent is written beside each query so the difference is checkable
// rather than argued: the same computation spelled with `.Where(…)` produces an
// `InvocationExpression` and a `SimpleMemberAccessExpression` per stage, every one of which
// the walk sees.

/// <summary>A row the queries below run over.</summary>
/// <param name="Slot">Which slot.</param>
/// <param name="Kind">Which side of the ledger.</param>
/// <param name="Amount">How much.</param>
public record SfQueryRow(int Slot, SfLedgerKind Kind, int Amount);

/// <summary>Every query clause kind.</summary>
public static class SfQueryForms
{
    private static readonly SfQueryRow[] Rows =
    [
        new SfQueryRow(1, SfLedgerKind.Debit, 30),
        new SfQueryRow(2, SfLedgerKind.Credit, 20),
        new SfQueryRow(3, SfLedgerKind.Debit, 10),
    ];

    private static readonly SfLedgerRecord[] Labels =
    [
        new SfLedgerRecord(1, "one"),
        new SfLedgerRecord(2, "two"),
    ];

    /// <summary>
    /// FromClause, WhereClause, LetClause, OrderByClause with an AscendingOrdering and a
    /// DescendingOrdering, and SelectClause.
    /// </summary>
    /// <returns>What the query produced.</returns>
    public static IEnumerable<string> Simple()
    {
        // A second `from` clause, which is the one that compiles to `SelectMany` rather than
        // to the query's own source.
        var query =
            from row in Rows
            from digit in row.Slot.ToString()
            where row.Amount > 5
            let scaled = row.Amount * 2
            orderby row.Slot ascending, scaled descending
            select $"{row.Kind}:{scaled}:{digit}";

        return query;
    }

    /// <summary>The same computation in method syntax, for the comparison the file exists to make.</summary>
    /// <returns>What the chain produced.</returns>
    public static IEnumerable<string> SimpleAsMethods() =>
        Rows.SelectMany(row => row.Slot.ToString(), (row, digit) => new { row, digit })
            .Where(pair => pair.row.Amount > 5)
            .Select(pair => new { pair.row, pair.digit, scaled = pair.row.Amount * 2 })
            .OrderBy(pair => pair.row.Slot)
            .ThenByDescending(pair => pair.scaled)
            .Select(pair => $"{pair.row.Kind}:{pair.scaled}:{pair.digit}");

    /// <summary>
    /// JoinClause and JoinIntoClause — the second of which declares a group range variable
    /// as well as referencing `GroupJoin` rather than `Join`.
    /// </summary>
    /// <returns>What the joins produced.</returns>
    public static IEnumerable<string> Joins()
    {
        var joined =
            from row in Rows
            join label in Labels on row.Slot equals label.Seed
            select $"{row.Slot}:{label.Label}";

        var grouped =
            from row in Rows
            join label in Labels on row.Slot equals label.Seed into matches
            select $"{row.Slot}:{matches.Count()}";

        return joined.Concat(grouped);
    }

    /// <summary>
    /// GroupClause and QueryContinuation — `group … by …` ends a query, and `into …`
    /// declares a new range variable that a fresh set of clauses runs over.
    /// </summary>
    /// <returns>What the grouping produced.</returns>
    public static IEnumerable<string> Groups()
    {
        // GroupClause with no continuation, which is what a query may end with.
        var byKind =
            from row in Rows
            group row by row.Kind;

        // QueryContinuation after a `group`, whose `into` declares `bucket`.
        var continued =
            from row in Rows
            group row by row.Kind into bucket
            where bucket.Count() > 1
            orderby bucket.Key
            select $"{bucket.Key}:{bucket.Sum(row => row.Amount)}";

        // QueryContinuation after a `select`, which is the other place `into` is legal.
        var afterSelect =
            from row in Rows
            select row.Amount into amount
            where amount > 10
            select amount.ToString();

        return byKind.Select(bucket => bucket.Key.ToString())
            .Concat(continued)
            .Concat(afterSelect);
    }

    /// <summary>Runs every query, so the entry point has one call to make.</summary>
    /// <returns>A rendering of everything the queries produced.</returns>
    public static string Report() =>
        string.Join(
            ",",
            Simple().Concat(SimpleAsMethods()).Concat(Joins()).Concat(Groups()));
}
