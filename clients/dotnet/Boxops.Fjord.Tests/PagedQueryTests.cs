using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A result read a page at a time, and a connection that survives being abandoned
/// half way through one.</b>
/// </para>
/// <para>
/// <see cref="FjordConnection.Query"/> collects every row before its caller sees the
/// first, so a caller that wanted five rows of a hundred-million-row result held a
/// hundred million — which is how an indexer run was killed at 20.6 GB by a smoke query
/// that printed five rows. The paged frames exist for exactly this and were not
/// implemented here.
/// </para>
/// </summary>
public sealed class PagedQueryTests
{
    /// <summary>How many facts these tests write, and it is not a round number on purpose.</summary>
    /// <remarks>
    /// Coprime with every page size used below, so no test can pass because its last page
    /// happened to land exactly on the end of the result — which is the case the resume
    /// token exists to tell apart from a short page.
    /// </remarks>
    private const int Facts = 47;

    private const string AllFiles = "P where src.File P";

    /// <summary>
    /// <b>A count is the number of rows, and costs no row.</b>
    /// </summary>
    [Fact]
    public void A_count_agrees_with_the_rows_the_query_answers()
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);
        Seed(connection);

        Assert.Equal(Facts, connection.Query(AllFiles).Rows.Count);
        Assert.Equal(Facts, connection.CountRows(AllFiles));
    }

    /// <summary>
    /// <b>A page carries at most what was asked for, and the token is the terminator.</b>
    /// </summary>
    /// <remarks>
    /// The last page sends no resume token even though it is full or short — "no token"
    /// is how a caller knows it has seen everything. A caller that instead paged until a
    /// page came back shorter than the limit would ask once more and be told nothing, and
    /// one that paged while rows kept arriving would never stop.
    /// </remarks>
    [Fact]
    public void Paging_ends_when_the_token_does_and_not_when_a_page_is_short()
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);
        Seed(connection);

        var seen = new List<string>();
        byte[]? cursor = null;
        var pages = 0;

        do
        {
            var page = connection.Page(AllFiles, limit: 10, cursor);
            Assert.True(page.Rows.Count <= 10, $"a page of {page.Rows.Count} rows against a limit of 10");

            seen.AddRange(page.Rows.Select(row => Assert.IsType<FjordValue.Str>(row).Value));
            cursor = page.Resume;
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(Facts, seen.Count);
        Assert.Equal(5, pages);
    }

    /// <summary>
    /// <b>Paged rows are the unpaged rows, in the same order.</b>
    /// </summary>
    /// <remarks>
    /// The property that makes paging a way of reading a result rather than a different
    /// question: a token resumes where the last page stopped, so no row is dropped at a
    /// page boundary and none is handed out twice.
    /// </remarks>
    [Fact]
    public void Streaming_answers_the_same_rows_in_the_same_order_as_one_read()
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);
        Seed(connection);

        var whole = connection.Query(AllFiles).Rows
            .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
            .ToList();

        foreach (var pageSize in new[] { 1, 3, 10, Facts, Facts + 1 })
        {
            var paged = connection.Rows(AllFiles, pageSize)
                .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
                .ToList();

            Assert.Equal(whole, paged);
        }
    }

    /// <summary>
    /// <b>Taking a few rows and walking away leaves the connection usable.</b>
    /// </summary>
    /// <remarks>
    /// <b>This is the trap the cancel exists for, and the reason the eager read was safe.</b>
    /// Stopping mid-page leaves that page's remaining rows on a socket every stream shares,
    /// so without the cancel-and-drain the *next* query reads this one's tail — and reads it
    /// as its own rows, which is a wrong answer rather than an error. The assertion is
    /// therefore what comes back afterwards, not what came back from the <c>Take</c>.
    /// </remarks>
    [Fact]
    public void A_result_abandoned_mid_page_leaves_the_connection_clean()
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);
        Seed(connection);

        // One row of a result that spans many pages: the rest of this page is in flight.
        var first = connection.Rows(AllFiles, pageSize: 5).Take(1).ToList();
        Assert.Single(first);

        // The connection is its own again — every later use of it is the real assertion.
        Assert.Equal(Facts, connection.CountRows(AllFiles));
        Assert.Equal(Facts, connection.Query(AllFiles).Rows.Count);
        Assert.Equal(Facts, connection.Rows(AllFiles).Count());
    }

    /// <summary>
    /// <b>Two results open at once are refused rather than left to decode each other.</b>
    /// </summary>
    /// <remarks>
    /// This client reads frames in arrival order and does not demultiplex on the stream id,
    /// so a second result would take rows belonging to the first. Refused where it can be
    /// named, because the alternative is two callers each receiving some of the other's
    /// rows and neither being told.
    /// </remarks>
    [Fact]
    public void A_second_result_opened_while_one_is_streaming_is_refused()
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);
        Seed(connection);

        using var open = connection.Rows(AllFiles, pageSize: 2).GetEnumerator();
        Assert.True(open.MoveNext());

        var refused = Assert.Throws<InvalidOperationException>(
            () => connection.Rows(AllFiles).First());

        Assert.Contains("already open", refused.Message);
    }

    /// <summary>
    /// <b>A page size the caller cannot have meant is refused at the call.</b>
    /// </summary>
    /// <remarks>
    /// Zero is the wire's "no limit", so passing it through would quietly turn a paged read
    /// into the unpaged one this exists to replace — the whole result in memory, which is
    /// the defect rather than an edge of it.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_page_size_of_nothing_is_refused(int pageSize)
    {
        using var server = FjordServer.Serving("paged", "dotnet.sigla");
        using var connection = Connect(server);

        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Rows(AllFiles, pageSize));
    }

    private static FjordConnection Connect(FjordServer server) =>
        FjordConnection.Connect(server.Socket, "paged", DotnetIndex.Schema, SessionMode.ReadWrite);

    /// <summary>
    /// <see cref="Facts"/> files, named so that their sort order is their write order —
    /// which is what lets the equivalence test compare two readings position by position.
    /// </summary>
    private static void Seed(FjordConnection connection)
    {
        var files = Enumerable
            .Range(0, Facts)
            .Select(each => DotnetIndex.FileFact($"src/File{each:D4}.cs"))
            .ToArray();

        Assert.True(connection.Write(DotnetIndex.File, files).Created >= Facts);
    }
}
