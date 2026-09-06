// Clause 7.4.7 (array members): an array type's members are the ones it inherits from
// `System.Array`, plus a `Length`, a `Rank` and an indexer that the array type itself
// provides. None of those has a declaration in any source file, so every row about them is
// a reference with no declaration site in the corpus.
//
// The hazard is the rank. `int[]`, `int[,]` and `int[,,]` are three distinct types whose
// element type and whose simple name are the same, and `int[][]` is a fourth that a type
// printer which drops the brackets' arrangement writes the same way as `int[,]`.

namespace Surface.Lexical.Members;

/// <summary>7.4.7: one field per array shape, and references to the members each provides.</summary>
public sealed class LexArrayMembers
{
    /// <summary>Rank one.</summary>
    public readonly int[] Single = [1, 2, 3];

    /// <summary>Rank two, which is one type and not an array of arrays.</summary>
    public readonly int[,] Rectangular = new int[2, 3];

    /// <summary>Rank three.</summary>
    public readonly int[,,] Cubic = new int[2, 3, 4];

    /// <summary>An array of arrays, whose element type is itself an array.</summary>
    public readonly int[][] Jagged = [[1], [2, 3]];

    /// <summary>An array of rank-two arrays.</summary>
    public readonly int[][,] JaggedRectangular = [new int[1, 2]];

    /// <summary>A rank-two array of arrays, which is the other way round again.</summary>
    public readonly int[][] AlsoJagged = new int[2][];

    /// <summary>An array whose element type is a type parameter's, at rank one.</summary>
    public readonly string[] Names = ["a"];

    /// <summary>Reads <c>Length</c>, which the array type provides and no source declares.</summary>
    public int SingleLength() => Single.Length;

    /// <summary>Reads <c>Length</c> on a rank-two array, which is the total and not a side.</summary>
    public int RectangularLength() => Rectangular.Length;

    /// <summary>Reads <c>Rank</c>, which is 1, 2 and 3 for the three shapes.</summary>
    public int Ranks() => Single.Rank + Rectangular.Rank + Cubic.Rank;

    /// <summary>Reads the rank-one indexer.</summary>
    public int FirstOfSingle() => Single[0];

    /// <summary>Reads the rank-two indexer, which takes two arguments and is one member.</summary>
    public int FirstOfRectangular() => Rectangular[0, 0];

    /// <summary>Reads the rank-three indexer.</summary>
    public int FirstOfCubic() => Cubic[0, 0, 0];

    /// <summary>Reads a jagged array through two rank-one indexers in turn.</summary>
    public int FirstOfJagged() => Jagged[0][0];

    /// <summary>Reads <c>GetLength</c>, inherited from <c>System.Array</c>.</summary>
    public int SecondDimension() => Rectangular.GetLength(1);

    /// <summary>Reads <c>LongLength</c>, also inherited.</summary>
    public long LongLength() => Single.LongLength;

    /// <summary>Reads <c>Clone</c>, which <c>System.Array</c> declares for
    /// <c>ICloneable</c> and whose return type is <c>object</c>.</summary>
    public object Cloned() => Single.Clone();

    /// <summary>Reads <c>GetEnumerator</c>, which the array's own indexer does not provide.</summary>
    public int Summed()
    {
        int total = 0;
        foreach (int value in Single)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Reads a static member of <c>System.Array</c> through the array type.</summary>
    public int Found() => System.Array.IndexOf(Single, 2);

    /// <summary>Reads the remaining fields, so none is unreferenced.</summary>
    public int Rest() => JaggedRectangular[0][0, 0] + AlsoJagged.Length + Names.Length;
}
