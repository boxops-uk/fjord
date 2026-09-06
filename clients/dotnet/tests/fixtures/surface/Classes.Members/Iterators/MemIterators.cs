// Clauses 15.15.1 (synchronous and asynchronous iterators — general) and 15.15.4 (yield type).
// A function member whose body contains `yield` is an iterator: the body is not the member's
// implementation but a description of one, and the compiler builds a nested class that *is* the
// enumerator. So, exactly as with `async`, one source declaration becomes a member and a type —
// and here the generated type implements up to five interfaces the source never names.
//
// A method is not the only thing an iterator may be. Clause 15.15.1 admits any function member
// with a body that can return an enumerable or enumerator type, so this file writes an iterator
// **method**, an iterator **property getter**, an iterator **indexer getter** and an iterator
// **operator** — and the last two are the ones no one expects to compile.
//
// The hazard on 15.15.4 is the generated type's name. `MemSequence` declares `Walk()` and
// `Walk<TItem>(...)`, both iterators, so the type has two nested classes whose mangled names are
// `<Walk>d__0` and `<Walk>d__1` — one demangled name, two types, separated by an ordinal that
// counts every state machine and lambda in the type rather than the overloads of one name. The
// same ordinal separates `<Walk>d__0` from `<get_Rows>d__2`, which is a *property's* state
// machine and whose demangled name is `get_Rows` — a name clause 15.7 says is reserved and
// clause 15.15 makes into a type.
//
// The yield type itself is the second half of 15.15.4: `object` for the non-generic interfaces
// and the type argument for the generic ones, so the same `yield return 1;` has yield type
// `object` in one member and `int` in the next.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Classes.Members.Iterators;

/// <summary>15.15.1: iterator methods at every return type, and at two arities of one
/// name.</summary>
public class MemSequence
{
    private readonly int[] _slots = [1, 2, 3];

    /// <summary>15.15.1 with 15.15.4: an iterator returning <c>IEnumerable&lt;int&gt;</c>, whose
    /// yield type is <c>int</c>.</summary>
    public IEnumerable<int> Walk()
    {
        foreach (var slot in _slots)
        {
            yield return slot;
        }
    }

    /// <summary>15.15.1: the arity-one sibling. Two iterators, one name — so two generated
    /// types whose demangled names are both `Walk`.</summary>
    public IEnumerable<TItem> Walk<TItem>(TItem seed)
    {
        yield return seed;
        yield return seed;
    }

    /// <summary>15.15.4: an iterator returning the *non-generic* <c>IEnumerable</c>, whose
    /// yield type is <c>object</c> — so the two `yield return`s below are legal together and
    /// would not be in any generic overload.</summary>
    public IEnumerable Untyped()
    {
        yield return 1;
        yield return "two";
        yield return this;
    }

    /// <summary>15.15.4: an iterator returning <c>IEnumerator&lt;int&gt;</c> rather than an
    /// enumerable. The generated type is an enumerator only — there is no `GetEnumerator` on
    /// it — which is the one difference clause 15.15.5 and 15.15.6 are about.</summary>
    public IEnumerator<int> Cursor()
    {
        yield return _slots[0];
        yield return _slots[1];
    }

    /// <summary>15.15.4: the non-generic enumerator return, whose yield type is
    /// <c>object</c>.</summary>
    public IEnumerator Raw()
    {
        yield return _slots[0];
    }

    /// <summary>15.15.1: `yield break`, which ends the iteration without producing a value — the
    /// only statement in the language whose meaning is "return from a member that has already
    /// returned".</summary>
    public IEnumerable<int> Stopped(int limit)
    {
        if (limit < 0)
        {
            yield break;
        }

        for (var index = 0; index < limit; index++)
        {
            yield return index;

            if (index > 2)
            {
                yield break;
            }
        }
    }

    /// <summary>15.15.1: an iterator with a `try`/`finally`, whose `finally` runs when the
    /// enumerator is disposed rather than when the body returns — which is clause 15.15.5.4
    /// seen from the producing side.</summary>
    public IEnumerable<int> Guarded()
    {
        try
        {
            yield return 1;
            yield return 2;
        }
        finally
        {
            Finished = true;
        }
    }

    /// <summary>15.15.1: whether the `finally` above has run.</summary>
    public bool Finished { get; private set; }

    /// <summary>15.15.1 with 15.7.3: an iterator **property getter**. Its state machine's
    /// demangled name is `get_Rows`, which clause 15.3.10.2 reserves as a member name and this
    /// clause turns into a type name.</summary>
    public IEnumerable<int> Rows
    {
        get
        {
            yield return _slots[0];
            yield return _slots[2];
        }
    }

    /// <summary>15.15.1: every iterator above, consumed. Enumerating is what runs the bodies —
    /// before the first `MoveNext` none of these statements has executed.</summary>
    public string UseAll()
    {
        var total = 0;

        foreach (var value in Walk())
        {
            total += value;
        }

        foreach (var value in Walk("x"))
        {
            total += value.Length;
        }

        foreach (var value in Untyped())
        {
            total += value is int number ? number : 0;
        }

        using (var cursor = Cursor())
        {
            while (cursor.MoveNext())
            {
                total += cursor.Current;
            }
        }

        var raw = Raw();
        while (raw.MoveNext())
        {
            total += (int)raw.Current!;
        }

        foreach (var value in Stopped(4))
        {
            total += value;
        }

        foreach (var value in Guarded())
        {
            total += value;
        }

        foreach (var value in Rows)
        {
            total += value;
        }

        return $"{total} {Finished}";
    }
}

/// <summary>15.15.1: an iterator **indexer getter**, which is the least-expected place an
/// iterator block may appear. One indexer per type, so it gets a type of its own.</summary>
public class MemSequenceWindow
{
    /// <summary>15.15.1: the indexer's `get` is the iterator, so the generated state machine's
    /// demangled name is `get_Item`.</summary>
    public IEnumerable<int> this[int count]
    {
        get
        {
            for (var index = 0; index < count; index++)
            {
                yield return index * index;
            }
        }
    }

    /// <summary>15.15.1: an element access that enumerates.</summary>
    public int UseAll()
    {
        var total = 0;

        foreach (var value in this[3])
        {
            total += value;
        }

        return total;
    }
}

/// <summary>15.15.1: an iterator **operator**. `a + b` produces a lazy sequence, so the
/// operator's body has no `return` and the member is `op_Addition`.</summary>
public readonly struct MemSequencePair
{
    /// <summary>What this half of the pair carries.</summary>
    public int Value { get; }

    /// <summary>Fixes the value.</summary>
    public MemSequencePair(int value) => Value = value;

    /// <summary>15.15.1 with 15.10.3: an operator implemented by an iterator block. Its state
    /// machine's demangled name is `op_Addition`.</summary>
    public static IEnumerable<int> operator +(MemSequencePair left, MemSequencePair right)
    {
        yield return left.Value;
        yield return right.Value;
        yield return left.Value + right.Value;
    }

    /// <summary>15.15.1: the operator, enumerated.</summary>
    public static int UseAll()
    {
        var total = 0;

        foreach (var value in new MemSequencePair(1) + new MemSequencePair(2))
        {
            total += value;
        }

        return total;
    }
}

/// <summary>15.15.1: the asynchronous iterators — `async` and `yield` in one body, which is the
/// only member shape that generates a state machine serving both clause 15.14 and clause
/// 15.15.</summary>
public class MemAsyncSequence
{
    /// <summary>15.15.1: an async iterator. Its yield type is <c>int</c> and its return type is
    /// <c>IAsyncEnumerable&lt;int&gt;</c>, so it is awaited *and* enumerated.</summary>
    public async IAsyncEnumerable<int> WalkAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await Task.Yield();
            yield return index;
        }
    }

    /// <summary>15.15.1: an async iterator with a cancellation token the compiler threads into
    /// the generated `GetAsyncEnumerator` — the attribute is how one parameter of an iterator
    /// becomes a parameter of a *different*, generated member.</summary>
    public async IAsyncEnumerable<string> LabelledAsync(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < count; index++)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return index.ToString();
        }
    }

    /// <summary>15.15.1: an async iterator returning <c>IAsyncEnumerator&lt;int&gt;</c> rather
    /// than an enumerable, which is legal and which `await foreach` cannot consume
    /// directly.</summary>
    public async IAsyncEnumerator<int> CursorAsync()
    {
        await Task.Yield();
        yield return 1;
    }

    /// <summary>15.15.1: `await foreach` over both async enumerables, and a hand-driven walk of
    /// the async enumerator.</summary>
    public async Task<string> UseAll()
    {
        var total = 0;

        await foreach (var value in WalkAsync(3).ConfigureAwait(false))
        {
            total += value;
        }

        await foreach (var label in LabelledAsync(2).WithCancellation(CancellationToken.None).ConfigureAwait(false))
        {
            total += label.Length;
        }

        var cursor = CursorAsync();

        try
        {
            while (await cursor.MoveNextAsync().ConfigureAwait(false))
            {
                total += cursor.Current;
            }
        }
        finally
        {
            await cursor.DisposeAsync().ConfigureAwait(false);
        }

        return $"{total}";
    }
}
