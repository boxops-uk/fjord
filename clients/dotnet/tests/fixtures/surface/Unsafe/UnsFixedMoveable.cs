// Clause 24.4 — fixed and moveable variables.
//
// The clause is a classification, not a construct: a variable is *fixed* if the garbage
// collector cannot move it (a local, a parameter, a `stackalloc`'d block, a field of a
// fixed variable of struct type) and *moveable* otherwise (an instance field of a class, an
// array element, a `string`'s characters). The classification decides only whether `&` is
// legal outside a `fixed` statement, and no predicate in this schema records it — so the
// code below is written for the reader's sake, and the census row for 24.4 says
// `not-applicable`.

namespace Surface.Unsafe;

/// <summary>
/// Clause 24.4 — the same address taken from a fixed variable and from a moveable one, so
/// that the difference is visible as the presence of a <c>fixed</c> statement.
/// </summary>
public unsafe class UnsFixedAndMoveable
{
    private int _cell = 3;

    /// <summary>The cells whose elements are moveable, being inside a heap object.</summary>
    public int[] Cells = [2, 3, 5, 7];

    /// <summary>Clause 24.4 — a local is fixed, so its address needs no <c>fixed</c> statement.</summary>
    public static int FromLocal()
    {
        int local = 7;
        int* at = &local;
        return *at;
    }

    /// <summary>Clause 24.4 — a value parameter is a local, and so is fixed.</summary>
    public static int FromParameter(int value)
    {
        int* at = &value;
        return *at;
    }

    /// <summary>
    /// Clause 24.4 — a field of a struct-typed <i>local</i> is itself fixed, because the
    /// local is: the rule composes through struct members.
    /// </summary>
    public static int FromFieldOfLocalStruct()
    {
        UnsPoint point = default;
        int* at = &point.Y;
        *at = 4;
        return point.Y;
    }

    /// <summary>Clause 24.4 — <c>stackalloc</c>'d memory is fixed for the lifetime of the frame.</summary>
    public static int FromStackAllocated()
    {
        int* cells = stackalloc int[2];
        cells[0] = 5;
        return *cells;
    }

    /// <summary>
    /// Clause 24.4 — an instance field of a class is moveable, so the address is available
    /// only inside a <c>fixed</c> statement's initializer.
    /// </summary>
    public int FromMoveableField()
    {
        fixed (int* at = &_cell)
        {
            return *at;
        }
    }

    /// <summary>Clause 24.4 — an array element is moveable, for the same reason.</summary>
    public int FromMoveableElement()
    {
        fixed (int* at = &Cells[1])
        {
            return *at;
        }
    }

    // Clause 24.4, written nowhere because it does not compile: `&_cell` outside a `fixed`
    // statement is CS0212, and `&Cells[1]` likewise. The classification is enforced by the
    // compiler rather than recorded by the index, which is what makes this row's verdict
    // `not-applicable` rather than `unbuildable`.
}
