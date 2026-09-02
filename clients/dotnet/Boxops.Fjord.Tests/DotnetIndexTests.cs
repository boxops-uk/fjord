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
        Assert.Equal(10, DotnetIndex.Schema.Predicates.Count);
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

    private static void Write(FjordConnection connection, uint predicate, FjordFact fact) =>
        Assert.Equal(1UL, connection.Write(predicate, [fact]).Created);
}
