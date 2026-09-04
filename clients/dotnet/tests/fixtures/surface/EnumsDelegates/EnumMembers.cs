// Clause 20.4 — enum members. Every member is a constant of the enum type itself, declared
// implicitly `public` with no modifier permitted; a member with no initializer takes the
// previous member's value plus one, or zero if it is the first; an initializer is a constant
// expression that may name other members of the same enum, in any order, so long as the
// naming is not circular. Every member is therefore a static literal field whose type is its
// own container — the one place in the language where that is the ordinary case.

namespace Surface.EnumsDelegates;

/// <summary>
/// 20.4 hazard — two members with one value. <c>Thin</c> and <c>Slim</c> are distinct
/// declarations of the constant 1; an identity keyed on the constant merges them, and a
/// round trip through the underlying type cannot tell which one it came from.
/// </summary>
public enum EdStrokeWeight
{
    /// <summary>20.4 — a member with an explicit value.</summary>
    Thin = 1,

    /// <summary>20.4 — a second member with the same value as <see cref="Thin"/>.</summary>
    Slim = 1,

    /// <summary>20.4 — a member whose value is its own.</summary>
    Thick = 2,
}

/// <summary>
/// 20.4 — implicit and explicit values interleaved. <c>Second</c> is 5 because it says so,
/// and <c>Third</c> is 6 because <c>Second</c> is 5 — a value produced by the member before
/// it rather than by anything written on its own line.
/// </summary>
public enum EdImplicitRun
{
    First,
    Second = 5,
    Third,
    Fourth,
}

/// <summary>
/// 20.4 hazard — a member initialized from a member declared after it. The reference runs
/// backwards through the file, so an index built in document order sees a use before its
/// declaration.
/// </summary>
public enum EdForwardReference
{
    /// <summary>20.4 — initialized from <see cref="Beta"/>, two lines down.</summary>
    Alpha = Beta,

    /// <summary>20.4 — the member the one above depends on.</summary>
    Beta = 2,

    /// <summary>20.4 — initialized from the member above it, which is the ordinary case.</summary>
    Gamma = Beta + 1,
}

/// <summary>
/// 20.4 — a bit-flags enum, whose members combine. Two of these members are composites
/// written as expressions over the others, and one of them is written twice: once as a
/// literal and once as the expression that produces it.
/// </summary>
[System.Flags]
public enum EdAccess : uint
{
    /// <summary>20.4 — the zero member, which a flags enum needs for the empty set.</summary>
    None = 0,

    Read = 1,
    Write = 2,
    Append = 4,

    /// <summary>20.4 — a composite value, as a constant expression over two members.</summary>
    ReadWrite = Read | Write,

    /// <summary>20.4 hazard — the same value as <see cref="Everything"/>, spelled as a literal.</summary>
    All = 7,

    /// <summary>20.4 hazard — the same value as <see cref="All"/>, spelled as an expression.</summary>
    Everything = Read | Write | Append,
}

/// <summary>
/// 20.4 hazard — a member whose name is the name of the enum that declares it. Two
/// declarations, one identifier, and one of them is the container of the other:
/// <c>EdEcho.EdEcho</c> is a field of type <c>EdEcho</c> declared in <c>EdEcho</c>.
/// </summary>
public enum EdEcho
{
    /// <summary>20.4 — the member that repeats its container's name.</summary>
    EdEcho,
}

/// <summary>20.4 — an enum whose member names are shared with another enum below.</summary>
public enum EdBearing
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// 20.4 hazard — <c>North</c> and <c>South</c> again, in a different enum and at different
/// values. Only the container tells the four declarations apart.
/// </summary>
public enum EdCompass : short
{
    South = -1,
    Level = 0,
    North = 1,
}

/// <summary>20.4 — attributes on enum members, which the grammar allows before each name.</summary>
public enum EdAttributedMembers
{
    /// <summary>20.4 — a member carrying an attribute of its own.</summary>
    [System.Obsolete("superseded by Current, and referenced by nothing on purpose")]
    Legacy = 1,

    /// <summary>20.4 — a member with two attributes.</summary>
    [System.ComponentModel.Description("the value in use")]
    [System.Diagnostics.CodeAnalysis.Experimental("EdEnum001")]
    Current = 2,
}

/// <summary>
/// 20.4 hazard — members named with verbatim identifiers (6.4.3). The `@` is not part of the
/// name, so these members are called `new` and `class`, which no other declaration in the
/// corpus can be called without it.
/// </summary>
public enum EdVerbatimMembers
{
    @new = 1,
    @class = 2,
}

/// <summary>20.4 — enum members at use sites, qualified by their containing enum.</summary>
public static class EdMemberUse
{
    /// <summary>20.4 — the two members that share the value 1, named separately.</summary>
    public static EdStrokeWeight Thin = EdStrokeWeight.Thin;

    /// <summary>20.4 — the alias, which is a different declaration and the same value.</summary>
    public static EdStrokeWeight Slim = EdStrokeWeight.Slim;

    /// <summary>20.4 — a member reached through the enum that repeats its own name.</summary>
    public static EdEcho Echo() => EdEcho.EdEcho;

    /// <summary>20.4 — a member named by a verbatim identifier at the use site too.</summary>
    public static EdVerbatimMembers Fresh() => EdVerbatimMembers.@new;

    /// <summary>20.4 — the two composite members that hold one value.</summary>
    public static bool CompositesAgree() => EdAccess.All == EdAccess.Everything;

    /// <summary>
    /// 20.4 hazard — <c>nameof</c> of a member. The result is the member's name as a string
    /// constant, so the reference is folded away and nothing is left at run time to point at
    /// the declaration.
    /// </summary>
    public static string Named() => nameof(EdAccess.Append);

    /// <summary>20.4 — a member of the nested enum, qualified through its containing type.</summary>
    public static EdEnumClassHost.EdNestedInClass Nested() =>
        EdEnumClassHost.EdNestedInClass.Busy;

    /// <summary>20.4 — a member of the enum in the nested namespace.</summary>
    public static Deep.EdDeepEnum Deeper() => Deep.EdDeepEnum.Deeper;
}
