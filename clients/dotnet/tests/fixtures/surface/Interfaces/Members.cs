// Clause 19.4 — interface members. 19.4.1 lists the kinds, and this file holds one of each that
// carries data or behaviour: 19.4.2 fields, 19.4.3 methods, 19.4.4 properties, 19.4.5 events and
// 19.4.6 indexers. Operators (19.4.7), static constructors (19.4.8) and nested types (19.4.9)
// have files of their own, because each brings a different kind of hazard with it.
//
// One member per name per interface, and one indexer per type: `IfaceEveryMember` declares
// exactly one `this[…]` and so does every class in this file.

using System;

namespace Surface.Interfaces;

/// <summary>
/// 19.4.1 hazard — every member kind an interface may declare, in one declaration. Six of the
/// seven kinds are here; the seventh, a nested type, is in <c>NestedTypes.cs</c>. Each member's
/// identity has to be distinct from the others' even though they share a container, and the
/// static ones have to be distinct from the instance ones even though nothing in the syntax
/// separates them but a keyword.
/// </summary>
public interface IfaceEveryMember
{
    /// <summary>19.4.2 — a constant field. Implicitly static, and the only kind of field an
    /// interface may declare that carries a value of its own.</summary>
    const int Limit = 8;

    /// <summary>19.4.2 — a static field, which an interface may declare because it belongs to
    /// the interface and not to any implementation of it.</summary>
    static int Created = 0;

    /// <summary>19.4.2 — a static readonly field, initialised in place.</summary>
    static readonly string Origin = "19.4.2";

    /// <summary>19.4.2 — a private static field, so an interface field's accessibility is not
    /// always the implicit public.</summary>
    private static int _touches = 0;

    /// <summary>19.4.4 — a property, get-only, with no implementation.</summary>
    int Count { get; }

    /// <summary>19.4.4 — a property with both accessors.</summary>
    string Tag { get; set; }

    /// <summary>19.4.6 — the one indexer this interface declares.</summary>
    string this[int index] { get; }

    /// <summary>19.4.5 — an event, declared with neither accessor.</summary>
    event EventHandler Changed;

    /// <summary>19.4.3 — a method with no implementation: implicitly public and abstract.</summary>
    void Touch();

    /// <summary>19.4.3 — a static method with a body, which an interface may declare.</summary>
    static void Note() => Created++;

    /// <summary>19.4.3 — a private static method, callable only from members of this
    /// interface.</summary>
    private static void Record() => _touches++;

    /// <summary>19.4.3 — a default implementation that calls the private static method, which
    /// is the only way <c>_touches</c> can be reached.</summary>
    void TouchAndRecord()
    {
        Touch();
        Record();
    }

    /// <summary>19.4.2 — the private field read back, so a query has a reference to it.</summary>
    static int Touches => _touches;
}

/// <summary>
/// 19.4.1 — a class implementing every declared member of <see cref="IfaceEveryMember"/>. The
/// default-implemented <c>TouchAndRecord</c> is not declared here: it is inherited from the
/// interface, which is the point of 19.4.3's default implementations.
/// </summary>
public sealed class IfaceEveryMemberBox : IfaceEveryMember
{
    /// <summary>19.4.4 — the class member the interface property maps onto.</summary>
    public int Count { get; private set; }

    /// <summary>19.4.4 — a property with both accessors, mapping onto both.</summary>
    public string Tag { get; set; } = "box";

    /// <summary>19.4.6 — the type's only indexer, mapping onto the interface's.</summary>
    public string this[int index] => $"{Tag}[{index}]";

    /// <summary>19.4.5 — a field-like event implementing the interface's event.</summary>
    public event EventHandler Changed;

    /// <summary>19.4.3 — the method implementation, which raises the event.</summary>
    public void Touch()
    {
        Count++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 19.4.3, 19.4.4, 19.4.5 and 19.4.6 — a default implementation of one member of each kind. A
/// default implementation is a body in an interface, and the member is still an interface
/// member: an implementing class that declares none of these inherits all four.
/// </summary>
public interface IfaceDefaults
{
    /// <summary>19.4.3 — the abstract member the four defaults are written in terms of.</summary>
    int Seed { get; }

    /// <summary>19.4.3 hazard — a method with a body in an interface. It has an implementation
    /// and no class declares it, so the implementation and the declaration are one member.</summary>
    int Doubled() => Seed * 2;

    /// <summary>19.4.4 — a property with a body in an interface.</summary>
    int Tripled => Seed * 3;

    /// <summary>19.4.6 — an indexer with a body, and the only indexer of this type.</summary>
    int this[int factor] => Seed * factor;

    /// <summary>19.4.5 — an event with accessor bodies, which must be backed by something
    /// static because an interface has no instance state to hold a handler in.</summary>
    event Action Ignored
    {
        add
        {
        }

        remove
        {
        }
    }
}

/// <summary>
/// 19.4.1 — a class that implements <see cref="IfaceDefaults"/> by declaring one member. The
/// other four members exist on the type and are declared nowhere in it.
/// </summary>
public sealed class IfaceDefaultsBox : IfaceDefaults
{
    /// <summary>19.4.4 — the only member this class declares.</summary>
    public int Seed => 5;
}

/// <summary>
/// 19.4.5 hazard — events in every form an interface may declare them, and a static field of
/// delegate type for the ones with bodies to keep their handlers in.
/// </summary>
public interface IfaceEventful
{
    /// <summary>19.4.5 — a field-like event; the implementer supplies the storage.</summary>
    event Action Fired;

    /// <summary>19.4.5 — a second field-like event, of a constructed delegate type.</summary>
    event Action<int> Counted;

    /// <summary>19.4.5 — the static store the broadcast event's accessors use.</summary>
    static Action Broadcasters;

    /// <summary>
    /// 19.4.5 — an event whose accessors have bodies, in an interface. The add and remove
    /// accessors are members of the event and not members of the interface, which is the
    /// distinction an index either records or loses.
    /// </summary>
    static event Action Broadcast
    {
        add => Broadcasters += value;
        remove => Broadcasters -= value;
    }

    /// <summary>19.4.5 — the static event raised, so both accessors are reachable.</summary>
    static void RaiseBroadcast() => Broadcasters?.Invoke();
}

/// <summary>19.4.5 — a class implementing the two field-like events.</summary>
public sealed class IfaceEventfulBox : IfaceEventful
{
    /// <summary>19.4.5 — a field-like event implementing a field-like event.</summary>
    public event Action Fired;

    /// <summary>19.4.5 — an event with explicit accessors implementing a field-like event, so
    /// the two forms meet across the mapping.</summary>
    public event Action<int> Counted
    {
        add => _counted += value;
        remove => _counted -= value;
    }

    private Action<int> _counted;

    /// <summary>19.4.5 — both events raised, so both mappings are exercised.</summary>
    public void Raise(int count)
    {
        Fired?.Invoke();
        _counted?.Invoke(count);
    }
}

/// <summary>
/// 19.4.4 — an interface declaring an abstract property, so a derived interface can implement
/// it explicitly, which 19.4.4 names as a case of its own.
/// </summary>
public interface IfaceMeasured
{
    /// <summary>19.4.4 — abstract, and implemented in an interface rather than in a class.</summary>
    int Measure { get; }
}

/// <summary>
/// 19.4.4 hazard — a derived interface explicitly implementing a base interface's abstract
/// property. The member's name is the qualified name <c>IfaceMeasured.Measure</c>, its container
/// is an interface, and it is at once an interface member and an implementation of one.
/// </summary>
public interface IfaceFixedMeasure : IfaceMeasured
{
    /// <summary>19.4.4 — the explicit implementation, in an interface.</summary>
    int IfaceMeasured.Measure => 12;
}

/// <summary>19.4.4 — a class implementing the derived interface and declaring nothing: the
/// property it answers with is declared in an interface and implemented in another.</summary>
public sealed class IfaceFixedMeasureBox : IfaceFixedMeasure
{
}

/// <summary>19.4.1 hazard — a member name declared in one base interface…</summary>
public interface IfaceCountAlpha
{
    /// <summary>19.4.1 — spelled <c>Count</c>, returning <c>int</c>.</summary>
    int Count { get; }
}

/// <summary>19.4.1 hazard — …and the same name declared in another, with a different type.</summary>
public interface IfaceCountBeta
{
    /// <summary>19.4.1 — spelled <c>Count</c> too, returning <c>long</c>.</summary>
    long Count { get; }
}

/// <summary>
/// 19.4.1 hazard — an interface inheriting both. Two members named <c>Count</c> are members of
/// this interface, and neither hides the other: a reference to <c>Count</c> through this type is
/// ambiguous, so every use below states which base interface it means.
/// </summary>
public interface IfaceCountJoin : IfaceCountAlpha, IfaceCountBeta
{
    /// <summary>19.4.1 — a third member, whose name is its own.</summary>
    string Describe();
}

/// <summary>19.4.11 — the members of this file used, so each declaration has a reference.</summary>
public static class IfaceMemberUse
{
    /// <summary>19.4.2 — a constant field of an interface, read through the interface name.</summary>
    public static int Limit => IfaceEveryMember.Limit;

    /// <summary>19.4.2 — a static field of an interface, read and written.</summary>
    public static int Bump()
    {
        IfaceEveryMember.Note();
        return IfaceEveryMember.Created;
    }

    /// <summary>19.4.2 — a static readonly field of an interface.</summary>
    public static string Origin => IfaceEveryMember.Origin;

    /// <summary>19.4.6 — an interface indexer, through the interface.</summary>
    public static string Element(IfaceEveryMember source, int index) => source[index];

    /// <summary>19.4.5 — an event of an interface, subscribed and unsubscribed through it.</summary>
    public static void Subscribe(IfaceEveryMember source, EventHandler handler)
    {
        source.Changed += handler;
        source.Changed -= handler;
    }

    /// <summary>19.4.3 — a default-implemented method, called through the interface on a class
    /// that does not declare it.</summary>
    public static int DefaultedSum(IfaceDefaults defaults) =>
        defaults.Doubled() + defaults.Tripled + defaults[4];

    /// <summary>19.4.1 — the two ambiguous <c>Count</c> members, each reached by naming the
    /// interface that declares it.</summary>
    public static long BothCounts(IfaceCountJoin join) =>
        ((IfaceCountAlpha)join).Count + ((IfaceCountBeta)join).Count;

    /// <summary>19.4.4 — a property whose implementation is in an interface, reached through the
    /// base interface that declares it.</summary>
    public static int Measure(IfaceFixedMeasureBox box) => ((IfaceMeasured)box).Measure;
}
