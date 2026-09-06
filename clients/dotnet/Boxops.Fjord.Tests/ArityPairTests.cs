using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A repository declaring <c>Result</c> beside <c>Result&lt;T&gt;</c> indexes to
/// completion.</b>
/// </para>
/// <para>
/// Roslyn's <c>Name</c> strips the arity that the metadata name spells, so a type
/// descriptor built from it minted one string for both — and <c>codemarkup.SymbolInfo</c>
/// is keyed <c>{symbol}</c> with the signature on the value side. The signature always
/// differs, so two facts wanted one key with two values, ingest refused
/// (<c>ops-I4</c>), <c>FactSink</c> latched the refusal, and the run died part-way through
/// a write. <b>No C# repository containing that everyday pair could be indexed at
/// all.</b>
/// </para>
/// <para>
/// So the claim is the whole run and not a key: the real program, a real socket, a real
/// database, exit code 0 — and then the two arities asked for by name.
/// </para>
/// </summary>
public sealed class ArityPairTests
{
    /// <summary>
    /// The strings the fixture must mint. <c>Result</c> is the control: **arity 0 keeps
    /// the bare name**, which is what keeps every non-generic symbol in every index
    /// byte-identical to the one it was.
    /// </summary>
    private const string Bare = "Arity/Pair/Result#";
    private const string Generic = "Arity/Pair/Result+1#";

    /// <summary>
    /// <b>The run finishes, and the pair is two symbols in the database it wrote.</b>
    /// </summary>
    /// <remarks>
    /// <b>The exit code is the conflict assertion</b>, the same way <c>LedgerTests</c>
    /// reads it: a conflicting fact is refused by the server and <c>FactSink</c> turns a
    /// refusal into a failure the next flush throws, so a run that reached its end wrote
    /// nothing that disagreed with anything already there. Before the arity went into the
    /// descriptor this returned 134 with
    /// <c>Conflict: predicate ... already holds a different fact under this key</c>.
    /// </remarks>
    [Fact]
    public void An_arity_overloaded_pair_indexes_to_completion_as_two_symbols()
    {
        using var fixture = Fixture.Copy("arity");
        using var server = FjordServer.Serving("arity", "dotnet.sigla");

        var code = Program.Main([
            "--sln", fixture.Path("Arity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//arity",
            "--no-smoke",
        ]);

        Assert.Equal(0, code);

        using var connection = FjordConnection.Connect(server.Socket, "arity", DotnetIndex.Schema);

        var symbols = Symbols(connection, "S where src.Symbol S");

        // The pair, and the control beside it.
        Assert.Contains(Bare, symbols);
        Assert.Contains(Generic, symbols);

        // **Everything under the generic half inherits the arity**, which is the property
        // that comes from putting the suffix on the containing type's descriptor rather
        // than applying a second rule per descriptor kind.
        Assert.Contains($"{Generic}Value.", symbols);
        Assert.Contains($"{Generic}[T]", symbols);
        Assert.Contains($"{Generic}`.ctor`().", symbols);

        // And the non-generic half's member is spelled without one, so a consumer cannot
        // reach the wrong `Ok` by dropping a suffix it did not expect.
        Assert.Contains($"{Bare}Ok.", symbols);
        Assert.Contains($"{Generic}Ok.", symbols);
    }

    /// <summary>
    /// <b>A use of each arity resolves to that arity, in the database.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reference to <c>Result&lt;int&gt;</c> is a *constructed* symbol, so this is the
    /// arm where a second rule would have been needed and is not: Roslyn's <c>Arity</c> is
    /// the same for the construction and the definition, and the reference therefore
    /// spells the declaration's own descriptor.
    /// </para>
    /// <para>
    /// <b>The other half is the method ordinal.</b> A reference to <c>M&lt;int&gt;</c> was
    /// resolved by searching <c>GetMembers()</c> for the reference itself, which answers
    /// with definitions — so the search missed, minus one collapsed into the empty
    /// disambiguator, and the call was filed under the *plain* overload. That is a wrong
    /// edge, not a missing one: find-references on the generic overload answered nothing
    /// and find-references on the plain one answered a call that was not its.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_use_of_each_arity_resolves_to_that_arity()
    {
        using var fixture = Fixture.Copy("arity");
        using var server = FjordServer.Serving("arity", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Arity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//arity",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "arity", DotnetIndex.Schema);

        var referenced = Symbols(
            connection,
            "T where codemarkup.FileXRef {file = F, span = SP, target = S, role = R}; S = src.Symbol T");

        Assert.Contains(Bare, referenced);
        Assert.Contains(Generic, referenced);
        Assert.Contains($"{Generic}Value.", referenced);

        // The plain overload and its generic sibling, each with the use that is its own.
        Assert.Contains("Arity/Pair/Overloads#M().", referenced);
        Assert.Contains("Arity/Pair/Overloads#M(+1).", referenced);
    }

    /// <summary>
    /// <para>
    /// <b>Each arity is its own <c>csharp.Class</c> fact, with its own definition
    /// location.</b>
    /// </para>
    /// <para>
    /// `csharp.Class` is key-only and its key leads with a `csharp.FullName` of
    /// `{name, containingNamespace, arity}`, so `Result` and `Result&lt;T&gt;` are two
    /// facts and `csharp.SymbolOf` crosses one symbol to each. Before the arity was in
    /// that key they were **one** fact carrying **two** `DefinitionLocation` rows —
    /// indistinguishable from a partial class, and never an error — with both symbols
    /// crossing onto the merged entity. `Interface`, `Record` and `Struct` share the key
    /// shape and so share the repair.
    /// </para>
    /// <para>
    /// <b>One location each is the load-bearing half.</b> Two rows is what the merge
    /// looked like, so a fix that split the entity but left both locations under one of
    /// them would answer "two classes" and still be wrong about where either is written.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>The partial class is the claim next to this one, and it is asserted elsewhere
    /// rather than here</b> — `arity` holds no partial type and a fixture is not edited to
    /// suit a test. `PartialMemberTests.One_definition_per_member_per_file_and_a_location_for_each_half`
    /// asserts the contrast over the `partial` fixture: one entity, two locations. Read
    /// together they are what separates this fix from a regression that made every
    /// declaration its own entity, and
    /// `EntityKeyCensusTests.Two_arities_are_two_entity_keys_and_a_partial_classs_halves_are_one`
    /// holds both in one compilation at the key level.
    /// </remarks>
    [Fact]
    public void Each_arity_is_its_own_class_fact_with_its_own_location()
    {
        using var fixture = Fixture.Copy("arity");
        using var server = FjordServer.Serving("arity", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Arity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//arity",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "arity", DotnetIndex.Schema);

        // Two class facts named `Result`, one per arity — and the arity is what says so.
        var arities = connection.Query(
            "{arity = FN.arity} where csharp.Class {name = FN}; "
            + "FN = csharp.FullName {name = M}; M = csharp.Name \"Result\"").Rows;

        Assert.Equal(
            [0L, 1L],
            arities.Select(row => Assert.IsType<FjordValue.Int>(
                Assert.IsType<FjordValue.Record>(row).Fields[0]).Value).Order());

        // One definition location under each, rather than two under one of them.
        foreach (var arity in (long[])[0L, 1L])
        {
            var located = connection.Query(
                "{at = X.location.span.start} where "
                + "X = csharp.DefinitionLocation {definition = {type = {namedType = {class_ = C}}}}; "
                + $"C = csharp.Class {{name = FN}}; FN = csharp.FullName {{name = M, arity = {arity}}}; "
                + "M = csharp.Name \"Result\"").Rows;

            Assert.Single(located);
        }

        // And the crossing is one-to-one now: one symbol reaches each entity.
        foreach (var (arity, symbol) in ((long Arity, string Symbol)[])[(0L, Bare), (1L, Generic)])
        {
            var crossed = Symbols(
                connection,
                "T where csharp.SymbolOf {definition = {type = {namedType = {class_ = C}}}, symbol = S}; "
                + $"C = csharp.Class {{name = FN}}; FN = csharp.FullName {{name = M, arity = {arity}}}; "
                + "M = csharp.Name \"Result\"; S = src.Symbol T");

            Assert.Equal([symbol], crossed);
        }

        // The `codemarkup` surface kept them apart all along — the symbol is in every key
        // there — and the two layers now agree rather than disagreeing.
        var found = Symbols(
            connection,
            "T where codemarkup.SearchEntry {nameLowercase = \"result\", name = N, kind = K, "
            + "symbol = S, file = F, line = L}; S = src.Symbol T");

        Assert.Equal(2, found.Count);
        Assert.Contains(Bare, found);
        Assert.Contains(Generic, found);
    }

    /// <summary>
    /// The symbol strings a query answers, with the scheme and package coordinate taken
    /// off so an assertion reads as the descriptors it is about.
    /// </summary>
    /// <remarks>
    /// The prefix is asserted rather than trimmed blind: a run that wrote a different
    /// token would otherwise pass every assertion in this file, and the token is the one
    /// check a fan-out has.
    /// </remarks>
    private static List<string> Symbols(FjordConnection connection, string query)
    {
        const string Prefix = "nuget Pair 1.0.0.0 ";

        return [.. Strings(connection, query).Select(symbol =>
        {
            Assert.StartsWith($"{ScipSymbols.Scheme} {Prefix}", symbol, StringComparison.Ordinal);
            return symbol[(ScipSymbols.Scheme.Length + 1 + Prefix.Length)..];
        })];
    }

    private static List<string> Strings(FjordConnection connection, string query) =>
        [.. connection.Query(query).Rows.Select(row => Assert.IsType<FjordValue.Str>(row).Value)];
}
