// Clause 15.2.2.4 — static classes. 15.2.2.4.1 states what a static class is (no instance
// members, no base class other than `object`, implicitly abstract and sealed, no
// instantiation); 15.2.2.4.2 states the only three places its *name* may appear.

namespace Surface.Classes;

/// <summary>
/// 15.2.2.4.1 hazard — a static class. `static` is the modifier written, and `abstract
/// sealed` is what metadata records: an index that stores modifiers from metadata and the
/// declaration both has two answers for one type, and they have to reconcile to one row.
/// </summary>
public static class ClsStaticUtility
{
    /// <summary>15.4 — a constant in a static class, which is implicitly static.</summary>
    public const int Limit = 64;

    /// <summary>15.5.2 — a static field; a static class may declare no other kind.</summary>
    internal static int Counter;

    /// <summary>15.2.2.4.2 — `typeof` on a static class type, in a field initializer.</summary>
    public static readonly System.Type SelfType = typeof(ClsStaticUtility);

    /// <summary>15.2.2.4.2 — `nameof` on a static class type, in a constant.</summary>
    public const string SelfName = nameof(ClsStaticUtility);

    /// <summary>15.2.2.4.1 — a static class nested in a static class.</summary>
    public static class ClsStaticNestedTools
    {
        /// <summary>15.4 — the constant the outer type's own reference below reads.</summary>
        public const string Tag = "tools";
    }

    /// <summary>15.3.9.1 — a *non*-static nested type inside a static class, which the
    /// "no instance members" rule permits: a nested type is not an instance member.</summary>
    public sealed class ClsStaticHostedValue
    {
        public int Amount;
    }
}

/// <summary>
/// 15.2.2.4.2 — every legal reference to a static class type, and there are only these: as
/// the left operand of a member access, as the operand of `typeof`, and inside `nameof`.
/// No field may have its type, no class may derive from it, and nothing may instantiate it.
/// </summary>
public sealed class ClsStaticClassReferences
{
    /// <summary>15.2.2.4.2 — member access on the static class type.</summary>
    public static readonly int CopiedLimit = ClsStaticUtility.Limit;

    /// <summary>15.2.2.4.2 — member access through a nested static class type.</summary>
    public static readonly string NestedTag = ClsStaticUtility.ClsStaticNestedTools.Tag;

    /// <summary>15.2.2.4.2 — `typeof`.</summary>
    public static readonly System.Type ReferencedType = typeof(ClsStaticUtility);

    /// <summary>15.2.2.4.2 — `nameof`, which is a reference that reaches metadata for nothing
    /// but the identifier's spelling.</summary>
    public const string ReferencedName = nameof(ClsStaticUtility);

    /// <summary>15.3.9.1 — the nested *non*-static type of a static class may be a field
    /// type, which is the one way a static class's nesting reaches an instance.</summary>
    public ClsStaticUtility.ClsStaticHostedValue? Hosted;
}
