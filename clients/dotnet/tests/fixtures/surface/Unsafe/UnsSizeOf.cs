// Clause 24.6.9 — the `sizeof` operator.
//
// `sizeof` is the one pointer-adjacent operator whose operand is a *type*, so it is the one
// that leaves references behind: `sizeof(UnsPoint)` mints a `csharp.TypeLocation` at
// `UnsPoint`, and `sizeof(T)` one at the type parameter, which is keyed as the `[T]`
// descriptor. `sizeof(int)` and `sizeof(delegate*<int, int>)` mint nothing at all — the
// first because a predefined type is a keyword and never reaches the walk as a name, the
// second because a function pointer type is spelled entirely in keywords too.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.9 — <c>sizeof</c> over every operand kind the clause admits.</summary>
public unsafe class UnsSizeOf
{
    /// <summary>Clause 24.6.9 — a predefined type, which is a constant expression.</summary>
    public const int OfInt32 = sizeof(int);

    /// <summary>Clause 24.6.9 — a one-byte predefined type, also constant.</summary>
    public const int OfByte = sizeof(byte);

    /// <summary>Clause 24.6.9 — <c>bool</c>, whose size the clause fixes at one.</summary>
    public const int OfBool = sizeof(bool);

    /// <summary>
    /// Clause 24.6.9 — <c>decimal</c>, which is <i>not</i> in the clause's list of operands
    /// whose <c>sizeof</c> is a constant expression, so this one cannot be a <c>const</c>.
    /// </summary>
    public static int OfDecimal => sizeof(decimal);

    /// <summary>Clause 24.6.9 — a struct from source, which is not a constant expression.</summary>
    public static int OfPoint => sizeof(UnsPoint);

    /// <summary>Clause 24.6.9 — a nested struct from source.</summary>
    public static int OfCorner => sizeof(UnsPoint.Corner);

    /// <summary>Clause 24.6.9 — an enum type, whose size is its underlying type's.</summary>
    public static int OfFlag => sizeof(UnsFlag);

    /// <summary>Clause 24.6.9 — a pointer type, whose size is the platform's.</summary>
    public static int OfPointer => sizeof(int*);

    /// <summary>Clause 24.6.9 — <c>void*</c>, which has a size even though it has no pointee.</summary>
    public static int OfUnknownPointer => sizeof(void*);

    /// <summary>Clause 24.6.9 — a pointer to a pointer.</summary>
    public static int OfHandle => sizeof(int**);

    /// <summary>Clause 24.6.9 — a function pointer type, which is unmanaged and so has a size.</summary>
    public static int OfFunctionPointer => sizeof(delegate*<int, int>);

    /// <summary>Clause 24.6.9 — a struct containing a fixed-size buffer, whose size includes the buffer.</summary>
    public static int OfBufferHost => sizeof(UnsFixedBufferHost);

    /// <summary>Clause 24.6.9 — a type parameter, which needs the <c>unmanaged</c> constraint.</summary>
    public static int Of<T>()
        where T : unmanaged
        => sizeof(T);

    /// <summary>Clause 24.6.9 — <c>sizeof</c> as the scale in an offset computation, its usual use.</summary>
    public static byte* ElementAt(byte* origin, int index) => origin + (index * sizeof(UnsPoint));
}
