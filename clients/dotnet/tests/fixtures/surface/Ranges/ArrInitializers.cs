// Clause 17.7 — array initializers: `{ … }`, in a field declaration, a local variable
// declaration, or after an array creation expression. The context decides the type; the
// nesting depth has to match the rank; a trailing comma is allowed; and an initializer beside
// an explicit length requires the length to be constant and the counts to agree.
//
// 17.7 is the one clause in this slice whose census row says `declares`, and it is not the
// array that declares: it is the field or local carrying the initializer. That is the trap.
// `Evens` below is a field declaration whose initializer creates an array with no `new`
// anywhere, so the array creation has no syntax to hang a fact on — while the *names* inside
// an initializer are ordinary references and are written normally, which is what `Referring`
// is here to show. An initializer is therefore a place where references appear and creations
// do not.

using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>
/// Every array-initializer form 17.7 admits, at every rank and in both declaration contexts
/// (17.7).
/// </summary>
public static class ArrInitializers
{
    /// <summary>
    /// 17.7's own example: a field initializer that is shorthand for
    /// <c>new int[] {0, 2, 4, 6, 8}</c>.
    /// </summary>
    public static readonly int[] Evens = { 0, 2, 4, 6, 8 };

    /// <summary>17.7 — the same array, written the long way, so both spellings are indexed.</summary>
    public static readonly int[] EvensSpelledOut = new int[] { 0, 2, 4, 6, 8 };

    /// <summary>17.7 — an explicit length beside an initializer, whose counts agree.</summary>
    public static readonly int[] Three = new int[3] { 0, 1, 2 };

    /// <summary>
    /// 17.7 — a rank-two initializer, one nesting level per dimension: five rows of two.
    /// </summary>
    public static readonly int[,] Pairs = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 8, 9 } };

    /// <summary>
    /// 17.7 — a rank-three initializer, three levels deep, since the nesting has to match the
    /// rank however deep that is.
    /// </summary>
    public static readonly int[,,] Layers = { { { 0, 1 }, { 2, 3 } }, { { 4, 5 }, { 6, 7 } } };

    /// <summary>
    /// 17.7 — an empty initializer for a rank-two array: a leftmost dimension of length zero
    /// makes every subsequent dimension zero too, so this is <c>new int[0, 0]</c>.
    /// </summary>
    public static readonly int[,] Nothing = { };

    /// <summary>17.7's closing note — a trailing comma, which is allowed exactly so lists can be edited.</summary>
    public static readonly string[] Trailing =
    {
        "first",
        "second",
    };

    /// <summary>
    /// 17.7 — an array-of-arrays initializer, where each element is itself a creation
    /// expression rather than a nested initializer: the jagged form the rank rule does not
    /// apply to.
    /// </summary>
    public static readonly int[][] Jagged =
    {
        new[] { 1 },
        new[] { 1, 1 },
        new[] { 1, 2, 1 },
    };

    /// <summary>
    /// 17.7 — an array of arrays of a constructed generic type, with initializers at both
    /// levels and object creation at the leaves.
    /// </summary>
    public static readonly List<int>[][] Groups =
    {
        new[] { new List<int> { 1, 2 } },
        new[] { new List<int>(), new List<int> { 3 } },
    };

    /// <summary>
    /// 17.7 — an initializer whose elements are constructions of a value type, so the element
    /// type is a struct and each element is built rather than copied.
    /// </summary>
    public static readonly ArrCell<string>[] Cells =
    {
        new ArrCell<string>("left"),
        new ArrCell<string>("right"),
    };

    /// <summary>
    /// 17.7 — the same initializer syntax in a *local* variable declaration, which is the
    /// second of the three contexts the clause lists.
    /// </summary>
    /// <returns>The locals' lengths, so none of them is dead.</returns>
    public static int Locals()
    {
        int[] evens = { 0, 2, 4, 6, 8 };
        int[,] pairs = { { 0, 1 }, { 2, 3 } };
        int[,] empty = { };
        string[] labels = { "a", "b", };
        int[][] jagged = { new[] { 1 }, new[] { 1, 1 } };
        ArrEdge[] edges = { ArrEdge.None, ArrEdge.Thin };

        return evens.Length
            + pairs.Length
            + empty.Length
            + labels.Length
            + jagged.Length
            + edges.Length;
    }

    /// <summary>
    /// 17.7 — an initializer holds *expressions*, and every name in one is an ordinary
    /// reference: a constant, an enum member, a static field, a method call, and a nested
    /// element access.
    /// </summary>
    /// <returns>An array built out of names rather than literals.</returns>
    public static int[] Referring() =>
        new[]
        {
            ArrCreation.Width,
            (int)ArrEdge.Thin,
            ArrGeneral.Single.Length,
            ArrGeneral.TotalElements(ArrGeneral.Cube),
            ArrGeneral.Pascals[2][1],
        };

    /// <summary>
    /// 17.7 — an initializer in the third context: directly after a creation expression whose
    /// element type is inferred from the initializer's own elements.
    /// </summary>
    /// <returns>An array whose element type nothing in the expression names.</returns>
    public static string[] Inferred() => new[] { "one", "two", "three" };

    /// <summary>
    /// 17.7 — each element of a single-dimensional initializer needs only an *implicit
    /// conversion* to the element type, so the elements here are written as <c>int</c>,
    /// <c>char</c> and <c>byte</c> and the array is <c>long[]</c>.
    /// </summary>
    /// <returns>An array whose elements all had to be converted to reach it.</returns>
    public static long[] Converted() => new long[] { 1, 'a', (byte)3 };
}
