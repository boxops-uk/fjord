using System.Xml.Linq;

using Boxops.Fjord.Client;

using Buildalyzer;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// One project: where it is, what it builds, and what it builds against.
/// </summary>
/// <remarks>
/// Held rather than recomputed because the <see cref="Fact"/> is nested into every
/// <c>msbuild.SourceFileToProject</c> edge, and a repository's larger projects have
/// thousands of files each.
/// </remarks>
internal sealed class ProjectInfo(string path)
{
    /// <summary>The project file, relative to the index root, with forward slashes.</summary>
    public string Path { get; } = path;

    private FjordFact? _fact;

    /// <summary>
    /// This project as an <c>msbuild.Project</c> fact — its file, with everything MSBuild
    /// evaluated on the value side.
    /// </summary>
    /// <remarks>
    /// <b>Built on first read, which must be after refinement.</b> The value side carries
    /// what a design-time build resolved, so a fact built before <c>Refine</c> would cache
    /// what the XML could only approximate. Nothing reads it during loading; the walk is
    /// the first caller.
    /// </remarks>
    public FjordFact Fact => _fact ??= DotnetIndex.ProjectFact(
        DotnetIndex.FileFact(Path),
        platformTarget: PlatformTarget,
        // One target framework, or none: a multi-targeting project resolves several and
        // `msbuild.ProjectCompilation` is what carries the crossing per framework.
        targetFramework: Frameworks.Count == 1 ? Frameworks[0] : null,
        sdk: Sdk,
        outputType: OutputType,
        assemblyName: Assembly,
        rootNamespace: RootNamespace);

    /// <summary>What MSBuild evaluated, where a design-time build answered.</summary>
    public string? PlatformTarget { get; set; }

    public string? Sdk { get; set; }

    public string? OutputType { get; set; }

    public string? RootNamespace { get; set; }

    /// <summary>
    /// The assembly this produces — MSBuild's own default until something says
    /// otherwise, which is the project file's base name.
    /// </summary>
    public string Assembly { get; set; } = System.IO.Path.GetFileNameWithoutExtension(path);

    /// <summary>
    /// The frameworks it targets, <b>as the project states them</b>.
    /// </summary>
    /// <remarks>
    /// A design-time build hands back a framework MSBuild has already resolved. Where
    /// only the project file could be read, a multi-targeting repository commonly names
    /// one by property — <c>$(NetCoreAppCurrent)</c> — and that is what is recorded,
    /// since the alternative is either inventing a value or dropping the project's only
    /// link to the assembly it produces. <see cref="Built"/> is which of the two a
    /// project got.
    /// </remarks>
    public List<string> Frameworks { get; } = [];

    /// <summary>Projects it references, as index-relative paths.</summary>
    public List<string> ProjectRefs { get; } = [];

    public List<(string Name, string Version)> Packages { get; } = [];

    /// <summary>Whether a design-time build answered for this project, rather than its XML.</summary>
    public bool Built { get; set; }
}

/// <summary>
/// The build layer: every project in the checkout, and which of them compiles a file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sources of knowledge, and the better one wins where it exists.</b> Every
/// <c>.csproj</c> under the source is read as XML — which always works, needs no SDK and
/// no restore, and is how a repository that will not build still gets a build layer.
/// A design-time build that succeeded then refines its own project: the resolved target
/// framework, the assembly name MSBuild actually computed, package versions after
/// central package management has had its say, and the <i>exact</i> source list rather
/// than a guess.
/// </para>
/// <para>
/// <b>The guess, where there is no build, is containment</b>: a file belongs to the
/// nearest project at or above it. That is right for the ordinary layout and wrong for
/// shared source — <c>src/libraries/Common</c> in dotnet/runtime is compiled into a
/// hundred assemblies by explicit <c>&lt;Compile Include&gt;</c> and lives under no
/// project at all. Such a file gets no <c>msbuild.SourceFileToProject</c> edge rather
/// than a plausible one: an index that quietly attributes shared code to whichever project
/// happens to sit above it answers "what builds this" wrongly, and nothing downstream
/// can tell.
/// </para>
/// </remarks>
internal sealed class ProjectIndex
{
    private readonly Dictionary<string, ProjectInfo> _byPath = new(StringComparer.Ordinal);

    /// <summary>Exact membership, from the design-time builds that succeeded.</summary>
    private readonly Dictionary<string, List<ProjectInfo>> _byFile = new(StringComparer.Ordinal);

    /// <summary>
    /// The projects living in each directory, for the containment fallback.
    /// </summary>
    /// <remarks>
    /// A list per directory rather than one project, because a directory holding two
    /// project files is ordinary — a reference assembly beside its implementation — and
    /// picking one of them by name order would be a coin toss recorded as a fact.
    /// </remarks>
    private readonly Dictionary<string, List<ProjectInfo>> _byDirectory = new(StringComparer.Ordinal);

    /// <summary>The solutions this index was built from, index-relative, and what each lists.</summary>
    /// <remarks>
    /// <b>Several, because a caller may name several.</b> Membership is per solution — two
    /// solutions sharing a project both get an edge to it, which is what "which solution is
    /// this project in" has to answer when the honest answer is "both".
    /// </remarks>
    private readonly List<(string Path, List<string> Listed)> _solutions = [];

    /// <summary>The listed projects an edge can point at, index-relative.</summary>

    private readonly List<string> _unlinked = [];

    public IReadOnlyCollection<ProjectInfo> Projects => _byPath.Values;

    /// <summary>How many projects a design-time build, rather than XML, answered for.</summary>
    public int Built => _byPath.Values.Count(project => project.Built);

    /// <summary>
    /// Projects the solution lists that this index holds no <c>msbuild.Project</c> for, by
    /// file name — the edges that were not written.
    /// </summary>
    /// <remarks>
    /// <b>An omission a run has to say out loud.</b> A solution's membership is a claim
    /// about the repository, and a project it lists that has no project fact loses both of
    /// its edges — so a database can be missing a third of a solution and look complete.
    /// The only such project a normal layout produces is one whose path climbs out of
    /// <c>--root</c>: it comes back as <c>../../elsewhere</c>, which is not a name two runs
    /// would agree on, so it gets no <c>src.File</c> and there is nothing for an edge to
    /// point at. Same rule as shared source and as a dropped declaration kind: no edge
    /// rather than a plausible one, named and counted rather than dropped.
    /// </remarks>
    public IReadOnlyList<string> Unlinked => _unlinked;

    /// <summary>
    /// Read every project under <paramref name="source"/>, then let the builds that
    /// succeeded overwrite what the XML could only approximate.
    /// </summary>
    public static ProjectIndex Build(
        string root,
        string source,
        IReadOnlyList<IAnalyzerResult> results,
        IReadOnlyList<ResolvedSolution> solutions,
        TextWriter log)
    {
        var index = new ProjectIndex();
        var directory = Directory.Exists(source) ? source : System.IO.Path.GetDirectoryName(source)!;

        foreach (var file in Discover(directory))
        {
            if (Paths.Relative(root, file) is not { } path)
            {
                continue;
            }

            var project = new ProjectInfo(path);
            index.Read(root, file, project);
            index._byPath[path] = project;
        }

        foreach (var result in results)
        {
            index.Refine(root, result);
        }

        foreach (var project in index._byPath.Values)
        {
            var holding = Parent(project.Path);

            if (!index._byDirectory.TryGetValue(holding, out var here))
            {
                here = [];
                index._byDirectory[holding] = here;
            }

            here.Add(project);
        }

        log.WriteLine($"  build layer: {index._byPath.Count} project(s), "
            + $"{index.Built} from a design-time build, {index._byFile.Count} file(s) attributed exactly");

        // Last, because it needs every project fact this layer will have: a project the
        // glob missed and a build rescued is one an edge can point at, and asking before
        // `Refine` would have counted it as lost.
        foreach (var solution in solutions)
        {
            index.Link(root, solution, log);
        }

        return index;
    }

    /// <summary>
    /// The solution this index was built from, and which of the projects it lists an edge
    /// can name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the solution says, not what is on disk.</b> The membership is read from the
    /// solution's own project list, so a <c>.csproj</c> the glob found beside it and the
    /// solution does not name gets no edge — which is the difference between "the projects
    /// of this solution" and "the projects in this directory", and the two are different
    /// answers in any repository with a second solution in it.
    /// </para>
    /// <para>
    /// <b>A listed project with no <c>msbuild.Project</c> fact gets no edge.</b> Both edges
    /// are references to a <c>Project</c>, and a reference to a fact that does not exist is
    /// not a fact — so the edge is left out, and <see cref="Unlinked"/> is what stops that
    /// being silent.
    /// </para>
    /// </remarks>
    private void Link(string root, ResolvedSolution solution, TextWriter log)
    {
        if (Paths.Relative(root, solution.File) is not { } path)
        {
            // The solution is outside `--root`, so it has no path this index can name it
            // by and `msbuild.Solution`'s key cannot be built — the same rule that skips a
            // project whose path climbs out. Written down rather than skipped, because a
            // whole solution silently absent is what a consumer would read as "this
            // checkout has no solution".
            log.WriteLine($"  ! {System.IO.Path.GetFileName(solution.File)}: outside the "
                + "index root, so there is no `src.File` to key a solution on and no "
                + "solution facts are written");
            return;
        }

        var listed = new List<string>();

        foreach (var project in solution.Projects)
        {
            if (Paths.Relative(root, project) is { } member && _byPath.ContainsKey(member))
            {
                listed.Add(member);
                continue;
            }

            var name = System.IO.Path.GetFileName(project);
            _unlinked.Add(name);

            log.WriteLine($"  ! {name}: listed by {path} and has no `msbuild.Project` fact "
                + "in this index, so neither solution edge names it");
        }

        _solutions.Add((path, listed));

        log.WriteLine($"  solution {path}: {listed.Count} of {solution.Projects.Count} "
            + "listed project(s) have a project fact to be an edge to");
    }

    /// <summary>The projects that compile <paramref name="file"/>, which may be none.</summary>
    /// <remarks>
    /// An exact answer is the whole answer: a file MSBuild listed for one project is not
    /// silently also attributed to whatever project sits above it on disk.
    /// </remarks>
    public IReadOnlyList<ProjectInfo> Owners(string file)
    {
        if (_byFile.TryGetValue(file, out var exact))
        {
            return exact;
        }

        // Up the path rather than across the projects: a walk asks this for every file
        // it reaches, and a repository has far more projects than a path has segments.
        for (var directory = Parent(file); ; directory = Parent(directory))
        {
            if (_byDirectory.TryGetValue(directory, out var here))
            {
                // **One project in the directory is an answer; several is not.**
                // dotnet/runtime's `src/tests/JIT/CodeGenBringUpTests` holds 645
                // project files beside its sources, one per test — containment says
                // "one of these 645" and a fact saying all of them is 644 edges that
                // are not true. Same rule as shared source, one directory lower: no
                // edge rather than a plausible one, and the run counts it.
                return here.Count == 1 ? here : [];
            }

            if (directory.Length == 0)
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Write the build layer itself: the solution where the run resolved one, the
    /// projects, the assemblies, the compilations that pair them, and the two dependency
    /// graphs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Emitted once, up front, rather than as files are reached — this is what the
    /// repository is, not what the walk found, and a run stopped early by
    /// <c>--max-files</c> should still say so.
    /// </para>
    /// <para>
    /// <b>An emitter rather than a sink.</b> The build layer's job is to know what MSBuild
    /// resolved; where those facts go is somebody else's, and a class that takes the write
    /// path as an argument cannot accidentally start depending on how it batches. Same
    /// shape <c>CsharpEntities</c> takes, for the same reason.
    /// </para>
    /// </remarks>
    public void Emit(Action<uint, FjordFact> emit)
    {
        EmitSolution(emit);

        foreach (var project in _byPath.Values)
        {
            emit(DotnetIndex.Project, project.Fact);

            var assembly = DotnetIndex.AssemblyFact(project.Assembly);
            emit(DotnetIndex.Assembly, assembly);

            // A project that names no framework still compiles into an assembly, and the
            // compilation is the only fact that says which — so it gets one, with the
            // empty string where the framework would be. Nobody can mistake that for a
            // target framework, which is the whole requirement.
            foreach (var framework in project.Frameworks.Count > 0 ? project.Frameworks : [""])
            {
                emit(
                    DotnetIndex.Compilation,
                    DotnetIndex.CompilationFact(assembly, framework, project.Fact));

                // The same crossing from the project's side, for the panel that opens on
                // a project. A second predicate rather than a sort, because a predicate
                // leads with one field.
                emit(
                    DotnetIndex.ProjectCompilation,
                    DotnetIndex.ProjectCompilationFact(project.Fact, framework, assembly));
            }

            foreach (var path in project.ProjectRefs)
            {
                // A reference out of the indexed tree — a project in a sibling
                // repository, or one `--source` did not reach. The target has no facts
                // here, and an edge to a project nothing else mentions is an edge to
                // nothing.
                if (!_byPath.TryGetValue(path, out var target))
                {
                    continue;
                }

                // **The edge between two projects, in both directions.** The old build
                // layer had neither: it carried a reference to a project keyed on a path
                // string, and no reverse at all — so "who depends on this" was a scan.
                emit(
                    DotnetIndex.ProjectReference,
                    DotnetIndex.ProjectReferenceFact(project.Fact, target.Fact));
                emit(
                    DotnetIndex.ProjectReferencedBy,
                    DotnetIndex.ProjectReferencedByFact(target.Fact, project.Fact));
            }

            foreach (var (name, version) in project.Packages)
            {
                var package = DotnetIndex.PackageFact(name, version);
                emit(
                    DotnetIndex.Package, package);

                // **`range` is what the file said and `Package.version` is the identity.**
                // This producer has one number for both: the declared version, after
                // central package management has had its say. The resolved version needs
                // the assets file, which is a restore this walk does not read — so the
                // range is the same string, and improving `Package.version` later will not
                // disturb it.
                emit(
                    DotnetIndex.PackageReference,
                    DotnetIndex.PackageReferenceFact(project.Fact, package, version));
                emit(
                    DotnetIndex.PackageDependent,
                    DotnetIndex.PackageDependentFact(package, project.Fact));
            }
        }
    }

    /// <summary>
    /// The solution this index was built from, and both edges to each project it lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing at all for a run that resolved no solution.</b> MSBuild's containment is
    /// one-way — a solution lists its projects and a project names no solution — so a run
    /// handed a <c>.csproj</c> has nothing to resolve, and searching the disk for a
    /// solution that happens to list it would put a claim in the database that the build
    /// system does not make. The predicate therefore means <i>the solution this index was
    /// built from</i>, which is a reading a consumer can use, and it is empty for a
    /// project-only run rather than approximate.
    /// </para>
    /// <para>
    /// <b>The solution file is interned as a path and gets no source-layer facts</b> — no
    /// <c>src.FileLanguage</c>, no <c>src.FileDigest</c>, no <c>src.FileInfo</c> and no
    /// line table — which is exactly how the <c>.csproj</c> in <c>msbuild.Project</c>'s own
    /// key is interned. The source layer describes files the run <i>read as source</i>:
    /// every offset in it is an offset into a file some compilation parsed, and nothing in
    /// this database holds a position in a solution file. Writing a
    /// <c>src.FileLanguage</c> of <c>xml</c> for it would also contradict
    /// <c>config.Setting {dimension = "language"}</c>, which says what the semantic layers
    /// cover; <c>--no-lines</c> and <c>--styles</c> are switches over that same source
    /// table, and Roslyn's classifier has no <c>Document</c> for a file no compilation
    /// contains. What makes the fact readable is the pair of edges below: through them the
    /// <c>file</c> field joins to exactly what a project's does.
    /// </para>
    /// <para>
    /// <b>Both directions, because <c>msbuild.sigla</c> stores both</b> — neither is
    /// derivable in a seek from the other, so writing one would leave "which solution is
    /// this project in" a read of every solution in the repository.
    /// </para>
    /// </remarks>
    private void EmitSolution(Action<uint, FjordFact> emit)
    {
        foreach (var (path, listed) in _solutions)
        {
            var solution = DotnetIndex.SolutionFact(DotnetIndex.FileFact(path));
            emit(DotnetIndex.Solution, solution);

            foreach (var member in listed)
            {
                // Only the paths `Link` found a project for are here, so the lookup cannot
                // fail — the ones it could not are in `Unlinked` and are reported.
                var project = _byPath[member].Fact;

                emit(DotnetIndex.SolutionToProject, DotnetIndex.SolutionToProjectFact(solution, project));
                emit(DotnetIndex.ProjectToSolution, DotnetIndex.ProjectToSolutionFact(project, solution));
            }
        }
    }

    /// <summary>What a design-time build knows and the project file cannot say.</summary>
    /// <remarks>
    /// <b>A project that built belongs here, wherever the glob looked.</b> The discovery
    /// pass reads every <c>.csproj</c> under the source, and a solution is free to name one
    /// that is not under it — a solution in <c>app/</c> listing <c>../lib/Lib.csproj</c> is
    /// an ordinary layout. Such a project used to be dropped by this one branch: looked up
    /// by path, not found, and returned from. It had a framework, an assembly name and an
    /// exact source list, and none of it reached the index — nor did the reference edge
    /// pointing at it, since an edge with no target is not written.
    /// <para>
    /// <b>The early return that stays</b> is for a path that does not resolve under the
    /// root. That one has no name two runs would agree on — it would come back as
    /// <c>../../elsewhere</c>, which depends on where the root happens to be — so there is
    /// nothing to call it and nothing to key it by.
    /// </para>
    /// </remarks>
    private void Refine(string root, IAnalyzerResult result)
    {
        if (Paths.Relative(root, result.ProjectFilePath) is not { } path)
        {
            return;
        }

        if (!_byPath.TryGetValue(path, out var project))
        {
            project = new ProjectInfo(path);

            // Read its file too, spelled exactly as `Discover` would have: a rescued
            // project is a discovered one that was looked for in the wrong place, and it
            // should carry the same facts for the same reasons.
            Read(root, System.IO.Path.GetFullPath(result.ProjectFilePath), project);
            _byPath[path] = project;
        }

        project.Built = true;

        if (result.TargetFramework is { Length: > 0 } framework)
        {
            // The resolved framework replaces whatever the XML said, rather than joining
            // it: `net10.0` and `$(NetCoreAppCurrent)` are the same target framework
            // said twice, and two compilation facts would claim otherwise.
            project.Frameworks.Clear();
            project.Frameworks.Add(framework);
        }

        if (Property(result, "AssemblyName") is { Length: > 0 } assembly)
        {
            project.Assembly = assembly;
        }

        // The rest of `msbuild.Project`'s value side. Each stays null where MSBuild left
        // it unset, which is a `nothing` rather than an empty string — the schema's six
        // optionals exist because "unset" and "empty" are different answers.
        project.PlatformTarget = Property(result, "PlatformTarget");
        project.Sdk = Property(result, "UsingMicrosoftNETSdk") == "true"
            ? Property(result, "MSBuildProjectSdk") ?? "Microsoft.NET.Sdk"
            : Property(result, "MSBuildProjectSdk");
        project.OutputType = Property(result, "OutputType");
        project.RootNamespace = Property(result, "RootNamespace");

        if (result.PackageReferences is { Count: > 0 } packages)
        {
            project.Packages.Clear();

            foreach (var (name, metadata) in packages)
            {
                project.Packages.Add((name, Version(metadata)));
            }
        }

        foreach (var reference in result.ProjectReferences)
        {
            if (Paths.Relative(root, reference) is { } target && !project.ProjectRefs.Contains(target))
            {
                project.ProjectRefs.Add(target);
            }
        }

        // **The exact source list**, which is the one thing containment cannot give:
        // shared files, generated files, and files a glob excluded all differ from what
        // sits under the project's directory.
        foreach (var source in result.SourceFiles)
        {
            if (Paths.Relative(root, source) is not { } file || Paths.IsBuildOutput(file))
            {
                continue;
            }

            if (!_byFile.TryGetValue(file, out var owners))
            {
                owners = [];
                _byFile[file] = owners;
            }

            if (!owners.Contains(project))
            {
                owners.Add(project);
            }
        }
    }

    /// <summary>
    /// What the project file itself says: its assembly name, its frameworks, and both
    /// kinds of reference.
    /// </summary>
    /// <remarks>
    /// XML rather than MSBuild evaluation, deliberately. Evaluating means an SDK, a
    /// restore and an out-of-process build per project — which is exactly what the
    /// design-time path already does and exactly what is unavailable when it fails. A
    /// property this cannot expand is recorded unexpanded rather than guessed at.
    /// </remarks>
    private void Read(string root, string file, ProjectInfo project)
    {
        XDocument document;

        try
        {
            document = XDocument.Load(file);
        }
        catch (Exception failure) when (failure is IOException or System.Xml.XmlException)
        {
            // A project this cannot read is still a project, and its path is still true.
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(file)!;

        foreach (var element in document.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "AssemblyName" when Literal(element.Value):
                    project.Assembly = element.Value.Trim();
                    break;

                case "TargetFramework" or "TargetFrameworks":
                    foreach (var framework in element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries))
                    {
                        if (!project.Frameworks.Contains(framework))
                        {
                            project.Frameworks.Add(framework);
                        }
                    }
                    break;

                case "ProjectReference" when element.Attribute("Include")?.Value is { Length: > 0 } include:
                {
                    var target = System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(directory, include.Replace('\\', '/')));

                    if (Paths.Relative(root, target) is { } path && !project.ProjectRefs.Contains(path))
                    {
                        project.ProjectRefs.Add(path);
                    }
                    break;
                }

                case "PackageReference" when element.Attribute("Include")?.Value is { Length: > 0 } name:
                {
                    // The version lives in an attribute or a child element, and under
                    // central package management in neither — the empty string is what
                    // "the project file does not say" looks like, and nobody can mistake
                    // it for a version.
                    var version = element.Attribute("Version")?.Value
                        ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value
                        ?? string.Empty;

                    project.Packages.Add((name.Trim(), Literal(version) ? version.Trim() : string.Empty));
                    break;
                }
            }
        }
    }

    /// <summary>Every project file under a directory, ignoring build output.</summary>
    private static IEnumerable<string> Discover(string directory)
    {
        IEnumerable<string> found;

        try
        {
            found = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return [];
        }

        return found
            .Where(path => !path.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    /// <summary>An MSBuild property from the build, or nothing if it did not report one.</summary>
    private static string? Property(IAnalyzerResult result, string name) =>
        result.Properties is { } properties && properties.TryGetValue(name, out var value) ? value : null;

    private static string Version(IReadOnlyDictionary<string, string> metadata) =>
        metadata.TryGetValue("Version", out var version) && Literal(version)
            ? version.Trim()
            : string.Empty;

    /// <summary>Whether a project file said a value outright, rather than by property.</summary>
    private static bool Literal(string? text) =>
        text is { Length: > 0 } && !text.Contains("$(", StringComparison.Ordinal);

    /// <summary>The directory part of an index-relative path, without its trailing slash.</summary>
    private static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }
}

/// <summary>Index-relative paths, spelled one way in one place.</summary>
internal static class Paths
{
    /// <summary>
    /// <paramref name="absolute"/> as the index names it, or nothing if it is outside
    /// the root.
    /// </summary>
    /// <remarks>
    /// A path outside the root would come back as <c>../../elsewhere</c>, which is not a
    /// name — it depends on where the root happens to be, so two runs of the same
    /// repository would disagree about it.
    /// </remarks>
    public static string? Relative(string root, string? absolute)
    {
        if (string.IsNullOrEmpty(absolute))
        {
            return null;
        }

        var relative = System.IO.Path.GetRelativePath(root, absolute)
            .Replace(System.IO.Path.DirectorySeparatorChar, '/');

        return relative.StartsWith("../", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative)
            ? null
            : relative;
    }

    /// <summary>
    /// Build output rather than source.
    /// </summary>
    /// <remarks>
    /// <c>obj/</c> in particular holds the generated assembly attributes every project
    /// has, which would be the same six declarations in every project and none of them
    /// anything anyone wants to find.
    /// </remarks>
    public static bool IsBuildOutput(string relative) =>
        relative.Contains("/obj/", StringComparison.Ordinal)
        || relative.Contains("/bin/", StringComparison.Ordinal)
        || relative.StartsWith("obj/", StringComparison.Ordinal)
        || relative.StartsWith("bin/", StringComparison.Ordinal);
}
