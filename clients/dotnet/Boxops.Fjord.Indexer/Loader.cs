using Buildalyzer;
using Buildalyzer.Environment;
using Buildalyzer.IO;
using Buildalyzer.Workspaces;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// A project, and the compilation of it — on demand.
/// </summary>
/// <remarks>
/// A function rather than a compilation, because asking for one materialises every
/// symbol table in it. Over four hundred projects that is the difference between a
/// machine indexing and a machine swapping; asked for one at a time, each is reachable
/// only while it is being walked.
/// </remarks>
/// <param name="Roslyn">
/// The workspace's project, or <c>null</c> where a compilation was built without one.
/// Carried only so <c>--styles</c> can reach a <see cref="Document"/>:
/// <see cref="Microsoft.CodeAnalysis.Classification.Classifier"/>'s semantic-model
/// overload is obsolete, and its supported form takes a document.
/// </param>
internal sealed record LoadedProject(string Name, Func<Compilation?> Compile, Project? Roslyn = null);

/// <summary>What there is to walk, and what compiled it.</summary>
/// <remarks>
/// The two travel together because they are answered by the same pass: the design-time
/// build that produces a compilation is also the only thing that knows the project's
/// resolved framework, its assembly name and its real source list.
/// </remarks>
/// <summary>One target framework's worth of a checkout: what to walk, and what compiled it.</summary>
/// <remarks>
/// <b>One of these becomes one database.</b> A project compiled for two frameworks is two
/// compilations with different preprocessor symbols, different references and, often,
/// different members — so the facts belong to the target rather than to the project, and
/// there is no key in the schema that could hold both. Fanning out is the only shape that
/// does not quietly index one of the two and call it the project.
/// </remarks>
internal sealed record LoadedTarget(
    string Framework,
    IReadOnlyList<LoadedProject> Projects,
    ProjectIndex Build);

internal sealed record LoadedSolution(
    IReadOnlyList<LoadedTarget> Targets,
    int Retried,
    IReadOnlyList<string> Skipped);

/// <summary>
/// One design-time build, as the thing that can be swapped for a test.
/// </summary>
/// <remarks>
/// <b>A seam rather than a race.</b> The failure this exists for needs several MSBuild
/// processes racing over one pipe, and a fixture small enough to run in a test cannot
/// reach it — so a repro-based gate would be green with the bug fully present. Injected
/// here, "a throw is retried and a clean empty result is not" is a statement about the
/// loader, checked in milliseconds.
/// </remarks>
internal delegate IAnalyzerResults? DesignTimeBuild(
    IProjectAnalyzer analyzer,
    EnvironmentOptions environment);

/// <summary>
/// Turning a checkout into compilations, which is the half of an indexer that is not
/// about facts at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>MSBuild is asked, not guessed at.</b> Buildalyzer runs a design-time build of
/// each project in a separate <c>dotnet msbuild</c> process — the same evaluation an
/// IDE does, with compilation skipped — and hands back what the compiler would have
/// been given: the source list after globs and conditions, the reference assemblies
/// NuGet resolved, the preprocessor symbols, the language version. Reconstructing any
/// of that from the project XML is how an indexer ends up quietly indexing a different
/// program from the one that builds.
/// </para>
/// <para>
/// Those results become a Roslyn <see cref="AdhocWorkspace"/>, and project references
/// inside it are <i>project</i> references rather than paths to built assemblies — so a
/// symbol used in one project and declared in another still has a source location, and
/// the reference resolves to a declaration this index holds.
/// </para>
/// <para>
/// <b>The fallback is deliberate.</b> A real repository has projects that will not
/// restore on this machine — a Windows-only target, a pinned SDK, a missing feed. One
/// project failing must not cost the other four hundred, so a failure is reported and
/// skipped. If <i>every</i> project fails there is nothing to index and the run says so:
/// a producer that cannot resolve types has nothing to write but degraded facts, and
/// writing those is the one thing this indexer refuses to do.
/// </para>
/// </remarks>
internal static class Loader
{
    /// <summary>How many times the pair of attempts is tried before a project is skipped.</summary>
    /// <remarks>
    /// Three, and the number is the point of the run: two would make one unlucky project
    /// a coin toss, and more would spend minutes on a repository that is simply broken.
    /// </remarks>
    private const int Attempts = 3;

    public static LoadedSolution Load(
        Options options,
        string root,
        TextWriter log,
        DesignTimeBuild? design = null)
    {
        var build = design ?? ((analyzer, environment) => analyzer.Build(environment));
        var retried = 0;

        var entry = ResolveEntryPoint(options.Source);
        log.WriteLine($"  entry point {entry}");

        var workspace = new AdhocWorkspace();
        var analyzers = Analyzers(entry, options, log);

        if (options.MaxProjects > 0 && analyzers.Count > options.MaxProjects)
        {
            log.WriteLine($"  stopping at {options.MaxProjects} projects (--max-projects)");
            analyzers = analyzers.Take(options.MaxProjects).ToList();
        }

        // Each design-time build is its own `dotnet msbuild` process, so several at once
        // is several processes and not several threads in this one. That is what makes
        // it worth doing: a few hundred projects at three seconds each is the difference
        // between a coffee and a lunch, and the results are independent.
        var results = new IReadOnlyList<IAnalyzerResult>[analyzers.Count];

        Parallel.For(0, analyzers.Count, new ParallelOptions { MaxDegreeOfParallelism = options.Jobs }, index =>
        {
            results[index] = BuildOne(analyzers[index], options, log, build, ref retried);
        });

        // Flattened in the order the solution lists them, so two runs over one checkout
        // produce the same index.
        var every = results.SelectMany(one => one).ToList();

        if (every.Count == 0)
        {
            // **A run that cannot resolve fails, and must not fall back to a syntax walk.**
            // Globbing the `.cs` files and parsing them against the running framework's
            // reference set finds every declaration and loses every reference into a NuGet
            // package — the type is an error type, so the member on it binds to nothing —
            // and the result is an index that looks complete while missing most of its
            // edges, with nothing in it to say so.
            //
            // The rule this protects: a producer that cannot resolve emits nothing rather
            // than a degraded fact.
            throw new InvalidOperationException(
                analyzers.Count == 0
                    ? "no projects were found under --source, so there is nothing to "
                        + "resolve against and nothing to index"
                    : $"every project failed to build ({analyzers.Count} of them), so no "
                        + "type in this checkout can be resolved. Fix the build — a "
                        + "restore, an SDK, a missing reference — and run again; this "
                        + "indexer writes no facts it cannot resolve");
        }

        // **Sorted and deduped here, because nothing else does it.** The server's name
        // check accepts `#` and says nothing about what follows it, so two runs that
        // disagreed about the order would be two sets of databases.
        var present = every
            .Select(result => result.TargetFramework!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(Rank)
            .ThenBy(framework => framework, StringComparer.Ordinal)
            .ToList();

        var wanted = options.Framework is { Length: > 0 } asked
            ? present.Where(framework =>
                string.Equals(framework, asked, StringComparison.OrdinalIgnoreCase)).ToList()
            : present;

        if (wanted.Count == 0)
        {
            throw new InvalidOperationException(
                $"no project in this checkout compiles as {options.Framework}. "
                + $"It builds for: {string.Join(", ", present)}");
        }

        // **Named, because a project silently absent from an index is the failure mode
        // this run exists to remove.** A project that compiles for none of the frameworks
        // being indexed is not broken and not indexed, and only the run can say so.
        var skipped = every
            .Where(result => !wanted.Contains(result.TargetFramework!, StringComparer.Ordinal))
            .Select(result => Path.GetFileName(result.ProjectFilePath))
            .Concat(analyzers
                .Where(analyzer => !every.Any(result => string.Equals(
                    Full(result.ProjectFilePath), Full(analyzer.ProjectFile.Path.ToString()), StringComparison.Ordinal)))
                .Select(analyzer => Path.GetFileName(analyzer.ProjectFile.Path.ToString())))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !every
                .Where(result => wanted.Contains(result.TargetFramework!, StringComparer.Ordinal))
                .Any(result => string.Equals(Path.GetFileName(result.ProjectFilePath), name, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        foreach (var name in skipped)
        {
            log.WriteLine($"  ! {name}: compiles for none of {string.Join(", ", wanted)}, skipping it");
        }

        var targets = new List<LoadedTarget>();

        foreach (var framework in wanted)
        {
            targets.Add(Target(
                framework,
                [.. every.Where(result => string.Equals(
                    result.TargetFramework, framework, StringComparison.Ordinal))],
                options,
                root,
                log));
        }

        return new LoadedSolution(targets, retried, skipped);
    }

    /// <summary>One framework's workspace and build layer, from that framework's results.</summary>
    /// <remarks>
    /// A workspace each, because a project compiled for two frameworks is two compilations
    /// and Roslyn holds one per project. A build layer each for the same reason: the
    /// <c>msbuild.Compilation</c> facts name this framework, so a database built for
    /// <c>net8.0</c> says <c>net8.0</c> everywhere rather than saying both and leaving a
    /// consumer to guess which half it is holding.
    /// </remarks>
    private static LoadedTarget Target(
        string framework,
        IReadOnlyList<IAnalyzerResult> results,
        Options options,
        string root,
        TextWriter log)
    {
        var workspace = new AdhocWorkspace();
        var added = new List<(IAnalyzerResult Result, ProjectId Id)>();
        var failed = 0;

        foreach (var result in results)
        {
            try
            {
                // **`addProjectReferences: false`, and the graph wired below instead.**
                // The `true` form walks to every project this one names and *builds* the
                // ones nobody asked for — so a solution listing two projects loaded three,
                // and one design-time build happened inside what is supposed to be pure
                // bookkeeping. It also left the compilation holding the referenced project
                // twice, once as a project and once as its assembly, which makes every type
                // in it ambiguous.
                //
                // Nothing is lost by refusing: a project this one references and the run
                // did not build is a project outside the indexed set, and its assembly is
                // already on the compiler's reference list.
                added.Add((result, result.AddToWorkspace(workspace, addProjectReferences: false).Id));
            }
            catch (InvalidOperationException refused)
            {
                // One project the workspace will not take is not worth the other four
                // hundred.
                log.WriteLine($"  ! {Path.GetFileName(result.ProjectFilePath)}: "
                    + $"the workspace refused it — {refused.Message}");
                failed++;
            }
        }

        if (failed > 0)
        {
            log.WriteLine($"  {failed} project(s) refused for {framework}, {added.Count} added");
        }

        Wire(workspace, added, log);

        // Ordered by path, not by whatever order the workspace hands them back: with
        // `--max-files` the order decides *which* files get indexed, and a run that
        // indexes a different two thousand each time is not a measurement.
        var walking = workspace.CurrentSolution.Projects
            .Where(project => project.Language == LanguageNames.CSharp)
            .OrderBy(project => project.FilePath ?? project.Name, StringComparer.Ordinal)
            .Select(project => new LoadedProject(
                project.Name,
                () => project.GetCompilationAsync().GetAwaiter().GetResult(),
                project))
            .ToList();

        // The build layer is built from *every* project file under the source, not only
        // the ones that built: a project MSBuild refused is still a project, its
        // references are still in its XML, and the files under it still have somewhere
        // to belong. The results that did succeed then overwrite what they know better.
        var layer = ProjectIndex.Build(root, options.Source, results, log);

        return new LoadedTarget(framework, walking, layer);
    }

    /// <summary>
    /// The reference graph, wired between the projects that were built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Keyed on the project file <i>and</i> its framework, not on the file alone.</b> A
    /// multi-targeting project is several results at one path, and a reference from
    /// something built for <c>net10.0</c> means that project's <c>net10.0</c> target.
    /// Keyed on the path, the second sighting would look like a duplicate to be skipped —
    /// which is also the thing a fan-out over frameworks consumes, so it would be
    /// discarding the run after next's input.
    /// </para>
    /// <para>
    /// <b>Adding the edge is half of it; removing the assembly is the other half.</b> The
    /// compiler's reference list names the *output* of every project this one references,
    /// so a project reference added beside it puts the same types in the compilation
    /// twice — from source and from a dll — and every one of them becomes ambiguous.
    /// Roslyn answers <c>null</c> for such a type rather than choosing, and a walk asking
    /// what a name means gets nothing back.
    /// </para>
    /// <para>
    /// A reference to a project the run did not build is left exactly as MSBuild resolved
    /// it: an assembly on the reference list, with no source behind it. That is the honest
    /// answer — the project is outside the indexed set — and it is what the run reports as
    /// a reference to a declaration outside the index.
    /// </para>
    /// </remarks>
    private static void Wire(
        AdhocWorkspace workspace,
        IReadOnlyList<(IAnalyzerResult Result, ProjectId Id)> added,
        TextWriter log)
    {
        var targets = new Dictionary<(string Path, string Framework), (ProjectId Id, string? Output)>();

        foreach (var (result, id) in added)
        {
            targets[(Full(result.ProjectFilePath), result.TargetFramework ?? string.Empty)] =
                (id, result.GetProperty("TargetPath"));
        }

        foreach (var (result, id) in added)
        {
            foreach (var reference in result.ProjectReferences)
            {
                if (Target(targets, Full(reference), result.TargetFramework) is not { } target)
                {
                    continue;
                }

                var solution = workspace.CurrentSolution;
                var project = solution.GetProject(id);

                if (project is null)
                {
                    continue;
                }

                if (!project.ProjectReferences.Any(held => held.ProjectId == target.Id))
                {
                    solution = solution.AddProjectReference(id, new ProjectReference(target.Id));
                }

                // The metadata reference this replaces, matched on the path MSBuild said
                // the referenced project writes.
                if (target.Output is { Length: > 0 } output)
                {
                    foreach (var metadata in solution.GetProject(id)!.MetadataReferences
                        .OfType<PortableExecutableReference>()
                        .Where(held => string.Equals(held.FilePath, output, StringComparison.Ordinal))
                        .ToList())
                    {
                        solution = solution.RemoveMetadataReference(id, metadata);
                    }
                }

                if (!workspace.TryApplyChanges(solution))
                {
                    log.WriteLine($"  ! {Path.GetFileName(result.ProjectFilePath)}: "
                        + $"the workspace refused a reference to {Path.GetFileName(reference)}");
                }
            }
        }
    }

    /// <summary>
    /// The built target a project reference names: this framework's, or the best one this
    /// project was built for.
    /// </summary>
    /// <remarks>
    /// The fallback is what makes a reference across frameworks resolve at all — a
    /// <c>net10.0</c> project referencing a <c>netstandard2.0</c> library names a project
    /// with no <c>net10.0</c> target, and MSBuild picked the compatible one long before
    /// this. Ranked rather than first-found, so two runs agree.
    /// </remarks>
    private static (ProjectId Id, string? Output)? Target(
        Dictionary<(string Path, string Framework), (ProjectId Id, string? Output)> targets,
        string path,
        string? framework)
    {
        if (targets.TryGetValue((path, framework ?? string.Empty), out var exact))
        {
            return exact;
        }

        foreach (var entry in targets
            .Where(entry => string.Equals(entry.Key.Path, path, StringComparison.Ordinal))
            .OrderByDescending(entry => Rank(entry.Key.Framework)))
        {
            return entry.Value;
        }

        return null;
    }

    /// <summary>One spelling of a path, so two of them can be compared.</summary>
    private static string Full(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : Path.GetFullPath(path);

    /// <summary>One project's design-time build, or nothing and a reason.</summary>
    /// <remarks>
    /// <para>
    /// <b>A throw is retried and a clean answer is not, and that is the whole distinction.</b>
    /// MSBuild is asked out of process, and several at once over one machine occasionally
    /// lose a pipe — a transient failure of the asking, which says nothing about the
    /// project and comes back different the next time. A build that *returns*, without
    /// error and without a compiler invocation to read, has told the truth about this
    /// project: asking again produces the same answer more slowly. Retrying both is how a
    /// broken repository takes three times as long to say so; retrying neither is how a
    /// project set depends on <c>--jobs</c>.
    /// </para>
    /// <para>
    /// <b>The pair is the unit.</b> A multi-targeting project needs the second attempt and
    /// a single-targeted one needs the first, so a throw in either is a throw of the
    /// question rather than of one phrasing of it.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<IAnalyzerResult> BuildOne(
        IProjectAnalyzer analyzer,
        Options options,
        TextWriter log,
        DesignTimeBuild build,
        ref int retried)
    {
        var name = Path.GetFileName(analyzer.ProjectFile.Path);
        var started = DateTime.UtcNow;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var plain = build(analyzer, BuildOptions(options, innerBuilds: false));

                // **A multi-targeting project has no `Compile` target to run.**
                // `TargetFrameworks` plural makes the project an *outer* build whose whole
                // job is to dispatch to one inner build per framework, and `Compile` lives
                // only on the inner ones — so the first attempt comes back `MSB4057: the
                // target does not exist`. Asking the outer build to dispatch `Compile`
                // rather than its default `Build` reaches the same `CoreCompile`, once per
                // framework, and `Preferred` still picks one to walk.
                //
                // Tried second rather than first because which of the two is right is a
                // property of the project, not of the repository: a single-targeted
                // project has no `DispatchToInnerBuilds` either, and would fail the
                // mirror-image way.
                var results = Usable(plain).Count == 0
                    ? build(analyzer, BuildOptions(options, innerBuilds: true))
                    : plain;

                if (Usable(results) is { Count: > 0 } usable)
                {
                    var elapsed = (DateTime.UtcNow - started).TotalSeconds;
                    Say($"  built {name} ({string.Join(", ", usable.Select(one => one.TargetFramework))}, "
                        + $"{usable[0].SourceFiles.Length} files, {elapsed:F1}s)");

                    return usable;
                }

                Say($"  ! {name}: the design-time build failed, skipping it — {Because(plain, results)}");
                return [];
            }
            catch (Exception failure) when (attempt < Attempts)
            {
                Interlocked.Increment(ref retried);
                Say($"  .. {name}: the design-time build threw, asking again "
                    + $"({attempt} of {Attempts}) — {failure.Message}");

                // Escalating, because the thing being waited out is another process
                // finishing with a resource this one wants.
                Thread.Sleep(TimeSpan.FromMilliseconds(200 * attempt));
            }
            catch (Exception failure)
            {
                Say($"  ! {name}: the design-time build threw {Attempts} times, "
                    + $"skipping it — {failure.Message}");
                return [];
            }
        }

        // Several builds run at once, and a half-interleaved progress line is worse
        // than a slightly delayed one.
        void Say(string line)
        {
            lock (log)
            {
                log.WriteLine(line);
            }
        }
    }

    /// <summary>Why a project was skipped, in words a reader can act on.</summary>
    /// <remarks>
    /// <para>
    /// <b>Both attempts are asked, and "no such target" is discounted.</b> One of the two
    /// is always wrong about this project by construction — a single-targeted project has
    /// no <c>DispatchToInnerBuilds</c> and a multi-targeted one has no <c>Compile</c> — so
    /// reporting the last attempt's error tells every reader the wrong thing about why
    /// their project was skipped. What is wanted is whichever attempt failed for a reason
    /// of its own, and the first error is nearly always the real one: a repository that
    /// will not restore says so in the same three words four hundred times.
    /// </para>
    /// <para>
    /// <b>And when every error is discounted, say that instead of printing one.</b> A
    /// build that succeeds without running the compiler leaves nothing to read, and the
    /// only messages left are the two that are wrong by construction — so the report used
    /// to name a target the project was never going to have. It is a real state with a
    /// real cause, and naming it is the difference between a fixable run and a mystery.
    /// </para>
    /// </remarks>
    private static string Because(IAnalyzerResults? plain, IAnalyzerResults? inner)
    {
        var reasons = Reasons(plain).Concat(Reasons(inner)).ToList();

        return reasons.FirstOrDefault(error =>
                !error.Contains("does not exist in the project", StringComparison.Ordinal))
            ?? "MSBuild reported no error and logged no compiler invocation, so there is "
                + "nothing to read. A target skipped as up to date does this";
    }

    /// <summary>Every project the entry point names.</summary>
    private static IReadOnlyList<IProjectAnalyzer> Analyzers(string entry, Options options, TextWriter log)
    {
        var managerOptions = new AnalyzerManagerOptions
        {
            LogWriter = options.Verbose ? log : null,
        };

        if (entry.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var single = new AnalyzerManager(managerOptions);
            var project = single.GetProject(IOPath.Parse(entry));

            return project is null ? [] : [project];
        }

        var manager = new AnalyzerManager(IOPath.Parse(entry), managerOptions);

        var projects = manager.Projects.Values
            .Where(analyzer => analyzer.ProjectFile.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(analyzer => analyzer.ProjectFile.Path, StringComparer.Ordinal)
            .Select(analyzer => Normalised(analyzer, managerOptions))
            .ToList();

        log.WriteLine($"  {projects.Count} C# project(s) in the solution");
        return projects;
    }

    /// <summary>
    /// The same project, asked for under the path MSBuild will call it by.
    /// </summary>
    /// <remarks>
    /// <b>A solution may name a project by a path that climbs.</b> A solution in
    /// <c>app/</c> listing <c>../lib/Lib.csproj</c> is ordinary, and the path is carried
    /// through exactly as written — while MSBuild reports the project it built under the
    /// normalised one. Buildalyzer pairs the two by string, so nothing matches: the build
    /// succeeds, reports no error, and hands back <i>no results</i>. The project is then
    /// skipped, which reads as "it does not build" for a project that builds perfectly.
    /// </remarks>
    private static IProjectAnalyzer Normalised(
        IProjectAnalyzer analyzer,
        AnalyzerManagerOptions options)
    {
        var written = analyzer.ProjectFile.Path.ToString();
        var real = Path.GetFullPath(written);

        return string.Equals(written, real, StringComparison.Ordinal)
            ? analyzer
            : new AnalyzerManager(options).GetProject(IOPath.Parse(real)) ?? analyzer;
    }

    private static EnvironmentOptions BuildOptions(Options options, bool innerBuilds)
    {
        var environment = new EnvironmentOptions
        {
            // The design-time build is the point: evaluate and resolve, do not compile.
            DesignTime = true,
            Restore = options.Restore,
            Preference = EnvironmentPreference.Core,
        };

        // **Which `dotnet` runs MSBuild is the checkout's business, not this process's.**
        // A repository pinning an SDK in `global.json` — dotnet/runtime pins a preview
        // one and bootstraps it into `.dotnet` — needs *that* host: MSBuild ships as a
        // managed dll beside the SDK, and the framework it asks for is the SDK's own.
        // Left to the default, Buildalyzer spawns whichever `dotnet` this indexer was
        // launched by, which resolves the pinned SDK and then cannot run it.
        if (options.Dotnet is { } host)
        {
            environment.DotnetExePath = host;
        }

        // **`Compile`, not `Build`, and certainly not Buildalyzer's default `Clean;Build`.**
        // Both of those delete things. `Clean` is obvious; `Build` is not — it depends
        // on `IncrementalClean`, which removes whatever the *last* build wrote and this
        // one did not, and a design-time build writes nothing. So a run over a checkout
        // someone had already built would empty every `bin` in it. It did exactly that
        // here, to the indexer's own output, while the indexer was running out of it.
        //
        // `Compile` reaches `CoreCompile` through `ResolveReferences`, which is where
        // the compiler command line — the source list, the references, the defines — is
        // logged, and that is the whole of what Buildalyzer reads.
        environment.TargetsToBuild.Clear();

        if (innerBuilds)
        {
            // The outer build of a multi-targeting project, asked to dispatch `Compile`
            // to each inner build rather than its default `Build` — which would drag
            // `IncrementalClean` back in, one framework at a time.
            environment.TargetsToBuild.Add("DispatchToInnerBuilds");
            environment.GlobalProperties["InnerTargets"] = "Compile";
        }
        else
        {
            environment.TargetsToBuild.Add("Compile");
        }

        // **A checkout somebody has built is the normal case, and it used to index as
        // nothing.** `CoreCompile` is incremental: its outputs are the intermediate
        // assembly, its inputs are the sources, and after any ordinary `dotnet build` the
        // former is newer than the latter — so MSBuild skips the target, the compiler
        // command line is never logged, and the whole of what Buildalyzer reads is that
        // line. Every project then comes back succeeded-with-no-result, which the loader
        // reports as a failed design-time build, and a run over a built repository ends
        // with "every project failed to build" and no clue why.
        //
        // `$(NonExistentFile)` is `CoreCompile`'s own escape hatch — it sits in the
        // target's `Inputs` list precisely so a caller can name a file that is not there
        // and make the up-to-date check fail. Nothing is written and nothing is deleted:
        // the target re-runs, logs its command line, and `SkipCompilerExecution` still
        // stops the compiler itself from doing any work.
        environment.GlobalProperties["NonExistentFile"] =
            Path.Combine("__NonExistentSubDir__", "__NonExistentFile.cs");


        // Node reuse leaves MSBuild processes alive between builds, which over a few
        // hundred projects is a few hundred idle processes holding a machine's memory.
        environment.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1";
        environment.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        return environment;
    }

    /// <summary>What MSBuild said went wrong, in the order it said it.</summary>
    private static IEnumerable<string> Reasons(IAnalyzerResults? results) =>
        results is null
            ? []
            : results.BuildEventArguments
                .OfType<Microsoft.Build.Framework.BuildErrorEventArgs>()
                .Select(error => error.Message)
                .OfType<string>();

    /// <summary>
    /// Every target framework this project actually compiled as, newest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>All of them, not the best of them.</b> An outer build dispatched to its inner
    /// builds answers once per framework, and this used to keep the newest and throw the
    /// rest away — so a project targeting <c>net8.0</c> and <c>net10.0</c> was indexed as
    /// the second, and its <c>net8.0</c> compilation, which is a different program, was
    /// never seen. The fan-out consumes exactly what was being discarded, which is why it
    /// costs another walk and not another build.
    /// </para>
    /// <para>
    /// <b>Compiled as, rather than compatible with.</b> A result is here only if MSBuild
    /// ran the compiler for that framework; there is no nearest-compatible reduction, so a
    /// project with no <c>net8.0</c> result is absent from the <c>net8.0</c> index rather
    /// than present under a target it was never built for.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<IAnalyzerResult> Usable(IAnalyzerResults? results) =>
        results is null
            ? []
            : [.. results.Results
                .Where(result => result.Succeeded
                    && result.SourceFiles is { Length: > 0 }
                    && result.TargetFramework is { Length: > 0 })
                .OrderByDescending(result => Rank(result.TargetFramework))];

    private static (int Family, int Version) Rank(string? framework)
    {
        if (string.IsNullOrEmpty(framework))
        {
            return (0, 0);
        }

        // `net10.0` beats `net8.0` beats `netstandard2.0` beats `net472`.
        if (framework.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return (1, Digits(framework));
        }

        if (framework.StartsWith("net", StringComparison.OrdinalIgnoreCase) && framework.Contains('.'))
        {
            return (2, Digits(framework));
        }

        return (0, Digits(framework));

        static int Digits(string text)
        {
            var value = 0;
            foreach (var character in text)
            {
                if (char.IsAsciiDigit(character))
                {
                    value = (value * 10) + (character - '0');
                }
            }

            return value;
        }
    }

    /// <summary>A solution, a project, or the directory one lives in.</summary>
    private static string ResolveEntryPoint(string source)
    {
        if (File.Exists(source))
        {
            return source;
        }

        if (!Directory.Exists(source))
        {
            throw new FileNotFoundException($"nothing to index at {source}");
        }

        // `.slnx` first: a repository carrying both is mid-migration, and the XML one is
        // the one being kept.
        foreach (var pattern in (string[])["*.slnx", "*.sln", "*.csproj"])
        {
            var found = Directory
                .EnumerateFiles(source, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();

            if (found is not null)
            {
                return found;
            }
        }

        throw new FileNotFoundException(
            $"no .slnx, .sln or .csproj directly under {source} — name one with --source");
    }
}
