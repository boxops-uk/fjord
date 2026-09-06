using System.Diagnostics.CodeAnalysis;

namespace Surface.Modern.Members;

/// <summary>
/// C# 11 — Required members. <c>required</c> is a modifier on a property or field that moves
/// an obligation to every construction site: an object creation that omits it is an error. It
/// is enforced by the compiler through attributes the compiler itself writes
/// (<c>RequiredMemberAttribute</c>, <c>CompilerFeatureRequiredAttribute</c>), so the modifier
/// exists in source and in metadata but in two different shapes.
/// </summary>
public class Recipe
{
    /// <summary>C# 11 — <c>required</c> on a property with an <c>init</c> accessor.</summary>
    public required string Name { get; init; }

    /// <summary>C# 11 — <c>required</c> on a settable property.</summary>
    public required int Servings { get; set; }

    /// <summary>C# 11 — <c>required</c> on a field, which is the other half of the feature.</summary>
    public required double Minutes;

    /// <summary>Not required: an ordinary property beside the three above.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// C# 11 — <c>[SetsRequiredMembers]</c>: a constructor that promises to assign every
    /// required member, which relieves *its* callers of the obligation. A call to this
    /// constructor and a call to the parameterless one below are the same syntax with
    /// opposite requirements.
    /// </summary>
    [SetsRequiredMembers]
    public Recipe(string name, int servings, double minutes)
    {
        Name = name;
        Servings = servings;
        Minutes = minutes;
    }

    /// <summary>A constructor with no such promise, so its callers must use an initializer.</summary>
    public Recipe()
    {
    }
}

/// <summary>C# 11 — a derived type inheriting required members and adding one.</summary>
public sealed class TimedRecipe : Recipe
{
    /// <summary>C# 11 — a required member added by a derived type.</summary>
    public required TimeSpan Rest { get; init; }

    /// <summary>Chains to the base constructor that discharges the base's requirements.</summary>
    [SetsRequiredMembers]
    public TimedRecipe(string name, int servings, double minutes, TimeSpan rest)
        : base(name, servings, minutes) => Rest = rest;

    /// <summary>The parameterless form, so the initializer route is available too.</summary>
    public TimedRecipe()
    {
    }
}

/// <summary>Constructs both, by both routes.</summary>
public static class RequiredMemberUses
{
    /// <summary>Through the object initializer, where the requirement is checked.</summary>
    public static Recipe ByInitializer() => new()
    {
        Name = "broth",
        Servings = 4,
        Minutes = 90,
    };

    /// <summary>Through the constructor that discharges the requirement.</summary>
    public static Recipe ByConstructor() => new("stock", 6, 120);

    /// <summary>A derived construction that must satisfy both levels.</summary>
    public static TimedRecipe Derived() => new()
    {
        Name = "dough",
        Servings = 2,
        Minutes = 20,
        Rest = TimeSpan.FromHours(1),
    };
}
