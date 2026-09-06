// M10 — `csharp.TypeParameter` is keyed `{name, variance, hasNotNullConstraint,
// hasReferenceTypeConstraint, hasValueTypeConstraint}` and nothing else: no declaring type,
// no declaring method, no ordinal. Everything in this file is named for the assembly it is
// in, so that the ONLY names shared with `Assemblies.Left` are the ones shared on purpose:
// `Surface.Assemblies.Shared`'s two types (M17) and the type parameters below (M10).
namespace Surface.Assemblies.Right;

using System.Collections.Generic;
using Surface.Assemblies.Shared;

/// <summary>Which half of the pair answered. Named per side, so the two never merge.</summary>
public static class AsmRightBackend
{
    /// <summary>The stamp <see cref="AsmPairSignal.Raise"/> returns from this assembly.</summary>
    public const string Stamp = "right";
}

/// <summary>
/// A box with one unconstrained type parameter (8.5). Its <c>T</c>, the <c>T</c> of
/// <c>Assemblies.Left</c>'s <c>AsmLeftBox&lt;T&gt;</c>, the <c>T</c> of
/// <see cref="AsmPairSlot{T}"/> in both assemblies and the <c>T</c> of
/// <see cref="AsmRightUses.Convey"/> all carry the same five key fields, so an index keyed the
/// way <c>csharp.TypeParameter</c> is keyed holds one fact for all of them.
/// </summary>
public class AsmRightBox<T>
{
    private readonly T[] _items;

    /// <summary>Creates a box over a copy of <paramref name="items"/>.</summary>
    public AsmRightBox(params T[] items) => _items = (T[])items.Clone();

    /// <summary>How many items the box holds.</summary>
    public int Count => _items.Length;

    /// <summary>The item at <paramref name="index"/>. One indexer, deliberately — two would conflict.</summary>
    public T this[int index] => _items[index];
}

/// <summary>
/// A map whose key parameter is constrained (15.2.5). <c>TKey</c> sets
/// <c>hasNotNullConstraint</c> and <c>TValue</c> sets none, so these two are the contrast
/// that shows the key's five fields are the only thing separating type parameters: change
/// <c>where TKey : notnull</c> and <c>TKey</c> moves to a different entity while its symbol
/// string does not change at all.
/// </summary>
public class AsmRightMap<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _entries = new();

    /// <summary>Records <paramref name="value"/> under <paramref name="key"/>.</summary>
    public void Set(TKey key, TValue value) => _entries[key] = value;

    /// <summary>Whether anything is recorded under <paramref name="key"/>.</summary>
    public bool Has(TKey key) => _entries.ContainsKey(key);
}

/// <summary>
/// A covariant source (19.2.3 — variant type parameter lists). Its <c>T</c> has
/// <c>variance = Out</c>, which is a different key from every invariant <c>T</c> in the
/// corpus and the same key as <c>Assemblies.Left</c>'s.
/// </summary>
public interface IAsmRightSource<out T>
{
    /// <summary>The item currently on offer.</summary>
    T Current { get; }
}

/// <summary>
/// The use sites. M17's damage is here rather than in the declaration: each assembly uses
/// its own <see cref="AsmPairSignal"/>, and both uses resolve to one entity, so
/// find-references on either declaration answers with both.
/// </summary>
public static class AsmRightUses
{
    /// <summary>Raises this assembly's signal twice and reports the count it kept.</summary>
    public static int RaiseTwice()
    {
        AsmPairSignal signal = new();
        signal.Raise();
        signal.Raise();
        return signal.Count;
    }

    /// <summary>Answers with the stamp of the assembly whose copy of the paired type this is.</summary>
    public static string Stamped() => new AsmPairSignal().Raise();

    /// <summary>Fills and empties a slot of the paired generic type.</summary>
    public static int Slotted()
    {
        AsmPairSlot<int> slot = new();
        slot.Put(7);
        return slot.Take();
    }

    /// <summary>A method type parameter named <c>T</c>, unconstrained, in a static method.</summary>
    public static void Convey<T>(T item, AsmPairSlot<T> into) => into.Put(item);

    /// <summary>Boxes the stamp, so the per-side generic type has a use as well as a declaration.</summary>
    public static AsmRightBox<string> Boxed() => new(AsmRightBackend.Stamp, Stamped());
}
