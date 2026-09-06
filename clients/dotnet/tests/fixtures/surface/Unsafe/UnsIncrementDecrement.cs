// Clause 24.6.6 — pointer increment and decrement.
//
// `++` and `--` on a pointer add and subtract one *element*, not one byte, and the clause
// says so in terms of `sizeof`. Like the other pointer operators, the operation names no
// declaration: the predefined operators live in no assembly and are not `csharp.Method`s, so
// a query asking what `at++` bound to gets nothing, where the same question about a
// user-defined `++` returns the operator's own `csharp.Method` row.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.6 — prefix and postfix, forwards and backwards, over two pointee sizes.</summary>
public unsafe class UnsPointerIncrement
{
    /// <summary>Clause 24.6.6 — postfix increment, walking forwards.</summary>
    public static int SumForwards(int* cells, int count)
    {
        int total = 0;
        int* at = cells;

        for (int index = 0; index < count; index++)
        {
            total += *at++;
        }

        return total;
    }

    /// <summary>Clause 24.6.6 — prefix decrement, walking backwards.</summary>
    public static int SumBackwards(int* end, int count)
    {
        int total = 0;
        int* at = end;

        for (int index = 0; index < count; index++)
        {
            total += *--at;
        }

        return total;
    }

    /// <summary>Clause 24.6.6 — prefix increment as a value.</summary>
    public static int* NextInteger(int* at) => ++at;

    /// <summary>Clause 24.6.6 — postfix increment over a struct pointee, so the step is <c>sizeof(UnsPoint)</c>.</summary>
    public static UnsPoint* NextPoint(UnsPoint* at)
    {
        at++;
        return at;
    }

    /// <summary>Clause 24.6.6 — decrement over a pointer to a pointer, whose step is a pointer's width.</summary>
    public static int** PreviousRow(int** at) => --at;

    /// <summary>Clause 24.6.6 — the operators on a <c>byte*</c>, whose step is one.</summary>
    public static byte* NextByte(byte* at)
    {
        byte* copy = at;
        copy++;
        return copy;
    }

    // Clause 24.6.6, written nowhere because it does not compile: `++` on a `void*` is
    // CS0242. The step is `sizeof` the pointee, and `void` has no size.
}
