namespace Surface.Bindings.Indexers;

/// <summary>
/// M4, reference face — an <c>ElementAccessExpressionSyntax</c> contains no
/// <c>SimpleNameSyntax</c> for the indexer, so no indexer use anywhere produces a row.
/// </summary>
/// <remarks>
/// <para>
/// An indexer is declared with the keyword <c>this</c> and used with brackets; neither is a
/// name. Measured: <c>box[1]</c>, <c>box[^1]</c> and <c>box[2] = 3</c> each contain exactly
/// one child <c>SimpleNameSyntax</c> and it is the receiver <c>box</c>. So the walk
/// dispatches the receiver, writes a reference row for the parameter, and writes nothing at
/// all for <see cref="BindBox.this[int]"/> — which does have a <c>codemarkup.Definition</c>, a
/// <c>SearchEntry</c>, a <c>SymbolInfo</c> and a <c>csharp.Property {isIndexer = true}</c>
/// row, and an empty <c>csharp.EntityRef</c> fan-out. Defined everywhere, referenced
/// nowhere.
/// </para>
/// <para>
/// <b>Which makes the write invisible as well as the read.</b> <c>box[2] = 3</c> is a
/// property write, and there is no row to carry a role — so M9's wrong role and this
/// mechanism's missing row are the two halves of "the index cannot say who writes what":
/// one files the write under the wrong value, the other files it under no value.
/// </para>
/// <para>
/// <b>The identity half of M4 is quarantined and is not here.</b> Roslyn's <c>Name</c> for
/// an indexer is <c>this[]</c>, and <c>ScipSymbols.Name</c> backtick-escapes it because it
/// is not a simple identifier — so the descriptor is <c>`this[]`.</c>, measured, and a
/// gate written against <c>Item.</c> (the metadata name, which the accessors carry) would
/// match nothing. It is a <i>term</i> descriptor, and terms get no disambiguator: two
/// indexers in one type are two declarations spelling one string, and two
/// <c>codemarkup.Definition</c> writes at one key in one file is a refused write that
/// fails the write stream. <b>This project declares exactly one indexer, in this file,
/// deliberately</b>, so that everything else in it can be measured. The accompanying
/// prediction, for the quarantine project to check: the accessors <i>are</i> separable
/// where their properties are not, because <c>get_Item</c> and <c>set_Item</c> are methods
/// and get an ordinal — and both are already visible in this project, since
/// <c>_slots[index]</c> inside each accessor mints
/// <c>…/BindBox#get_Item().(index)</c> and <c>…/BindBox#set_Item().(value)</c>, symbols
/// whose accessor is reached by no <c>Declare</c> and which therefore join to no
/// definition row at all. That last is M18's mechanism, showing up here.
/// </para>
/// <para>
/// <b>The controls are in the same file on purpose.</b> <see cref="Named"/> reaches
/// <see cref="BindBox.Length"/> through a <c>.</c> and writes a row; <see cref="Read"/>
/// reaches the indexer through brackets and does not. The two members are declared side by
/// side, are used side by side, and differ only in the syntax that reaches them.
/// </para>
/// </remarks>
public sealed class BindBox
{
    private readonly int[] _slots = new int[4];

    /// <summary>
    /// The one indexer in this project. Also half of the <c>^1</c> pattern, with
    /// <c>Length</c>.
    /// </summary>
    /// <param name="index">Which slot.</param>
    public int this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value;
    }

    /// <summary>
    /// The control: an ordinary property, reached by name, and the other half of the
    /// <c>^1</c> pattern — which reaches it without naming it.
    /// </summary>
    public int Length => _slots.Length;
}

/// <summary>M4 — every way to reach an indexer, and the named control beside them.</summary>
public static class BindElementAccess
{
    /// <summary>Two reads through brackets, one of them with an index-from-end.</summary>
    /// <param name="box">The box.</param>
    public static int Read(BindBox box) => box[1] + box[^1];

    /// <summary>A write through brackets, which carries no role because it carries no row.</summary>
    /// <param name="box">The box.</param>
    public static void Write(BindBox box) => box[2] = 3;

    /// <summary>The control: the same type, a member reached by name.</summary>
    /// <param name="box">The box.</param>
    public static int Named(BindBox box) => box.Length;
}
