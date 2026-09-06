// Clause 16.4.8 — Field initializers. The standard's clause 16.4.8 says an instance field
// declaration in a struct cannot include a variable initializer. Post-standard C# 10
// permits it, on one condition: the struct must declare at least one constructor, because
// the initializers run as part of a constructor body and `default(S)` never runs one. So
// a struct field with an initializer has TWO values depending on how the instance came to
// exist, and neither of them is written twice.
//
// That is the hazard: the initializer and the constructor's assignment are two writes to
// one field, on two different lines, and a field's declaration is one row.

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.8, post-standard — field initializers in a struct, beside the declared
/// constructor that C# requires before they are legal. <c>new StSeeded()</c> gives 8 and
/// 1.0; <c>default(StSeeded)</c> gives 0 and 0.0, from the same declarations.
/// </summary>
public struct StSeeded
{
    /// <summary>Clause 16.4.8 — a field with an initializer, which only a constructor runs.</summary>
    public int Capacity = 8;

    /// <summary>Clause 16.4.8 — a second initialized field, of a different type.</summary>
    public double Scale = 1.0;

    /// <summary>Clause 16.4.8 — an initialized reference-typed field.</summary>
    public string Label = "seeded";

    /// <summary>
    /// Clause 16.4.8 / 16.4.9 — the explicitly declared parameterless constructor. Without
    /// a declared constructor of some kind the initializers above are a compile error;
    /// with this one, `new StSeeded()` runs all three.
    /// </summary>
    public StSeeded()
    {
    }

    /// <summary>
    /// Clause 16.4.8 hazard — a second constructor that overwrites one of the initialized
    /// fields. <c>Capacity</c> is written on its declaration and written again here, so
    /// two lines assign one field and the second one wins.
    /// </summary>
    public StSeeded(int capacity)
    {
        Capacity = capacity;
    }
}

/// <summary>
/// Clause 16.4.8 / 16.4.11 — the property form of the same thing. An auto-property
/// initializer writes a backing field that no line declares, so here the initializer's
/// target is unwritten as well as its second write.
/// </summary>
public struct StSeededProperty
{
    /// <summary>Clause 16.4.11 — an auto-property with an initializer.</summary>
    public int Capacity { get; set; } = 8;

    /// <summary>Clause 16.4.11 — an `init`-only auto-property with an initializer.</summary>
    public string Label { get; init; } = "seeded";

    /// <summary>Clause 16.4.8 — the constructor the initializers need.</summary>
    public StSeededProperty(int capacity) => Capacity = capacity;
}

/// <summary>
/// Clause 16.4.8 — a struct with a static field initializer, which is a different rule: a
/// static field of a struct may always have an initializer, and it runs once with the
/// static constructor rather than once per instance.
/// </summary>
public struct StStaticSeeded
{
    /// <summary>Clause 16.4.8 — a static field initializer, legal with no constructor at all.</summary>
    public static int Created = 0;

    /// <summary>Clause 16.3.1 — a const, whose initializer is required and is not a field write.</summary>
    public const int Ceiling = 64;

    /// <summary>An instance field with no initializer, so the struct needs no constructor.</summary>
    public int Used;
}

/// <summary>
/// Clause 16.4.8 — the uses, which are the only place the difference between an
/// initialized field and a default one can be seen.
/// </summary>
public static class StFieldInitializerUse
{
    /// <summary>Clause 16.4.8 — through the parameterless constructor: the initializers ran.</summary>
    public static int SeededCapacity() => new StSeeded().Capacity;

    /// <summary>
    /// Clause 16.4.8 / 16.4.5 — through `default`, which runs no constructor, so the field
    /// holds zero and the initializer on its declaration is dead.
    /// </summary>
    public static int DefaultCapacity() => default(StSeeded).Capacity;

    /// <summary>Clause 16.4.8 — through the constructor that overwrites one field.</summary>
    public static int OverriddenCapacity() => new StSeeded(3).Capacity;

    /// <summary>
    /// Clause 16.4.8 — an array of the struct, whose elements are all default: a hundred
    /// instances for which the initializers never ran.
    /// </summary>
    public static double TotalScale()
    {
        StSeeded[] row = new StSeeded[4];
        double total = 0.0;
        foreach (StSeeded item in row)
        {
            total += item.Scale;
        }

        return total;
    }

    /// <summary>Clause 16.4.8 — the property form, both ways round.</summary>
    public static string Labels() =>
        new StSeededProperty(1).Label + "/" + (default(StSeededProperty).Label ?? "null");

    /// <summary>Clause 16.4.8 — the static initializer, which needs no instance.</summary>
    public static int StaticReach() => StStaticSeeded.Created + StStaticSeeded.Ceiling;
}
