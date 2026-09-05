using System.Diagnostics;
using System.Globalization;

using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// A real indexer for a real language, pointed at a Fjord database.
/// </summary>
/// <remarks>
/// <para>
/// <c>Fjord.Demo</c> shows that the protocol works by writing six declarations
/// somebody typed out. This writes however many a checkout of .NET source contains,
/// which is the other thing a database needs to be shown: that it holds up when the
/// facts are not chosen to be convenient.
/// </para>
/// <para>
/// The shape of the run is deliberately the same as the demo's, because the demo's
/// shape is the point — <b>a producer that holds no fact ids</b>. Roslyn hands this
/// program a symbol; it turns the symbol into the declaration fact that names it and
/// nests that whole fact wherever a reference to it goes. It keeps no map from entities
/// to identities and it emits in whatever order the walk reaches things. At a million
/// facts that stops being an elegance argument and starts being the only tractable
/// option: the alternative is a second pass over an index that no longer fits in
/// memory.
/// </para>
/// </remarks>
internal static class Program
{
    public static int Main(string[] argv)
    {
        if (!Options.TryParse(argv, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return ReferenceEquals(error, Options.Usage) ? 0 : 2;
        }

        var root = options.Root
            ?? (Directory.Exists(options.Source) ? options.Source : Path.GetDirectoryName(options.Source)!);

        // **`--list-frameworks` answers on stdout and says everything else on stderr**, so
        // a caller can read the list with `$(...)` rather than by filtering a log.
        var say = options.ListFrameworks ? Console.Error : Console.Out;

        say.WriteLine($"indexing {options.Source}");
        say.WriteLine($"  paths relative to {root}");
        say.WriteLine($"  schema fingerprint {DotnetIndex.Schema.Fingerprint:x16}");

        var loading = Stopwatch.StartNew();
        LoadedSolution solution;

        try
        {
            solution = Loader.Load(options, root, say);
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"could not load {options.Source}: {failure.Message}");
            return 1;
        }

        loading.Stop();

        var flavoured = solution.Targets.Count > 1;

        // Asked and answered: the caller that has to create these databases cannot be told
        // by a run that has already tried to write to them.
        if (options.ListFrameworks)
        {
            foreach (var target in solution.Targets)
            {
                Console.WriteLine(target.Framework);
            }

            return 0;
        }

        say.WriteLine($"  {solution.Targets.Count} target framework(s) — "
            + $"{string.Join(", ", solution.Targets.Select(target => target.Framework))}"
            + $", loaded in {loading.Elapsed.TotalSeconds:F1}s"
            // Said out loud because it is the difference between a machine under load and
            // a repository that does not build: a retried build is one that threw, and a
            // run with many of them was fighting for a machine rather than reading code.
            + (solution.Retried > 0 ? $", {solution.Retried} build(s) retried" : string.Empty));

        // **`--strict` is for CI, where "the index is complete" should be a check rather
        // than a line somebody reads.** A developer indexing a repository with one
        // unbuildable project wants the other four hundred, so this is off by default and
        // the run says what it left out either way.
        if (options.Strict && solution.Skipped.Count > 0)
        {
            Console.Error.WriteLine(
                $"--strict: {solution.Skipped.Count} project(s) were left out of this index — "
                + string.Join(", ", solution.Skipped));
            return 1;
        }

        Console.WriteLine();

        foreach (var target in solution.Targets)
        {
            // **One database per target, and the flavour is only added when there is one
            // to add.** A checkout with a single target framework writes the database it
            // always wrote; a checkout with two writes `code#net10.0` and `code#net8.0`,
            // because a project compiled twice is two programs and there is no key in the
            // schema that could hold both.
            //
            // **`#` and not `@`**: the server's name check refuses `@`, which separates a
            // name from an instance, and would resolve `code@net9.0` as a lookup by id.
            var each = flavoured
                ? options with
                {
                    Address = FjordAddress.Parse($"{options.Address}#{target.Framework}"),

                    // **The emitted file is flavoured too, or the second target silently
                    // replaces the first's.** `--emit` opens its path for writing, so a
                    // fan-out over two frameworks would leave one file holding whichever
                    // ran last — a golden that depends on the order of a loop.
                    Emit = options.Emit is { } path
                        ? Path.Combine(
                            Path.GetDirectoryName(path) ?? string.Empty,
                            $"{Path.GetFileNameWithoutExtension(path)}.{target.Framework}"
                                + Path.GetExtension(path))
                        : null,
                }
                : options;

            if (flavoured)
            {
                Console.WriteLine($"== {target.Framework} → {each.Address}");
            }

            int code;

            try
            {
                code = Walk(each, root, target);
            }
            catch (FjordServerException refused)
            {
                // **A refusal is an answer, not a crash.** The server says no for reasons a
                // person can act on — the database is sealed, the schema does not match,
                // the name is not there — and every one of them arrived as an unhandled
                // exception with a stack trace through `Connect`, which buries the sentence
                // that matters under twenty frames of this program's own plumbing.
                Console.Error.WriteLine($"could not write to {each.Address}: {refused.Message}");
                return 1;
            }

            if (code != 0)
            {
                return code;
            }
        }

        return 0;
    }

    /// <summary>One target framework: connect, write its facts, and say what it wrote.</summary>
    private static int Walk(Options options, string root, LoadedTarget target)
    {
        List<FjordConnection> connections = Connect(options);
        using var closing = new Closing<FjordConnection>(connections);
        var connection = connections.Count > 0 ? connections[0] : null;

        if (connection is not null)
        {
            Console.WriteLine($"  connected: protocol {connection.Hello.Version}, "
                + $"{connection.Hello.Predicates} predicates, schema {connection.Hello.SchemaFingerprint:x16}");
            Console.WriteLine();
        }

        var targets = Targets(connections);
        using var closingTargets = new Closing<IBlockTarget>(targets);

        var walking = Stopwatch.StartNew();
        int files;
        Indexer indexer;

        using (var sink = new FactSink(
            DotnetIndex.Schema, targets, options.Batch, options.Emit))
        {
            // **`--emit` walks on one thread as well as writing on one.** The flag exists
            // to produce a file whose bytes can be compared — a golden — and one writer is
            // only half of what that takes: the block *order* is the order the walk
            // reached things, so eight walker threads produce a different file every run
            // with the same facts in it. The design-time builds have already happened by
            // here, so `--jobs` keeps its meaning for the half of the run that is slow.
            indexer = new Indexer(
                options.Emit is null ? options : options with { Jobs = 1 },
                sink,
                root,
                target.Build);
            var reported = TimeSpan.Zero;

            // What this database is, before what is in it: the axes it was resolved
            // against are the first thing a consumer has to agree with, and a database
            // that does not say them can only be guessed at.
            foreach (var setting in Provenance.Of(options, root, target.Framework, Version))
            {
                sink.Add(DotnetIndex.Setting, setting);
            }

            // The build layer first, and whole: this is what the repository *is*, not
            // what the walk reached, so a run stopped early by `--max-files` still says
            // which projects exist and what they depend on.
            target.Build.Emit(sink.Add);

            foreach (var project in target.Projects)
            {
                if (indexer.Exhausted)
                {
                    Console.WriteLine($"stopping at {options.MaxFiles} files (--max-files)");
                    break;
                }

                // Compiled here rather than up front, so one project's symbols are
                // reachable only while that project is being walked.
                if (project.Compile() is not { } compilation)
                {
                    Console.WriteLine($"  ! {project.Name}: no compilation, skipping it");
                    continue;
                }

                indexer.Index(compilation, project.Roslyn, _ =>
                {
                    // Every couple of seconds, not every file: a hundred thousand
                    // progress lines is not progress.
                    if (walking.Elapsed - reported < TimeSpan.FromSeconds(2))
                    {
                        return;
                    }

                    reported = walking.Elapsed;
                    var rate = sink.Total / Math.Max(walking.Elapsed.TotalSeconds, 0.001);

                    Console.WriteLine(
                        $"  {indexer.Files,7} files  {Count(sink.Total),12} facts  "
                        + $"{Count((long)rate),9} facts/s  {project.Name}");
                });
            }

            // Drain rather than flush: the writer thread is still draining what
            // FlushAll queues, and every count below — and the elapsed time they are
            // divided by — is only final once it has stopped.
            sink.Drain();
            walking.Stop();
            files = indexer.Files;

            Report(options, sink, indexer, target.Build, walking.Elapsed);
        }

        if (connection is not null && options.Smoke && files > 0)
        {
            Smoke(connection, indexer);
        }

        // **The same reading a project that would not build gets** — the load layer checks
        // its own skips before the walk, and this is the skip only the walk can see, so it
        // is checked here rather than beside that one. A run that left a project's source
        // out is not a complete index, and `--strict` is what makes that a check rather
        // than a line somebody reads.
        if (options.Strict && indexer.DuplicateAssemblies.Count > 0)
        {
            Console.Error.WriteLine(
                $"--strict: {indexer.DuplicateAssemblies.Count} project(s) were left out of "
                + "this index because another project produces the same assembly — "
                + string.Join(", ", indexer.DuplicateAssemblies));
            return 1;
        }

        return 0;
    }

    /// <summary>This indexer's version, as the assembly records it.</summary>
    /// <remarks>
    /// Read rather than written down, so <c>config.Setting {dimension = "producer"}</c>
    /// cannot drift from the package a consumer would go and fetch.
    /// </remarks>
    private static string Version =>
        typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion.Split('+')[0]
        ?? "unknown";

    /// <summary>Closes every one of them when the run ends, however it ends.</summary>
    /// <remarks>
    /// A list is not <see cref="IDisposable"/>, and a run that threw halfway would
    /// otherwise leave sockets open until the process exited — which is tidy enough for
    /// a tool and untidy for a server counting connections.
    /// </remarks>
    private sealed class Closing<T>(IReadOnlyList<T> items) : IDisposable
        where T : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }

    /// <summary>One write target per connection.</summary>
    /// <remarks>
    /// A list rather than one, because a producer with several writers holds a connection
    /// each — the client issues streams sequentially over one socket, so concurrency is
    /// sockets.
    /// </remarks>
    private static List<IBlockTarget> Targets(IReadOnlyList<FjordConnection> connections) =>
        [.. connections.Select(IBlockTarget (connection) => new FjordTarget(connection))];

    /// <summary>One connection per writer thread.</summary>
    /// <remarks>
    /// <para>
    /// <b>Connections rather than streams, because the client cannot multiplex.</b>
    /// <see cref="FjordConnection"/> issues streams sequentially over one socket, so
    /// two concurrent write streams need two sockets. The server does not mind: it
    /// excludes writers per key rather than per database.
    /// </para>
    /// <para>
    /// <b>One writer when emitting.</b> <c>--emit</c> writes every block to a file, and
    /// that file is a checked-in golden — several writers would interleave into it and
    /// make its contents depend on scheduling. Anything that has to be reproducible byte
    /// for byte gets one writer, whatever <c>--writers</c> says.
    /// </para>
    /// </remarks>
    private static List<FjordConnection> Connect(Options options)
    {
        if (options.DryRun)
        {
            Console.WriteLine("  --dry-run: encoding the facts and connecting to nothing");
            Console.WriteLine();
            return [];
        }

        var writers = options.Emit is null ? options.Writers : 1;
        if (options.Emit is not null && (options.Writers > 1 || options.Jobs > 1))
        {
            Console.WriteLine(
                "  --emit: one writer and one walker, so the file is a deterministic run of blocks");
        }

        Console.WriteLine($"connecting to {options.Address}, {writers} writer(s)");

        // A claim, not a question: an indexer that disagrees with the server about the
        // schema is refused at the handshake rather than after an hour of writing facts
        // nobody can read back.
        var connections = new List<FjordConnection>(writers);
        for (var n = 0; n < writers; n++)
        {
            connections.Add(FjordConnection.Connect(
                options.Address,
                DotnetIndex.Schema,
                SessionMode.ReadWrite,
                assertSchema: true));
        }

        return connections;
    }

    private static void Report(
        Options options,
        FactSink sink,
        Indexer indexer,
        ProjectIndex projects,
        TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine($"indexed {Count(indexer.Files)} file(s) in {elapsed.TotalSeconds:F1}s");

        foreach (var predicate in DotnetIndex.Predicates)
        {
            Console.WriteLine($"  {DotnetIndex.NameOf(predicate),-32}{Count(sink.Facts[predicate]),14}");
        }

        Console.WriteLine($"  {"total",-20}{Count(sink.Total),14} facts in {Count(sink.Blocks)} blocks");

        if (sink.Bytes > 0)
        {
            Console.WriteLine($"  {"encoded",-20}{Megabytes(sink.Bytes),14} MB"
                + $"  ({(double)sink.Bytes / Math.Max(sink.Total, 1):F0} bytes/fact)");
        }

        if (!options.DryRun)
        {
            // Created counts every fact written, nested targets included; deduped those
            // already there. A million references naming ten thousand declarations is
            // supposed to show up here as a large dedup count — that is interning
            // working, and it is the number this whole exercise is a measurement of.
            Console.WriteLine($"  {"server",-20}{Count((long)sink.Created),14} created, "
                + $"{Count((long)sink.Deduped)} deduped");
        }

        if (!options.DryRun)
        {
            Console.WriteLine($"  {"writing",-20}{sink.Writing.TotalSeconds,14:F1}s"
                + $"  (summed over {sink.Writers} writer(s), overlapped — not wall clock)");
            Console.WriteLine($"  {"queueing",-20}{sink.Queueing.TotalSeconds,14:F1}s"
                + $"  (walk blocked on a full queue)");
        }

        {
            // The successor to `gate wait`/`gate held`. The walk no longer has a gate; what
            // it has is one lock per predicate, and this is what they cost together.
            Console.WriteLine($"  {"contended",-20}{sink.Contended.TotalSeconds,14:F1}s"
                + $"  ({Count(sink.Contentions)} of {Count(sink.Total)} facts waited for a batch)");
        }

        var rate = sink.Total / Math.Max(elapsed.TotalSeconds, 0.001);
        Console.WriteLine($"  {"throughput",-20}{Count((long)rate),14} facts/s");

        Console.WriteLine();
        Console.WriteLine($"references: {Count(indexer.References)} resolved, "
            + $"{Count(indexer.External)} to declarations outside the index, "
            + $"{Count(indexer.Unresolved)} unresolved");

        if (indexer.ReferenceAssemblies > 0)
        {
            // Said out loud because it is a decision about the corpus and not a detail:
            // a reader comparing file counts between two runs, or wondering why a `ref/`
            // tree has no definitions in it, is owed the reason here rather than in a
            // doc comment.
            Console.WriteLine($"  {Count(indexer.ReferenceAssemblies)} reference assembly(s) "
                + "left unwalked: the implementation beside each one declares the same API");
        }

        if (indexer.DuplicateAssemblies.Count > 0)
        {
            // Named individually, because this is source somebody wrote that is not in the
            // index — where a reference assembly is a restatement of source that is. A
            // reader has to be able to see *which* project, to decide whether the one that
            // was kept is the one they meant.
            Console.WriteLine(
                $"  {Count(indexer.DuplicateAssemblies.Count)} project(s) left unwalked: "
                + "another project already produces their assembly, and one database "
                + "cannot hold two");

            foreach (var project in indexer.DuplicateAssemblies)
            {
                Console.WriteLine($"    {project}");
            }
        }

        if (indexer.Unattributed > 0)
        {
            // Shared source, or a checkout with no project files under `--source`. Said
            // out loud because a silent zero for `msbuild.SourceFileToProject` looks like
            // a bug in the schema rather than a fact about the repository.
            Console.WriteLine($"  {Count(indexer.Unattributed)} file(s) no project compiles "
                + "(shared source, or outside every project directory)");
        }

        if (projects.Unlinked.Count > 0)
        {
            // A project the solution lists and this index cannot key — its path climbs out
            // of `--root`, so there is no `src.File` for an edge to point at. Counted here
            // because both of its solution edges are missing, and a database holding part
            // of a solution's membership looks exactly like one holding all of it.
            Console.WriteLine($"  {Count(projects.Unlinked.Count)} project(s) the solution "
                + "lists have no project fact, so no solution edge names them "
                + $"({string.Join(", ", projects.Unlinked)})");
        }

        foreach (var dropped in Dropped(indexer))
        {
            Console.WriteLine($"  {dropped}");
        }
    }

    /// <summary>
    /// What this run could not express, one line per cause.
    /// </summary>
    /// <remarks>
    /// <b>Three causes, so one line cannot carry them.</b> A signature naming a type with
    /// no <c>csharp.AType</c> alternative is one, a declaration kind this layer has no
    /// entity for at all is another, and a symbol this producer cannot spell a
    /// <c>src.Symbol</c> for is the third. Attributing the whole count to the first sends
    /// somebody looking for a <c>dynamic</c> that is not there, which is exactly the
    /// silence the counter exists to break — and so does naming one form of the second
    /// cause when it has two: an event, and an <c>extension</c> block, whose members go
    /// with it. The third is the one whose loss depends on where it happened — a
    /// declaration keeps its entity and its span, a reference keeps only its
    /// <c>csharp</c>-layer occurrence, and a relation edge is gone entirely — so the line
    /// points at <see cref="Indexer.Unspellable"/>, which sets that out, rather than
    /// stating the mildest of the three as though it were all of them.
    /// </remarks>
    internal static IEnumerable<string> Dropped(Indexer indexer)
    {
        if (indexer.Unspellable > 0)
        {
            yield return $"{Count(indexer.Unspellable)} spelling(s) not made: a shape this "
                + "producer has no `src.Symbol` for (a declaration keeps its entity and "
                + "span, a reference its `csharp`-layer occurrence, a relation edge "
                + "nothing)";
        }

        if (indexer.InexpressibleTypes > 0)
        {
            yield return $"{Count(indexer.InexpressibleTypes)} declaration(s) dropped: "
                + "a type this layer cannot express (`dynamic`, a function pointer, or a "
                + "name that did not resolve)";
        }

        if (indexer.InexpressibleKinds > 0)
        {
            yield return $"{Count(indexer.InexpressibleKinds)} declaration(s) dropped: "
                + "a kind this layer has no entity for at all (an event, or an "
                + "`extension` block)";
        }
    }

    /// <summary>
    /// Ask the database what it just took, on the same connection.
    /// </summary>
    /// <remarks>
    /// Three questions, chosen for what they cost rather than for what they mean: a scan
    /// of a small predicate, a seek into the search index, and the join that reaches
    /// through a reference. The last one is the interesting number — a cross-reference
    /// keyed by the file it is in reads the whole table to answer "every use of this",
    /// and one keyed by what it points at seeks. Both keyings are stored, which is the
    /// whole argument for a derived predicate.
    /// </remarks>
    /// <summary>Rows each smoke query prints, and therefore the most any of them reads.</summary>
    private const int Sample = 5;

    /// <summary>
    /// What a smoke run demonstrates: a description and the sigla behind it, for a
    /// repository whose walk found <paramref name="sample"/> to ask about.
    /// </summary>
    /// <remarks>
    /// <b>Separate from the printing so that something can run them.</b> A query here is
    /// only ever executed after an index run, and a refusal only ever reaches a person
    /// reading the tail of one — which is how a query with a type error in it shipped, was
    /// printed as `refused (BadQuery)` on every run for a release, and was noticed by
    /// somebody reading output rather than by a red suite.
    /// </remarks>
    internal static IEnumerable<(string What, string Sigla)> SmokeQueries(string? sample)
    {
        yield return ("every namespace, which is a scan",
            "N where csharp.Namespace {name = M, containingNamespace = _}; csharp.Name N; M = csharp.Name N");

        yield return ("every assembly the repository builds, which is the build layer",
            "A where msbuild.Assembly {name = A}");

        yield return ("what a project compiles, which is a seek keyed by the project file",
            "{project = P, src = S} where "
            + "F = src.File P; Q = msbuild.Project {file = F}; "
            + "msbuild.ProjectToSourceFile {project = Q, src = G}; G = src.File S");

        if (sample is not null)
        {
            yield return ($"the definitions named `{sample}`, which is a seek into the search index",
                $"{{name = L}} where csharp.NameLowerCase {{nameLowercase = L, name = N}}; "
                + $"N = csharp.Name \"{sample}\"");

            // **The join that reaches through a reference**, and the one this schema
            // changes the cost of: `EntityRef` leads with the target, so every use of a
            // definition is a seek rather than a read of the whole cross-reference table
            // — which a cross-reference keyed by its own position cannot do. Both steps
            // that reach the definition lead with what is bound, so the whole chain is
            // seeks: over `dotnet/runtime`'s CoreLib this examines two rows to find the
            // symbol, two to reach the definition, and one per answer thereafter.
            //
            // **A `src.Symbol` field holds a reference to the symbol fact and not the
            // string in it**, so `src.Symbol S` against an `S` already bound by
            // `SymbolOf` asks for a fact whose *string key* is a fact — which the
            // typechecker refuses, and refuses at the point the smoke output prints
            // rather than anywhere a test would see. `SymbolByName` is the way in from a
            // name, because the name is the thing this query is given.
            yield return ($"every use of `{sample}`, which is a seek because the target leads",
                $"{{file = P, at = X.use.start}} where "
                + $"codemarkup.SymbolByName {{name = \"{sample}\", symbol = S}}; "
                + $"csharp.DefinitionBySymbol {{symbol = S, definition = D}}; "
                + $"X = csharp.EntityRef {{target = D, file = F}}; F = src.File P");

            // A declaration, the line it is written on, and the text of that line: the
            // search index carries the line number in its key, so the line table is
            // reached by its own key and answers one row.
            //
            // **A `src.FileLine {file = F}` seek is a prefix over every line of the
            // file**, so pairing it with anything that binds only the file is a cross
            // product — every declaration in a file against every line of it, which is
            // tens of millions of rows on a real repository and looks like a join in the
            // query's shape. Reaching the line from `csharp.DefinitionLocation` instead
            // would need the greatest `src.FileLineAt.start` at or below a definition's
            // byte offset, and sigla has no descending seek: the line number in this key
            // is what makes it a seek at all.
            yield return ($"the line declaring a definition, which is a seek keyed by file and line",
                "{name = N, line = Ln, text = L.value} where "
                + "codemarkup.SearchEntry {name = N, file = F, line = Ln}; "
                + "L = src.FileLine {file = F, line = Ln}");
        }

    }

    private static void Smoke(FjordConnection connection, Indexer indexer)
    {
        Console.WriteLine();
        Console.WriteLine("querying it back");

        foreach (var (what, sigla) in SmokeQueries(indexer.SampleName))
        {
            Run(what, sigla);
        }

        // **Five rows and a total, and neither reads the result.** A smoke query is a
        // demonstration whose size nobody chose: one of these once answered 109,720,432
        // rows, and collecting them to print five killed the run at 20.6 GB. The count
        // executes the query without encoding a row, and the sample is one bounded page —
        // so what this prints costs the same whether the answer has five rows or a
        // hundred million.
        void Run(string what, string sigla)
        {
            Console.WriteLine();
            Console.WriteLine($"  {what}");
            Console.WriteLine($"  sigla> {sigla}");

            var started = Stopwatch.StartNew();

            try
            {
                var total = connection.CountRows(sigla);
                started.Stop();

                var page = connection.Page(sigla, limit: Sample);

                foreach (var row in page.Rows)
                {
                    Console.WriteLine($"    {Render(row, page.Shape)}");
                }

                if (total > page.Rows.Count)
                {
                    Console.WriteLine($"    ... and {Count(total - page.Rows.Count)} more");
                }

                Console.WriteLine($"    {Count(total)} row(s) in {started.Elapsed.TotalSeconds:F2}s");
            }
            catch (FjordServerException failure)
            {
                Console.WriteLine($"    refused ({failure.Code}): {failure.ServerMessage.Split('\n')[0]}");
            }
        }
    }

    /// <summary>
    /// A row, named by the descriptor it came with.
    /// </summary>
    /// <remarks>
    /// A record is positional on the wire — the names are in the row descriptor the
    /// server sent once, at the head of the stream. Printing a row without them says
    /// <c>{5, 283}</c> for a line and a column and leaves a reader to guess which is
    /// which, and the two orders are both plausible.
    /// </remarks>
    private static string Render(FjordValue value, FjordType type) => (value, type) switch
    {
        (FjordValue.Int number, _) => number.Value.ToString(CultureInfo.InvariantCulture),
        (FjordValue.Str text, _) => $"\"{text.Value}\"",
        (FjordValue.Ref { Value: FjordRef.Id id }, _) => $"#{id.FactId >> 40}:{id.FactId & 0xFFFFFFFFFF}",

        (FjordValue.Record record, FjordType.Record shape)
            when record.Fields.Count == shape.Fields.Count =>
            "{" + string.Join(", ", record.Fields.Select((field, index) =>
                $"{shape.Fields[index].Name} = {Render(field, shape.Fields[index].Type)}")) + "}",

        _ => "?",
    };

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Megabytes(long bytes) =>
        (bytes / (1024.0 * 1024.0)).ToString("N1", CultureInfo.InvariantCulture);
}
