// Clause 19.6 — interface implementations. 19.6.1 says a class or struct implements the
// interfaces of its base list and their base interfaces; 19.6.2 gives explicit interface member
// implementations, whose name is a qualified name; 19.6.4 says how a generic interface method is
// implemented. The remaining subclauses — uniqueness, mapping, inheritance, re-implementation and
// abstract classes — are in `Mapping.cs`.
//
// One indexer per type is observed throughout: a class that implements an interface indexer
// explicitly declares no other `this[…]`.

using System;
using System.Collections.Generic;

namespace Surface.Interfaces;

/// <summary>19.6.1 — a base class for the base list below to begin with.</summary>
public class IfaceImplBase
{
    /// <summary>19.6.1 — a member of the base class, which is not an interface member.</summary>
    public virtual int Rank => 1;
}

/// <summary>
/// 19.6.1 hazard — a base list with a base class and two interfaces. This class implements
/// three members: two implicitly and one explicitly, and the explicit one is invisible on the
/// class's own type.
/// </summary>
public sealed class IfaceImplLeaf : IfaceImplBase, IfaceBaseAlpha, IfaceBaseBeta
{
    /// <summary>19.6.1 — an implicit implementation: a public member with a matching signature.</summary>
    public string Name => "leaf";

    /// <summary>19.6.1 — another.</summary>
    public void Reset()
    {
    }

    /// <summary>19.6.2 — and an explicit one, in the same class, for the second interface.</summary>
    int IfaceBaseBeta.Order => 2;

    /// <summary>19.6.1 — an override of the base class member, which no interface names.</summary>
    public override int Rank => 3;
}

/// <summary>
/// 19.6.1 — a struct's base list, which may hold interfaces and no base class. A struct's
/// implementations are not virtual, and reaching one through the interface boxes the value.
/// </summary>
public struct IfaceImplStruct : IfaceBaseBeta, IfaceAlphaNamed
{
    /// <summary>19.6.1 — an implicit implementation in a struct.</summary>
    public int Order => 4;

    /// <summary>19.6.2 — an explicit implementation in a struct, which is reachable only from
    /// a boxed copy.</summary>
    string IfaceAlphaNamed.Name() => "struct";
}

/// <summary>
/// 19.6.1 hazard — a generic class implementing a constructed interface built from its own type
/// parameter. Every construction of this class implements a different constructed interface, and
/// there is one base-type edge in the source.
/// </summary>
/// <typeparam name="TItem">19.6.1 — passed to the interface in the base list.</typeparam>
public sealed class IfaceImplRelay<TItem> : IfaceRelay<TItem>
{
    private TItem _held;

    /// <summary>19.6.1 — implements the inherited covariant member.</summary>
    public TItem Current => _held;

    /// <summary>19.6.1 — implements the inherited member of the other base interface.</summary>
    public void Give(TItem item) => _held = item;

    public bool Accepts(TItem item) => item is not null;

    public TItem Take() => _held;

    /// <summary>19.6.1 — implements the member the relay itself declares.</summary>
    public TItem Relay(TItem item)
    {
        Give(item);
        return Take();
    }
}

/// <summary>
/// 19.6.2 — an interface with one member of every kind that can be implemented explicitly,
/// including a generic method for 19.6.4.
/// </summary>
public interface IfaceExplicit
{
    /// <summary>19.6.2 — a property to implement explicitly.</summary>
    string Label { get; }

    /// <summary>19.6.2 — a property with both accessors.</summary>
    int Weight { get; set; }

    /// <summary>19.6.2 — an event.</summary>
    event Action Fired;

    /// <summary>19.6.2 — an indexer, whose explicit implementation's name is <c>this</c>.</summary>
    string this[int index] { get; }

    /// <summary>19.6.2 — a method.</summary>
    void Ping();

    /// <summary>19.6.4 — a generic method with a constraint, implemented explicitly below.</summary>
    string Render<T>(T value)
        where T : IfaceContract;
}

/// <summary>
/// 19.6.2 hazard — every member of <see cref="IfaceExplicit"/> implemented explicitly. None of
/// these members is accessible on this class's own type: each is named by a qualified name and
/// reached only through the interface, so a reference to <c>Ping</c> on an
/// <c>IfaceExplicitBox</c> does not compile while a reference through the interface does.
/// </summary>
public sealed class IfaceExplicitBox : IfaceExplicit
{
    private int _weight;

    private Action _fired;

    /// <summary>19.6.2 — an explicitly implemented get-only property.</summary>
    string IfaceExplicit.Label => "explicit";

    /// <summary>19.6.2 — an explicitly implemented property with both accessors, so both
    /// accessors' names are qualified too.</summary>
    int IfaceExplicit.Weight
    {
        get => _weight;
        set => _weight = value;
    }

    /// <summary>19.6.2 — an explicitly implemented event, which must have both accessors.</summary>
    event Action IfaceExplicit.Fired
    {
        add => _fired += value;
        remove => _fired -= value;
    }

    /// <summary>19.6.2 — an explicitly implemented indexer, and the only indexer this type
    /// declares.</summary>
    string IfaceExplicit.this[int index] => index.ToString();

    /// <summary>19.6.2 — an explicitly implemented method.</summary>
    void IfaceExplicit.Ping() => _fired?.Invoke();

    /// <summary>19.6.4 — an explicitly implemented generic method. Its constraints are
    /// inherited from the interface declaration and may not be restated here, so the
    /// constraint is part of this member and is written nowhere in it.</summary>
    string IfaceExplicit.Render<T>(T value) => value.Label;
}

/// <summary>19.6.2 hazard — a member name declared in one interface…</summary>
public interface IfaceAlphaNamed
{
    /// <summary>19.6.2 — spelled <c>Name</c>, taking no arguments.</summary>
    string Name();
}

/// <summary>19.6.2 hazard — …and the same name and signature declared in another.</summary>
public interface IfaceBetaNamed
{
    /// <summary>19.6.2 — spelled <c>Name</c> too, with the same signature.</summary>
    string Name();
}

/// <summary>
/// 19.6.2 hazard — one class implementing both, with two explicit implementations. The two
/// members have identical signatures and identical simple names, and only the interface in the
/// qualified name separates them: <c>IfaceAlphaNamed.Name</c> and <c>IfaceBetaNamed.Name</c>.
/// </summary>
public sealed class IfaceTwoNames : IfaceAlphaNamed, IfaceBetaNamed
{
    /// <summary>19.6.2 — the first of the pair.</summary>
    string IfaceAlphaNamed.Name() => "alpha";

    /// <summary>19.6.2 — the second, whose only difference is its qualifier.</summary>
    string IfaceBetaNamed.Name() => "beta";
}

/// <summary>
/// 19.6.5 hazard — the opposite arrangement: one class member implementing the same-named
/// member of both interfaces. Two interface members map onto one declaration, so the mapping is
/// two edges into one member rather than two members.
/// </summary>
public sealed class IfaceOneName : IfaceAlphaNamed, IfaceBetaNamed
{
    /// <summary>19.6.5 — the single member both interfaces map onto.</summary>
    public string Name() => "both";
}

/// <summary>
/// 19.6.2 — a generic interface whose members are implemented explicitly under a substitution.
/// It declares one indexer, so no implementation of it may declare a second.
/// </summary>
/// <typeparam name="TKey">19.6.2 — the key type, substituted at every implementation.</typeparam>
/// <typeparam name="TValue">19.6.2 — the value type.</typeparam>
public interface IfaceMap<TKey, TValue>
{
    /// <summary>19.6.2 — a method whose signature mentions both type parameters.</summary>
    TValue Get(TKey key);

    /// <summary>19.6.2 — another.</summary>
    void Put(TKey key, TValue value);

    /// <summary>19.6.2 — the one indexer.</summary>
    TValue this[TKey key] { get; }

    /// <summary>19.6.2 — a member whose signature mentions neither.</summary>
    int Count { get; }
}

/// <summary>
/// 19.6.2 hazard — explicit implementation of the members of a constructed generic interface.
/// The name of each member below is qualified by <c>IfaceMap&lt;string, int&gt;</c>, so its
/// identity has to carry the substitution: <c>Get</c> here takes a <c>string</c> and returns an
/// <c>int</c>, and the declaration it implements takes a <c>TKey</c> and returns a
/// <c>TValue</c>.
/// </summary>
public sealed class IfaceStringIntMap : IfaceMap<string, int>
{
    private readonly Dictionary<string, int> _items = [];

    /// <summary>19.6.2 — the explicit implementation of <c>IfaceMap&lt;string, int&gt;.Get</c>.</summary>
    int IfaceMap<string, int>.Get(string key) => _items.TryGetValue(key, out var value) ? value : 0;

    /// <summary>19.6.2 — and of <c>Put</c>.</summary>
    void IfaceMap<string, int>.Put(string key, int value) => _items[key] = value;

    /// <summary>19.6.2 — and of the indexer, which is this type's only one.</summary>
    int IfaceMap<string, int>.this[string key] => _items[key];

    /// <summary>19.6.5 — implemented implicitly instead, so one class mixes both mappings.</summary>
    public int Count => _items.Count;
}

/// <summary>19.6.4 — an interface whose methods are generic, with constraints.</summary>
public interface IfaceGenericMethods
{
    /// <summary>19.6.4 — a generic method with an interface constraint.</summary>
    T Pick<T>(T first, T second)
        where T : IComparable<T>;

    /// <summary>19.6.4 — a generic method with a value type constraint.</summary>
    void Consume<T>(T item)
        where T : struct;

    /// <summary>19.6.4 — a generic method with two type parameters, one constrained to the
    /// other, so the constraint names a type parameter of the same method.</summary>
    TOut Convert<TIn, TOut>(TIn input)
        where TIn : TOut;
}

/// <summary>
/// 19.6.4 hazard — the same three methods implemented two ways in one class: implicitly, where
/// the constraints must be restated identically, and explicitly, where they must not be restated
/// at all. The type parameter names differ from the interface's on purpose, because a type
/// parameter's identity is its position and not its name.
/// </summary>
public sealed class IfaceGenericImpl : IfaceGenericMethods
{
    /// <summary>19.6.4 — an implicit implementation restating the constraint, with the type
    /// parameter renamed.</summary>
    public TValue Pick<TValue>(TValue first, TValue second)
        where TValue : IComparable<TValue>
        => first.CompareTo(second) >= 0 ? first : second;

    /// <summary>19.6.4 — an explicit implementation, whose constraint is inherited.</summary>
    void IfaceGenericMethods.Consume<TValue>(TValue item)
    {
    }

    /// <summary>19.6.4 — an explicit implementation of the two-parameter method, whose
    /// inter-parameter constraint is likewise inherited and unwritten.</summary>
    TTo IfaceGenericMethods.Convert<TFrom, TTo>(TFrom input) => input;
}

/// <summary>19.6 — the implementations above used, each through the interface it implements.</summary>
public static class IfaceImplementationUse
{
    /// <summary>19.6.1 — the three members of a class with a base class and two interfaces,
    /// two of them reached through the interfaces.</summary>
    public static string Leaf(IfaceImplLeaf leaf) =>
        $"{leaf.Name}{((IfaceBaseBeta)leaf).Order}{leaf.Rank}";

    /// <summary>19.6.1 — a struct's explicit implementation, reached through the interface,
    /// which boxes.</summary>
    public static string Struct(IfaceImplStruct value) => ((IfaceAlphaNamed)value).Name();

    /// <summary>19.6.2 — every explicitly implemented member, through the interface.</summary>
    public static string Explicit(IfaceExplicitBox box)
    {
        IfaceExplicit explicitly = box;
        explicitly.Weight = 2;
        explicitly.Ping();
        return $"{explicitly.Label}{explicitly.Weight}{explicitly[3]}"
            + explicitly.Render(new IfaceContractClass());
    }

    /// <summary>19.6.2 — the two same-named members, each through its own interface.</summary>
    public static string TwoNames(IfaceTwoNames names) =>
        ((IfaceAlphaNamed)names).Name() + ((IfaceBetaNamed)names).Name();

    /// <summary>19.6.5 — the one member both interfaces map onto, through both.</summary>
    public static string OneName(IfaceOneName name) =>
        ((IfaceAlphaNamed)name).Name() + ((IfaceBetaNamed)name).Name() + name.Name();

    /// <summary>19.6.2 — the constructed interface's members, through the constructed
    /// interface.</summary>
    public static int Map(IfaceStringIntMap map)
    {
        IfaceMap<string, int> constructed = map;
        constructed.Put("a", 1);
        return constructed.Get("a") + constructed["a"] + constructed.Count;
    }

    /// <summary>19.6.4 — the generic methods, one bound implicitly and two through the
    /// interface.</summary>
    public static string Generics(IfaceGenericImpl impl)
    {
        IfaceGenericMethods methods = impl;
        methods.Consume(1);
        object converted = methods.Convert<string, object>("x");
        return $"{impl.Pick(1, 2)}{methods.Pick("a", "b")}{converted}";
    }

    /// <summary>19.6.1 — a construction of the generic implementation.</summary>
    public static int Relay() => new IfaceImplRelay<int>().Relay(5);
}
