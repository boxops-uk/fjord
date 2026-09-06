// C# 10 — File-scoped namespace declaration. Every file in this project uses this form except
// `Interceptors/CallInterceptor.cs`, which has to declare two namespaces in one file and so
// cannot: a file-scoped declaration covers the whole file by definition. The two spellings
// nest identically, which is the fact worth checking — `Surface.Modern.Records` here and
// `Surface.Modern.Interceptors` there should be the same kind of thing to an index.
namespace Surface.Modern.Records;

/// <summary>
/// C# 10 — a <c>record class</c>, spelled with the optional <c>class</c> keyword that the
/// same release introduced. C# 10 — the record also seals <see cref="ToString"/>.
/// </summary>
public record class Reading(string Sensor, double Value)
{
    /// <summary>
    /// C# 10 — Record types can seal ToString: a record may declare
    /// <c>sealed override string ToString()</c>, which no earlier version allowed.
    /// </summary>
    public sealed override string ToString() => $"{Sensor}={Value}";
}

/// <summary>C# 10 — Record structs: a <c>record struct</c>, whose members are mutable.</summary>
public record struct Sample(int Index, double Value)
{
    /// <summary>A member added to the primary constructor's body, so the struct is not empty.</summary>
    public double Scaled => Value * Index;
}

/// <summary>C# 10 — Record structs: the <c>readonly record struct</c> form.</summary>
public readonly record struct SampleWindow(int Start, int Length)
{
    /// <summary>The index one past the end of the window.</summary>
    public int End => Start + Length;
}

/// <summary>Reads the three record forms, so each declaration has a use.</summary>
public static class RecordUses
{
    /// <summary>Builds one of each and returns their descriptions.</summary>
    public static IReadOnlyList<Str> Describe()
    {
        var reading = new Reading("depth", 12.5);
        var sample = new Sample(3, 4.5);
        var window = new SampleWindow(0, 8);

        return
        [
            reading.ToString(),
            $"{sample.Index}:{sample.Scaled}",
            $"{window.Start}..{window.End}",
        ];
    }
}
