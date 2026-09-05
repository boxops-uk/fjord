using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Two projects producing one assembly: the run finishes, and the one left out is
/// named.</b>
/// </para>
/// <para>
/// `src/coreclr/System.Private.CoreLib` and `src/mono/System.Private.CoreLib` are two real
/// implementations of one assembly for two runtimes, and a run over `dotnet/runtime`
/// reaching both died on `codemarkup.SymbolInfo`: the package coordinate in a
/// <c>src.Symbol</c> is the assembly identity, theirs is the same string, so every symbol
/// they both declare is one key wanted twice with two values (<c>ops-I4</c>).
/// </para>
/// <para>
/// <b>Not the reference-assembly case, and not its answer.</b> That one drops a
/// restatement and loses nothing. This drops source somebody wrote, so it is reported by
/// name and <c>--strict</c> fails on it — the reading a project that would not build
/// already gets.
/// </para>
/// </summary>
public sealed class SharedAssemblyTests
{
    /// <summary>
    /// <b>The run finishes, one implementation is indexed, and the other is named.</b>
    /// </summary>
    /// <remarks>
    /// The exit code is the conflict assertion, as in <c>ArityPairTests</c>: before this
    /// the run returned 134 with <c>Conflict: predicate ... already holds a different fact
    /// under this key</c>, part-way through a write.
    /// </remarks>
    [Fact]
    public void Two_projects_producing_one_assembly_index_to_completion()
    {
        using var fixture = Fixture.Copy("identity");
        using var server = FjordServer.Serving("identity", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--source", fixture.Path("Identity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//identity",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "identity", DotnetIndex.Schema);

        // One `Rotor`, with one documentation comment, rather than two facts fighting.
        var docs = connection.Query(
            "{info = I.value} where I = codemarkup.SymbolInfo {symbol = S}; "
            + $"S = src.Symbol \"{ScipSymbols.Scheme} nuget Engine 1.0.0.0 Engine/Rotor#Spin().\"")
            .Rows;

        Assert.Single(docs);
    }

    /// <summary>
    /// <b>Both projects are in the build graph; only one of them is walked.</b>
    /// </summary>
    /// <remarks>
    /// The same decision <c>--max-files</c> and the reference-assembly rule already make.
    /// What projects a repository has is a fact about the repository — deleting the one
    /// that was not walked would answer "what builds here" wrongly rather than partially.
    /// </remarks>
    [Fact]
    public void The_project_left_out_is_still_in_the_build_graph()
    {
        using var fixture = Fixture.Copy("identity");
        using var server = FjordServer.Serving("identity", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--source", fixture.Path("Identity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//identity",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "identity", DotnetIndex.Schema);

        var projects = connection.Query("P where msbuild.Project {file = F}; F = src.File P")
            .Rows
            .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
            .ToList();

        Assert.Contains(projects, path => path.Contains("Engine/fast/Engine.csproj"));
        Assert.Contains(projects, path => path.Contains("Engine/portable/Engine.csproj"));

        // And exactly one of the two source files carries declarations.
        var files = connection.Query(
            "P where codemarkup.Definition {symbol = S, file = F}; F = src.File P")
            .Rows
            .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
            .Distinct()
            .ToList();

        Assert.Single(files);
    }

    /// <summary>
    /// <b><c>--strict</c> makes it a failure, because the index is not complete.</b>
    /// </summary>
    /// <remarks>
    /// Off by default for the reason a project that would not build is: a developer
    /// indexing a repository wants the rest of it. On for CI, where "the index is
    /// complete" should be a check rather than a line somebody reads.
    /// </remarks>
    [Fact]
    public void Strict_fails_a_run_that_left_an_implementation_out()
    {
        using var fixture = Fixture.Copy("identity");
        using var server = FjordServer.Serving("identity", "dotnet.sigla");

        Assert.Equal(1, Program.Main([
            "--source", fixture.Path("Identity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//identity",
            "--strict",
            "--no-smoke",
        ]));
    }

    /// <summary>
    /// <b>A reference assembly never claims the identity, so the implementation beside it
    /// is still the one walked.</b>
    /// </summary>
    /// <remarks>
    /// <b>The two rules interact, and their order is the whole of it.</b> `refimpl` lists
    /// its `ref/` project first, so a rule that claimed an assembly for whichever project
    /// came first would keep the reference assembly and leave the implementation out —
    /// exit 0, every documentation query answered with the empty string, and nothing said.
    /// Asserted here rather than left to the order of two `if`s in one method.
    /// </remarks>
    [Fact]
    public void A_reference_assembly_listed_first_does_not_claim_the_assembly()
    {
        using var fixture = Fixture.Copy("refimpl");
        using var server = FjordServer.Serving("refimpl", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--source", fixture.Path("RefImpl.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//refimpl",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "refimpl", DotnetIndex.Schema);

        // The implementation's documentation, which only the `src/` half carries.
        var info = Assert.Single(connection.Query(
            "{info = I.value} where I = codemarkup.SymbolInfo {symbol = S}; "
            + $"S = src.Symbol \"{ScipSymbols.Scheme} nuget Widgets 1.0.0.0 Widgets/Gadget#Spin().\"")
            .Rows);

        var doc = Assert.IsType<FjordValue.Str>(
            Assert.IsType<FjordValue.Record>(
                Assert.IsType<FjordValue.Record>(info).Fields[0]).Fields[1]).Value;

        Assert.Contains("Spins it", doc);
    }
}
