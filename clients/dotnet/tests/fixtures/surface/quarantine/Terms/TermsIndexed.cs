namespace Surface.Quarantine.Terms;

/// <summary>
/// M4 — three indexers in one type. The run-killer this project is quarantined for.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 15.9.1 and 15.9.2 (indexers and indexer overloading), 12.8.12.4 (element
/// access): a type may declare any number of indexers so long as their parameter lists
/// differ, and none of them has a name of its own. Clause 15.9.2's own wording is that an
/// indexer's signature consists of the number and types of its formal parameters — the
/// name contributes nothing, because there is no name.
/// </para>
/// <para>
/// <c>ISymbol.Name</c> for an indexer is the literal string <c>this[]</c> (its
/// <c>MetadataName</c> is <c>Item</c>), and <c>ScipSymbols.Descriptor</c>'s
/// Property/Field/Event arm is <c>text.Append(Name(symbol)).Append('.')</c> — no
/// parameter list, and no ordinal, because the ordinal from <c>Disambiguator</c> exists
/// only on the Method arm. All three declarations below therefore spell one
/// backtick-escaped descriptor, <c>…/TermsIndexed#`this[]`.</c>, at three distinct spans
/// in one file.
/// </para>
/// <para>
/// <b>Why the run dies.</b> <c>Definition</c> is <c>{symbol, file} -&gt; {span, kind,
/// name, qualified}</c>, so three spans under one key is a same-key-different-value
/// refusal on the second write; <c>SymbolInfo</c> is <c>{symbol} -&gt; {signature, doc,
/// modifiers}</c>, and the three <c>Hover</c> signatures differ, so it would refuse even
/// with the three indexers spread over three files. <c>Indexer.NameLocation</c> gives each
/// declaration its own <c>ThisKeyword</c> token, which is what makes the three spans
/// distinct rather than identical — an identical value would have deduped in silence.
/// </para>
/// <para>
/// <b>The inversion worth gating.</b> The accessors these properties own <i>are</i>
/// ordinal-separated, because they reach the Method arm: <c>get_Item()</c>,
/// <c>get_Item(+1)</c>, <c>get_Item(+2)</c>. So the getters are distinguishable exactly
/// where the properties owning them are not, and adding a fourth indexer renumbers the
/// existing three.
/// </para>
/// </remarks>
public class TermsIndexed
{
    /// <summary>Indexed by one <c>int</c>. Clause 15.9.1.</summary>
    /// <param name="i">The only parameter.</param>
    public int this[int i] => i;

    /// <summary>Indexed by one <c>string</c> — a different parameter type, same descriptor.</summary>
    /// <param name="s">The only parameter.</param>
    public int this[string s] => s.Length;

    /// <summary>Indexed by two <c>int</c>s — a different parameter count, same descriptor.</summary>
    /// <param name="i">The first parameter.</param>
    /// <param name="j">The second parameter.</param>
    public int this[int i, int j] => i + j;

    /// <summary>
    /// M4, reference side — three element accesses with no name token between them.
    /// </summary>
    /// <remarks>
    /// An <c>ElementAccessExpressionSyntax</c> carries no <c>SimpleNameSyntax</c>, so the
    /// reference walk reaches these three uses through no name node and, when it does
    /// reach them, files all three under the one merged descriptor. Clause 12.8.12.4.
    /// </remarks>
    public int Use() => this[1] + this["k"] + this[1, 2];
}
