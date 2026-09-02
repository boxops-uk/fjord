using System;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The new target schema, and the claim that lets it arrive a layer at a time.</b>
/// </para>
/// <para>
/// <c>schemas/dotnet.sigla</c> resolves to 67 predicates. <see cref="DotnetIndex"/>
/// declares ten of them — the layers whose emission exists — and that is legal rather
/// than provisional: predicate ids are the client's own, a block header carries the
/// predicate's name, and a nested reference takes its predicate from the field's declared
/// target. Nothing positional crosses the wire, so a client states what it writes.
/// </para>
/// <para>
/// This is the test that would fail if that were wrong, and it is worth failing on its
/// own: the alternative reading — that a client must state the whole database — turns
/// every layer of the switch into one 67-predicate paste with no gate until the end.
/// </para>
/// </summary>
public sealed class DotnetIndexTests
{
    [Fact]
    public void A_client_may_declare_only_the_predicates_it_writes()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");

        // Ten declarations against a sixty-seven predicate database. The handshake
        // compares the *database's* fingerprint, which this client carries.
        using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

        Assert.Equal(DotnetIndex.SchemaFingerprint, connection.Hello.SchemaFingerprint);

        // Fewer than the database's sixty-seven, which is the whole claim. The exact
        // number grows with each layer's emission and is not what this asserts.
        Assert.Equal(DotnetIndex.Predicates.Length, DotnetIndex.Schema.Predicates.Count);
        Assert.InRange(DotnetIndex.Schema.Predicates.Count, 1, 66);
    }

    /// <summary>
    /// **A stale fingerprint is still refused**, so declaring less does not mean asserting
    /// less: the provenance claim is exactly as strong as it was.
    /// </summary>
    [Fact]
    public void A_partial_statement_with_the_wrong_fingerprint_is_refused()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");

        var stale = new FjordSchema(
            DotnetIndex.Schema.Predicates,
            DotnetIndex.SchemaFingerprint ^ 0xFF);

        var refused = Assert.Throws<FjordServerException>(() =>
            FjordConnection.Connect(server.Socket, "dotnet", stale));

        Assert.Contains("schema mismatch", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Every declared predicate round-trips against the server's own schema.** The
    /// fingerprint asserts provenance and nothing about the shapes; what proves a
    /// transcription is writing a fact of each predicate and reading it back, because the
    /// server decodes against its statement rather than this one.
    /// </summary>
    [Fact]
    public void Every_declared_predicate_round_trips_through_the_server()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

        var file = DotnetIndex.FileFact("src/Thing.cs");

        Write(connection, DotnetIndex.File, file);
        Write(connection, DotnetIndex.Symbol, DotnetIndex.SymbolFact(
            "scip-csharp nuget Fixture 1.0.0.0 Fixture/Thing#"));
        Write(connection, DotnetIndex.Setting,
            DotnetIndex.SettingFact("position-encoding", "utf16"));
        Write(connection, DotnetIndex.FileLanguage, new FjordFact(
            DotnetIndex.FileLanguage,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(FjordValue.Alt(1u, FjordValue.Rec()))));
        Write(connection, DotnetIndex.FileDigest, new FjordFact(
            DotnetIndex.FileDigest,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(FjordValue.Of(new string('a', 64)))));
        Write(connection, DotnetIndex.FileOrigin, new FjordFact(
            DotnetIndex.FileOrigin,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(FjordValue.Of("github.com/boxops-uk/fjord"), FjordValue.Of("1d89165"))));
        Write(connection, DotnetIndex.FileInfo, new FjordFact(
            DotnetIndex.FileInfo,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(
                FjordValue.Of(12L),
                FjordValue.Of(3L),
                FjordValue.Alt(1u, FjordValue.Rec()))));
        Write(connection, DotnetIndex.FileLine, new FjordFact(
            DotnetIndex.FileLine,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), FjordValue.Of(1L)),
            FjordValue.Rec(
                FjordValue.Of("class A"),
                FjordValue.Of(0L),
                FjordValue.Of(7L),
                FjordValue.Of(0L))));
        Write(connection, DotnetIndex.FileLineAt, new FjordFact(
            DotnetIndex.FileLineAt,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(file)),
                FjordValue.Of(0L),
                FjordValue.Of(1L))));
        Write(connection, DotnetIndex.FileLineStyles, new FjordFact(
            DotnetIndex.FileLineStyles,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), FjordValue.Of(1L)),
            FjordValue.Rec(FjordValue.Of(new byte[] { 0x00, 0x00, 0x07, 0x01, 0x00 }.AsMemory()))));

        // One question per predicate. A shape this client got wrong would have been
        // refused at the write above; this is the other half — that what came back is
        // what a consumer asks for.
        foreach (var query in new[]
        {
            "F where src.File F",
            "S where src.Symbol S",
            "X.value where X = src.FileLanguage _",
            "X.value where X = src.FileDigest _",
            "X.value where X = src.FileOrigin _",
            "X.value where X = src.FileInfo _",
            "X.value where X = src.FileLine {file = F, line = 1}",
            "{s = S, l = L} where src.FileLineAt {file = F, start = S, line = L}",
            "X.value where X = src.FileLineStyles {file = F, line = 1}",
            "V where config.Setting {dimension = \"position-encoding\", value = V}",
        })
        {
            Assert.NotEmpty(connection.Query(query).Rows);
        }
    }

    /// <summary>
    /// **The project graph, written and asked back.** Sixteen predicates whose shapes this
    /// side states independently: a wrong one is refused at the write, because the server
    /// decodes against its own statement. The two answers asserted at the end are the ones
    /// `code.sigla`'s build layer could not give — a project identified by its file alone,
    /// and an edge between two projects.
    /// </summary>
    [Fact]
    public void The_msbuild_layer_round_trips_through_the_server()
    {
        using var server = FjordServer.Serving("dotnet", "dotnet.sigla");
        using var connection = FjordConnection.Connect(server.Socket, "dotnet", DotnetIndex.Schema);

        var solutionFile = DotnetIndex.FileFact("Boxops.slnx");
        var appFile = DotnetIndex.FileFact("App/App.csproj");
        var libFile = DotnetIndex.FileFact("Lib/Lib.csproj");
        var sourceFile = DotnetIndex.FileFact("App/Program.cs");

        var solution = DotnetIndex.SolutionFact(solutionFile);
        var app = DotnetIndex.ProjectFact(
            appFile,
            targetFramework: "net10.0",
            sdk: "Microsoft.NET.Sdk",
            outputType: "Exe",
            assemblyName: "App",
            rootNamespace: "Boxops.App");
        // Every field MSBuild left unset is `nothing`, which is a different fact from the
        // empty string and the reason the value side is six `MaybeString`s.
        var lib = DotnetIndex.ProjectFact(libFile, targetFramework: "net10.0");
        var assembly = DotnetIndex.AssemblyFact("App");
        var external = DotnetIndex.AssemblyFact("System.Runtime");
        var package = DotnetIndex.PackageFact("Newtonsoft.Json", "13.0.3");

        Write(connection, DotnetIndex.Solution, solution);
        Write(connection, DotnetIndex.Project, app);
        Write(connection, DotnetIndex.Project, lib);
        Write(connection, DotnetIndex.Assembly, assembly);
        Write(connection, DotnetIndex.Assembly, external);
        Write(connection, DotnetIndex.Package, package);
        Write(connection, DotnetIndex.SolutionToProject, DotnetIndex.SolutionToProjectFact(solution, app));
        Write(connection, DotnetIndex.ProjectToSolution, DotnetIndex.ProjectToSolutionFact(app, solution));
        Write(connection, DotnetIndex.ProjectToSourceFile, DotnetIndex.ProjectToSourceFileFact(app, sourceFile));
        Write(connection, DotnetIndex.SourceFileToProject, DotnetIndex.SourceFileToProjectFact(sourceFile, app));
        Write(connection, DotnetIndex.ProjectReference, DotnetIndex.ProjectReferenceFact(app, lib));
        Write(connection, DotnetIndex.ProjectReferencedBy, DotnetIndex.ProjectReferencedByFact(lib, app));
        Write(connection, DotnetIndex.PackageReference, DotnetIndex.PackageReferenceFact(app, package, "13.0.*"));
        Write(connection, DotnetIndex.PackageDependent, DotnetIndex.PackageDependentFact(package, app));
        Write(connection, DotnetIndex.AssemblyReference, DotnetIndex.AssemblyReferenceFact(app, external));
        Write(connection, DotnetIndex.AssemblyDependent, DotnetIndex.AssemblyDependentFact(external, app));
        Write(connection, DotnetIndex.Compilation, DotnetIndex.CompilationFact(assembly, "net10.0", app));
        Write(connection, DotnetIndex.ProjectCompilation, DotnetIndex.ProjectCompilationFact(app, "net10.0", assembly));

        foreach (var query in new[]
        {
            "S where msbuild.Solution {file = S}",
            "X.value where X = msbuild.Project {file = F}",
            "A where msbuild.Assembly {name = A}",
            "{n = N, v = V} where msbuild.Package {name = N, version = V}",
            "{s = S, p = P} where msbuild.SolutionToProject {solution = S, project = P}",
            "{p = P, s = S} where msbuild.ProjectToSolution {project = P, solution = S}",
            "{p = P, f = F} where msbuild.ProjectToSourceFile {project = P, src = F}",
            "{f = F, p = P} where msbuild.SourceFileToProject {src = F, project = P}",
            "X.value where X = msbuild.PackageReference {project = P, package = K}",
            "{k = K, p = P} where msbuild.PackageDependent {package = K, project = P}",
            "{p = P, a = A} where msbuild.AssemblyReference {project = P, assembly = A}",
            "{a = A, p = P} where msbuild.AssemblyDependent {assembly = A, project = P}",
            "{a = A, f = F, p = P} where msbuild.Compilation {assembly = A, framework = F, project = P}",
            "X.value where X = msbuild.ProjectCompilation {project = P, framework = \"net10.0\"}",
        })
        {
            Assert.NotEmpty(connection.Query(query).Rows);
        }

        // **The edge between two projects**, which the retired build layer never had: it
        // keyed a project on a path string and carried no reference graph at all.
        // `src.File` is key-only, so the path is bound in the key position — asking for
        // `A.value` is `reject/no-value`, which is the diagnostic that found this.
        var edges = connection.Query(
            "{from = FromPath, to = ToPath} where "
            + "A = src.File FromPath; B = src.File ToPath; "
            + "P = msbuild.Project {file = A}; Q = msbuild.Project {file = B}; "
            + "msbuild.ProjectReference {from = P, to = Q}").Rows;

        Assert.Single(edges);

        // **One project file, one project.** Two evaluations differing only in what
        // MSBuild resolved reach the same key, because the key is the file — this is the
        // defect `msbuild.Project` exists to fix, asserted rather than described.
        var reEvaluated = DotnetIndex.ProjectFact(
            libFile,
            targetFramework: "net9.0",
            sdk: "Microsoft.NET.Sdk");

        // Identical is free — the dedup a producer relies on for keeping no book of what
        // it has written.
        var again = connection.Write(DotnetIndex.Project, [lib]);
        Assert.Equal(0UL, again.Created);
        Assert.True(again.Deduped >= 1);

        // Differing is refused rather than resolved: `ops-I4` rejects a conflict order
        // independently, so the *evaluation* being on the value side is what keeps a
        // second SDK from minting a second project — and a genuine disagreement about one
        // project file is loud.
        Assert.Throws<FjordServerException>(() =>
            connection.Write(DotnetIndex.Project, [reEvaluated]));
    }

    /// <summary>Write one fact, and assert something actually landed.</summary>
    /// <remarks>
    /// <c>Created</c> counts the fact <b>and every nested one interned along with it</b> —
    /// a `msbuild.Solution` whose `src.File` is new creates two — so the assertion is that
    /// the write was not a no-op rather than an exact count. What the fact *is* is the
    /// round trip's claim, not this one's.
    /// </remarks>
    private static void Write(FjordConnection connection, uint predicate, FjordFact fact) =>
        Assert.True(connection.Write(predicate, [fact]).Created >= 1);
}
