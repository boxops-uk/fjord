// The collection vocabulary clause 13.9.5 binds to. No `foreach` in this file.
//
// `foreach` finds its enumerator by *pattern* first: a public parameterless `GetEnumerator`
// on the collection type, whose result has a `Current` property and a `MoveNext` returning
// bool. Only if that fails does it fall back to `IEnumerable<T>`, and only then to
// `IEnumerable` with a conversion inserted on the iteration variable. So five different
// collection shapes bind five different ways, and none of `GetEnumerator`, `Current`,
// `MoveNext` or `Dispose` is written at any `foreach` in this project. They are all declared
// here so a reference edge has somewhere to land.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>
/// Clause 13.9.5.2's first form: a collection with a <c>GetEnumerator</c> and no interface.
/// The pattern is structural — nothing here says "enumerable".
/// </summary>
public sealed class StmtRoster
{
    private readonly string[] _names;

    /// <summary>Builds a roster.</summary>
    /// <param name="names">The names on it.</param>
    public StmtRoster(params string[] names) => _names = names;

    /// <summary>How many names are on the roster.</summary>
    public int Count => _names.Length;

    /// <summary>
    /// The pattern <c>GetEnumerator</c>. A <c>foreach</c> over a
    /// <see cref="StmtRoster"/> is a reference to this method that writes no name.
    /// </summary>
    /// <returns>A cursor over the names.</returns>
    public StmtRosterCursor GetEnumerator() => new(_names);
}

/// <summary>
/// The enumerator <see cref="StmtRoster.GetEnumerator"/> hands back. It implements no
/// interface either: <c>Current</c> and <c>MoveNext</c> are enough, and it has no
/// <c>Dispose</c>, which the pattern also permits.
/// </summary>
public sealed class StmtRosterCursor
{
    private readonly string[] _names;
    private int _index = -1;

    internal StmtRosterCursor(string[] names) => _names = names;

    /// <summary>The name at the cursor. Bound by <c>foreach</c> without being named.</summary>
    public string Current => _names[_index];

    /// <summary>Advances the cursor.</summary>
    /// <returns>Whether there is a name at the new position.</returns>
    public bool MoveNext() => ++_index < _names.Length;
}

/// <summary>
/// A collection whose enumerator is a <c>ref struct</c> — the shape <c>Span&lt;T&gt;</c> has,
/// and the reason clause 13.9.5.2's pattern lookup cannot be replaced by the interface: a
/// <c>ref struct</c> cannot implement <see cref="IEnumerator{T}"/>.
/// </summary>
public readonly struct StmtSpanRun
{
    private readonly int[] _cells;

    /// <summary>Wraps the cells to be walked.</summary>
    /// <param name="cells">The cells.</param>
    public StmtSpanRun(int[] cells) => _cells = cells;

    /// <summary>The pattern <c>GetEnumerator</c>, returning a <c>ref struct</c>.</summary>
    /// <returns>An enumerator that cannot be boxed or stored.</returns>
    public StmtSpanRunEnumerator GetEnumerator() => new(_cells);
}

/// <summary>
/// A <c>ref struct</c> enumerator with a pattern <c>Dispose</c>. <c>foreach</c> calls the
/// <c>Dispose</c> of a <c>ref struct</c> enumerator by pattern, so this type is a reference
/// target reached with no name and no interface at either end.
/// </summary>
public ref struct StmtSpanRunEnumerator
{
    private readonly Span<int> _cells;
    private int _index;

    internal StmtSpanRunEnumerator(int[] cells)
    {
        _cells = cells;
        _index = -1;
    }

    /// <summary>The cell at the cursor.</summary>
    public int Current => _cells[_index];

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    public bool Closed { get; private set; }

    /// <summary>Advances the cursor.</summary>
    /// <returns>Whether there is a cell at the new position.</returns>
    public bool MoveNext() => ++_index < _cells.Length;

    /// <summary>The pattern <c>Dispose</c> a <c>foreach</c> over this enumerator calls.</summary>
    public void Dispose() => Closed = true;
}

/// <summary>
/// Clause 13.9.5.2's third form: a collection that implements only the non-generic
/// <see cref="IEnumerable"/>, so the iteration type is <c>object</c> and a
/// <c>foreach (string s in …)</c> over it carries an explicit reference conversion the
/// compiler inserts and the source does not spell.
/// </summary>
public sealed class StmtLegacyBag : IEnumerable
{
    private readonly object[] _items;

    /// <summary>Builds a bag.</summary>
    /// <param name="items">What goes in it.</param>
    public StmtLegacyBag(params object[] items) => _items = items;

    /// <summary>The non-generic enumerator, which is all this type offers.</summary>
    /// <returns>An enumerator whose <c>Current</c> is <c>object</c>.</returns>
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
}

/// <summary>A pair, deconstructable, for clause 13.9.5.4's deconstructing <c>foreach</c>.</summary>
public readonly struct StmtPair
{
    /// <summary>Makes a pair.</summary>
    /// <param name="key">The key.</param>
    /// <param name="weight">The weight.</param>
    public StmtPair(string key, int weight)
    {
        Key = key;
        Weight = weight;
    }

    /// <summary>The key.</summary>
    public string Key { get; }

    /// <summary>The weight.</summary>
    public int Weight { get; }

    /// <summary>
    /// The member a deconstructing <c>foreach</c> binds. `foreach (var (key, weight) in …)`
    /// writes neither `Deconstruct` nor a type name, and this is what it reaches.
    /// </summary>
    /// <param name="key">Receives the key.</param>
    /// <param name="weight">Receives the weight.</param>
    public void Deconstruct(out string key, out int weight)
    {
        key = Key;
        weight = Weight;
    }
}

/// <summary>
/// Clause 13.9.5.2's second form: a collection that implements
/// <see cref="IEnumerable{T}"/> properly, over an element type that deconstructs.
/// </summary>
public sealed class StmtPairBook : IEnumerable<StmtPair>
{
    private readonly List<StmtPair> _pairs = [];

    /// <summary>Adds a pair to the book.</summary>
    /// <param name="key">The key.</param>
    /// <param name="weight">The weight.</param>
    public void Add(string key, int weight) => _pairs.Add(new StmtPair(key, weight));

    /// <summary>The generic enumerator, reached through the interface.</summary>
    /// <returns>An enumerator over the pairs.</returns>
    public IEnumerator<StmtPair> GetEnumerator() => _pairs.GetEnumerator();

    /// <summary>The non-generic enumerator, which the generic one is required to carry.</summary>
    /// <returns>The same enumerator, unhelpfully typed.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Clause 13.9.5.3's collection, bound by pattern: a <c>GetAsyncEnumerator</c> that takes an
/// optional <see cref="CancellationToken"/>, and no <c>IAsyncEnumerable&lt;T&gt;</c> in sight.
/// </summary>
public sealed class StmtTicker
{
    private readonly int _ticks;

    /// <summary>Builds a ticker.</summary>
    /// <param name="ticks">How many ticks it will produce.</param>
    public StmtTicker(int ticks) => _ticks = ticks;

    /// <summary>The pattern <c>GetAsyncEnumerator</c> an <c>await foreach</c> binds.</summary>
    /// <param name="token">Cancellation, defaulted so the pattern matches.</param>
    /// <returns>An asynchronous cursor.</returns>
    public StmtTickerCursor GetAsyncEnumerator(CancellationToken token = default) =>
        new(_ticks, token);
}

/// <summary>
/// The asynchronous cursor: <c>Current</c>, a <c>MoveNextAsync</c> returning an awaitable
/// bool, and a <c>DisposeAsync</c>. No interface, so every one of the three is reached by
/// pattern.
/// </summary>
public sealed class StmtTickerCursor
{
    private readonly int _ticks;
    private readonly CancellationToken _token;
    private int _index = -1;

    internal StmtTickerCursor(int ticks, CancellationToken token)
    {
        _ticks = ticks;
        _token = token;
    }

    /// <summary>The tick at the cursor.</summary>
    public int Current => _index;

    /// <summary>Whether <see cref="DisposeAsync"/> has run.</summary>
    public bool Closed { get; private set; }

    /// <summary>Advances the cursor, asynchronously.</summary>
    /// <returns>Whether there is a tick at the new position.</returns>
    public ValueTask<bool> MoveNextAsync()
    {
        _token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(++_index < _ticks);
    }

    /// <summary>The pattern <c>DisposeAsync</c> the loop calls when it finishes.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        Closed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The post-standard extension form of clause 13.9.5.2's pattern: a <c>GetEnumerator</c>
/// declared as an extension method, which lets <c>foreach</c> walk a type whose own
/// declaration knows nothing about enumeration — here, <see cref="int"/>.
/// </summary>
public static class StmtForeachExtensions
{
    /// <summary>
    /// Makes <c>foreach (int i in 3)</c> legal. The receiver is a value type in the
    /// framework, so the collection of a <c>foreach</c> can now bind to a method in a
    /// completely different assembly from the one holding the collection's type.
    /// </summary>
    /// <param name="count">How many numbers to yield.</param>
    /// <returns>An enumerator over <c>0 .. count - 1</c>.</returns>
    public static IEnumerator<int> GetEnumerator(this int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }
}
