using System.Linq;

using Boxops.Fjord.Indexer;

using Microsoft.CodeAnalysis;

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
    private static Options Over(Fixture fixture) => new()
    {
        Source = fixture.Path("Graph.slnx"),
        Jobs = 2,
    };

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

        Assert.Equal(["A", "B"], solution.Projects.Select(project => project.Name).Order());
    }

    /// <summary>
    /// <b>Inside the set resolves to source; outside it resolves to an assembly.</b>
    /// </summary>
    /// <remarks>
    /// The trade the loader makes. A reference from A to B lands on a symbol with a
    /// location in <c>B.cs</c>, because both are in the workspace and the graph is wired
    /// by project id. A reference to C — which the solution does not list — lands on a
    /// symbol from <c>Fixture.C.dll</c>, with no source location: the reference still
    /// resolves, and the index says truthfully that it points outside itself.
    /// </remarks>
    [Fact]
    public void A_reference_inside_the_set_is_source_and_one_outside_it_is_metadata()
    {
        using var fixture = Fixture.Copy("graph");

        // C is compiled for real: a reference to a project outside the indexed set
        // degrades to its assembly, and there is no assembly until something builds one.
        fixture.Build("external/C/C.csproj");

        var solution = Loader.Load(Over(fixture), fixture.Root, TextWriter.Null);

        var b = Assert.Single(solution.Projects, project => project.Name == "B");
        var compilation = b.Compile()!;

        var thing = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName("Fixture.B.Thing"));
        Assert.All(thing.Locations, location => Assert.True(location.IsInSource));

        var deep = Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName("Fixture.C.Deep"));
        Assert.Equal("Fixture.C", deep.ContainingAssembly.Name);
        Assert.DoesNotContain(deep.Locations, location => location.IsInSource);
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

        var walked = solution.Projects
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

        Assert.Equal(["A", "B"], solution.Projects.Select(project => project.Name).Order());

        // And the compilations are real ones, not empty shells: an up-to-date project
        // whose source list came back empty would still count as a project.
        Assert.All(
            solution.Projects,
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
            solution.Build.Projects.Select(project => project.Path).Order());
        Assert.Equal(2, solution.Build.Built);
    }
}
