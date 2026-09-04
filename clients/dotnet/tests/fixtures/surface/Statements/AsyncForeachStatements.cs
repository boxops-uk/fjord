// Clause 13.9.5.3 — the asynchronous foreach.
//
// `await foreach` is 13.9.5.2 with every binding moved: `GetAsyncEnumerator` instead of
// `GetEnumerator`, `MoveNextAsync` returning an awaitable bool instead of `MoveNext`, and
// `DisposeAsync` instead of `Dispose`. It binds by pattern first, exactly as the synchronous
// form does, so a type with none of the interfaces can still be awaited over — `StmtTicker`
// in `ForeachCollections.cs` is that type.
//
// The hazard is `tick`, the iteration variable of four `await foreach` loops in
// `EveryAsyncForm`, at two types. The same shape as the synchronous hazard, and worth writing
// twice because the two forms are compiled by different rewriters: an index that reads the
// syntax tree sees four ordinary declarations, and one that reads what the async rewriter
// emitted sees four fields of a state machine.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Clause 13.9.5.3 — <c>await foreach</c>, and what produces something to await.</summary>
public static class StmtAsyncForeach
{
    /// <summary>
    /// An async iterator: the ordinary way to get an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <remarks>
    /// 13.9.5.3 with 13.15. The method is both an iterator (it yields) and async (it awaits),
    /// which is a combination only `IAsyncEnumerable&lt;T&gt;` permits as a return type. The
    /// `[EnumeratorCancellation]` attribute is how the token an `await foreach` passes
    /// through `WithCancellation` reaches the body — a parameter whose value comes from the
    /// loop rather than from the caller.
    /// </remarks>
    /// <param name="count">How many numbers to produce.</param>
    /// <param name="token">Cancellation, supplied by the loop.</param>
    /// <returns>A lazy asynchronous sequence.</returns>
    public static async IAsyncEnumerable<int> CountAsync(
        int count,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();

            yield return i;
        }
    }

    /// <summary>
    /// An async iterator over pairs, so that a deconstructing <c>await foreach</c> has
    /// something to deconstruct.
    /// </summary>
    /// <param name="count">How many pairs to produce.</param>
    /// <returns>A lazy asynchronous sequence of pairs.</returns>
    public static async IAsyncEnumerable<StmtPair> PairsAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();

            yield return new StmtPair($"key{i}", i);
        }
    }

    /// <summary>
    /// Four <c>await foreach</c> loops, four declarations of <c>tick</c>.
    /// </summary>
    /// <remarks>
    /// 13.9.5.3, and its hazard. The first loop binds through
    /// <see cref="IAsyncEnumerable{T}"/>; the second binds `StmtTicker.GetAsyncEnumerator` by
    /// pattern with no interface anywhere; the third goes through `WithCancellation`, which
    /// returns a `ConfiguredCancelableAsyncEnumerable&lt;T&gt;` — a `struct` whose
    /// `GetAsyncEnumerator` is again found by pattern; and the fourth through
    /// `ConfigureAwait`, which returns the same struct configured differently. Four loops,
    /// four enumerator types, one iteration variable name.
    /// </remarks>
    /// <param name="ticks">How far each loop counts.</param>
    /// <param name="token">Cancellation to hand to one of the loops.</param>
    /// <returns>A total that touches every loop.</returns>
    public static async Task<int> EveryAsyncForm(int ticks, CancellationToken token)
    {
        int total = 0;

        // 13.9.5.3, through the interface.
        await foreach (int tick in CountAsync(ticks))
        {
            total += tick;
        }

        // 13.9.5.3, by pattern. `StmtTicker` implements nothing.
        StmtTicker ticker = new(ticks);
        await foreach (int tick in ticker)
        {
            total += tick;
        }

        // 13.9.5.3, through `WithCancellation`. The token reaches `CountAsync`'s
        // `[EnumeratorCancellation]` parameter, which is a data flow with no call site.
        await foreach (int tick in CountAsync(ticks).WithCancellation(token))
        {
            total += tick;
        }

        // 13.9.5.3, through `ConfigureAwait`, which changes what the loop's awaits capture
        // and nothing else about the binding.
        await foreach (var tick in CountAsync(ticks).ConfigureAwait(false))
        {
            total += tick;
        }

        return total;
    }

    /// <summary>
    /// A deconstructing <c>await foreach</c>, and one with a jump in its body.
    /// </summary>
    /// <remarks>
    /// 13.9.5.3 with 13.9.5.4. Deconstruction composes with the asynchronous form: the
    /// iteration variable position holds a deconstruction declaration, and
    /// `StmtPair.Deconstruct` is bound with no name written. Leaving the loop early calls
    /// `DisposeAsync` — an awaited call in a path the source does not contain.
    /// </remarks>
    /// <param name="count">How many pairs to walk.</param>
    /// <param name="stopAt">A weight to stop at.</param>
    /// <returns>The keys seen before stopping.</returns>
    public static async Task<List<string>> UntilWeight(int count, int stopAt)
    {
        List<string> seen = [];

        await foreach (var (key, weight) in PairsAsync(count))
        {
            if (weight >= stopAt)
            {
                break;
            }

            seen.Add(key);
        }

        // 13.9.5.3 — an `await foreach` inside an async *local function* (13.6.4), which
        // makes the local function its own state machine.
        async Task<int> TailAsync()
        {
            int running = 0;

            await foreach (StmtPair pair in PairsAsync(count))
            {
                running += pair.Weight;
            }

            return running;
        }

        seen.Add((await TailAsync()).ToString());

        return seen;
    }
}
