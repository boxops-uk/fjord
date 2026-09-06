// Clause 15.2.4 — the class base specification. 15.2.4.1 gives the production (a base class,
// an interface list, or a base class followed by an interface list); 15.2.4.2 is the base
// class, including the one that is written nowhere; 15.2.4.3 is the interface list, whose
// entries are references and whose effect is a declaration of what the class implements.
//
// The interfaces are markers on purpose: an interface's own members are clause 18's rows, and
// what clause 15.2.4 is about is the *list*.

namespace Surface.Classes;

/// <summary>15.2.4.3 — an interface to appear in an interface list.</summary>
public interface IClsTagged
{
}

/// <summary>15.2.4.3 — a second one, so a list can have two entries.</summary>
public interface IClsNamed
{
}

/// <summary>15.2.4.3 — a generic interface, so one declaration can be implemented at two
/// different type arguments.</summary>
/// <typeparam name="TItem">The item accepted.</typeparam>
public interface IClsSink<TItem>
{
}

/// <summary>15.2.4.3 — an interface that inherits both markers, so a class can acquire an
/// interface it never names.</summary>
public interface IClsTaggedAndNamed : IClsTagged, IClsNamed
{
}

/// <summary>15.2.4.2 — the base class of the chain. Its own base is `object`, written
/// nowhere.</summary>
public class ClsBase
{
    /// <summary>15.3.4 — inherited by everything below.</summary>
    public int BaseSlot;
}

/// <summary>15.2.4.2 — a base class that is itself derived, so the chain is three deep.</summary>
public class ClsMiddle : ClsBase
{
    public int MiddleSlot;
}

/// <summary>15.2.4.2 — the leaf of the chain. Its inherited members come from two levels
/// (15.3.4) and its direct base is one of them.</summary>
public sealed class ClsLeaf : ClsMiddle
{
    public int LeafSlot;
}

/// <summary>
/// 15.2.4.2 hazard — `object` written out. This declaration and <see cref="ClsPlain"/> have
/// the same base class and differ in whether it appears in the source: the base of the one
/// is a reference a walk can see, and the base of the other is a reference no token carries.
/// Both have to answer the same question about their base type.
/// </summary>
public sealed class ClsExplicitObjectBase : object
{
    public int Slot;
}

/// <summary>15.2.4.2 — a generic base class, so a base can be a constructed type.</summary>
/// <typeparam name="TPayload">What the base carries.</typeparam>
public class ClsGenericBase<TPayload>
{
    public TPayload? Payload;
}

/// <summary>15.2.4.2 — a base that is a *closed* constructed type, with an interface list
/// after it. The base's `Payload` field is inherited as an `int` field here.</summary>
public sealed class ClsIntSink : ClsGenericBase<int>, IClsSink<int>
{
    public int Received;
}

/// <summary>15.2.4.2 — a base that is an *open* constructed type: the derived declaration
/// passes its own type parameter through.</summary>
/// <typeparam name="TPayload">Forwarded to the base.</typeparam>
public class ClsDerivedOpen<TPayload> : ClsGenericBase<TPayload>
{
    public int Revision;
}

/// <summary>15.2.4.1 — a base class followed by an interface list of two, which is the full
/// production.</summary>
public sealed class ClsDressed : ClsBase, IClsTagged, IClsNamed
{
    public int Slot;
}

/// <summary>15.2.4.3 — an interface list with no base class, which is the other branch of
/// the production.</summary>
public sealed class ClsInterfacesOnly : IClsTagged, IClsNamed
{
    public int Slot;
}

/// <summary>
/// 15.2.4.3 hazard — one interface declaration named twice in one list, at two different type
/// arguments. `IClsSink&lt;int&gt;` and `IClsSink&lt;string&gt;` are two implemented
/// interfaces and one interface declaration: an index keyed on the declaration records one
/// edge where there are two, and an index keyed on the constructed type records two rows that
/// must both resolve back to the same declaration.
/// </summary>
public sealed class ClsTwoSinks : IClsSink<int>, IClsSink<string>
{
    public int Ints;

    public int Strings;
}

/// <summary>15.2.4.3 — implements the marker, so a derived class inherits it.</summary>
public class ClsTaggedBase : IClsTagged
{
    public int Slot;
}

/// <summary>
/// 15.2.4.3 hazard — an interface implemented twice over: once through the base class and
/// once written here. The class's interface *set* has one entry and the source has two
/// mentions of it, one of which adds nothing.
/// </summary>
public sealed class ClsRedundantTag : ClsTaggedBase, IClsTagged
{
    public int Extra;
}

/// <summary>
/// 15.2.4.3 hazard — an interface acquired without being named: `IClsTagged` and
/// `IClsNamed` are in this class's interface set because `IClsTaggedAndNamed` inherits them,
/// and neither appears in the source below.
/// </summary>
public sealed class ClsInheritedInterfaces : IClsTaggedAndNamed
{
    public int Slot;
}

/// <summary>15.2.4.2 hazard — the base class is this declaration's own constraint, so the
/// base type mentions the derived type. See <see cref="ClsSelfBound{T}"/>.</summary>
public sealed class ClsCurious : ClsSelfBound<ClsCurious>
{
    public int Depth;
}

/// <summary>15.2.4.2 — a base class that is a nested type, so the base reference is a
/// qualified name rather than an identifier.</summary>
public sealed class ClsFromNested : ClsOuterHost.Springboard
{
    public int NestedSlot;
}

/// <summary>15.2.3 — a class implementing a covariant interface, which is the only route a
/// variance annotation has into a class declaration.</summary>
public sealed class ClsProducerOfInt : IClsProducer<int>
{
    public int Produced;
}

/// <summary>15.2.3 — and the contravariant one, plus the two-annotation interface.</summary>
public sealed class ClsConsumerOfString : IClsConsumer<string>, IClsTransform<string, int>
{
    public int Consumed;
}

/// <summary>15.2.4.1 — a base class *and* a constraint clause, which the grammar orders:
/// the `class_base` comes first and the `where` clauses come after it.</summary>
/// <typeparam name="TPayload">Constrained after the base list.</typeparam>
public sealed class ClsBasedAndConstrained<TPayload> : ClsGenericBase<TPayload>, IClsSink<TPayload>
    where TPayload : class, IClsTagged, new()
{
    public TPayload? Latest;
}
