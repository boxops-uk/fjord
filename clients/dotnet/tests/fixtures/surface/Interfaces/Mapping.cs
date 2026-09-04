// Clauses 19.6.3 to 19.6.8 — what happens after the base list is written: the interface set must
// hold each interface once (19.6.3), the members are mapped onto implementations (19.6.5), a
// derived class inherits its base's mappings (19.6.6) unless it re-implements the interface
// (19.6.7), and an abstract class may map an interface member onto an abstract member of its own
// (19.6.8).
//
// 19.6.3's own hazard is written here in the shape that is safe to index: two constructed forms
// of one generic interface in one base list, implemented by overloads. The property form of the
// same shape — two explicit implementations named `IfaceTagged<int>.Tag` and
// `IfaceTagged<string>.Tag` — is a quarantined collision and is deliberately absent; README.md
// records the prediction.

namespace Surface.Interfaces;

/// <summary>
/// 19.6.3 — a generic interface with one member whose signature mentions its type parameter, so
/// two constructions of it can be implemented by two overloads rather than by two members of one
/// name.
/// </summary>
/// <typeparam name="TItem">19.6.3 — appears in an input position only.</typeparam>
public interface IfaceAccepts<TItem>
{
    /// <summary>19.6.3 — the member each construction contributes once.</summary>
    bool Accepts(TItem item);
}

/// <summary>
/// 19.6.3 hazard — one generic interface declaration named twice in one base list, at two
/// substitutions. The uniqueness rule permits this because <c>IfaceAccepts&lt;int&gt;</c> and
/// <c>IfaceAccepts&lt;string&gt;</c> are different types; the interface set holds two entries
/// whose declaration is one, and both base-type edges leave this class for it.
/// </summary>
public sealed class IfaceAcceptsBoth : IfaceAccepts<int>, IfaceAccepts<string>
{
    /// <summary>19.6.5 — implements <c>IfaceAccepts&lt;int&gt;.Accepts</c> implicitly.</summary>
    public bool Accepts(int item) => item > 0;

    /// <summary>19.6.5 — implements <c>IfaceAccepts&lt;string&gt;.Accepts</c> implicitly. Two
    /// members of one name in one class, separated by their parameter types, each mapped from a
    /// different construction of one interface declaration.</summary>
    public bool Accepts(string item) => item.Length > 0;
}

/// <summary>
/// 19.6.3 hazard — the rule as the clause states it: the interfaces a *generic* declaration
/// implements shall stay unique for every constructed type. <c>TItem[]</c> can never be
/// <c>int</c>, so no substitution makes these two base types the same and the declaration is
/// permitted. The pair the rule refuses — <c>IfaceAccepts&lt;TFirst&gt;</c> beside
/// <c>IfaceAccepts&lt;TSecond&gt;</c>, which collide when both are substituted alike — is
/// CS0695 and cannot be written at all.
/// </summary>
/// <typeparam name="TItem">19.6.3 — the element type, which keeps the two apart.</typeparam>
public sealed class IfaceUniqueAccepts<TItem> : IfaceAccepts<TItem[]>, IfaceAccepts<int>
{
    /// <summary>19.6.3 — implements the open construction.</summary>
    public bool Accepts(TItem[] item) => item.Length > 0;

    /// <summary>19.6.3 — implements the closed one, in the same class.</summary>
    public bool Accepts(int item) => item != 0;
}

/// <summary>
/// 19.6.3 hazard — an interface named in a base list that also arrives through inheritance.
/// <see cref="IfaceBaseAlpha"/> is a base interface of <see cref="IfaceDiamondJoin"/> and is
/// named here as well, and the interface set holds it once however many paths reach it.
/// </summary>
public sealed class IfaceRepeatedBase : IfaceDiamondJoin, IfaceBaseAlpha
{
    /// <summary>19.6.5 — one member, mapped from the interface however it was reached.</summary>
    public string Name => "repeated";

    public void Reset()
    {
    }

    public void Left()
    {
    }

    public void Right()
    {
    }

    public void Join()
    {
    }
}

/// <summary>
/// 19.6.5 — a base class whose public members happen to match an interface's, and which does
/// not itself implement that interface.
/// </summary>
public class IfaceMapBase
{
    /// <summary>19.6.5 — a member of a class that names no interface.</summary>
    public string Name => "from the base class";

    /// <summary>19.6.5 — and another.</summary>
    public void Reset()
    {
    }
}

/// <summary>
/// 19.6.5 hazard — a class that implements an interface by declaring nothing at all. Both
/// members of <see cref="IfaceBaseAlpha"/> map onto members inherited from
/// <see cref="IfaceMapBase"/>, so the mapping edges leave this class and land in a type that
/// knows nothing about the interface.
/// </summary>
public sealed class IfaceMapDerived : IfaceMapBase, IfaceBaseAlpha
{
}

/// <summary>19.6.6 — a base class that does implement the interface.</summary>
public class IfaceOrderBase : IfaceBaseBeta
{
    /// <summary>19.6.6 — the implementation the derived classes below inherit.</summary>
    public int Order => 7;
}

/// <summary>
/// 19.6.6 — a derived class that declares nothing. It implements <see cref="IfaceBaseBeta"/>
/// because its base class does, and it names neither the interface nor the member.
/// </summary>
public sealed class IfaceOrderInherits : IfaceOrderBase
{
}

/// <summary>
/// 19.6.6 hazard — a derived class hiding the implementing member with <c>new</c> and not naming
/// the interface. The mapping is not recomputed, so <c>((IfaceBaseBeta)x).Order</c> is the base
/// class's 7 while <c>x.Order</c> is 9: two members of one name in one hierarchy, and the
/// interface's mapping edge points at the one that is not found by the simple name.
/// </summary>
public class IfaceOrderHides : IfaceOrderBase
{
    /// <summary>19.6.6 — hides the base member and implements nothing.</summary>
    public new int Order => 9;
}

/// <summary>
/// 19.6.7 hazard — interface re-implementation. This class names <see cref="IfaceBaseBeta"/> in
/// its own base list even though its base class already implements it, so the mapping is
/// recomputed and this member wins: <c>((IfaceBaseBeta)x).Order</c> is 11. The difference from
/// <see cref="IfaceOrderHides"/> is the base list and nothing else.
/// </summary>
public sealed class IfaceOrderReimplements : IfaceOrderBase, IfaceBaseBeta
{
    /// <summary>19.6.7 — the re-implementing member, which also hides the inherited one.</summary>
    public new int Order => 11;
}

/// <summary>19.6.6 — a base class implementing the interface with a virtual member.</summary>
public class IfaceVirtualOrder : IfaceBaseBeta
{
    /// <summary>19.6.6 — virtual, so an override travels with the mapping.</summary>
    public virtual int Order => 1;
}

/// <summary>
/// 19.6.6 hazard — an override of the implementing member. The mapping still names the base
/// class's declaration and the override is what runs, so the member a reference resolves to and
/// the member that executes are two different declarations.
/// </summary>
public sealed class IfaceOverrideOrder : IfaceVirtualOrder
{
    /// <summary>19.6.6 — the override the interface mapping reaches at run time.</summary>
    public override int Order => 2;
}

/// <summary>19.6.7 — a base class implementing an interface member explicitly.</summary>
public class IfaceExplicitBaseImpl : IfaceAlphaNamed
{
    /// <summary>19.6.7 — an explicit implementation in the base class.</summary>
    string IfaceAlphaNamed.Name() => "explicit base";
}

/// <summary>
/// 19.6.7 hazard — re-implementation by a second explicit implementation. Two members named
/// <c>IfaceAlphaNamed.Name</c> now exist in one hierarchy, in two classes, and the qualified
/// names are spelled identically: only the containing type separates them.
/// </summary>
public sealed class IfaceExplicitReimpl : IfaceExplicitBaseImpl, IfaceAlphaNamed
{
    /// <summary>19.6.7 — the re-implementation, which the mapping now points at.</summary>
    string IfaceAlphaNamed.Name() => "explicit derived";
}

/// <summary>
/// 19.6.8 hazard — an abstract class implementing an interface with abstract members. The
/// mapping is complete here and no member has a body: the interface member maps onto a
/// declaration that is itself an obligation.
/// </summary>
public abstract class IfaceAbstractHost : IfaceBaseAlpha
{
    /// <summary>19.6.8 — an abstract property implementing an interface property.</summary>
    public abstract string Name { get; }

    /// <summary>19.6.8 — an abstract method implementing an interface method.</summary>
    public abstract void Reset();
}

/// <summary>
/// 19.6.8 — the concrete class. Its overrides are what execute through the interface, and they
/// implement nothing directly: the mapping was fixed by the abstract class above.
/// </summary>
public sealed class IfaceAbstractConcrete : IfaceAbstractHost
{
    /// <summary>19.6.8 — the override the interface reaches.</summary>
    public override string Name => "concrete";

    public override void Reset()
    {
    }
}

/// <summary>
/// 19.6.8 hazard — an abstract class whose explicit implementation forwards to an abstract
/// member of its own. The interface member's mapping lands on a member with a body, whose body
/// calls a member with none.
/// </summary>
public abstract class IfaceAbstractForwarder : IfaceBaseBeta
{
    /// <summary>19.6.8 — the explicit implementation, which has a body.</summary>
    int IfaceBaseBeta.Order => Rank;

    /// <summary>19.6.8 — the abstract member it forwards to, which is not an interface member
    /// and is not public.</summary>
    protected abstract int Rank { get; }
}

/// <summary>19.6.8 — the concrete forwarder.</summary>
public sealed class IfaceAbstractForwarded : IfaceAbstractForwarder
{
    /// <summary>19.6.8 — supplies the value the explicit implementation returns.</summary>
    protected override int Rank => 13;
}

/// <summary>
/// 19.6.3 to 19.6.8 — the mappings above observed. Every claim in this file's comments about
/// which member runs is a call here.
/// </summary>
public static class IfaceMappingUse
{
    /// <summary>19.6.3 — the two constructions of one interface, each through its own type.</summary>
    public static bool BothAccepts(IfaceAcceptsBoth both) =>
        ((IfaceAccepts<int>)both).Accepts(1) && ((IfaceAccepts<string>)both).Accepts("x");

    /// <summary>19.6.5 — the interface satisfied entirely by inherited class members.</summary>
    public static string FromBaseClass(IfaceMapDerived derived)
    {
        IfaceBaseAlpha alpha = derived;
        alpha.Reset();
        return alpha.Name;
    }

    /// <summary>19.6.6 — an implementation inherited with the interface.</summary>
    public static int Inherited(IfaceOrderInherits inherits) => ((IfaceBaseBeta)inherits).Order;

    /// <summary>19.6.6 — the hiding case: 7 through the interface, 9 through the class.</summary>
    public static string Hidden(IfaceOrderHides hides) =>
        $"{((IfaceBaseBeta)hides).Order}:{hides.Order}";

    /// <summary>19.6.7 — the re-implementing case: 11 through the interface and through the
    /// class alike.</summary>
    public static string Reimplemented(IfaceOrderReimplements reimplements) =>
        $"{((IfaceBaseBeta)reimplements).Order}:{reimplements.Order}";

    /// <summary>19.6.6 — the override reached through the interface.</summary>
    public static int Overridden(IfaceOverrideOrder overridden) =>
        ((IfaceBaseBeta)overridden).Order;

    /// <summary>19.6.7 — the two explicit implementations, one hierarchy, one interface.</summary>
    public static string ExplicitReimplemented() =>
        ((IfaceAlphaNamed)new IfaceExplicitBaseImpl()).Name()
        + ((IfaceAlphaNamed)new IfaceExplicitReimpl()).Name();

    /// <summary>19.6.8 — the abstract mapping, reached on a concrete instance.</summary>
    public static string Abstract()
    {
        IfaceBaseAlpha alpha = new IfaceAbstractConcrete();
        return alpha.Name;
    }

    /// <summary>19.6.8 — the forwarding explicit implementation.</summary>
    public static int Forwarded() => ((IfaceBaseBeta)new IfaceAbstractForwarded()).Order;

    /// <summary>19.6.3 — the repeated base interface, reached through both paths.</summary>
    public static string Repeated(IfaceRepeatedBase repeated) =>
        ((IfaceBaseAlpha)repeated).Name + ((IfaceDiamondLeft)repeated).Name;

    /// <summary>19.6.3 — the generic declaration's two constructions, one open and one closed,
    /// each through its own interface.</summary>
    public static bool UniqueAccepts(IfaceUniqueAccepts<string> unique) =>
        ((IfaceAccepts<string[]>)unique).Accepts(["x"]) && ((IfaceAccepts<int>)unique).Accepts(1);
}
