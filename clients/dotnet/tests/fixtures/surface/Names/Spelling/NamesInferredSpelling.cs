namespace Surface.Names.Spelling;

/// <summary>
/// M27 and M14 — the inferred half of the matched pair: the same members as
/// <c>NamesPredefinedSpelling</c>, with every local's type spelled <c>var</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>var</c> is parsed as an <c>IdentifierNameSyntax</c>, so it reaches
/// <c>Indexer.IndexTree</c>'s <c>case SimpleNameSyntax</c> arm;
/// <c>SemanticModel.GetSymbolInfo</c> on it answers the <i>inferred</i> type. So each
/// <c>var</c> token below writes a <c>codemarkup.FileXRef {role = typeRef}</c>, a
/// <c>codemarkup.SymbolXRef</c>, a <c>csharp.EntityXRef</c>, a <c>csharp.EntityRef</c> and
/// — because the <c>Definition</c> union's discriminant is 0 for a type — a
/// <c>csharp.TypeLocation</c>, all at the span of three characters that name no type.
/// </para>
/// <para>
/// <b>The wrong answer is on the reverse index, not on this file.</b> Find-references on
/// <c>System.Int32</c> answers every <c>var</c> in the corpus that inferred <c>int</c>,
/// and the spans it returns are over a token whose text is <c>var</c>. There is nothing to
/// click through to and nothing a reader would recognise, and no row is missing or
/// duplicated: the index is complete and says something nobody wrote.
/// </para>
/// <para>
/// <b>Only the locals change.</b> The parameters and return types below are still
/// keywords, because C# has no inferred form for either — so the delta between this file
/// and <c>NamesPredefinedSpelling.cs</c> is exactly the local declarations, and the count
/// is checkable by hand.
/// </para>
/// </remarks>
public static class NamesInferredSpelling
{
    /// <summary>Two <c>var</c> tokens, both inferring <c>System.Int32</c>.</summary>
    public static int Counted()
    {
        var total = 0;
        var step = 3;

        total += step;

        return total;
    }

    /// <summary>Two <c>var</c> tokens, both inferring <c>System.String</c>.</summary>
    public static string Joined()
    {
        var first = "a";
        var second = "b";

        return first + second;
    }

    /// <summary>One <c>var</c> token inferring <c>System.Boolean</c>.</summary>
    public static bool Wider(double weight, object tag)
    {
        var held = weight > 0.0 && tag is not null;

        return held;
    }
}
