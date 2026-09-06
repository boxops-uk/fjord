// Clause 15.3.10 (reserved member names). 15.3.10.1 general: certain member declarations
// reserve *other* names in the same declaration space, because of what the compiler emits for
// them. A property `Weight` reserves `get_Weight` and `set_Weight` (15.3.10.2); an event `Tick`
// reserves `add_Tick` and `remove_Tick` (15.3.10.3); an indexer reserves `get_Item` and
// `set_Item` (15.3.10.4); a finalizer reserves `Finalize` (15.3.10.5); an operator reserves
// `op_Addition` and its siblings (15.3.10.6).
//
// This is the clause where source names and emitted names come apart, and it is the sharpest
// hazard in the area: **the member a query is asked about may have a name that appears nowhere
// in the file**. Every type below writes the collision in the one form the compiler allows.
//
// What the compiler actually does, checked rather than recalled:
//
//   * The reservation is by name **and parameter types**. `int Weight { get; }` beside
//     `int get_Weight()` is CS0082, but beside `int get_Weight(int scale)` it compiles clean —
//     no warning. So one type may hold two members whose emitted names are both `get_Weight`,
//     separated by nothing but a parameter list.
//   * An operator reservation is not a reservation but a definition: `operator +` beside
//     `static op_Addition(OpRes, OpRes)` is CS0111, not CS0082 — the operator *is* a method
//     called `op_Addition`.
//   * `~C()` beside `void Finalize()` is CS0111 for the same reason. Beside
//     `void Finalize(int)` it compiles with no diagnostic at all, not even CS0465.
//   * `[IndexerName("Element")]` moves the reservation: with it, `get_Item` is free and
//     `get_Element` is taken.
//
// Each of those exact diagnostics is quoted in this project's README.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Classes.Members.Reserved;

/// <summary>15.3.10.1: the names a member declaration reserves, declared as ordinary methods in
/// a type that declares none of the members that would reserve them. Every method here is
/// spelled like something the compiler emits, and every one of them is just a method.</summary>
public class MemReservedNamesFree
{
    /// <summary>15.3.10.2: the spelling of a property getter, with no property to own it.</summary>
    public int get_Weight() => 1;

    /// <summary>15.3.10.2: the spelling of a property setter, likewise.</summary>
    public void set_Weight(int value) => _ = value;

    /// <summary>15.3.10.3: the spelling of an event's `add`, with no event.</summary>
    public void add_Tick(EventHandler handler) => _ = handler;

    /// <summary>15.3.10.3: the spelling of an event's `remove`.</summary>
    public void remove_Tick(EventHandler handler) => _ = handler;

    /// <summary>15.3.10.4: the spelling of an indexer's getter, with no indexer.</summary>
    public int get_Item(int index) => index;

    /// <summary>15.3.10.4: and its setter.</summary>
    public void set_Item(int index, int value) => _ = index + value;

    /// <summary>15.3.10.5: the spelling of a finalizer, with no finalizer. This one *does* warn
    /// — CS0465 — because the runtime would call it.</summary>
    public void Finalize() => _ = 0;

    /// <summary>15.3.10.6: the spelling of the addition operator, with no operator.</summary>
    public static int op_Addition(int left, int right) => left + right;

    /// <summary>15.3.10.6: the spelling of the explicit conversion operator.</summary>
    public static int op_Explicit(long value) => (int)value;

    /// <summary>15.3.10.1: references to all of them, so that a name that looks generated is
    /// nonetheless called from source.</summary>
    public string UseAll()
    {
        set_Weight(2);
        add_Tick((_, _) => { });
        remove_Tick((_, _) => { });
        set_Item(0, 1);
        Finalize();
        return $"{get_Weight()} {get_Item(0)} {op_Addition(1, 2)} {op_Explicit(3L)}";
    }
}

/// <summary>15.3.10.2: the property that reserves the names, beside a method that takes the
/// reserved name with a different parameter list. Two members of one type whose emitted names
/// are both <c>get_Weight</c>.</summary>
public class MemReservedProperty
{
    /// <summary>15.7.1: the property. Emits `get_Weight()` and `set_Weight(int)`.</summary>
    public int Weight { get; set; }

    /// <summary>15.3.10.2: a method whose source name is already the property's emitted getter
    /// name. Legal because the parameter types differ; `get_Weight()` here would be CS0082.</summary>
    public int get_Weight(int scale) => Weight * scale;

    /// <summary>15.3.10.2: and the setter's name, with an extra parameter.</summary>
    public void set_Weight(int value, int scale) => Weight = value * scale;

    /// <summary>15.3.10.2: the property and the two same-named methods, all read.</summary>
    public int UseAll()
    {
        Weight = 3;
        set_Weight(2, 2);
        return Weight + get_Weight(2);
    }
}

/// <summary>15.3.10.3: the event that reserves the names, beside methods that take them.</summary>
public class MemReservedEvent
{
    /// <summary>15.8.2: the field-like event. Emits `add_Tick(EventHandler)` and
    /// `remove_Tick(EventHandler)`.</summary>
    public event EventHandler? Tick;

    /// <summary>15.3.10.3: a method spelled like the event's `add`, with a different parameter
    /// type. `add_Tick(EventHandler)` here would be CS0082.</summary>
    public void add_Tick(int repeats) => _ = repeats;

    /// <summary>15.3.10.3: and the `remove`.</summary>
    public void remove_Tick(int repeats) => _ = repeats;

    /// <summary>15.8.1: raising the event, which is the only reference to it that a class other
    /// than the declaring one cannot write.</summary>
    public void Raise()
    {
        add_Tick(1);
        remove_Tick(1);
        Tick?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>15.3.10.4: the indexer that reserves <c>get_Item</c>, beside a method that takes the
/// name with a different parameter type. One indexer only — a second `this[...]` is a
/// quarantined shape.</summary>
public class MemReservedIndexer
{
    private readonly int[] _slots = new int[4];

    /// <summary>15.9.1: the indexer. Emits `get_Item(int)` and `set_Item(int, int)`.</summary>
    public int this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value;
    }

    /// <summary>15.3.10.4: a method spelled like the indexer's getter, keyed by string rather
    /// than by int. `get_Item(int)` here would be CS0082.</summary>
    public int get_Item(string key) => key.Length;

    /// <summary>15.3.10.4: the indexer and the same-named method, both read.</summary>
    public int UseAll()
    {
        this[0] = 5;
        return this[0] + get_Item("ab");
    }
}

/// <summary>15.3.10.4 with 15.9.2: <c>IndexerName</c> moves the reservation. This indexer emits
/// <c>get_Element</c>, so <c>get_Item</c> is an ordinary name here and the source name
/// <c>this[...]</c> corresponds to no emitted name a reader could guess.</summary>
public class MemRenamedIndexer
{
    private readonly string[] _slots = ["a", "b"];

    /// <summary>15.9.2: the indexer, emitted as `Element` rather than as `Item`.</summary>
    [IndexerName("Element")]
    public string this[int index] => _slots[index];

    /// <summary>15.3.10.4: the name the indexer would have reserved without the attribute, free
    /// and taken by a method with the very signature that would have clashed.</summary>
    public string get_Item(int index) => _slots[index].ToUpperInvariant();

    /// <summary>15.3.10.4: both, so the pair is reached.</summary>
    public string UseAll() => this[0] + get_Item(1);
}

/// <summary>15.3.10.5: the finalizer that reserves <c>Finalize</c>, beside a method of that name
/// with a parameter. The finalizer's source name is <c>~MemReservedFinalizer</c> and its emitted
/// name is <c>Finalize</c>, so this type has two members named <c>Finalize</c> in metadata and
/// no two members with one name in source.</summary>
public class MemReservedFinalizer
{
    private int _generation;

    /// <summary>15.13: the finalizer. Emitted as `Finalize()`.</summary>
    ~MemReservedFinalizer() => _generation = -1;

    /// <summary>15.3.10.5: a method named exactly what the finalizer is emitted as, with one
    /// parameter. `Finalize()` here would be CS0111 — the finalizer already defines it.</summary>
    public int Finalize(int generation) => _generation = generation;

    /// <summary>15.3.10.5: the method, called. The finalizer is called by the runtime and by
    /// nothing in this corpus.</summary>
    public int UseAll() => Finalize(2);
}

/// <summary>15.3.10.6: the operator that defines <c>op_Addition</c>, beside a method of that
/// name with different parameter types. An operator is not a reservation — it is a method — so
/// the clash here is CS0111 rather than CS0082.</summary>
public sealed class MemReservedOperator
{
    /// <summary>What is added.</summary>
    public int Weight { get; init; }

    /// <summary>15.10.3: the operator. Emitted as
    /// `op_Addition(MemReservedOperator, MemReservedOperator)`.</summary>
    public static MemReservedOperator operator +(MemReservedOperator left, MemReservedOperator right)
        => new MemReservedOperator { Weight = left.Weight + right.Weight };

    /// <summary>15.3.10.6: a method whose source name is the operator's emitted name, over
    /// different parameter types.</summary>
    public static int op_Addition(int left, int right) => left + right;

    /// <summary>15.3.10.6: a method named for an operator this type does not declare at
    /// all, so the emitted name has no operator behind it.</summary>
    public static int op_Multiply(int left, int right) => left * right;

    /// <summary>15.3.10.6: the operator through its symbol, and the two methods through their
    /// names.</summary>
    public static int UseAll()
    {
        var sum = new MemReservedOperator { Weight = 1 } + new MemReservedOperator { Weight = 2 };
        return sum.Weight + op_Addition(1, 2) + op_Multiply(2, 3);
    }
}
