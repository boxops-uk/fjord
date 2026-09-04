// Clauses 15.15.3 (enumerable interfaces), 15.15.6 (enumerable objects) — 15.15.6.1 general and
// 15.15.6.2 the `GetEnumerator` or `GetAsyncEnumerator` method.
//
// 15.15.6.2 is the census row with the plainest hazard in the whole area, and it is plain
// because every collection ever written has it:
//
//   **`GetEnumerator` is declared twice in one type, with no parameters either time.**
//   `IEnumerable<T>.GetEnumerator()` returns `IEnumerator<T>` and `IEnumerable.GetEnumerator()`
//   returns `IEnumerator`. They differ *only in return type*, which is not part of a signature —
//   so the generic one is implemented implicitly and the non-generic one has to be explicit.
//   One source name, zero parameters, one containing type, two members. An index keyed on
//   (type, name, parameter types) mints one string for both, and the two rows it should hold are
//   `GetEnumerator` and `System.Collections.IEnumerable.GetEnumerator`.
//
// `MemRange` writes that pair three deep: a *public struct-returning* `GetEnumerator` beside
// both interface implementations, which is the shape the framework's collections use so that
// `foreach` binds to the struct and never to either interface. Three members named
// `GetEnumerator`, no parameters on any of them, three different return types, and the one
// `foreach` picks is the one that implements no interface at all.
//
// 15.15.6.2's other half is that `foreach` does not need an interface: it binds `GetEnumerator`
// **by name**, so `MemDuckRange` is enumerable while implementing nothing, and `MemAsyncRange`
// is `await foreach`-able through `GetAsyncEnumerator` alone.

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Classes.Members.Iterators;

/// <summary>15.15.3 and 15.15.6.2: the three-`GetEnumerator` shape. The public one returns a
/// struct so `foreach` allocates nothing; the two interface ones exist for callers holding an
/// interface.</summary>
public sealed class MemRange : IEnumerable<int>
{
    private readonly int _limit;

    /// <summary>15.15.6.1: takes the bound.</summary>
    public MemRange(int limit) => _limit = limit;

    /// <summary>15.15.6.2: the `GetEnumerator` `foreach` binds to — found by name, returning a
    /// type that implements the enumerator interfaces but is not one of them. This member
    /// satisfies no interface.</summary>
    public MemStructCursor GetEnumerator() => new MemStructCursor(_limit);

    /// <summary>15.15.6.2: the generic interface's `GetEnumerator`, implemented explicitly
    /// because the public one above already took the name with a different return type.</summary>
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new MemCursor(Slots());

    /// <summary>15.15.6.2: the non-generic interface's `GetEnumerator`, also explicit. Third
    /// member of this name in this type, and third return type.</summary>
    IEnumerator IEnumerable.GetEnumerator() => new MemCursor(Slots());

    /// <summary>15.15.6.1: the storage the two interface enumerators walk.</summary>
    private int[] Slots()
    {
        var slots = new int[_limit];

        for (var index = 0; index < _limit; index++)
        {
            slots[index] = index;
        }

        return slots;
    }
}

/// <summary>15.15.3: the ordinary two-member shape — a public generic `GetEnumerator` beside an
/// explicit non-generic one, which is what an iterator block generates and what most
/// hand-written collections declare.</summary>
public sealed class MemLabels : IEnumerable<string>
{
    private readonly List<string> _labels = ["a", "bb", "ccc"];

    /// <summary>15.15.6.2: the generic one, implicitly — so it is both the interface
    /// implementation and the member `foreach` binds to.</summary>
    public IEnumerator<string> GetEnumerator() => _labels.GetEnumerator();

    /// <summary>15.15.6.2: the non-generic one, explicitly, delegating to the member above.
    /// `GetEnumerator()` calling `GetEnumerator()` is a call from one member to another of the
    /// same name in the same type.</summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>15.15.6.1: how many there are, so the type is not only its enumerators.</summary>
    public int Count => _labels.Count;
}

/// <summary>15.15.6.2: an enumerable that implements nothing. `foreach` binds `GetEnumerator` by
/// name, so this compiles, and a query asked "what interfaces make this enumerable" must answer
/// none.</summary>
public sealed class MemDuckRange
{
    private readonly int _limit;

    /// <summary>15.15.6.1: takes the bound.</summary>
    public MemDuckRange(int limit) => _limit = limit;

    /// <summary>15.15.6.2: the only member `foreach` needs — one name, no interface behind
    /// it.</summary>
    public MemStructCursor GetEnumerator() => new MemStructCursor(_limit);
}

/// <summary>15.15.6.2: an enumerable made so by an *extension* method, which is the furthest the
/// name-based binding goes — `foreach` will use an extension `GetEnumerator` when the type has
/// no instance one.</summary>
public sealed class MemBareRange
{
    /// <summary>15.15.6.1: the bound, read by the extension below.</summary>
    public int Limit { get; init; } = 3;
}

/// <summary>15.15.6.2: the extension that makes <see cref="MemBareRange"/> enumerable. The
/// declaration is in this type and the reference reads as a member of the other one.</summary>
public static class MemBareRangeExtensions
{
    /// <summary>15.15.6.2: an extension `GetEnumerator`, which `foreach` finds by name in
    /// extension scope.</summary>
    public static MemStructCursor GetEnumerator(this MemBareRange range) => new MemStructCursor(range.Limit);
}

/// <summary>15.15.3 with 15.14: an async enumerable, whose one required member takes a
/// cancellation token — so `GetAsyncEnumerator` is the only member in these two clauses whose
/// parameter list is not empty.</summary>
public sealed class MemAsyncRange : IAsyncEnumerable<int>
{
    private readonly int _limit;

    /// <summary>15.15.6.1: takes the bound.</summary>
    public MemAsyncRange(int limit) => _limit = limit;

    /// <summary>15.15.6.2: `GetAsyncEnumerator`, implemented implicitly, so it is both the
    /// interface member and what `await foreach` binds to. There is no non-generic async
    /// enumerable interface, so this name is declared once and not twice.</summary>
    public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new MemAsyncCursor(_limit);
}

/// <summary>15.15.6.2: an async enumerable that implements nothing, enumerated by name
/// alone.</summary>
public sealed class MemDuckAsyncRange
{
    /// <summary>15.15.6.2: the only member `await foreach` needs.</summary>
    public MemAsyncCursor GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new MemAsyncCursor(2);
}

/// <summary>15.15.6.1: the references. Each `foreach` below binds one of the several
/// `GetEnumerator` declarations above, and which one it binds is the fact these rows are
/// about.</summary>
public static class MemEnumerableUse
{
    /// <summary>15.15.6.2: `foreach` over <see cref="MemRange"/> binds the *public* member and
    /// neither interface one; the two casts then reach the two it skipped. Three references to
    /// three members named `GetEnumerator` in one method.</summary>
    public static string ThreeWays()
    {
        var range = new MemRange(3);
        var total = 0;

        foreach (var value in range)
        {
            total += value;
        }

        foreach (var value in (IEnumerable<int>)range)
        {
            total += value;
        }

        foreach (var value in (IEnumerable)range)
        {
            total += value is int number ? number : 0;
        }

        return $"{total}";
    }

    /// <summary>15.15.3: the ordinary shape, enumerated through the type and through the
    /// non-generic interface.</summary>
    public static string Ordinary()
    {
        var labels = new MemLabels();
        var total = labels.Count;

        foreach (var label in labels)
        {
            total += label.Length;
        }

        foreach (var label in (IEnumerable)labels)
        {
            total += label is string text ? text.Length : 0;
        }

        return $"{total}";
    }

    /// <summary>15.15.6.2: the two enumerables that implement nothing — one with an instance
    /// `GetEnumerator` and one with only an extension.</summary>
    public static string ByName()
    {
        var total = 0;

        foreach (var value in new MemDuckRange(3))
        {
            total += value;
        }

        foreach (var value in new MemBareRange { Limit = 2 })
        {
            total += value;
        }

        return $"{total}";
    }

    /// <summary>15.15.6.2: `await foreach` over both async enumerables, one through the
    /// interface and one by name.</summary>
    public static async Task<string> AsyncWays()
    {
        var total = 0;

        await foreach (var value in new MemAsyncRange(3).ConfigureAwait(false))
        {
            total += value;
        }

        await foreach (var value in new MemDuckAsyncRange())
        {
            total += value;
        }

        return $"{total}";
    }
}
