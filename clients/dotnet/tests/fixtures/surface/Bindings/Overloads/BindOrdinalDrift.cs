namespace Surface.Bindings.Overloads;

/// <summary>
/// M2, reference face — an overload ordinal counted on the raw bound symbol, so a use on a
/// constructed receiver spells a sibling overload's name.
/// </summary>
/// <remarks>
/// <para>
/// <c>ScipSymbols.Disambiguator</c> answers "which of my same-named siblings am I" by
/// sorting <c>ContainingType.GetMembers()</c> on
/// <c>GetDocumentationCommentId()</c> and taking <c>FindIndex</c>. Sorting rather than
/// taking declaration order is deliberate and right: <c>GetMembers()</c> for a partial
/// class follows the order the compiler was handed the files, so declaration order is not a
/// property of the code. What is not right is <i>which symbol it is asked about</i>.
/// <c>Indexer.Reference</c> hands <c>ScipSymbols.Of</c> the symbol
/// <c>GetSymbolInfo</c> bound — for <c>pair.Describe(1)</c> that is
/// <c>BindPair&lt;int&gt;.Describe(int)</c>, a member of the <i>constructed</i> type — while the
/// declaration was walked as a member of <c>BindPair&lt;TItem&gt;</c>. The two types have
/// different member lists, so the sort produces different ordinals, and the reference names
/// a declaration that is not the one it bound to.
/// </para>
/// <para>
/// <b>The inversion here is exact, and it is caused by one byte.</b> On the declaration
/// side the ids are <c>M:…BindPair`1.Describe(`0)</c> and
/// <c>M:…BindPair`1.Describe(System.String)</c>; the backtick that stands for a type parameter is
/// U+0060 and <c>S</c> is U+0053, so <b><c>Describe(string)</c> sorts first and is spelled
/// <c>Describe().</c></b> while <c>Describe(TItem)</c> is spelled <c>Describe(+1).</c>. On the use side the type
/// argument is substituted, the ids compare as <c>Int32</c> against <c>String</c>, and
/// <b><c>Describe(int)</c> sorts first</b>. So <c>pair.Describe(1)</c> — which bound to
/// <c>Describe(TItem)</c> — is filed under <c>Describe().</c>, the string minted for the declaration of
/// <c>Describe(string)</c>, and <c>pair.Describe("a")</c> is filed under <c>Describe(+1).</c>. Each use points at
/// the other declaration. Find-references on either overload answers the other's call site,
/// with no marker of any kind.
/// </para>
/// <para>
/// <b>The <c>csharp</c> layer at the same span is right, and that is the gate.</b>
/// <c>CsharpEntities.Canonical</c> applies <c>OriginalDefinition</c> before building the
/// key, so <c>csharp.EntityXRef</c> at these two spans names the two declarations
/// correctly. One query over the indexed corpus decides it: for every span, the entity in
/// <c>csharp.EntityXRef.target</c> and the entity reached through
/// <c>codemarkup.FileXRef.target → csharp.DefinitionBySymbol</c> must be the same row.
/// Here they are not, at two spans, in opposite directions.
/// </para>
/// <para>
/// <b>Silent, not fatal, and the asymmetry matters.</b> Only <c>Declare</c> calls
/// <c>Markup</c>, so only declarations write <c>codemarkup.SymbolInfo</c> — the one
/// predicate whose key is the symbol alone and whose value would disagree. A reference
/// writes <c>Symbol</c>, <c>FileXRef</c> and <c>SymbolXRef</c>, all key-only, so a wrong
/// symbol merges into the right symbol's fan-out and the run completes. The declaration
/// face of the same mechanism — a partial method's implementing half, absent from
/// <c>GetMembers()</c>, taking <c>FindIndex</c> to -1 — is a run-killer and lives in a
/// quarantine project.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The type argument whose substitution inverts the sort.</typeparam>
public sealed class BindPair<TItem>
{
    /// <summary>
    /// The generic overload. Spelled <c>Describe(+1).</c> at its declaration, and named by
    /// <c>pair.Describe("a")</c> — a call that does not reach it.
    /// </summary>
    /// <param name="item">Whatever <c>TItem</c> was bound to.</param>
    public string Describe(TItem item) => $"generic:{item}";

    /// <summary>
    /// The string overload. Spelled <c>Describe().</c> at its declaration, and named by
    /// <c>pair.Describe(1)</c> — a call that does not reach it either.
    /// </summary>
    /// <param name="text">The text.</param>
    public string Describe(string text) => $"string:{text}";
}

/// <summary>M2 — the two crossed use sites, and the control beside them.</summary>
public static class BindOrdinalDrift
{
    /// <summary>
    /// The crossed pair: two spans, each filed under the other declaration's symbol.
    /// </summary>
    public static string Crossed()
    {
        var pair = new BindPair<int>();

        return pair.Describe(1) + pair.Describe("a");
    }

    /// <summary>
    /// The control: the same two overloads reached through the <i>unconstructed</i> type,
    /// where <c>GetMembers()</c> is the list the declarations were counted against.
    /// </summary>
    /// <remarks>
    /// A generic type has no unconstructed use site in C# — <c>BindPair&lt;TItem&gt;</c> can
    /// only be named with a type argument — so the closest control is a call from <i>inside</i>
    /// a generic method whose own type parameter is the argument: <c>BindPair&lt;TAny&gt;</c>
    /// is still a constructed type, but its member ids carry a backtick again and sort the
    /// way the declarations did. So this method's two spans are <i>correct</i> and
    /// <see cref="Crossed"/>'s two are not, with no difference between them a reader would
    /// point at. That is what makes the defect expensive: it depends on the type argument.
    /// </remarks>
    /// <typeparam name="TAny">Left as a type parameter on purpose.</typeparam>
    /// <param name="item">Something to pass to the generic overload.</param>
    public static string Uncrossed<TAny>(TAny item)
    {
        var pair = new BindPair<TAny>();

        return pair.Describe(item) + pair.Describe("a");
    }
}
