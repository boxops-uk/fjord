namespace Surface.Locals.Query;

/// <summary>
/// M23 — a query range variable is the one file-local form that resolves in neither
/// direction.
/// </summary>
/// <remarks>
/// <para>
/// <c>Indexer.Reference</c> drops <c>SymbolKind.RangeVariable</c> in the same early return
/// as namespaces, labels, discards and aliases — and that return is <i>above</i> the
/// <c>Declared(symbol, name, offsets)</c> fall-back that answers every other file-local
/// symbol. A local gets a <c>codemarkup.FileLocalXRef</c> pointing at its declarator's
/// span; a lambda parameter gets one; a range variable gets nothing. And the declaring end
/// is unreachable too: <c>from item in source</c> holds <c>item</c> as a
/// <c>SyntaxToken</c> on the <c>FromClauseSyntax</c>, not as a name node, so the walk never
/// dispatches it. Clicking <c>item</c> at either end of <c>from item in source select
/// item</c> resolves to nothing, in both directions.
/// </para>
/// <para>
/// <b>And the methods the query calls are referenced by nothing.</b> A query expression is
/// translated after parsing: there is no <c>InvocationExpressionSyntax</c> for the
/// <c>Where</c> and <c>Select</c> calls it becomes, so <c>Invoked</c> never runs and no
/// <c>csharp.MethodInvocationLocation</c> is written; there is no
/// <c>SimpleNameSyntax</c> spelling <c>Select</c>, so no reference row is written either.
/// <see cref="LocalsQuerySource{TItem}.Select"/> is declared in this project, called three
/// times from this file, and has an empty <c>csharp.EntityRef</c> fan-out.
/// </para>
/// <para>
/// <b>What <i>is</i> written over a query is the assertion's other half.</b> The source
/// parameter is an ordinary reference, the type names in the signatures are ordinary
/// references, and the transparent identifier a <c>let</c> introduces is invisible in both
/// layers. So a query expression's contribution to the reference index is its inputs and
/// nothing about its own machinery.
/// </para>
/// </remarks>
public static class LocalsRangeVariables
{
    /// <summary>
    /// The minimal shape: one range variable, declared and used, and no row for either.
    /// </summary>
    /// <remarks>
    /// Every identifier in the body is a range variable except <c>source</c>, so this
    /// method's reference rows are: <c>source</c>, and the two type names in its own
    /// signature. Nothing for <c>item</c>, at either end.
    /// </remarks>
    /// <param name="source">The query's input.</param>
    public static LocalsQuerySource<int> Echoed(LocalsQuerySource<int> source) =>
        from item in source
        select item;

    /// <summary>
    /// Three clauses, two range variables, one transparent identifier: still no row for any
    /// of them.
    /// </summary>
    /// <remarks>
    /// <c>let</c> lowers to a <c>Select</c> onto an anonymous type holding <c>item</c> and
    /// <c>doubled</c>, and a second <c>Select</c> reads the field back out — so this one
    /// query is three calls into <see cref="LocalsQuerySource{TItem}"/> and contributes
    /// three uses that no predicate records.
    /// </remarks>
    /// <param name="source">The query's input.</param>
    public static LocalsQuerySource<int> Doubled(LocalsQuerySource<int> source) =>
        from item in source
        where item > 1
        let doubled = item * 2
        select doubled;

    /// <summary>
    /// The control: the same translation written by hand, where every call <i>is</i> a row.
    /// </summary>
    /// <remarks>
    /// Line for line the lowering of <see cref="Doubled"/>, so the two methods are a
    /// matched pair: this one writes a <c>csharp.MethodInvocationLocation</c> and a
    /// reference row for <c>Where</c> and for each <c>Select</c>, and the lambda parameters
    /// get <c>FileLocalXRef</c> rows where the range variables got none. The difference
    /// between the two methods' row counts is the whole of M23, measured rather than
    /// argued.
    /// </remarks>
    /// <param name="source">The query's input.</param>
    public static LocalsQuerySource<int> DoubledByHand(LocalsQuerySource<int> source) =>
        source
            .Where(item => item > 1)
            .Select(item => new { Item = item, Doubled = item * 2 })
            .Select(pair => pair.Doubled);

    /// <summary>Runs all three, so none of the queries is dead.</summary>
    public static int Use()
    {
        var source = new LocalsQuerySource<int>([1, 2, 3]);

        return Echoed(source).Items.Length
            + Doubled(source).Items.Length
            + DoubledByHand(source).Items.Length;
    }
}
