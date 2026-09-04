// Clauses 15.7.3 (accessors), 15.7.4 (automatically implemented properties) and 15.7.5
// (accessibility). Every accessor form the language has, in one file, because the accessor is
// the unit an index actually holds a row for and the property is the unit the source names.
//
// The forms: block-bodied, expression-bodied, automatic (no body at all), `init` in place of
// `set`, an accessor with its own accessibility modifier narrower than the property's, and the
// C# 14 `field` keyword — which is a *contextual keyword naming the backing field*, so it is the
// one accessor form where the compiler-generated storage is referenced from source and has no
// declaration anywhere in it.
//
// The `field` keyword compiles here with no `LangVersion` override: the pinned SDK is 10.0.101,
// whose default language version for `net10.0` is C# 14. Both spellings work — a half-automatic
// property (`get;` with a manual `set`) and a fully manual pair that never mentions a field of
// its own.
//
// The hazard on 15.7.5 is the one worth stating twice: an accessor may be *less* accessible
// than the property that contains it, so the property's declared accessibility is not the
// accessibility of both its emitted members. `Locked` below is public with a private setter:
// three rows, two accessibilities, one source declaration.

namespace Surface.Classes.Members.Properties;

/// <summary>15.7.3: the accessor forms that do have bodies.</summary>
public class MemAccessorForms
{
    private int _backing;

    /// <summary>15.7.3: two block-bodied accessors.</summary>
    public int Blocks
    {
        get { return _backing; }
        set { _backing = value; }
    }

    /// <summary>15.7.3: two expression-bodied accessors.</summary>
    public int Arrows
    {
        get => _backing;
        set => _backing = value;
    }

    /// <summary>15.7.3: one of each, in one property.</summary>
    public int Mixed
    {
        get => _backing;
        set { _backing = value * 2; }
    }

    /// <summary>15.7.3: an accessor whose body is a `throw`, so the member exists and no call to
    /// it can return.</summary>
    public int Refused
    {
        get => throw new System.NotSupportedException();
        set => throw new System.NotSupportedException();
    }

    /// <summary>15.7.3: every form above, exercised.</summary>
    public string UseAll()
    {
        Blocks = 1;
        Arrows = Blocks + 1;
        Mixed = Arrows;
        return $"{Blocks} {Arrows} {Mixed} {Refused is 0}";
    }
}

/// <summary>15.7.4: automatically implemented properties — an accessor list with no bodies, and
/// a backing field the source never names.</summary>
public class MemAutoProperties
{
    /// <summary>15.7.4: the plain read-write case.</summary>
    public int Weight { get; set; }

    /// <summary>15.7.4: get-only, which is assignable from a constructor and from an
    /// initializer and from nowhere else.</summary>
    public string Name { get; }

    /// <summary>15.7.4: an auto-property with an initializer, which runs where an instance field
    /// initializer would.</summary>
    public int Ceiling { get; set; } = 100;

    /// <summary>15.7.4 with 15.7.3: `init` in place of `set` — settable in an object initializer
    /// and immutable after construction.</summary>
    public int Floor { get; init; }

    /// <summary>15.7.4: `required`, which moves the obligation to set it onto every caller of
    /// every constructor.</summary>
    public required string Unit { get; init; }

    /// <summary>15.7.4: a static auto-property, whose backing field is static too.</summary>
    public static int Instances { get; set; }

    /// <summary>15.7.4: the get-only property is assigned here, which is the only place other
    /// than an initializer that may.</summary>
    public MemAutoProperties(string name)
    {
        Name = name;
        Instances++;
    }

    /// <summary>15.7.4: all of them, with the two initialize-only ones set through an object
    /// initializer because that is the only reference form that reaches an `init`.</summary>
    public static string UseAll()
    {
        var subject = new MemAutoProperties("dense") { Floor = 1, Unit = "px", Weight = 2 };
        return $"{subject.Weight} {subject.Name} {subject.Ceiling} {subject.Floor} {subject.Unit} {Instances}";
    }
}

/// <summary>15.7.3 with C# 14: the `field` keyword. Inside an accessor, `field` names the
/// backing field the compiler generates for this very property — a reference to storage that
/// has no declaration in the source, which is the inverse of every other reference in this
/// project.</summary>
public class MemFieldKeyword
{
    /// <summary>15.7.3: a fully manual pair over `field`, so the property has no field
    /// declaration of its own and both accessors read the generated one.</summary>
    public int Clamped
    {
        get => field;
        set => field = value < 0 ? 0 : value;
    }

    /// <summary>15.7.3: half automatic — an automatic `get` beside a manual `set` that
    /// normalises through `field`. The `get` has no body and the `set` does.</summary>
    public string Trimmed
    {
        get;
        set => field = value.Trim();
    } = string.Empty;

    /// <summary>15.7.3: `field` under an `init` accessor rather than a `set`.</summary>
    public int Seeded
    {
        get => field;
        init => field = value == 0 ? 1 : value;
    }

    /// <summary>15.7.3: `field` in a static property, so the generated storage is static.</summary>
    public static int Cached
    {
        get => field;
        set => field = value;
    }

    /// <summary>15.7.3: all four, so each generated field is written and read.</summary>
    public static string UseAll()
    {
        var subject = new MemFieldKeyword { Clamped = -1, Trimmed = "  x  ", Seeded = 0 };
        Cached = 4;
        return $"{subject.Clamped} {subject.Trimmed} {subject.Seeded} {Cached}";
    }
}

/// <summary>15.7.5: accessibility. The property states one accessibility and an accessor may
/// state a narrower one, so a single source declaration produces two emitted members that are
/// not equally reachable.</summary>
public class MemAccessorAccessibility
{
    /// <summary>15.7.5: public property, private setter. The getter is public; the setter is
    /// reachable only from inside this type.</summary>
    public int Locked { get; private set; }

    /// <summary>15.7.5: public property, protected setter, so a derived type may write it and an
    /// unrelated one may not.</summary>
    public int Guarded { get; protected set; }

    /// <summary>15.7.5: public property, internal setter — the narrowing is by assembly rather
    /// than by hierarchy.</summary>
    public string Tagged { get; internal set; } = string.Empty;

    /// <summary>15.7.5: `protected internal` property with a `private protected` setter, which
    /// is the widest and the narrowest of the compound accessibilities in one declaration.</summary>
    protected internal int Compound { get; private protected set; }

    /// <summary>15.7.5: an internal property with a private getter, so it is the *read* that is
    /// narrowed and not the write.</summary>
    internal int Sealed { private get; set; }

    /// <summary>15.7.5: every narrowed accessor, written from the one place that may.</summary>
    public string UseAll()
    {
        Locked = 1;
        Guarded = 2;
        Tagged = "t";
        Compound = 3;
        Sealed = 4;
        return $"{Locked} {Guarded} {Tagged} {Compound} {Sealed}";
    }
}

/// <summary>15.7.5: the derived type that reaches the `protected` setter and cannot reach the
/// `private` one, so the narrowing has a reference proving it.</summary>
public sealed class MemAccessorAccessibilityHeir : MemAccessorAccessibility
{
    /// <summary>15.7.5: writes the protected setter and only reads the private-set one.</summary>
    public int Raise()
    {
        Guarded = Locked + 1;
        Compound = Guarded;
        return Guarded;
    }
}
