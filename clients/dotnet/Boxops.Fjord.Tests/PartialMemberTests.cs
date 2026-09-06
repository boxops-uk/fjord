using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A repository declaring a partial member indexes to completion, however its halves
/// are laid out.</b>
/// </para>
/// <para>
/// <c>partial void Ping();</c> beside <c>partial void Ping() { }</c> is two declarations
/// of one member, and a containing type's <c>GetMembers()</c> lists only the first of
/// them — so the walk reached the second holding a symbol its own type does not list. It
/// died two different ways depending on how the halves were written. <b>Across two
/// files</b>, the sibling search missed and the ordinal refused to guess, which was an
/// exception and a dead run. <b>In one file</b>, both halves reached one
/// <c>{symbol, file}</c> key of <c>codemarkup.Definition</c> with two spans, ingest
/// refused one key with two values (<c>ops-I4</c>), <c>FactSink</c> latched the refusal
/// and the write stream died part-way through — and so, for the same reason, did two
/// <c>partial class</c> parts written in one file, which has nothing to do with a partial
/// member at all.
/// </para>
/// <para>
/// So the claim is the whole run and not a key: the real program, a real socket, a real
/// database, exit code 0 — and then the shape of what it wrote.
/// </para>
/// </summary>
public sealed class PartialMemberTests
{
    /// <summary>The member written across two files, and its overload.</summary>
    private const string Ping = "Halves/Across#Ping().";
    private const string Overload = "Halves/Across#Ping(+1).";

    /// <summary>The member whose two halves are in one file.</summary>
    private const string Tick = "Halves/Together#Tick().";

    /// <summary>
    /// <b>The run finishes, and both halves of every partial member are one symbol.</b>
    /// </summary>
    /// <remarks>
    /// <b>The exit code is the conflict assertion</b>, the same way <c>LedgerTests</c>,
    /// <c>ArityPairTests</c> and <c>OverloadedIndexerTests</c> read it: a conflicting fact
    /// is refused by the server and <c>FactSink</c> turns a refusal into a failure the
    /// next flush throws, so a run that reached its end wrote nothing that disagreed with
    /// anything already there.
    /// </remarks>
    [Fact]
    public void A_partial_member_indexes_to_completion_as_one_symbol_per_member()
    {
        using var indexed = Indexed();

        var symbols = Symbols(indexed.Connection, "S where src.Symbol S");

        // The two-file member and its overload, each one string for two declarations.
        Assert.Contains(Ping, symbols);
        Assert.Contains(Overload, symbols);

        // The one-file member, the property form of it, and the part of a partial type
        // that shares its file.
        Assert.Contains(Tick, symbols);
        Assert.Contains("Halves/Together#Beats.", symbols);
        Assert.Contains("Halves/Together#Extra.", symbols);

        // A partial property and a partial indexer, which is the arm the ordinal has to
        // land inside an escaped name for.
        Assert.Contains("Halves/Across#Count.", symbols);
        Assert.Contains("Halves/Across#`this[]`.", symbols);
        Assert.Contains("Halves/Across#`this[]+1`.", symbols);

        // The control: an ordinary member of the same type, spelled as it always was.
        Assert.Contains("Halves/Across#Whole().", symbols);

        // **Two halves are not two members.** A second string for the implementing half
        // is what a producer that spelled it from the unlisted symbol would leave behind.
        Assert.DoesNotContain("Halves/Across#Ping(+2).", symbols);
        Assert.DoesNotContain("Halves/Together#Tick(+1).", symbols);
    }

    /// <summary>
    /// <b>One definition per member per file, with a location for every declaration.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>codemarkup.Definition</c> is keyed <c>{symbol, file}</c> and carries a span on
    /// the value side, so its value has to be a function of exactly those two — one row
    /// per member per file, at that file's first declaration of it. Two halves in one file
    /// otherwise fill one key twice and kill the run; two halves in two files are two
    /// keys, each of which must answer with a span in *its own* file.
    /// </para>
    /// <para>
    /// <b>Nothing is lost, which is the other half of the claim.</b>
    /// <c>csharp.DefinitionLocation</c> and <c>codemarkup.FileDefinition</c> are keyed per
    /// span, so the implementing half stays reachable as a second location — which is what
    /// a partial *type* has answered "where is this written" with all along.
    /// </para>
    /// <para>
    /// <b>And that is the claim a fix for the arity merge must leave standing.</b>
    /// <c>Result</c> beside <c>Result&lt;T&gt;</c> used to be one entity with two
    /// locations, which is exactly this shape, and
    /// <c>ArityPairTests.Each_arity_is_its_own_class_fact_with_its_own_location</c> asserts
    /// they are two entities with one each. A regression that made every declaration its
    /// own entity would satisfy that one and break the <c>csharp.Class</c> assertion below,
    /// which is why the two exist as a pair.
    /// </para>
    /// </remarks>
    [Fact]
    public void One_definition_per_member_per_file_and_a_location_for_each_half()
    {
        using var indexed = Indexed();

        // **The member written across two files is two keys**, and each answers with a
        // span in its own file rather than both answering with the declaring half's.
        var across = DefinedAt(indexed.Connection, Ping);
        Assert.Equal(2, across.Count);
        Assert.Equal(2, across.Distinct().Count());

        // Every declaration keeps a location, both halves of both overloads.
        Assert.Equal(4, LocatedAt(indexed.Connection, "Ping").Count);
        Assert.Equal(across, [.. across.Intersect(LocatedAt(indexed.Connection, "Ping"))]);

        // **The two halves in one file are one key**, at the first of them — which is a
        // property and not a byte offset: the earliest of the locations the member has.
        var ticks = LocatedAt(indexed.Connection, "Tick");

        Assert.Equal(2, ticks.Count);
        Assert.Equal([ticks.Min()], DefinedAt(indexed.Connection, Tick));
        Assert.Single(DefinedAt(indexed.Connection, "Halves/Together#Beats."));

        // **A partial type in one file is the same shape and was the same dead run.**
        // `Together` is written twice in `Together.cs`; `Across` once in each of two
        // files.
        Assert.Single(DefinedAt(indexed.Connection, "Halves/Together#"));
        Assert.Equal(2, DefinedAt(indexed.Connection, "Halves/Across#").Count);

        // Two overloads are two entities, not four — one per member with a location each,
        // which is what a partial class has answered with all along.
        Assert.Equal(2, Rows(indexed.Connection, Methods("Ping")));
        Assert.Equal(1, Rows(indexed.Connection, Methods("Tick")));

        // And the per-span keying beside it carries both halves of the one-file member.
        Assert.Equal(
            2,
            Rows(
                indexed.Connection,
                "X where X = codemarkup.FileDefinition {file = F, span = SP, symbol = S}; "
                + $"S = src.Symbol \"{Full(Tick)}\""));

        // **One `csharp.Class` for a partial type, with a location for each part.** The
        // entity is keyed on `{name, containingNamespace, arity}` and nothing per
        // declaration, so `Together`'s two parts and `Across`'s two files each reach one
        // fact — the shape two arities of a name used to be indistinguishable from.
        foreach (var (name, parts) in ((string Name, int Parts)[])[("Together", 2), ("Across", 2)])
        {
            Assert.Equal(1, Rows(indexed.Connection, Classes(name)));
            Assert.Equal(parts, LocatedClasses(indexed.Connection, name).Distinct().Count());
        }
    }

    /// <summary>
    /// <b>What a hover shows for a partial member is the declaring half's.</b>
    /// </summary>
    /// <remarks>
    /// <c>codemarkup.SymbolInfo</c> is keyed <c>{symbol}</c> alone with the signature, the
    /// documentation comment and the modifiers on the value side — so a value read from
    /// the implementing half is a second value under a key the declaring half already
    /// filled. It is not hypothetical: <c>GetDocumentationCommentXml</c> is <b>empty</b>
    /// on the implementing part, so a walk that read it from whichever declaration it was
    /// standing on wrote one key with a comment and without one, and the run died — in
    /// the two-file layout as well, where nothing else conflicts.
    /// </remarks>
    [Fact]
    public void The_documentation_a_partial_member_carries_is_the_declaring_halfs()
    {
        using var indexed = Indexed();

        Assert.Equal(
            "Only this half carries a documentation comment, which is the point.",
            Doc(indexed.Connection, Ping));

        Assert.Equal(
            "One member, two declarations, one file, one {symbol, file} key.",
            Doc(indexed.Connection, Tick));

        // The overload's own, so this is not one comment answering for the name.
        Assert.Equal(
            "A second overload, so the sibling ordinal is counted and not just looked up.",
            Doc(indexed.Connection, Overload));
    }

    /// <summary>
    /// <b>A use of a partial member resolves to the one string both halves mint.</b>
    /// </summary>
    /// <remarks>
    /// The overload is what makes this more than a spelling check: <c>across.Ping(2)</c>
    /// binds the second overload, whose ordinal is counted over a sibling list that
    /// contains the *declaring* halves — so a reference and a declaration agree only
    /// because both are counted over the same list.
    /// </remarks>
    [Fact]
    public void A_use_of_a_partial_member_resolves_to_the_symbol_its_halves_mint()
    {
        using var indexed = Indexed();

        var referenced = Symbols(
            indexed.Connection,
            "T where codemarkup.FileXRef {file = F, span = SP, target = S, role = R}; "
            + "S = src.Symbol T");

        Assert.Contains(Ping, referenced);
        Assert.Contains(Overload, referenced);
        Assert.Contains(Tick, referenced);
        Assert.Contains("Halves/Across#Count.", referenced);
        Assert.Contains("Halves/Together#Beats.", referenced);
    }

    // ---- the harness -----------------------------------------------------------------

    private const string Package = " nuget Halves 1.0.0.0 ";

    private static string Full(string descriptors) =>
        $"{ScipSymbols.Scheme}{Package}{descriptors}";

    /// <summary>The fixture, indexed once, with the connection to ask about it.</summary>
    private sealed class Index : IDisposable
    {
        private readonly Fixture fixture;
        private readonly FjordServer server;

        public FjordConnection Connection { get; }

        public Index(Fixture fixture, FjordServer server, FjordConnection connection)
        {
            this.fixture = fixture;
            this.server = server;
            Connection = connection;
        }

        public void Dispose()
        {
            Connection.Dispose();
            server.Dispose();
            fixture.Dispose();
        }
    }

    private static Index Indexed()
    {
        var fixture = Fixture.Copy("partial");
        var server = FjordServer.Serving("partial", "dotnet.sigla");

        var code = Program.Main([
            "--sln", fixture.Path("Split.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//partial",
            "--no-smoke",
        ]);

        Assert.Equal(0, code);

        return new Index(
            fixture,
            server,
            FjordConnection.Connect(server.Socket, "partial", DotnetIndex.Schema));
    }

    private static string Methods(string name) =>
        $"M where M = csharp.Method {{name = N}}; N = csharp.Name \"{name}\"";

    /// <summary>Every <c>csharp.Class</c> fact of a non-generic type name.</summary>
    private static string Classes(string name) =>
        $"C where C = csharp.Class {{name = FN}}; FN = csharp.FullName {{name = N, arity = 0}}; "
        + $"N = csharp.Name \"{name}\"";

    /// <summary>Where every <c>csharp.DefinitionLocation</c> of that class starts.</summary>
    private static List<long> LocatedClasses(FjordConnection connection, string name) =>
        [.. connection.Query(
                "{at = X.location.span.start} where "
                + "X = csharp.DefinitionLocation {definition = {type = {namedType = {class_ = C}}}}; "
                + $"C = csharp.Class {{name = FN}}; FN = csharp.FullName {{name = N, arity = 0}}; "
                + $"N = csharp.Name \"{name}\"")
            .Rows
            .Select(row => Assert.IsType<FjordValue.Int>(Field(row, 0)).Value)];

    private static int Rows(FjordConnection connection, string query) =>
        connection.Query(query).Rows.Count;

    /// <summary>
    /// Where every <c>csharp.DefinitionLocation</c> of the methods with one name starts.
    /// </summary>
    private static List<long> LocatedAt(FjordConnection connection, string name) =>
        [.. connection.Query(
                "{at = X.location.span.start} where "
                + "X = csharp.DefinitionLocation {definition = {method = M}}; "
                + $"M = csharp.Method {{name = N}}; N = csharp.Name \"{name}\"")
            .Rows
            .Select(row => Assert.IsType<FjordValue.Int>(Field(row, 0)).Value)];

    /// <summary>
    /// Where every <c>codemarkup.Definition</c> one symbol has starts.
    /// </summary>
    /// <remarks>
    /// <b>Read positionally, because a record on the wire is a list of values</b> and the
    /// schema supplies the names: the projection is <c>{def = &lt;value&gt;}</c>, the
    /// value's first field is its <c>span</c> and a span's first field is its
    /// <c>start</c>. The whole value comes back rather than the one field because
    /// <c>nyi/value-field</c> stands.
    /// </remarks>
    private static List<long> DefinedAt(FjordConnection connection, string descriptors) =>
        [.. connection.Query(
                "{def = D.value} where D = codemarkup.Definition {symbol = S, file = F}; "
                + $"S = src.Symbol \"{Full(descriptors)}\"")
            .Rows
            .Select(row =>
                Assert.IsType<FjordValue.Int>(Field(Field(Field(row, 0), 0), 0)).Value)];

    private static FjordValue Field(FjordValue value, int index) =>
        Assert.IsType<FjordValue.Record>(value).Fields[index];

    /// <summary>
    /// The documentation comment the one <c>codemarkup.SymbolInfo</c> of a symbol carries.
    /// </summary>
    /// <remarks>
    /// <b><c>Assert.Single</c> is load-bearing rather than convenience.</b> The key is
    /// <c>{symbol}</c> alone, so a second row here is the conflict this test is about
    /// seen from the read side — and the whole value comes back, read positionally,
    /// because <c>nyi/value-field</c> stands.
    /// </remarks>
    private static string Doc(FjordConnection connection, string descriptors) =>
        Assert.IsType<FjordValue.Str>(
            Field(
                Field(
                    Assert.Single(
                        connection.Query(
                            "{info = I.value} where I = codemarkup.SymbolInfo {symbol = S}; "
                            + $"S = src.Symbol \"{Full(descriptors)}\"")
                            .Rows),
                    0),
                1))
            .Value;

    /// <summary>
    /// The symbol strings a query answers, with the scheme and package coordinate taken
    /// off so an assertion reads as the descriptors it is about.
    /// </summary>
    /// <remarks>
    /// <b>Selected by package and *asserted* on the token.</b> The fixture reaches
    /// <c>System.String.Length</c>, so a query for every symbol answers with the
    /// framework's too — but a run that wrote a different token would then leave every
    /// assertion here vacuous, and the token is the one check a fan-out has before it
    /// trusts a string match.
    /// </remarks>
    private static List<string> Symbols(FjordConnection connection, string query) =>
        [.. connection.Query(query).Rows
            .Select(row => Assert.IsType<FjordValue.Str>(row).Value)
            .Where(symbol => symbol.Contains(Package, StringComparison.Ordinal))
            .Select(symbol =>
            {
                Assert.StartsWith($"{ScipSymbols.Scheme}{Package}", symbol, StringComparison.Ordinal);
                return symbol[(ScipSymbols.Scheme.Length + Package.Length)..];
            })];
}
