using System.Collections.Generic;
using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The walk has no gate, and the index does not depend on how many threads walked it.</b>
/// </para>
/// <para>
/// The producer used to hold one lock around everything downstream of a symbol — the
/// memos, the counters, the sink — so eight walker threads took turns to write. Removing
/// it is worth nothing if the facts move: a sealed database's identity is a hash over the
/// facts it holds, so an index that differs between <c>--jobs 1</c> and <c>--jobs 8</c> is
/// an index whose identity depends on the machine that built it.
/// </para>
/// <para>
/// So the gate is the facts themselves, encoded with the same codec the server is sent —
/// compared as a multiset, because the *order* is expected to move and nothing downstream
/// reads it.
/// </para>
/// </summary>
public sealed class ConcurrentWalkTests
{
    /// <summary>
    /// A corpus with enough in it to keep several threads busy, and enough sharing to make
    /// them collide.
    /// </summary>
    /// <remarks>
    /// Every file names types from every other, so the entity memo, the name memo and the
    /// file memo are all reached concurrently for the same keys — which is where a
    /// published-too-early memo or a cycle broken across threads would show up as a fact
    /// of the wrong depth.
    /// </remarks>
    private static string[] Corpus(int files) =>
    [
        .. Enumerable.Range(0, files).Select(n => $$"""
            namespace Fixture.Shared;

            public interface IThing{{n}}
            {
                string Describe();
            }

            public class Thing{{n}} : IThing{{n}}
            {
                private readonly Helper _helper = new();

                public string Name { get; set; } = "thing{{n}}";

                public string Describe() => _helper.Help(Name) + typeof(Thing0).Name;

                public T Echo<T>(T value) where T : notnull => value;
            }
            """),
        """
        namespace Fixture.Shared;

        public class Helper
        {
            public string Help(string what) => what.ToUpperInvariant();
        }

        // A type constrained by itself: the shape whose entity graph is a cycle, and the
        // reason the in-progress set has to be per thread rather than shared.
        public class Recursive<T> where T : Recursive<T>
        {
            public T? Self { get; init; }
        }
        """,
    ];

    /// <summary>Every fact the walk wrote, encoded as the server would be sent it.</summary>
    /// <remarks>
    /// The real codec rather than a rendering written for the test: these are the bytes a
    /// sealed identity is a hash over, so two runs that agree here agree about the
    /// database.
    /// </remarks>
    private static List<string> Encoded(SourceWalkTests.Recorder recorder)
    {
        var encoded = new List<string>();

        foreach (var predicate in DotnetIndex.Predicates)
        {
            foreach (var fact in recorder.Of(predicate))
            {
                var buffer = new ByteBuffer();
                ValueCodec.WriteFact(buffer, DotnetIndex.Schema, fact);
                encoded.Add($"{predicate}:{Convert.ToHexString(buffer.Span)}");
            }
        }

        encoded.Sort(StringComparer.Ordinal);
        return encoded;
    }

    /// <summary>Walk one corpus at a stated degree of parallelism.</summary>
    private static SourceWalkTests.Recorder Walk(int jobs)
    {
        var directory = Directory.CreateTempSubdirectory("fjord-concurrent-walk");

        try
        {
            var sources = Corpus(24);
            var trees = new List<SyntaxTree>();

            for (var n = 0; n < sources.Length; n++)
            {
                var path = Path.Combine(directory.FullName, $"F{n}.cs");
                File.WriteAllText(path, sources[n]);
                trees.Add(CSharpSyntaxTree.ParseText(sources[n], path: path));
            }

            var options = new Options { Source = directory.FullName, Jobs = jobs };
            var projects = ProjectIndex.Build(
                directory.FullName, directory.FullName, [], TextWriter.Null);
            var recorder = new SourceWalkTests.Recorder();

            var compilation = CSharpCompilation.Create(
                "Walked",
                trees,
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

            using (var sink = new FactSink(options, [recorder]))
            {
                new Boxops.Fjord.Indexer.Indexer(options, sink, directory.FullName, projects)
                    .Index(compilation, null);
                sink.Drain();
            }

            return recorder;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// <b>Eight threads write the index one thread writes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the sealed-identity claim, taken at the seam instead of through a server: a
    /// database's identity is a hash over its facts, so identical facts are an identical
    /// identity and a difference here would be one there.
    /// </para>
    /// <para>
    /// <b>Twice at eight, and both compared with one.</b> A single comparison of a
    /// concurrent run against a serial one is a coin toss dressed as a test — the failure
    /// this guards against is a race, and a race that loses once in three runs passes a
    /// gate that runs it once.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_facts_do_not_depend_on_how_many_threads_walked()
    {
        var serial = Encoded(Walk(jobs: 1));

        Assert.NotEmpty(serial);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            Assert.Equal(serial, Encoded(Walk(jobs: 8)));
        }
    }

    /// <summary>
    /// <b>And the counters agree too, which the facts alone would not say.</b>
    /// </summary>
    /// <remarks>
    /// A counter incremented with <c>++</c> from several threads loses updates silently,
    /// and a fact total that is quietly low is indistinguishable from a smaller
    /// repository. The facts are written through a sink that would still hold them all;
    /// only the counts would be wrong.
    /// </remarks>
    [Fact]
    public void The_counts_do_not_depend_on_how_many_threads_walked()
    {
        static (int Files, long Facts) Counted(int jobs)
        {
            var recorder = Walk(jobs);
            var total = DotnetIndex.Predicates.Sum(predicate => (long)recorder.Of(predicate).Count);

            return (recorder.Of(DotnetIndex.File).Count, total);
        }

        var serial = Counted(1);

        Assert.True(serial.Files >= 25, $"the corpus is {serial.Files} files");
        Assert.Equal(serial, Counted(8));
        Assert.Equal(serial, Counted(8));
    }
}
