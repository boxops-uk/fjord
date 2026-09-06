namespace Boxops.Fjord.Scip;

/// <summary>What a symbol occurrence is, once read off the wire.</summary>
/// <param name="Range">
/// <c>[startLine, startCharacter, endLine, endCharacter]</c>, or three when the
/// occurrence is on one line: <c>[line, startCharacter, endCharacter]</c>.
/// </param>
internal sealed record Occurrence(
    IReadOnlyList<int> Range,
    string Symbol,
    int Roles,
    int SyntaxKind)
{
    /// <summary><c>SymbolRole.Definition</c>, which is bit 0.</summary>
    public bool IsDefinition => (Roles & 0x1) != 0;

    public int StartLine => Range.Count > 0 ? Range[0] : 0;

    public int StartCharacter => Range.Count > 1 ? Range[1] : 0;

    public int EndLine => Range.Count == 4 ? Range[2] : StartLine;

    public int EndCharacter => Range.Count switch
    {
        4 => Range[3],
        3 => Range[2],
        _ => StartCharacter,
    };
}

/// <summary>What an index says about a symbol, beyond where it appears.</summary>
internal sealed record SymbolInformation(string Symbol, int Kind, string DisplayName);

/// <summary>One file, and everything the index says about it.</summary>
internal sealed record Document(
    string RelativePath,
    string Language,
    string Text,
    int PositionEncoding,
    IReadOnlyList<Occurrence> Occurrences,
    IReadOnlyList<SymbolInformation> Symbols);

/// <summary>A whole SCIP index.</summary>
internal sealed record ScipIndex(
    string ProjectRoot,
    IReadOnlyList<Document> Documents,
    IReadOnlyList<SymbolInformation> ExternalSymbols)
{
    /// <summary>
    /// Read an index off the wire.
    /// </summary>
    /// <remarks>
    /// <b>The field numbers are the specification's, transcribed.</b> They are frozen by
    /// protobuf's own compatibility rules, which is what makes transcribing them safe —
    /// but they are also the one thing here a reader cannot check by reasoning, so each is
    /// written beside the name the specification gives it.
    /// </remarks>
    public static ScipIndex Read(ReadOnlySpan<byte> bytes)
    {
        var root = string.Empty;
        var documents = new List<Document>();
        var externals = new List<SymbolInformation>();

        var index = new Protobuf(bytes);

        while (index.Next())
        {
            switch (index.Field)
            {
                case 1: // Index.metadata
                    root = ProjectRootOf(index.Message());
                    break;

                case 2: // Index.documents
                    documents.Add(ReadDocument(index.Message()));
                    break;

                case 3: // Index.external_symbols
                    externals.Add(ReadSymbol(index.Message()));
                    break;

                default:
                    index.Skip();
                    break;
            }
        }

        return new ScipIndex(root, documents, externals);
    }

    private static string ProjectRootOf(Protobuf metadata)
    {
        var root = string.Empty;

        while (metadata.Next())
        {
            if (metadata.Field == 3) // Metadata.project_root
            {
                root = metadata.Text();
            }
            else
            {
                metadata.Skip();
            }
        }

        return root;
    }

    private static Document ReadDocument(Protobuf document)
    {
        string path = string.Empty, language = string.Empty, text = string.Empty;
        var encoding = 0;
        var occurrences = new List<Occurrence>();
        var symbols = new List<SymbolInformation>();

        while (document.Next())
        {
            switch (document.Field)
            {
                case 1: // Document.relative_path
                    path = document.Text();
                    break;

                case 2: // Document.occurrences
                    occurrences.Add(ReadOccurrence(document.Message()));
                    break;

                case 3: // Document.symbols
                    symbols.Add(ReadSymbol(document.Message()));
                    break;

                case 4: // Document.language
                    language = document.Text();
                    break;

                case 5: // Document.text
                    text = document.Text();
                    break;

                case 6: // Document.position_encoding
                    encoding = (int)document.Varint();
                    break;

                default:
                    document.Skip();
                    break;
            }
        }

        return new Document(path, language, text, encoding, occurrences, symbols);
    }

    private static Occurrence ReadOccurrence(Protobuf occurrence)
    {
        var range = new List<int>();
        var symbol = string.Empty;
        int roles = 0, syntax = 0;

        while (occurrence.Next())
        {
            switch (occurrence.Field)
            {
                case 1: // Occurrence.range
                    occurrence.Int32s(range);
                    break;

                case 2: // Occurrence.symbol
                    symbol = occurrence.Text();
                    break;

                case 3: // Occurrence.symbol_roles
                    roles = (int)occurrence.Varint();
                    break;

                case 5: // Occurrence.syntax_kind
                    syntax = (int)occurrence.Varint();
                    break;

                default:
                    occurrence.Skip();
                    break;
            }
        }

        return new Occurrence(range, symbol, roles, syntax);
    }

    private static SymbolInformation ReadSymbol(Protobuf symbol)
    {
        string name = string.Empty, display = string.Empty;
        var kind = 0;

        while (symbol.Next())
        {
            switch (symbol.Field)
            {
                case 1: // SymbolInformation.symbol
                    name = symbol.Text();
                    break;

                case 5: // SymbolInformation.kind
                    kind = (int)symbol.Varint();
                    break;

                case 6: // SymbolInformation.display_name
                    display = symbol.Text();
                    break;

                default:
                    symbol.Skip();
                    break;
            }
        }

        return new SymbolInformation(name, kind, display);
    }
}
