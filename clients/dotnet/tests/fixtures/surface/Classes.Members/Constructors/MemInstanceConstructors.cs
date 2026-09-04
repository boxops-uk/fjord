// Clauses 15.11.1 (instance constructors — general), 15.11.2 (constructor initializers),
// 15.11.3 (instance variable initializers) and 15.11.5 (default constructors).
//
// An instance constructor is the one function member with no name of its own: in source it is
// spelled with the containing type's name, and in metadata every one of them is `.ctor`. So a
// type with five constructors has five members that agree on both spellings, and only the
// parameter list separates them — which makes 15.11 the clause where "name plus ordinal" is not
// a shortcut but the only thing there is.
//
// Four hazards, in the four types below.
//
//   1. **Five `.ctor`s in one type.** `MemBox` declares five, one of which is private and
//      reachable only through a `: this(...)` initializer.
//   2. **A constructor whose declaration is the class header.** `MemPrimary` has a primary
//      constructor: its parameter list is on the type declaration, its "body" is the field and
//      property initializers, and its parameters are in scope in every instance member. The
//      second constructor must chain to it, which is the one call site that names it.
//   3. **A constructor that exists with no declaration at all.** `MemDefaulted` declares no
//      constructor, so it has a public parameterless one that appears nowhere in the source —
//      the inverse of every other row in this project, where the source has a declaration and
//      the question is what identity it gets.
//   4. **A constructor that stops existing.** `MemNoDefault` declares one constructor with
//      parameters, so the default one is *not* generated: `new MemNoDefault()` is CS7036. The
//      absence is a fact about the type, and it is caused by an unrelated declaration.
//
// 15.11.3 is written by `MemBox`: instance variable initializers run in every constructor that
// does not chain to another one of the same type, so the reference in `_stamp`'s initializer
// belongs to four constructors and to none of them in particular. 15.11.4 (constructor
// execution) states only the order in which those parts run and is recorded `not-applicable`.

using System;

namespace Surface.Classes.Members.Constructors;

/// <summary>15.11.1: five constructors, one name. The chain through them is 15.11.2.</summary>
public class MemBox
{
    /// <summary>15.11.3: an instance variable initializer. It runs before the body of whichever
    /// constructor is entered, and it is a reference from no constructor's body.</summary>
    private readonly DateTime _stamp = DateTime.UnixEpoch;

    /// <summary>15.11.3: an initializer that calls a method, so a reference exists in a place
    /// that is inside no method body in the source.</summary>
    private readonly int _seed = Seed();

    /// <summary>15.11.1: the parameterless constructor, chaining to the two-parameter one
    /// through `: this(...)`. Because it chains, the two initializers above do *not* run for it
    /// a second time.</summary>
    public MemBox()
        : this(0, "unnamed")
    {
    }

    /// <summary>15.11.1: one parameter, chaining to the same target with a default filled
    /// in.</summary>
    public MemBox(int width)
        : this(width, "unnamed")
    {
    }

    /// <summary>15.11.2: the target of both chains — the constructor that actually assigns, and
    /// the only one whose body runs the initializers.</summary>
    public MemBox(int width, string name)
    {
        Width = width;
        Name = name;
    }

    /// <summary>15.11.1: a constructor with a different parameter type, so the overload set is
    /// separated by type as well as by count.</summary>
    public MemBox(string name)
        : this(name.Length, name)
    {
    }

    /// <summary>15.11.1: a private constructor, reachable only from inside the type — and the
    /// only member here that a caller outside it cannot name at all.</summary>
    private MemBox(bool empty)
        : this(empty ? 0 : 1, "private")
    {
    }

    /// <summary>15.11.1: the width the box was built with.</summary>
    public int Width { get; }

    /// <summary>15.11.1: the name the box was built with.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>15.11.3: the method an instance variable initializer calls.</summary>
    private static int Seed() => 7;

    /// <summary>15.11.1: the reference that reaches the private constructor, which is a call to
    /// a `.ctor` no other type may name.</summary>
    public static MemBox Empty() => new MemBox(true);

    /// <summary>15.11.1: every public constructor, called. Four call sites, one name.</summary>
    public static string UseAll()
    {
        var boxes = new[] { new MemBox(), new MemBox(2), new MemBox(3, "third"), new MemBox("fourth"), Empty() };
        return $"{boxes.Length} {boxes[0].Name} {boxes[4]._stamp:yyyy} {boxes[4]._seed}";
    }
}

/// <summary>15.11.2: a base class, so `: base(...)` has somewhere to point.</summary>
public class MemCrateBase
{
    /// <summary>15.11.1: a base constructor with a parameter, so a derived one must call
    /// it.</summary>
    protected MemCrateBase(int capacity) => Capacity = capacity;

    /// <summary>15.11.1: a second base constructor, so `: base(...)` picks between two.</summary>
    protected MemCrateBase(int capacity, string label)
        : this(capacity)
        => Label = label;

    /// <summary>How much fits.</summary>
    public int Capacity { get; }

    /// <summary>What it is called.</summary>
    public string Label { get; } = string.Empty;
}

/// <summary>15.11.2: constructor initializers of both kinds in one type — `: base(...)` up and
/// `: this(...)` sideways. Every constructor has an initializer, explicit or not: one with none
/// written has an implicit `: base()`.</summary>
public sealed class MemCrate : MemCrateBase
{
    /// <summary>15.11.2: `: base(...)` selecting the one-parameter base constructor.</summary>
    public MemCrate(int capacity)
        : base(capacity)
    {
    }

    /// <summary>15.11.2: `: base(...)` selecting the two-parameter one, so the initializer is
    /// itself an overload resolution.</summary>
    public MemCrate(int capacity, string label)
        : base(capacity, label)
    {
    }

    /// <summary>15.11.2: `: this(...)` sideways, which reaches a base constructor only
    /// indirectly — the chain is this, then that, then `base`.</summary>
    public MemCrate()
        : this(1, "default")
    {
    }

    /// <summary>15.11.2: all three chains, entered at each of their heads.</summary>
    public static string UseAll()
    {
        var crates = new[] { new MemCrate(), new MemCrate(2), new MemCrate(3, "third") };
        return $"{crates[0].Label} {crates[1].Capacity} {crates[2].Label}";
    }
}

/// <summary>15.11.1: a primary constructor. The parameter list is on the type declaration, so
/// the constructor has no declaration of its own to point at, and `capacity` and `label` are in
/// scope in every instance member below.</summary>
public class MemPrimary(int capacity, string label)
{
    /// <summary>15.11.3: an instance variable initializer that reads a primary constructor
    /// parameter — a reference whose enclosing member is the constructor the header
    /// declares.</summary>
    private readonly int _doubled = capacity * 2;

    /// <summary>15.11.2: the one constructor initializer form a primary constructor forces. A
    /// class with a primary constructor may declare another constructor only if it chains to
    /// the primary one, so this `: this(...)` is required rather than chosen.</summary>
    public MemPrimary(int capacity)
        : this(capacity, "unlabelled")
    {
    }

    /// <summary>15.11.1: a property whose initializer reads a primary constructor
    /// parameter.</summary>
    public string Label { get; } = label;

    /// <summary>15.11.1: a method body reading a primary constructor parameter, which is a
    /// reference to a parameter of a constructor from inside a different member.</summary>
    public int Capacity() => capacity;

    /// <summary>15.11.1: the doubled value, so the initializer above has a reader.</summary>
    public int Doubled() => _doubled;

    /// <summary>15.11.1: both constructors, called.</summary>
    public static string UseAll()
    {
        var full = new MemPrimary(2, "full");
        var chained = new MemPrimary(3);
        return $"{full.Capacity()} {full.Label} {chained.Label} {chained.Doubled()}";
    }
}

/// <summary>15.11.5: a default constructor. This type declares no constructor, so it has a
/// public parameterless one that exists in metadata and nowhere in this file.</summary>
public class MemDefaulted
{
    /// <summary>15.11.5: a field with an initializer, which the generated default constructor
    /// runs — so the constructor with no declaration has a body with statements in it.</summary>
    public int Weight = 1;

    /// <summary>15.11.5: a property the generated constructor also initializes.</summary>
    public string Name { get; set; } = "defaulted";
}

/// <summary>15.11.5: the type where the default constructor is *absent*. Declaring one
/// constructor with parameters suppresses it, so `new MemNoDefault()` is CS7036 — a fact about a
/// member that does not exist, caused by a declaration of a different one.</summary>
public class MemNoDefault
{
    /// <summary>15.11.5: the declaration that suppresses the default constructor.</summary>
    public MemNoDefault(int weight) => Weight = weight;

    /// <summary>What it was built with.</summary>
    public int Weight { get; }
}

/// <summary>15.11.5: references to the generated constructor and to the one that replaced
/// it.</summary>
public static class MemDefaultConstructorUse
{
    /// <summary>15.11.5: a call to a constructor with no declaration, beside a call to one with
    /// a declaration and no generated sibling.</summary>
    public static string UseAll()
    {
        var generated = new MemDefaulted();
        var declared = new MemNoDefault(2);
        return $"{generated.Weight} {generated.Name} {declared.Weight}";
    }
}
