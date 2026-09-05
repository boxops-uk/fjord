using System;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A producer can make the databases it is about to write to.</b>
/// </para>
/// <para>
/// The protocol has carried <c>CONTROL</c> — create, finish, remove — since it had
/// databases, and the Rust CLI has used it all along; this client implemented none of it.
/// So a caller had to create every database out of band, which for a checkout that
/// compiles for several frameworks meant discovering the framework names first — a full
/// design-time load of the whole solution — to learn names the indexer computes for itself
/// a moment later. A 213-project repository spent 482 seconds on that before failing with
/// <c>UnknownDatabase</c>.
/// </para>
/// </summary>
public sealed class DatabaseControlTests
{
    /// <summary>
    /// <b>The schema comes back as source, and it is the schema being <i>served</i> — which
    /// is a shade wider than the one the database was created from.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>So this source cannot create a sibling database, and the refusal is the
    /// contract.</b> A session is served the database's own predicates plus the server's
    /// virtual <c>fjord.db.*</c> catalogue, and <c>SCHEMA</c> prints what is being served
    /// so that a query compiled against the answer matches what the server will run.
    /// Handing that back to <c>create</c> would declare the reserved namespace as stored
    /// predicates — and serving a database appends the virtuals to its own schema, so the
    /// result composes to two of each and cannot be opened at all.
    /// </para>
    /// <para>
    /// It once <i>succeeded</i>: the artifact was written and only the next open failed,
    /// with <c>error[reject/redeclaration]: `fjord.db.List` is already declared</c>,
    /// leaving a database no listing could explain. Now the create is refused and nothing
    /// is written, which is what makes "ask a database what it holds" a safe thing to do
    /// with the answer.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_schema_answered_is_the_one_served_virtuals_included()
    {
        using var server = FjordServer.Serving("first", "dotnet.sigla");
        using var connection = FjordConnection.Connect(
            server.Socket, "first", DotnetIndex.Schema, SessionMode.ReadWrite);

        var source = connection.SchemaSource();

        Assert.NotEmpty(source);

        // Import-free — the server lowers what it is sent and resolves no import.
        Assert.DoesNotContain("import ", source, StringComparison.Ordinal);

        // And wider than what was created: the reserved catalogue namespace is in it.
        Assert.Contains("fjord.db", source, StringComparison.Ordinal);

        // Which is exactly why it cannot come back the other way.
        var refused = Assert.Throws<FjordServerException>(
            () => connection.CreateDatabase("sibling", source));

        Assert.Contains("fjord.db", refused.ServerMessage, StringComparison.Ordinal);

        // **Nothing written is half the claim**, and the half that used to fail: the
        // refusal has to leave the store root as it found it.
        Assert.Throws<FjordServerException>(
            () => FjordConnection.Connect(
                server.Socket, "sibling", DotnetIndex.Schema, SessionMode.ReadOnly));
    }

    /// <summary>
    /// <b>Creating one that is already there is refused, by name.</b>
    /// </summary>
    /// <remarks>
    /// Which is what makes "create it if it is missing" a thing a caller can write: the
    /// refusal is a server error with a code, not a corrupted store or a silent second
    /// artifact under one name.
    /// </remarks>
    [Fact]
    public void Creating_a_database_that_exists_is_refused()
    {
        using var server = FjordServer.Serving("only", "dotnet.sigla");
        using var connection = FjordConnection.Connect(
            server.Socket, "only", DotnetIndex.Schema, SessionMode.ReadWrite);

        var source = connection.SchemaSource();

        var refused = Assert.Throws<FjordServerException>(
            () => connection.CreateDatabase("only", source));

        Assert.NotEqual(default, refused.Code);
    }

    /// <summary>
    /// <b>A read-only session cannot create one.</b>
    /// </summary>
    /// <remarks>
    /// Creating a database changes the store root rather than asking it something, so it
    /// takes the same mode a write stream does.
    /// </remarks>
    [Fact]
    public void A_read_only_session_cannot_create_a_database()
    {
        using var server = FjordServer.Serving("readonly", "dotnet.sigla");
        using var connection = FjordConnection.Connect(
            server.Socket, "readonly", DotnetIndex.Schema);

        var source = connection.SchemaSource();

        Assert.Throws<FjordServerException>(() => connection.CreateDatabase("nope", source));
    }
}
