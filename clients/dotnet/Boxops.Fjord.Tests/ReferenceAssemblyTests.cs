using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A repository that builds a reference assembly beside its implementation indexes to
/// completion.</b>
/// </para>
/// <para>
/// A reference assembly restates the whole public API of the assembly it stands for, under
/// that assembly's own identity — so <see cref="ScipSymbols"/> mints the *same* string for
/// the declaration in <c>ref/</c> and the one in <c>src/</c>, which is correct: they are
/// one symbol. But <c>codemarkup.SymbolInfo</c> is keyed <c>{symbol}</c> with
/// <c>{signature, doc, modifiers}</c> on the value side, and a reference assembly carries
/// no documentation comments and spells its members <c>partial</c>. Two facts wanted one
/// key with two values, ingest refused it (<c>ops-I4</c>), <c>FactSink</c> latched the
/// refusal, and the run died part-way through a write.
/// </para>
/// <para>
/// <b>This is the shape that made <c>dotnet/runtime</c> unindexable</b>, and it is not a corner
/// of C#: every library in its shared framework ships a <c>ref/</c> project beside its
/// <c>src/</c> one, and so does anything that ships a reference pack.
/// </para>
/// </summary>
public sealed class ReferenceAssemblyTests
{
    /// <summary>The package coordinate both projects mint, because both are <c>Widgets</c>.</summary>
    private const string Prefix = "nuget Widgets 1.0.0.0 ";

    private const string Gadget = "Widgets/Gadget#";

    /// <summary>
    /// <b>The run finishes, and the API is one symbol rather than two facts fighting over
    /// one key.</b>
    /// </summary>
    /// <remarks>
    /// <b>The exit code is the conflict assertion</b>, as in <c>ArityPairTests</c> and
    /// <c>PartialMemberTests</c>: a conflicting fact is refused by the server and
    /// <c>FactSink</c> turns a refusal into a failure the next flush throws, so a run that
    /// reached its end wrote nothing that disagreed with anything already there. Before
    /// reference assemblies were left unwalked this returned 134 with
    /// <c>Conflict: predicate ... already holds a different fact under this key</c>.
    /// </remarks>
    [Fact]
    public void A_reference_assembly_beside_its_implementation_indexes_to_completion()
    {
        using var fixture = Fixture.Copy("refimpl");
        using var server = FjordServer.Serving("refimpl", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("RefImpl.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//refimpl",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "refimpl", DotnetIndex.Schema);

        var symbols = Symbols(connection, "S where src.Symbol S");

        Assert.Contains(Gadget, symbols);
        Assert.Contains($"{Gadget}Spin().", symbols);
        Assert.Contains($"{Gadget}Name.", symbols);
        Assert.Contains("Widgets/Bolt#", symbols);
    }

    /// <summary>
    /// <b>The documentation that survives is the implementation's, not the empty one the
    /// reference assembly would have written.</b>
    /// </summary>
    /// <remarks>
    /// This is why the rule cannot be "whichever declaration the walk reached first wins":
    /// projects are walked in solution order, the reference assembly is listed first here
    /// on purpose, and a first-wins rule would answer every documentation query with the
    /// empty string while still exiting 0.
    /// </remarks>
    [Fact]
    public void The_documentation_written_is_the_implementations()
    {
        using var fixture = Fixture.Copy("refimpl");
        using var server = FjordServer.Serving("refimpl", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("RefImpl.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//refimpl",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "refimpl", DotnetIndex.Schema);

        // **`Assert.Single` is load-bearing rather than convenience**, the same reading
        // `PartialMemberTests.Doc` gives it: the key is `{symbol}` alone, so a second row
        // here is this test's conflict seen from the read side. The whole value comes back
        // and is read positionally — `signature`, `doc`, `modifiers` — because
        // `nyi/value-field` stands.
        var info = Assert.Single(
            connection.Query(
                "{info = I.value} where I = codemarkup.SymbolInfo {symbol = S}; "
                + $"S = src.Symbol \"{ScipSymbols.Scheme} {Prefix}{Gadget}Spin().\"")
                .Rows);

        var doc = Assert.IsType<FjordValue.Str>(Field(Field(info, 0), 1)).Value;

        Assert.Contains("Spins it", doc);
    }

    /// <summary>
    /// <b>Every definition is in the implementation's file, and none is in the reference
    /// assembly's.</b>
    /// </summary>
    /// <remarks>
    /// The quality half of the same decision. A reference assembly's declarations are an
    /// API surface restated for the compiler — "go to definition" landing in one is a
    /// worse answer than the one beside it, not an extra one.
    /// </remarks>
    [Fact]
    public void No_definition_lands_in_the_reference_assembly()
    {
        using var fixture = Fixture.Copy("refimpl");
        using var server = FjordServer.Serving("refimpl", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("RefImpl.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//refimpl",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "refimpl", DotnetIndex.Schema);

        var files = Strings(
            connection,
            "P where codemarkup.Definition {symbol = S, file = F}; F = src.File P");

        Assert.NotEmpty(files);
        Assert.All(files, path => Assert.DoesNotContain("Widgets/ref/", path));
        Assert.Contains(files, path => path.Contains("Widgets/src/"));
    }

    /// <summary>
    /// <b>The build layer is still whole: the reference assembly is a project in the graph,
    /// with its compilation, and the two projects share one assembly.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same decision <c>--max-files</c> already made. What projects a repository has is
    /// a fact about the repository, not about which files this run walked — so leaving a
    /// reference assembly unwalked must not delete it from the graph, or a consumer asking
    /// what builds here gets a wrong answer rather than a smaller one.
    /// </para>
    /// <para>
    /// <b>The single <c>msbuild.Assembly</c> row is the cause of the defect, asserted.</b>
    /// Two projects, one assembly identity — which is what makes both mint one
    /// <c>src.Symbol</c> per member, and why the two <c>codemarkup.SymbolInfo</c> facts
    /// collided. A fixture that stopped reproducing that would pass the tests above for the
    /// wrong reason.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_build_layer_still_names_the_reference_assembly()
    {
        using var fixture = Fixture.Copy("refimpl");
        using var server = FjordServer.Serving("refimpl", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("RefImpl.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//refimpl",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "refimpl", DotnetIndex.Schema);

        var projects = Strings(connection, "P where msbuild.Project {file = F}; F = src.File P");

        Assert.Contains(projects, path => path.Contains("Widgets/ref/Widgets.csproj"));
        Assert.Contains(projects, path => path.Contains("Widgets/src/Widgets.csproj"));

        // And each still has its compilation: unwalked is not undeclared.
        var compiled = Strings(
            connection,
            "P where msbuild.ProjectCompilation {project = J, framework = FW}; "
            + "J = msbuild.Project {file = F}; F = src.File P");

        Assert.Contains(compiled, path => path.Contains("Widgets/ref/Widgets.csproj"));
        Assert.Contains(compiled, path => path.Contains("Widgets/src/Widgets.csproj"));

        // One assembly for the two of them — the collision this fixture is about.
        Assert.Equal(
            ["Widgets"],
            Strings(connection, "N where msbuild.Assembly {name = N}"));
    }

    /// <summary>
    /// <b>A marker that did not bind is still a marker.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fixture above cannot reach this, and an implementation that only it gates is
    /// broken on the corpus that matters.</b> A design-time build resolves no metadata
    /// reference it did not need, so on an unbuilt checkout <c>ReferenceAssemblyAttribute</c>
    /// arrives as an <c>IErrorTypeSymbol</c>: <c>GetTypeByMetadataName</c> answers
    /// <see langword="null"/> for it and its <c>ContainingNamespace</c> is <c>System</c>,
    /// the deepest part that did resolve. Both a symbol comparison and a namespace check
    /// pass every test in this file and then walk every reference assembly in
    /// <c>dotnet/runtime</c> — silently, because "no reference assemblies here" and "found
    /// none" are the same answer.
    /// </para>
    /// <para>
    /// So this compilation is given <b>no references at all</b>, which is the sharpest form
    /// of what a real one is short of, and the assertion is the counter rather than the
    /// predicate: the walk is what has to change its mind.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_marker_that_did_not_bind_is_still_a_reference_assembly()
    {
        var (files, skipped) = Walk(
            "[assembly: System.Runtime.CompilerServices.ReferenceAssemblyAttribute()]\n"
            + "namespace Widgets { public partial class Gadget { } }");

        Assert.Equal(0, files);
        Assert.Equal(1, skipped);
    }

    /// <summary>
    /// <b>The control: the same compilation without the marker is walked.</b>
    /// </summary>
    /// <remarks>
    /// Without this, a predicate that answered <see langword="true"/> for everything would
    /// pass the test above and index nothing at all.
    /// </remarks>
    [Fact]
    public void A_compilation_without_the_marker_is_walked()
    {
        var (files, skipped) = Walk("namespace Widgets { public partial class Gadget { } }");

        Assert.Equal(1, files);
        Assert.Equal(0, skipped);
    }

    /// <summary>
    /// Walk one source text as its own compilation, and answer what the walk did with it.
    /// </summary>
    private static (int Files, int Skipped) Walk(string source)
    {
        var directory = Directory.CreateTempSubdirectory("fjord-refasm-");

        try
        {
            var path = System.IO.Path.Combine(directory.FullName, "Widgets.cs");
            File.WriteAllText(path, source);

            var options = new Options { Projects = [] };
            var projects = ProjectIndex.Build(
                directory.FullName, directory.FullName, [], solutions: [], TextWriter.Null);

            // **No metadata references**, so nothing in the source binds — the state a
            // design-time build over an unbuilt checkout hands this walk.
            var compilation = CSharpCompilation.Create(
                "Widgets",
                [CSharpSyntaxTree.ParseText(source, path: path)]);

            // Fully qualified: from `Boxops.Fjord.Tests`, the bare name `Indexer` resolves
            // to the sibling *namespace* rather than the type in it.
            Boxops.Fjord.Indexer.Indexer indexer;

            using (var sink = new FactSink(DotnetIndex.Schema, [new SourceWalkTests.Recorder()]))
            {
                indexer = new Boxops.Fjord.Indexer.Indexer(
                    options, sink, directory.FullName, projects);
                indexer.Index(compilation, null);
                sink.Drain();
            }

            return (indexer.Files, indexer.ReferenceAssemblies);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static FjordValue Field(FjordValue value, int index) =>
        Assert.IsType<FjordValue.Record>(value).Fields[index];

    /// <summary>
    /// The fixture's own symbols, with the scheme and package coordinate taken off.
    /// </summary>
    /// <remarks>
    /// <b>Filtered rather than asserted.</b> <c>Uses.Go</c> reaches <c>string.Length</c>, so
    /// <c>src.Symbol</c> holds the BCL's symbols beside this fixture's — a coordinate
    /// assertion over every row would be asserting what the framework is called.
    /// </remarks>
    private static List<string> Symbols(FjordConnection connection, string query)
    {
        var mine = $"{ScipSymbols.Scheme} {Prefix}";

        return [.. Strings(connection, query)
            .Where(symbol => symbol.StartsWith(mine, System.StringComparison.Ordinal))
            .Select(symbol => symbol[mine.Length..])];
    }

    private static List<string> Strings(FjordConnection connection, string query) =>
        [.. connection.Query(query).Rows.Select(row => Assert.IsType<FjordValue.Str>(row).Value)];
}
