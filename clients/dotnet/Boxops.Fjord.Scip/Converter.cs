using System.Text;

using Boxops.Fjord.Client;

namespace Boxops.Fjord.Scip;

/// <summary>
/// A SCIP index, transcribed into the surface a UI reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only what the index contains.</b> A SCIP index is occurrences and symbol
/// information; it has no type graph, no build layer and no declaration model, so this
/// writes <c>codemarkup</c> and the source layer and stops. Revision 2 required the
/// converter to synthesise a whole declaration layer for a language it has no compiler
/// for — inventing a `src.Decl` per occurrence — and `codemarkup` removed the need,
/// which is the reduction W13 recorded.
/// </para>
/// <para>
/// <b>Positions are the work.</b> SCIP counts in lines and characters, and the character
/// is UTF-8, UTF-16 or UTF-32 code units depending on what the indexer was written in;
/// this schema counts UTF-8 bytes from the start of the file. So the file's text is
/// needed — from the index where it carries one, and from disk where it does not — and a
/// document with neither is skipped by name rather than placed at offset zero.
/// </para>
/// </remarks>
internal sealed class Converter(FactSink sink, string root, TextWriter log)
{
    /// <summary>Symbols seen, so one `src.Symbol` fact is built per string.</summary>
    private readonly Dictionary<string, FjordFact> _symbols = new(StringComparer.Ordinal);

    /// <summary>What every index this converter writes says about itself.</summary>
    /// <remarks>
    /// <c>position-encoding</c> is this database's, not the index's: the whole point of
    /// the conversion is that a consumer reads one number wherever the facts came from.
    /// </remarks>
    public void Settings(ScipIndex index, string tool)
    {
        void Say(string dimension, string value)
        {
            if (value.Length > 0)
            {
                sink.Add(ScipFacts.Setting, ScipFacts.SettingFact(dimension, value));
            }
        }

        Say("position-encoding", "utf8");
        Say("symbol-scheme", "scip");
        Say("producer", tool);
        Say("style-encoding", Styles.Encoding);
        Say("index-root", index.ProjectRoot);

        foreach (var language in index.Documents
            .Select(document => document.Language)
            .Where(language => language.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            Say("language", language);
        }
    }

    /// <summary>Convert one document, and say how many facts it was worth.</summary>
    public int Convert(Document document)
    {
        if (document.RelativePath.Length == 0)
        {
            return 0;
        }

        if (TextOf(document) is not { } text)
        {
            log.WriteLine($"  ! {document.RelativePath}: no text in the index and none on disk, "
                + "so its occurrences have no byte offsets — skipping it");
            return 0;
        }

        // Read before anything is written, so a document this converter has to refuse
        // contributes no half of itself to the database.
        var named = Named(document);

        var file = ScipFacts.FileFact(document.RelativePath);
        sink.Add(ScipFacts.File, file);

        var lines = Lines.Of(text);
        Emit(file, lines);

        var locals = LocalTargets(document, lines);
        var written = 0;

        foreach (var occurrence in document.Occurrences)
        {
            if (occurrence.Symbol.Length == 0)
            {
                continue;
            }

            var start = lines.Offset(
                occurrence.StartLine, occurrence.StartCharacter, document.PositionEncoding);
            var end = lines.Offset(
                occurrence.EndLine, occurrence.EndCharacter, document.PositionEncoding);
            var length = Math.Max(0, end - start);

            // **A local never reaches `src.Symbol`.** Its id is an occurrence ordinal
            // scoped to this document, so `local 1` here and `local 1` in the next file
            // are different entities — interning the string would make find-references
            // in one file answer the other file's unrelated variable. A local with no
            // definition in this document has no span to point at and is dropped.
            if (Descriptors.IsLocal(occurrence.Symbol))
            {
                if (locals.TryGetValue(occurrence.Symbol, out var target))
                {
                    sink.Add(
                        ScipFacts.FileLocalXRef,
                        ScipFacts.FileLocalXRefFact(
                            file, start, length, target.Start, target.Length,
                            ScipFacts.RoleOf(occurrence.Roles)));

                    written += 1;
                }

                continue;
            }

            var symbol = SymbolOf(occurrence.Symbol);

            // **Every occurrence is a cross-reference, definitions included.** A definition
            // is the occurrence a jump lands on, so leaving it out of the reference index
            // would make "find every use" answer everything but the declaration.
            sink.Add(
                ScipFacts.FileXRef,
                ScipFacts.FileXRefFact(
                    file, start, length, symbol, ScipFacts.RoleOf(occurrence.Roles)));
            sink.Add(
                ScipFacts.SymbolXRef,
                ScipFacts.SymbolXRefFact(symbol, file, start, length));

            written += 2;

            if (!occurrence.IsDefinition)
            {
                continue;
            }

            var information = named.GetValueOrDefault(occurrence.Symbol);
            var kind = ScipFacts.KindOf(information?.Kind ?? 0);
            var name = information?.DisplayName is { Length: > 0 } display
                ? display
                : Descriptors.NameOf(occurrence.Symbol);

            sink.Add(
                ScipFacts.Definition,
                ScipFacts.DefinitionFact(
                    symbol, file, start, length, kind, name, occurrence.Symbol));
            sink.Add(
                ScipFacts.FileDefinition,
                ScipFacts.FileDefinitionFact(file, start, length, symbol, kind, name));
            sink.Add(
                ScipFacts.SearchEntry,
                ScipFacts.SearchEntryFact(name, kind, symbol, file, occurrence.StartLine + 1));
            sink.Add(ScipFacts.SymbolByName, ScipFacts.SymbolByNameFact(name, symbol));

            written += 4;
        }

        written += Styles.Emit(sink, file, document, lines);

        return written;
    }

    /// <summary>
    /// What this document says about each of its symbols, one entry per symbol.
    /// </summary>
    /// <remarks>
    /// <b>A duplicate is refused rather than resolved.</b> Two <c>SymbolInformation</c>
    /// entries for one symbol disagree about the kind and the display name and there is no
    /// entry to prefer, so the file is malformed — and <c>ToDictionary</c>'s
    /// <c>ArgumentException</c> is outside the CLI's catch filter, which turns a malformed
    /// input into a stack trace instead of a diagnosis naming the symbol.
    /// </remarks>
    private static Dictionary<string, SymbolInformation> Named(Document document)
    {
        var named = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);

        foreach (var information in document.Symbols)
        {
            if (!named.TryAdd(information.Symbol, information))
            {
                throw new FormatException(
                    $"{document.RelativePath} states `{information.Symbol}` twice; a "
                    + "document carries one SymbolInformation per symbol");
            }
        }

        return named;
    }

    /// <summary>
    /// Where each of this document's locals is declared, as a byte span.
    /// </summary>
    /// <remarks>
    /// Built per document rather than per converter, which is the whole point: the map
    /// is keyed on a string whose scope is one file, so a converter-wide one would let
    /// the last file indexed decide where every earlier file's <c>local 1</c> points.
    /// The first definition wins, so a symbol an index defines twice still answers the
    /// span a reader reaches first.
    /// </remarks>
    private static Dictionary<string, (long Start, long Length)> LocalTargets(
        Document document, Lines lines)
    {
        var targets = new Dictionary<string, (long, long)>(StringComparer.Ordinal);

        foreach (var occurrence in document.Occurrences)
        {
            if (!occurrence.IsDefinition || !Descriptors.IsLocal(occurrence.Symbol))
            {
                continue;
            }

            var start = lines.Offset(
                occurrence.StartLine, occurrence.StartCharacter, document.PositionEncoding);
            var end = lines.Offset(
                occurrence.EndLine, occurrence.EndCharacter, document.PositionEncoding);

            targets.TryAdd(occurrence.Symbol, (start, Math.Max(0, end - start)));
        }

        return targets;
    }

    /// <summary>The line table, and the offset lookup that inverts it.</summary>
    private void Emit(FjordFact file, Lines lines)
    {
        sink.Add(
            ScipFacts.FileInfo,
            ScipFacts.FileInfoFact(file, lines.Bytes, lines.Count, lines.EndsInNewline));

        for (var n = 0; n < lines.Count; n++)
        {
            var row = lines[n];

            sink.Add(
                ScipFacts.FileLine,
                ScipFacts.FileLineFact(file, n + 1, row.Text, row.Start, row.Bytes, row.CStart));
            sink.Add(ScipFacts.FileLineAt, ScipFacts.FileLineAtFact(file, row.Start, n + 1));
        }
    }

    private FjordFact SymbolOf(string symbol)
    {
        if (_symbols.TryGetValue(symbol, out var known))
        {
            return known;
        }

        var fact = ScipFacts.SymbolFact(symbol);
        _symbols[symbol] = fact;
        sink.Add(ScipFacts.Symbol, fact);

        return fact;
    }

    /// <summary>The document's text, from the index or from the checkout.</summary>
    private string? TextOf(Document document)
    {
        if (document.Text.Length > 0)
        {
            return document.Text;
        }

        var path = Path.Combine(root, document.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }
}
