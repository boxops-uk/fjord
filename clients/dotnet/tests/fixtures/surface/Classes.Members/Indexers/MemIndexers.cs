// Clauses 15.9.1 (indexers — general) and 15.9.2 (indexer and property differences). An indexer
// is a property with a parameter list and no name: the source spells it `this[...]`, and the
// emitted members are `get_Item(...)` and `set_Item(...)` unless `IndexerName` says otherwise.
//
// **One indexer per type here, on purpose.** A type with two `this[...]` — differing by
// parameter count or by parameter type, it makes no difference — is one of the five shapes that
// kills the whole-corpus indexing run, so the overload set that clause 15.9.1 allows is spread
// across the types below instead, one member each. The two-indexer shape belongs to the
// quarantine project that owns it.
//
// The hazard on both rows is that an indexer's identity has no name in it. Every type below has
// a member called `Item` in metadata and no member called `Item` in source, so:
//
//   * `MemIndexerByInt.this[int]` and `MemIndexerByString.this[string]` are two rows that a
//     (type, name) key separates only because the *types* differ, not because anything about
//     the members does.
//   * `MemRenamedIndexer` (in `Members/MemReservedNames.cs`) has an indexer emitted as `Element`
//     *and* a method spelled `get_Item`, so the type's `Item`-shaped member is the method.
//
// What clause 15.9.2 says is different about an indexer, each checked against the compiler:
//
//   * It cannot be static — CS0106, "the modifier 'static' is not valid for this item".
//   * Its parameters cannot be `ref` or `out` — CS0631, "ref and out are not valid in this
//     context". They may be `params`, which `MemIndexerByParams` uses.
//   * Its default emitted name collides with a property called `Item` — CS0102, "the type
//     already contains a definition for 'Item'" — so an ordinary property may take that name
//     only in a type with no indexer.
//   * It *may* have an `init` accessor, which is not obvious and which `MemIndexerWithInit`
//     writes.

using System;
using System.Collections.Generic;

namespace Surface.Classes.Members.Indexers;

/// <summary>15.9.1: the plain indexer — one `int` parameter, a getter and a setter.</summary>
public class MemIndexerByInt
{
    private readonly int[] _slots = new int[4];

    /// <summary>15.9.1: `get` and `set` over an array. `value` in the setter is the implicit
    /// parameter and `index` is the declared one.</summary>
    public int this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value;
    }

    /// <summary>15.9.1: an element access reference, which is a call to each accessor in
    /// turn.</summary>
    public int UseAll()
    {
        this[0] = 1;
        this[1] += 2;
        return this[0] + this[1];
    }
}

/// <summary>15.9.1: an indexer keyed by a reference type, get-only and expression-bodied — the
/// shortest form an indexer has.</summary>
public class MemIndexerByString
{
    private readonly Dictionary<string, int> _weights = new() { ["a"] = 1 };

    /// <summary>15.9.1: one arrow, no accessor keywords, one parameter.</summary>
    public int this[string key] => _weights.TryGetValue(key, out var weight) ? weight : 0;

    /// <summary>15.9.1: read through the only accessor there is.</summary>
    public int UseAll() => this["a"] + this["b"];
}

/// <summary>15.9.1: two parameters, so the emitted `get_Item` has an arity greater than
/// one.</summary>
public class MemIndexerByPair
{
    private readonly int[,] _grid = new int[2, 2];

    /// <summary>15.9.1: a two-dimensional indexer, with block-bodied accessors.</summary>
    public int this[int row, int column]
    {
        get
        {
            return _grid[row, column];
        }

        set
        {
            _grid[row, column] = value;
        }
    }

    /// <summary>15.9.1: an element access with two arguments.</summary>
    public int UseAll()
    {
        this[0, 1] = 3;
        return this[0, 1];
    }
}

/// <summary>15.9.2: an indexer whose parameter list is a parameter array, which a property
/// cannot have and an indexer can.</summary>
public class MemIndexerByParams
{
    private readonly List<int> _seen = [];

    /// <summary>15.9.2: `params` in an indexer. The element access may be written with any
    /// number of arguments, or with one array.</summary>
    public int this[params int[] keys]
    {
        get
        {
            var total = 0;

            foreach (var key in keys)
            {
                total += key;
            }

            return total;
        }
    }

    /// <summary>15.9.2: expanded and normal form at one member.</summary>
    public int UseAll()
    {
        _seen.Add(this[1, 2, 3]);
        return this[new[] { 4, 5 }] + _seen.Count;
    }
}

/// <summary>15.9.1: a generic type's indexer, whose parameter and return types are both type
/// parameters, so the emitted `get_Item` signature is open.</summary>
public class MemIndexerByKey<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _entries = [];

    /// <summary>15.9.1: keyed by one type parameter, returning the other.</summary>
    public TValue? this[TKey key]
    {
        get => _entries.TryGetValue(key, out var value) ? value : default;
        set => _entries[key] = value!;
    }

    /// <summary>15.9.1: read and written at the open type.</summary>
    public TValue? UseAll(TKey key, TValue value)
    {
        this[key] = value;
        return this[key];
    }
}

/// <summary>15.9.2: an indexer with an `init` accessor rather than a `set`, so an element may be
/// written in an object initializer and nowhere else. This is legal and rarely written.</summary>
public class MemIndexerWithInit
{
    private readonly int[] _slots = new int[2];

    /// <summary>15.9.2: `get` beside `init`.</summary>
    public int this[int index]
    {
        get => _slots[index];
        init => _slots[index] = value;
    }

    /// <summary>15.9.2: the only reference form an `init` indexer accessor has.</summary>
    public static int UseAll()
    {
        var subject = new MemIndexerWithInit { [0] = 7 };
        return subject[0];
    }
}

/// <summary>15.9.1: an indexer whose getter returns a reference, so an element access is
/// assignable without there being a setter.</summary>
public class MemIndexerByReference
{
    private readonly int[] _slots = new int[2];

    /// <summary>15.9.1: `ref` return on an indexer.</summary>
    public ref int this[int index] => ref _slots[index];

    /// <summary>15.9.1: assignment through a `ref` return, which is a write with no
    /// `set_Item`.</summary>
    public int UseAll()
    {
        this[0] = 9;
        ref var slot = ref this[1];
        slot = 8;
        return _slots[0] + _slots[1];
    }
}

/// <summary>15.9.1: an indexer keyed by <c>Index</c>, which is what makes `subject[^1]`
/// compile.</summary>
public class MemIndexerByIndex
{
    private readonly int[] _slots = [1, 2, 3];

    /// <summary>15.9.1: keyed by <see cref="Index"/>, so a from-end element access binds to
    /// it.</summary>
    public int this[Index index] => _slots[index];

    /// <summary>15.9.1: both a from-start and a from-end reference to one accessor.</summary>
    public int UseAll() => this[0] + this[^1];
}

/// <summary>15.9.1: an indexer keyed by <c>Range</c>, so a slice expression binds to it. The
/// clause is the same; the emitted `get_Item` takes a `Range`.</summary>
public class MemIndexerByRange
{
    private readonly int[] _slots = [1, 2, 3, 4];

    /// <summary>15.9.1: keyed by <see cref="Range"/>.</summary>
    public int[] this[Range range] => _slots[range];

    /// <summary>15.9.1: a slice reference.</summary>
    public int UseAll() => this[1..3].Length;
}

/// <summary>15.9.1 with 15.7.6: an abstract indexer, whose accessors are bodiless and which an
/// override must supply.</summary>
public abstract class MemIndexerRoot
{
    /// <summary>15.9.1: abstract `get` and `set`.</summary>
    public abstract int this[int index] { get; set; }

    /// <summary>15.9.1: a reference to the abstract member, which resolves to whatever the
    /// runtime type overrode it with.</summary>
    public int Sum() => this[0] + this[1];
}

/// <summary>15.9.1: the override, sealed, so the chain stops. Three declarations of one
/// nameless member exist across this file's hierarchy and the metadata name of every one of them
/// is `Item`.</summary>
public sealed class MemIndexerLeaf : MemIndexerRoot
{
    private readonly int[] _slots = new int[2];

    /// <summary>15.9.1: `sealed override` on an indexer, which seals both accessors.</summary>
    public sealed override int this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value * 2;
    }

    /// <summary>15.9.1: the override through the derived type and through the base one, so both
    /// static types select the same member.</summary>
    public int UseAll()
    {
        this[0] = 1;
        MemIndexerRoot root = this;
        root[1] = 2;
        return Sum();
    }
}

/// <summary>15.9.2: the property that may take the name `Item` — because this type declares no
/// indexer, so nothing has reserved it. In `MemIndexerByInt` the same declaration would be
/// CS0102.</summary>
public sealed class MemItemProperty
{
    /// <summary>15.9.2: an ordinary property called `Item`, which is the name every indexer in
    /// this file emits and which no indexer in this file spells.</summary>
    public int Item { get; set; }

    /// <summary>15.9.2: a read of it, so the name `Item` appears in a reference too.</summary>
    public int UseAll()
    {
        Item = 1;
        return Item;
    }
}

/// <summary>15.9.1: one place that reads every indexer in the file, so no accessor above is
/// declared and never reached from outside its own type.</summary>
public static class MemIndexerUse
{
    /// <summary>15.9.1: nine element accesses, nine different emitted `get_Item`s.</summary>
    public static string ReadAll()
    {
        var byKey = new MemIndexerByKey<string, int>();
        byKey["k"] = 1;
        return $"{new MemIndexerByInt().UseAll()} {new MemIndexerByString().UseAll()} " +
               $"{new MemIndexerByPair().UseAll()} {new MemIndexerByParams().UseAll()} " +
               $"{byKey["k"]} {MemIndexerWithInit.UseAll()} {new MemIndexerByReference().UseAll()} " +
               $"{new MemIndexerByIndex().UseAll()} {new MemIndexerByRange().UseAll()} " +
               $"{new MemIndexerLeaf().UseAll()} {new MemItemProperty().UseAll()}";
    }
}
