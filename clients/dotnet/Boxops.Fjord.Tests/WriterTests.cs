using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>What the writer count may and may not change.</b>
/// </para>
/// <para>
/// The number itself is a measurement and lives in the commit that sets the default —
/// `bench/FINDINGS.md` is closed, and one fresh entry in a closed register would be the
/// only number in it a reader could mistake for current. What lives here is the half that
/// can rot: a writer count is a throughput knob, so the moment it changes *what is
/// written* it has stopped being one.
/// </para>
/// </summary>
public sealed class WriterTests
{
    /// <summary>
    /// <b>Eight writers write what one writer writes.</b>
    /// </summary>
    /// <remarks>
    /// The whole permission to tune the knob. Each writer holds its own connection and the
    /// server excludes per key rather than per database, so eight of them interleave their
    /// blocks — and the facts must not care. `LedgerTests` says the same thing about the
    /// sealed identity across four axes; this says it about the counts, cheaply enough to
    /// run at eight.
    /// </remarks>
    [Fact]
    public void The_facts_do_not_depend_on_the_writer_count()
    {
        using var fixture = Fixture.Copy("ledger");
        using var server = FjordServer.ServingAll("dotnet.sigla", "writers-1", "writers-8");

        long[] Index(string database, int writers)
        {
            var code = Program.Main([
                "--sln", fixture.Path("Ledger.slnx"),
                "--root", fixture.Root,
                "--at", $"{server.Socket}//{database}",
                "--writers", writers.ToString(),
                "--no-smoke",
            ]);

            Assert.Equal(0, code);

            using var connection = FjordConnection.Connect(
                server.Socket, database, DotnetIndex.Schema);

            return [.. DotnetIndex.Predicates.Select(predicate =>
                (long)connection.Query($"X where X = {DotnetIndex.NameOf(predicate)} _").Rows.Count)];
        }

        var one = Index("writers-1", 1);

        Assert.True(one.Sum() > 0, "the corpus indexed to nothing");
        Assert.Equal(one, Index("writers-8", 8));
    }

    /// <summary>
    /// <b><c>--emit</c> is byte-identical across two runs, and it takes two things.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// One writer was the half already there: several would interleave their blocks into
    /// one file and make its contents depend on scheduling. The other half is the walk —
    /// the block order is the order the walk reached things, so eight walker threads
    /// produced a different file every run with exactly the same facts in it, which is the
    /// worst shape a golden can have.
    /// </para>
    /// <para>
    /// Asserted at <c>--jobs 8</c> on purpose. At one job it is byte-identical either way,
    /// which is how it stayed broken.
    /// </para>
    /// </remarks>
    [Fact]
    public void An_emitted_file_is_the_same_bytes_however_many_threads_walked()
    {
        using var fixture = Fixture.Copy("ledger");

        byte[] Emit(string name, int jobs)
        {
            var path = fixture.Path($"{name}.bin");

            Assert.Equal(0, Program.Main([
                "--sln", fixture.Path("Ledger.slnx"),
                "--root", fixture.Root,
                "--dry-run", "--no-smoke",
                "--jobs", jobs.ToString(),
                "--emit", path,
            ]));

            return File.ReadAllBytes(path);
        }

        var eight = Emit("eight-a", 8);

        Assert.NotEmpty(eight);
        Assert.Equal(eight, Emit("eight-b", 8));
        Assert.Equal(eight, Emit("one", 1));
    }
}
