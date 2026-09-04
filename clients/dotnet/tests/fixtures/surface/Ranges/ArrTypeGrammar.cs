// Clauses 17.2 and 17.2.1 — array types: a non_array_type followed by rank specifiers, or an
// array type followed by a nullable annotation followed by rank specifiers. The rank is the
// leftmost rank specifier *of the outermost production*, and the element type is what is left
// when that specifier is deleted.
//
// The table below is 17.2.1's own example, declaration for declaration, because the clause's
// note about the last two is the hazard in one sentence: `Array9` and `Array10` "appear the
// same other than for the use of ?" and have different ranks and different element types. Two
// declarations that differ only inside their type, in a producer where the type of a field is
// not a symbol — an array type gets no SCIP symbol at all — so the difference lives in the
// signature string and nowhere else. A query that joins declarations by symbol cannot see it.

namespace Surface.Ranges;

/// <summary>
/// Every form of array type 17.2.1 enumerates, with the rank and element type of each named
/// in a comment and demonstrated by an element access whose static type is written out.
/// </summary>
public static class ArrTypeGrammar
{
    /// <summary>Rank 1, element type <c>int</c>.</summary>
    public static readonly int[] Array1 = default!;

    /// <summary>Rank 2, element type <c>int</c>.</summary>
    public static readonly int[,] Array2 = default!;

    /// <summary>Rank 1, element type <c>int?</c> — a nullable value type element.</summary>
    public static readonly int?[] Array3 = default!;

    /// <summary>Rank 1, element type <c>string?</c> — a nullable reference type element.</summary>
    public static readonly string?[] Array4 = default!;

    /// <summary>Rank 1, element type <c>string[,,][,]</c>.</summary>
    public static readonly string[][,,][,] Array5 = default!;

    /// <summary>Rank 1, element type <c>string</c>; the array itself is nullable.</summary>
    public static readonly string[]? Array6 = default!;

    /// <summary>Rank 1, element type <c>string[,]?</c>.</summary>
    public static readonly string[,]?[] Array7 = default!;

    /// <summary>Rank 3, element type <c>int[]?[,]</c> — the nullable annotation sits inside.</summary>
    public static readonly int[]?[,,][,] Array8 = default!;

    /// <summary>Rank 1, element type <c>string[,]?[]?[,,]</c>.</summary>
    public static readonly string[,]?[]?[][,,] Array9 = default!;

    /// <summary>
    /// Rank 2, element type <c>string[][][,,]</c> — the same tokens as <see cref="Array9"/>
    /// but for the annotations, and a different rank and element type for it.
    /// </summary>
    public static readonly string[,][][][,,] Array10 = default!;

    /// <summary>
    /// One element read out of each, so that the element type of each declaration above is
    /// written a second time as the type of a local (17.2.1's example does exactly this).
    /// </summary>
    /// <returns>A number every element read above contributes to, so none folds away.</returns>
    public static int Elements()
    {
        int element1 = Array1[0];
        int element2 = Array2[0, 1];
        int? element3 = Array3[0];
        string? element4 = Array4[0];
        string[,,][,] element5 = Array5[0];
        string element6 = Array6?[0] ?? string.Empty;
        string[,]? element7 = Array7[0];
        int[]?[,] element8 = Array8[0, 1, 2];
        string[,]?[]?[,,] element9 = Array9[0];
        string[][][,,] element10 = Array10[0, 1];

        return element1
            + element2
            + (element3 ?? 0)
            + (element4?.Length ?? 0)
            + element5.Length
            + element6.Length
            + (element7?.Length ?? 0)
            + element8.Length
            + element9.Length
            + element10.Length;
    }

    /// <summary>
    /// A non-array type followed by one rank specifier, where the non-array type is itself a
    /// constructed generic — the production's other side.
    /// </summary>
    /// <param name="cells">An array of cells.</param>
    /// <returns>The first cell's value, or zero.</returns>
    public static int FirstCell(ArrCell<int>[] cells) =>
        cells.Length == 0 ? 0 : cells[0].Value;

    /// <summary>
    /// 17.2.1's closing sentence — a value of an array type can be <c>null</c>, which is why
    /// the annotated form of the type exists at all.
    /// </summary>
    /// <param name="rows">An array that may be absent.</param>
    /// <returns>How many rows there are, counting an absent array as none.</returns>
    public static int RowCount(int[][]? rows) => rows?.Length ?? 0;

    /// <summary>
    /// An overload set whose members differ only by rank specifier: a single-dimensional
    /// array, a rank-two array, and an array of arrays. Three declarations with one name,
    /// told apart by nothing a use site spells — the method disambiguator counts them in
    /// documentation-comment order, so this set is what an ordinal has to survive.
    /// </summary>
    /// <param name="row">A rank-one array.</param>
    /// <returns>Its length.</returns>
    public static int Take(int[] row) => row.Length;

    /// <summary>The rank-two overload of <see cref="Take(int[])"/>.</summary>
    /// <param name="grid">A rank-two array.</param>
    /// <returns>Its total length.</returns>
    public static int Take(int[,] grid) => grid.Length;

    /// <summary>The array-of-arrays overload of <see cref="Take(int[])"/>.</summary>
    /// <param name="rows">An array of arrays.</param>
    /// <returns>How many rows there are.</returns>
    public static int Take(int[][] rows) => rows.Length;

    /// <summary>Calls all three overloads, so each has a use site that names it.</summary>
    /// <returns>The three lengths added up.</returns>
    public static int TakeAll() =>
        Take(new int[2]) + Take(new int[2, 2]) + Take(new int[2][]);
}
