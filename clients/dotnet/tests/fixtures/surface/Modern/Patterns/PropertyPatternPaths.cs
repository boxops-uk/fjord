namespace Surface.Modern.Patterns;

/// <summary>The innermost of three nested shapes the patterns below walk into.</summary>
public sealed class Datum
{
    /// <summary>The reading.</summary>
    public double Value { get; init; }

    /// <summary>How the reading is spelled.</summary>
    public string Unit { get; init; } = "m";
}

/// <summary>The middle shape.</summary>
public sealed class Channel
{
    /// <summary>The channel's latest datum.</summary>
    public Datum? Latest { get; init; }

    /// <summary>The channel's name.</summary>
    public string Name { get; init; } = "unnamed";
}

/// <summary>The outer shape.</summary>
public sealed class Instrument
{
    /// <summary>The instrument's primary channel.</summary>
    public Channel? Primary { get; init; }

    /// <summary>How many channels the instrument has.</summary>
    public int ChannelCount { get; init; }
}

/// <summary>
/// C# 10 — Extended property patterns: <c>{ Prop1.Prop2: pattern }</c>. Before C# 10 the same
/// test needed a pattern nested per level (<c>{ Primary: { Latest: { Value: &gt; 1 } } }</c>);
/// the extended form spells the whole path in one subpattern. The interesting fact is that
/// the path's *intermediate* members are referenced by a name that appears in no expression —
/// <c>Primary</c> and <c>Latest</c> below are reads with no dot-completion behind them.
/// </summary>
public static class PropertyPatternPaths
{
    /// <summary>C# 10 — a two-step path inside a property pattern.</summary>
    public static bool NamedDepth(Instrument instrument) =>
        instrument is { Primary.Name: "depth" };

    /// <summary>C# 10 — a three-step path, and a second one in the same pattern.</summary>
    public static bool DeepReading(Instrument instrument) =>
        instrument is { Primary.Latest.Value: > 10.0, Primary.Latest.Unit: "m" };

    /// <summary>C# 10 — an extended path whose leaf subpattern declares a variable.</summary>
    public static bool Capture(Instrument instrument, out double value)
    {
        if (instrument is { Primary.Latest.Value: var captured })
        {
            value = captured;

            return true;
        }

        value = 0;

        return false;
    }

    /// <summary>The pre-C# 10 nesting, for contrast: the same test, spelled the long way.</summary>
    public static bool NestedTheOldWay(Instrument instrument) =>
        instrument is { Primary: { Latest: { Value: > 10.0 } } };

    /// <summary>C# 10 — an extended path in a <c>switch</c> arm rather than an <c>is</c>.</summary>
    public static string Classify(Instrument instrument) => instrument switch
    {
        { Primary.Latest.Unit: "m", ChannelCount: 1 } => "single metre channel",
        { Primary.Latest.Unit: "m" } => "metres",
        { Primary.Name.Length: > 8 } => "long name",
        { Primary: null } => "no primary",
        _ => "other",
    };
}
