// Clause 13.7 — expression statements.
//
// The clause is a *restriction*: only a `statement_expression` may stand alone as a
// statement, and the list is invocation, object creation, assignment, increment, decrement,
// await and — post-standard — the null-conditional forms of the first of those. `left +
// right;` is not on the list and is CS0201, so this clause is one of the few whose content is
// half a set of legal shapes and half an error nobody can write down.
//
// The row is a *reference* row, and the hazard is what a reference row means when the same
// target is reached twice. `BumpTwice` calls one method as two expression statements in one
// member: two occurrences, one (source member, target member) pair. Anything that keys a
// reference by that pair and nothing else records one of the two calls and loses the other
// silently.

using System;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Something for an expression statement to reach.</summary>
public sealed class StmtCounter
{
    /// <summary>How many times <see cref="Bump"/> has run.</summary>
    public int Count { get; private set; }

    /// <summary>A field an assignment statement can write to.</summary>
    public int Slack;

    /// <summary>Adds one to the count. Called twice as a statement, from one member.</summary>
    public void Bump() => Count++;

    /// <summary>Adds one and says what the count is now, so the value can be discarded.</summary>
    /// <returns>The new count.</returns>
    public int BumpAndReport() => ++Count;

    /// <summary>Waits, so an <c>await</c> can be a statement.</summary>
    /// <returns>A task that is already done.</returns>
    public Task BumpAsync()
    {
        Count++;
        return Task.CompletedTask;
    }
}

/// <summary>Clause 13.7 — every expression that may stand as a statement.</summary>
public static class StmtExpressionStatements
{
    /// <summary>
    /// Calls one method twice, as two expression statements.
    /// </summary>
    /// <remarks>
    /// 13.7, and its hazard: one member, one target, two occurrences. The two calls differ
    /// only in where they are written.
    /// </remarks>
    /// <param name="counter">The counter to bump.</param>
    /// <returns>The count afterwards, which is two more than it was.</returns>
    public static int BumpTwice(StmtCounter counter)
    {
        counter.Bump();
        counter.Bump();

        return counter.Count;
    }

    /// <summary>
    /// Every form on 13.7's list, one statement each.
    /// </summary>
    /// <param name="counter">A counter to reach.</param>
    /// <returns>The count afterwards.</returns>
    public static int EveryForm(StmtCounter counter)
    {
        // 13.7 — invocation, with the result discarded. `BumpAndReport` returns an int and
        // nothing takes it, which is legal precisely because the expression is on the list.
        counter.BumpAndReport();

        // 13.7 — object creation as a statement. The object is created and dropped; the
        // constructor reference is the only trace the statement leaves.
        new StmtCounter();

        // 13.7 — simple assignment.
        counter.Slack = 1;

        // 13.7 — compound assignment, which is a distinct production and a reference to
        // `op_Addition` in the general case.
        counter.Slack += 2;

        // 13.7 — post-increment and pre-increment, which are two forms, not one.
        counter.Slack++;
        ++counter.Slack;

        // 13.7 — post-decrement and pre-decrement.
        counter.Slack--;
        --counter.Slack;

        // 13.7, post-standard — a null-conditional invocation. The whole statement is a
        // no-op when the receiver is null, and the receiver here is not.
        StmtCounter? maybe = counter;
        maybe?.Bump();

        // 13.7, post-standard — a discard assignment, which is how a value that must be read
        // is turned into a statement without declaring anything.
        _ = counter.Count;

        // Not on 13.7's list, and the error the clause is really about:
        //   counter.Count + 1;
        // is CS0201 — only assignment, call, increment, decrement, await and new can be used
        // as a statement. It cannot be in the corpus, so it is here as a comment.

        return counter.Count + counter.Slack;
    }

    /// <summary>
    /// The <c>await</c> form, which is only a statement inside an async body.
    /// </summary>
    /// <param name="counter">A counter to bump asynchronously.</param>
    /// <returns>The count afterwards.</returns>
    public static async Task<int> EveryAwaitForm(StmtCounter counter)
    {
        // 13.7 — await as a statement, discarding a `Task`.
        await counter.BumpAsync();

        // 13.7 — await as a statement over a task with a result, discarding the result.
        await Task.FromResult(counter.Count);

        // 13.7 — an await whose operand is a call on a value the source never names, so the
        // reference chain in one statement is three members deep.
        await Task.Delay(TimeSpan.Zero).ConfigureAwait(false);

        return counter.Count;
    }
}
