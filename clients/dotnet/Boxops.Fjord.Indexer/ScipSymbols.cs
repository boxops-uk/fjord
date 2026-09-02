using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>A Roslyn symbol as a SCIP symbol string — what <c>src.Symbol</c> holds.</b>
/// </para>
/// <para>
/// The format is the SCIP specification's: a scheme token, a package coordinate of
/// manager, name and version, then suffix-encoded descriptors. It is the cross-language,
/// cross-database join key, so the two things that matter about it are that it is
/// unambiguous and that two runs over one commit produce the same string.
/// </para>
/// <para>
/// <b>Why this is not <c>scip-dotnet</c>'s output, and why the token says so.</b> The
/// reference C# indexer attaches a namespace descriptor to the package rather than to its
/// parent namespace, so <c>DiffPlex.DiffBuilder.Model.DiffPaneModel</c> comes out as
/// <c>Model/DiffPaneModel#</c> and <c>A.B.T</c> collides with <c>C.B.T</c>; and by default
/// it writes the package of the code it is indexing as <c>. .</c>, which collides across
/// repositories. Both are deliberate for its purpose and neither is usable as a key a
/// fan-out seeks, so this writes conformant symbols under its own scheme token. Claiming
/// <c>scip-dotnet</c> without matching it byte for byte would be worse than either, and
/// neither difference can be repaired downstream — nothing can recover a namespace a
/// producer never wrote.
/// </para>
/// </summary>
internal static class ScipSymbols
{
    /// <summary>
    /// This producer's scheme token. A database says which schemes it holds in
    /// <c>config.Setting {dimension = "symbol-scheme"}</c>, and a cross-database join is
    /// within one scheme.
    /// </summary>
    public const string Scheme = "scip-csharp";

    /// <summary>
    /// The SCIP symbol for <paramref name="symbol"/>, or <see langword="null"/> where it
    /// has no global name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null rather than a synthesised name for anything file-local — a local, a label, a
    /// range variable, or a member declared inside a method body. SCIP models these as
    /// <c>local N</c>, an occurrence ordinal that moves when the file is edited;
    /// <c>codemarkup.FileLocalXRef</c> answers them span to span, so minting a string here
    /// would only create one that has to be invalidated.
    /// </para>
    /// <para>
    /// Null for the global namespace too, which has no descriptor of its own: it is the
    /// root the walk stops at, not a namespace anything is in.
    /// </para>
    /// </remarks>
    public static string? Of(ISymbol symbol)
    {
        if (symbol is INamespaceSymbol { IsGlobalNamespace: true })
        {
            return null;
        }

        var chain = new List<ISymbol>();

        for (var here = symbol; here is not null; here = here.ContainingSymbol)
        {
            if (here is INamespaceSymbol { IsGlobalNamespace: true } or IAssemblySymbol or IModuleSymbol)
            {
                break;
            }

            if (!HasGlobalName(here))
            {
                return null;
            }

            chain.Add(here);
        }

        chain.Reverse();

        var text = new StringBuilder();
        text.Append(Scheme).Append(' ').Append(Package(symbol)).Append(' ');

        foreach (var link in chain)
        {
            Descriptor(text, link);
        }

        return text.ToString();
    }

    /// <summary>
    /// Whether a symbol is one SCIP gives a global name. Everything a method body
    /// introduces is excluded, and so is anything whose kind SCIP has no descriptor for.
    /// </summary>
    private static bool HasGlobalName(ISymbol symbol) =>
        symbol.Kind switch
        {
            SymbolKind.Namespace or SymbolKind.NamedType or SymbolKind.Method
                or SymbolKind.Property or SymbolKind.Field or SymbolKind.Event
                or SymbolKind.Parameter or SymbolKind.TypeParameter => true,
            _ => false,
        };

    /// <summary>
    /// The package coordinate: the containing assembly's own identity, or <c>. .</c> for a
    /// symbol that has no assembly.
    /// </summary>
    /// <remarks>
    /// <b>The assembly names itself even for the code being indexed.</b> The reference
    /// indexer writes <c>. .</c> there to avoid publishing symbols into a namespace shared
    /// by every repository in the world; this schema's `Symbol` is exactly a key for
    /// joining across databases, so an empty coordinate would make the join unsound —
    /// two repositories' <c>Main/Program#</c> are not the same symbol. Which checkout a
    /// database was built from is `config.Setting {dimension = "repo"}`'s answer, and
    /// `src.FileOrigin`'s per file.
    /// </remarks>
    private static string Package(ISymbol symbol)
    {
        var identity = symbol.ContainingAssembly?.Identity;

        return identity is null
            ? "nuget . ."
            : $"nuget {Spaced(identity.Name)} {Spaced(identity.Version.ToString())}";
    }

    /// <summary>
    /// A package field, escaped the way the package coordinate is escaped: a space is
    /// doubled, and nothing else is touched.
    /// </summary>
    /// <remarks>
    /// <b>Not a descriptor name.</b> The three package fields are delimited by single
    /// spaces, so a space inside one is what has to be escaped — where a descriptor is
    /// delimited by its suffix and escapes with backticks. A version like <c>10.0.0.0</c>
    /// is ordinary here and would be mangled by the other rule.
    /// </remarks>
    private static string Spaced(string field) => field.Replace(" ", "  ");

    private static void Descriptor(StringBuilder text, ISymbol symbol)
    {
        switch (symbol.Kind)
        {
            case SymbolKind.Namespace:
                text.Append(Name(symbol)).Append('/');
                break;
            case SymbolKind.NamedType:
                text.Append(Name(symbol)).Append('#');
                break;
            case SymbolKind.Method:
                text.Append(Name(symbol)).Append('(').Append(Disambiguator(symbol)).Append(").");
                break;
            // A property, a field and an event are all terms. The reference indexer files
            // an event under `#`, which reads as a type and is the one divergence here that
            // is a plain disagreement with the grammar rather than a scope decision.
            case SymbolKind.Property:
            case SymbolKind.Field:
            case SymbolKind.Event:
                text.Append(Name(symbol)).Append('.');
                break;
            case SymbolKind.Parameter:
                text.Append('(').Append(Name(symbol)).Append(')');
                break;
            case SymbolKind.TypeParameter:
                text.Append('[').Append(Name(symbol)).Append(']');
                break;
            default:
                throw new ArgumentException($"no SCIP descriptor for {symbol.Kind}", nameof(symbol));
        }
    }

    /// <summary>
    /// Which of its same-named siblings this method is, as SCIP's optional method
    /// disambiguator: empty for the first, <c>+N</c> for the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counted over a sorted order, not over declaration order.</b> The reference
    /// indexer counts <c>ContainingType.GetMembers()</c>, which for a partial class follows
    /// the order the compiler was handed the files — so one commit produces different
    /// symbols depending on how the walk was invoked, and a sealed identity hashes the
    /// facts. Sorting by <c>docId</c> — Roslyn's own signature encoding, which is a total
    /// order over overloads and includes the return type for a conversion operator — makes
    /// the count a property of the code.
    /// </para>
    /// <para>
    /// It is still an ordinal, so **inserting an overload that sorts earlier renumbers the
    /// ones after it**. That is inherent to the format and is why nothing keys on a symbol
    /// across revisions: the language layer's structural entity does that, and carries
    /// `docId` itself.
    /// </para>
    /// </remarks>
    private static string Disambiguator(ISymbol symbol)
    {
        if (symbol.ContainingType is not { } containing)
        {
            return string.Empty;
        }

        var siblings = containing.GetMembers()
            .Where(member => member.Kind == SymbolKind.Method && member.Name == symbol.Name)
            .OrderBy(Signature, StringComparer.Ordinal)
            .ToList();

        var index = siblings.FindIndex(member => member.Equals(symbol, SymbolEqualityComparer.Default));

        return index <= 0 ? string.Empty : $"+{index}";
    }

    /// <summary>The total order overloads are counted in.</summary>
    /// <remarks>
    /// `GetDocumentationCommentId` is null for a few symbols the compiler synthesises, so
    /// the display string is the tie-break rather than the sort collapsing to equality —
    /// two members compared equal would make the ordinal depend on `GetMembers()` again.
    /// </remarks>
    private static string Signature(ISymbol symbol) =>
        symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString();

    /// <summary>
    /// A descriptor's name: as written when it is a simple identifier, backtick-escaped
    /// when it is not, with an embedded backtick doubled.
    /// </summary>
    /// <remarks>
    /// <b>Simple means ASCII</b>, per the grammar — letters, digits, <c>_</c>, <c>+</c>,
    /// <c>-</c> and <c>$</c>. A legal C# identifier outside that set (`Ünïcode`) is escaped
    /// rather than passed through, which is where a `\w`-based test would emit a symbol a
    /// strict parser rejects. `.ctor` is the everyday case: the dot would otherwise
    /// terminate the descriptor.
    /// </remarks>
    private static string Name(ISymbol symbol) => Escaped(symbol.Name);

    private static string Escaped(string name) =>
        IsSimple(name) ? name : $"`{name.Replace("`", "``")}`";

    private static bool IsSimple(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var character in name)
        {
            var simple = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '_' or '+' or '-' or '$';

            if (!simple)
            {
                return false;
            }
        }

        return true;
    }
}
