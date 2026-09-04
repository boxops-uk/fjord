// Clause 19.2.4 — base interfaces. An interface may inherit from zero or more interfaces; its
// inherited members are members of it; a base list may name a constructed interface; and a
// derived interface may hide an inherited member with `new`.

namespace Surface.Interfaces;

/// <summary>19.2.4 — a base interface with two members, inherited below by four others.</summary>
public interface IfaceBaseAlpha
{
    /// <summary>19.2.4 — inherited by every interface that names this one.</summary>
    string Name { get; }

    /// <summary>19.2.4 — inherited likewise.</summary>
    void Reset();
}

/// <summary>19.2.4 — a second, unrelated base interface.</summary>
public interface IfaceBaseBeta
{
    /// <summary>19.2.4 — the one member of the second base.</summary>
    int Order { get; }
}

/// <summary>
/// 19.2.4 hazard — two base interfaces in one base list. The members of
/// <see cref="IfaceBaseAlpha"/> and <see cref="IfaceBaseBeta"/> are members of this interface
/// without being declared in it, so an index either records inherited members here — and then
/// has to give them identities distinct from the declared ones — or records only the two edges.
/// </summary>
public interface IfaceDerivedBoth : IfaceBaseAlpha, IfaceBaseBeta
{
    /// <summary>19.2.4 — the only member this interface itself declares.</summary>
    void Both();
}

/// <summary>19.2.4 — the left arm of a diamond.</summary>
public interface IfaceDiamondLeft : IfaceBaseAlpha
{
    /// <summary>19.2.4 — a member the join inherits through the left arm only.</summary>
    void Left();
}

/// <summary>19.2.4 — the right arm.</summary>
public interface IfaceDiamondRight : IfaceBaseAlpha
{
    void Right();
}

/// <summary>
/// 19.2.4 hazard — the join of the diamond. <see cref="IfaceBaseAlpha"/> is a base interface of
/// this one twice over, through two different paths, and it is one interface in the set either
/// way: the transitive closure has to hold it once while two paths reach it.
/// </summary>
public interface IfaceDiamondJoin : IfaceDiamondLeft, IfaceDiamondRight
{
    void Join();
}

/// <summary>
/// 19.2.4 hazard — a derived interface hiding an inherited member with `new`. Two members
/// spelled <c>Name</c> are now visible on this type, of two different types, and only the
/// declaring interface separates them.
/// </summary>
public interface IfaceHidesBaseMember : IfaceBaseAlpha
{
    /// <summary>19.2.4 — hides <see cref="IfaceBaseAlpha.Name"/>, and is an <c>int</c>.</summary>
    new int Name { get; }
}

/// <summary>
/// 19.2.4 hazard — a base list naming a constructed interface. The substitution
/// <c>TItem = int</c> is part of the base type and not of either declaration.
/// </summary>
public interface IfaceIntSource : IfaceSource<int>
{
    /// <summary>19.2.4 — a member alongside the inherited <c>Current</c> and <c>Take</c>.</summary>
    int Sum();
}

/// <summary>
/// 19.2.4 hazard — a generic interface whose base list names two constructed interfaces built
/// with its own type parameter. Both base types are open, and both close when this one does.
/// </summary>
/// <typeparam name="TItem">19.2.4 — passed straight through to both base interfaces.</typeparam>
public interface IfaceRelay<TItem> : IfaceSource<TItem>, IfaceSink<TItem>
{
    /// <summary>19.2.4 — a member that uses the parameter in both variance positions, which is
    /// safe because this declaration annotates it with neither.</summary>
    TItem Relay(TItem item);
}

/// <summary>19.2.4 — the inherited members used through the derived interfaces.</summary>
public static class IfaceBaseUse
{
    /// <summary>19.2.4 — a member of a base interface reached through a derived interface type,
    /// which is the reference the inheritance exists to permit.</summary>
    public static string NameThroughDerived(IfaceDerivedBoth both) => both.Name;

    /// <summary>19.2.4 — a member inherited through two paths of a diamond, reached once.</summary>
    public static void ResetThroughJoin(IfaceDiamondJoin join) => join.Reset();

    /// <summary>19.2.4 — the hiding member, reached by its own simple name.</summary>
    public static int HidingName(IfaceHidesBaseMember hidden) => hidden.Name;

    /// <summary>19.2.4 — the hidden member, reached only by converting to the base interface.</summary>
    public static string HiddenName(IfaceHidesBaseMember hidden) => ((IfaceBaseAlpha)hidden).Name;

    /// <summary>19.2.4 — an inherited member of a constructed base interface.</summary>
    public static int CurrentOfIntSource(IfaceIntSource source) => source.Current;
}
