// Clauses 15.15.2 (enumerator interfaces) and 15.15.5 (enumerator objects) — 15.15.5.1 general,
// 15.15.5.2 advance the enumerator, 15.15.5.3 retrieve the current value, 15.15.5.4 dispose of
// resources. What the compiler generates for an iterator block, written by hand instead, so that
// the members clause 15.15.5 describes are declarations an index can hold rather than mangled
// names it has to guess at.
//
// This is where the corpus's most ordinary identity collision lives, and it is ordinary because
// every hand-written enumerator in every codebase has it:
//
//   **`Current` is declared twice in one type.** `IEnumerator<T>.Current` returns `T` and
//   `IEnumerator.Current` returns `object`, and the two differ *only in return type*, which is
//   not part of a signature. So the generic one is implemented implicitly, as
//   `public int Current`, and the non-generic one has to be implemented *explicitly*, as
//   `object IEnumerator.Current`. Two members, one source name `Current`, no parameters on
//   either. In metadata the second is named `System.Collections.IEnumerator.Current` — dots and
//   all — so the separation is a name a C# reader would not recognise as one, and an index that
//   takes the declared identifier mints one string for both.
//
// The same shape appears once more here: `MemCursor` implements `IDisposable.Dispose` implicitly
// and `IEnumerator.Reset` implicitly, and `MemExplicitCursor` implements every one of the five
// members explicitly — so the file holds two enumerators over the same element type whose member
// names differ in nothing but qualification.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Surface.Classes.Members.Iterators;

/// <summary>15.15.5.1: a hand-written enumerator object. Five members: two `Current`s, a
/// `MoveNext`, a `Reset` and a `Dispose`.</summary>
public sealed class MemCursor : IEnumerator<int>
{
    private readonly int[] _slots;
    private int _position = -1;
    private bool _disposed;

    /// <summary>15.15.5.1: takes what it walks.</summary>
    public MemCursor(int[] slots) => _slots = slots;

    /// <summary>15.15.5.3: the generic `Current`, implemented implicitly. Reading it before the
    /// first `MoveNext` or after the last is undefined by the clause and returns zero
    /// here.</summary>
    public int Current => _position >= 0 && _position < _slots.Length ? _slots[_position] : 0;

    /// <summary>15.15.5.3: the non-generic `Current`, implemented **explicitly** because it
    /// differs from the member above in return type alone. Its metadata name is
    /// `System.Collections.IEnumerator.Current`.</summary>
    object IEnumerator.Current => Current;

    /// <summary>15.15.5.2: advance. Returns whether there is a value to retrieve, and the clause
    /// requires that it keep returning `false` once it has.</summary>
    public bool MoveNext()
    {
        if (_disposed || _position >= _slots.Length)
        {
            return false;
        }

        _position++;
        return _position < _slots.Length;
    }

    /// <summary>15.15.5.1: `Reset`, which the interface requires and which a generated iterator
    /// implements by throwing. This one actually resets.</summary>
    public void Reset() => _position = -1;

    /// <summary>15.15.5.4: dispose. For a generated enumerator this is what runs the `finally`
    /// blocks of a suspended iterator body; here it just latches.</summary>
    public void Dispose()
    {
        _disposed = true;
        _position = _slots.Length;
    }

    /// <summary>15.15.5.1: whether it has been disposed, so the latch has a reader.</summary>
    public bool Disposed => _disposed;
}

/// <summary>15.15.2: the same five members, every one implemented **explicitly**. So this type
/// has no public `Current`, no public `MoveNext` and no public `Dispose` — the members exist
/// only through the interfaces, and every reference to them needs a cast.</summary>
public sealed class MemExplicitCursor : IEnumerator<string>, IDisposable
{
    private readonly string[] _slots;
    private int _position = -1;

    /// <summary>15.15.2: takes what it walks.</summary>
    public MemExplicitCursor(string[] slots) => _slots = slots;

    /// <summary>15.15.5.3: the generic `Current`, explicitly — metadata name
    /// `System.Collections.Generic.IEnumerator&lt;System.String&gt;.Current`.</summary>
    string IEnumerator<string>.Current => _position >= 0 ? _slots[_position] : string.Empty;

    /// <summary>15.15.5.3: the non-generic `Current`, explicitly. Two explicit members named
    /// `Current` in one type, separated by which interface they name.</summary>
    object IEnumerator.Current => _position >= 0 ? _slots[_position] : string.Empty;

    /// <summary>15.15.5.2: advance, explicitly.</summary>
    bool IEnumerator.MoveNext()
    {
        if (_position >= _slots.Length)
        {
            return false;
        }

        _position++;
        return _position < _slots.Length;
    }

    /// <summary>15.15.5.1: reset, explicitly.</summary>
    void IEnumerator.Reset() => _position = -1;

    /// <summary>15.15.5.4: dispose, explicitly.</summary>
    void IDisposable.Dispose() => _position = _slots.Length;
}

/// <summary>15.15.2: a *struct* enumerator, which is what the framework's collections return —
/// so `foreach` over it copies rather than boxes, and the interface implementations are the slow
/// path that nothing in a `foreach` reaches.</summary>
public struct MemStructCursor : IEnumerator<int>
{
    private readonly int _limit;
    private int _position;

    /// <summary>15.15.2: takes the bound.</summary>
    public MemStructCursor(int limit)
    {
        _limit = limit;
        _position = -1;
    }

    /// <summary>15.15.5.3: the generic `Current`, implicitly.</summary>
    public int Current => _position;

    /// <summary>15.15.5.3: the non-generic one, explicitly, which boxes.</summary>
    object IEnumerator.Current => Current;

    /// <summary>15.15.5.2: advance.</summary>
    public bool MoveNext() => ++_position < _limit;

    /// <summary>15.15.5.1: reset.</summary>
    public void Reset() => _position = -1;

    /// <summary>15.15.5.4: a struct enumerator's dispose, which does nothing and is still
    /// called by `foreach`.</summary>
    public void Dispose()
    {
    }
}

/// <summary>15.15.2 with 15.14: a hand-written *async* enumerator, so `MoveNextAsync` and
/// `DisposeAsync` exist as declarations rather than as generated members.</summary>
public sealed class MemAsyncCursor : IAsyncEnumerator<int>
{
    private readonly int _limit;
    private int _position = -1;

    /// <summary>15.15.2: takes the bound.</summary>
    public MemAsyncCursor(int limit) => _limit = limit;

    /// <summary>15.15.5.3: `Current` on an async enumerator, which is synchronous — only the
    /// advance is awaited.</summary>
    public int Current => _position;

    /// <summary>15.15.5.2: advance, asynchronously. Returns `ValueTask&lt;bool&gt;` and not
    /// `bool`, which is the whole difference between the two enumerator interfaces.</summary>
    public async ValueTask<bool> MoveNextAsync()
    {
        await Task.Yield();
        return ++_position < _limit;
    }

    /// <summary>15.15.5.4: dispose, asynchronously.</summary>
    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        _position = _limit;
    }
}

/// <summary>15.15.5.1: the references. Every enumerator above is driven by hand, because that is
/// what `foreach` does and what only an explicit walk makes visible: `MoveNext`, then `Current`,
/// then `Dispose`.</summary>
public static class MemCursorUse
{
    /// <summary>15.15.5.2 through 15.15.5.4: the implicit members through the type, and the
    /// explicit non-generic `Current` through a cast — two references to two members that share
    /// the name `Current`.</summary>
    public static string Implicit()
    {
        using var cursor = new MemCursor([1, 2, 3]);
        var total = 0;

        while (cursor.MoveNext())
        {
            total += cursor.Current;
            total += (int)((IEnumerator)cursor).Current!;
        }

        cursor.Reset();
        return $"{total} {cursor.Disposed}";
    }

    /// <summary>15.15.2: the fully explicit enumerator, which cannot be driven without a cast —
    /// every member reference here goes through an interface type.</summary>
    public static string Explicit()
    {
        var cursor = new MemExplicitCursor(["a", "bb"]);
        var walker = (IEnumerator)cursor;
        var typed = (IEnumerator<string>)cursor;
        var total = 0;

        while (walker.MoveNext())
        {
            total += typed.Current.Length;
            total += walker.Current is string text ? text.Length : 0;
        }

        walker.Reset();
        ((IDisposable)cursor).Dispose();
        return $"{total}";
    }

    /// <summary>15.15.5.1: the struct enumerator, driven both directly and through its
    /// interface, so the boxing and non-boxing references to one member are both present.</summary>
    public static string Struct()
    {
        var cursor = new MemStructCursor(3);
        var total = 0;

        while (cursor.MoveNext())
        {
            total += cursor.Current;
        }

        cursor.Dispose();
        IEnumerator boxed = new MemStructCursor(2);
        while (boxed.MoveNext())
        {
            total += (int)boxed.Current!;
        }

        return $"{total}";
    }

    /// <summary>15.15.5.2: the async enumerator, driven by hand — which is what `await foreach`
    /// compiles to.</summary>
    public static async Task<string> Async()
    {
        var cursor = new MemAsyncCursor(3);
        var total = 0;

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
