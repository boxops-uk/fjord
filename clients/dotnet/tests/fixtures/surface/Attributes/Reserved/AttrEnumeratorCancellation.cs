// Clause 23.5.8 (the EnumeratorCancellation attribute).
//
// The attribute marks a `CancellationToken` parameter of an async iterator, and what it
// buys is a rewrite: the token the *consumer* supplies through `WithCancellation` — an
// argument to the generated `GetAsyncEnumerator`, not to the iterator method — is what the
// parameter holds inside the body. So the parameter has two sources, one of which is a
// call to a method nothing in this file declares or names.
//
// The hazard is that pair. `Pages` below is called once with an explicit token and once
// with none, and consumed once through `WithCancellation`; the value the body reads comes
// from a different place in each case. An index sees one parameter, one default, and no
// edge to `WithCancellation` at all — and a query for "what supplies this parameter" has
// three answers, of which the source shows two.
//
// The application is also on a parameter of a method the compiler replaces wholesale: an
// async iterator becomes a state machine class whose `MoveNextAsync` carries the body, so
// the parameter the attribute annotates ends up as a field of a generated type.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Attributes.Reserved;

/// <summary>23.5.8: an async iterator whose token comes from its consumer.</summary>
public sealed class AttrPagedLedger
{
    private readonly string[] _pages = ["first", "second", "third"];

    /// <summary>
    /// 23.5.8: the attribute on the token parameter, which is what makes
    /// <c>WithCancellation</c> reach this body at all.
    /// </summary>
    /// <param name="from">Which page to start at.</param>
    /// <param name="token">Supplied by the caller or by the consumer.</param>
    /// <returns>The pages, one at a time.</returns>
    public async IAsyncEnumerable<string> Pages(
        int from,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        for (int index = from; index < _pages.Length; index++)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            yield return _pages[index];
        }
    }

    /// <summary>
    /// 23.5.8: an async iterator with *no* such attribute, so the token it receives is
    /// only ever the argument at its own call site. The pair is the point.
    /// </summary>
    /// <param name="token">Supplied by the caller only.</param>
    /// <returns>The pages, one at a time.</returns>
    public async IAsyncEnumerable<string> PagesUnthreaded(CancellationToken token = default)
    {
        foreach (string page in _pages)
        {
            await Task.Yield();

            if (token.IsCancellationRequested)
            {
                yield break;
            }

            yield return page;
        }
    }

    /// <summary>
    /// 23.5.8: the three ways the parameter gets its value — defaulted, passed, and
    /// threaded in by the consumer.
    /// </summary>
    /// <param name="token">The consumer's token.</param>
    /// <returns>How many pages were read across all three.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2016:Forward the CancellationToken parameter",
        Justification = "23.5.8's point is the call that does not forward it.")]
    public async Task<int> ReadsAllThreeWays(CancellationToken token)
    {
        int read = 0;

        // Defaulted: nothing at the call site, nothing from a consumer.
        await foreach (string page in Pages(0))
        {
            read += page.Length;
        }

        // Passed: an ordinary argument.
        await foreach (string page in Pages(1, token))
        {
            read += page.Length;
        }

        // Threaded: the argument goes to GetAsyncEnumerator, and the attribute is what
        // carries it to the parameter.
        await foreach (string page in Pages(2).WithCancellation(token))
        {
            read += page.Length;
        }

        await foreach (string page in PagesUnthreaded().WithCancellation(token))
        {
            read += page.Length;
        }

        return read;
    }
}
