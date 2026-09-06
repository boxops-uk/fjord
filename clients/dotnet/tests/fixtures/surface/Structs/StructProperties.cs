// Clause 16.4.11 — Properties. A property of a struct follows the rules for a property of
// a class, with the struct rules layered on: an automatically implemented property of a
// struct is an instance field the source does not declare, and in a readonly struct it may
// only have a get accessor or an `init` one.
//
// The hazard is the backing field. `Reading { get; set; }` declares one member in source
// and two in metadata — the property and `<Reading>k__BackingField` — and the field's name
// contains the property's. Anything that keys a field on the name it can see either
// invents a declaration for the backing field or loses it; and if the mangling is stripped
// the field and the property collide on one name in one type.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.11 — the accessor forms, one property each. Two of the five have backing
/// fields nothing declares, and three have no storage at all.
/// </summary>
public struct StPropertySet
{
    private int _explicitStore;

    /// <summary>Clause 16.4.9 — the constructor, so the field initializer below is legal.</summary>
    public StPropertySet(int reading)
    {
        _explicitStore = reading;
        Reading = reading;
    }

    /// <summary>Clause 16.4.11 hazard — an automatically implemented property, get and set.</summary>
    public int Reading { get; set; }

    /// <summary>Clause 16.4.11 — a get-only auto-property, settable only from a constructor.</summary>
    public int Origin { get; }

    /// <summary>Clause 16.4.11, post-standard — an `init`-only accessor.</summary>
    public string? Label { get; init; }

    /// <summary>Clause 16.4.11 — a property with a full accessor body over a declared field.</summary>
    public int Explicit
    {
        get { return _explicitStore; }
        set { _explicitStore = value < 0 ? 0 : value; }
    }

    /// <summary>Clause 16.4.11 — an expression-bodied property, which is a get accessor.</summary>
    public int Sum => Reading + _explicitStore;

    /// <summary>Clause 16.4.11 — a static property, which has no receiver.</summary>
    public static int Ceiling { get; set; } = 100;

    /// <summary>Clause 16.4.11 — a property with a private set, so the accessors differ in access.</summary>
    public int Sealed { get; private set; }

    /// <summary>Clause 16.4.12 — the only writer of the private set accessor.</summary>
    public void Seal(int value) => Sealed = value;
}

/// <summary>
/// Clause 16.4.11, post-standard — `required` properties on a struct. A required member
/// must be set by every object initializer, and `default(T)` sets nothing, so the language
/// rule is enforced at one of the two ways of making this struct and not at the other.
/// </summary>
public struct StRequiredSlot
{
    /// <summary>Clause 16.4.11 — a required property, which no constructor here sets.</summary>
    public required int Capacity { get; init; }

    /// <summary>Clause 16.4.11 — an ordinary property beside it, which may be omitted.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Clause 16.4.11 — a readonly struct's properties, which may not have an ordinary set
/// accessor. The `init` one is legal, and it writes a field of a readonly struct, which no
/// other member may do.
/// </summary>
public readonly struct StFrozenProperties
{
    /// <summary>Clause 16.4.11 — a get-only auto-property in a readonly struct.</summary>
    public int Reading { get; }

    /// <summary>Clause 16.4.11 — an `init` accessor in a readonly struct.</summary>
    public string? Label { get; init; }

    /// <summary>Clause 16.4.9 — the constructor, which sets the get-only property.</summary>
    public StFrozenProperties(int reading)
    {
        Reading = reading;
        Label = null;
    }

    /// <summary>Clause 16.4.11 — an expression-bodied property, implicitly readonly.</summary>
    public bool IsZero => Reading == 0;
}

/// <summary>
/// Clause 16.4.11 — the property accesses. A property read on a struct passed by `in` may
/// copy the whole struct first, which is invisible at the call site.
/// </summary>
public static class StPropertyUse
{
    /// <summary>Clause 16.4.11 — a set accessor, then a get accessor, on a local.</summary>
    public static int SetThenGet()
    {
        StPropertySet properties = new StPropertySet(1);
        properties.Reading = 4;
        return properties.Reading;
    }

    /// <summary>Clause 16.4.11 — an object initializer, which calls set and init accessors.</summary>
    public static StPropertySet Initialized() => new StPropertySet(1) { Reading = 2, Label = "p" };

    /// <summary>Clause 16.4.11 — the static property, read and written with no instance.</summary>
    public static int Ceiling()
    {
        StPropertySet.Ceiling = 50;
        return StPropertySet.Ceiling;
    }

    /// <summary>Clause 16.4.11 — the required property, which the initializer must set.</summary>
    public static int Required() => new StRequiredSlot { Capacity = 4 }.Capacity;

    /// <summary>
    /// Clause 16.4.11 / 16.4.5 — the same struct through `default`, which satisfies no
    /// required member and is legal anyway.
    /// </summary>
    public static int RequiredBypassed() => default(StRequiredSlot).Capacity;

    /// <summary>Clause 16.4.11 — the private set accessor, reached through its method.</summary>
    public static int Sealed()
    {
        StPropertySet properties = new StPropertySet(0);
        properties.Seal(9);
        return properties.Sealed;
    }

    /// <summary>Clause 16.4.11 — a property read through an `in` parameter of a mutable struct.</summary>
    public static int ReadThroughIn(in StPropertySet properties) => properties.Sum;

    /// <summary>Clause 16.4.11 — and through a nullable, where the access is on Value.</summary>
    public static int ReadThroughNullable(StFrozenProperties? properties) => properties?.Reading ?? -1;
}
