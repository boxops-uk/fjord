using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>S1's gate: every <c>src.*</c> predicate answers against a database this indexer
/// wrote.</b>
/// </para>
/// <para>
/// The Rust battery asks one query per predicate against facts written by hand, which
/// proves the schema answers. This asks the same questions of an index the real walk
/// produced, which is the other half — a predicate the schema declares and the producer
/// never fills is a name in a file, and nothing else in the suite would notice.
/// </para>
/// <para>
/// A real server over a real socket, and a workspace rather than a bare compilation, so
/// that the classifier has a <c>Document</c> and the style layer is covered too.
/// </para>
/// </summary>
public sealed class SourceLayerDatabaseTests
{
    /// <summary>
    /// The fixture, which has to contain <b>references</b> and not only declarations: a
    /// class with two members and no method bodies produces no cross-reference facts at
    /// all, and the joins this file exists to assert would pass on an empty collection.
    /// </summary>
    private const string Source = """
        namespace Fixture.Deep
        {
            /// <summary>A thing with a <c>Count</c>.</summary>
            public class Thing
            {
                public int Count;

                public string Label { get; set; }

                public void Do()
                {
                    Count = Helper(1);
                }

                public void Do(int times)
                {
                    var local = times;
                    Count = local;
                }

                private int Helper(int a) => a;
            }
        }
        """;

    [Fact]
    public void Every_source_layer_predicate_answers_against_an_indexed_database()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        var directory = Directory.CreateTempSubdirectory("fjord-source-db");

        try
        {
            var path = Path.Combine(directory.FullName, "Thing.cs");
            File.WriteAllText(path, Source);

            Index(directory.FullName, path, server.Socket);

            using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

            // One question per predicate. Each asserts rows, because "the query compiles"
            // is not the claim — the claim is that the producer filled it.
            var questions = new (string What, string Query)[]
            {
                ("the file", "F where src.File F"),
                ("the symbol", "S where src.Symbol S"),
                ("the language", "X.value where X = src.FileLanguage _"),
                ("the digest", "X.value where X = src.FileDigest _"),
                ("the origin", "X.value where X = src.FileOrigin _"),
                ("the file's shape", "X.value where X = src.FileInfo _"),
                ("a line", "X.value where X = src.FileLine {file = F, line = 3}"),
                ("the offset table", "{s = S, l = L} where src.FileLineAt {file = F, start = S, line = L}"),
                ("the styles", "X.value where X = src.FileLineStyles {file = F, line = 3}"),
            };

            foreach (var (what, query) in questions)
            {
                Assert.NotEmpty(connection.Query(query).Rows);
            }

            // And two answers, not just row counts. **The summary agrees with the table it
            // summarises** — the invariant a consumer leans on when an offset resolves past
            // the last line's start, asserted here across the whole round trip rather than
            // inside the producer.
            var lines = connection.Query("L where src.FileLine {file = F, line = L}").Rows.Count;
            var info = Assert.Single(connection.Query("X.value where X = src.FileInfo _").Rows);

            Assert.Equal(lines, Int(Fields(info)[1]));

            // And the overload disambiguator survived being written and read back.
            var symbols = connection.Query("S where src.Symbol S").Rows
                .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
                .ToList();

            Assert.Contains(symbols, symbol =>
                symbol.EndsWith("Fixture/Deep/Thing#Do(+1).", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// <para>
    /// <b>S4's gate: the questions a UI asks, against an index this producer wrote.</b>
    /// </para>
    /// <para>
    /// `codemarkup` is the same facts as `csharp` re-keyed, so what is worth asserting is
    /// not that the predicates have rows — the round-trip test covers that — but that the
    /// *joins they exist for* answer. These are the two `index.sigla`'s own header names,
    /// narrowed to the one language this producer fills.
    /// </para>
    /// </summary>
    [Fact]
    public void The_ui_surface_answers_the_joins_it_exists_for()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        var directory = Directory.CreateTempSubdirectory("fjord-markup-db");

        try
        {
            var path = Path.Combine(directory.FullName, "Thing.cs");
            File.WriteAllText(path, Source);

            Index(directory.FullName, path, server.Socket);

            using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

            // **Go to definition.** A symbol in, the file and span it is declared at out —
            // one seek, and the row a cross-database fan-out returns.
            Assert.NotEmpty(connection.Query(
                "{at = D.value} where "
                + "D = codemarkup.Definition {symbol = S, file = F}").Rows);

            // **Every reference in one file, resolved to its definition.** The join the
            // whole layer exists for: a renderer asks it once per file and splices links
            // over the text.
            Assert.NotEmpty(connection.Query(
                "{use = SP, defined = D.value} where "
                + "codemarkup.FileXRef {file = F, span = SP, target = S, role = R}; "
                + "D = codemarkup.Definition {symbol = S, file = DF}").Rows);

            // **Find references**, keyed by the target so it is a seek rather than a scan
            // of the largest table in the index.
            Assert.NotEmpty(connection.Query(
                "{file = P, at = SP} where "
                + "codemarkup.SymbolXRef {target = S, file = F, span = SP}; F = src.File P").Rows);

            // **A case-insensitive prefix search**, which is what `nameLowercase` leads
            // for — a guided seek rather than a scan over every name in the index.
            Assert.NotEmpty(connection.Query(
                "{name = N, kind = K, line = L} where "
                + "codemarkup.SearchEntry {nameLowercase = \"th\".., name = N, kind = K, "
                + "symbol = S, file = F, line = L}").Rows);

            // **Containment, both ways, and which way is which.** A relation rather than
            // a field, so "what is in this type" and "what contains this member" are both
            // seeks — and `codemarkup.sigla` reads `Relation` as "`from` <kind> `to`", so
            // the containing type is `from`. The two predicates carry one edge reversed,
            // so a fact written under the other one's id answers both of these and
            // answers them backwards, with every symbol still resolving.
            var contains = Related(
                connection, "codemarkup.Relation {from = A, kind = {contains = _}, to = B}");

            Assert.Contains(contains, edge =>
                edge.From.EndsWith("Fixture/Deep/Thing#", StringComparison.Ordinal)
                && edge.To.EndsWith("Fixture/Deep/Thing#Do().", StringComparison.Ordinal));
            Assert.DoesNotContain(contains, edge =>
                edge.From.EndsWith("Fixture/Deep/Thing#Do().", StringComparison.Ordinal));

            Assert.Contains(
                Related(connection, "codemarkup.RelationOf {from = A, kind = {contains = _}, to = B}"),
                edge => edge.From.EndsWith("Fixture/Deep/Thing#", StringComparison.Ordinal)
                    && edge.To.EndsWith("Fixture/Deep/Thing#Do().", StringComparison.Ordinal));

            // **The hover card**, which is a value fetch on one symbol rather than a join.
            Assert.NotEmpty(connection.Query(
                "X.value where X = codemarkup.SymbolInfo {symbol = S}").Rows);

            // And completeness: every predicate of the layer has facts. A UI surface with
            // one empty predicate is a UI with one dead feature, and nothing else here
            // would notice — the joins above each touch only three or four of the ten.
            foreach (var predicate in new[]
            {
                "codemarkup.Definition", "codemarkup.SymbolInfo", "codemarkup.FileDefinition",
                "codemarkup.FileXRef", "codemarkup.SymbolXRef", "codemarkup.FileLocalXRef",
                "codemarkup.SearchEntry", "codemarkup.SymbolByName", "codemarkup.Relation",
                "codemarkup.RelationOf",
            })
            {
                Assert.NotEmpty(connection.Query($"X where X = {predicate} _").Rows);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The heritage fixture. <see cref="Source"/> is one class with no base type, no
    /// interface and no <c>override</c>, so every relation kind but <c>contains</c> is
    /// unreachable from it and a transposed edge is invisible to every assertion above.
    /// </summary>
    private const string Hierarchy = """
        namespace Fixture.Aviary
        {
            public interface IQuack
            {
                void Quack();
            }

            public class Bird
            {
                public virtual void Fly()
                {
                }
            }

            public class Duck : Bird, IQuack
            {
                public override void Fly()
                {
                }

                public void Quack()
                {
                }
            }
        }
        """;

    /// <summary>
    /// <para>
    /// <b>Heritage edges run from the deriving symbol to the one it derives from.</b>
    /// </para>
    /// <para>
    /// <c>codemarkup.sigla</c> fixes the direction — <c>Relation</c> reads "<c>from</c>
    /// &lt;kind&gt; <c>to</c>" — and <c>RelationOf</c> is the same edge reversed. So a
    /// transposed pair answers both queries with every symbol resolving and says "Base
    /// extends Derived": neither direction recovers the truth, and a row count notices
    /// nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void Heritage_edges_run_from_the_deriving_symbol_to_the_one_it_derives_from()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        var directory = Directory.CreateTempSubdirectory("fjord-heritage-db");

        try
        {
            var path = Path.Combine(directory.FullName, "Aviary.cs");
            File.WriteAllText(path, Hierarchy);

            Index(directory.FullName, path, server.Socket);

            using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

            foreach (var (kind, from, to) in new (string Kind, string From, string To)[]
            {
                ("extends", "Fixture/Aviary/Duck#", "Fixture/Aviary/Bird#"),
                ("implements", "Fixture/Aviary/Duck#", "Fixture/Aviary/IQuack#"),
                ("overrides", "Fixture/Aviary/Duck#Fly().", "Fixture/Aviary/Bird#Fly()."),
            })
            {
                var edges = Related(
                    connection, $"codemarkup.Relation {{from = A, kind = {{{kind} = _}}, to = B}}");
                var reversed = Related(
                    connection, $"codemarkup.RelationOf {{from = A, kind = {{{kind} = _}}, to = B}}");

                Assert.Contains(edges, edge => Ends(edge, from, to));
                Assert.Contains(reversed, edge => Ends(edge, from, to));
                Assert.DoesNotContain(edges, edge => Ends(edge, to, from));
                Assert.DoesNotContain(reversed, edge => Ends(edge, to, from));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static bool Ends((string From, string To) edge, string from, string to) =>
        edge.From.EndsWith(from, StringComparison.Ordinal)
        && edge.To.EndsWith(to, StringComparison.Ordinal);

    /// <summary>
    /// The symbol pairs a relation pattern answers, named by the field it binds them to
    /// rather than by the predicate's own field order.
    /// </summary>
    private static List<(string From, string To)> Related(FjordConnection connection, string edge) =>
        [.. connection.Query(
            "{from = FromName, to = ToName} where "
            + "A = src.Symbol FromName; B = src.Symbol ToName; "
            + edge).Rows
            .Select(row => (Str(Fields(row)[0]), Str(Fields(row)[1])))];

    private static string Str(FjordValue value) => Assert.IsType<FjordValue.Str>(value).Value;

    private static long Int(FjordValue value) => Assert.IsType<FjordValue.Int>(value).Value;

    private static IReadOnlyList<FjordValue> Fields(FjordValue value) =>
        Assert.IsType<FjordValue.Record>(value).Fields;

    /// <summary>Walk one file into the database behind <paramref name="socket"/>.</summary>
    private static void Index(string root, string path, string socket)
    {
        var options = new Options
        {
            Solutions = [root],
            Styles = true,
            Repo = "github.com/boxops-uk/fixture",
            Revision = "3fa4961",
        };

        using var workspace = new AdhocWorkspace();

        // **The references have to be on the project the workspace holds.**
        // `AddProject(name, language).AddMetadataReference(...)` returns a *detached*
        // project in a new solution, so the document then lands on a project with no
        // references — every type is an error type, `csharp.AType` cannot express one, and
        // the walk correctly drops every member while still writing the class. Which is
        // what `Inexpressible` is for, and why this test asserts it is zero.
        var project = workspace.AddProject(Microsoft.CodeAnalysis.ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            name: "Fixture",
            assemblyName: "Fixture",
            language: LanguageNames.CSharp,
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]));
        // `DocumentInfo` rather than `AddDocument(id, name, text)`, because the walk keys
        // everything on `SyntaxTree.FilePath` and the simple overload leaves it empty — the
        // file would then be outside the root and silently skipped.
        var document = workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            name: Path.GetFileName(path),
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(File.ReadAllText(path)), VersionStamp.Create(), path)),
            filePath: path));

        var compilation = document.Project.GetCompilationAsync().GetAwaiter().GetResult()!;

        var projects = ProjectIndex.Build(root, root, [], solutions: [], TextWriter.Null);

        using var writing = FjordConnection.Connect(socket, "dotnet", DotnetIndex.Schema);
        using var sink = new FactSink(DotnetIndex.Schema, [new FjordTarget(writing)]);

        var indexer = new Boxops.Fjord.Indexer.Indexer(options, sink, root, projects);
        indexer.Index(compilation, document.Project);

        sink.Drain();

        // A fixture whose references did not load drops every member and indexes the
        // class alone — which reads as an indexer that works and an index that is empty.
        Assert.Equal(0, indexer.Inexpressible);
    }
}
