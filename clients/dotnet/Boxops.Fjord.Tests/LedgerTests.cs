using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The ledger: one frozen corpus, indexed four ways, sealing to one identity.</b>
/// </para>
/// <para>
/// A sealed database's identity is a hash over the facts it holds, so it is the strongest
/// single statement this producer can make about itself — an identity that moved with
/// <c>--jobs</c> would mean the index depended on the machine that built it, and every
/// number ever recorded against it would be a number about that machine.
/// </para>
/// <para>
/// <b>The gate revision 2 wrote could not be run.</b> It asked for <c>Conflicts == 0</c>
/// against a counter the semantic key removed, and <c>M == 0</c> against a counter that
/// exists nowhere. Nothing replaces the first: <c>ops-I4</c> makes a conflicting fact a
/// rejected one and the sink latches a rejection into a failed run, so <i>the run
/// completing</i> is the conflict assertion. What replaces the second is
/// <c>Inexpressible</c>, which is the counter that once caught a fixture indexing one type
/// out of nine — the failure a ledger most needs to be told about, because a nearly-empty
/// database seals to a perfectly stable identity.
/// </para>
/// </summary>
public sealed class LedgerTests
{
    /// <summary>The axes the identity must not depend on.</summary>
    private static readonly (int Jobs, int Writers)[] Axes =
    [
        (1, 1),
        (8, 1),
        (1, 4),
        (8, 4),
    ];

    private static string Name(int jobs, int writers) => $"ledger-j{jobs}-w{writers}";

    /// <summary>What `fjord describe --format json` says about a sealed database.</summary>
    private static JsonElement Described(string root, string database) =>
        JsonDocument.Parse(FjordServer.Run(root, "describe", database, "--format", "json"))
            .RootElement;

    [Fact]
    public void One_corpus_indexed_four_ways_seals_to_one_identity()
    {
        using var fixture = Fixture.Copy("ledger");
        using var server = FjordServer.Serving(
            "dotnet.sigla",
            [.. Axes.Select(axis => Name(axis.Jobs, axis.Writers))]);

        var counts = new List<IReadOnlyDictionary<string, long>>();

        foreach (var (jobs, writers) in Axes)
        {
            var database = Name(jobs, writers);

            var code = Program.Main([
                "--source", fixture.Path("Ledger.slnx"),
                "--root", fixture.Root,
                "--at", $"{server.Socket}//{database}",
                "--jobs", jobs.ToString(),
                "--writers", writers.ToString(),
                "--no-smoke",
            ]);

            // **The run completing is the conflict assertion.** A conflicting fact is
            // refused by the server, and `FactSink` latches a refusal into a failure that
            // the next flush throws — so a run that reached its end wrote nothing that
            // disagreed with anything already there.
            Assert.Equal(0, code);

            counts.Add(Stored(server.Socket, database));
        }

        var identities = new List<ulong>();
        var facts = new List<long>();

        foreach (var (jobs, writers) in Axes)
        {
            var database = Name(jobs, writers);

            FjordServer.Run(server.Root, "finish", database);

            var described = Described(server.Root, database);
            identities.Add(described.GetProperty("content_fingerprint").GetUInt64());
            Assert.Equal("complete", described.GetProperty("status").GetString());
            facts.Add(described.GetProperty("facts").GetInt64());
        }

        // The primary assertion, and the reason for the other three.
        Assert.Single(identities.Distinct());
        Assert.Single(facts.Distinct());
        Assert.True(facts[0] > 0, "the corpus sealed to nothing");

        // Secondary: per-predicate stored counts, queried back. An identity is one number
        // and says nothing about *where* two indexes differ; these say which predicate.
        foreach (var stored in counts.Skip(1))
        {
            Assert.Equal(counts[0], stored);
        }
    }

    /// <summary>
    /// <b>Every file belongs to a project, and every declaration is expressible.</b>
    /// </summary>
    /// <remarks>
    /// The two ways a ledger run can be stable and wrong. A file no project compiles gets
    /// no <c>msbuild.SourceFileToProject</c> edge, and a declaration whose type this layer
    /// cannot key is dropped — both quietly, both producing a smaller index that seals to a
    /// perfectly reproducible identity. The fixture is built to make both zero, so a
    /// number other than zero is a defect rather than a property of the corpus.
    /// </remarks>
    [Fact]
    public void The_frozen_corpus_loses_no_file_and_no_declaration()
    {
        using var fixture = Fixture.Copy("ledger");

        var solution = Loader.Load(
            new Options { Source = fixture.Path("Ledger.slnx"), Jobs = 2 },
            fixture.Root,
            TextWriter.Null);

        var target = Assert.Single(solution.Targets);
        var recorder = new SourceWalkTests.Recorder();
        var options = new Options { Source = fixture.Path("Ledger.slnx") };

        // Fully qualified: from `Boxops.Fjord.Tests`, the bare name resolves to the
        // sibling *namespace* rather than the type in it.
        Boxops.Fjord.Indexer.Indexer indexer;

        using (var sink = new FactSink(options, [recorder]))
        {
            indexer = new Boxops.Fjord.Indexer.Indexer(options, sink, fixture.Root, target.Build);

            foreach (var project in target.Projects)
            {
                indexer.Index(project.Compile()!, project.Roslyn);
            }

            sink.Drain();
        }

        Assert.Equal(0, indexer.Unattributed);
        Assert.Equal(0, indexer.Inexpressible);
        Assert.True(indexer.Declarations > 30, $"{indexer.Declarations} declarations");
    }

    /// <summary>Every predicate's stored row count, asked of the database itself.</summary>
    /// <remarks>
    /// <c>--count</c> through a query rather than the sink's own tally: the sink counts
    /// what it <i>queued</i>, which is the same number however the server took it. What is
    /// wanted here is what the database ended up holding.
    /// </remarks>
    private static IReadOnlyDictionary<string, long> Stored(string socket, string database)
    {
        using var connection = FjordConnection.Connect(socket, database, DotnetIndex.Schema);

        var stored = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var predicate in DotnetIndex.Predicates)
        {
            var name = DotnetIndex.NameOf(predicate);
            stored[name] = connection.Query($"X where X = {name} _").Rows.Count;
        }

        return stored;
    }
}
