// Clause 24.5 — pointer conversions: 24.5.1 (General) and 24.5.2 (Pointer arrays).
//
// A conversion has no predicate of its own here. What a cast leaves in the index is the
// *type reference* at the pointee's name — `(UnsPoint*)` mints a `csharp.TypeLocation` at
// `UnsPoint`, while `(byte*)`, `(long)` and `(void*)` mint none, because a predefined type
// is a keyword and never reaches the walk as a `SimpleNameSyntax`. So the census row for
// 24.5.1 is exercised by declarations whose bodies are conversions, and the query that
// finds them looks for the pointee names.

using System;

namespace Surface.Unsafe;

/// <summary>Clause 24.5.1 — every pointer conversion the clause lists, one per member.</summary>
public unsafe class UnsPointerConversions
{
    /// <summary>Clause 24.5.1 — the implicit conversion from any pointer type to <c>void*</c>.</summary>
    public static void* ToUnknown(int* from) => from;

    /// <summary>Clause 24.5.1 — the implicit conversion from <c>null</c> to any pointer type.</summary>
    public static int* FromNull() => null;

    /// <summary>Clause 24.5.1 — the explicit conversion from <c>void*</c> to a pointer type.</summary>
    public static int* FromUnknown(void* from) => (int*)from;

    /// <summary>Clause 24.5.1 — the explicit conversion between two unrelated pointer types.</summary>
    public static byte* Reinterpret(int* from) => (byte*)from;

    /// <summary>Clause 24.5.1 — a cast whose target names a type from source, so the name is a reference.</summary>
    public static UnsPoint* AsPoint(void* from) => (UnsPoint*)from;

    /// <summary>Clause 24.5.1 — a cast to a pointer to a pointer.</summary>
    public static int** AsHandle(void* from) => (int**)from;

    /// <summary>Clause 24.5.1 — pointer to <c>long</c>, the widest signed integral the clause admits.</summary>
    public static long ToInt64(int* from) => (long)from;

    /// <summary>Clause 24.5.1 — pointer to <c>ulong</c>.</summary>
    public static ulong ToUInt64(void* from) => (ulong)from;

    /// <summary>Clause 24.5.1 — pointer to <c>int</c>, which truncates on a 64-bit platform and is still legal.</summary>
    public static int ToInt32(int* from) => (int)from;

    /// <summary>Clause 24.5.1 — pointer to <c>byte</c>: the conversion is unchecked, and never throws.</summary>
    public static byte ToByte(byte* from) => (byte)from;

    /// <summary>Clause 24.5.1 — <c>long</c> to a pointer type.</summary>
    public static int* FromInt64(long from) => (int*)from;

    /// <summary>Clause 24.5.1 — <c>uint</c> to a pointer type.</summary>
    public static void* FromUInt32(uint from) => (void*)from;

    /// <summary>Clause 24.5.1 — a platform-sized integer to a pointer type, and back.</summary>
    public static nint RoundTripNative(nint from) => (nint)(void*)from;

    /// <summary>Clause 24.5.1 — <see cref="IntPtr"/>, which is the same type by another name.</summary>
    public static int* FromIntPtr(IntPtr from) => (int*)from;

    /// <summary>Clause 24.5.2 — an array <i>of</i> pointers: the array is managed, the elements are not.</summary>
    public static int*[] MakePointerArray() => new int*[2];

    /// <summary>Clause 24.5.2 — a pointer array as a parameter, read through element access.</summary>
    public static int SumThrough(int*[] pointers)
    {
        int total = 0;

        foreach (int* at in pointers)
        {
            total += *at;
        }

        return total;
    }

    /// <summary>Clause 24.5.2 — a jagged array of <c>void*</c>.</summary>
    public static void*[][] MakeJagged() => new void*[1][];

    /// <summary>Clause 24.5.2 — a rectangular array of pointers, whose <c>rank</c> is 2.</summary>
    public static int*[,] MakeRectangular() => new int*[2, 2];

    /// <summary>Clause 24.5.2 — an array of function pointers, whose element type cannot be keyed.</summary>
    public static delegate*<int, int>[] MakeFunctionPointerArray() => new delegate*<int, int>[1];

    /// <summary>
    /// Clause 24.5.2 — a pointer array is not itself a pointer, so it is pinned like any
    /// other array: this is the one place the two clauses meet.
    /// </summary>
    public static long FirstAddress(int*[] pointers)
    {
        fixed (int** at = pointers)
        {
            return (long)*at;
        }
    }
}
