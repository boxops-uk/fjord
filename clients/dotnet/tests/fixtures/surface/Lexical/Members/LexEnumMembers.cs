// Clause 7.4.4 (enumeration members): an enum's members are its enumeration constants and
// the members it inherits from `System.Enum`. Two constants of one enum may share a value,
// which is the hazard here: `Small` and `Little` are both 1 and are two members, so a
// value-keyed index of enum constants loses one of them.

namespace Surface.Lexical.Members;

/// <summary>7.4.4: constants with explicit values, two of which are equal.</summary>
public enum LexEnumMembers
{
    /// <summary>Zero, which is what a default-initialised field of this type holds.</summary>
    Extent = 0,

    /// <summary>One.</summary>
    Small = 1,

    /// <summary>One again — a second member with the first one's value.</summary>
    Little = 1,

    /// <summary>Two.</summary>
    Large = 2,
}

/// <summary>
/// 7.4.4: constants with no explicit value, which take the previous one plus one, and a
/// non-default underlying type.
/// </summary>
public enum LexImplicitEnumMembers : byte
{
    /// <summary>Implicitly zero.</summary>
    Extent,

    /// <summary>Implicitly one.</summary>
    Next,

    /// <summary>Explicitly ten, which resets the run.</summary>
    Jump = 10,

    /// <summary>Implicitly eleven.</summary>
    AfterJump,
}

/// <summary>
/// 7.4.4: constants whose values are computed from the others rather than written out, so
/// the value an index holds is not a token in the source.
/// </summary>
[System.Flags]
public enum LexFlagEnumMembers : long
{
    /// <summary>No bits.</summary>
    None = 0,

    /// <summary>The first bit, written as a shift.</summary>
    First = 1L << 0,

    /// <summary>The second bit.</summary>
    Second = 1L << 1,

    /// <summary>Both bits, written as a reference to the other two members.</summary>
    Both = First | Second,

    /// <summary>An alias for <c>Both</c>, which is a fourth member with a third value.</summary>
    Extent = Both,
}

/// <summary>7.4.4: reads the enum constants, so each one has a reference.</summary>
public static class LexEnumMemberUses
{
    /// <summary>Sums the members of all three enums.</summary>
    public static long Total() =>
        (int)LexEnumMembers.Extent + (int)LexEnumMembers.Small + (int)LexEnumMembers.Little
        + (int)LexEnumMembers.Large
        + (byte)LexImplicitEnumMembers.Extent + (byte)LexImplicitEnumMembers.Next
        + (byte)LexImplicitEnumMembers.Jump + (byte)LexImplicitEnumMembers.AfterJump
        + (long)LexFlagEnumMembers.None + (long)LexFlagEnumMembers.First
        + (long)LexFlagEnumMembers.Second + (long)LexFlagEnumMembers.Both
        + (long)LexFlagEnumMembers.Extent;
}
