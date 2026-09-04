// Clause 24.7 — the fixed statement.
//
// Two things about this clause are worth a query. The first is that the pointer the
// statement declares is a *local*, and `Indexer.IndexTree`'s switch has no arm for a local
// declarator outside a field — and `ScipSymbols.Of` would return null for it anyway, since
// SCIP gives nothing a method body introduces a global name. So `at`, `left` and `right`
// below have no declaration in the index at all, and a query that looks for them finds
// their *initializers* instead.
//
// The second is the pattern-based form. `fixed (int* at = source)` on a type with a
// `GetPinnableReference` method binds that method with no syntax node to hang the reference
// on: `Invoked` is called from an `InvocationExpressionSyntax`, and there is none here. So
// `UnsPinnable.GetPinnableReference` has a declaration and zero references, and looks
// unused to anything reading the index.

using System;

namespace Surface.Unsafe;

/// <summary>
/// Clause 24.7 — a type conforming to the fixed statement's pattern, by exposing
/// <c>GetPinnableReference</c>.
/// </summary>
public class UnsPinnable
{
    private readonly int[] _cells = [11, 13, 17];

    /// <summary>Clause 24.7 — the pattern's member: a <c>ref</c> return the statement pins.</summary>
    public ref int GetPinnableReference() => ref _cells[0];
}

/// <summary>Clause 24.7 — every initializer form the fixed statement accepts.</summary>
public unsafe class UnsFixedStatement
{
    private int _cell = 19;

    /// <summary>The cells pinned by the array form below.</summary>
    public int[] Cells = [23, 29, 31];

    /// <summary>Clause 24.7 — an array initializer: the pointer addresses element zero.</summary>
    public static int FirstOfArray(int[] cells)
    {
        fixed (int* at = cells)
        {
            return at is null ? 0 : *at;
        }
    }

    /// <summary>Clause 24.7 — a <c>string</c> initializer, which pins the characters.</summary>
    public static char FirstOfString(string text)
    {
        fixed (char* at = text)
        {
            return at is null ? '\0' : *at;
        }
    }

    /// <summary>Clause 24.7 — the <c>&amp;variable</c> form, over a moveable instance field.</summary>
    public int OfField()
    {
        fixed (int* at = &_cell)
        {
            return *at;
        }
    }

    /// <summary>Clause 24.7 — the <c>&amp;variable</c> form, over an array element.</summary>
    public int OfElement()
    {
        fixed (int* at = &Cells[1])
        {
            return *at;
        }
    }

    /// <summary>Clause 24.7 — two declarators in one statement, whose two pointers are both locals.</summary>
    public static long OfTwoArrays(int[] left, int[] right)
    {
        fixed (int* first = left, second = right)
        {
            return first - second;
        }
    }

    /// <summary>Clause 24.7 — nested fixed statements, whose scopes overlap.</summary>
    public static int OfNested(int[] outer, int[] inner)
    {
        fixed (int* first = outer)
        {
            fixed (int* second = inner)
            {
                return *first + *second;
            }
        }
    }

    /// <summary>Clause 24.7 — a fixed statement whose body is a single embedded statement, not a block.</summary>
    public static int OfEmbeddedStatement(int[] cells)
    {
        int total = 0;

        fixed (int* at = cells)
            total = *at;

        return total;
    }

    /// <summary>Clause 24.7 — the pattern-based form, through <see cref="UnsPinnable.GetPinnableReference"/>.</summary>
    public static int OfPinnable(UnsPinnable source)
    {
        fixed (int* at = source)
        {
            return *at;
        }
    }

    /// <summary>Clause 24.7 — a <see cref="Span{T}"/>, which conforms to the same pattern.</summary>
    public static int OfSpan(Span<int> cells)
    {
        fixed (int* at = cells)
        {
            return *at;
        }
    }

    /// <summary>Clause 24.7 — a <see cref="ReadOnlySpan{T}"/>, whose pattern member returns <c>ref readonly</c>.</summary>
    public static int OfReadOnlySpan(ReadOnlySpan<int> cells)
    {
        fixed (int* at = cells)
        {
            return *at;
        }
    }

    /// <summary>Clause 24.7 — a fixed statement over an array of pointers, whose pointer is a <c>int**</c>.</summary>
    public static long OfPointerArray(int*[] pointers)
    {
        fixed (int** at = pointers)
        {
            return (long)*at;
        }
    }

    /// <summary>Clause 24.7 — a fixed statement whose body walks the pinned memory.</summary>
    public static int SumOfArray(int[] cells)
    {
        int total = 0;

        fixed (int* origin = cells)
        {
            for (int* at = origin; at < origin + cells.Length; at++)
            {
                total += *at;
            }
        }

        return total;
    }

    // Clause 24.7, written nowhere because it does not compile: the pointer a fixed
    // statement declares is read-only for the statement's extent, so `at = null` inside the
    // body is CS1656. The restriction is on the local the index does not hold, which is why
    // it leaves nothing behind to assert.
}
