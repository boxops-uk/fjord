using System;

namespace Surface.Preview;

/// <summary>
/// C# 13 — The field keyword (preview in 13). This is the shape the feature shipped as a
/// preview for: a property whose two accessors both have bodies and reach the
/// compiler-synthesized backing field by the contextual keyword <c>field</c>. Before it, the
/// same property needed a field declared by hand, and that field was a *member* — nameable,
/// indexable, referable. Here it is none of those, and the accessors still read and write it.
/// </summary>
public sealed class PreviewFielded
{
    /// <summary>C# 13 — <c>field</c> in both accessors of one property.</summary>
    public string Label
    {
        get => field ?? "unset";
        set => field = value?.Trim() ?? string.Empty;
    }

    /// <summary>C# 13 — <c>field</c> in a property with an <c>init</c> accessor.</summary>
    public string Slug
    {
        get => field ?? string.Empty;
        init => field = value.ToLowerInvariant();
    }

    /// <summary>C# 13 — <c>field</c> used twice in one accessor body.</summary>
    public int Ordinal
    {
        get => field;
        set => field = field > value ? field : value;
    }

    /// <summary>
    /// C# 13 — the hand-written equivalent, for contrast: this property's backing store is a
    /// member and the two above have none.
    /// </summary>
    public string Manual
    {
        get => _manual;
        set => _manual = value.Trim();
    }

    private string _manual = string.Empty;
}

/// <summary>
/// C# 13 — the breaking change the preview flagged: a *member* named <c>field</c>. Inside an
/// accessor body <c>field</c> now means the synthesized backing field, so the member has to be
/// reached as <c>@field</c> or through <c>this</c>. Two different variables, one spelling.
/// </summary>
public sealed class ShadowedField
{
    private int field = 7;

    /// <summary>The synthesized backing field, reached by the keyword.</summary>
    public int Synthesized
    {
        get => field;
        set => field = value;
    }

    /// <summary>The declared member, reached by its escaped name from inside an accessor.</summary>
    public int Declared
    {
        get => @field;
        set => @field = value;
    }

    /// <summary>The declared member, reached through <c>this</c> — the other escape.</summary>
    public int ThroughThis => this.field;

    /// <summary>Outside an accessor, <c>field</c> is an ordinary identifier again.</summary>
    public int Sum() => field + Synthesized;
}
