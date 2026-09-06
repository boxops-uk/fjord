// Clause 17.1 — what an array is: an element type, a rank, one length per dimension, and
// the arrays-of-arrays form the standard names "jagged".
//
// Nothing here declares a member *of* an array — every array type inherits its members from
// `System.Array` (17.5) and declares none of its own. So the only declarations in this file
// are the fields and methods that *hold* array types, which is the trap the whole clause
// carries: an array type has no symbol of its own in this producer. `SymbolKind.ArrayType`
// is not one of the kinds `ScipSymbols.HasGlobalName` admits, so `Of` returns null for
// `int[]`, and the rank and jaggedness of everything below survive only inside a
// declaration's signature string.

using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>How a cell's edge is drawn — an enum, so an array can have an enum element type.</summary>
public enum ArrEdge
{
    /// <summary>Not drawn.</summary>
    None = 0,

    /// <summary>Drawn thin.</summary>
    Thin = 1,
}

/// <summary>A cell of a grid, generic in what it holds, so an array can have a constructed element type.</summary>
/// <typeparam name="T">What the cell holds.</typeparam>
public readonly struct ArrCell<T>(T value)
{
    /// <summary>What the cell holds.</summary>
    public T Value { get; } = value;
}

/// <summary>
/// Rank, element type, emptiness, and the difference between a multi-dimensional array and
/// an array of arrays (17.1).
/// </summary>
public static class ArrGeneral
{
    /// <summary>A single-dimensional array: rank one, element type <c>int</c>.</summary>
    public static readonly int[] Single = new int[4];

    /// <summary>Rank two — what the standard calls a two-dimensional array.</summary>
    public static readonly int[,] Rectangular = new int[3, 2];

    /// <summary>Rank three, so a rank specifier holding two commas.</summary>
    public static readonly int[,,] Cube = new int[2, 2, 2];

    /// <summary>
    /// Rank four, which is where the rank specifier stops being readable and starts being
    /// the only thing that tells two declarations apart.
    /// </summary>
    public static readonly int[,,,] Tesseract = new int[1, 1, 1, 1];

    /// <summary>
    /// 17.1's own example: an array of arrays, whose rows have different lengths. Distinct
    /// from <see cref="Rectangular"/>, which is one array with two dimensions.
    /// </summary>
    public static readonly int[][] Pascals =
    {
        new int[] { 1 },
        new int[] { 1, 1 },
        new int[] { 1, 2, 1 },
        new int[] { 1, 3, 3, 1 },
    };

    /// <summary>An empty array: a dimension of length zero (17.1).</summary>
    public static readonly int[] Empty = new int[0];

    /// <summary>
    /// Empty for the other reason — one dimension of length zero makes the whole instance
    /// empty however long the others are.
    /// </summary>
    public static readonly int[,] EmptyRectangle = new int[0, 3];

    /// <summary>An element type that is a value type.</summary>
    public static readonly ArrEdge[] Edges = new ArrEdge[2];

    /// <summary>An element type that is a nullable value type.</summary>
    public static readonly int?[] Optional = new int?[2];

    /// <summary>An element type that is a reference type.</summary>
    public static readonly string[] Labels = { "left", "right" };

    /// <summary>An element type that is itself a constructed generic type.</summary>
    public static readonly ArrCell<int>[] Cells = new ArrCell<int>[2];

    /// <summary>
    /// An element type that is an array of a constructed generic type — arrays of arrays of
    /// generics, where the element type is written with the generic argument list inside it.
    /// </summary>
    public static readonly List<string>[][] Buckets =
    {
        new[] { new List<string> { "a" } },
        new[] { new List<string>(), new List<string> { "b", "c" } },
    };

    /// <summary>
    /// 17.1 — the total number of elements is the product of the dimension lengths, which is
    /// what makes <c>GetLength</c> rather than <c>Length</c> the honest reading for a rank
    /// above one.
    /// </summary>
    /// <param name="cube">A rank-three array.</param>
    /// <returns>How many elements it has.</returns>
    public static int TotalElements(int[,,] cube) =>
        cube.GetLength(0) * cube.GetLength(1) * cube.GetLength(2);

    /// <summary>17.1 — an array with any dimension of length zero is empty.</summary>
    /// <param name="grid">A rank-two array.</param>
    /// <returns>Whether the array is empty.</returns>
    public static bool IsEmpty(int[,] grid) =>
        grid.GetLength(0) == 0 || grid.GetLength(1) == 0;

    /// <summary>
    /// 17.1 — the valid index range of a dimension of length <c>N</c> is <c>0</c> to
    /// <c>N - 1</c>, stated as code rather than as prose.
    /// </summary>
    /// <param name="row">A single-dimensional array.</param>
    /// <param name="index">A candidate index.</param>
    /// <returns>Whether the index is in range.</returns>
    public static bool InRange(int[] row, int index) => index >= 0 && index < row.Length;

    /// <summary>
    /// 17.1 — a jagged array's rows are separate instances, so the row length is a property
    /// of the row and not of the outer array.
    /// </summary>
    /// <param name="rows">An array of arrays.</param>
    /// <returns>The length of the longest row.</returns>
    public static int WidestRow(int[][] rows)
    {
        var widest = 0;

        foreach (var row in rows)
        {
            if (row.Length > widest)
            {
                widest = row.Length;
            }
        }

        return widest;
    }
}
