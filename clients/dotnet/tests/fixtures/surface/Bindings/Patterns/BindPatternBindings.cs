using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Bindings.Patterns;

/// <summary>The enumerator half of a pattern-based sequence.</summary>
public struct BindCursor
{
    private int _at;

    /// <summary>The current item. Bound by <c>foreach</c> and named by nothing.</summary>
    public int Current => _at;

    /// <summary>Advances. Bound by <c>foreach</c> and named by nothing.</summary>
    public bool MoveNext() => ++_at < 4;
}

/// <summary>A sequence that satisfies the <c>foreach</c> pattern without an interface.</summary>
public sealed class BindSequence
{
    /// <summary>The pattern's entry point. Bound by <c>foreach</c>, named by nothing.</summary>
    public BindCursor GetEnumerator() => default;
}

/// <summary>A <c>ref struct</c> whose <c>Dispose</c> is found by pattern, not by interface.</summary>
public ref struct BindScope
{
    /// <summary>Bound by a <c>using</c> statement, named by nothing.</summary>
    public void Dispose()
    {
    }
}

/// <summary>A collection whose initializer calls <c>Add</c> by pattern.</summary>
public sealed class BindBag : IEnumerable
{
    private readonly System.Collections.Generic.List<int> _items = [];

    /// <summary>How many.</summary>
    public int Count => _items.Count;

    /// <summary>Bound by a collection initializer, named by nothing.</summary>
    /// <param name="item">What to add.</param>
    public void Add(int item) => _items.Add(item);

    /// <summary>Required for a collection initializer to be legal.</summary>
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
}

/// <summary>A type with a <c>Deconstruct</c>, reached by two different patterns.</summary>
public readonly struct BindPoint
{
    /// <summary>Builds a point.</summary>
    /// <param name="x">Across.</param>
    /// <param name="y">Down.</param>
    public BindPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Across.</summary>
    public int X { get; }

    /// <summary>Down.</summary>
    public int Y { get; }

    /// <summary>
    /// Bound by a deconstructing declaration and by a positional pattern, and named by
    /// neither.
    /// </summary>
    /// <param name="x">Across.</param>
    /// <param name="y">Down.</param>
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }
}

/// <summary>A type that satisfies the range-indexer pattern with <c>Length</c> + <c>Slice</c>.</summary>
public sealed class BindRun
{
    /// <summary>Half of the range pattern. Bound by <c>run[1..]</c>, named by nothing.</summary>
    public int Length => 4;

    /// <summary>
    /// The other half. Called <c>Slice</c> because the pattern requires that name, and
    /// unrelated to <c>Surface.Bindings.Overloads.BindSliceExtensions.Slice</c>.
    /// </summary>
    /// <param name="start">Where.</param>
    /// <param name="length">How much.</param>
    public BindRun Slice(int start, int length) => this;
}

/// <summary>An awaiter, found by the <c>await</c> pattern.</summary>
public sealed class BindAwaiter : INotifyCompletion
{
    /// <summary>Bound by <c>await</c>, named by nothing.</summary>
    public bool IsCompleted => true;

    /// <summary>Bound by <c>await</c>, named by nothing.</summary>
    /// <param name="continuation">What to run.</param>
    public void OnCompleted(Action continuation) => continuation();

    /// <summary>Bound by <c>await</c>, named by nothing.</summary>
    public int GetResult() => 5;
}

/// <summary>An awaitable, found by the <c>await</c> pattern.</summary>
public sealed class BindAwaitable
{
    /// <summary>Bound by <c>await</c>, named by nothing.</summary>
    public BindAwaiter GetAwaiter() => new();
}

/// <summary>
/// M29 — six constructs whose meaning is a member the compiler found by pattern, each
/// needing its own <c>SemanticModel</c> API to recover, and none of which produces a
/// reference row.
/// </summary>
/// <remarks>
/// <para>
/// <c>Indexer.IndexTree</c> dispatches references on <c>SimpleNameSyntax</c>. Every member
/// bound below is bound without being named, so there is no name node to dispatch — and
/// this was measured rather than reasoned: enumerating every <c>SimpleNameSyntax</c> inside
/// the ten methods here yields <c>var</c>, the receivers, the type names in <c>new</c>, the
/// locals, and <c>Count</c>. Not one of <c>GetEnumerator</c>, <c>MoveNext</c>,
/// <c>Current</c>, <c>Dispose</c>, <c>GetAwaiter</c>, <c>IsCompleted</c>,
/// <c>GetResult</c>, <c>Deconstruct</c>, <c>Add</c>, <c>Length</c> or <c>Slice</c> appears.
/// So each of those declarations has a definition row and an empty
/// <c>csharp.EntityRef</c> fan-out, corpus-wide.
/// </para>
/// <para>
/// <b>Recovering them is six different questions.</b> <c>GetForEachStatementInfo</c>,
/// the type's own <c>GetMembers("Dispose")</c>, <c>GetAwaitExpressionInfo</c>,
/// <c>GetDeconstructionInfo</c>, the pattern's <c>IOperation</c>, and
/// <c>GetCollectionInitializerSymbolInfo</c> — six APIs, no two alike, and the walk calls
/// none of them. Which is why this is a missing answer rather than a wrong one: nothing is
/// written, nothing conflicts, and the gap is only visible against a fixture that says what
/// should have been there.
/// </para>
/// <para>
/// <b>The <c>foreach</c> target is decided by a fall-back chain, which is worth pinning
/// separately.</b> Over <see cref="BindSequence"/> the pattern binds
/// <c>BindSequence.GetEnumerator</c>, <c>BindCursor.MoveNext</c> and
/// <c>BindCursor.Current</c> — three members on two types, one of them a struct — while over
/// an <c>IEnumerable&lt;int&gt;</c> the same statement binds <c>MoveNext</c> off the
/// <i>non-generic</i> <c>IEnumerator</c> and <c>Current</c> off the generic one. So a
/// producer that recovered the members from one interface would get the pair from two
/// different interfaces wrong, and <see cref="Interfaced"/> is here so that both shapes are
/// in the corpus.
/// </para>
/// <para>
/// <b>And <c>var</c> in a deconstruction names a tuple nobody wrote.</b> The <c>var</c> of
/// <c>var (x, y) = point</c> binds to <c>(int x, int y)</c> — measured — so it writes a
/// <c>typeRef</c> to a constructed <c>ValueTuple</c>, at a span whose text is three
/// characters. That is M14's mechanism reached through M29's syntax, and it is noted here
/// so a reader does not attribute it to either alone.
/// </para>
/// </remarks>
public static class BindPatternBindings
{
    /// <summary>Pattern-based <c>foreach</c>: three members bound, no rows.</summary>
    /// <param name="sequence">The sequence.</param>
    public static int Looped(BindSequence sequence)
    {
        var total = 0;

        foreach (var item in sequence)
        {
            total += item;
        }

        return total;
    }

    /// <summary>
    /// Interface-based <c>foreach</c>: <c>MoveNext</c> off <c>IEnumerator</c> and
    /// <c>Current</c> off <c>IEnumerator&lt;int&gt;</c>, and no rows for either.
    /// </summary>
    /// <param name="source">The sequence.</param>
    public static int Interfaced(System.Collections.Generic.IEnumerable<int> source)
    {
        var total = 0;

        foreach (var item in source)
        {
            total += item;
        }

        return total;
    }

    /// <summary>A <c>using</c> statement: <c>Dispose</c> bound by pattern, no row.</summary>
    public static int Scoped()
    {
        using (var scope = new BindScope())
        {
            return 1;
        }
    }

    /// <summary>A <c>using</c> declaration: the same binding, the same absence.</summary>
    public static int Declared()
    {
        using var scope = new BindScope();

        return 2;
    }

    /// <summary>A collection initializer: two <c>Add</c> calls, no rows.</summary>
    public static int Built() => new BindBag { 1, 2 }.Count;

    /// <summary>A deconstructing declaration: <c>Deconstruct</c> bound, no row.</summary>
    /// <param name="point">The point.</param>
    public static int Torn(BindPoint point)
    {
        var (x, y) = point;

        return x + y;
    }

    /// <summary>A positional pattern: the same <c>Deconstruct</c>, no row.</summary>
    /// <param name="point">The point.</param>
    public static bool Matched(BindPoint point) => point is (1, 2);

    /// <summary>A range index: <c>Length</c> and <c>Slice</c> bound, no rows.</summary>
    /// <param name="run">The run.</param>
    public static BindRun Tail(BindRun run) => run[1..];

    /// <summary>An <c>await</c>: four members bound, no rows.</summary>
    public static async Task<int> Waited() => await new BindAwaitable();

    /// <summary>
    /// The control: the same members called by name, where every one <i>is</i> a row.
    /// </summary>
    /// <remarks>
    /// Line for line, the members the constructs above bind. So the difference between this
    /// method's reference count and the sum of the others' is the whole of M29, and it is a
    /// subtraction rather than an argument.
    /// </remarks>
    /// <param name="sequence">The sequence.</param>
    /// <param name="point">The point.</param>
    /// <param name="run">The run.</param>
    public static int ByName(BindSequence sequence, BindPoint point, BindRun run)
    {
        var cursor = sequence.GetEnumerator();
        var total = 0;

        while (cursor.MoveNext())
        {
            total += cursor.Current;
        }

        var scope = new BindScope();
        scope.Dispose();

        var bag = new BindBag();
        bag.Add(1);

        point.Deconstruct(out var x, out var y);

        var awaiter = new BindAwaitable().GetAwaiter();
        total += awaiter.IsCompleted ? awaiter.GetResult() : 0;

        return total + x + y + run.Length + run.Slice(1, 2).Length + bag.Count;
    }
}
