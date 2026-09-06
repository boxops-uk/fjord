using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>What to index, where the facts go, and how much of it to do.</summary>
/// <remarks>
/// Hand-rolled rather than a command-line library: this program's dependencies are
/// MSBuild and Roslyn, both of which are large, and adding a third to read six flags
/// would be the wrong trade.
/// </remarks>
internal sealed record Options
{
    /// <summary>The solutions to index — <c>.sln</c> or <c>.slnx</c>, repeatable.</summary>
    /// <remarks>
    /// <b>Named rather than discovered, because discovery has no right answer.</b> A
    /// directory holding two solutions gave whichever sorted first, silently: a repository
    /// with <c>ShareX.ImageEditor.sln</c> beside <c>ShareX.sln</c> indexed three of its
    /// thirteen projects and reported the index complete under <c>--strict</c>, because
    /// "complete" could only ever mean "complete for the entry point I chose". Choosing is
    /// the caller's, and saying so is what makes <c>--strict</c>'s claim true.
    /// </remarks>
    public string[] Solutions { get; init; } = [];

    /// <summary>Individual projects to index — <c>.csproj</c>, repeatable.</summary>
    public string[] Projects { get; init; } = [];

    /// <summary>
    /// A composed schema to create missing databases from — the output of
    /// <c>fjord schema compose</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Composed, because this program cannot compose one.</b> A schema's imports are
    /// namespaces mapped to relative paths under a search root, and following them is
    /// resolution — sigla's, not something to reimplement here badly. Asking the *server*
    /// to read the files instead would be asking it to have this machine's filesystem,
    /// which is false the moment the address is a TCP one. So the composing is done by
    /// the one resolver there is, ahead of the run, and what arrives here is a single file
    /// carrying no <c>import</c> at all.
    /// </para>
    /// <para>
    /// <b>Optional, and only for the databases this run would otherwise fail on.</b>
    /// Without it a missing database is the error it always was; with it a run that
    /// compiles for three frameworks makes its own three rather than requiring a caller to
    /// discover the framework names first — which cost a full design-time load of the
    /// whole solution to learn names this program computes for itself a moment later.
    /// </para>
    /// </remarks>
    public string? Schema { get; init; }

    /// <summary>Everything named, in the order it was named.</summary>
    /// <remarks>
    /// <b>One index, however many were named.</b> Solutions and projects union into one
    /// database per target framework: a caller naming two solutions means "index both into
    /// this", and a project appearing in two of them is walked once because the walk
    /// deduplicates by path.
    /// </remarks>
    public IEnumerable<string> Entries => Solutions.Concat(Projects);

    /// <summary>
    /// The directory paths are reported relative to. Defaults to the solution's own
    /// directory, so a file fact reads <c>src/Foo/Bar.cs</c> rather than naming whoever
    /// happened to check the repository out.
    /// </summary>
    public string? Root { get; init; }

    /// <summary>
    /// The <c>dotnet</c> host that runs the design-time builds.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>&lt;root&gt;/.dotnet/dotnet</c> when the checkout has one, which
    /// is where a repository pinning an SDK in <c>global.json</c> bootstraps it — the
    /// arcade convention, and dotnet/runtime's. Null means whichever <c>dotnet</c>
    /// Buildalyzer finds, which is right for a repository that pins nothing.
    /// </remarks>
    public string? Dotnet { get; init; }

    /// <summary>
    /// Where to write, and which database: <c>[where//]name[@instance]</c>.
    /// </summary>
    /// <remarks>
    /// The same address grammar the `fjord` CLI takes, so a producer is pointed at a
    /// server the way everything else is. An address naming no target means the socket
    /// in <see cref="DefaultSocket"/>, since this producer has no configuration layer to
    /// consult.
    /// </remarks>
    public FjordAddress Address { get; init; } =
        FjordAddress.ForSocket(DefaultSocket, "code");

    /// <summary>Where a target-less address goes.</summary>
    public const string DefaultSocket = "/tmp/fjord.sock";

    /// <summary>Index and encode, but connect to nothing — what the volume would be.</summary>
    public bool DryRun { get; init; }

    /// <summary>Also write every block to this file, which is the fact-file format.</summary>
    public string? Emit { get; init; }

    /// <summary>Emit cross-references. Off is a declarations-only index.</summary>
    public bool References { get; init; } = true;

    /// <summary>
    /// Emit <c>src.FileLine</c>: the file's line table, one fact per line of source.
    /// </summary>
    /// <remarks>
    /// The largest predicate in the index by bytes and the second largest by count, and
    /// the only one whose facts are not about a symbol — so it is the one worth being
    /// able to leave out when what is being measured is the semantic index.
    /// </remarks>
    public bool Lines { get; init; } = true;

    /// <summary>Read the doc comment above a declaration, for `codemarkup.SymbolInfo`.</summary>
    public bool Docs { get; init; } = true;

    /// <summary>
    /// Emit <c>src.FileLineStyles</c>: syntax highlighting, from Roslyn's own classifier.
    /// </summary>
    /// <remarks>
    /// <b>Off by default, because it is not free and not everyone renders.</b> Semantic
    /// classification runs the same machinery Visual Studio colours with, per file, and an
    /// index built to answer queries has no use for it. It is on for a viewer and off for
    /// everything else, which is why this is a flag rather than a property of the schema —
    /// the facts are simply absent, and absent means "not tokenised".
    /// </remarks>
    public bool Styles { get; init; }

    /// <summary>Facts per block. A block is one <c>CopyData</c> frame and one interning batch.</summary>
    public int Batch { get; init; } = 4096;

    /// <summary>Stop after this many source files. 0 means all of them.</summary>
    public int MaxFiles { get; init; }

    /// <summary>Stop after this many projects. 0 means all of them.</summary>
    public int MaxProjects { get; init; }

    /// <summary>
    /// How much of the machine to use: design-time builds at once — each its own
    /// process — and files walked at once inside this one.
    /// </summary>
    public int Jobs { get; init; } = Math.Min(4, Environment.ProcessorCount);

    /// <summary>
    /// Write streams to the server, each on its own connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database takes as many writers as there are streams — it excludes them per key
    /// rather than per database — so this is how much of that the indexer asks for.
    /// </para>
    /// <para>
    /// <b>One by default, because that is what has been measured.</b> This was set to
    /// follow <see cref="Jobs"/> on the reasoning that the two sides of a run should be
    /// sized alike, and a 16-file corpus said otherwise: four writers cost ~10%
    /// throughput and moved nothing, because <c>queueing</c> was already near zero — the
    /// writer was not the ceiling, so more of them could only add connections and
    /// handshakes. Most of that is fixed cost and would vanish at scale, but "would" is
    /// not a measurement, and a default should not be an argument.
    /// </para>
    /// <para>
    /// <b>Measured, and the answer is the corpus rather than a number.</b> Over 5.8M facts
    /// from 111 src-only projects of <c>dotnet/runtime</c>'s shared framework: 55,652
    /// facts/s at one writer, 54,443 at four, 51,020 at eight — worse each time. Over 6.1M
    /// facts from 40 of them: 51,013 at one writer and 64,849 at four — <b>1.27× better</b>.
    /// Same flag, same machine, opposite sign. So one is the right default for a caller who
    /// has measured nothing, and it is not the right answer everywhere.
    /// <para>
    /// <b>The writers are the ceiling on both.</b> At one writer, the writer is inside
    /// <c>Write</c> for 116.5s of a 122.0s walk; at four, each is busy 93% of a shorter
    /// one, and four of them buy 1.31× rather than anything near four — so what they wait
    /// on is shared, and it is the server. 6.1M facts sent are 54.7M interning attempts
    /// (5,431,545 created, 49,289,453 deduped), which is a <c>keys</c> probe apiece plus
    /// staging, commit and wire decode.
    /// </para>
    /// <para>
    /// <b><c>contended</c> is no longer the number to read here, and used to be
    /// misleading.</b> It stood at ~48% of a walker's time because
    /// <see cref="Boxops.Fjord.Client.FactSink"/> held a predicate's batch lock across an
    /// enqueue that blocks — so a writer's backpressure was counted as producer
    /// serialisation. It is ~0 now, and the wait it was hiding is in <c>queueing</c>,
    /// where it belongs.
    /// </para>
    /// </para>
    /// </remarks>
    public int Writers { get; init; } = 1;

    /// <summary>Let the design-time build restore first. Off is much faster when it is already restored.</summary>
    public bool Restore { get; init; } = true;

    /// <summary>Run a handful of queries against what was just written.</summary>
    public bool Smoke { get; init; } = true;

    /// <summary>Let MSBuild's own output through.</summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// The repository this checkout is of, written as <c>src.FileOrigin</c> — null when a
    /// run does not say.
    /// </summary>
    /// <remarks>
    /// <b>Provenance is not in the code.</b> Which repository and which revision a
    /// directory is a checkout of are facts about the checkout, so a run either states them
    /// or the index does not carry them; guessing from a `.git` directory would put
    /// something in the index that the next consumer would have to distrust. Paired with
    /// <see cref="Revision"/> and refused without it.
    /// </remarks>
    public string? Repo { get; init; }

    /// <summary>The revision indexed. Paired with <see cref="Repo"/>.</summary>
    public string? Revision { get; init; }

    /// <summary>
    /// Index only this target framework, rather than fanning out over every one the
    /// checkout compiles for.
    /// </summary>
    /// <remarks>
    /// <b>Selects, never reduces.</b> A project is indexed under a framework only if
    /// MSBuild compiled it as that framework; there is no nearest-compatible fallback, so
    /// asking for <c>net10.0</c> in a solution half of which is <c>net8.0</c> leaves that
    /// half out and says which projects those were. That is the honest answer — a
    /// <c>net8.0</c> compilation is a different program — and <see cref="Strict"/> is how
    /// a run refuses to accept it quietly.
    /// </remarks>
    public string? Framework { get; init; }

    /// <summary>The build configuration the index was resolved against.</summary>
    /// <remarks>
    /// Recorded rather than pinned: nothing here passes it to MSBuild, so what this says
    /// is what the design-time build defaulted to. Silence was the thing to fix — a
    /// database that does not say which configuration it was built for cannot be told from
    /// one built for the other.
    /// </remarks>
    public string Configuration { get; init; } = "Debug";

    /// <summary>
    /// Print the target frameworks this checkout compiles for, and write nothing.
    /// </summary>
    /// <remarks>
    /// For the caller that has to <i>create</i> the databases a fan-out writes to, which
    /// is a server operation this producer cannot do. It costs a full load, so a large
    /// checkout is better off pinning <c>--framework</c> than asking twice.
    /// </remarks>
    public bool ListFrameworks { get; init; }

    /// <summary>
    /// Any project or target left out makes the run fail rather than warn.
    /// </summary>
    /// <remarks>
    /// For CI, which wants "the index is complete" to be a check rather than a line
    /// somebody reads. Off by default, because a developer indexing a repository with one
    /// unbuildable project wants the other four hundred.
    /// </remarks>
    public bool Strict { get; init; }

    public const string Usage = """
        fjord-indexer — index a .NET solution into a Fjord database

          --sln <path>          a .sln or .slnx to index; repeatable
          --project <path>      a .csproj to index; repeatable
                                (name at least one of --sln or --project)
          --root <path>         paths are reported relative to this (default: the single
                                input's own directory; required if more than one is named)
          --schema <path>       a composed schema (`fjord schema compose`) to create any
                                database this run needs and does not find
          --dotnet <path>       the dotnet host to build with (default: <root>/.dotnet/dotnet if present)
          --at <address>        where to write: [where//]name[@instance]
                                (default: code, on /tmp/fjord.sock — a bare name means
                                that socket, `box:7280//code` means TCP, and
                                `/run/fjord.sock//code` names a socket)
          --batch <n>           facts per block (default: 4096)
          --max-files <n>       stop after n source files
          --max-projects <n>    stop after n projects
          --jobs <n>            builds, and files walked, at once (default: 4, or fewer cores)
          --writers <n>         concurrent write streams, one connection each (default: 1;
                                raise it when the report's `queueing` is a real share of the run)
          --no-refs             declarations only: no cross-references
          --no-lines            do not write the line table (src.FileLine)
          --styles              also write syntax highlighting (src.FileLineStyles)
          --framework <tfm>     index only this target framework (default: one database
                                per framework the checkout compiles for, named <at>#<tfm>)
          --configuration <c>   the configuration this index is resolved against, recorded
                                as config.Setting (default: Debug)
          --strict              a project or target left out fails the run
          --list-frameworks     print the frameworks this checkout compiles for, and stop
          --repo <id>           the repository this checkout is of, per file
          --revision <rev>      the revision indexed (both, or neither: src.FileOrigin)
          --no-docs             do not read doc comments (codemarkup.SymbolInfo.doc)
          --no-restore          do not let the design-time build restore first
          --dry-run             index and encode, but connect to nothing
          --emit <path>         also write every block to a file
          --no-smoke            do not query the index afterwards
          --verbose             let MSBuild's output through
          --help
        """;

    /// <summary>The `where` half of an address, for the flags that set one half.</summary>
    private static string Where(string address)
    {
        var at = address.LastIndexOf(FjordAddress.Separator, StringComparison.Ordinal);
        return at < 0 ? "" : address[..at];
    }

    /// <summary>The database half, likewise.</summary>
    private static string Database(string address)
    {
        var at = address.LastIndexOf(FjordAddress.Separator, StringComparison.Ordinal);
        return at < 0 ? address : address[(at + FjordAddress.Separator.Length)..];
    }

    /// <summary>Parse <paramref name="argv"/>, or say what is wrong with it.</summary>
    public static bool TryParse(string[] argv, out Options options, out string? error)
    {
        options = null!;
        error = null;

        string? root = null, emit = null, dotnet = null, schema = null;
        var solutions = new List<string>();
        var projects = new List<string>();
        var at = $"{DefaultSocket}{FjordAddress.Separator}code";
        int batch = 4096, maxFiles = 0, maxProjects = 0;
        var jobs = Math.Min(4, Environment.ProcessorCount);
        int? writers = null;
        bool references = true, restore = true;
        bool lines = true, docs = true, styles = false;
        string? repo = null, revision = null, framework = null;
        var configuration = "Debug";
        bool dryRun = false, smoke = true, verbose = false, strict = false;
        var listFrameworks = false;

        for (var index = 0; index < argv.Length; index++)
        {
            var flag = argv[index];

            // A flag that takes a value, and the value that is not there.
            string Value()
            {
                if (index + 1 >= argv.Length)
                {
                    throw new FormatException($"`{flag}` wants a value");
                }

                return argv[++index];
            }

            int Number()
            {
                var text = Value();
                return int.TryParse(text, out var number) && number >= 0
                    ? number
                    : throw new FormatException($"`{flag}` wants a number, not `{text}`");
            }

            try
            {
                switch (flag)
                {
                    case "--sln": solutions.Add(Value()); break;
                    case "--project": projects.Add(Value()); break;
                    case "--root": root = Value(); break;
                    case "--schema": schema = Value(); break;
                    case "--dotnet": dotnet = Value(); break;
                    case "--at": at = Value(); break;

                    // The two flags this replaced, kept working so a script written
                    // against them still runs — they compose into one address.
                    case "--socket": at = $"{Value()}{FjordAddress.Separator}{Database(at)}"; break;
                    case "--database": at = $"{Where(at)}{FjordAddress.Separator}{Value()}"; break;
                    case "--emit": emit = Value(); break;
                    case "--batch": batch = Number(); break;
                    case "--max-files": maxFiles = Number(); break;
                    case "--max-projects": maxProjects = Number(); break;
                    case "--jobs": jobs = Math.Max(1, Number()); break;
                    case "--writers": writers = Math.Max(1, Number()); break;
                    case "--no-refs": references = false; break;
                    case "--no-lines": lines = false; break;
                    case "--styles": styles = true; break;
                    case "--repo": repo = Value(); break;
                    case "--revision": revision = Value(); break;
                    case "--framework": framework = Value(); break;
                    case "--configuration": configuration = Value(); break;
                    case "--strict": strict = true; break;
                    case "--list-frameworks": listFrameworks = true; break;
                    case "--no-docs": docs = false; break;
                    case "--no-restore": restore = false; break;
                    case "--dry-run": dryRun = true; break;
                    case "--no-smoke": smoke = false; break;
                    case "--verbose": verbose = true; break;

                    case "--help" or "-h":
                        error = Usage;
                        return false;

                    default:
                        error = $"unknown flag `{flag}`\n\n{Usage}";
                        return false;
                }
            }
            catch (FormatException failure)
            {
                error = failure.Message;
                return false;
            }
        }

        if (solutions.Count + projects.Count == 0)
        {
            error = $"name what to index: --sln <path> or --project <path>\n\n{Usage}";
            return false;
        }

        // **One root, and it is what every `src.File` path is relative to** — the schema
        // says so, as `config.Setting {dimension = "index-root"}`. With one input it can be
        // inferred; with several there is no answer that is not a guess, and a wrong one
        // makes every path in the index depend on where a checkout happens to sit. Two
        // roots mean two databases, which is a second run.
        if (root is null && solutions.Count + projects.Count > 1)
        {
            error = "--root is required when more than one --sln or --project is named: "
                + "every file path in the index is relative to it";
            return false;
        }

        if (batch < 1)
        {
            error = "--batch must be at least 1";
            return false;
        }

        // **Half a pair is worse than neither.** `src.FileOrigin` carries both fields on one
        // fact, so a run stating one would write provenance asserting the other is the empty
        // string — a claim in the index that no consumer can tell apart from a gap.
        if (repo is null != revision is null)
        {
            error = "--repo and --revision are a pair: state both, or neither";
            return false;
        }

        FjordAddress address;
        try
        {
            address = FjordAddress.Parse(at).OrSocket(DefaultSocket);
        }
        catch (FormatException failure)
        {
            error = failure.Message;
            return false;
        }

        options = new Options
        {
            Address = address,
            Solutions = [.. solutions.Select(Path.GetFullPath)],
            Projects = [.. projects.Select(Path.GetFullPath)],
            Root = root is null ? null : Path.GetFullPath(root),
            Schema = schema is null ? null : Path.GetFullPath(schema),
            Dotnet = dotnet is null
                ? Bootstrapped(solutions.Concat(projects).First(), root)
                : Path.GetFullPath(dotnet),
            Emit = emit is null ? null : Path.GetFullPath(emit),
            Batch = batch,
            MaxFiles = maxFiles,
            MaxProjects = maxProjects,
            Jobs = jobs,
            Writers = writers ?? 1,
            References = references,
            Lines = lines,
            Styles = styles,
            Framework = framework,
            Configuration = configuration,
            Strict = strict,
            ListFrameworks = listFrameworks,
            Repo = repo,
            Revision = revision,
            Docs = docs,
            Restore = restore,
            DryRun = dryRun,
            Smoke = smoke && !dryRun,
            Verbose = verbose,
        };

        return true;
    }

    /// <summary>
    /// The <c>dotnet</c> a checkout bootstrapped for itself, if it did.
    /// </summary>
    /// <remarks>
    /// A repository pinning an SDK in <c>global.json</c> installs it into <c>.dotnet</c>
    /// at its root — arcade's convention, and what <c>eng/common/dotnet.sh</c> does.
    /// Finding it is worth more than a flag nobody remembers to pass: the failure it
    /// prevents is every design-time build failing at once, which reads as a repository
    /// that will not build rather than a host that cannot run its MSBuild.
    /// <para>
    /// <b>The <c>global.json</c> beside it is the whole test.</b> A bare <c>.dotnet</c>
    /// directory is not evidence of anything — <c>$HOME/.dotnet</c> is where a per-user
    /// install lands, so walking ancestors for the directory alone finds it for every
    /// checkout under a home directory and quietly builds against the wrong SDK. What
    /// is being looked for is a checkout that pinned one, and the pin is the file.
    /// </para>
    /// </remarks>
    private static string? Bootstrapped(string named, string? root)
    {
        var checkout = root ?? Path.GetDirectoryName(Path.GetFullPath(named));

        while (checkout is not null)
        {
            var host = Path.Combine(checkout, ".dotnet", "dotnet");

            if (File.Exists(host) && File.Exists(Path.Combine(checkout, "global.json")))
            {
                return Path.GetFullPath(host);
            }

            checkout = Path.GetDirectoryName(checkout);
        }

        return null;
    }
}
