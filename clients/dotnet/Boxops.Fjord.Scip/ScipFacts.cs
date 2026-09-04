using Boxops.Fjord.Client;

namespace Boxops.Fjord.Scip;

/// <summary>
/// What this converter writes, stated independently.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fourteen predicates against a database of a hundred and thirty-eight.</b> A client
/// declares the shapes it uses, not the database's whole schema: predicate ids are its
/// own, a block header carries the predicate's name, and a nested reference takes its
/// predicate from the field's declared target. What is checked is that this database holds
/// each of these <i>identically</i> — so a shape spelled differently here is refused by
/// name rather than written wrongly.
/// </para>
/// <para>
/// <b>Only what SCIP actually contains.</b> There is no declaration layer here, no type
/// graph and no build layer: a SCIP index is occurrences and symbol information, and the
/// <c>codemarkup</c> surface is what those transcribe into. Anything else would have to be
/// invented, and an invented fact is worse than a missing one.
/// </para>
/// <para>
/// <b>The vocabularies are transcribed, and their discriminants are frozen.</b> `Kind` is
/// LSP's `SymbolKind` and `Role` is SCIP's `SymbolRole` projected; they sit in keys, so a
/// slip in a twenty-seven-line table is permanent. They are written out in full rather
/// than referenced because this project depends on no producer.
/// </para>
/// </remarks>
internal static class ScipFacts
{
    // Ids are this client's own, and the order is this file's. What crosses the wire is
    // the name.
    public const uint File = 0;
    public const uint Symbol = 1;
    public const uint FileInfo = 2;
    public const uint FileLine = 3;
    public const uint FileLineAt = 4;
    public const uint FileLineStyles = 5;
    public const uint Setting = 6;
    public const uint Definition = 7;
    public const uint FileDefinition = 8;
    public const uint FileXRef = 9;
    public const uint SymbolXRef = 10;
    public const uint FileLocalXRef = 11;
    public const uint SearchEntry = 12;
    public const uint SymbolByName = 13;

    public static readonly uint[] Predicates =
    [
        File, Symbol, FileInfo, FileLine, FileLineAt, FileLineStyles, Setting,
        Definition, FileDefinition, FileXRef, SymbolXRef, FileLocalXRef, SearchEntry,
        SymbolByName,
    ];

    /// <summary>`src.Bool`, spelled as the source layer spells it.</summary>
    private static readonly FjordType Bool = FjordType.OneOf(
        ("false_", 0u, FjordType.Rec()),
        ("true_", 1u, FjordType.Rec()));

    /// <summary>`src.ByteSpan` — a UTF-8 byte offset and a length in bytes.</summary>
    private static readonly FjordType ByteSpan = FjordType.Rec(
        ("start", FjordType.Integer),
        ("length", FjordType.Integer));

    /// <summary>`codemarkup.Kind` — LSP's `SymbolKind`, 1–26, verbatim.</summary>
    private static readonly FjordType Kind = FjordType.OneOf(
        ("other", 0u, FjordType.String),
        ("file", 1u, FjordType.Rec()),
        ("module_", 2u, FjordType.Rec()),
        ("namespace_", 3u, FjordType.Rec()),
        ("package_", 4u, FjordType.Rec()),
        ("class_", 5u, FjordType.Rec()),
        ("method_", 6u, FjordType.Rec()),
        ("property_", 7u, FjordType.Rec()),
        ("field", 8u, FjordType.Rec()),
        ("constructor_", 9u, FjordType.Rec()),
        ("enum_", 10u, FjordType.Rec()),
        ("interface_", 11u, FjordType.Rec()),
        ("function_", 12u, FjordType.Rec()),
        ("variable", 13u, FjordType.Rec()),
        ("constant", 14u, FjordType.Rec()),
        ("string_", 15u, FjordType.Rec()),
        ("number", 16u, FjordType.Rec()),
        ("boolean_", 17u, FjordType.Rec()),
        ("array", 18u, FjordType.Rec()),
        ("object_", 19u, FjordType.Rec()),
        ("key", 20u, FjordType.Rec()),
        ("null_", 21u, FjordType.Rec()),
        ("enumMember", 22u, FjordType.Rec()),
        ("struct_", 23u, FjordType.Rec()),
        ("event", 24u, FjordType.Rec()),
        ("operator", 25u, FjordType.Rec()),
        ("typeParameter", 26u, FjordType.Rec()));

    /// <summary>`codemarkup.Role` — SCIP's `SymbolRole`, projected.</summary>
    private static readonly FjordType Role = FjordType.OneOf(
        ("other", 0u, FjordType.String),
        ("definition", 1u, FjordType.Rec()),
        ("read", 2u, FjordType.Rec()),
        ("write", 3u, FjordType.Rec()),
        ("call", 4u, FjordType.Rec()),
        ("typeRef", 5u, FjordType.Rec()),
        ("new_", 6u, FjordType.Rec()),
        ("import_", 7u, FjordType.Rec()),
        ("export_", 8u, FjordType.Rec()),
        ("heritage", 9u, FjordType.Rec()),
        ("decorator", 10u, FjordType.Rec()),
        ("jsx", 11u, FjordType.Rec()));

    /// <summary>
    /// The schema this converter claims, and the fingerprint of the database it fills.
    /// </summary>
    /// <remarks>
    /// The fingerprint is <c>schemas/index.sigla</c>'s — the composite that declares every
    /// language layer, which is the database a cross-language converter belongs in. It is
    /// carried rather than computed, so it asserts provenance and says nothing about the
    /// shapes; what proves those is the server decoding a fact of each against its own
    /// statement.
    /// </remarks>
    public static readonly FjordSchema Schema = new(
        [
            new FjordPredicate("src.File", FjordType.String, null),
            new FjordPredicate("src.Symbol", FjordType.String, null),
            new FjordPredicate(
                "src.FileInfo",
                FjordType.Rec(("file", FjordType.Reference(File))),
                FjordType.Rec(
                    ("bytes", FjordType.Integer),
                    ("lines", FjordType.Integer),
                    ("endsInNewline", Bool))),
            new FjordPredicate(
                "src.FileLine",
                FjordType.Rec(("file", FjordType.Reference(File)), ("line", FjordType.Integer)),
                FjordType.Rec(
                    ("text", FjordType.String),
                    ("start", FjordType.Integer),
                    ("bytes", FjordType.Integer),
                    ("cstart", FjordType.Integer))),
            new FjordPredicate(
                "src.FileLineAt",
                FjordType.Rec(
                    ("file", FjordType.Reference(File)),
                    ("start", FjordType.Integer),
                    ("line", FjordType.Integer)),
                null),
            new FjordPredicate(
                "src.FileLineStyles",
                FjordType.Rec(("file", FjordType.Reference(File)), ("line", FjordType.Integer)),
                FjordType.Rec(("styles", FjordType.Blob))),
            new FjordPredicate(
                "config.Setting",
                FjordType.Rec(("dimension", FjordType.String), ("value", FjordType.String)),
                null),
            new FjordPredicate(
                "codemarkup.Definition",
                FjordType.Rec(
                    ("symbol", FjordType.Reference(Symbol)),
                    ("file", FjordType.Reference(File))),
                FjordType.Rec(
                    ("span", ByteSpan),
                    ("kind", Kind),
                    ("name", FjordType.String),
                    ("qualified", FjordType.String))),
            new FjordPredicate(
                "codemarkup.FileDefinition",
                FjordType.Rec(
                    ("file", FjordType.Reference(File)),
                    ("span", ByteSpan),
                    ("symbol", FjordType.Reference(Symbol))),
                FjordType.Rec(("kind", Kind), ("name", FjordType.String))),
            new FjordPredicate(
                "codemarkup.FileXRef",
                FjordType.Rec(
                    ("file", FjordType.Reference(File)),
                    ("span", ByteSpan),
                    ("target", FjordType.Reference(Symbol)),
                    ("role", Role)),
                null),
            new FjordPredicate(
                "codemarkup.SymbolXRef",
                FjordType.Rec(
                    ("target", FjordType.Reference(Symbol)),
                    ("file", FjordType.Reference(File)),
                    ("span", ByteSpan)),
                null),
            new FjordPredicate(
                "codemarkup.FileLocalXRef",
                FjordType.Rec(
                    ("file", FjordType.Reference(File)),
                    ("span", ByteSpan),
                    ("target", ByteSpan),
                    ("role", Role)),
                null),
            new FjordPredicate(
                "codemarkup.SearchEntry",
                FjordType.Rec(
                    ("nameLowercase", FjordType.String),
                    ("name", FjordType.String),
                    ("kind", Kind),
                    ("symbol", FjordType.Reference(Symbol)),
                    ("file", FjordType.Reference(File)),
                    ("line", FjordType.Integer)),
                null),
            new FjordPredicate(
                "codemarkup.SymbolByName",
                FjordType.Rec(
                    ("name", FjordType.String),
                    ("symbol", FjordType.Reference(Symbol))),
                null),
        ],
        // `schemas/index.sigla`. A moved fingerprint is a rebuild of this converter.
        0xea69e11d083ae95f);

    public static string NameOf(uint predicate) => Schema.NameOf(predicate);

    private static FjordValue Tag(uint disc) => FjordValue.Alt(disc, FjordValue.Rec());

    private static FjordValue R(FjordFact fact) => FjordValue.Of(FjordRef.To(fact));

    public static FjordFact FileFact(string path) => new(File, FjordValue.Of(path));

    public static FjordFact SymbolFact(string symbol) => new(Symbol, FjordValue.Of(symbol));

    public static FjordFact SettingFact(string dimension, string value) =>
        new(Setting, FjordValue.Rec(FjordValue.Of(dimension), FjordValue.Of(value)));

    public static FjordFact FileInfoFact(FjordFact file, long bytes, long lines, bool newline) =>
        new(
            FileInfo,
            FjordValue.Rec(R(file)),
            FjordValue.Rec(FjordValue.Of(bytes), FjordValue.Of(lines), Tag(newline ? 1u : 0u)));

    public static FjordFact FileLineFact(
        FjordFact file, long line, string text, long start, long bytes, long cstart) =>
        new(
            FileLine,
            FjordValue.Rec(R(file), FjordValue.Of(line)),
            FjordValue.Rec(
                FjordValue.Of(text),
                FjordValue.Of(start),
                FjordValue.Of(bytes),
                FjordValue.Of(cstart)));

    public static FjordFact FileLineAtFact(FjordFact file, long start, long line) =>
        new(FileLineAt, FjordValue.Rec(R(file), FjordValue.Of(start), FjordValue.Of(line)));

    public static FjordFact FileLineStylesFact(FjordFact file, long line, byte[] styles) =>
        new(
            FileLineStyles,
            FjordValue.Rec(R(file), FjordValue.Of(line)),
            FjordValue.Rec(FjordValue.Of(styles)));

    private static FjordValue Span(long start, long length) =>
        FjordValue.Rec(FjordValue.Of(start), FjordValue.Of(length));

    public static FjordFact DefinitionFact(
        FjordFact symbol, FjordFact file, long start, long length,
        FjordValue kind, string name, string qualified) =>
        new(
            Definition,
            FjordValue.Rec(R(symbol), R(file)),
            FjordValue.Rec(Span(start, length), kind, FjordValue.Of(name), FjordValue.Of(qualified)));

    public static FjordFact FileDefinitionFact(
        FjordFact file, long start, long length, FjordFact symbol, FjordValue kind, string name) =>
        new(
            FileDefinition,
            FjordValue.Rec(R(file), Span(start, length), R(symbol)),
            FjordValue.Rec(kind, FjordValue.Of(name)));

    public static FjordFact FileXRefFact(
        FjordFact file, long start, long length, FjordFact target, FjordValue role) =>
        new(FileXRef, FjordValue.Rec(R(file), Span(start, length), R(target), role));

    public static FjordFact SymbolXRefFact(FjordFact target, FjordFact file, long start, long length) =>
        new(SymbolXRef, FjordValue.Rec(R(target), R(file), Span(start, length)));

    /// <summary>A reference whose target is file-local, answered span to span.</summary>
    /// <remarks>
    /// No <c>src.Symbol</c>: a SCIP <c>local</c> is an occurrence ordinal scoped to one
    /// document, so <c>local 1</c> in two files names two different entities and interning
    /// it would make them one.
    /// </remarks>
    public static FjordFact FileLocalXRefFact(
        FjordFact file, long start, long length,
        long targetStart, long targetLength, FjordValue role) =>
        new(
            FileLocalXRef,
            FjordValue.Rec(
                R(file), Span(start, length), Span(targetStart, targetLength), role));

    public static FjordFact SearchEntryFact(
        string name, FjordValue kind, FjordFact symbol, FjordFact file, long line) =>
        new(
            SearchEntry,
            FjordValue.Rec(
                FjordValue.Of(name.ToLowerInvariant()),
                FjordValue.Of(name),
                kind,
                R(symbol),
                R(file),
                FjordValue.Of(line)));

    public static FjordFact SymbolByNameFact(string name, FjordFact symbol) =>
        new(SymbolByName, FjordValue.Rec(FjordValue.Of(name), R(symbol)));

    /// <summary>
    /// SCIP's `SymbolInformation.Kind` as `codemarkup.Kind`.
    /// </summary>
    /// <remarks>
    /// <b>Two citations meeting, and neither invented here.</b> SCIP's kinds are far finer
    /// than LSP's — it distinguishes a static method from a method and a self-parameter
    /// from a parameter — so this projects rather than translates, and anything with no
    /// LSP counterpart takes the `other` valve carrying SCIP's own number. A consumer then
    /// sees a kind it can render, and nothing is silently called a method that was not one.
    /// </remarks>
    public static FjordValue KindOf(int scip) => scip switch
    {
        7 => Tag(5u),                          // Class
        8 => Tag(14u),                         // Constant
        9 => Tag(9u),                          // Constructor
        11 => Tag(10u),                        // Enum
        12 => Tag(22u),                        // EnumMember
        13 => Tag(24u),                        // Event
        15 or 79 => Tag(8u),                   // Field, StaticField
        16 => Tag(1u),                         // File
        17 => Tag(12u),                        // Function
        21 or 53 => Tag(11u),                  // Interface, Trait
        26 or 66 or 80 => Tag(6u),             // Method, AbstractMethod, StaticMethod
        29 => Tag(2u),                         // Module
        30 => Tag(3u),                         // Namespace
        34 => Tag(25u),                        // Operator
        35 => Tag(4u),                         // Package
        37 or 44 => Tag(13u),                  // Parameter, SelfParameter
        41 or 72 or 81 => Tag(7u),             // Property, Accessor, StaticProperty
        49 => Tag(23u),                        // Struct
        58 => Tag(26u),                        // TypeParameter
        61 => Tag(13u),                        // Variable
        _ => FjordValue.Alt(0u, FjordValue.Of($"scip:{scip}")),
    };

    /// <summary>
    /// SCIP's `symbol_roles` bitset as one `codemarkup.Role`.
    /// </summary>
    /// <remarks>
    /// <b>A bitset becoming one alternative loses something, and it is stated rather than
    /// hidden.</b> SCIP can say an occurrence is a write *and* generated; `Role` is a
    /// single choice, so the strongest fact wins — a definition is a definition whatever
    /// else it is. What the projection drops is `Generated`, `Test` and
    /// `ForwardDefinition`, none of which a cross-reference surface asks about.
    /// </remarks>
    public static FjordValue RoleOf(int roles) => (roles & 0x1) != 0
        ? Tag(1u)   // Definition
        : (roles & 0x4) != 0
            ? Tag(3u)   // WriteAccess
            : (roles & 0x8) != 0
                ? Tag(2u)   // ReadAccess
                : (roles & 0x2) != 0
                    ? Tag(7u)   // Import
                    // `other` carries a string, so a role with no counterpart says which
                    // bitset it was rather than becoming an empty alternative.
                    : FjordValue.Alt(0u, FjordValue.Of($"scip:{roles}"));
}
