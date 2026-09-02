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
internal sealed record LoadedSolution(IReadOnlyList<LoadedProject> Projects, ProjectIndex Build);

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
    public static LoadedSolution Load(Options options, string root, TextWriter log)
    {
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
        var results = new IAnalyzerResult?[analyzers.Count];

        Parallel.For(0, analyzers.Count, new ParallelOptions { MaxDegreeOfParallelism = options.Jobs }, index =>
        {
            results[index] = BuildOne(analyzers[index], options, log);
        });

        var added = new List<(IAnalyzerResult Result, ProjectId Id)>();
        var failed = 0;

        // Added in the order the solution lists them rather than the order they
        // finished, so two runs over one checkout produce the same index.
        foreach (var result in results)
        {
            if (result is null)
            {
                failed++;
                continue;
            }

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

        var built = added.Count;

        // **A run that cannot resolve fails, and must not fall back to a syntax walk.**
        // Globbing the `.cs` files and parsing them against the running framework's
        // reference set finds every declaration and loses every reference into a NuGet
        // package — the type is an error type, so the member on it binds to nothing — and
        // the result is an index that looks complete while missing most of its edges,
        // with nothing in it to say so.
        //
        // The rule this protects: a producer that cannot resolve emits nothing rather
        // than a degraded fact.
        if (built == 0)
        {
            throw new InvalidOperationException(
                failed == 0
                    ? "no projects were found under --source, so there is nothing to "
                        + "resolve against and nothing to index"
                    : $"every project failed to build ({failed} of them), so no type in "
                        + "this checkout can be resolved. Fix the build — a restore, an "
                        + "SDK, a missing reference — and run again; this indexer writes "
                        + "no facts it cannot resolve");
        }

        if (failed > 0)
        {
            log.WriteLine($"  {failed} project(s) skipped, {built} built");
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
        var build = ProjectIndex.Build(
            root,
            options.Source,
            results.Where(result => result is not null).Select(result => result!).ToList(),
            log);

        return new LoadedSolution(walking, build);
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
    private static IAnalyzerResult? BuildOne(IProjectAnalyzer analyzer, Options options, TextWriter log)
    {
        var name = Path.GetFileName(analyzer.ProjectFile.Path);
        var started = DateTime.UtcNow;

        var plain = Attempt(innerBuilds: false);
        var results = plain;

        // **A multi-targeting project has no `Compile` target to run.** `TargetFrameworks`
        // plural makes the project an *outer* build whose whole job is to dispatch to one
        // inner build per framework, and `Compile` lives only on the inner ones — so the
        // first attempt comes back `MSB4057: the target does not exist`. Asking the outer
        // build to dispatch `Compile` rather than its default `Build` reaches the same
        // `CoreCompile`, once per framework, and `Preferred` still picks one to walk.
        //
        // Tried second rather than first because which of the two is right is a property
        // of the project, not of the repository: a single-targeted project has no
        // `DispatchToInnerBuilds` either, and would fail the mirror-image way.
        if (Preferred(results) is null)
        {
            results = Attempt(innerBuilds: true);
        }

        if (results is null)
        {
            return null;
        }

        if (Preferred(results) is not { } result)
        {
            // The first error is nearly always the real one, and a repository that will
            // not restore says so in the same three words four hundred times.
            //
            // **Both attempts are asked, and "no such target" is discounted.** One of
            // the two is always wrong about this project by construction — a
            // single-targeted project has no `DispatchToInnerBuilds` and a
            // multi-targeted one has no `Compile` — so reporting the last attempt's
            // error tells every reader the wrong thing about why their project was
            // skipped. What is wanted is whichever attempt failed for a reason of its
            // own.
            var reason = Reasons(plain).Concat(Reasons(results))
                .FirstOrDefault(error => !error.Contains("does not exist in the project", StringComparison.Ordinal))
                ?? Reasons(plain).Concat(Reasons(results)).FirstOrDefault();

            Say($"  ! {name}: the design-time build failed, skipping it"
                + (reason is null ? string.Empty : $" — {reason}"));

            return null;
        }

        var elapsed = (DateTime.UtcNow - started).TotalSeconds;
        Say($"  built {name} ({result.TargetFramework}, {result.SourceFiles.Length} files, {elapsed:F1}s)");

        return result;

        // One design-time build, or nothing and a reason. A throw is this project's
        // failure and not the run's, exactly as a build error is.
        IAnalyzerResults? Attempt(bool innerBuilds)
        {
            try
            {
                return analyzer.Build(BuildOptions(options, innerBuilds));
            }
            catch (Exception failure)
            {
                Say($"  ! {name}: the design-time build threw — {failure.Message}");
                return null;
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
            .ToList();

        log.WriteLine($"  {projects.Count} C# project(s) in the solution");
        return projects;
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

    /// <summary>
    /// One target framework's result, preferring the newest .NET a multi-targeted
    /// project builds for.
    /// </summary>
    /// <remarks>
    /// Indexing every target framework of a multi-targeted project would index the same
    /// files two or three times over. They dedup on the way in — the facts are
    /// identical — but the work is not, so one is picked here.
    /// </remarks>
    /// <summary>What MSBuild said went wrong, in the order it said it.</summary>
    private static IEnumerable<string> Reasons(IAnalyzerResults? results) =>
        results is null
            ? []
            : results.BuildEventArguments
                .OfType<Microsoft.Build.Framework.BuildErrorEventArgs>()
                .Select(error => error.Message)
                .OfType<string>();

    private static IAnalyzerResult? Preferred(IAnalyzerResults? results) =>
        results?.Results
            .Where(result => result.Succeeded && result.SourceFiles is { Length: > 0 })
            .OrderByDescending(result => Rank(result.TargetFramework))
            .FirstOrDefault();

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
