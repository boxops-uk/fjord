namespace Surface.Quarantine.FileLocal;

/// <summary>
/// M5 — the second file-local type of the same name, whose <c>Measure</c> has a different
/// signature.
/// </summary>
/// <remarks>
/// C# 11 file-local types. This declaration is legal precisely because the other
/// <c>FileLocalHelper</c> is <c>file</c>-scoped: within one file the pair would be
/// CS9071, across two files it is conforming. Its <c>MetadataName</c> carries a different
/// per-file prefix from <c>FileLocalFirst.cs</c>'s; nothing the descriptor reads does.
/// </remarks>
file class FileLocalHelper
{
    /// <summary>Takes a <c>string</c>. The lone <c>Measure</c> of this file's type.</summary>
    /// <param name="a">The measured value.</param>
    public int Measure(string a) => a.Length;
}

/// <summary>M5, reference side — a use from the second file, which binds locally.</summary>
public class FileLocalSecondUse
{
    /// <summary>Calls the file-local <c>Measure</c> visible here.</summary>
    public int Go() => new FileLocalHelper().Measure("xy");
}
