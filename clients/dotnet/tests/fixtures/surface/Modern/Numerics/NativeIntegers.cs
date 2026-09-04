namespace Surface.Modern.Numerics;

/// <summary>
/// C# 11 — Numeric IntPtr: <c>nint</c> and <c>nuint</c> became *simple types* aliasing
/// <see cref="IntPtr"/> and <see cref="UIntPtr"/>, rather than the distinct compiler-only
/// types C# 9 introduced. The consequence worth indexing is that the two spellings are now
/// the same type: a member declared as <c>nint</c> and a member declared as <c>IntPtr</c>
/// have identical signatures, so they cannot be overloads of each other.
/// </summary>
public static class NativeIntegers
{
    /// <summary>C# 11 — <c>nint</c> as a constant's type, with the simple type's members.</summary>
    public static nint Largest => nint.MaxValue;

    /// <summary>C# 11 — <c>nuint</c>, the unsigned half.</summary>
    public static nuint Smallest => nuint.MinValue;

    /// <summary>
    /// C# 11 — the same field, spelled two ways. Neither line changes the type: the field is
    /// <see cref="IntPtr"/> and the property returns it as <c>nint</c>.
    /// </summary>
    private static readonly IntPtr Offset = 8;

    /// <summary>Reads the <see cref="IntPtr"/> field as an <c>nint</c>.</summary>
    public static nint OffsetAsNative => Offset;

    /// <summary>C# 11 — <c>nint</c> arithmetic, in both contexts.</summary>
    public static nint Advance(nint address, int stride)
    {
        var moved = unchecked(address + stride);

        return checked(moved + Offset);
    }

    /// <summary>C# 11 — <c>nuint</c> arithmetic and a conversion, which is checked.</summary>
    public static nuint Size(nuint count, nuint width) => checked(count * width);

    /// <summary>C# 11 — <c>nint</c> in a pattern, where it behaves as a simple type.</summary>
    public static string Classify(nint value) => value switch
    {
        0 => "zero",
        1 or 2 => "small",
        < 0 => "negative",
        _ => "large",
    };

    /// <summary>C# 11 — the explicit conversions between the native integers and <c>long</c>.</summary>
    public static long Total()
    {
        var native = (nint)42;
        var unsignedNative = (nuint)42;

        return (long)native + (long)unsignedNative + (long)Advance(native, 2) + (long)Size(2, 3);
    }
}
