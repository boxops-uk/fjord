// Clause 24.6.5 — the address-of operator, `&V`.
//
// The operand is a variable reference, and *that* is what the index holds: `&point.Y` mints
// a `csharp.MemberAccessLocation` at `Y`, and `&UnsFunctionPointers.Twice` mints a reference
// whose target is the method — a `csharp.Method` fact, fully keyed — even though the type of
// the expression containing it is a function pointer, which cannot be keyed at all. One
// expression, one half of it expressible.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.5 — the address of each kind of fixed variable.</summary>
public unsafe class UnsAddressOf
{
    /// <summary>Clause 24.6.5 — the address of a local.</summary>
    public static int OfLocal()
    {
        int local = 3;
        int* at = &local;
        return *at;
    }

    /// <summary>Clause 24.6.5 — the address of a value parameter.</summary>
    public static int OfParameter(int value)
    {
        int* at = &value;
        return *at;
    }

    /// <summary>Clause 24.6.5 — the address of a field of a struct-typed local.</summary>
    public static int OfStructField()
    {
        UnsPoint point = default;
        int* at = &point.Y;
        *at = 4;
        return point.Y;
    }

    /// <summary>Clause 24.6.5 — the address of a pointer, which is a pointer to a pointer.</summary>
    public static int OfPointer(int* at)
    {
        int** handle = &at;
        return **handle;
    }

    /// <summary>Clause 24.6.5 — the address of an array element, which is moveable and so needs pinning.</summary>
    public static int OfArrayElement(int[] cells)
    {
        fixed (int* at = &cells[0])
        {
            return *at;
        }
    }

    /// <summary>Clause 24.6.5 — the address of a <c>stackalloc</c>'d element.</summary>
    public static int OfStackAllocatedElement()
    {
        int* cells = stackalloc int[2];
        int* second = &cells[1];
        *second = 9;
        return *second;
    }

    /// <summary>
    /// Clause 24.6.5's post-standard form — the address of a <i>method</i>, whose value is a
    /// function pointer. The method name resolves; the expression's type does not.
    /// </summary>
    public static delegate*<int, int> OfMethod() => &UnsFunctionPointers.Twice;

    /// <summary>Clause 24.6.5 — the address of a field of a struct-typed local, of nested struct type.</summary>
    public static byte OfNestedStructField()
    {
        UnsPoint.Corner corner = default;
        byte* at = &corner.Index;
        *at = 1;
        return corner.Index;
    }
}
