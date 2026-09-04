// Clause 20.4 and 20.6 at use sites where the containing enum is not written. `using static`
// (14.5.5) brings an enum's members into scope as bare identifiers, and a using alias
// directive (14.5.2) gives the enum type a second name — so the same member declaration is
// reached here by three different spellings, only one of which contains the enum's own name.

using static Surface.EnumsDelegates.EdAccess;

using EdShade = Surface.EnumsDelegates.EdColor;

namespace Surface.EnumsDelegates;

/// <summary>
/// 20.4 hazard — enum members named without their enum. Every identifier below is a
/// reference to a member of <see cref="EdAccess"/> or <see cref="EdColor"/>, and the type
/// that declares it appears only in the using directives at the top of the file.
/// </summary>
public static class EdUnqualifiedUse
{
    /// <summary>
    /// 20.4 hazard — two bare identifiers, each a member of an enum no token here names.
    /// The reference has to be resolved through a file-scoped directive to be attributed at
    /// all, and the result is the same declaration <c>EdAccess.Read</c> names elsewhere.
    /// </summary>
    public static EdAccess Both() => Read | Write;

    /// <summary>20.4 — a bare member in a bitwise test.</summary>
    public static bool CanAppend(EdAccess value) => (value & Append) != None;

    /// <summary>20.4 — a bare member as the whole expression.</summary>
    public static EdAccess Nothing() => None;

    /// <summary>
    /// 20.4 hazard — the same member reached through a using alias for its type. <c>EdShade</c>
    /// and <c>EdColor</c> are one type under two names, so <c>EdShade.Red</c> and
    /// <c>EdColor.Red</c> are one member under two spellings and must merge.
    /// </summary>
    public static EdShade Aliased() => EdShade.Red;

    /// <summary>
    /// 20.4 hazard — the alias and the real name compared. This warns CS1718, "comparison
    /// made to same variable", which is the compiler stating the merge claim itself: the two
    /// spellings are one member, and it can see that before an index can.
    /// </summary>
    public static bool OneMember() => EdShade.Red == EdColor.Red;

    /// <summary>20.4 — the alias in a declaration position, as the type of a local.</summary>
    public static string Local()
    {
        EdShade shade = EdShade.Blue;
        return shade.ToString();
    }
}
