using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Surface.Synthesised;

/// <summary>Methods whose bodies the compiler rewrites into whole types (13.15, 15.15).</summary>
/// <remarks>
/// Every member here makes the compiler emit a <em>type</em> that no source declares:
/// <list type="bullet">
///   <item><see cref="Steps"/> — an iterator, rewritten into a nested class
///         <c>&lt;Steps&gt;d__0</c> implementing <c>IEnumerable&lt;int&gt;</c>,
///         <c>IEnumerator&lt;int&gt;</c> and <c>IDisposable</c>, with <c>MoveNext</c>,
///         <c>Current</c>, <c>Dispose</c>, <c>Reset</c> and two <c>GetEnumerator</c>s</item>
///   <item><see cref="SumAsync"/> — an async method, rewritten into a struct state machine
///         with <c>MoveNext</c> and <c>SetStateMachine</c></item>
///   <item><see cref="StreamAsync"/> — an async iterator, which is both at once</item>
///   <item><see cref="Deferred"/> — a lambda over a local, rewritten into a display class
///         <c>&lt;&gt;c__DisplayClass</c> with a field per captured local</item>
///   <item><see cref="Cached"/> — a <c>static</c> lambda, cached in a singleton
///         <c>&lt;&gt;c</c> with a method named <c>&lt;Cached&gt;b__0</c></item>
///   <item><see cref="WithLocal"/> — a local function, lowered to
///         <c>&lt;WithLocal&gt;g__Tally|5_0</c> beside a ref struct closure</item>
/// </list>
/// None of these types has a declaration, a name a C# identifier can spell, or a
/// containing namespace a source walk would attribute them to; all of them are members of
/// <see cref="SynMachines"/> in metadata. An index built from syntax records the six
/// methods and none of the types, which is a missing answer rather than a wrong one — and
/// exactly the asymmetry the corpus is for.
/// </remarks>
public class SynMachines
{
    /// <summary>An iterator: <c>yield return</c> and <c>yield break</c> (13.15).</summary>
    /// <param name="count">How many steps to take.</param>
    /// <returns>The steps.</returns>
    public IEnumerable<int> Steps(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (i > 8)
            {
                yield break;
            }

            yield return i * i;
        }
    }

    /// <summary>An iterator returning the non-generic interface.</summary>
    /// <returns>Boxed steps.</returns>
    public System.Collections.IEnumerable Boxed()
    {
        yield return 1;
        yield return "two";
    }

    /// <summary>An iterator property, whose accessor is the iterator.</summary>
    public IEnumerator<string> Names
    {
        get
        {
            yield return "alpha";
            yield return "beta";
        }
    }

    /// <summary>An async method (15.15.1).</summary>
    /// <param name="count">How many steps to sum.</param>
    /// <returns>The sum.</returns>
    public async Task<int> SumAsync(int count)
    {
        var total = 0;

        foreach (var step in Steps(count))
        {
            total += step;
            await Task.Yield();
        }

        return total;
    }

    /// <summary>An <c>async void</c> method, whose state machine has no result field.</summary>
    public async void FireAndForget()
    {
        await Task.CompletedTask;
    }

    /// <summary>An async iterator: a state machine that is also an enumerator.</summary>
    /// <returns>The steps, asynchronously.</returns>
    public async IAsyncEnumerable<int> StreamAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    /// <summary>Consumes the async iterator, which needs <c>await foreach</c>.</summary>
    /// <returns>How many items arrived.</returns>
    public async Task<int> CountStreamAsync()
    {
        var seen = 0;

        await foreach (var item in StreamAsync())
        {
            seen += item;
        }

        return seen;
    }

    /// <summary>A lambda capturing a local, which forces a display class.</summary>
    /// <param name="seed">Captured by the returned lambda.</param>
    /// <returns>A closure over <paramref name="seed"/>.</returns>
    public Func<int, int> Deferred(int seed)
    {
        var scale = seed * 2;

        return value => value * scale + seed;
    }

    /// <summary>A <c>static</c> lambda, which captures nothing and so is cached.</summary>
    /// <returns>The cached delegate.</returns>
    public static Func<int, int> Cached() => static value => value + 1;

    /// <summary>An anonymous method — the C# 2 spelling of the same synthesis.</summary>
    /// <returns>A predicate.</returns>
    public static Predicate<int> Anonymous() => delegate(int value) { return value > 0; };

    /// <summary>A parameterless anonymous method, which binds to any argument list.</summary>
    /// <returns>An action.</returns>
    public static Action Untyped() => delegate { };

    /// <summary>A local function and a static local function (13.6.4).</summary>
    /// <param name="count">How far to tally.</param>
    /// <returns>The tally.</returns>
    public int WithLocal(int count)
    {
        var offset = 1;

        int Tally(int upTo)
        {
            var running = 0;

            for (var i = 0; i < upTo; i++)
            {
                running += i + offset;
            }

            return running;
        }

        static int Doubled(int value) => value * 2;

        return Doubled(Tally(count));
    }

    /// <summary>A local function that is itself an iterator, nesting the two rewrites.</summary>
    /// <returns>The nested steps.</returns>
    public IEnumerable<int> NestedIterator()
    {
        return Inner();

        static IEnumerable<int> Inner()
        {
            yield return 42;
        }
    }
}
