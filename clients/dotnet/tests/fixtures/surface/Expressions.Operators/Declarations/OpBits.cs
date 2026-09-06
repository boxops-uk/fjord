using System;

namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A fixed-width bit set that declares the logical (12.15) and shift (12.13) operators,
/// including the C# 11 unsigned right shift <c>&gt;&gt;&gt;</c> and a relaxed shift whose
/// right operand is not an <c>int</c>.
/// </summary>
/// <remarks>
/// Before C# 11 a user-defined shift operator's second operand had to be <c>int</c>. The
/// relaxation means one type can declare several <c>op_LeftShift</c> methods that differ
/// only in the second parameter's type, so 12.4.5's choice between them is the whole
/// content of a use like <c>bits &lt;&lt; "3"</c>.
/// </remarks>
public readonly struct OpBits
{
    /// <summary>Constructs a bit set from its raw value.</summary>
    public OpBits(uint raw) => Raw = raw;

    /// <summary>The raw bits.</summary>
    public uint Raw { get; }

    // 12.9.5 — bitwise complement.
    public static OpBits operator ~(OpBits value) => new(~value.Raw);

    // 12.15.2 — the logical operators, in their user-defined form.
    public static OpBits operator &(OpBits left, OpBits right) => new(left.Raw & right.Raw);

    public static OpBits operator |(OpBits left, OpBits right) => new(left.Raw | right.Raw);

    public static OpBits operator ^(OpBits left, OpBits right) => new(left.Raw ^ right.Raw);

    // 12.13 — the classic shift pair.
    public static OpBits operator <<(OpBits value, int count) => new(value.Raw << count);

    public static OpBits operator >>(OpBits value, int count) => new(value.Raw >> count);

    // 12.13 — the unsigned right shift, added in C# 11. `op_UnsignedRightShift`.
    public static OpBits operator >>>(OpBits value, int count) => new(value.Raw >>> count);

    // 12.13 — the relaxed form: a second `op_LeftShift` at the same arity, differing only
    // in the type of the operand that the `<<` token never names.
    public static OpBits operator <<(OpBits value, string count) =>
        new(value.Raw << int.Parse(count));

    /// <summary>The count of set bits, so the type has a use beyond its operators.</summary>
    public int Population => System.Numerics.BitOperations.PopCount(Raw);

    /// <inheritdoc/>
    public override string ToString() => Convert.ToString(Raw, 2);
}
