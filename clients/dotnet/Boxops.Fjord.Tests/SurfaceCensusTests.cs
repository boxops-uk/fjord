using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The reference corpus covers the C# language surface, and the surface is defined by the
/// specification rather than by this repository.</b>
/// </para>
/// <para>
/// <c>tests/fixtures/surface/POPULATION.tsv</c> holds one row per numbered heading of ECMA-334
/// draft-v9 — every clause file diffed against its own heading tree, so the row count is a
/// property of the document — plus one per <c>SyntaxKind</c>, <c>SymbolKind</c>, <c>TypeKind</c>,
/// <c>MethodKind</c> and <c>LanguageVersion</c> member of the pinned compiler, read by reflection,
/// plus one per language feature the standard predates — 1,129 rows, which is the whole standard
/// and not a reading of it: clauses 1 to 24, annexes A to E, and the two populations no document
/// carries. <c>SURFACE.tsv</c> is this corpus's answer to each of the 1,129: exercised here, or
/// not applicable, or quarantined, or unbuildable, each for a reason a reader can check.
/// </para>
/// <para>
/// <b>Why a census and not a list of features.</b> A list of what somebody remembered to cover is
/// exhaustive only until the next thing nobody thought of, and this repository has been bitten by
/// exactly that: seven symbol collisions, each from a shape no list held. Deriving the population
/// externally is what makes "the entire surface" checkable — and it caught a hole immediately.
/// Clause 18, <i>Extended indexing and slicing</i>, had been read by nobody, and it carries four
/// confirmed defects: every indexer in a type minting one symbol string, element access writing no
/// reference at all, <c>..</c> binding to four different members by which operand is absent, and a
/// corelib target whose symbol string changes with the target framework.
/// </para>
/// <para>
/// These tests read files and index nothing. The claims that need a run are in
/// <c>SurfaceCorpusTests</c>.
/// </para>
/// </summary>
public sealed class SurfaceCensusTests
{
    /// <summary>The fixture, read where it lies: nothing here builds, so nothing needs a copy.</summary>
    private static string Root => Path.Combine(
        FjordServer.RepositoryRoot, "clients", "dotnet", "tests", "fixtures", "surface");

    /// <summary>
    /// A population row: what the specification names, and what an index holds about it.
    /// </summary>
    /// <param name="Declares">
    /// <c>declaration</c>, <c>reference</c>, <c>both</c> or <c>neither</c> — the reading agent's
    /// judgement, and the column
    /// <see cref="A_member_an_index_holds_a_fact_about_is_never_answered_not_applicable"/> holds
    /// the census to.
    /// </param>
    /// <param name="Area">Which population it came from: a clause, the compiler, a feature.</param>
    /// <param name="Hazard"><c>yes</c> where the shape can make two declarations want one identity.</param>
    private sealed record Member(string Id, string Declares, string Area, string Hazard, string Title);

    /// <summary>A census row: this corpus's verdict on one population member.</summary>
    /// <param name="Coverage">One of exercised, not-applicable, quarantined, unbuildable.</param>
    /// <param name="Evidence">The file and construct, or the reason there is none.</param>
    private sealed record Verdict(string Id, string Coverage, string Evidence);

    /// <remarks>
    /// Tab-separated and split by hand rather than through a CSV library, because both files are
    /// written and read only here and a dependency for them would be the larger cost. A field
    /// holding a tab would break it, which is why the writer strips them.
    /// </remarks>
    private static IReadOnlyList<string[]> Fields(string name)
    {
        var path = Path.Combine(Root, name);

        Assert.True(File.Exists(path), $"the corpus census {name} is missing from {Root}");

        return
        [
            .. File.ReadLines(path)
                .Skip(1)
                .Where(line => line.Length > 0)
                .Select(line => line.Split('\t')),
        ];
    }

    private static IReadOnlyList<Member> Population() =>
    [
        .. Fields("POPULATION.tsv")
            .Select(f => new Member(f[0], At(f, 1), At(f, 2), At(f, 3), At(f, 4))),
    ];

    private static IReadOnlyList<Verdict> Census() =>
    [
        .. Fields("SURFACE.tsv").Select(f => new Verdict(f[0], At(f, 1), At(f, 2))),
    ];

    /// <summary>A trailing empty field is elided by some writers, so a short row is not a bug.</summary>
    private static string At(string[] fields, int index) =>
        index < fields.Length ? fields[index] : string.Empty;

    /// <summary>
    /// <b>Every population member has exactly one verdict, and every verdict names a member.</b>
    /// </summary>
    /// <remarks>
    /// The two failures this refuses are opposite and both silent. A population id with no row is
    /// a piece of the language nobody decided about — the shape of the clause-18 hole, which
    /// survived because no count was ever taken. A census id with no population row is a verdict
    /// about something the specification does not contain, which is how a census drifts into
    /// self-report.
    /// </remarks>
    [Fact]
    public void The_census_answers_the_whole_population_and_nothing_else()
    {
        var population = Population();
        var census = Census();

        var populated = population.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var answered = census.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);

        // Stated as sorted sets rather than counts: a failure has to name the ids, because the
        // number alone sends somebody looking through 1,129 rows for it.
        Assert.Equal(
            [.. populated.Order(StringComparer.Ordinal)],
            [.. answered.Order(StringComparer.Ordinal)]);

        var twice = census.GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal);

        Assert.Equal([], twice);
    }

    /// <summary>
    /// <b>Nothing an index holds a fact about is answered "not applicable".</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the assertion that refuses a lazy census, and it is the only one that can. The
    /// population's <c>declares</c> column is the judgement of the agent that read the clause:
    /// <c>declaration</c>, <c>reference</c> or <c>both</c> means an index writes something down
    /// about that member. Once that is on record, <c>not-applicable</c> is a contradiction — so a
    /// member the corpus does not exercise has to be marked <c>quarantined</c> (exercising it kills
    /// the indexing run, so it lives in a project indexed alone) or <c>unbuildable</c> (no
    /// compiling C# produces it), both of which are claims a reader can check.
    /// </para>
    /// <para>
    /// Without this, a census passes by writing <c>not-applicable</c> against everything hard,
    /// which is a coverage report that reports its own gaps as absences.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_member_an_index_holds_a_fact_about_is_never_answered_not_applicable()
    {
        var census = Census().ToDictionary(r => r.Id, r => r, StringComparer.Ordinal);

        var wrong = Population()
            .Where(row => row.Declares is "declaration" or "reference" or "both")
            .Where(row => census.TryGetValue(row.Id, out var answer)
                && answer.Coverage == "not-applicable")
            .Select(row => $"{row.Id} ({row.Declares}, {row.Area}): {census[row.Id].Evidence}")
            .Order(StringComparer.Ordinal);

        Assert.Equal([], wrong);
    }

    /// <summary>
    /// <b>Every verdict is one of the four, and an exercised one names a file that exists.</b>
    /// </summary>
    /// <remarks>
    /// A census row's evidence is the only thing tying a claim of coverage to the corpus, and a
    /// path that has been renamed since is a claim that has quietly stopped being true. The check
    /// is deliberately weak — the file exists, not that the construct is in it — because a
    /// stronger textual check would be a second, worse parser. What actually establishes that the
    /// construct is there is <c>SurfaceCorpusTests</c>, which indexes the corpus and asks the
    /// database.
    /// </remarks>
    [Fact]
    public void Every_verdict_is_one_of_the_four_and_names_evidence_that_exists()
    {
        string[] verdicts = ["exercised", "not-applicable", "quarantined", "unbuildable"];

        var census = Census();

        Assert.Equal(
            [],
            census.Where(r => !verdicts.Contains(r.Coverage, StringComparer.Ordinal))
                .Select(r => $"{r.Id}: {r.Coverage}")
                .Order(StringComparer.Ordinal));

        // An exercised row's evidence leads with the file, then an em dash and the construct.
        var missing = census
            .Where(r => r.Coverage == "exercised")
            .Select(r => (r.Id, File: r.Evidence.Split('—')[0].Split('#')[0].Trim()))
            .Where(x => x.File.Length > 0 && !File.Exists(Path.Combine(Root, x.File)))
            .Select(x => $"{x.Id}: no such file {x.File}")
            .Order(StringComparer.Ordinal);

        Assert.Equal([], missing);
    }

    /// <summary>
    /// <b>Every project is in the solution, or deliberately out of it.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Build output is not checked here, because git already refuses it.</b>
    /// <c>clients/dotnet/.gitignore</c> ignores <c>obj/</c> and <c>bin/</c>, so a restore's
    /// leftovers cannot be committed — while an <c>obj/</c> in a working tree is *ordinary*: an
    /// IDE design-time builds a fixture the moment it appears in the workspace. An assertion over
    /// the filesystem would therefore fail on a developer's machine for a reason that has nothing
    /// to do with the repository, which is a test that trains people to ignore it.
    /// </para>
    /// <para>
    /// And the solution's membership is load-bearing rather than incidental: the projects under
    /// <c>quarantine/</c> each provoke a refused write, which fails the write stream and loses
    /// every fact after it in the walk. They are excluded so the whole-corpus run completes, and
    /// each is indexed alone so the conflict it provokes is attributable to it. A quarantine
    /// project that drifted into <c>Surface.slnx</c> would take the rest of the corpus down with
    /// it, and the symptom would be a corpus that measures nothing while passing.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_solution_holds_every_project_except_the_quarantined_ones()
    {
        var projects = Directory
            .EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .ToList();

        Assert.NotEmpty(projects);

        var solution = File.ReadAllText(Path.Combine(Root, "Surface.slnx"));

        var listed = projects.Where(p => solution.Contains(p, StringComparison.Ordinal));
        var quarantined = projects.Where(p => p.StartsWith("quarantine/", StringComparison.Ordinal));

        // Every project is listed exactly when it is not quarantined.
        Assert.Equal(
            [.. projects.Except(quarantined).Order(StringComparer.Ordinal)],
            [.. listed.Order(StringComparer.Ordinal)]);

        Assert.NotEmpty(quarantined);
    }
}
