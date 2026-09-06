// Clause 17.4 — array element access: `A[I1, ..., Ix]` where `A` has an array type and each
// index is `int`, `uint`, `long`, `ulong`, or implicitly convertible to one of them. The
// result is a *variable reference*, not a value, which is why every form below can be
// assigned to, incremented, aliased by a `ref` local and passed as an `out` argument.
//
// The second paragraph is the seam with clause 18: for a single-dimensional array the sole
// index may instead be an `Index` or a `Range`, giving an element or a shallow-copied array.
//
// **This file writes no member references at all, and that is the defect it pins.** The
// walk's switch has a case for `SimpleNameSyntax`, object creation, invocation and member
// access, and no case for `ElementAccessExpressionSyntax`. So for `Row[0] = 1` the only fact
// written points at `Row`, the field — the access itself is nothing. Worse for the clause-18
// forms: `Row[^1]` and `Row[1..3]` are an `ArrayElementReference` operation whose
// `GetSymbolInfo` is *null*, so a producer that adds the missing case and reads a null symbol
// as "unresolved" will count legal code as a failure and poison the tally that is supposed to
// signal real ones.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Surface.Ranges;

/// <summary>
/// Every index type, every rank, and every way an element access can be used as a variable
/// rather than read as a value (17.4).
/// </summary>
public static class ArrElementAccess
{
    /// <summary>The array every access below is written against.</summary>
    public static readonly int[] Row = { 3, 1, 4, 1, 5 };

    /// <summary>A rank-two array, for the multi-index form.</summary>
    public static readonly int[,] Grid = new int[2, 3];

    /// <summary>A rank-three array, so an access with three indices is written somewhere.</summary>
    public static readonly int[,,] Cube = new int[2, 2, 2];

    /// <summary>17.4 — the four index types the clause names outright.</summary>
    /// <param name="row">Any rank-one array.</param>
    /// <returns>The same element, reached four ways.</returns>
    public static int EveryIndexType(int[] row)
    {
        int asInt = row[1];
        int asUint = row[(uint)1];
        int asLong = row[1L];
        int asUlong = row[(ulong)1];

        return asInt + asUint + asLong + asUlong;
    }

    /// <summary>
    /// 17.4 — an index that is not one of those four but converts to one: <c>byte</c>,
    /// <c>short</c>, <c>char</c> and an enum member's underlying value.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <returns>The elements those indices reach, added up.</returns>
    public static int ConvertedIndices(int[] row)
    {
        const byte first = 0;
        const short second = 1;
        const char third = (char)2;

        return row[first] + row[second] + row[third] + row[(int)ArrEdge.Thin];
    }

    /// <summary>
    /// 17.4 — an index that is an arbitrary expression rather than a constant, evaluated
    /// before the access.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <param name="offset">Where to start.</param>
    /// <returns>The element one past the offset, or the first.</returns>
    public static int ComputedIndex(int[] row, int offset) =>
        row[(offset + 1) % row.Length];

    /// <summary>17.4 — one index per dimension, for rank two and rank three.</summary>
    /// <returns>Two elements read through multi-index accesses.</returns>
    public static int MultiIndex() => Grid[0, 1] + Cube[1, 1, 1];

    /// <summary>
    /// 17.4 — an array of arrays takes one access per level, and each level is a separate
    /// element access expression.
    /// </summary>
    /// <param name="rows">An array of arrays.</param>
    /// <returns>An element of the first row.</returns>
    public static int Chained(int[][] rows) => rows[0][1];

    /// <summary>
    /// 17.4 — <c>A</c> is any expression of an array type, not only a variable: here it is a
    /// method call's result and a member access.
    /// </summary>
    /// <returns>Elements read through accesses whose receiver is not a name.</returns>
    public static int ReceiverIsAnExpression() =>
        ArrCreation.Nested()[1][0] + ArrGeneral.Pascals[3][2];

    /// <summary>
    /// 17.4 — a null-conditional element access, where the whole access is skipped rather
    /// than the index being out of range.
    /// </summary>
    /// <param name="row">An array that may be absent.</param>
    /// <returns>The first element, or nothing.</returns>
    public static int? Conditionally(int[]? row) => row?[0];

    /// <summary>
    /// 17.4 — the result is a variable reference, so it can be assigned, compound-assigned
    /// and incremented in place.
    /// </summary>
    /// <param name="row">The array to write into.</param>
    public static void AsAVariable(int[] row)
    {
        row[0] = 7;
        row[1] += 2;
        row[2]++;
        --row[3];
        Grid[0, 0] = row[0];
    }

    /// <summary>
    /// 17.4 — a <c>ref</c> local aliasing an element, which is only possible because the
    /// access is a variable reference and not a value.
    /// </summary>
    /// <param name="row">The array to alias into.</param>
    /// <returns>What the aliased element holds after being written through the alias.</returns>
    public static int Aliased(int[] row)
    {
        ref var slot = ref row[0];

        slot = 11;

        return row[0];
    }

    /// <summary>
    /// 17.4 — an element passed as an <c>out</c> argument and as a <c>ref</c> argument, the
    /// two argument forms that require a variable.
    /// </summary>
    /// <param name="row">The array whose elements are written through.</param>
    /// <param name="text">A number to parse into the first element.</param>
    /// <returns>Whether the parse succeeded.</returns>
    public static bool ByReference(int[] row, string text)
    {
        var parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out row[0]);

        Swap(ref row[0], ref row[1]);

        return parsed;
    }

    /// <summary>17.4 — the callee that makes the <c>ref</c> argument above meaningful.</summary>
    /// <param name="left">One slot.</param>
    /// <param name="right">The other.</param>
    public static void Swap(ref int left, ref int right) => (left, right) = (right, left);

    /// <summary>
    /// 17.4's second paragraph — the sole index of a single-dimensional array access may be
    /// an <c>Index</c>, giving a variable reference to one element. This is 18.4.1's first
    /// fallthrough arm: array access, tried before any indexer or pattern.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <returns>The last element, and the last but one.</returns>
    public static (int Last, int Penultimate) ByIndex(int[] row) => (row[^1], row[^2]);

    /// <summary>
    /// 17.4's second paragraph — an <c>Index</c> that arrives as a value rather than as a
    /// <c>^</c> expression, so the conversion is not visible at the access.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <param name="index">Where to read.</param>
    /// <returns>The element there.</returns>
    public static int ByIndexValue(int[] row, Index index) => row[index];

    /// <summary>
    /// 17.4's second paragraph — an <c>Index</c> access is still a variable reference, so it
    /// can be assigned to.
    /// </summary>
    /// <param name="row">The array to write into.</param>
    public static void WriteFromEnd(int[] row)
    {
        row[^1] = 0;
        row[^2] += 1;
    }

    /// <summary>
    /// 17.4's second paragraph — a <c>Range</c> index gives a *new array*, a shallow copy
    /// keeping element order, so it is a value and not a variable reference.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <returns>Four slices: interior, prefix, suffix, and the whole thing.</returns>
    public static (int[] Middle, int[] Head, int[] Tail, int[] All) ByRange(int[] row) =>
        (row[1..^1], row[..2], row[2..], row[..]);

    /// <summary>
    /// 17.4's second paragraph — a <c>Range</c> that arrives as a value, and the shallow-copy
    /// property stated as code: writing into the slice does not touch the source.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <param name="range">What to copy out.</param>
    /// <returns>The source's first element after the copy has been overwritten.</returns>
    public static int ShallowCopy(int[] row, Range range)
    {
        var slice = row[range];

        if (slice.Length > 0)
        {
            slice[0] = -1;
        }

        return row[0];
    }

    /// <summary>
    /// 17.4's third paragraph — the elements of an array can be enumerated with
    /// <c>foreach</c>, at rank one and above, and above rank one the loop variable's type is
    /// still the element type.
    /// </summary>
    /// <returns>Everything in <see cref="Row"/> and <see cref="Grid"/>, added up.</returns>
    public static int Enumerated()
    {
        var total = 0;

        foreach (var element in Row)
        {
            total += element;
        }

        foreach (int element in Grid)
        {
            total += element;
        }

        foreach (var row in ArrGeneral.Pascals)
        {
            foreach (var element in row)
            {
                total += element;
            }
        }

        return total;
    }

    /// <summary>
    /// 17.4 — an element access on the interface view of an array reaches
    /// <c>IList&lt;T&gt;</c>'s indexer instead, which is a declared member with a name. The
    /// contrast is the point: the same brackets, and this time there is something to bind to.
    /// </summary>
    /// <param name="row">Any rank-one array.</param>
    /// <returns>The first element, read through the interface.</returns>
    public static int ThroughTheInterface(int[] row)
    {
        IList<int> list = row;

        return list[0];
    }
}
