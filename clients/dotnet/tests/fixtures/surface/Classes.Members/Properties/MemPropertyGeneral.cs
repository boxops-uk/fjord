// Clause 15.7.1 (properties — general) and 15.7.2 (static and instance properties). A property
// declares a name, a type and accessors, and unlike a field it names no storage — the accessors
// are function members, so a property is a *pair* of methods wearing the syntax of a variable.
//
// That is the hazard on every row in 15.7, and it is the reason this clause is not just "fields
// with extra steps" as far as identity goes:
//
//   * A property `Weight` is one source declaration and two emitted members, `get_Weight()` and
//     `set_Weight(int)`. An index that keys members by emitted name holds two rows where the
//     source holds one, and neither row's name appears in the file.
//   * An automatically implemented property adds a third, a compiler-named backing field, whose
//     name (`<Weight>k__BackingField`) is not a C# identifier at all.
//   * Reading a property and writing it are two references to *different* members through one
//     name, so `subject.Weight = subject.Weight + 1` is one identifier and two callees.
//
// `MemReservedProperty` in `Members/MemReservedNames.cs` writes the collision this implies.

namespace Surface.Classes.Members.Properties;

/// <summary>15.7.1: one property per shape, from a read-only computed one to a fully manual
/// one with a private backing field.</summary>
public class MemPropertyGeneral
{
    private int _weight;

    /// <summary>15.7.1: a manual property whose accessors have block bodies and whose storage
    /// is a field the source names.</summary>
    public int Weight
    {
        get
        {
            return _weight;
        }

        set
        {
            _weight = value < 0 ? 0 : value;
        }
    }

    /// <summary>15.7.1: a computed property with a `get` and no `set`, so the pair has one
    /// half.</summary>
    public bool IsEmpty
    {
        get { return _weight == 0; }
    }

    /// <summary>15.7.1: the same, in the expression-bodied spelling — one arrow and no `get`
    /// keyword, which is a property and not a method however much it reads like one.</summary>
    public int Doubled => _weight * 2;

    /// <summary>15.7.1: a set-only property, which is legal and which no `get` may read.</summary>
    public int Reset
    {
        set { _weight = value; }
    }

    /// <summary>15.7.1: a property whose type is a reference type, so the accessor pair is over
    /// a reference rather than a value.</summary>
    public string Label
    {
        get => _weight.ToString();
        set => _weight = value.Length;
    }

    /// <summary>15.7.1: a `ref` property — the `get` returns a reference to storage, so
    /// assignment through it does not go through a setter, and there is no setter to go
    /// through.</summary>
    public ref int Slot => ref _weight;

    /// <summary>15.7.1: reads and writes of every property above. `Weight` appears once as a
    /// getter reference and once as a setter reference in one statement.</summary>
    public string UseAll()
    {
        Weight = Weight + 1;
        Reset = 3;
        Label = "abcd";
        Slot = 5;
        return $"{Weight} {IsEmpty} {Doubled} {Label} {Slot}";
    }
}

/// <summary>15.7.2: static properties beside instance ones. A static property's accessors take
/// no `this`, and a static one may be read before any instance exists.</summary>
public class MemStaticProperties
{
    private static int _total;
    private int _own;

    /// <summary>15.7.2: a static property with both accessors, over static storage.</summary>
    public static int Total
    {
        get => _total;
        set => _total = value;
    }

    /// <summary>15.7.2: a static read-only property whose value is computed from static state
    /// alone.</summary>
    public static bool AnyTotal => _total > 0;

    /// <summary>15.7.2: an instance property of the same type, so the pair differs in nothing
    /// but the modifier.</summary>
    public int Own
    {
        get => _own;
        set => _own = value;
    }

    /// <summary>15.7.2: a static property that reads an instance one through a parameter, which
    /// is the only way a static accessor reaches instance state.</summary>
    public static int OwnOf(MemStaticProperties subject) => subject.Own;

    /// <summary>15.7.2: both kinds, read through their two different receivers — the type name
    /// and an instance.</summary>
    public string UseAll()
    {
        Total = 2;
        Own = 3;
        return $"{Total} {AnyTotal} {Own} {OwnOf(this)}";
    }
}
