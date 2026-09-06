using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A repository declaring <c>this[int]</c> beside <c>this[int, int]</c> indexes to
/// completion.</b>
/// </para>
/// <para>
/// Roslyn names every indexer of a type <c>this[]</c>, and a term descriptor is
/// <c>&lt;name&gt; '.'</c> — the specification's one disambiguator slot is a method's. So
/// both minted <c>Slots/Shelf+1#`this[]`.</c>, <c>codemarkup.Definition</c> is keyed
/// <c>{symbol, file}</c>, and ingest refused one key with two values part-way through a
/// write (<c>ops-I4</c>): the run died with exit 134, exactly as two arities of a type
/// name did. <b>An unremarkable C# shape made a repository unindexable.</b>
/// </para>
/// <para>
/// So the claim is the whole run and not a key: the real program, a real socket, a real
/// database, exit code 0 — and then the indexers asked for by symbol.
/// </para>
/// </summary>
/// <remarks>
/// <b>The reference half is not asserted here, because the walk does not write one.</b>
/// A reference is collected from a <c>SimpleNameSyntax</c> and an indexer's use is an
/// element access with no name node, so <c>shelf[0]</c> produces no
/// <c>codemarkup.FileXRef</c> row — a gap in the walk rather than in the format, recorded
/// in the indexer's README. That a reference is byte-identical to its declaration is
/// asserted where the strings are minted: <c>ScipSymbolsTests</c> and the scheme golden,
/// both of which resolve an element access themselves.
/// </remarks>
public sealed class OverloadedIndexerTests
{
    /// <summary>
    /// The three indexers of <c>Shelf&lt;T&gt;</c>, in the order their documentation ids
    /// sort — which is the order the ordinal counts, so this list is also the mapping.
    /// </summary>
    private static readonly string[] Shelf =
    [
        "Slots/Shelf+1#`this[]`.",
        "Slots/Shelf+1#`this[]+1`.",
        "Slots/Shelf+1#`this[]+2`.",
    ];

    /// <summary>
    /// <b>The run finishes, and the overloaded indexers are one symbol each.</b>
    /// </summary>
    /// <remarks>
    /// <b>The exit code is the conflict assertion</b>, the same way <c>LedgerTests</c> and
    /// <c>ArityPairTests</c> read it: a conflicting fact is refused by the server and
    /// <c>FactSink</c> turns a refusal into a failure the next flush throws, so a run that
    /// reached its end wrote nothing that disagreed with anything already there. Before
    /// the ordinal went into the term descriptor this returned 134 with
    /// <c>Conflict: predicate PredicateId(0) already holds a different fact under this
    /// key</c>.
    /// </remarks>
    [Fact]
    public void Overloaded_indexers_index_to_completion_as_distinct_symbols()
    {
        using var fixture = Fixture.Copy("indexer");
        using var server = FjordServer.Serving("indexer", "dotnet.sigla");

        var code = Program.Main([
            "--sln", fixture.Path("Indexer.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//indexer",
            "--no-smoke",
        ]);

        Assert.Equal(0, code);

        using var connection = FjordConnection.Connect(server.Socket, "indexer", DotnetIndex.Schema);

        var symbols = Symbols(connection, "S where src.Symbol S");

        // Three declarations, three strings — where there was one string and a dead run.
        Assert.All(Shelf, spelling => Assert.Contains(spelling, symbols));

        // **The lone indexer is the control.** The ordinal is empty for the only sibling,
        // so a type with one indexer keeps the string it has today — which is what keeps
        // this off every property in every index.
        Assert.Contains("Slots/Single#`this[]`.", symbols);
        Assert.DoesNotContain("Slots/Single#`this[]+1`.", symbols);

        // Two explicit implementations of one interface's indexers: the ordinal lands
        // inside the backticks the name was already escaped by.
        Assert.Contains("Slots/Explicit#`Slots.IShelf.this[]`.", symbols);
        Assert.Contains("Slots/Explicit#`Slots.IShelf.this[]+1`.", symbols);

        // And the interface's own pair, which is where they are declared.
        Assert.Contains("Slots/IShelf#`this[]`.", symbols);
        Assert.Contains("Slots/IShelf#`this[]+1`.", symbols);
    }

    /// <summary>
    /// <para>
    /// <b>Every indexer in the fixture reaches a symbol of its own, and no two share
    /// one.</b>
    /// </para>
    /// <para>
    /// This is the contrast with the arity pair, and it is why the collision was only ever
    /// in the string: <c>csharp.Property</c> trails <c>docId</c>, so the entity layer told
    /// eight indexers apart all along. What could not tell them apart was
    /// <c>src.Symbol</c>, and <c>csharp.SymbolOf</c> was therefore many-to-one — the
    /// mapping a find-references answer is read through.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_indexer_reaches_a_symbol_of_its_own()
    {
        using var fixture = Fixture.Copy("indexer");
        using var server = FjordServer.Serving("indexer", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Indexer.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//indexer",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "indexer", DotnetIndex.Schema);

        var indexers = Strings(
            connection,
            "D where csharp.Property {name = N, isIndexer = {true_ = _}, docId = D}");

        var crossed = Symbols(
            connection,
            "T where csharp.SymbolOf {definition = {property = P}, symbol = S}; "
            + "P = csharp.Property {isIndexer = {true_ = _}}; S = src.Symbol T");

        Assert.Equal(8, indexers.Count);
        Assert.Equal(indexers.Count, indexers.Distinct().Count());

        // One symbol per indexer, and no symbol standing for two of them. A merged pair
        // shows up here as a shorter distinct set than the row count.
        Assert.Equal(indexers.Count, crossed.Count);
        Assert.Equal(crossed.Count, crossed.Distinct().Count());
    }

    /// <summary>
    /// The symbol strings a query answers, with the scheme and package coordinate taken
    /// off so an assertion reads as the descriptors it is about.
    /// </summary>
    /// <remarks>
    /// <b>Selected by package and *asserted* on the token</b>, rather than filtered on
    /// both. The fixture reaches <c>System.String.Length</c>, so a query for every symbol
    /// answers with the framework's too — but a run that wrote a different token would
    /// then leave every assertion here vacuous, and the token is the one check a fan-out
    /// has before it trusts a string match.
    /// </remarks>
    private static List<string> Symbols(FjordConnection connection, string query)
    {
        const string Package = " nuget Slots 1.0.0.0 ";

        return [.. Strings(connection, query)
            .Where(symbol => symbol.Contains(Package, StringComparison.Ordinal))
            .Select(symbol =>
            {
                Assert.StartsWith($"{ScipSymbols.Scheme}{Package}", symbol, StringComparison.Ordinal);
                return symbol[(ScipSymbols.Scheme.Length + Package.Length)..];
            })];
    }

    private static List<string> Strings(FjordConnection connection, string query) =>
        [.. connection.Query(query).Rows.Select(row => Assert.IsType<FjordValue.Str>(row).Value)];
}
