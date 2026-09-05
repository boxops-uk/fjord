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
    /// <b>So this source cannot create a sibling database, and that is the finding rather
    /// than the intent.</b> A session is served the database's own predicates plus the
    /// server's virtual <c>fjord.db.*</c> catalogue, and <c>SCHEMA</c> prints what is being
    /// served so that a query compiled against the answer matches what the server will run.
    /// Feeding it back to <c>create</c> declares the reserved namespace as stored
    /// predicates; the artifact is created and the server then cannot open it —
    /// <c>error[reject/redeclaration]: `fjord.db.List` is already declared</c> — leaving a
    /// broken database behind. <c>create</c> validating the reserved namespace before it
    /// writes anything is the fix, and it is not this client's to make.
    /// </para>
    /// <para>
    /// Pinned as a characterisation rather than left to be rediscovered: a producer that
    /// wants to create the databases it writes to needs the <i>embedded</i> schema, and no
    /// frame answers with that today.
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
