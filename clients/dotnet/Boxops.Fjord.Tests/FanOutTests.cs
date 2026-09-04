using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>One database per target framework, each holding one target's facts.</b>
/// </para>
/// <para>
/// A project compiled for two frameworks is two compilations — different preprocessor
/// symbols, different references, often different members — and there is no key in the
/// schema that could hold both. Indexing one of the two and calling it the project is what
/// this replaces; it was invisible, because the index that resulted looked complete.
/// </para>
/// </summary>
public sealed class FanOutTests
{
    private static Options Over(Fixture fixture) => new()
    {
        Source = fixture.Path("Targets.slnx"),
        Jobs = 2,
    };

    private static string Dimension(FjordFact setting) =>
        Assert.IsType<FjordValue.Str>(Assert.IsType<FjordValue.Record>(setting.Key).Fields[0]).Value;

    private static string Value(FjordFact setting) =>
        Assert.IsType<FjordValue.Str>(Assert.IsType<FjordValue.Record>(setting.Key).Fields[1]).Value;

    /// <summary>
    /// <b>Every framework the checkout compiles for, sorted and deduped here.</b>
    /// </summary>
    /// <remarks>
    /// The order is this run's to fix: the server's name check accepts <c>#</c> and says
    /// nothing about what follows it, so two runs disagreeing about the order would be two
    /// sets of databases rather than one set written twice.
    /// </remarks>
    [Fact]
    public void A_checkout_fans_out_over_every_framework_it_compiles_for()
    {
        using var fixture = Fixture.Copy("targets");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        Assert.Equal(
            ["net10.0", "net8.0"],
            solution.Targets.Select(target => target.Framework));
        Assert.Empty(solution.Skipped);
    }

    /// <summary>
    /// <b>Compiled as, not compatible with.</b>
    /// </summary>
    /// <remarks>
    /// There is no nearest-compatible reduction: <c>Old</c> targets <c>net8.0</c> and is
    /// absent from the <c>net10.0</c> index rather than present under a framework MSBuild
    /// never built it for. A reduction here would put declarations in a database that the
    /// compiler, asked directly, would resolve differently.
    /// </remarks>
    [Fact]
    public void Each_target_holds_only_the_projects_that_compile_as_it()
    {
        using var fixture = Fixture.Copy("targets");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        var newest = Assert.Single(solution.Targets, target => target.Framework == "net10.0");
        var older = Assert.Single(solution.Targets, target => target.Framework == "net8.0");

        Assert.Equal(["Multi", "Single"], newest.Projects.Select(project => project.Name).Order());
        Assert.Equal(["Multi", "Old"], older.Projects.Select(project => project.Name).Order());
    }

    /// <summary>
    /// <b>The two siblings are different programs, and the difference is observable.</b>
    /// </summary>
    /// <remarks>
    /// Two databases with different names and identical contents would be a naming
    /// convention rather than a fan-out. <c>Multi</c> has a member behind
    /// <c>#if NET10_0_OR_GREATER</c>, so the same file compiled for the two frameworks
    /// declares different things — which is the whole reason a database may hold only one.
    /// </remarks>
    [Fact]
    public void The_same_file_compiled_for_two_frameworks_declares_different_things()
    {
        using var fixture = Fixture.Copy("targets");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        string[] Members(string framework) =>
        [
            .. Assert.Single(solution.Targets, target => target.Framework == framework)
                .Projects
                .Where(project => project.Name == "Multi")
                .SelectMany(project => project.Compile()!
                    .GetTypeByMetadataName("Fixture.Multi.Shared")!
                    .GetMembers()
                    .Select(member => member.Name))
                .Order(),
        ];

        Assert.Contains("Only10", Members("net10.0"));
        Assert.DoesNotContain("Only10", Members("net8.0"));
    }

    /// <summary>
    /// <b><c>--framework</c> selects one target, and the run names what it leaves out.</b>
    /// </summary>
    /// <remarks>
    /// A project silently absent from an index is the failure mode this run exists to
    /// remove. <c>Old</c> compiles for none of the frameworks being indexed — it is not
    /// broken and not indexed, and only the run can say so.
    /// </remarks>
    [Fact]
    public void A_framework_selects_one_target_and_the_run_names_what_it_leaves_out()
    {
        using var fixture = Fixture.Copy("targets");
        var log = new StringWriter();

        var solution = Loader.Load(
            Over(fixture) with { Framework = "net10.0" },
            fixture.Root,
            log);

        var only = Assert.Single(solution.Targets);
        Assert.Equal("net10.0", only.Framework);
        Assert.Equal(["Multi", "Single"], only.Projects.Select(project => project.Name).Order());

        Assert.Equal(["Old.csproj"], solution.Skipped);
        Assert.Contains("Old.csproj", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A framework nothing compiles for is refused, with what the checkout does build.</b>
    /// </summary>
    /// <remarks>
    /// The alternative is an empty database with a plausible name, which is worse than no
    /// database: it answers every query with nothing and looks exactly like a repository
    /// with no code in it.
    /// </remarks>
    [Fact]
    public void A_framework_no_project_compiles_for_is_refused_by_name()
    {
        using var fixture = Fixture.Copy("targets");

        var refused = Assert.Throws<InvalidOperationException>(() => Loader.Load(
            Over(fixture) with { Framework = "net6.0" },
            fixture.Root,
            TextWriter.Null));

        Assert.Contains("net6.0", refused.Message, StringComparison.Ordinal);
        Assert.Contains("net10.0", refused.Message, StringComparison.Ordinal);
        Assert.Contains("net8.0", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A database says which framework it was built for, exactly once.</b>
    /// </summary>
    /// <remarks>
    /// The fan-out's claim from the consumer's side: a database holds one target's facts,
    /// so joining two of them is a join somebody chose rather than one that happened. The
    /// other axes travel with it because every one of them is something a consumer has to
    /// agree with before it can read a single fact.
    /// </remarks>
    [Fact]
    public void A_database_says_the_one_framework_it_was_built_for()
    {
        var options = new Options
        {
            Source = "/checkout/App.slnx",
            Configuration = "Release",
            Repo = "github.com/boxops-uk/fjord",
            Revision = "29636a3",
        };

        var settings = Provenance.Of(options, "/checkout", "net8.0", "0.2.0");

        var framework = Assert.Single(settings, setting => Dimension(setting) == "framework");
        Assert.Equal("net8.0", Value(framework));

        Assert.Equal(
            ["configuration", "framework", "index-root", "language", "position-encoding",
             "producer", "repo", "revision", "symbol-scheme"],
            settings.Select(Dimension).Order());

        Assert.Equal("Release", Value(Assert.Single(settings, s => Dimension(s) == "configuration")));
        Assert.Equal("/checkout", Value(Assert.Single(settings, s => Dimension(s) == "index-root")));
        Assert.Equal("utf8", Value(Assert.Single(settings, s => Dimension(s) == "position-encoding")));
        Assert.Equal("scip-csharp-2", Value(Assert.Single(settings, s => Dimension(s) == "symbol-scheme")));
        Assert.Equal(
            "boxops-fjord-indexer/0.2.0",
            Value(Assert.Single(settings, s => Dimension(s) == "producer")));
    }

    /// <summary>
    /// <b>A run that states no provenance carries none, rather than guessing.</b>
    /// </summary>
    [Fact]
    public void A_run_that_states_no_repository_writes_no_repository()
    {
        var settings = Provenance.Of(
            new Options { Source = "/checkout" }, "/checkout", "net10.0", "0.2.0");

        Assert.DoesNotContain(settings, setting => Dimension(setting) == "repo");
        Assert.DoesNotContain(settings, setting => Dimension(setting) == "revision");

        // And the configuration is still stated, because "Debug because nobody said" is an
        // answer and silence is not.
        Assert.Equal("Debug", Value(Assert.Single(settings, s => Dimension(s) == "configuration")));
    }

    /// <summary>
    /// <b>The run writes one output per target, and they are not the same bytes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// End to end, through <c>Main</c>, because the fan-out is a property of the run rather
    /// than of the loader: it is the loop that connects, walks and reports once per target.
    /// </para>
    /// <para>
    /// <c>--emit</c> is flavoured with it. The flag opens its path for writing, so without
    /// that a fan-out would leave one file holding whichever target ran last — a golden
    /// that depends on the order of a loop.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_run_writes_one_output_per_target()
    {
        using var fixture = Fixture.Copy("targets");
        var emit = Path.Combine(fixture.Root, "blocks.bin");

        var code = Program.Main([
            "--source", fixture.Path("Targets.slnx"),
            "--root", fixture.Root,
            "--dry-run", "--no-smoke", "--emit", emit,
        ]);

        Assert.Equal(0, code);
        Assert.False(File.Exists(emit), "the unflavoured path is not written when fanning out");

        var newest = Path.Combine(fixture.Root, "blocks.net10.0.bin");
        var older = Path.Combine(fixture.Root, "blocks.net8.0.bin");

        Assert.True(File.Exists(newest), newest);
        Assert.True(File.Exists(older), older);
        Assert.NotEqual(File.ReadAllBytes(newest), File.ReadAllBytes(older));
    }

    /// <summary>
    /// <b><c>--strict</c> is how CI refuses an index that is quietly incomplete.</b>
    /// </summary>
    /// <remarks>
    /// Off by default, because a developer indexing a repository with one unbuildable
    /// project wants the other four hundred. On, a project left out is the run's answer
    /// rather than a line in its output — which is the difference between a check and a
    /// convention.
    /// </remarks>
    [Fact]
    public void Strict_refuses_a_run_that_leaves_a_project_out()
    {
        using var fixture = Fixture.Copy("targets");

        string[] Run(params string[] extra) =>
        [
            "--source", fixture.Path("Targets.slnx"),
            "--root", fixture.Root,
            "--dry-run", "--no-smoke", "--framework", "net10.0",
            .. extra,
        ];

        // `Old` compiles for net8.0 alone, so a net10.0 index leaves it out either way.
        Assert.Equal(0, Program.Main(Run()));
        Assert.Equal(1, Program.Main(Run("--strict")));

        // And a run that leaves nothing out is not refused by `--strict`.
        Assert.Equal(0, Program.Main([
            "--source", fixture.Path("Targets.slnx"),
            "--root", fixture.Root,
            "--dry-run", "--no-smoke", "--strict",
        ]));
    }
}
