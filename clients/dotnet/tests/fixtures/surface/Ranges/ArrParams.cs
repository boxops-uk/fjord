// Clause 17.3's third route into existence — "array instances can also be created implicitly
// as part of evaluating an argument list involving a parameter array" (15.6.2.4).
//
// This is the array creation with no syntax at all: `Sum(1, 2, 3)` builds an `int[]` that
// appears nowhere in the source, so the reference written at that call site is to `Sum` and
// the array is not in the output in any form. The declaration half *is* indexable — the
// parameter carries the array type in the method's signature string — which makes a parameter
// array the sharpest version of the clause-17 shape: the type is a fact, every instance of it
// is not.

using System;
using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>A delegate whose parameter is a parameter array, so the type is written in a
/// declaration that is not a method (17.3).</summary>
/// <param name="values">However many numbers the caller passes.</param>
/// <returns>Something derived from them.</returns>
public delegate int ArrFold(params int[] values);

/// <summary>
/// A type whose only indexer takes a parameter array, so an element access with three
/// arguments binds to a single-parameter member (17.3, 17.4).
/// </summary>
/// <remarks>
/// One indexer, deliberately. A second `this[...]` in the same type would mint the same
/// symbol string as this one — `ScipSymbols.Descriptor` sends a property through
/// `Name(symbol) + '.'` with no disambiguator and Roslyn names every indexer `this[]` — and
/// two declarations wanting one identity string is a refused write that ends the run. That
/// shape belongs to a quarantine project, not to this one.
/// </remarks>
public sealed class ArrParamsIndexed
{
    private readonly int[,] _grid = new int[4, 4];

    /// <summary>How many slots there are in total.</summary>
    public int Count => _grid.Length;

    /// <summary>
    /// An indexer whose parameter is a parameter array, so `grid[1, 2]` and `grid[1]` and
    /// `grid[]`... — the last of which is not legal, which is the one edge a parameter array
    /// does not smooth over.
    /// </summary>
    /// <param name="keys">The coordinates, however many are passed.</param>
    /// <returns>What is at those coordinates, or zero.</returns>
    public int this[params int[] keys] =>
        keys.Length == 2 ? _grid[keys[0], keys[1]] : 0;
}

/// <summary>
/// Parameter arrays at every declaration site that admits one, and every call form that fills
/// one in (17.3).
/// </summary>
public static class ArrParams
{
    /// <summary>17.3 — the classic parameter array of a value type.</summary>
    /// <param name="values">However many numbers the caller passes.</param>
    /// <returns>Their sum.</returns>
    public static int Sum(params int[] values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>
    /// 17.3 — a parameter array preceded by fixed parameters, which is the only position it
    /// may take.
    /// </summary>
    /// <param name="separator">What to join with.</param>
    /// <param name="parts">However many parts the caller passes.</param>
    /// <returns>The parts, joined.</returns>
    public static string Join(string separator, params object[] parts) =>
        string.Join(separator, parts);

    /// <summary>
    /// 17.3 — a parameter array whose element type is itself an array type, so the implicit
    /// creation is of an array of arrays.
    /// </summary>
    /// <param name="rows">However many rows the caller passes.</param>
    /// <returns>How many elements there are across all the rows.</returns>
    public static int Flatten(params string[][] rows)
    {
        var total = 0;

        foreach (var row in rows)
        {
            total += row.Length;
        }

        return total;
    }

    /// <summary>17.3 — a parameter array whose element type is a constructed generic.</summary>
    /// <param name="cells">However many cells the caller passes.</param>
    /// <returns>The first cell's value, or zero.</returns>
    public static int FirstValue(params ArrCell<int>[] cells) =>
        cells.Length == 0 ? 0 : cells[0].Value;

    /// <summary>
    /// 17.3 — every call form: none, one, several, an array passed directly, and an array
    /// built by an initializer at the call site. Only the last two put an array type in the
    /// source; the first three create one out of nothing.
    /// </summary>
    /// <returns>The five results added up.</returns>
    public static int EveryCallForm()
    {
        var none = Sum();
        var one = Sum(1);
        var several = Sum(1, 2, 3);
        var passed = Sum(ArrInitializers.Evens);
        var built = Sum(new[] { 4, 5 });

        return none + one + several + passed + built;
    }

    /// <summary>
    /// 17.3 — a parameter array reached through a delegate, where the call site's implicit
    /// creation is decided by the delegate's signature rather than by a method's.
    /// </summary>
    /// <returns>What the delegate made of three arguments.</returns>
    public static int ThroughADelegate()
    {
        ArrFold fold = Sum;

        return fold(1, 2, 3);
    }

    /// <summary>
    /// 17.3 — a parameter array on a local function, and on a constructor, which are the two
    /// declaration sites that are not a method or a delegate.
    /// </summary>
    /// <returns>What both of them counted.</returns>
    public static int OtherDeclarationSites()
    {
        var holder = new ArrParamsHolder("a", "b", "c");

        return Longest("one", "three") + holder.Count;

        static int Longest(params string[] words)
        {
            var longest = 0;

            foreach (var word in words)
            {
                longest = Math.Max(longest, word.Length);
            }

            return longest;
        }
    }

    /// <summary>
    /// 17.4 — an element access on the parameter-array indexer, with two arguments and with
    /// one, so the same member is reached at two arities from brackets that name nothing.
    /// </summary>
    /// <returns>Both readings added up.</returns>
    public static int ThroughTheIndexer()
    {
        var indexed = new ArrParamsIndexed();

        return indexed[1, 2] + indexed[1] + indexed.Count;
    }
}

/// <summary>A type whose constructor takes a parameter array (17.3).</summary>
public sealed class ArrParamsHolder
{
    private readonly List<string> _names;

    /// <summary>Builds a holder from however many names are passed.</summary>
    /// <param name="names">The names.</param>
    public ArrParamsHolder(params string[] names) => _names = new List<string>(names);

    /// <summary>How many names it holds.</summary>
    public int Count => _names.Count;
}
