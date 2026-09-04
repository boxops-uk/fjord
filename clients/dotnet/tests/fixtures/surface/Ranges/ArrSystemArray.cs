// Clauses 17.2.2 and 17.5 — `System.Array` is the abstract base type of every array type,
// and every array type inherits its members and declares none of its own.
//
// That makes this file the one place in clause 17 where a *member* is named at a use site,
// and every one of those names resolves into metadata the corpus never declares. The package
// coordinate is therefore what identifies them, and `ScipSymbols.Package` builds it from
// `ContainingAssembly.Identity` — which for the core library is `System.Runtime` under a
// reference pack and `System.Private.CoreLib` for a compilation over the runtime assemblies,
// with the version moving per target framework. So `Row.Length` below is a reference whose
// target's symbol string depends on how the indexer was pointed at the code, and no
// declaration in this corpus can ever match it.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>
/// The conversions between an array type and <c>System.Array</c> (17.2.2), and the members an
/// array type inherits from it (17.5).
/// </summary>
public static class ArrSystemArray
{
    /// <summary>A rank-one array to hang the member accesses off.</summary>
    public static readonly int[] Row = { 3, 1, 4, 1, 5 };

    /// <summary>A rank-two array, because half of 17.5's members only mean anything above rank one.</summary>
    public static readonly int[,] Grid = new int[2, 3];

    /// <summary>
    /// 17.2.2 — the implicit reference conversion from an array type to
    /// <c>System.Array</c>, which is a class type and not an array type.
    /// </summary>
    /// <param name="row">Any array.</param>
    /// <returns>The same instance, seen as the base class.</returns>
    public static Array AsBase(int[] row) => row;

    /// <summary>
    /// 17.2.2 — the explicit reference conversion back, which is checked at run time and is
    /// the only direction that can fail.
    /// </summary>
    /// <param name="array">Anything that is an array.</param>
    /// <returns>The same instance, seen as a rank-one <c>int</c> array.</returns>
    public static int[] AsInts(Array array) => (int[])array;

    /// <summary>
    /// 17.2.2 — the implicit conversion to an interface <c>System.Array</c> implements, one
    /// per interface, since each is a separate conversion the clause licenses.
    /// </summary>
    /// <param name="row">Any array.</param>
    /// <returns>What the four non-generic interfaces see.</returns>
    public static (bool Fixed, bool ReadOnly, int Count, bool Any) AsInterfaces(int[] row)
    {
        ICloneable cloneable = row;
        IList list = row;
        ICollection collection = row;
        IEnumerable enumerable = row;

        return (
            list.IsFixedSize,
            list.IsReadOnly,
            collection.Count,
            cloneable.Clone() is not null && enumerable.GetEnumerator().MoveNext());
    }

    /// <summary>
    /// 17.2.2 — the explicit reference conversion from an interface implemented by
    /// <c>System.Array</c> back to an array type.
    /// </summary>
    /// <param name="list">A list that is really an array.</param>
    /// <returns>The array it really is.</returns>
    public static int[] FromInterface(IList list) => (int[])list;

    /// <summary>
    /// 17.2.2 — at run time a value of type <c>System.Array</c> can be null, or a reference
    /// to an instance of *any* array type, whatever its rank or element type.
    /// </summary>
    /// <returns>One of each, plus the absent case.</returns>
    public static Array?[] Anything() =>
        new Array?[] { null, Row, Grid, ArrGeneral.Pascals, ArrGeneral.Labels };

    /// <summary>
    /// 17.5 — the instance members every array type inherits, each named at a use site so the
    /// reference is written. Not one of them is declared anywhere in this corpus.
    /// </summary>
    /// <returns>A tuple of everything the inherited members answer.</returns>
    public static (int Length, long Long, int Rank, int Lower, int Upper) Inherited()
    {
        var length = Row.Length;
        var longLength = Row.LongLength;
        var rank = Grid.Rank;
        var lower = Grid.GetLowerBound(0);
        var upper = Grid.GetUpperBound(1);

        return (length, (long)longLength, rank, lower, upper);
    }

    /// <summary>
    /// 17.5 — the inherited methods, including the two that reach an element without an
    /// element access expression at all.
    /// </summary>
    /// <returns>What <c>GetValue</c> saw after <c>SetValue</c> wrote.</returns>
    public static object? Reflected()
    {
        var copy = (int[])Row.Clone();

        copy.SetValue(9, 0);
        Row.CopyTo(copy, 0);

        return copy.GetValue(0);
    }

    /// <summary>
    /// 17.5 — <c>System.Array</c>'s own static members, reached through the type name rather
    /// than inherited through an instance: the distinction the clause's word "declared" makes.
    /// </summary>
    /// <returns>A sorted copy of <see cref="Row"/>, and where a value sits in it.</returns>
    public static (int[] Sorted, int Found) Statics()
    {
        var sorted = Array.Empty<int>();

        if (Row.Length > 0)
        {
            sorted = new int[Row.Length];
            Array.Copy(Row, sorted, Row.Length);
            Array.Sort(sorted);
        }

        return (sorted, Array.IndexOf(sorted, 4));
    }

    /// <summary>
    /// 17.5 — the inherited enumerator, which is what makes <c>foreach</c> over an array of
    /// any rank legal without the element type appearing in the loop.
    /// </summary>
    /// <returns>The elements of <see cref="Grid"/>, boxed, in row-major order.</returns>
    public static List<object?> Boxed()
    {
        var seen = new List<object?>();
        var walker = Grid.GetEnumerator();

        while (walker.MoveNext())
        {
            seen.Add(walker.Current);
        }

        return seen;
    }
}
