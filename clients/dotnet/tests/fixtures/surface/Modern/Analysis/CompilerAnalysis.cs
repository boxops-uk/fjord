namespace Surface.Modern.Analysis;

/// <summary>A source of lengths, used by the definite-assignment witness below.</summary>
public sealed class LengthSource
{
    /// <summary>Creates a source over some text.</summary>
    public LengthSource(string text) => Text = text;

    /// <summary>The text.</summary>
    public string Text { get; }

    /// <summary>Reports the length through an <c>out</c> parameter.</summary>
    public bool TryLength(out int length)
    {
        length = Text.Length;

        return length > 0;
    }
}

/// <summary>
/// C# 11 — Auto-default structs. A struct constructor no longer has to assign every field:
/// the compiler zeroes what the constructor leaves alone, with a warning, where C# 10 made it
/// an error. Nothing about the declaration changes — the fact is only that this compiles.
/// </summary>
public struct PartiallyAssigned
{
    /// <summary>Assigned by the constructor.</summary>
    public int Known;

    /// <summary>Left alone by the constructor, and zeroed for it.</summary>
    public int Unknown;

    /// <summary>C# 11 — a constructor that assigns one of two fields.</summary>
    public PartiallyAssigned(int known) => Known = known;

    /// <summary>The sum, which reads the field the constructor never wrote.</summary>
    public int Total => Known + Unknown;
}

/// <summary>
/// C# 11 — Warning wave 7: CS8981, a type whose name is nothing but lower-case ASCII letters
/// is warned about, because the language reserves that shape for future keywords. The
/// declaration is legal and indexable; the warning is the only trace of the rule.
/// </summary>
internal sealed class waveseven
{
    /// <summary>Something to read, so the type is not empty.</summary>
    public int Ordinal => 7;
}

/// <summary>
/// The compiler analyses that changed after C# 9 without changing what any declaration looks
/// like. Each method here is a witness that *compiles*, and that is the entire claim — an
/// index holds the same facts about these bodies as it would about any others.
/// </summary>
public static class CompilerAnalysis
{
    /// <summary>
    /// C# 10 — Improved definite assignment. <c>len</c> is assigned only inside the
    /// <c>TryLength</c> call, which is reached through a conditional access whose result is
    /// coalesced. C# 9 reported <c>len</c> as unassigned at the read below; C# 10's analysis
    /// follows the <c>?.</c> and the <c>??</c> and does not.
    /// </summary>
    public static int Improved(LengthSource? source)
    {
        int length;

        if (source?.TryLength(out length) ?? false)
        {
            return length;
        }

        return 0;
    }

    /// <summary>
    /// C# 10 — the same improvement through a <c>&amp;&amp;</c> over a conditional access,
    /// which is the other shape the feature covers.
    /// </summary>
    public static int ImprovedAnd(LengthSource? source)
    {
        int length;

        if (source is not null && source.TryLength(out length) && length > 2)
        {
            return length;
        }

        return -1;
    }

    /// <summary>C# 11 — auto-defaulting, read through the struct above.</summary>
    public static int AutoDefaulted() => new PartiallyAssigned(3).Total;

    /// <summary>
    /// C# 11 — Improved method group conversion to delegate. The two conversions below may
    /// now produce the *same* delegate object, where C# 10 guaranteed two. Nothing in source
    /// says which happened, and no member declaration differs either way — which is why this
    /// witness can only be a witness.
    /// </summary>
    public static bool MethodGroupConversionsMayBeCached()
    {
        Func<LengthSource, int> first = Improved;
        Func<LengthSource, int> second = Improved;

        return ReferenceEquals(first, second);
    }

    /// <summary>Reads the lower-cased type, so warning wave 7's subject has a use.</summary>
    public static int WaveSeven() => new waveseven().Ordinal;
}
