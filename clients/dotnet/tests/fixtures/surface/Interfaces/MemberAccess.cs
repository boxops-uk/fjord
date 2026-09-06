// Clause 19.4.11 — interface member access. A member of an interface is reached through an
// expression whose type is that interface, or through a type parameter constrained to it, and
// for an explicitly implemented member that is the only way to reach it at all.

using System;

namespace Surface.Interfaces;

/// <summary>
/// 19.4.11 hazard — the access forms, one per member. Every reference here binds to a member
/// declared in an interface even where the object it runs on is an instance of a class, which is
/// the distinction between the member a reference names and the member that executes.
/// </summary>
public static class IfaceAccess
{
    /// <summary>19.4.11 — a member accessed through an interface-typed parameter.</summary>
    public static int CountOf(IfaceEveryMember source) => source.Count;

    /// <summary>19.4.11 — a member of a base interface accessed through a derived interface
    /// type, which is where the inherited member is a member of the derived interface.</summary>
    public static string NameThroughDerived(IfaceDerivedBoth both) => both.Name;

    /// <summary>19.4.11 — a member reached through a cast, which is what a hidden name needs.</summary>
    public static string HiddenName(IfaceHidesBaseMember hidden) => ((IfaceBaseAlpha)hidden).Name;

    /// <summary>19.4.11 — a member reached through a type parameter constrained to the
    /// interface, so the qualifier is a type parameter and not a type.</summary>
    public static string LabelOf<T>(T value)
        where T : IfaceContract
        => value.Label;

    /// <summary>19.4.11 hazard — the same declaration reached twice: once through the interface
    /// and once through the class that implements it. Two references, two static types, one
    /// executing member — and only one of the two names the interface member.</summary>
    public static string BothWays(IfaceEveryMemberBox box)
    {
        IfaceEveryMember through = box;
        return $"{through.Tag}{box.Tag}";
    }

    /// <summary>19.4.11 — a static member of an interface, whose qualifier is the interface
    /// name and not an instance.</summary>
    public static int StaticLimit() => IfaceEveryMember.Limit;

    /// <summary>19.4.11 — a default-implemented member reached through the interface on a class
    /// that declares no such member.</summary>
    public static int DefaultedDoubled(IfaceDefaultsBox box)
    {
        IfaceDefaults defaults = box;
        return defaults.Doubled();
    }

    /// <summary>19.4.11 — an interface member invoked on a boxed struct, where the conversion
    /// to the interface type is the boxing.</summary>
    public static int WeighBoxed(IfaceContractStruct value)
    {
        IfaceContract contract = value;
        return contract.Weigh(3);
    }

    /// <summary>19.4.11 — an interface event, subscribed through the interface.</summary>
    public static void SubscribeThrough(IfaceEventfulBox box, Action handler)
    {
        IfaceEventful eventful = box;
        eventful.Fired += handler;
        eventful.Fired -= handler;
    }

    /// <summary>19.4.11 — an interface indexer, through the interface.</summary>
    public static string ElementThrough(IfaceEveryMemberBox box)
    {
        IfaceEveryMember source = box;
        return source[1];
    }

    /// <summary>19.4.11 — a member of a constructed interface, where the reference's binding
    /// carries the substitution the declaration does not.</summary>
    public static int SumOfIntSource(IfaceIntSource source) => source.Take() + source.Sum();

    /// <summary>19.4.11 — a member reached through <c>object</c> and a pattern, so the
    /// interface type appears in a pattern rather than in a declaration.</summary>
    public static int WeighIfContract(object value) =>
        value is IfaceContract contract ? contract.Weigh(1) : 0;

    /// <summary>19.4.11 — the base access the clause qualifies: <c>base.Order</c> is valid
    /// because it binds to a class implementation of the interface member and not to the
    /// interface member itself.</summary>
    public static int OrderFromBase() => new IfaceAccessBaseCall().OrderFromBase();

    /// <summary>19.4.11 — a class whose base class implements the interface member, so a base
    /// access reaches an implementation rather than a declaration.</summary>
    private sealed class IfaceAccessBaseCall : IfaceOrderBase
    {
        /// <summary>19.4.11 — <c>base.Order</c>, which is the only form of base access the
        /// clause permits for an interface member.</summary>
        public int OrderFromBase() => base.Order;
    }
}
