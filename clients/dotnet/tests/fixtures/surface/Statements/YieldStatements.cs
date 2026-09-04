// Clause 13.15 — the yield statement.
//
// `yield return e` and `yield break` are marked `neither`, and in one sense that is right:
// they declare nothing and name nothing. In another sense they are the most consequential
// statements in clause 13, because a member containing one is not compiled as a member at all
// — it becomes a nested state machine class whose fields are the enclosing member's locals and
// whose `MoveNext` holds its statements. So every declaration in an iterator block has two
// forms, and an index built from source and one built from the emitted metadata disagree about
// every one of them. That is a fact about the *containers* an index reports, not about the
// yield statements, and the six iterators below are here so a query has enough of them to
// tell which side it is reading.
//
// `yield` is also the statement with the most positional restrictions in the language, and
// three of them are shapes no compiling C# contains. They are named in comments at the place
// they would have gone.
//
// The `fixed` statement is *not* here: it belongs to the unsafe clause, and its rows are the
// Unsafe project's. Nothing in this project takes an address or declares a pointer.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Clause 13.15 — <c>yield return</c> and <c>yield break</c>, in every container.</summary>
public sealed class StmtYield
{
    private readonly int[] _cells;

    /// <summary>Builds something with numbers in it to yield.</summary>
    /// <param name="cells">The numbers.</param>
    public StmtYield(params int[] cells) => _cells = cells;

    /// <summary>
    /// An iterator returning <see cref="IEnumerable{T}"/>, with both yield forms.
    /// </summary>
    /// <remarks>
    /// 13.15. `yield break` ends the sequence; it is not `return`, and an iterator may not
    /// use `return e` at all (CS1622). The two statements below are the only two ways out of
    /// an iterator block that are not an exception.
    /// </remarks>
    /// <param name="limit">Stop yielding at this value.</param>
    /// <returns>A lazy sequence.</returns>
    public IEnumerable<int> Below(int limit)
    {
        foreach (int cell in _cells)
        {
            if (cell >= limit)
            {
                // 13.15 — `yield break`, which ends the sequence rather than the method.
                yield break;
            }

            // 13.15 — `yield return`, in a `foreach` body inside an iterator block.
            yield return cell;
        }
    }

    /// <summary>
    /// An iterator returning <see cref="IEnumerator{T}"/> rather than an enumerable.
    /// </summary>
    /// <remarks>
    /// 13.15. Both interfaces are legal return types for an iterator, and they generate
    /// different state machines: the enumerable form's machine implements four interfaces and
    /// can be walked more than once, the enumerator form's implements two and cannot. Same
    /// statements, different emitted container.
    /// </remarks>
    /// <returns>A cursor over the cells, doubled.</returns>
    public IEnumerator<int> Doubled()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            yield return _cells[i] * 2;
        }
    }

    /// <summary>
    /// An iterator returning the non-generic <see cref="IEnumerable"/>.
    /// </summary>
    /// <remarks>
    /// 13.15. The yield type is `object`, so every `yield return` here carries a boxing
    /// conversion the source does not spell — the mirror image of the conversion 13.9.5.2
    /// inserts when walking a non-generic collection.
    /// </remarks>
    /// <returns>A lazy sequence of boxed numbers.</returns>
    public IEnumerable Boxed()
    {
        foreach (int cell in _cells)
        {
            yield return cell;
        }
    }

    /// <summary>
    /// An iterator that is a property accessor rather than a method.
    /// </summary>
    /// <remarks>
    /// 13.15. A `get` accessor whose body contains `yield` is an iterator, so the state
    /// machine's container is an accessor — a member with no name of its own in the source
    /// and a compiler-generated one (`get_Running`) in the metadata. Two levels of
    /// synthesised container over one statement.
    /// </remarks>
    public IEnumerable<int> Running
    {
        get
        {
            int total = 0;

            foreach (int cell in _cells)
            {
                total += cell;
                yield return total;
            }
        }
    }

    /// <summary>
    /// An iterator local function, and an iterator nested inside one.
    /// </summary>
    /// <remarks>
    /// 13.15 with 13.6.4. A `yield` inside a local function belongs to the *local function*,
    /// which is why the enclosing method here is not an iterator and can `return` a value.
    /// The state machine's container is a local function, which has no metadata name a query
    /// can be handed — and `Tail` below nests a second one inside the first, so the container
    /// chain is two synthesised levels deep.
    ///
    /// An iterator *lambda* would be the obvious third case and does not exist: `yield` in a
    /// lambda or an anonymous method is CS1621, which is why both nested iterators here are
    /// local functions.
    /// </remarks>
    /// <param name="limit">How many to take.</param>
    /// <returns>What the nested iterators produced.</returns>
    public List<int> FromNested(int limit)
    {
        // 13.15 — a local function containing `yield`, so `FromNested` itself is an ordinary
        // method with an ordinary `return` at the bottom.
        IEnumerable<int> Head()
        {
            int taken = 0;

            foreach (int cell in _cells)
            {
                if (taken++ >= limit)
                {
                    yield break;
                }

                yield return cell;
            }
        }

        // 13.15 — an iterator local function declared inside another local function, whose
        // own `yield` statements belong to the inner one.
        IEnumerable<int> Tail()
        {
            IEnumerable<int> Inner()
            {
                yield return -1;
                yield return -2;
            }

            foreach (int value in Inner())
            {
                yield return value;
            }
        }

        List<int> seen = [.. Head()];
        seen.AddRange(Tail());

        return seen;
    }

    /// <summary>
    /// A <c>yield</c> inside a <c>try</c> with a <c>finally</c>, which is where the
    /// restrictions start.
    /// </summary>
    /// <remarks>
    /// 13.15. `yield return` is allowed in a `try` block that has a `finally` and no `catch`,
    /// and the `finally` runs when the *consumer* stops enumerating — so the disposal below
    /// happens at the caller's `foreach`, not at any statement in this method. Three shapes
    /// are refused and cannot be in the corpus:
    ///
    ///   * `yield return` in a `try` with a `catch` clause — CS1626;
    ///   * `yield return` in a `finally` block — CS1625, or in a `catch` block, CS1631;
    ///   * `yield return` in an anonymous method or a `lock` body — CS1621 and CS9237.
    /// </remarks>
    /// <returns>A lazy sequence that cleans up after itself.</returns>
    public IEnumerable<int> Guarded()
    {
        StmtHandle handle = new("iterator");

        try
        {
            foreach (int cell in _cells)
            {
                // 13.15 — legal: the `try` has a `finally` and no `catch`.
                yield return cell + handle.Measure();
            }
        }
        finally
        {
            // 13.15 — runs when the consumer disposes the enumerator, which is a call the
            // consumer's `foreach` makes and does not write (13.9.5.2).
            handle.Dispose();
        }
    }

    /// <summary>
    /// An async iterator, which is <c>yield</c> and <c>await</c> in one body.
    /// </summary>
    /// <remarks>
    /// 13.15, post-standard. `IAsyncEnumerable&lt;T&gt;` is the only return type that admits
    /// both, and the emitted state machine implements the async and the iterator machinery at
    /// once. `AsyncForeachStatements.cs` is the consumer side.
    /// </remarks>
    /// <param name="limit">Stop yielding at this value.</param>
    /// <returns>A lazy asynchronous sequence.</returns>
    public async IAsyncEnumerable<int> BelowAsync(int limit)
    {
        foreach (int cell in _cells)
        {
            await Task.Yield();

            if (cell >= limit)
            {
                yield break;
            }

            yield return cell;
        }
    }
}
