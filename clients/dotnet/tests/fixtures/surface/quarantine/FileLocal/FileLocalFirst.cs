namespace Surface.Quarantine.FileLocal;

/// <summary>
/// M5 — a file-local type. The run-killer this project is quarantined for, first of the
/// three declarations that mint one identity.
/// </summary>
/// <remarks>
/// <para>
/// C# 11's file-local types (there is no ECMA-334 draft-v9 clause; the nearest are 6.1 on
/// programs, 7.8.3 on declaration spaces and 14.2 on namespace members). <c>file</c>
/// restricts a type's name to its declaring file, so this <c>FileLocalHelper</c> and the
/// one in <c>FileLocalSecond.cs</c> are two unrelated types that may legally coexist —
/// exactly the guarantee the language gives, and exactly the one the identity string
/// cannot express.
/// </para>
/// <para>
/// Roslyn keeps the restriction only in <c>INamedTypeSymbol.MetadataName</c>, as a
/// per-file prefix of the shape <c>&lt;F0&gt;FD8A78B4…__FileLocalHelper</c>.
/// <c>ISymbol.Name</c> is plain <c>FileLocalHelper</c>, <c>ToDisplayString()</c> is plain
/// <c>Surface.Quarantine.FileLocal.FileLocalHelper</c>, and
/// <c>GetDocumentationCommentId()</c> is plain
/// <c>T:Surface.Quarantine.FileLocal.FileLocalHelper</c>. Both
/// <c>ScipSymbols.Name</c> and <c>CsharpEntities.FullName</c> read <c>ISymbol.Name</c>,
/// and the descriptor path has namespace and name segments but no segment for the file —
/// so all three declarations in this project mint one descriptor and reach one
/// <c>csharp.Class</c> key. <c>MetadataName</c>'s prefix is the discriminator a fix
/// borrows.
/// </para>
/// <para>
/// <b>Two severities, which is why the members matter.</b> At <i>type</i> level the merge
/// is silent: the <c>SymbolInfo</c> values agree (one signature
/// <c>FileLocalHelper</c>, one set of modifiers, one absent doc), so the second write
/// dedupes rather than conflicting, and <c>Definition</c>'s keys differ because
/// <c>file</c> is part of the key. One level down it is fatal: <c>Measure</c> is the lone
/// method of its own type in each of the three files, so each takes the bare
/// disambiguator <c>…/FileLocalHelper#Measure().</c>, and the three <c>Hover</c>
/// signatures — <c>int FileLocalHelper.Measure(int a)</c>,
/// <c>…Measure(string a)</c>, <c>…Measure(double a)</c> — are three values under one
/// <c>SymbolInfo {symbol}</c> key. That is the refusal, and it fires across files.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> The plan also names a same-file arity stack —
/// <c>file class C</c> beside <c>file class C&lt;T&gt;</c> — as a face of this mechanism,
/// and it does conflict, on both value-bearing keys. It is not written here: it is an
/// arity pair, it would refuse before the walk ever reached the second file, and the
/// refusal would then be attributable to the <c>Arity</c> project's mechanism rather than
/// to file-locality. The prediction is recorded in this project's README instead.
/// </para>
/// </remarks>
file class FileLocalHelper
{
    /// <summary>Takes an <c>int</c>. The lone <c>Measure</c> of this file's type.</summary>
    /// <param name="a">The measured value.</param>
    public int Measure(int a) => a;
}

/// <summary>
/// M5, reference side — a use of the file-local name from inside its own file.
/// </summary>
/// <remarks>
/// The name binds to <i>this</i> file's <c>FileLocalHelper</c>, which the language
/// guarantees. A cross-reference relation seeked on the symbol declared here should hold
/// this use and nothing from the other two files; with the file segment missing it holds
/// all three uses, so a rename driven off the index would edit code in files that never
/// mentioned this type.
/// </remarks>
public class FileLocalFirstUse
{
    /// <summary>Calls the file-local <c>Measure</c> visible here.</summary>
    public int Go() => new FileLocalHelper().Measure(1);
}
