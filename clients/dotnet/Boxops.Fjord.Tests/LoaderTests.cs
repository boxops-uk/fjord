using System.Linq;
using System.Threading;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Turning a checkout into compilations, gated on a real solution.</b>
/// </para>
/// <para>
/// These drive MSBuild — a design-time build per project, out of process, the same one a
/// real run does. That is the point: every defect these gates are about is a defect in
/// what MSBuild and Roslyn do with each other, and a mocked analyzer result would be this
/// file agreeing with itself about behaviour neither of them has.
/// </para>
/// </summary>
public sealed class LoaderTests
{
    private static Options Over(Fixture fixture, string solution = "Graph.slnx") => new()
    {
        Source = fixture.Path(solution),
        Jobs = 2,
    };

    /// <summary>
    /// The one target framework these fixtures have.
    /// </summary>
    /// <remarks>
    /// Asserting there is one is part of every test below: a checkout that compiles for a
    /// single framework fans out to a single database, and would otherwise have had every
    /// one of its databases renamed the day the fan-out landed.
    /// </remarks>
    private static LoadedTarget Only(LoadedSolution solution) => Assert.Single(solution.Targets);

    /// <summary>
    /// <b>The workspace holds what was built, and nothing it built to find out.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the observable form of "no MSBuild during workspace load". Adding a result
    /// with <c>addProjectReferences: true</c> walks to every project it names and
    /// <i>builds</i> the ones nobody asked for — so the fixture's C, which the solution
    /// does not list, used to arrive in the workspace as a third project and cost a
    /// design-time build to get there. A project cannot appear without one, so counting
    /// them is counting the builds.
    /// </para>
    /// <para>
    /// It is also the invariant <c>--max-projects</c> needs: a run told to stop after two
    /// projects that walks three is not stopping.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_workspace_holds_exactly_the_projects_that_were_built()
    {
        using var fixture = Fixture.Copy("graph");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        Assert.Equal(["A", "B"], Only(solution).Projects.Select(project => project.Name).Order());
    }

    /// <summary>
    /// <b>A reference to a project outside the set resolves to that project's assembly.</b>
    /// </summary>
    /// <remarks>
    /// Half the trade the loader makes; the other half is the test below. A reference
    /// from B to C — which the solution does not list — lands on a symbol from
    /// <c>Fixture.C.dll</c>, with no source location: the reference still resolves, and
    /// the index says truthfully that it points outside itself.
    /// </remarks>
    [Fact]
    public void A_reference_to_a_project_outside_the_set_is_metadata()
    {
        using var fixture = Fixture.Copy("graph");

        // C is compiled for real: a reference to a project outside the indexed set
        // degrades to its assembly, and there is no assembly until something builds one.
        fixture.Build("external/C/C.csproj");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        var b = Assert.Single(Only(solution).Projects, project => project.Name == "B");
        var compilation = b.Compile()!;

        var deep = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName("Fixture.C.Deep"));
        Assert.Equal("Fixture.C", deep.ContainingAssembly.Name);
        Assert.DoesNotContain(deep.Locations, location => location.IsInSource);
    }

    /// <summary>
    /// <b>A symbol declared in B and used in A has a source location, over a built checkout.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half, and the one worth stating carefully: it has to be <i>A's</i> model
    /// answering about <i>B's</i> type. B's own compilation knows B's types however the
    /// graph is wired, so a gate phrased over B is green with no wiring at all — and one
    /// over an unbuilt checkout is green because there is no assembly to be ambiguous
    /// with.
    /// </para>
    /// <para>
    /// <b>The reference on A's command line is the reference assembly, not the output.</b>
    /// <c>ProduceReferenceAssembly</c> is the SDK default, so a built B arrives as
    /// <c>obj/…/ref/Fixture.B.dll</c> while <c>TargetPath</c> names
    /// <c>bin/…/Fixture.B.dll</c>. Remove only what <c>TargetPath</c> spells and nothing
    /// is removed: A holds B from source <i>and</i> from a dll, every type in B is
    /// ambiguous, and Roslyn answers nothing rather than choosing.
    /// </para>
    /// <para>
    /// <b>Then the same claim in the numbers a run reports.</b> Nothing else asserts
    /// <c>Unresolved</c>, and it is the counter this failure lands in: a name the walk
    /// cannot bind writes no reference at all, so the index comes out smaller and says
    /// nothing about it. Two references and none unresolved is the whole of what this
    /// fixture has to say.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_type_declared_in_B_and_used_in_A_resolves_to_source_in_a_built_checkout()
    {
        using var fixture = Fixture.Copy("graph");

        // A alone, which drags B in behind it — the normal state of any checkout
        // somebody has worked in, and the state this is about.
        fixture.Build("src/A/A.csproj");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        var a = Assert.Single(Only(solution).Projects, project => project.Name == "A");
        var compilation = a.Compile()!;

        var tree = Assert.Single(
            compilation.SyntaxTrees,
            candidate => System.IO.Path.GetFileName(candidate.FilePath) == "A.cs");

        var use = Assert.Single(
            tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>());

        var constructed = Assert.IsAssignableFrom<IMethodSymbol>(
            compilation.GetSemanticModel(tree).GetSymbolInfo(use).Symbol);

        Assert.Equal("Fixture.B.Thing", constructed.ContainingType.ToDisplayString());
        Assert.All(
            constructed.ContainingType.Locations,
            location => Assert.True(location.IsInSource));

        // The compiler's own name for the same defect, so a failure says which one it is.
        Assert.DoesNotContain(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == "CS0433");

        var indexer = Walk(fixture, Only(solution));

        Assert.Equal(0, indexer.Unresolved);
        Assert.Equal(2, indexer.References);
        Assert.Equal(0, indexer.External);
    }

    /// <summary>Index a loaded target into nothing, for the counters it keeps.</summary>
    private static Boxops.Fjord.Indexer.Indexer Walk(Fixture fixture, LoadedTarget target)
    {
        using var sink = new FactSink(DotnetIndex.Schema, [new SourceWalkTests.Recorder()]);

        // Fully qualified: from `Boxops.Fjord.Tests`, the bare name resolves to the
        // sibling *namespace* rather than the type in it.
        var indexer = new Boxops.Fjord.Indexer.Indexer(
            Over(fixture), sink, fixture.Root, target.Build);

        foreach (var project in target.Projects)
        {
            indexer.Index(project.Compile()!, project.Roslyn);
        }

        sink.Drain();

        return indexer;
    }

    /// <summary>
    /// <b>A file the walk indexes is a file some project in the set compiles.</b>
    /// </summary>
    /// <remarks>
    /// The consequence of the count above, stated where it is visible: C's source is not
    /// walked, because C is not built here. An index that quietly picked it up would be
    /// holding declarations from a project whose build nothing in the run checked.
    /// </remarks>
    [Fact]
    public void A_project_the_solution_does_not_list_is_not_walked()
    {
        using var fixture = Fixture.Copy("graph");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        var walked = Only(solution).Projects
            .SelectMany(project => project.Compile()!.SyntaxTrees)
            .Select(tree => tree.FilePath)
            .ToList();

        Assert.Contains(walked, path => path.EndsWith("B.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(walked, path => path.EndsWith("C.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A checkout that has been built still indexes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The regression guard for the defect that made this the normal case and the broken
    /// one. <c>CoreCompile</c> is incremental, so after an ordinary <c>dotnet build</c>
    /// MSBuild skips it — and the compiler command line it would have logged is the
    /// entire content of a design-time build. Every project came back
    /// succeeded-with-nothing, which the loader reported as a failed build, so indexing
    /// a repository anybody had built produced <i>nothing at all</i>.
    /// </para>
    /// <para>
    /// The fixture is built first on purpose: this test is green by accident on a fresh
    /// checkout, which is exactly how the defect survived.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_project_whose_outputs_are_up_to_date_is_still_built_for_its_command_line()
    {
        using var fixture = Fixture.Copy("graph");

        // A alone, which drags B in behind it: the two projects the solution lists are
        // then both up to date, which is the state this is about.
        fixture.Build("src/A/A.csproj");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        Assert.Equal(["A", "B"], Only(solution).Projects.Select(project => project.Name).Order());

        // And the compilations are real ones, not empty shells: an up-to-date project
        // whose source list came back empty would still count as a project.
        Assert.All(
            Only(solution).Projects,
            project => Assert.NotEmpty(project.Compile()!.SyntaxTrees));
    }

    /// <summary>
    /// <b>The build layer is every project under the source, not only the built ones.</b>
    /// </summary>
    /// <remarks>
    /// C is discovered by the glob and gets its facts from its XML; A and B get theirs
    /// from a design-time build. A repository that will not build still has a project
    /// graph, and this is what says so.
    /// </remarks>
    [Fact]
    public void The_build_layer_holds_the_project_the_workspace_does_not()
    {
        using var fixture = Fixture.Copy("graph");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        Assert.Equal(
            ["external/C/C.csproj", "src/A/A.csproj", "src/B/B.csproj"],
            Only(solution).Build.Projects.Select(project => project.Path).Order());
        Assert.Equal(2, Only(solution).Build.Built);
    }

    /// <summary>
    /// <b>A build that throws is asked again; the count says how often that happened.</b>
    /// </summary>
    /// <remarks>
    /// The transient half. MSBuild is asked out of process and several at once over one
    /// machine occasionally lose a pipe — a failure of the asking, which says nothing about
    /// the project and comes back different next time. The throw is injected because the
    /// real race needs several processes and a fixture small enough to run here cannot
    /// reach it: a repro-based gate would be green with the bug fully present.
    /// </remarks>
    [Fact]
    public void A_design_time_build_that_throws_is_asked_again_and_counted()
    {
        using var fixture = Fixture.Copy("graph");

        var calls = 0;

        var solution = Loader.Load(
            Over(fixture) with { Jobs = 1 },
            fixture.Root,
            TextWriter.Null,
            (analyzer, environment) => Interlocked.Increment(ref calls) == 1
                ? throw new IOException("the pipe went away")
                : analyzer.Build(environment));

        Assert.Equal(1, solution.Retried);
        Assert.Equal(["A", "B"], Only(solution).Projects.Select(project => project.Name).Order());
    }

    /// <summary>
    /// <b>A build that answers with nothing is asked twice, and never a third time.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The deterministic half, and the reason the distinction is worth drawing: a build
    /// that returns without error and without a compiler invocation has told the truth
    /// about this project, so asking again produces the same answer more slowly. Twice is
    /// the pair — <c>Compile</c> for a single-targeted project, <c>DispatchToInnerBuilds</c>
    /// for a multi-targeted one — and neither is a retry.
    /// </para>
    /// <para>
    /// Answering <see langword="null"/> is how a result with nothing usable in it reaches
    /// the loader: what it does with the answer is the subject, not how Buildalyzer spells
    /// an empty one.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_design_time_build_that_answers_with_nothing_is_asked_twice_and_not_retried()
    {
        using var fixture = Fixture.Copy("graph");

        var calls = 0;

        var refused = Assert.Throws<InvalidOperationException>(() => Loader.Load(
            Over(fixture) with { Jobs = 1 },
            fixture.Root,
            TextWriter.Null,
            (_, _) =>
            {
                Interlocked.Increment(ref calls);
                return null;
            }));

        // Two projects, the pair each, and nothing asked a third time.
        Assert.Equal(4, calls);
        Assert.Contains("every project failed to build", refused.Message);
    }

    /// <summary>
    /// <b>The reason a project was skipped is its own reason.</b>
    /// </summary>
    /// <remarks>
    /// One of the two attempts is always wrong about any given project by construction — a
    /// single-targeted project has no <c>DispatchToInnerBuilds</c> — so reporting the last
    /// error tells every reader the wrong thing about why their project was skipped, and
    /// sends them looking for a target rather than for the import that is missing.
    /// </remarks>
    [Fact]
    public void The_reason_a_project_was_skipped_is_its_own_and_not_the_wrong_attempts()
    {
        using var fixture = Fixture.Copy("broken");
        var log = new StringWriter();

        var solution = Loader.Load(Over(fixture, "Broken.slnx"), fixture.Root, log);

        Assert.Equal(["Good"], Only(solution).Projects.Select(project => project.Name));

        var said = log.ToString();
        Assert.Contains("Missing.props", said, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchToInnerBuilds", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A project that built belongs in the graph, wherever it was found.</b>
    /// </summary>
    /// <remarks>
    /// The build layer globs every <c>.csproj</c> under the source, and a solution is free
    /// to list one that is not under it — <c>app/App.slnx</c> naming <c>../lib/Lib.csproj</c>
    /// is an ordinary layout. Such a project used to be dropped by the one branch that
    /// could see it: refinement looked it up by path, did not find it, and returned. It had
    /// a design-time build, a framework, an assembly name and a source list, and none of it
    /// reached the index.
    /// </remarks>
    [Fact]
    public void A_project_the_glob_missed_and_the_build_found_is_in_the_layer()
    {
        using var fixture = Fixture.Copy("rescue");

        var solution = Loader.Load(
            new Options { Source = fixture.Path("app", "App.slnx"), Jobs = 2 },
            fixture.Root,
            TextWriter.Null);

        Assert.Equal(
            ["app/Main/Main.csproj", "lib/Lib.csproj"],
            Only(solution).Build.Projects.Select(project => project.Path).Order());

        // Refined, not merely present: the framework is the one MSBuild resolved rather
        // than one the XML happened to spell.
        var lib = Assert.Single(Only(solution).Build.Projects, project => project.Path == "lib/Lib.csproj");
        Assert.True(lib.Built);
        Assert.Equal(["net10.0"], lib.Frameworks);

        // And the edge is no longer dropped for want of a target to point at.
        var main = Assert.Single(Only(solution).Build.Projects, project => project.Path.EndsWith("Main.csproj", StringComparison.Ordinal));
        Assert.Contains("lib/Lib.csproj", main.ProjectRefs);
    }

    /// <summary>
    /// <b>The class invariant: a build with a usable compilation has a project fact.</b>
    /// </summary>
    /// <remarks>
    /// One count against one count, over both fixtures — the layout where every project is
    /// under the source and the one where a project is not. A fixture can be fixed by
    /// hand; this is the rule the fixture is an example of.
    /// </remarks>
    [Theory]
    [InlineData("graph", "Graph.slnx")]
    [InlineData("rescue", "app/App.slnx")]
    public void Every_project_that_built_has_a_project_fact(string name, string solutionFile)
    {
        using var fixture = Fixture.Copy(name);

        var solution = Loader.Load(
            new Options { Source = fixture.Path(solutionFile.Split('/')), Jobs = 2 },
            fixture.Root,
            TextWriter.Null);

        Assert.NotEmpty(Only(solution).Projects);
        Assert.Equal(Only(solution).Projects.Count, Only(solution).Build.Built);
    }
}
