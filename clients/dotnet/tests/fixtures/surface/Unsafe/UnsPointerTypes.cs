// Clause 24.3 — pointer types.
//
// `type* ` for any unmanaged pointee, `void*` for an unknown one, and both nested to any
// depth. Every field below is a `csharp.Field` whose `type` is the `pointerType`
// alternative of `csharp.AType`, and `csharp.PointerType`'s only key field is the pointee —
// so `int*` written eleven times in this project is one row, and `int**` is a row whose
// pointee is itself the pointer alternative. The pointer type has no name of its own:
// `ScipSymbols.Of` returns null for any symbol whose `Name` is empty, which a pointer
// type's is, so nothing in `csharp.Symbol` spells `int*`.

namespace Surface.Unsafe;

/// <summary>An unmanaged struct: the pointee for <c>UnsPoint*</c>, and a nested one inside it.</summary>
public struct UnsPoint
{
    /// <summary>The horizontal coordinate.</summary>
    public int X;

    /// <summary>The vertical coordinate.</summary>
    public int Y;

    /// <summary>Both coordinates added, so the struct has a member as well as state.</summary>
    public int Sum() => X + Y;

    /// <summary>A nested unmanaged struct, so that a pointee can be a nested type.</summary>
    public struct Corner
    {
        /// <summary>Which corner, clockwise from the origin.</summary>
        public byte Index;
    }
}

/// <summary>An enum, so that a pointee can be an enum type: clause 24.3 admits any unmanaged type.</summary>
public enum UnsFlag : byte
{
    /// <summary>Nothing set.</summary>
    None = 0,

    /// <summary>Set.</summary>
    Set = 1,
}

/// <summary>
/// Clause 24.3 — one field per pointee kind the clause admits.
/// </summary>
/// <remarks>
/// The fields are public and unassigned on purpose: a pointer field's <i>type</i> is the
/// fact under test, and giving each one a value would need an address to take.
/// </remarks>
public unsafe class UnsPointerTypes
{
    /// <summary>Clause 24.3 — a pointer to a predefined integral type.</summary>
    public int* Integer;

    /// <summary>Clause 24.3 — a pointer to a one-byte type.</summary>
    public byte* Byte;

    /// <summary>Clause 24.3 — a pointer to a signed one-byte type.</summary>
    public sbyte* Signed;

    /// <summary>Clause 24.3 — a pointer to a character.</summary>
    public char* Character;

    /// <summary>Clause 24.3 — a pointer to a Boolean.</summary>
    public bool* Boolean;

    /// <summary>Clause 24.3 — a pointer to a floating-point type.</summary>
    public double* Weight;

    /// <summary>Clause 24.3 — a pointer to <c>decimal</c>, which is unmanaged because its fields are.</summary>
    public decimal* Money;

    /// <summary>Clause 24.3 — a pointer to a platform-sized integer.</summary>
    public nint* Native;

    /// <summary>Clause 24.3 — <c>void*</c>, a pointer to an unknown type.</summary>
    public void* Unknown;

    /// <summary>Clause 24.3 — a pointer to a pointer.</summary>
    public int** IntegerHandle;

    /// <summary>Clause 24.3 — three levels, to show the nesting is not special-cased at two.</summary>
    public int*** IntegerHandleHandle;

    /// <summary>Clause 24.3 — a pointer to <c>void*</c>, which is a pointer to a pointer with no pointee.</summary>
    public void** UnknownHandle;

    /// <summary>Clause 24.3 — a pointer to a struct declared in this project.</summary>
    public UnsPoint* Point;

    /// <summary>Clause 24.3 — a pointer to a struct nested inside another struct.</summary>
    public UnsPoint.Corner* Corner;

    /// <summary>Clause 24.3 — a pointer to a pointer to a struct.</summary>
    public UnsPoint** PointHandle;

    /// <summary>Clause 24.3 — a pointer to an enum type.</summary>
    public UnsFlag* Flag;

    /// <summary>Clause 24.3 — a pointer to a tuple, whose underlying <c>ValueTuple</c> is unmanaged.</summary>
    public (int Row, int Column)* Cell;

    /// <summary>Clause 24.3 — a pointer to a struct from the framework rather than from source.</summary>
    public System.DateTime* When;

    /// <summary>
    /// Clause 24.3 — a pointer to a type parameter, which needs the <c>unmanaged</c>
    /// constraint to be a pointee at all.
    /// </summary>
    /// <remarks>
    /// The pointee is the <c>typeParameter</c> alternative of <c>csharp.AType</c> nested
    /// inside the <c>pointerType</c> one, and <c>unmanaged</c> in the <c>where</c> clause
    /// is a constraint keyword that binds to no type.
    /// </remarks>
    public static T* FirstOf<T>(T* items)
        where T : unmanaged
        => items;

    /// <summary>Clause 24.3 — a pointer-typed parameter and a pointer-typed return, in one signature.</summary>
    public static int** Through(int** rows) => rows;
}
