namespace Surface.CrossProject;

using Surface.Types;

/// <summary>
/// Uses declarations that live in another project, so the corpus holds a reference whose
/// definition the walk reaches through a compilation reference rather than through a syntax
/// tree of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one project of the corpus has a `ProjectReference`.</b> Two edges of the build graph
/// — <c>msbuild.ProjectReference</c> and <c>msbuild.ProjectReferencedBy</c> — are written only
/// where one project names another, and every other project here stands alone. More than the
/// edges, though, a cross-project use is a different path through the producer: the bound symbol
/// comes from a <c>MetadataReference</c> or a referenced compilation, not from this project's
/// trees, and it is the path on which a symbol string minted here has to equal the one minted
/// where the declaration was walked. That equality is the whole promise of a cross-database name,
/// and a corpus of 27 mutually-blind projects never tests it.
/// </para>
/// <para>
/// The types used are generic, so what crosses the boundary is a <i>constructed</i> type whose
/// definition is elsewhere — the shape whose reference spelling has already been found to differ
/// from its declaration's.
/// </para>
/// </remarks>
public static class AcrossTheBoundary
{
    /// <summary>A constructed generic whose definition is in the `Types` project.</summary>
    public static TyPair<string, int> Constructed() => new() { First = "left", Second = 2 };

    /// <summary>Reads a field declared in another project, through a constructed receiver.</summary>
    public static string? FirstOf(TyPair<string, int> pair) => pair.First;

    /// <summary>A second instantiation, so the definition is reached at two substitutions.</summary>
    public static TyPair<int, int> Both() => new() { First = 1, Second = 1 };
}
