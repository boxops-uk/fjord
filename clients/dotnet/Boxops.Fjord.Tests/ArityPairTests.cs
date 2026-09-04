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
            "--source", fixture.Path("Arity.slnx"),
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
            "--source", fixture.Path("Arity.slnx"),
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
    /// <b>The entity layer still merges the two arities into one fact, and that is an open
    /// maintainer decision rather than something this unit took.</b>
    /// </para>
    /// <para>
    /// `csharp.Class` is key-only and its key leads with a `csharp.FullName` of
    /// `{name, containingNamespace}`, where the name is the arity-stripped one. So the
    /// database holds **one** class named `Result` with **two** `DefinitionLocation` rows
    /// beside it — indistinguishable from a partial class — and `csharp.SymbolOf` maps
    /// *both* symbols onto it. `Interface`, `Record`, `Struct` and `csharp.FullName` share
    /// the shape.
    /// </para>
    /// <para>
    /// **This is newly observable and was not newly created**: until the symbol string
    /// distinguished the arities the run died before anything could merge. Fixing it needs
    /// either a schema change — a fingerprint move, so a flag day — or a redefinition of
    /// what `csharp.Name` holds. `docs/unified-plan/15-retire-code-sigla.md` §S3 carries
    /// the decision and the indexer README carries the limitation.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>A gate on a known limitation, so that removing it is deliberate.</b> Whoever
    /// takes the decision makes this red, which is the point: a consumer must not read
    /// "arities are distinguished now" and believe it of the entity layer.
    /// </remarks>
    [Fact]
    public void The_entity_layer_still_merges_the_two_arities_into_one_class()
    {
        using var fixture = Fixture.Copy("arity");
        using var server = FjordServer.Serving("arity", "dotnet.sigla");

        Assert.Equal(0, Program.Main([
            "--source", fixture.Path("Arity.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//arity",
            "--no-smoke",
        ]));

        using var connection = FjordConnection.Connect(server.Socket, "arity", DotnetIndex.Schema);

        // One class fact for the two declarations.
        var classes = Strings(
            connection,
            "N where csharp.Class {name = FN}; FN = csharp.FullName {name = M}; M = csharp.Name N");

        Assert.Equal(1, classes.Count(name => name == "Result"));

        // Two definition locations under it, which is the shape a partial class has.
        var located = connection.Query(
            "{at = X.location.span.start} where "
            + "X = csharp.DefinitionLocation {definition = {type = {namedType = {class_ = C}}}}; "
            + "C = csharp.Class {name = FN}; FN = csharp.FullName {name = M}; "
            + "M = csharp.Name \"Result\"").Rows;

        Assert.Equal(2, located.Count);

        // And both symbols cross to that one entity, so the crossing is many-to-one.
        var crossed = Symbols(
            connection,
            "T where csharp.SymbolOf {definition = {type = {namedType = {class_ = C}}}, symbol = S}; "
            + "C = csharp.Class {name = FN}; FN = csharp.FullName {name = M}; "
            + "M = csharp.Name \"Result\"; S = src.Symbol T");

        Assert.Equal(2, crossed.Count);
        Assert.Contains(Bare, crossed);
        Assert.Contains(Generic, crossed);

        // The `codemarkup` surface, by contrast, does keep them apart: the symbol is in
        // every key there, so the search index has a row per arity.
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
