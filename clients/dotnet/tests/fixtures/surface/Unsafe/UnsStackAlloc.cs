// Clause 24.9 — stack allocation.
//
// `stackalloc T[N]` is parsed as a `StackAllocArrayCreationExpressionSyntax`, which is *not*
// a `BaseObjectCreationExpressionSyntax` — so `Indexer.IndexTree`'s object-creation arm never
// sees it and no `csharp.ObjectCreationLocation` row exists for any allocation below. What
// the clause does leave is a type reference at the element type when it is named rather than
// predefined: `stackalloc UnsPoint[2]` mints a `csharp.TypeLocation` at `UnsPoint`, and
// `stackalloc int[2]` mints nothing. The `Span<T>` forms additionally mint a reference to
// `Span`, an external named type the index keys through the framework assembly.

using System;

namespace Surface.Unsafe;

/// <summary>Clause 24.9 — every written form of <c>stackalloc</c>, to a pointer and to a span.</summary>
public unsafe class UnsStackAllocation
{
    /// <summary>Clause 24.9 — allocation with a run-time length, assigned to a pointer local.</summary>
    public static int SumOfLength(int count)
    {
        int* cells = stackalloc int[count];
        int total = 0;

        for (int index = 0; index < count; index++)
        {
            cells[index] = index;
            total += cells[index];
        }

        return total;
    }

    /// <summary>Clause 24.9 — allocation with a length and an initializer.</summary>
    public static int OfInitializer()
    {
        int* cells = stackalloc int[3] { 1, 2, 3 };
        return cells[2];
    }

    /// <summary>Clause 24.9 — an initializer with no length, whose length is the element count.</summary>
    public static int OfLengthlessInitializer()
    {
        int* cells = stackalloc int[] { 5, 8 };
        return cells[1];
    }

    /// <summary>Clause 24.9 — an inferred element type, from the initializer alone.</summary>
    public static int OfInferredElementType()
    {
        int* cells = stackalloc[] { 13, 21 };
        return cells[1];
    }

    /// <summary>Clause 24.9 — a struct element type, whose name is a reference the index holds.</summary>
    public static int OfStructElements()
    {
        UnsPoint* points = stackalloc UnsPoint[2];
        points[0].X = 3;
        points[1] = new UnsPoint { X = 4, Y = 5 };
        return points[0].X + points[1].Y;
    }

    /// <summary>Clause 24.9 — an enum element type.</summary>
    public static UnsFlag OfEnumElements()
    {
        UnsFlag* flags = stackalloc UnsFlag[2];
        flags[1] = UnsFlag.Set;
        return flags[1];
    }

    /// <summary>Clause 24.9 — a pointer element type, which is unmanaged and so allowed.</summary>
    public static int OfPointerElements()
    {
        int value = 37;
        int** rows = stackalloc int*[2];
        rows[0] = &value;
        return *rows[0];
    }

    /// <summary>
    /// Clause 24.9 — allocation to a <see cref="Span{T}"/>, which is the form that needs no
    /// unsafe context of its own; this one is inside one anyway, since the whole file is.
    /// </summary>
    public static int OfSpan()
    {
        Span<int> cells = stackalloc int[4];
        cells[0] = 43;
        return cells[0] + cells.Length;
    }

    /// <summary>Clause 24.9 — a span allocation with an initializer.</summary>
    public static int OfSpanInitializer()
    {
        Span<byte> cells = stackalloc byte[] { 1, 2, 3 };
        return cells.Length;
    }

    /// <summary>Clause 24.9's post-standard form — <c>stackalloc</c> as an argument, in a nested expression.</summary>
    public static int AsArgument() => Total(stackalloc int[] { 47, 53 });

    /// <summary>Clause 24.9's post-standard form — <c>stackalloc</c> in both arms of a conditional.</summary>
    public static int InConditional(bool wide)
    {
        Span<int> cells = wide ? stackalloc int[8] : stackalloc int[2];
        return cells.Length;
    }

    /// <summary>Clause 24.9 — a zero-length allocation, which the clause permits and which yields a valid pointer.</summary>
    public static bool OfZeroLength()
    {
        int* cells = stackalloc int[0];
        return cells != null;
    }

    private static int Total(Span<int> cells)
    {
        int total = 0;

        foreach (int cell in cells)
        {
            total += cell;
        }

        return total;
    }

    // Clause 24.9, written nowhere because it does not compile: a managed element type is
    // CS0208 (`stackalloc string[2]`), and the allocation may not appear in a `catch` or
    // `finally` block.
}
