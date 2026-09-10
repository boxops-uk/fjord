using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// A client that learns its types from the server rather than stating them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The point of these is that nothing here is shared with the Rust side.</b> The
/// decoding is this client's own, written from the frame's documented layout, so a
/// descriptor list that round-trips in Rust and does not decode here is a format that was
/// only ever agreed with itself.
/// </para>
/// <para>
/// What the derived schema is checked against is <see cref="DotnetIndex.Schema"/> — the
/// 1,104 hand-written lines this feature exists to retire. Agreement between the two is
/// the claim: a schema derived over the wire is the same schema, predicate for predicate
/// and field for field, as the one transcribed by hand.
/// </para>
/// </remarks>
public class DerivedSchemaTests
{
    /// <summary>
    /// Every predicate the hand-written schema declares comes back from the server with
    /// the same key, the same value side and the same field order.
    /// </summary>
    [Fact]
    public void A_schema_derived_from_the_server_agrees_with_the_one_written_by_hand()
    {
        using var server = FjordServer.Serving("derived", "dotnet.sigla");
        using var connection = FjordConnection.Connect(
            server.Socket, "derived", DotnetIndex.Schema);

        var derived = connection.SchemaTypes();

        foreach (var stated in DotnetIndex.Schema.Predicates)
        {
            var found = derived.Predicates.SingleOrDefault(p => p.Name == stated.Name);

            Assert.True(found is not null, $"the server did not describe `{stated.Name}`");
            Assert.True(
                Same(stated.Key, found!.Key, DotnetIndex.Schema, derived),
                $"`{stated.Name}` key: stated {Show(stated.Key, DotnetIndex.Schema)}, "
                + $"served {Show(found.Key, derived)}");
            Assert.True(
                (stated.Value is null) == (found.Value is null)
                    && (stated.Value is null
                        || Same(stated.Value!, found.Value!, DotnetIndex.Schema, derived)),
                $"`{stated.Name}` value: stated {Show(stated.Value, DotnetIndex.Schema)}, "
                + $"served {Show(found.Value, derived)}");
            Assert.False(found.IsVirtual, $"`{stated.Name}` is stored, not answered");
        }
    }

    /// <summary>
    /// Structural comparison, resolving each reference through the schema it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two things make <c>==</c> the wrong question here.</b> A record's synthesised
    /// equality compares its members with <c>Equals</c>, and the member is an
    /// <c>IReadOnlyList</c> — so identical trees are unequal unless they share a
    /// collection instance, which an array built by hand and a list built by a decoder
    /// never do.
    /// </para>
    /// <para>
    /// The second is the real one: <b>a reference carries an id, and an id means
    /// something only in the numbering that minted it.</b> <c>src.File</c> is predicate 0
    /// in the schema this client writes out and 56 in the one the server serves, and
    /// both are right — which is why a block header carries the predicate's name. So the
    /// comparison resolves each side's id through its own schema and compares the names.
    /// </para>
    /// </remarks>
    private static bool Same(FjordType left, FjordType right, FjordSchema mine, FjordSchema theirs)
        => (left, right) switch
        {
            (FjordType.Int, FjordType.Int) => true,
            (FjordType.Str, FjordType.Str) => true,
            (FjordType.Bytes, FjordType.Bytes) => true,
            (FjordType.Fact a, FjordType.Fact b) =>
                mine.NameOf(a.Predicate) == theirs.NameOf(b.Predicate),
            (FjordType.Record a, FjordType.Record b) =>
                a.Fields.Count == b.Fields.Count
                && a.Fields.Zip(b.Fields).All(pair =>
                    pair.First.Name == pair.Second.Name
                    && Same(pair.First.Type, pair.Second.Type, mine, theirs)),
            (FjordType.Union a, FjordType.Union b) =>
                a.Alternatives.Count == b.Alternatives.Count
                && a.Alternatives.Zip(b.Alternatives).All(pair =>
                    pair.First.Name == pair.Second.Name
                    && pair.First.Disc == pair.Second.Disc
                    && Same(pair.First.Type, pair.Second.Type, mine, theirs)),
            _ => false,
        };

    private static string Show(FjordType? type, FjordSchema schema) => type switch
    {
        null => "(none)",
        FjordType.Int => "int",
        FjordType.Str => "string",
        FjordType.Bytes => "bytes",
        FjordType.Fact fact => schema.NameOf(fact.Predicate),
        FjordType.Record record =>
            "{" + string.Join(
                ", ", record.Fields.Select(f => $"{f.Name}: {Show(f.Type, schema)}")) + "}",
        FjordType.Union union =>
            "(" + string.Join(" | ", union.Alternatives.Select(a => $"{a.Name}={a.Disc}")) + ")",
        _ => type.ToString() ?? "?",
    };

    /// <summary>
    /// The derived schema also carries what a hand-written one cannot: the virtual
    /// predicates, marked.
    /// </summary>
    /// <remarks>
    /// A client writing them out by hand describes only what it writes to, so it has no
    /// row for <c>fjord.db.List</c> at all — and therefore no way to know it exists, or
    /// that writing to it would be refused.
    /// </remarks>
    [Fact]
    public void A_derived_schema_names_the_virtual_predicates_and_marks_them()
    {
        using var server = FjordServer.Serving("derived", "dotnet.sigla");
        using var connection = FjordConnection.Connect(
            server.Socket, "derived", DotnetIndex.Schema);

        var derived = connection.SchemaTypes();
        var listing = derived.Predicates.SingleOrDefault(p => p.Name == "fjord.db.List");

        Assert.True(listing is not null, "a served session can name the catalogue");
        Assert.True(listing!.IsVirtual);

        Assert.DoesNotContain(
            DotnetIndex.Schema.Predicates,
            stated => stated.Name == "fjord.db.List");
    }

    /// <summary>
    /// A fact built against the derived schema is written and read back — with the
    /// producer placing values by field <i>name</i> and never stating an order.
    /// </summary>
    /// <remarks>
    /// This is the whole of what the feature buys. The record below is assembled by
    /// looking each field up in what the server said, so a schema that reorders its
    /// fields moves nothing here — which is what makes a schema edit stop being a client
    /// rebuild.
    /// </remarks>
    [Fact]
    public void A_fact_built_against_the_derived_schema_is_accepted_and_read_back()
    {
        using var server = FjordServer.Serving("derived", "dotnet.sigla");

        // **Two connections, and the second is the producer.** A connection encodes a
        // fact against the schema it was opened with, so deriving one has to happen on a
        // session opened for the purpose — which is the shape a real producer takes:
        // ask, then write against the answer. `assertSchema: false` because this end has
        // no opinion yet, which is the whole premise.
        FjordSchema derived;
        using (var asking = FjordConnection.Connect(
            server.Socket, "derived", DotnetIndex.Schema, SessionMode.ReadOnly, false))
        {
            derived = asking.SchemaTypes();
        }

        using var connection = FjordConnection.Connect(
            server.Socket, "derived", derived, SessionMode.ReadWrite, false);

        var id = derived.IdOf("src.File");
        Assert.True(Same(FjordType.String, derived[id].Key, derived, derived));

        var written = connection.Write(
            id,
            [new FjordFact(id, FjordValue.Of("derived/Program.cs"))]);

        Assert.Equal(1ul, written.Created);

        var rows = connection.Query("P where src.File P").Rows;
        var only = Assert.Single(rows);
        Assert.Equal("derived/Program.cs", Assert.IsType<FjordValue.Str>(only).Value);
    }
}
