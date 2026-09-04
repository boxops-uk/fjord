using System;

namespace Surface.Quarantine.Partial;

/// <summary>
/// M3 — every part of a partial declaration is walked, so one symbol takes two spans.
/// The run-killer this project is quarantined for, in its one-file form.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 15.2.7 (partial type declarations), 15.6.9 (partial methods), 15.5.6.1 and
/// 7.3: a partial type is one type however many parts declare it, and the parts may sit
/// in one file — clause 15.2.7 requires only that they be in one program. A partial
/// member's two halves are likewise one member.
/// </para>
/// <para>
/// <c>Indexer.IndexTree</c> switches on <c>BaseTypeDeclarationSyntax</c>,
/// <c>BaseMethodDeclarationSyntax</c> and <c>BasePropertyDeclarationSyntax</c> and calls
/// <c>Declare</c> once per declaration <i>syntax</i>, while the descriptor path is built
/// from the <i>symbol</i>. Nothing in the path records which part it came from, and the
/// two halves of a partial member are two distinct <c>ISymbol</c>s
/// (<c>Equals</c> is false) that produce byte-identical strings: same <c>Name</c>, same
/// <c>ContainingType</c>, and <c>Disambiguator</c> hands both the bare form because
/// <c>GetMembers()</c> returns only the defining half and <c>index &lt;= 0</c> spells that
/// miss as the first overload.
/// </para>
/// <para>
/// <b>Why the run dies.</b> <c>Definition</c> is keyed <c>{symbol, file}</c> with the span
/// on the value side, so the second write of each pair below is a
/// same-key-different-value refusal. Six pairs are stated here — the type itself and five
/// member kinds — because the parts of the fix differ: a constructor has no member name at
/// all, so <c>`.ctor`</c> plus an ordinal is the whole identity; a property and an event
/// have no parameter list either, so there is no ordinal of last resort for any of the
/// three.
/// </para>
/// <para>
/// <b>The residue after an identity fix.</b> <c>Declare</c> also calls
/// <c>Edges(symbol)</c> once per declaration, which writes
/// <c>MethodParameter {method, index}</c> — all key, no value — so those rows coexist
/// rather than conflict and the member ends with two parameters at index 0. That face
/// survives the identity fix and needs its own assertion; it is stated in
/// <see cref="PartialHandler"/>, where the two halves name their parameters differently
/// and the untruth is visible.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> No overload sits beside the partial method here:
/// a partial method beside a same-named sibling is the <c>Ordinal</c> project's
/// mechanism, and putting it here would make the first refusal unattributable.
/// </para>
/// </remarks>
public partial class PartialSplit
{
    /// <summary>The defining half of a partial constructor (C# 14).</summary>
    /// <param name="seed">Named the same in both halves, unlike <see cref="PartialHandler"/>.</param>
    public partial PartialSplit(int seed);

    /// <summary>The defining half of a partial method. Clause 15.6.9.</summary>
    /// <param name="amount">The amount to fold in.</param>
    partial void Update(int amount);

    /// <summary>The defining half of a partial property (C# 13). Clause 15.7.1.</summary>
    public partial string Label { get; set; }

    /// <summary>The defining half of a partial indexer (C# 13). Clause 15.9.1.</summary>
    /// <param name="i">The position.</param>
    public partial int this[int i] { get; }

    /// <summary>The defining half of a partial event (C# 14). Clause 15.8.1.</summary>
    public partial event EventHandler Changed;
}

/// <summary>
/// M3 — the second part of the same type, in the same file: the pair of spans that
/// <c>Definition {symbol, file}</c> cannot hold.
/// </summary>
/// <remarks>
/// The cross-file partial <i>type</i> is not a break at all — <c>codemarkup</c>'s own
/// schema comment blesses a symbol declared in two files as two facts, and
/// <see cref="PartialHandler"/> is that contrast case. It is two parts in <i>one</i> file
/// that puts two spans under one key, and nothing gates that today.
/// </remarks>
public partial class PartialSplit
{
    private string label = string.Empty;

    private EventHandler changed;

    private int total;

    /// <summary>The implementing half of the constructor. Two spans, one <c>`.ctor`().</c>.</summary>
    /// <param name="seed">The initial total.</param>
    public partial PartialSplit(int seed)
    {
        this.total = seed;
    }

    /// <summary>The implementing half of the method. Two spans, one <c>Update().</c>.</summary>
    /// <param name="amount">The amount to fold in.</param>
    partial void Update(int amount)
    {
        this.total += amount;
    }

    /// <summary>The implementing half of the property. Two spans, one <c>Label.</c>.</summary>
    public partial string Label
    {
        get => this.label;
        set => this.label = value;
    }

    /// <summary>The implementing half of the indexer. Two spans, one <c>`this[]`.</c>.</summary>
    /// <param name="i">The position.</param>
    public partial int this[int i] => this.total + i;

    /// <summary>
    /// The implementing half of the event, whose two halves are two different node kinds.
    /// </summary>
    /// <remarks>
    /// The defining half is a <c>variable_declarator</c> inside an
    /// <c>EventFieldDeclarationSyntax</c>; the implementing half is an
    /// <c>EventDeclarationSyntax</c>. Both spell <c>…/PartialSplit#Changed.</c>, and
    /// neither reaches the markup layer at all, because an event has no entity — so this
    /// pair is the one that fails <i>silently</i> where the other five refuse.
    /// </remarks>
    public partial event EventHandler Changed
    {
        add => this.changed += value;
        remove => this.changed -= value;
    }

    /// <summary>Reads the fields so nothing here is dead. Clause 12.5.1.</summary>
    public int Total => this.total;
}
