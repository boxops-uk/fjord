namespace Surface.Bindings.Overloads;

/// <summary>
/// M2, reference face — the other way the ordinal goes wrong: <c>FindIndex</c> does not
/// find the symbol at all, and <c>index &lt;= 0</c> spells that as "the first overload".
/// </summary>
/// <remarks>
/// <para>
/// <c>ScipSymbols.Disambiguator</c> ends
/// <c>siblings.FindIndex(member =&gt; member.Equals(symbol, SymbolEqualityComparer.Default))</c>
/// followed by <c>index &lt;= 0 ? string.Empty : $"+{index}"</c>. A miss returns -1, which
/// the comparison folds into the same answer as a hit at position 0 — so a symbol that is
/// not in its own containing type's member list is filed under the first-sorting overload's
/// name. Two symbol shapes reach that: a <b>constructed generic method</b>, which does not
/// equal the unconstructed member <c>GetMembers()</c> holds, and a <b>reduced extension
/// method</b>, which does not equal the static declaration.
/// </para>
/// <para>
/// <b>Measured, on this file's shapes.</b> The declarations sort and number as
/// <c>Convert(string)</c> → <c>Convert().</c> and <c>Convert&lt;TValue&gt;(TValue)</c> →
/// <c>Convert(+1).</c>, and <c>Slice(BindWords, int)</c> → <c>Slice().</c> and
/// <c>Slice(BindWords, int, int)</c> → <c>Slice(+1).</c>. Every reduced or constructed use
/// below returns <c>FindIndex = -1</c> and is filed under the bare form. So
/// <see cref="BindConvertUses.Constructed"/> and
/// <see cref="BindSliceUses.ReducedTwoArguments"/> point at declarations they do not call,
/// and <see cref="BindSliceUses.ReducedOneArgument"/> is right by luck — it calls the
/// overload that happened to sort first.
/// </para>
/// <para>
/// <b>The unreduced spelling is the control, and it is the same call.</b>
/// <c>BindSliceExtensions.Slice(words, 1, 2)</c> binds to the static declaration itself,
/// <c>FindIndex</c> finds it at 1, and the reference is correct. Two spellings of one call,
/// two different targets, and nothing in the index marks which is which.
/// </para>
/// <para>
/// <b>A prediction about the fix, not the break.</b> <c>OriginalDefinition</c> alone does
/// not repair the reduced case: on a reduced symbol it stays reduced — Roslyn's
/// <c>OriginalDefinition</c> for <c>words.Slice(1, 2)</c> displays as
/// <c>BindWords.Slice(int, int)</c>, still an instance-shaped method — so
/// <c>ReducedFrom.OriginalDefinition</c> is what is needed, which is exactly what
/// <c>CsharpEntities.Canonical</c> already does one layer down. Which is why
/// <c>csharp.EntityXRef</c> at every span here is correct while
/// <c>codemarkup.FileXRef</c> is not.
/// </para>
/// <para>
/// <b>And the container is right, which narrows the fix.</b> The reduced symbol's
/// <c>ContainingType</c> is <c>BindSliceExtensions</c> and its <c>ContainingSymbol</c>
/// chain runs through it, so the descriptor prefix is correct and only the disambiguator is
/// wrong: the bad symbol joins to a real <c>codemarkup.Definition</c> row, for the wrong
/// declaration. It is a crossed answer, not a missing one, and an anti-join for xref
/// targets with no definition will not see it.
/// </para>
/// </remarks>
public static class BindConvertOverloads
{
    /// <summary>
    /// Sorts first (<c>M:…Convert(System.String)</c>) and is spelled <c>Convert().</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    public static string Convert(string text) => $"text:{text}";

    /// <summary>
    /// Sorts second (<c>M:…Convert``1(``0)</c>, the double backtick being U+0060) and is
    /// spelled <c>Convert(+1).</c>.
    /// </summary>
    /// <typeparam name="TValue">Whatever is being converted.</typeparam>
    /// <param name="value">The value.</param>
    public static string Convert<TValue>(TValue value) => $"value:{value}";
}

/// <summary>M2 — a constructed generic method's use, and the control beside it.</summary>
public static class BindConvertUses
{
    /// <summary>
    /// The miss: the bound symbol is <c>Convert&lt;int&gt;</c>, which is not in
    /// <c>BindConvertOverloads.GetMembers()</c>, so the reference is filed under
    /// <c>Convert().</c> — the string overload's name.
    /// </summary>
    public static string Constructed() => BindConvertOverloads.Convert<int>(1);

    /// <summary>
    /// The same miss with the type argument inferred rather than written, which shows the
    /// defect is about the symbol and not about the syntax.
    /// </summary>
    public static string Inferred() => BindConvertOverloads.Convert(2L);

    /// <summary>
    /// The control: a call that binds to a declaration <c>GetMembers()</c> holds, and is
    /// filed correctly.
    /// </summary>
    public static string Unconstructed() => BindConvertOverloads.Convert("a");
}

/// <summary>The receiver an extension method extends.</summary>
public sealed class BindWords
{
    /// <summary>Wraps a string.</summary>
    /// <param name="text">The text.</param>
    public BindWords(string text) => Text = text;

    /// <summary>The wrapped text.</summary>
    public string Text { get; }
}

/// <summary>Two same-named extension methods, so the ordinal has something to be.</summary>
public static class BindSliceExtensions
{
    /// <summary>
    /// Sorts first and is spelled <c>Slice().</c>, which every reduced call below reaches.
    /// </summary>
    /// <param name="words">The receiver.</param>
    /// <param name="start">Where to start.</param>
    public static string Slice(this BindWords words, int start) => words.Text[start..];

    /// <summary>
    /// Sorts second and is spelled <c>Slice(+1).</c>, which only the unreduced call
    /// reaches.
    /// </summary>
    /// <param name="words">The receiver.</param>
    /// <param name="start">Where to start.</param>
    /// <param name="count">How much.</param>
    public static string Slice(this BindWords words, int start, int count) =>
        words.Text.Substring(start, count);
}

/// <summary>M2 — the reduced and unreduced spellings of one call.</summary>
public static class BindSliceUses
{
    /// <summary>Reduced, and right by luck: it calls the overload that sorts first.</summary>
    /// <param name="words">The receiver.</param>
    public static string ReducedOneArgument(BindWords words) => words.Slice(1);

    /// <summary>
    /// Reduced, and wrong: it calls the two-argument overload and is filed under the
    /// one-argument overload's symbol.
    /// </summary>
    /// <param name="words">The receiver.</param>
    public static string ReducedTwoArguments(BindWords words) => words.Slice(1, 2);

    /// <summary>The control: the same call spelled as the static method it is.</summary>
    /// <param name="words">The receiver.</param>
    public static string Unreduced(BindWords words) =>
        BindSliceExtensions.Slice(words, 1, 2);
}
