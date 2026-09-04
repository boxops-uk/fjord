namespace Surface.Quarantine.FileLocal;

/// <summary>
/// M5 — the ordinary, namespace-scoped type of the same name, beside the two file-local
/// ones.
/// </summary>
/// <remarks>
/// Clause 14.2 (namespace members) and 7.8.3: this is a normal type declaration, visible
/// throughout the namespace, and it coexists with the two <c>file class</c>es because a
/// file-local name is preferred inside its own file and invisible outside it. Three
/// declarations, three distinct types, one descriptor — and the ordinary one is the one a
/// consumer of the index would think it had found.
/// </remarks>
public class FileLocalHelper
{
    /// <summary>Takes a <c>double</c>. The lone <c>Measure</c> of the namespace-scoped type.</summary>
    /// <param name="a">The measured value.</param>
    public int Measure(double a) => (int)a;
}

/// <summary>M5, reference side — a use that binds to the namespace-scoped type.</summary>
public class FileLocalThirdUse
{
    /// <summary>Calls the only <c>Measure</c> visible in a file with no local override.</summary>
    public int Go() => new FileLocalHelper().Measure(1.5);
}
