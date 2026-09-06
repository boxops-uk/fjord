// Clause 15.12 (static constructors). A static constructor is spelled with the type's name and
// no parameters, takes no accessibility modifier, cannot be overloaded, cannot be called, and is
// emitted as `.cctor` rather than `.ctor`.
//
// The hazard is the sharpest name collision in the whole area, and `MemRegistry` writes it: a
// type may declare **both** a parameterless instance constructor and a static constructor. In
// source the two are spelled identically apart from one keyword; both have the type's name and
// an empty parameter list; and neither may be given a parameter to tell them apart. An identity
// of (type, name, parameter types) mints one string for the two of them. In metadata they are
// `.ctor()` and `.cctor()`, so the separation exists — but nothing in the source's *name*
// carries it.
//
// The second hazard is that a static constructor has no call site anywhere. It runs before the
// first instance is created or the first static member is touched, so a query for its callers
// must answer "none", and that answer is correct rather than a resolution failure.

using System;
using System.Collections.Generic;

namespace Surface.Classes.Members.Constructors;

/// <summary>15.12: a static constructor beside a parameterless instance one.</summary>
public class MemRegistry
{
    /// <summary>15.12: static state the static constructor initializes.</summary>
    private static readonly Dictionary<string, int> Known;

    /// <summary>15.12: the static constructor. No modifiers, no parameters, no callers.</summary>
    static MemRegistry()
    {
        Known = new Dictionary<string, int> { ["seed"] = 1 };
        Created = DateTime.UnixEpoch;
    }

    /// <summary>15.12: the instance constructor whose source spelling differs from the static
    /// one by the word `static` and by nothing else.</summary>
    public MemRegistry()
    {
        Instances++;
    }

    /// <summary>15.12: an instance constructor with a parameter, so the type has three
    /// constructors and two of them are `.ctor`.</summary>
    public MemRegistry(string key)
        : this()
        => Known[key] = Known.Count;

    /// <summary>15.12: when the static constructor ran.</summary>
    public static DateTime Created { get; private set; }

    /// <summary>15.12: how many instances there have been.</summary>
    public static int Instances { get; private set; }

    /// <summary>15.12: how many keys the static constructor left behind.</summary>
    public int KnownCount => Known.Count;

    /// <summary>15.12: a reference that triggers the static constructor without naming it — the
    /// static constructor has no call sites, and this is the closest thing to one.</summary>
    public static string UseAll()
    {
        var first = new MemRegistry();
        var second = new MemRegistry("second");
        return $"{Created:yyyy} {Instances} {first.KnownCount} {second.KnownCount}";
    }
}

/// <summary>15.12: a static constructor in a static class, which is where most of them are —
/// no instance constructor of any kind exists here, so `.cctor` is the type's only
/// constructor.</summary>
public static class MemDefaults
{
    /// <summary>15.12: the static constructor of a static class.</summary>
    static MemDefaults()
    {
        Unit = "px";
        Ceiling = 100;
    }

    /// <summary>15.12: what it set.</summary>
    public static string Unit { get; }

    /// <summary>15.12: and the other thing it set.</summary>
    public static int Ceiling { get; }

    /// <summary>15.12: a read that forces the static constructor to have run.</summary>
    public static string UseAll() => $"{Unit} {Ceiling}";
}

/// <summary>15.12: a static constructor in a generic type, which runs once per constructed type
/// rather than once — so one declaration corresponds to as many executions as there are
/// instantiations, and `MemCounterOf&lt;int&gt;` and `MemCounterOf&lt;string&gt;` each have their
/// own.</summary>
public class MemCounterOf<TItem>
{
    /// <summary>15.12: the static constructor of an open type.</summary>
    static MemCounterOf() => TypeName = typeof(TItem).Name;

    /// <summary>15.12: the name of whichever type argument this instantiation carries.</summary>
    public static string TypeName { get; } = string.Empty;

    /// <summary>15.12: two instantiations, so the one declaration runs twice.</summary>
    public static string UseAll() => $"{MemCounterOf<int>.TypeName} {MemCounterOf<string>.TypeName}";
}

/// <summary>15.12: a static constructor in a struct, which is legal and which runs later than a
/// class's — a struct with a static constructor is still default-constructible without running
/// it.</summary>
public struct MemStamp
{
    /// <summary>15.12: the static constructor of a value type.</summary>
    static MemStamp() => Origin = 1;

    /// <summary>15.12: what it set.</summary>
    public static int Origin { get; }

    /// <summary>The stamped value.</summary>
    public int Value;

    /// <summary>15.12: `default(MemStamp)` does not run the static constructor and reading
    /// `Origin` does, so both references are here.</summary>
    public static string UseAll()
    {
        var blank = default(MemStamp);
        return $"{blank.Value} {Origin}";
    }
}
