using System;

namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A whole-minor-unit money amount that declares one of every overloadable arithmetic,
/// increment and relational operator, each beside its C# 11 <c>checked</c> variant where
/// the language allows one.
/// </summary>
/// <remarks>
/// <para>Clause 12.4.3 names the overloadable operators; this type is the corpus's
/// complete instance of that list for the arithmetic family.</para>
/// <para>The point of interest for an index is that <c>operator +</c> and
/// <c>operator checked +</c> are two declarations written with the same token in one type.
/// They differ only by a modifier in source; in metadata they are <c>op_Addition</c> and
/// <c>op_CheckedAddition</c>. An identity minted from the token alone merges them.</para>
/// </remarks>
public readonly struct OpMoney : IEquatable<OpMoney>
{
    /// <summary>Constructs an amount from a count of minor units.</summary>
    public OpMoney(long minor) => Minor = minor;

    /// <summary>The amount, counted in minor units.</summary>
    public long Minor { get; }

    // 12.9.2 — unary plus. There is no `checked` form of unary plus.
    public static OpMoney operator +(OpMoney value) => value;

    // 12.9.3 — unary minus, and 12.4.3's checked counterpart of it.
    public static OpMoney operator -(OpMoney value) => new(unchecked(-value.Minor));

    public static OpMoney operator checked -(OpMoney value) => new(checked(-value.Minor));

    // 12.9.7 / 12.8.16 — the one `operator ++` declaration serves both the prefix and the
    // postfix form: the token is what binds, and the position is not part of the identity.
    public static OpMoney operator ++(OpMoney value) => new(unchecked(value.Minor + 1));

    public static OpMoney operator checked ++(OpMoney value) => new(checked(value.Minor + 1));

    public static OpMoney operator --(OpMoney value) => new(unchecked(value.Minor - 1));

    public static OpMoney operator checked --(OpMoney value) => new(checked(value.Minor - 1));

    // 12.12.5 — addition, three overloads at the same arity plus a checked variant of the
    // homogeneous one. 12.4.5 (binary operator overload resolution) picks between them.
    public static OpMoney operator +(OpMoney left, OpMoney right) =>
        new(unchecked(left.Minor + right.Minor));

    public static OpMoney operator checked +(OpMoney left, OpMoney right) =>
        new(checked(left.Minor + right.Minor));

    public static OpMoney operator +(OpMoney left, long right) => new(unchecked(left.Minor + right));

    public static OpMoney operator +(long left, OpMoney right) => new(unchecked(left + right.Minor));

    // 12.12.6 — subtraction.
    public static OpMoney operator -(OpMoney left, OpMoney right) =>
        new(unchecked(left.Minor - right.Minor));

    public static OpMoney operator checked -(OpMoney left, OpMoney right) =>
        new(checked(left.Minor - right.Minor));

    // 12.12.2 — multiplication.
    public static OpMoney operator *(OpMoney left, long right) => new(unchecked(left.Minor * right));

    public static OpMoney operator checked *(OpMoney left, long right) =>
        new(checked(left.Minor * right));

    // 12.12.3 — division.
    public static OpMoney operator /(OpMoney left, long right) => new(left.Minor / right);

    public static OpMoney operator checked /(OpMoney left, long right) => new(checked(left.Minor / right));

    // 12.12.4 — remainder. There is no checked form of `%`.
    public static OpMoney operator %(OpMoney left, long right) => new(left.Minor % right);

    // 12.14.1 — equality, declared pairwise as the language requires.
    public static bool operator ==(OpMoney left, OpMoney right) => left.Minor == right.Minor;

    public static bool operator !=(OpMoney left, OpMoney right) => left.Minor != right.Minor;

    // 12.14.1 — the relational quartet, also declared pairwise.
    public static bool operator <(OpMoney left, OpMoney right) => left.Minor < right.Minor;

    public static bool operator >(OpMoney left, OpMoney right) => left.Minor > right.Minor;

    public static bool operator <=(OpMoney left, OpMoney right) => left.Minor <= right.Minor;

    public static bool operator >=(OpMoney left, OpMoney right) => left.Minor >= right.Minor;

    // 12.4.6 — a conversion operator is a candidate for the conversions overload
    // resolution performs on an operator's operands, so it belongs in this set.
    // `op_Implicit` here; the type's only implicit conversion, so nothing merges.
    public static implicit operator OpMoney(long minor) => new(minor);

    // 12.9.8 — the explicit conversion a cast expression selects, beside its checked
    // variant: `op_Explicit` and `op_CheckedExplicit`, one token in source.
    public static explicit operator int(OpMoney value) => unchecked((int)value.Minor);

    public static explicit operator checked int(OpMoney value) => checked((int)value.Minor);

    /// <inheritdoc/>
    public bool Equals(OpMoney other) => Minor == other.Minor;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is OpMoney other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Minor.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => $"{Minor}m";
}
