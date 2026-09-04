namespace Surface.Names.Keywords;

/// <summary>
/// M14 — the two <c>var</c> tokens whose inferred type is not an ordinary named type.
/// </summary>
/// <remarks>
/// Kept out of <c>Spelling/NamesInferredSpelling.cs</c> on purpose. That file and
/// <c>Spelling/NamesPredefinedSpelling.cs</c> are a matched pair whose only difference is
/// how the locals are spelled, and the reference-count inversion between them is a
/// measurement — one extra member on either side would make the number a story instead.
/// </remarks>
public static class NamesInferredEdges
{
    /// <summary>
    /// M14 — a <c>var</c> whose inferred type is an anonymous type, which has no global
    /// name at all.
    /// </summary>
    /// <remarks>
    /// <c>ScipSymbols.HasGlobalName</c> rejects a symbol whose <c>Name.Length</c> is 0 and
    /// an anonymous type's name is empty, so <c>Of</c> returns null and this <c>var</c>
    /// writes no <c>codemarkup</c> row. The <c>csharp</c> layer still answers: the entity
    /// layer expresses the anonymous type or counts it inexpressible, and either way the
    /// two layers disagree about whether there is anything at this span.
    /// </remarks>
    public static int Anonymous()
    {
        var pair = new { Left = 1, Right = 2 };

        return pair.Left + pair.Right;
    }

    /// <summary>
    /// M14 — a <c>var</c> in a <c>foreach</c>, where the inferred type comes from a
    /// pattern the walk never sees.
    /// </summary>
    public static int Iterated(int[] source)
    {
        var total = 0;

        foreach (var item in source)
        {
            total += item;
        }

        return total;
    }
}
