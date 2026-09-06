using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Every query a smoke run demonstrates is one the server accepts.</b>
/// </para>
/// <para>
/// These run only after an index run, and a refusal only ever reaches a person reading the
/// tail of one. So a query carrying a type error — <c>src.Symbol S</c> against an <c>S</c>
/// already bound to a symbol *fact*, which asks for a fact whose string key is a fact — was
/// printed as <c>refused (BadQuery)</c> on every run for a release, and was found by
/// somebody reading output rather than by a red suite. Every indexer test passes
/// <c>--no-smoke</c>, so nothing else here executes one.
/// </para>
/// </summary>
public sealed class SmokeQueryTests
{
    /// <summary>
    /// <b>Each of them typechecks, plans and runs against a real database.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted per query rather than over the set, so a failure names the one that broke
    /// and prints the sigla behind it — the same reading the corpus tests give a
    /// classification.
    /// </para>
    /// <para>
    /// <b>Against an indexed fixture rather than an empty database</b>, because a query
    /// over nothing is planned and answers no rows whatever its shape: a join whose two
    /// sides cannot meet is a clean run when neither side has facts in it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_smoke_query_is_one_the_server_accepts()
    {
        using var fixture = Fixture.Copy("arity");
        using var server = FjordServer.Serving("smoke", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Arity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//smoke",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "smoke", DotnetIndex.Schema);

        // `Result` is a type the fixture declares, so the queries that take a sample name
        // are asked about something the database holds.
        var queries = Program.SmokeQueries("Result").ToList();

        // A guard over an empty list is a guard over nothing.
        Assert.True(queries.Count >= 5, $"only {queries.Count} smoke queries");

        foreach (var (what, sigla) in queries)
        {
            var refused = Record.Exception(() => connection.CountRows(sigla));

            Assert.True(
                refused is null,
                $"the smoke query `{what}` was refused:\n  {sigla}\n  {refused?.Message}");
        }
    }

    /// <summary>
    /// <b>The queries that name a declaration are dropped when the walk found none.</b>
    /// </summary>
    /// <remarks>
    /// A run over a repository with nothing to sample would otherwise interpolate a null
    /// into three of them and ask the server about a declaration called "".
    /// </remarks>
    [Fact]
    public void A_run_with_nothing_to_sample_asks_only_what_it_can()
    {
        var sampled = Program.SmokeQueries("Result").Count();
        var unsampled = Program.SmokeQueries(null).ToList();

        Assert.True(unsampled.Count < sampled);
        Assert.All(unsampled, query => Assert.DoesNotContain("\"\"", query.Sigla));
    }
}
