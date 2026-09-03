using System.Diagnostics;

using Boxops.Fjord.Client;

namespace Boxops.Fjord.Scip;

/// <summary>
/// <c>scip2fjord</c> — a SCIP index into a Fjord database, or into a file of blocks.
/// </summary>
/// <remarks>
/// <para>
/// <b>No per-language indexer, which is the claim.</b> Every language with a SCIP indexer
/// — TypeScript, Java, Scala, Rust, Python, Go, Ruby — reaches a Fjord database through
/// this one program, and none of them needs anything written here. What "no per-language
/// work" does not mean is "no work": positions have to be converted, kinds projected, and
/// a name recovered where the index gives none.
/// </para>
/// <para>
/// <b>It writes blocks, so it is a file ingester for free.</b> `--emit` produces exactly
/// what the .NET indexer's does — a run of encoded blocks — which is the format the read
/// side ingests when it lands. A converter that needed a live database would be a second
/// client instead of an ingestion path.
/// </para>
/// </remarks>
internal static class Program
{
    private const string Usage = """
        scip2fjord — load a SCIP index into a Fjord database

          --input <path>        the .scip index to read (required)
          --root <path>         where the indexed files are, for documents whose text the
                                index does not carry (default: the index's directory)
          --at <address>        where to write: [where//]name (default: none — see --emit)
          --emit <path>         write every block to a file as well
          --batch <n>           facts per block (default: 4096)
          --producer <name>     what config.Setting {dimension = "producer"} says
                                (default: scip2fjord/<version>)
          --help
        """;

    public static int Main(string[] argv)
    {
        string? input = null, root = null, at = null, emit = null, producer = null;
        var batch = 4096;

        for (var n = 0; n < argv.Length; n++)
        {
            string Value() => n + 1 < argv.Length
                ? argv[++n]
                : throw new FormatException($"`{argv[n]}` wants a value");

            try
            {
                switch (argv[n])
                {
                    case "--input": input = Value(); break;
                    case "--root": root = Value(); break;
                    case "--at": at = Value(); break;
                    case "--emit": emit = Value(); break;
                    case "--producer": producer = Value(); break;
                    case "--batch": batch = int.Parse(Value()); break;

                    case "--help" or "-h":
                        Console.WriteLine(Usage);
                        return 0;

                    default:
                        Console.Error.WriteLine($"unknown flag `{argv[n]}`\n\n{Usage}");
                        return 2;
                }
            }
            catch (FormatException failure)
            {
                Console.Error.WriteLine(failure.Message);
                return 2;
            }
        }

        if (input is null)
        {
            Console.Error.WriteLine($"--input is required\n\n{Usage}");
            return 2;
        }

        if (at is null && emit is null)
        {
            Console.Error.WriteLine(
                "nothing to write to: give --at, --emit, or both");
            return 2;
        }

        try
        {
            return Run(input, root, at, emit, batch, producer);
        }
        catch (Exception failure) when (failure is IOException or FormatException
            or InvalidOperationException)
        {
            Console.Error.WriteLine($"could not convert {input}: {failure.Message}");
            return 1;
        }
    }

    private static int Run(
        string input, string? root, string? at, string? emit, int batch, string? producer)
    {
        var bytes = File.ReadAllBytes(input);
        var reading = Stopwatch.StartNew();
        var index = ScipIndex.Read(bytes);
        reading.Stop();

        Console.WriteLine($"read {input}");
        Console.WriteLine($"  {index.Documents.Count} document(s), "
            + $"{index.Documents.Sum(document => document.Occurrences.Count)} occurrence(s), "
            + $"in {reading.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"  schema {ScipFacts.Schema.Fingerprint:x16}");

        var where = root ?? Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".";

        FjordConnection? connection = null;
        var targets = new List<IBlockTarget>();

        try
        {
            if (at is not null)
            {
                var address = FjordAddress.Parse(at);
                connection = FjordConnection.Connect(
                    address.SocketPath ?? throw new InvalidOperationException(
                        "only a socket address is supported here"),
                    address.Database,
                    ScipFacts.Schema);

                Console.WriteLine($"  connected: protocol {connection.Hello.Version}, "
                    + $"{connection.Hello.Predicates} predicates, "
                    + $"schema {connection.Hello.SchemaFingerprint:x16}");

                targets.Add(new FjordTarget(connection));
            }

            var walking = Stopwatch.StartNew();
            var written = 0;

            using (var sink = new FactSink(ScipFacts.Schema, targets, batch, emit))
            {
                var converter = new Converter(sink, where, Console.Out);

                converter.Settings(
                    index,
                    producer ?? $"scip2fjord/{typeof(Program).Assembly.GetName().Version}");

                foreach (var document in index.Documents)
                {
                    written += converter.Convert(document);
                }

                sink.Drain();
                walking.Stop();

                Console.WriteLine();
                Console.WriteLine($"converted {written} fact(s) in {sink.Blocks} block(s), "
                    + $"{walking.Elapsed.TotalSeconds:F1}s");

                if (connection is not null)
                {
                    Console.WriteLine($"  server {sink.Created} created, {sink.Deduped} deduped");
                }
            }

            return 0;
        }
        finally
        {
            foreach (var target in targets)
            {
                target.Dispose();
            }

            connection?.Dispose();
        }
    }
}
