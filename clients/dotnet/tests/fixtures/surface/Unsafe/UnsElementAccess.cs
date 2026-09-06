// Clause 24.6.4 — pointer element access, `P[E]`, which the clause defines as `*(P + E)`.
//
// Nothing is written for it. There is no indexer to bind — a pointer is not a type with
// members — and no name token, so the walk sees a `BracketedArgumentList` and files no
// fact. The same is true of an array element access, which the `Ranges` project measured;
// the difference is that an array at least has a `csharp.ArrayType`, while the pointer's
// element access is invisible in both directions.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.4 — pointer element access at each index type and pointee.</summary>
public unsafe class UnsPointerElementAccess
{
    /// <summary>Clause 24.6.4 — an <c>int</c> index, as a value.</summary>
    public static int At(int* cells, int index) => cells[index];

    /// <summary>Clause 24.6.4 — an <c>int</c> index, as an assignment target.</summary>
    public static void Set(int* cells, int index, int value) => cells[index] = value;

    /// <summary>Clause 24.6.4 — a <c>long</c> index over a struct pointee, so the scaling is by <c>sizeof</c>.</summary>
    public static UnsPoint At(UnsPoint* points, long index) => points[index];

    /// <summary>Clause 24.6.4 — a <c>uint</c> index, which the clause admits alongside the signed ones.</summary>
    public static int At(int* cells, uint index) => cells[index];

    /// <summary>Clause 24.6.4 — two element accesses in one expression, through a pointer to a pointer.</summary>
    public static int Nested(int** rows, int row, int column) => rows[row][column];

    /// <summary>
    /// Clause 24.6.4 — a negative index, which is unchecked and legal: <c>P[-1]</c> is
    /// <c>*(P - 1)</c>, and the clause imposes no bound.
    /// </summary>
    public static int Before(int* cells) => cells[-1];

    /// <summary>Clause 24.6.4 — element access on the result of a cast, and a member access off it.</summary>
    public static int FirstByte(int* cells) => ((byte*)cells)[0];
}
