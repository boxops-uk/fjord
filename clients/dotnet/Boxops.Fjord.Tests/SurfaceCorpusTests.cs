using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The reference corpus is indexed for real, and what it answers is asserted.</b>
/// </para>
/// <para>
/// <see cref="SurfaceCensusTests"/> checks that the corpus <i>claims</i> to cover the language;
/// these tests run the producer over it and check what the database ends up holding. Both are
/// needed, and neither substitutes for the other: a census can be complete about a corpus that
/// indexes to nothing, and a run can succeed over a corpus that covers a third of the language.
/// </para>
/// <para>
/// <b>The corpus is split on one line, and the line is a property of this database rather than a
/// matter of taste.</b> A key here holds one value: two declarations that mint one identity string
/// with different values is a refused write, <c>FactSink</c> latches the refusal, and the next
/// flush throws — so the run dies part-way through and every fact after it in the walk is lost.
/// One such shape anywhere in a solution therefore makes every <i>other</i> shape in that solution
/// unmeasurable. So the 29 projects of <c>Surface.slnx</c> hold everything that indexes to
/// completion, including every shape that answers wrongly or answers nothing, and the five under
/// <c>quarantine/</c> hold one run-killer each and are indexed one at a time, alone, so that the
/// refusal names itself.
/// </para>
/// <para>
/// <b>The refusals are asserted, not skipped, and that is the measurement.</b> Every row of
/// <see cref="A_quarantined_shape_still_refuses_its_write"/> is a defect this producer has today,
/// named by the predicate whose key it collapses. A commit that repairs one has to delete its row
/// and assert completion in its place, in the same commit — so the repair is a diff a reviewer
/// reads as a claim, and the corpus is what made the claim checkable. A shape nobody indexes is a
/// shape nobody notices getting better.
/// </para>
/// </summary>
/// <summary>
/// The corpus, indexed once for the whole class.
/// </summary>
/// <remarks>
/// A run over 29 projects is most of a minute, and two of the claims below want the same database
/// rather than one each. The quarantine theory still runs its own, because the whole point of
/// those is that the run does not finish.
/// </remarks>
public sealed class SurfaceIndex : IDisposable
{
    public SurfaceIndex()
    {
        Corpus = Fixture.Copy("surface");
        Server = FjordServer.Serving("surface", "dotnet.sigla");
        Exit = SurfaceCorpusTests.Run(Corpus, Server, "surface", "Surface.slnx");
    }

    internal Fixture Corpus { get; }

    internal FjordServer Server { get; }

    /// <summary>The run's exit code, asserted by the test whose claim that is.</summary>
    public int Exit { get; }

    internal FjordConnection Connect() =>
        FjordConnection.Connect(Server.Socket, "surface", DotnetIndex.Schema);

    public void Dispose()
    {
        Server.Dispose();
        Corpus.Dispose();
    }
}

public sealed class SurfaceCorpusTests(SurfaceIndex indexed) : IClassFixture<SurfaceIndex>
{
    /// <summary>The corpus, indexed whole, as the producer's own entry point sees it.</summary>
    /// <remarks>
    /// <b>Every layer the producer can write, so the corpus measures all of them.</b>
    /// <c>--styles</c> turns on the per-line semantic-token pass (<c>src.FileLineStyles</c>) and
    /// <c>--repo</c>/<c>--revision</c> supply the provenance <c>src.FileOrigin</c> needs — none of
    /// which a run can infer from a fixture copied out of the tree, since the copy is not a
    /// checkout. A run without them leaves two predicates empty that
    /// <c>PredicateCensusTests</c> classifies as written, so the corpus would be measuring less
    /// than it looks like it is.
    /// </remarks>
    internal static int Run(Fixture fixture, FjordServer server, string database, string source) =>
        Program.Main([
            "--source", fixture.Path(source.Split('/')),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//{database}",
            "--repo", "github.com/boxops-uk/fjord",
            "--revision", "surface",
            "--styles",
            "--no-smoke",
            "--framework", "net10.0",
        ]);

    /// <summary>One string out of a scalar row, or out of a one-field record row.</summary>
    private static string Text(FjordValue row) => row switch
    {
        FjordValue.Str text => text.Value,
        FjordValue.Record record => Assert.IsType<FjordValue.Str>(record.Fields[0]).Value,
        _ => throw new InvalidOperationException($"not a string row: {row.GetType().Name}"),
    };

    /// <summary>
    /// Every <c>.cs</c> file the corpus checks in, as the index would spell it.
    /// </summary>
    /// <remarks>
    /// <c>obj/</c> and <c>bin/</c> are excluded because they are the design-time build's, not the
    /// corpus's — but they are <b>not</b> excluded from the index, and that asymmetry is asserted
    /// rather than hidden: the SDK generates an <c>AssemblyInfo</c> and an
    /// <c>AssemblyAttributes</c> file per project, the compiler is handed them, and so the walk
    /// indexes them. A corpus file missing from the index is a dropped file; an indexed file that
    /// is neither the corpus's nor the SDK's is something nobody meant to walk.
    /// </remarks>
    private static IReadOnlyCollection<string> CheckedIn(Fixture fixture) =>
    [
        .. Directory
            .EnumerateFiles(fixture.Root, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fixture.Root, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("quarantine/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal)),
    ];

    /// <summary>
    /// <b>The whole corpus indexes to completion, and every file it checks in is in the
    /// database.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exit zero is the conflict assertion, the same way <c>LedgerTests</c> reads it: a refused
    /// fact fails the write stream, so a run that reached its end wrote nothing that disagreed
    /// with anything already there. Over a corpus this size that is a real claim — 29 projects and
    /// 446 C# files chosen to exercise every clause of ECMA-334, so anything the producer cannot
    /// spell is in here somewhere.
    /// </para>
    /// <para>
    /// <b>The file set rather than a fact count.</b> A count is a number that moves whenever
    /// anybody adds a doc comment, so it would be a baseline that has to be re-blessed and would
    /// train a reader to re-bless it. What cannot move without something being wrong is that every
    /// file belongs to a project and every project's files were walked.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_whole_corpus_indexes_to_completion()
    {
        Assert.Equal(0, indexed.Exit);

        var fixture = indexed.Corpus;

        using var connection = indexed.Connect();

        var walked = connection
            .Query("F where src.File F")
            .Rows
            .Select(Text)
            .ToHashSet(StringComparer.Ordinal);

        var checkedIn = CheckedIn(fixture).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            [],
            checkedIn.Where(path => !walked.Contains(path)).Order(StringComparer.Ordinal));

        // **And nothing else was walked.** `src.File` is interned by both layers, so a project
        // and a solution file are in it legitimately — the `msbuild` layer keys its facts on
        // them. Two things are worth stating rather than filtering silently. The SDK generates an
        // `AssemblyInfo` and an `AssemblyAttributes` file per project under `obj/`, hands them to
        // the compiler, and the walk indexes them like any other source. And **all 34 project
        // files are here, not the solution's 29**: the build layer describes the projects it finds
        // under the index root, so the five deliberately left out of `Surface.slnx` still get a
        // `msbuild.Project` fact — their C# is not walked, which is what the quarantine is for,
        // but their existence is recorded.
        Assert.Equal(
            [],
            walked
                .Where(path => !path.EndsWith(".csproj", StringComparison.Ordinal))
                .Where(path => !path.EndsWith(".slnx", StringComparison.Ordinal))
                .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
                .Where(path => !checkedIn.Contains(path))
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>Every predicate this producer fills has rows over the corpus, and the three that do not
    /// are the three no corpus here can fill.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what turns the census from a claim into a measurement.</b>
    /// <c>SURFACE.tsv</c> says a clause is exercised and names a file; this asks the database
    /// whether the producer actually wrote anything for it. A predicate
    /// <c>PredicateCensusTests</c> classifies as filled, coming back empty over 29 projects
    /// written to cover the whole language, means one of two things and both are worth a failure:
    /// nothing in the language surface reaches it, or the census claims coverage the corpus does
    /// not have.
    /// </para>
    /// <para>
    /// <b>The three exceptions are asserted to be empty, not skipped.</b> A corpus here takes no
    /// <c>PackageReference</c> — CI has no network, and a fixture that reaches nuget.org is a test
    /// that fails on a train — so <c>msbuild.Package</c>, <c>PackageReference</c> and
    /// <c>PackageDependent</c> cannot be filled from source, and the <c>census</c> fixture is
    /// where they are checked. Stating them as an equality rather than an exclusion makes the pair
    /// self-correcting: a fourth predicate falling empty fails, and so does somebody adding a
    /// package reference and leaving this list alone.
    /// </para>
    /// <para>
    /// Two predicates are classified <c>Excused</c> and so are outside this claim, both because
    /// the producer declines to write them rather than because the corpus misses them:
    /// <c>csharp.Local</c>, since a local gets no global name by decision, and
    /// <c>csharp.FunctionPointerType</c>, which cannot be keyed at all — its <c>signature</c> is a
    /// <c>csharp.Method</c> whose key leads with a containing type, and a function pointer's
    /// signature symbol has none. The corpus declares 34 function pointers and confirms it: the
    /// predicate is empty and the drop is counted instead.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_predicate_this_producer_fills_has_rows_over_the_corpus()
    {
        Assert.Equal(0, indexed.Exit);

        using var connection = indexed.Connect();

        string[] needAPackageReference =
        [
            "msbuild.Package",
            "msbuild.PackageDependent",
            "msbuild.PackageReference",
        ];

        var empty = PredicateCensusTests.Filled
            .Select(DotnetIndex.NameOf)
            .Where(name => connection.Query($"X where X = {name} _").Rows.Count == 0)
            .Order(StringComparer.Ordinal);

        Assert.Equal([.. needAPackageReference.Order(StringComparer.Ordinal)], empty);
    }

    /// <summary>
    /// <b>Each quarantined shape still refuses the write it is quarantined for, on the predicate
    /// it is quarantined for.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which predicate is the whole content of the claim.</b> Three of these five conflict on
    /// <c>codemarkup.Definition</c>, keyed <c>{symbol, file}</c>, so they need two declarations of
    /// one symbol in one file. The other two conflict on <c>codemarkup.SymbolInfo</c>, keyed on
    /// the symbol alone — which is why they fire <i>across</i> files, and why a fix that dedupes
    /// <c>Definition</c> per file would leave them live. A test asserting only "the run fails"
    /// would pass for the wrong reason after such a fix.
    /// </para>
    /// <para>
    /// The failure arrives as an <see cref="InvalidOperationException"/> from the flush that
    /// follows the refusal, wrapping the server's own <c>Conflict</c>. Both halves are asserted:
    /// the predicate names which key collapsed, and the word <c>Conflict</c> distinguishes a
    /// refused write from any other way a run can die — a producer that started throwing on these
    /// shapes instead of refusing them would be a different defect, and this would say so.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("Arity", "codemarkup.Definition")]
    [InlineData("Terms", "codemarkup.Definition")]
    [InlineData("Partial", "codemarkup.Definition")]
    [InlineData("Ordinal", "codemarkup.SymbolInfo")]
    [InlineData("FileLocal", "codemarkup.SymbolInfo")]
    public void A_quarantined_shape_still_refuses_its_write(string project, string predicate)
    {
        using var fixture = Fixture.Copy("surface");
        using var server = FjordServer.Serving("quarantine", "dotnet.sigla");

        var failure = Record.Exception(
            () => Run(fixture, server, "quarantine", $"quarantine/{project}/{project}.csproj"));

        Assert.NotNull(failure);

        var said = string.Join(" | ", Chain(failure));

        Assert.Contains(predicate, said, StringComparison.Ordinal);
        Assert.Contains("Conflict", said, StringComparison.Ordinal);
    }

    /// <summary>Every message in an exception chain, including an aggregate's branches.</summary>
    private static IEnumerable<string> Chain(Exception failure)
    {
        if (failure is AggregateException aggregate)
        {
            foreach (var message in aggregate.Flatten().InnerExceptions.SelectMany(Chain))
            {
                yield return message;
            }

            yield break;
        }

        for (var here = failure; here is not null; here = here.InnerException)
        {
            yield return here.Message;
        }
    }
}
