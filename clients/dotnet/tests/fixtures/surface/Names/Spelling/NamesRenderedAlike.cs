namespace Surface.Names.Spelling;

/// <summary>
/// M15 — two names that render identically, spelled with different bytes.
/// </summary>
/// <remarks>
/// <para>
/// ECMA-334 draft-v9 6.4.3 says an identifier is compared after conversion to Unicode
/// normalization form C, and Roslyn does not do it: the two members below are two
/// <c>IFieldSymbol</c>s with two different <c>Name</c> strings, and the compiler accepts
/// both in one type. So there is no CS0102 here and nothing in the walk ever sees a
/// normalised form — <c>ScipSymbols.Name</c> backtick-escapes whichever bytes it was
/// handed, and the two descriptors differ inside the backticks.
/// </para>
/// <para>
/// <b>The consequence is entirely in the search layer.</b> Both declarations produce a
/// <c>codemarkup.SearchEntry</c> and a <c>codemarkup.SymbolByName</c>, keyed on
/// <c>name</c> and <c>nameLowercase</c>, and <c>ToLowerInvariant</c> normalises case and
/// not composition — so the two rows sort apart, render the same, and a person who types
/// one form finds one of the two declarations and has no way to learn the other exists.
/// Nothing conflicts and nothing is lost: the index is exactly as wrong as the keyboard
/// that typed the query.
/// </para>
/// <para>
/// The first field is <c>Café</c> as U+0043 U+0061 U+0066 U+00E9 (composed). The second is
/// <c>Café</c> as U+0043 U+0061 U+0066 U+0065 U+0301 (decomposed). Every other name in
/// this file is ASCII, so a byte-level diff of the file names the two lines exactly.
/// </para>
/// </remarks>
public static class NamesRenderedAlike
{
    /// <summary>Composed: the acute accent is part of the letter.</summary>
    public const string Café = "precomposed";

    /// <summary>Decomposed: the acute accent is a combining mark of its own.</summary>
    public const string Café = "decomposed";

    /// <summary>Both constants, in the order the file declares them.</summary>
    public static string[] Both() => [Café, Café];
}

/// <summary>M15 — the same collision one level up, on a type name (composed).</summary>
/// <remarks>
/// A type rather than a member, because <c>codemarkup.Definition</c>'s <c>qualified</c> is
/// <c>ToDisplayString()</c> and <c>SymbolInfo</c>'s <c>signature</c> is a hover string:
/// both come out rendering as <c>Surface.Names.Spelling.NamesCafé</c> for this type and for
/// its decomposed twin, so the two hover cards are indistinguishable while the two keys
/// are not. That is the shape a de-duplicating consumer gets wrong.
/// </remarks>
public sealed class NamesCafé
{
    /// <summary>Which spelling this is, for a reader with a hex editor.</summary>
    public string Form => "composed";
}

/// <summary>M15 — the same collision one level up, on a type name (decomposed).</summary>
public sealed class NamesCafé
{
    /// <summary>Which spelling this is, for a reader with a hex editor.</summary>
    public string Form => "decomposed";
}

/// <summary>M15 — a use site for each spelling, so each declaration has a reference.</summary>
public static class NamesRenderedAlikeUses
{
    /// <summary>Names the composed type and the composed constant.</summary>
    public static string Composed() => new NamesCafé().Form + NamesRenderedAlike.Café;

    /// <summary>Names the decomposed type and the decomposed constant.</summary>
    public static string Decomposed() => new NamesCafé().Form + NamesRenderedAlike.Café;
}
