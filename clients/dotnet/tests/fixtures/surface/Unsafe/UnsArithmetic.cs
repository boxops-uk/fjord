// Clause 24.6.7 — pointer arithmetic.
//
// `P + N`, `N + P`, `P - N` and `P - Q`. The first three yield a pointer, the last a `long`,
// and that asymmetry is the one thing about this clause an index can be asked about: the
// `csharp.Method` row for `Distance` has `returnType` `long` while both of its parameters
// are `pointerType(int)`, so the difference of two pointers is visible as a signature even
// though the subtraction itself is not.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.7 — every written form of pointer arithmetic.</summary>
public unsafe class UnsPointerArithmetic
{
    /// <summary>Clause 24.6.7 — <c>P + N</c>.</summary>
    public static int* Advance(int* at, int count) => at + count;

    /// <summary>Clause 24.6.7 — <c>N + P</c>, which the clause defines to mean the same thing.</summary>
    public static int* AdvanceReversed(int count, int* at) => count + at;

    /// <summary>Clause 24.6.7 — <c>P - N</c>.</summary>
    public static int* Retreat(int* at, int count) => at - count;

    /// <summary>Clause 24.6.7 — <c>P - Q</c>, whose type is <c>long</c> and whose unit is the pointee.</summary>
    public static long Distance(int* left, int* right) => left - right;

    /// <summary>Clause 24.6.7 — arithmetic over a struct pointee, scaled by <c>sizeof(UnsPoint)</c>.</summary>
    public static UnsPoint* AdvancePoint(UnsPoint* at, int count) => at + count;

    /// <summary>Clause 24.6.7 — the difference of two struct pointers, counted in elements.</summary>
    public static long PointDistance(UnsPoint* left, UnsPoint* right) => left - right;

    /// <summary>Clause 24.6.7 — a <c>uint</c> offset, which the clause admits with the signed ones.</summary>
    public static int* AdvanceUnsigned(int* at, uint count) => at + count;

    /// <summary>Clause 24.6.7 — a <c>long</c> offset.</summary>
    public static int* AdvanceLong(int* at, long count) => at + count;

    /// <summary>Clause 24.6.7 — compound assignment, which the clause reaches through the binary operators.</summary>
    public static int* Compound(int* at)
    {
        at += 2;
        at -= 1;
        return at;
    }

    /// <summary>
    /// Clause 24.6.7 — the arithmetic is unchecked and never overflows into an exception,
    /// even inside a <c>checked</c> context.
    /// </summary>
    public static int* Unchecked(int* at)
    {
        checked
        {
            return at + int.MaxValue;
        }
    }
}
