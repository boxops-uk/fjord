namespace Surface.Modern.Operators;

/// <summary>
/// C# 11 — Relaxing shift operator requirements and the unsigned right-shift operator. Before
/// C# 11 a user-defined shift operator's right operand had to be <c>int</c>, and <c>&gt;&gt;&gt;</c>
/// did not exist. Both changes are visible only in a declaration's signature, so a type that
/// declares them is the only witness available.
/// </summary>
public readonly struct Bits
{
    /// <summary>Creates a bit set.</summary>
    public Bits(uint value) => Value = value;

    /// <summary>The bits.</summary>
    public uint Value { get; }

    /// <summary>
    /// C# 11 — Relaxing shift operator requirements: the right operand is a <c>string</c>,
    /// which no pre-C# 11 compiler would accept in a shift operator's signature.
    /// </summary>
    public static Bits operator <<(Bits left, string places) =>
        new(left.Value << (places.Length & 31));

    /// <summary>C# 11 — a shift whose right operand is an enum, another previously-illegal type.</summary>
    public static Bits operator >>(Bits left, ShiftWidth places) => new(left.Value >> (int)places);

    /// <summary>
    /// C# 11 — Unsigned right-shift operator: <c>&gt;&gt;&gt;</c> as a declarable operator. The
    /// compound form <c>&gt;&gt;&gt;=</c> is derived from it and cannot be declared separately.
    /// </summary>
    public static Bits operator >>>(Bits left, int places) => new(left.Value >>> places);

    /// <summary>The conventional <c>int</c>-shifted form, so the relaxed ones are a contrast.</summary>
    public static Bits operator <<(Bits left, int places) => new(left.Value << places);
}

/// <summary>How far to shift — an enum used as a shift operator's right operand.</summary>
public enum ShiftWidth
{
    /// <summary>One place.</summary>
    Single = 1,

    /// <summary>A nibble.</summary>
    Nibble = 4,
}

/// <summary>Applies each shift, including the compound assignments.</summary>
public static class ShiftOperators
{
    /// <summary>C# 11 — the relaxed right operands, at their call sites.</summary>
    public static uint Relaxed()
    {
        var bits = new Bits(0b1011);

        var shiftedByText = bits << "abc";
        var shiftedByEnum = bits >> ShiftWidth.Single;
        var shiftedByInt = bits << 2;

        return shiftedByText.Value + shiftedByEnum.Value + shiftedByInt.Value;
    }

    /// <summary>
    /// C# 11 — <c>&gt;&gt;&gt;</c> and <c>&gt;&gt;&gt;=</c>. The compound assignment resolves to
    /// the declared <c>&gt;&gt;&gt;</c> operator, which is the reference worth checking: the
    /// token in source is <c>&gt;&gt;&gt;=</c> and the member it names is <c>op_UnsignedRightShift</c>.
    /// </summary>
    public static uint Unsigned()
    {
        var bits = new Bits(0b1100_0000_0000_0000_0000_0000_0000_0000);

        var shifted = bits >>> 3;
        shifted >>>= 1;

        return shifted.Value;
    }

    /// <summary>C# 11 — <c>&gt;&gt;&gt;</c> on the built-in integral types, signed and unsigned.</summary>
    public static int Builtin()
    {
        var signed = -16;
        signed >>>= 2;

        var unsignedShift = -16 >>> 2;

        return signed + unsignedShift;
    }
}
