// Clause 20 — enums — and clause 20.1's general characteristics: an enum type is a distinct
// value type (8.3.10) that declares a set of named constants, every enum has an integral
// underlying type, and the enum's default value is zero whether or not a member says so.

namespace Surface.EnumsDelegates;

/// <summary>
/// 20 / 20.1 — the headline enum declaration. One syntax node mints a type and one constant
/// field per member, and each of those fields has as its type the enum that contains it.
/// </summary>
public enum EdColor
{
    /// <summary>20.4 — the first member with no initializer, so its value is zero.</summary>
    Red,

    /// <summary>20.4 — the next member with no initializer, so its value is one.</summary>
    Green,

    /// <summary>20.4 — and two.</summary>
    Blue,
}

/// <summary>
/// 20.1 hazard — an enum no member of which is zero. <c>default(EdVoltage)</c> is still
/// zero, so the type has a value that no declaration in this file produces.
/// </summary>
public enum EdVoltage
{
    Low = 1,
    High = 2,
}

/// <summary>20.1 — the general characteristics of an enum type, exercised at a use site.</summary>
public static class EdEnumGeneral
{
    /// <summary>20.1 — an enum is a value type, so this field holds a value, not a reference.</summary>
    public static EdColor Pen = EdColor.Green;

    /// <summary>
    /// 20.1 hazard — the zero of an enum with no zero member. The expression names the type
    /// and no member, so nothing at this site can carry a reference to a member declaration.
    /// </summary>
    public static EdVoltage Unset() => default;

    /// <summary>20.6 — the one implicit conversion into an enum type: the constant zero.</summary>
    public static EdColor FromZero() => 0;

    /// <summary>20.1 — assigning an enum copies it, because the type is a value type.</summary>
    public static EdColor Copy(EdColor source)
    {
        EdColor destination = source;
        return destination;
    }

    /// <summary>20.5 — an enum value boxes to its base class, which is written nowhere.</summary>
    public static object Box(EdColor value) => value;

    /// <summary>20.1 — an enum is a distinct type: two enums never share a value's type.</summary>
    public static bool Distinct() => Pen.GetType() != Unset().GetType();
}
