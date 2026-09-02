using System.Collections.Generic;
using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>The schema set a .NET producer writes — <c>schemas/dotnet.sigla</c>, stated
/// independently.</b>
/// </para>
/// <para>
/// This replaces <see cref="CodeIndex"/>, whose schema is being retired. It arrives a
/// layer at a time rather than as one 67-predicate paste, because each layer's emission
/// lands with its own gate and a declaration nothing writes is a name in a file.
/// </para>
/// <para>
/// <b>A client may declare only what it writes, and that is the contract rather than a
/// shortcut.</b> Predicate ids here are <i>this client's</i>: a block header carries the
/// predicate's **name**, and a nested reference takes its predicate from the field's
/// declared target — so nothing positional crosses the wire. The fingerprint is the
/// database's, carried and not computed, and it asserts provenance: that this client was
/// written against that schema. What proves the shapes are right is writing a fact of
/// every declared predicate through a real server and reading it back, which is what each
/// layer's gate does.
/// </para>
/// </summary>
internal static class DotnetIndex
{
    /// <summary>
    /// <c>schemas/dotnet.sigla</c>'s fingerprint, as <c>fjord schema check</c> prints it.
    /// </summary>
    /// <remarks>
    /// Carried, not computed — the whole schema's, not this partial statement's. A stale
    /// one fails the handshake loudly, which is the assertion it is for.
    /// </remarks>
    public const ulong SchemaFingerprint = 0xc20dfe719b04e025;

    // ---- src: the shared source layer ------------------------------------------------

    public const uint File = 0;
    public const uint Symbol = 1;
    public const uint FileLanguage = 2;
    public const uint FileDigest = 3;
    public const uint FileOrigin = 4;
    public const uint FileInfo = 5;
    public const uint FileLine = 6;
    public const uint FileLineAt = 7;
    public const uint FileLineStyles = 8;

    // ---- config: what the index was resolved against ---------------------------------

    public const uint Setting = 9;

    /// <summary>Every predicate id this client holds, in schema order.</summary>
    public static readonly uint[] Predicates =
    [
        File, Symbol, FileLanguage, FileDigest, FileOrigin, FileInfo, FileLine,
        FileLineAt, FileLineStyles, Setting,
    ];

    /// <summary>
    /// <c>src.Language</c>'s named alternatives, in discriminant order — alternative
    /// <c>n + 1</c> is <c>LanguageNames[n]</c>, and <c>other : string = 0</c> is the valve
    /// that is not in this list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Append only.</b> I10 froze these the day the layer shipped: one added in the
    /// wrong place renumbers every one after it, which reads on disk as every file having
    /// changed language.
    /// </para>
    /// <para>
    /// <b>Its own copy, not <see cref="CodeIndex"/>'s.</b> That class is being deleted, and
    /// a statement that borrows from it would break when it goes.
    /// </para>
    /// <para>
    /// <b>Declared above <see cref="Schema"/> on purpose.</b> Static field initialisers run
    /// in declaration order and <c>Schema</c>'s reads this array, so moving it below turns
    /// every use of this class into a <c>TypeInitializationException</c> — at run time,
    /// with nothing wrong at the site that fails.
    /// </para>
    /// </remarks>
    public static readonly string[] LanguageNames =
    [
        "csharp", "typescript", "javascript", "tsx", "jsx", "rust", "python",
        "java", "cpp", "c", "go", "json", "yaml", "markdown", "css", "html",
        "sql", "shell", "xml", "proto",
    ];

    /// <summary>
    /// The statement itself. Transcribed from the `.sigla` files rather than generated
    /// from the resolved schema: two independent statements of one schema is what the
    /// fingerprint is for, and a generated one would agree by construction.
    /// </summary>
    public static readonly FjordSchema Schema = new([
        // `src.File` — a path relative to the index root, interned once.
        new FjordPredicate("src.File", FjordType.String, null),

        new FjordPredicate("src.Symbol", FjordType.String, null),

        new FjordPredicate("src.FileLanguage",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(("language", LanguageType))),

        new FjordPredicate("src.FileDigest",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(("digest", FjordType.String))),

        new FjordPredicate("src.FileOrigin",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("repo", FjordType.String),
                ("revision", FjordType.String))),

        new FjordPredicate("src.FileInfo",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("bytes", FjordType.Integer),
                ("lines", FjordType.Integer),
                ("endsInNewline", BoolType))),

        new FjordPredicate("src.FileLine", FjordType.Rec(
                ("file", FjordType.Reference(File)),
                ("line", FjordType.Integer)),
            FjordType.Rec(
                ("text", FjordType.String),
                ("start", FjordType.Integer),
                ("bytes", FjordType.Integer),
                ("cstart", FjordType.Integer))),

        new FjordPredicate("src.FileLineAt", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("start", FjordType.Integer),
            ("line", FjordType.Integer)), null),

        new FjordPredicate("src.FileLineStyles", FjordType.Rec(
                ("file", FjordType.Reference(File)),
                ("line", FjordType.Integer)),
            FjordType.Rec(("styles", FjordType.Blob))),

        // `config.Setting` — key-only and multi-valued, so one dimension may hold
        // several values. Both fields are strings so a new dimension costs nothing.
        new FjordPredicate("config.Setting", FjordType.Rec(
            ("dimension", FjordType.String),
            ("value", FjordType.String)), null),
    ], SchemaFingerprint);

    /// <summary>`{ false_ = 0 | true_ = 1 }`.</summary>
    private static FjordType BoolType => FjordType.OneOf(
        ("false_", 0u, FjordType.Rec()),
        ("true_", 1u, FjordType.Rec()));

    private static FjordType LanguageType
    {
        get
        {
            var alternatives = new List<(string, uint, FjordType)>
            {
                ("other", 0u, FjordType.String),
            };
            for (var index = 0; index < LanguageNames.Length; index++)
            {
                alternatives.Add((LanguageNames[index], (uint)index + 1, FjordType.Rec()));
            }

            return FjordType.OneOf([.. alternatives]);
        }
    }

    public static string NameOf(uint predicate) => Schema[predicate].Name;

    // ---- the facts -------------------------------------------------------------------

    public static FjordFact FileFact(string path) => new(File, FjordValue.Of(path));

    public static FjordFact SymbolFact(string symbol) => new(Symbol, FjordValue.Of(symbol));

    /// <summary>
    /// <c>config.Setting</c>: one axis of the index. Key-only, so the pair is the fact.
    /// </summary>
    public static FjordFact SettingFact(string dimension, string value) =>
        new(Setting, FjordValue.Rec(FjordValue.Of(dimension), FjordValue.Of(value)));
}
