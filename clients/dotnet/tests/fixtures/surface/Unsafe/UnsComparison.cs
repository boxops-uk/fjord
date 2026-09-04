// Clause 24.6.8 — pointer comparison.
//
// The six relational operators are predefined on `void*`, and every pointer type converts
// to `void*` implicitly — so comparing an `int*` with a `byte*` compiles, through a
// conversion nobody wrote. No fact records any of it: the operators belong to no type, so
// there is no `csharp.Method` for a query to reach, and the comparison of two pointers is
// indistinguishable in the index from the comparison of two integers.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.8 — all six operators, plus the conversions the clause leans on.</summary>
public unsafe class UnsPointerComparison
{
    /// <summary>Clause 24.6.8 — <c>==</c>.</summary>
    public static bool Same(int* left, int* right) => left == right;

    /// <summary>Clause 24.6.8 — <c>!=</c>.</summary>
    public static bool Differ(int* left, int* right) => left != right;

    /// <summary>Clause 24.6.8 — <c>&lt;</c>, which orders addresses as unsigned integers.</summary>
    public static bool Below(int* left, int* right) => left < right;

    /// <summary>Clause 24.6.8 — <c>&gt;</c>.</summary>
    public static bool Above(int* left, int* right) => left > right;

    /// <summary>Clause 24.6.8 — <c>&lt;=</c>.</summary>
    public static bool NotAbove(int* left, int* right) => left <= right;

    /// <summary>Clause 24.6.8 — <c>&gt;=</c>.</summary>
    public static bool NotBelow(int* left, int* right) => left >= right;

    /// <summary>Clause 24.6.8 — a comparison against <c>null</c>, through the implicit conversion.</summary>
    public static bool IsNull(void* at) => at == null;

    /// <summary>
    /// Clause 24.6.8 — two <i>different</i> pointer types compared: both operands convert
    /// implicitly to <c>void*</c> first, which is the only reason this is legal.
    /// </summary>
    public static bool SameAddress(int* left, byte* right) => left == right;

    /// <summary>Clause 24.6.8 — a comparison used as a loop condition, which is the clause's everyday shape.</summary>
    public static int CountUntil(int* from, int* to)
    {
        int count = 0;

        for (int* at = from; at < to; at++)
        {
            count++;
        }

        return count;
    }
}
