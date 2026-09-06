using System;

namespace Surface.Locals.Parameters;

/// <summary>
/// M13 — three parameters Roslyn names <c>this</c>, and the two layers disagree about how
/// many there are.
/// </summary>
/// <remarks>
/// <para>
/// <c>this</c> is a keyword, and <c>@this</c> is a verbatim identifier that is legal
/// wherever an identifier is: <c>IParameterSymbol.Name</c> for such a parameter is
/// <c>"this"</c>, with no <c>@</c>. The receiver of an extension method is also an
/// <c>IParameterSymbol</c>, and naming <i>it</i> <c>@this</c> — which reads naturally and
/// is common in real code — gives a second symbol with that name and
/// <c>IsThis</c> set.
/// </para>
/// <para>
/// <b><c>csharp.Parameter</c> keys them apart on <c>isThis</c>, and only on that.</b> The
/// three declarations here differ in nothing else the key holds: same name, same type
/// <c>string</c>, same <c>refKind</c>, not <c>params</c>, not optional. So the predicate
/// holds two rows — one <c>isThis = true</c>, one <c>isThis = false</c> — and the
/// <c>false</c> one is shared by <see cref="LocalsThisNamed.Take"/>'s parameter and the
/// lambda's, which is M11 again on a name that makes it hard to see.
/// </para>
/// <para>
/// <b>The SCIP layer separates two of the three and gives the third nothing.</b>
/// <c>ScipSymbols.Descriptor</c> spells a parameter <c>(name)</c>, so the two named
/// methods' parameters end <c>…Measure().(this)</c> and <c>…Take().(this)</c> — the same
/// descriptor tail under different method prefixes — while the lambda's parameter reaches
/// <c>HasGlobalName</c> through a symbol whose <c>Name.Length</c> is 0 and gets no symbol
/// at all. Which means the descriptor tail <c>(this)</c> is ambiguous in principle and
/// saved here only by the methods having different names: two overloads of one name, one
/// of them an extension method with a <c>@this</c> receiver, would spell one string.
/// </para>
/// <para>
/// <b>And the keyword itself is in no row anywhere.</b> <c>this</c> parses as a
/// <c>ThisExpressionSyntax</c>, which is not a <c>SimpleNameSyntax</c>, so
/// <c>Indexer.IndexTree</c>'s reference arm never sees it: <see cref="LocalsThisNamed.Sum"/>
/// writes a reference row for <c>Held</c> and none for the <c>this</c> it is reached
/// through. The count of reference rows over a <c>this</c> token, corpus-wide, is nought.
/// </para>
/// </remarks>
public static class LocalsThisExtension
{
    /// <summary>
    /// Declaration 1: an extension method receiver named <c>@this</c>, so
    /// <c>Name == "this"</c> and <c>IsThis</c> is true.
    /// </summary>
    /// <param name="this">The receiver, named the way real extension methods name it.</param>
    public static int Measure(this string @this) => @this.Length;
}

/// <summary>M13 — the two <c>isThis = false</c> declarations, and the keyword.</summary>
public sealed class LocalsThisNamed
{
    /// <summary>Something for the implicit receiver to reach.</summary>
    public int Held => 1;

    /// <summary>
    /// Declaration 2: an ordinary parameter named <c>@this</c> on an instance method.
    /// </summary>
    /// <param name="this">A parameter whose <c>Name</c> is <c>this</c>.</param>
    public int Take(string @this) => @this.Length;

    /// <summary>
    /// Declaration 3: a lambda parameter named <c>@this</c>, which fuses with declaration
    /// 2 and has no symbol of its own.
    /// </summary>
    public Func<string, int> Lambda() => (string @this) => @this.Length;

    /// <summary>
    /// The keyword, in the one position where it is an expression: no reference row.
    /// </summary>
    public int Sum() => this.Held + Take("ab") + Lambda()("cde") + "fghi".Measure();
}
