// Clause 15.4 — constants. A constant is a member whose value is computed at compile time, so
// its type must be one the standard lists: an integral type, a floating-point type, `decimal`,
// `bool`, `char`, `string`, an enumeration type, or a reference type whose value is `null`.
// Every one of those appears below. A constant of a nullable value type is *not* on that list,
// and the comment where one would go says so rather than a declaration that would not compile.

namespace Surface.Classes;

/// <summary>15.4 — an enumeration type, so a constant can have one.</summary>
public enum ClsGrade
{
    Low = 0,
    Mid = 1,
    High = 2,
}

/// <summary>
/// 15.4 hazard — every constant type at once. Two of these constants have the same value and
/// different names (`SByteLimit` and the negative of `ByteLimit` do not collide, but
/// <see cref="ClsConstValueTwins"/> below makes the point directly); an index that keys a
/// constant on its value rather than on its declaration merges members that share one.
/// </summary>
public class ClsConstantTypes
{
    /// <summary>15.4 — `sbyte`.</summary>
    public const sbyte SByteLimit = -8;

    /// <summary>15.4 — `byte`.</summary>
    public const byte ByteLimit = 8;

    /// <summary>15.4 — `short`.</summary>
    public const short ShortLimit = -16;

    /// <summary>15.4 — `ushort`.</summary>
    public const ushort UShortLimit = 16;

    /// <summary>15.4 — `int`, the type a bare integer literal has.</summary>
    public const int IntLimit = -32;

    /// <summary>15.4 — `uint`, with the suffix the literal needs.</summary>
    public const uint UIntLimit = 32u;

    /// <summary>15.4 — `long`.</summary>
    public const long LongLimit = -64L;

    /// <summary>15.4 — `ulong`.</summary>
    public const ulong ULongLimit = 64UL;

    /// <summary>15.4 — `char`.</summary>
    public const char CharMark = 'k';

    /// <summary>15.4 — `float`.</summary>
    public const float FloatRatio = 0.5f;

    /// <summary>15.4 — `double`.</summary>
    public const double DoubleRatio = 0.25;

    /// <summary>15.4 — `decimal`, which the standard lists although its value is not a
    /// metadata constant in the same way the others are.</summary>
    public const decimal DecimalRatio = 0.125m;

    /// <summary>15.4 — `bool`.</summary>
    public const bool Enabled = true;

    /// <summary>15.4 — `string`, the one reference type whose constants can be non-null.</summary>
    public const string Label = "constants";

    /// <summary>15.4 — an enumeration type, with a member of it as the value.</summary>
    public const ClsGrade Grade = ClsGrade.High;

    /// <summary>15.4 — a reference type, whose constant value can only be `null`.</summary>
    public const ClsPlain? NullReference = null;

    /// <summary>15.4 — `object`, likewise null.</summary>
    public const object? NullObject = null;

    /// <summary>15.4 — a `string` constant that *is* null, which the type permits and the
    /// literal shape of the others hides.</summary>
    public const string? NullString = null;

    // 15.4 — a constant of a nullable value type is not permitted: `const int? x = null;` is
    // an error, because `System.Nullable<int>` is neither on the list nor a reference type.

    /// <summary>15.4 — a constant whose value is a constant expression over another constant
    /// in the same class, which is a reference resolved at compile time.</summary>
    public const int Derived = IntLimit + 1;

    /// <summary>15.4 — a constant expression over a constant in *another* class.</summary>
    public const int Borrowed = ClsStaticUtility.Limit * 2;

    /// <summary>15.4 — a `string` constant built by concatenation and `nameof`, so the
    /// initializer contains a reference to a type as well as to a constant.</summary>
    public const string Composed = Label + "-" + nameof(ClsConstantTypes);

    /// <summary>
    /// 15.4 hazard — one `const` declaration, three constant members. The declaration node is
    /// shared and the members are not: an identity minted per declaration collapses three
    /// members into one, and the values differ so the collapse is visible.
    /// </summary>
    public const int First = 1, Second = 2, Third = 3;

    /// <summary>
    /// 15.4 hazard — a nested type declaring a constant with the same simple name as its
    /// container's. `IntLimit` is declared twice in this file, in two containers, with two
    /// values and two types.
    /// </summary>
    public sealed class Inner
    {
        /// <summary>15.4 — the same simple name as <c>ClsConstantTypes.IntLimit</c>.</summary>
        public const uint IntLimit = 99;
    }
}

/// <summary>15.4 — a base class with a constant, hidden below.</summary>
public class ClsConstBase
{
    /// <summary>15.4 — the constant `ClsConstDerived` hides.</summary>
    public const int Ceiling = 10;
}

/// <summary>
/// 15.4 hazard — `new const` hiding an inherited constant. A constant is bound at the call
/// site, so `ClsConstBase.Ceiling` and `ClsConstDerived.Ceiling` are two members with two
/// values and one name, and code compiled against one keeps that one's value.
/// </summary>
public sealed class ClsConstDerived : ClsConstBase
{
    /// <summary>15.4 — hides the base's `Ceiling`.</summary>
    public new const int Ceiling = 20;
}

/// <summary>
/// 15.4 hazard — two constants of one type with one value. Nothing but the name separates
/// them, which is the minimal case for an identity that keys on anything else.
/// </summary>
public static class ClsConstValueTwins
{
    /// <summary>15.4 — the left twin.</summary>
    public const int Left = 7;

    /// <summary>15.4 — the right twin, indistinguishable by value.</summary>
    public const int Right = 7;

    /// <summary>15.4 — a third, whose value is a reference to the first.</summary>
    public const int Echo = Left;
}
