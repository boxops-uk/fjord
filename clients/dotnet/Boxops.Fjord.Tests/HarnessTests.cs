using Boxops.Fjord.Client;
using Xunit;
using Boxops.Fjord.Indexer;

namespace Boxops.Fjord.Tests;

/// <summary>
/// **The harness proves itself before anything relies on it.**
/// </summary>
/// <remarks>
/// Run 0.5's claim is that the .NET side has a test project, a CI job that runs it, and a
/// stated way for a test to acquire a server. The first two are the build; this is the
/// third, and it is asserted rather than described — a harness that could not actually
/// start a server would make every gate above it green for the wrong reason.
/// </remarks>
public class HarnessTests
{
    [Fact]
    public void A_test_can_start_a_server_write_facts_and_read_them_back()
    {
        using var server = FjordServer.Serving("code", "code.sigla");

        using var connection = FjordConnection.Connect(
            server.Socket,
            "code",
            CodeIndex.Schema);

        // The handshake is the first assertion: the fingerprint this client carries is the
        // one the database holds, or the connection above would already have been refused.
        Assert.Equal(CodeIndex.SchemaFingerprint, connection.Hello.SchemaFingerprint);

        var file = CodeIndex.FileFact("src/Parser.cs");
        var written = connection.Write(CodeIndex.File, [file]);
        Assert.Equal(1UL, written.Created);

        // Writing the identical fact again is free — `ops-I5`'s dedup, and the reason a
        // producer keeps no book of what it has already sent.
        var again = connection.Write(CodeIndex.File, [file]);
        Assert.Equal(0UL, again.Created);
        Assert.Equal(1UL, again.Deduped);

        var rows = connection.Query("F where src.File F");
        Assert.Single(rows.Rows);
    }

    /// <summary>
    /// **A stale fingerprint is refused at the handshake**, which is the designed failure
    /// of a flag day rather than a surprise — and the message names both numbers so an
    /// operator can tell a stale client from one pointed at the wrong database.
    /// </summary>
    [Fact]
    public void A_client_carrying_the_wrong_fingerprint_is_refused()
    {
        using var server = FjordServer.Serving("code", "code.sigla");

        var stale = new FjordSchema(CodeIndex.Schema.Predicates, CodeIndex.SchemaFingerprint ^ 0xFF);

        // A *server* refusal, not a protocol fault: the frames were well formed and the
        // server declined. The two are different exceptions on purpose.
        var refused = Assert.Throws<FjordServerException>(() =>
            FjordConnection.Connect(server.Socket, "code", stale));

        Assert.Contains("schema mismatch", refused.Message, StringComparison.Ordinal);
        Assert.Contains($"{CodeIndex.SchemaFingerprint:x}", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The `fjord` binary and the shipped schemas are findable from the test assembly,
    /// which is what every gate above this one depends on and is worth failing separately.
    /// </summary>
    [Fact]
    public void The_binary_and_the_schemas_are_where_the_harness_looks()
    {
        Assert.True(File.Exists(FjordServer.Binary), FjordServer.Binary);
        foreach (var schema in new[] { "code.sigla", "index.sigla", "csharp.sigla" })
        {
            Assert.True(File.Exists(FjordServer.Schema(schema)), schema);
        }
    }
}
