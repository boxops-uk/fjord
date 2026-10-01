using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <b>Two compilations that both describe one entity must describe it the same way.</b>
/// </summary>
/// <remarks>
/// <para>
/// A predicate keyed on an entity and valued with a description of it is written by every
/// compilation that can see the entity — including ones that see it through a narrower
/// window. A project the run design-time-built is described fully; the same project merely
/// discovered by a glob is described from its XML. A symbol whose source this compilation
/// holds has its doc comment; the same symbol reached through a reference assembly does
/// not. The second write is then the same key with a different value, which
/// <c>ops-I5</c> rejects — so a database cannot be filled by more than one run.
/// </para>
/// <para>
/// Reported as <a href="https://github.com/boxops-uk/fjord/issues/82">#82</a>. This is the
/// guard for the class rather than for the two predicates the report names: the comparison
/// is a <b>differential</b>, and the oracle is the other database. Nothing here knows which
/// predicates are at risk.
/// </para>
/// <para>
/// <b>Why two databases rather than one.</b> A conflict is what the server does about this,
/// so a single database can never hold both descriptions — the second is refused and rolled
/// back, and the disagreement is invisible. Indexing each project into its own database is
/// what makes both claims observable at once, which is also how the report's own
/// investigation eventually found the divergence.
/// </para>
/// </remarks>
public sealed class ObserverAgreementTests
{
    /// <summary>
    /// How to read each value-side predicate's key <i>as content</i>, so two databases can
    /// be compared without their local fact ids meeting.
    /// </summary>
    /// <remarks>
    /// A key is mostly references, and a reference is a number assigned per database — so
    /// comparing keys means resolving them to the strings they stand for. One row per
    /// predicate, and <see cref="Every_predicate_with_a_value_side_says_how_its_key_reads"/>
    /// is what stops a new one being added to the schema without saying how it reads.
    /// </remarks>
    private static readonly (string Predicate, Reach Reach, string Query)[] Keyed =
    [
        // One file, named by its path.
        ("src.FileLanguage", Reach.Shared,
            "{k = P, v = X.value} where X = src.FileLanguage {file = src.File P}"),
        ("src.FileDigest", Reach.Shared,
            "{k = P, v = X.value} where X = src.FileDigest {file = src.File P}"),
        ("src.FileOrigin", Reach.Shared,
            "{k = P, v = X.value} where X = src.FileOrigin {file = src.File P}"),
        ("src.FileInfo", Reach.Shared,
            "{k = P, v = X.value} where X = src.FileInfo {file = src.File P}"),

        // A file and a line number.
        ("src.FileLine", Reach.Shared,
            "{k = {f = P, l = L}, v = X.value} "
            + "where X = src.FileLine {file = src.File P, line = L}"),
        ("src.FileLineStyles", Reach.Shared,
            "{k = {f = P, l = L}, v = X.value} "
            + "where X = src.FileLineStyles {file = src.File P, line = L}"),

        // A project, named by the path of its project file.
        ("msbuild.Project", Reach.Shared,
            "{k = P, v = X.value} where X = msbuild.Project {file = src.File P}"),
        ("msbuild.ProjectCompilation", Reach.Shared,
            "{k = {p = P, f = F}, v = X.value} where X = msbuild.ProjectCompilation "
            + "{project = msbuild.Project {file = src.File P}, framework = F}"),
        ("msbuild.PackageReference", Reach.Shared,
            "{k = {p = P, n = N, v2 = V}, v = X.value} where X = msbuild.PackageReference "
            + "{project = msbuild.Project {file = src.File P}, "
            + "package = msbuild.Package {name = N, version = V}}"),

        // A symbol, named by its SCIP string.
        ("codemarkup.SymbolInfo", Reach.PendingIssue84,
            "{k = S, v = X.value} where X = codemarkup.SymbolInfo {symbol = src.Symbol S}"),
        ("codemarkup.Definition", Reach.Disjoint,
            "{k = {s = S, f = P}, v = X.value} where X = codemarkup.Definition "
            + "{symbol = src.Symbol S, file = src.File P}"),
        ("codemarkup.FileDefinition", Reach.Disjoint,
            "{k = {f = P, st = ST, ln = LN, s = S}, v = X.value} "
            + "where X = codemarkup.FileDefinition "
            + "{file = src.File P, span = {start = ST, length = LN}, symbol = src.Symbol S}"),
    ];

    /// <summary>Whether two runs are expected to describe any of the same entities.</summary>
    /// <remarks>
    /// <b>Zero overlap is not success, so it has to be declared.</b> Two runs that describe
    /// no entity in common prove nothing about agreement — and if they *should* have had one
    /// in common, zero overlap means they disagree about what things are <i>called</i>,
    /// which is worse than disagreeing about a description because nothing conflicts and
    /// cross-project references quietly fail to resolve.
    /// </remarks>
    private enum Reach
    {
        /// <summary>Both runs describe some of the same entities. No overlap is a failure.</summary>
        Shared,

        /// <summary>
        /// Each run describes only its own, so disjointness is the correct answer — a
        /// definition site belongs to the compilation that holds the source.
        /// </summary>
        Disjoint,

        /// <summary>
        /// Expected to share, and does not — because of
        /// <a href="https://github.com/boxops-uk/fjord/issues/84">#84</a>, where a symbol
        /// defined in one project and referenced from another gets two names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Only zero overlap is excused, and only for this predicate.</b> Where two views
        /// do share a key the comparison runs as it does everywhere else — within one run
        /// symbols are named consistently, so a whole-solution view overlaps a single-project
        /// one on that project's own symbols and those are compared normally. Excusing the
        /// predicate outright would have thrown that away.
        /// </para>
        /// <para>
        /// Nothing here asserts the defect <i>persists</i>: once #84 is fixed the two views
        /// simply start sharing keys and the comparison begins running, which is the
        /// behaviour wanted and needs no flag day. #84 carries the note to flip this row.
        /// </para>
        /// </remarks>
        PendingIssue84,
    }

    /// <summary>Value-side predicates deliberately outside the comparison, and why.</summary>
    private static readonly Dictionary<string, string> Excused = new(StringComparer.Ordinal);

    /// <summary>
    /// <b>Every predicate with a value side is either compared or excused by name.</b>
    /// </summary>
    /// <remarks>
    /// The census half. A predicate added to the schema with a value side is a new member of
    /// the class in this test's docs, and a guard that silently did not cover it would pass
    /// while the bug returned — which is exactly how <see cref="Keyed"/> would rot.
    /// </remarks>
    [Fact]
    public void Every_predicate_with_a_value_side_says_how_its_key_reads()
    {
        var covered = Keyed.Select(row => row.Predicate)
            .Concat(Excused.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var missing = DotnetIndex.Schema.Predicates
            .Where(predicate => predicate.Value is not null && !predicate.IsVirtual)
            .Select(predicate => predicate.Name)
            .Where(name => !covered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} predicate(s) with a value side are neither compared nor "
            + $"excused:\n    {string.Join("\n    ", missing)}");
    }

    /// <summary>
    /// <b>Two observers of one entity describe it identically, or the class is broken.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>graph</c>'s <c>A</c> references <c>B</c>, so indexing <c>A</c> design-time-builds
    /// <c>A</c> and reaches <c>B</c> through its build output, while indexing <c>B</c> builds
    /// <c>B</c>. Each run therefore describes <c>B</c>, from two windows — which is the
    /// asymmetry #82 is about, and the one a single <c>--sln</c> run does not have.
    /// </para>
    /// <para>
    /// Every disagreement is collected before anything is asserted. One failure per run would
    /// turn a class into a queue, and the point of a differential is the whole list.
    /// </para>
    /// </remarks>
    [Fact]
    public void Two_observers_of_one_entity_describe_it_the_same_way()
    {
        using var fixture = Fixture.Copy("graph");

        // Built first, because the second run must reach the other project through a build
        // output rather than through its source. A run that had to build it would see the
        // source and agree, which is the case that already worked.
        fixture.Build("src/A/A.csproj");
        fixture.Build("src/B/B.csproj");

        using var server = FjordServer.ServingAll("dotnet.sigla", "a", "b");

        foreach (var (project, database) in Apart)
        {
            Assert.Equal(0, Program.Main([
                "--project", fixture.Path(project),
                "--root", fixture.Root,
                "--at", $"{server.Socket}//{database}",
                "--no-smoke",
            ]));
        }

        using var left = FjordConnection.Connect(server.Socket, "a", DotnetIndex.Schema);
        using var right = FjordConnection.Connect(server.Socket, "b", DotnetIndex.Schema);

        AssertAgreement(("a", left), ("b", right));
    }

    /// <summary>
    /// <b>One run over the whole solution agrees with the same projects indexed apart.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second arm, and the one that says <i>which</i> description is right. A
    /// <c>--sln</c> run puts both projects in one workspace, so each is design-time-built
    /// and each is seen as source — the view with the most information, and the one the
    /// report notes does not conflict. Comparing it against the per-project runs therefore
    /// does not merely find a disagreement, it names the authority in each one.
    /// </para>
    /// <para>
    /// It is also the arm that would catch a <i>second</i> mechanism. #82 reports a
    /// conflict inside a single <c>--sln</c> run on a repository that is not public, which
    /// the two-run case does not explain; if one exists in a shape this fixture has, the
    /// run below fails to complete rather than disagreeing.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_whole_solution_run_agrees_with_the_same_projects_indexed_apart()
    {
        using var fixture = Fixture.Copy("graph");
        fixture.Build("src/A/A.csproj");
        fixture.Build("src/B/B.csproj");

        using var server = FjordServer.ServingAll("dotnet.sigla", "whole", "a", "b");

        // The claim the report makes about one run, asserted rather than assumed: both
        // projects in one workspace describe each other identically, so the duplicate
        // writes dedup instead of colliding.
        Assert.Equal(0, Program.Main([
            "--sln", fixture.Path("Graph.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//whole",
            "--no-smoke",
        ]));

        foreach (var (project, database) in Apart)
        {
            Assert.Equal(0, Program.Main([
                "--project", fixture.Path(project),
                "--root", fixture.Root,
                "--at", $"{server.Socket}//{database}",
                "--no-smoke",
            ]));
        }

        using var whole = FjordConnection.Connect(server.Socket, "whole", DotnetIndex.Schema);
        using var left = FjordConnection.Connect(server.Socket, "a", DotnetIndex.Schema);
        using var right = FjordConnection.Connect(server.Socket, "b", DotnetIndex.Schema);

        AssertAgreement(("whole", whole), ("a", left), ("b", right));
    }

    /// <summary>The project-by-project runs, as (project, database).</summary>
    private static readonly (string Project, string Database)[] Apart =
        [("src/A/A.csproj", "a"), ("src/B/B.csproj", "b")];

    /// <summary>
    /// <b>The same comparison over <c>observers</c>, where the two views agree on names.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>graph</c> sets <c>AssemblyName</c>, so a symbol defined in one project and
    /// referenced from another gets two names (<a
    /// href="https://github.com/boxops-uk/fjord/issues/84">#84</a>) and the two views never
    /// meet on a symbol at all. That hides the divergence #82 is actually about.
    /// </para>
    /// <para>
    /// <c>observers</c> sets neither, so project name and assembly name coincide, the two
    /// views agree on what the method is <i>called</i>, and they can therefore disagree
    /// about its description — which is what this arm measures and what the fix must remove.
    /// </para>
    /// </remarks>
    [Fact]
    public void Two_observers_of_one_symbol_describe_it_the_same_way()
    {
        using var fixture = Fixture.Copy("observers");
        fixture.Build("Lib/Lib.csproj");
        fixture.Build("App/App.csproj");

        using var server = FjordServer.ServingAll("dotnet.sigla", "lib", "app");

        foreach (var (project, database) in new[]
        {
            ("Lib/Lib.csproj", "lib"), ("App/App.csproj", "app"),
        })
        {
            Assert.Equal(0, Program.Main([
                "--project", fixture.Path(project),
                "--root", fixture.Root,
                "--at", $"{server.Socket}//{database}",
                "--no-smoke",
            ]));
        }

        using var lib = FjordConnection.Connect(server.Socket, "lib", DotnetIndex.Schema);
        using var app = FjordConnection.Connect(server.Socket, "app", DotnetIndex.Schema);

        AssertAgreement(("lib", lib), ("app", app));
    }

    /// <summary>
    /// Every pair of databases must describe every entity they share identically.
    /// </summary>
    /// <remarks>
    /// Every disagreement is collected before anything is asserted. One failure per run
    /// would turn a class into a queue, and the point of a differential is the whole list.
    /// </remarks>
    private static void AssertAgreement(params (string Name, FjordConnection Connection)[] views)
    {
        var findings = new List<string>();
        var overlapped = 0;

        foreach (var (predicate, reach, query) in Keyed)
        {
            var described = views
                .Select(view => (view.Name, Rows: Describe(view.Connection, query)))
                .ToList();

            // Nothing wrote any: this fixture does not reach the predicate, which is a gap
            // in the fixture rather than a disagreement.
            if (described.All(view => view.Rows.Count == 0))
            {
                continue;
            }

            for (var i = 0; i < described.Count; i++)
            {
                for (var j = i + 1; j < described.Count; j++)
                {
                    var (leftName, mine) = described[i];
                    var (rightName, theirs) = described[j];

                    if (mine.Count == 0 || theirs.Count == 0)
                    {
                        continue;
                    }

                    var shared = mine.Keys.Intersect(theirs.Keys, StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToList();

                    if (shared.Count == 0)
                    {
                        if (reach == Reach.Shared)
                        {
                            findings.Add(
                                $"{predicate}  ({leftName} vs {rightName})\n      both wrote rows "
                                + $"({mine.Count} and {theirs.Count}) and share no key, so they "
                                + "disagree about what the same entity is *called*\n      "
                                + $"{leftName}     "
                                + string.Join(
                                    $"\n      {leftName}     ",
                                    mine.Keys.Order(StringComparer.Ordinal))
                                + $"\n      {rightName}     "
                                + string.Join(
                                    $"\n      {rightName}     ",
                                    theirs.Keys.Order(StringComparer.Ordinal)));
                        }

                        continue;
                    }

                    overlapped++;

                    foreach (var key in shared)
                    {
                        if (!string.Equals(mine[key], theirs[key], StringComparison.Ordinal))
                        {
                            findings.Add(
                                $"{predicate}  ({leftName} vs {rightName})\n      key   {key}"
                                + $"\n      {leftName}     {mine[key]}"
                                + $"\n      {rightName}     {theirs[key]}");
                        }
                    }
                }
            }
        }

        Assert.True(
            overlapped > 0,
            "no predicate was described by two views, so this proved nothing — the fixture "
            + "or the key queries have stopped overlapping");

        Assert.True(
            findings.Count == 0,
            $"{findings.Count} finding(s) across observers of one entity:\n    "
            + string.Join("\n    ", findings));
    }

    /// <summary>Every <c>{k, v}</c> row of <paramref name="query"/>, as key to value.</summary>
    /// <remarks>
    /// <para>
    /// A row's fields are <b>positional</b> — the schema supplies the names — so the
    /// positions of <c>k</c> and <c>v</c> are read out of the result's own shape rather
    /// than assumed. A query's head fields are sorted by name at lowering, so they would
    /// in fact be in that order; reading the shape is what stops this test depending on
    /// that quietly.
    /// </para>
    /// <para>
    /// A key appearing twice in one database would be a different bug — one key names one
    /// fact — so that is asserted rather than letting a later comparison read whichever
    /// row happened to arrive last.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> Describe(FjordConnection connection, string query)
    {
        var result = connection.Query(query);
        var shape = Assert.IsType<FjordType.Record>(result.Shape);

        var at = (string name) =>
        {
            for (var index = 0; index < shape.Fields.Count; index++)
            {
                if (string.Equals(shape.Fields[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException($"the query's head has no `{name}`: {query}");
        };

        var (key, value) = (at("k"), at("v"));
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in result.Rows)
        {
            var fields = Assert.IsType<FjordValue.Record>(row).Fields;
            var rendered = Render(fields[key]);

            Assert.False(
                rows.ContainsKey(rendered),
                $"one key named two facts in one database: {rendered}");

            rows[rendered] = Render(fields[value]);
        }

        return rows;
    }

    /// <summary>A value as text, for comparing one database's answer with another's.</summary>
    /// <remarks>
    /// <b>A reference renders as its raw id and that is deliberate</b>: an id is assigned
    /// per database, so two of them are never comparable. No value side in the table above
    /// holds one — if one ever does, this renders something that always differs, and the
    /// disagreement it reports is this renderer's rather than the producer's. Resolve the
    /// reference in that predicate's query instead.
    /// </remarks>
    private static string Render(FjordValue value) => value switch
    {
        FjordValue.Str text => text.Value,
        FjordValue.Int number =>
            number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FjordValue.Bytes payload => Convert.ToHexString(payload.Value.Span),
        FjordValue.Ref reference => $"#{reference.Value}",
        FjordValue.Record record =>
            "{" + string.Join(", ", record.Fields.Select(Render)) + "}",
        FjordValue.Union union => $"{union.Disc} = {Render(union.Value)}",
        _ => value.ToString() ?? "?",
    };
}
