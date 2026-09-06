// Clause 16.2.2 and 16.4, post-standard — record structs. C# 10 added `record struct` and
// `readonly record struct`, which are struct declarations with a contextual keyword before
// `struct`, and which synthesize more members than any other declaration in the language.
//
// A positional record struct is the widest gap in this project between what is written and
// what exists. `StTally` below is one line of source; the type it declares has a primary
// constructor, an unwritten parameterless constructor (16.4.9), two properties with
// backing fields (16.4.11), a Deconstruct method, an override of Equals(object), a
// strongly typed Equals, an override of GetHashCode, an override of ToString, a
// PrintMembers method, an equality operator pair (16.4.15.5), and an implemented
// IEquatable<T> (16.2.5). More than a dozen members and two backing fields, and the file
// names not one of them.

using System;

namespace Surface.Structs;

/// <summary>
/// Post-standard — a positional `record struct`. The parameters of the primary constructor
/// become public mutable properties, which is the difference from `readonly record struct`
/// below.
/// </summary>
/// <param name="Hits">The number of hits, which becomes a settable property.</param>
/// <param name="Misses">The number of misses.</param>
public record struct StTally(int Hits, int Misses)
{
    /// <summary>Clause 16.4.11 — a member added to the body, beside the synthesized ones.</summary>
    public int Total => Hits + Misses;

    /// <summary>Clause 16.4.12 — a method in the body of a record struct.</summary>
    public double Rate() => Total == 0 ? 0.0 : (double)Hits / Total;
}

/// <summary>
/// Post-standard — a `readonly record struct`. The positional parameters become
/// get/`init` properties, every instance member is implicitly readonly, and the `with`
/// expression is the only way to produce a changed value.
/// </summary>
/// <param name="Label">The name of the reading.</param>
/// <param name="Value">The reading itself.</param>
public readonly record struct StReading(string Label, double Value)
{
    /// <summary>Clause 16.4.11 — an extra property, computed.</summary>
    public bool IsZero => Value == 0.0;

    /// <summary>
    /// Post-standard — an explicitly declared <c>PrintMembers</c>, which replaces the
    /// synthesized one and changes what <c>ToString</c> produces. This is the one
    /// synthesized member of a record that the source can take over without `override`.
    /// </summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append(Label).Append('=').Append(Value);
        return true;
    }
}

/// <summary>
/// Post-standard — a `record struct` with no parameter list at all. It synthesizes the
/// equality members and the parameterless constructor and nothing else: no properties, no
/// Deconstruct, because there are no positional parameters to deconstruct.
/// </summary>
public record struct StRollup
{
    /// <summary>Clause 16.3.1 — a field, which a record struct may declare like any struct.</summary>
    public int Count;

    /// <summary>Clause 16.4.11 — a property with an initializer, which needs a constructor.</summary>
    public string Label { get; init; } = "rollup";

    /// <summary>Clause 16.4.9 — the constructor the initializer requires, declared explicitly.</summary>
    public StRollup(int count) => Count = count;
}

/// <summary>
/// Post-standard — a record struct that declares an override the compiler would otherwise
/// synthesize. <c>ToString</c> here is a real override, and the synthesized one is gone;
/// <c>Equals</c> and <c>GetHashCode</c> are still synthesized and still unwritten.
/// </summary>
/// <param name="Serial">The serial number.</param>
public readonly record struct StStamped(long Serial)
{
    /// <summary>Post-standard — the override that replaces a synthesized member.</summary>
    public override string ToString() => $"stamp/{Serial}";
}

/// <summary>
/// Post-standard — the uses. `with`, deconstruction, positional patterns and value
/// equality all reach members that no declaration in this project names, which is what
/// makes record structs worth their own file.
/// </summary>
public static class StRecordStructUse
{
    /// <summary>Post-standard — a `with` expression on a record struct, changing one property.</summary>
    public static StTally OneMoreHit(StTally tally) => tally with { Hits = tally.Hits + 1 };

    /// <summary>Post-standard — a `with` expression naming both properties.</summary>
    public static StTally Cleared(StTally tally) => tally with { Hits = 0, Misses = 0 };

    /// <summary>Post-standard — a `with` expression on a readonly record struct, which calls `init`.</summary>
    public static StReading Doubled(StReading reading) => reading with { Value = reading.Value * 2.0 };

    /// <summary>Post-standard — an empty `with`, which is a copy and names no member.</summary>
    public static StReading Copy(StReading reading) => reading with { };

    /// <summary>Post-standard — deconstruction, which calls a Deconstruct nothing declared.</summary>
    public static int Spread(StTally tally)
    {
        (int hits, int misses) = tally;
        return hits - misses;
    }

    /// <summary>Post-standard — a positional pattern, which calls the same unwritten Deconstruct.</summary>
    public static string Classify(StTally tally) => tally switch
    {
        (0, 0) => "unused",
        (_, 0) => "clean",
        var (hits, misses) when hits > misses => "mostly hits",
        _ => "mostly misses",
    };

    /// <summary>Post-standard — value equality through the synthesized operator pair.</summary>
    public static bool SameTally(StTally left, StTally right) => left == right;

    /// <summary>Post-standard — inequality, the other synthesized operator.</summary>
    public static bool DifferentReading(StReading left, StReading right) => left != right;

    /// <summary>Post-standard — the synthesized strongly typed Equals, through a constraint.</summary>
    public static bool SameVia<T>(T left, T right)
        where T : struct, IEquatable<T> => left.Equals(right);

    /// <summary>Post-standard — the synthesized ToString, and the one that was overridden.</summary>
    public static string Texts() =>
        new StTally(1, 2).ToString() + "|" + new StStamped(3).ToString() + "|" + new StReading("a", 1.0).ToString();

    /// <summary>Post-standard — the parameterless constructor of a positional record struct.</summary>
    public static int EmptyTally() => new StTally().Total;

    /// <summary>Post-standard — the record struct with no parameter list.</summary>
    public static string Rollup() => new StRollup(2) { Label = "seen" }.Label;

    /// <summary>Clause 16.4.11 — the mutable property of a record struct, assigned.</summary>
    public static int Mutate()
    {
        StTally tally = new StTally(1, 1);
        tally.Hits = 5;
        return tally.Hits;
    }
}
