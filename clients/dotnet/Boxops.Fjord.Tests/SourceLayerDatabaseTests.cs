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
    private const string Source = """
        namespace Fixture.Deep
        {
            public class Thing
            {
                public int Count;

                public string Name { get; set; }

                public void Do() {}

                public void Do(int times) {}
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

    private static long Int(FjordValue value) => Assert.IsType<FjordValue.Int>(value).Value;

    private static IReadOnlyList<FjordValue> Fields(FjordValue value) =>
        Assert.IsType<FjordValue.Record>(value).Fields;

    /// <summary>Walk one file into the database behind <paramref name="socket"/>.</summary>
    private static void Index(string root, string path, string socket)
    {
        var options = new Options
        {
            Source = root,
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

        var projects = ProjectIndex.Build(root, root, [], TextWriter.Null);

        using var writing = FjordConnection.Connect(socket, "dotnet", DotnetIndex.Schema);
        using var sink = new FactSink(options, [new FjordTarget(writing)]);

        var indexer = new Boxops.Fjord.Indexer.Indexer(options, sink, root, projects);
        indexer.Index(compilation, document.Project);

        sink.Drain();

        // A fixture whose references did not load drops every member and indexes the
        // class alone — which reads as an indexer that works and an index that is empty.
        Assert.Equal(0, indexer.Inexpressible);
    }
}
