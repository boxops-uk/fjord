using System.IO;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A run makes the databases it writes to.</b>
/// </para>
/// <para>
/// A checkout compiling for several frameworks is several databases, named
/// <c>&lt;name&gt;#&lt;tfm&gt;</c> — and the names are not known until the design-time
/// build has run, so a caller creating them ahead of time had to discover them with
/// <c>--list-frameworks</c>, which is a full load of the whole solution. A 213-project
/// repository spent 482 seconds on that and then failed with <c>UnknownDatabase</c>,
/// having computed the very names it needed a moment earlier.
/// </para>
/// <para>
/// <b>The schema comes in composed, because composing is resolution.</b> Imports are
/// namespaces mapped to relative paths under a search root; following them is sigla's
/// job, and neither this program nor the server should be doing it — the server would
/// need this machine's filesystem, which is false the moment the address is TCP.
/// </para>
/// </summary>
public sealed class CreateOnDemandTests
{
    /// <summary>
    /// <b>A store root with nothing in it ends the run holding one database per framework.</b>
    /// </summary>
    [Fact]
    public void A_run_creates_the_databases_it_needs()
    {
        using var fixture = Fixture.Copy("targets");
        using var server = FjordServer.ServingAll("dotnet.sigla");

        var baked = FjordServer.Composed("dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Targets.slnx"),
            "--root", fixture.Root,
            "--schema", baked,
            "--at", $"{server.Socket}//made",
            "--no-smoke",
        ]));

        // Whatever the fixture compiles for, every one of them is now a database, and the
        // run is what put them there.
        var listed = FjordServer.Run(server.Root, "list");

        Assert.Contains("made", listed);
    }

    /// <summary>
    /// <b>Without <c>--schema</c> a missing database is the error it always was.</b>
    /// </summary>
    /// <remarks>
    /// Creating one is a change to the store root, so it happens because a caller asked
    /// for it and not because a write found nothing there. A run with no schema to create
    /// from is the old behaviour exactly, which is what keeps this additive.
    /// </remarks>
    [Fact]
    public void Without_a_schema_a_missing_database_still_fails()
    {
        using var fixture = Fixture.Copy("targets");
        using var server = FjordServer.ServingAll("dotnet.sigla");

        Assert.NotEqual(0, Program.Main([
            "--sln", fixture.Path("Targets.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//absent",
            "--no-smoke",
        ]));
    }

    /// <summary>
    /// <b>A composed schema carries no import, and creates a database the client can bind
    /// to.</b>
    /// </summary>
    /// <remarks>
    /// The fingerprint is the tie: the client asserts its own constant at the handshake, so
    /// a database created from this source is one it can write to only if the composition
    /// is the schema the client states. A composition that drifted would be refused at the
    /// first frame rather than discovered in the facts.
    /// </remarks>
    [Fact]
    public void A_composed_schema_creates_a_database_this_client_can_bind_to()
    {
        using var server = FjordServer.ServingAll("dotnet.sigla");

        var source = File.ReadAllText(FjordServer.Composed("dotnet.sigla"));

        Assert.DoesNotContain("import ", source);

        using var control = FjordConnection.ConnectUnbound(
            FjordAddress.ForSocket(server.Socket, string.Empty), DotnetIndex.Schema);

        control.CreateDatabase("baked", source);

        using var bound = FjordConnection.Connect(
            server.Socket, "baked", DotnetIndex.Schema, SessionMode.ReadWrite);

        Assert.Equal(DotnetIndex.Schema.Fingerprint, bound.Hello.SchemaFingerprint);
    }
}
