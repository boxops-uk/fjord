using System.Collections.Generic;
using System.Linq;

using Boxops.Fjord.Client;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The write seam, used by a consumer that has never heard of Roslyn.</b>
/// </para>
/// <para>
/// <b>The using list at the top of this file is the assertion.</b> There is no
/// <c>using Boxops.Fjord.Indexer</c> here and nothing below names a type from it: no
/// schema constants, no predicate ids, no producer helpers. If the seam still needed one,
/// this file would not compile — which is the only form of "usable from outside" that
/// cannot quietly stop being true.
/// </para>
/// <para>
/// It replaces a gate that rested on the Glean target riding the seam unmodified. That
/// target is retired, and it was carrying more than it looked: it was the run's only
/// existing external consumer, so the claim had something holding it up on the day the
/// seam landed rather than on the day a converter arrived.
/// </para>
/// </summary>
public sealed class WriteSeamTests
{
    /// <summary>
    /// A tiny schema of this test's own — two predicates, one nesting the other.
    /// </summary>
    /// <remarks>
    /// Declared here rather than imported, because importing a producer's schema is
    /// exactly the dependency this file exists to prove is gone. It is also the smallest
    /// schema that can show a nested reference, which is what makes a producer able to
    /// hold no fact ids.
    /// </remarks>
    private static FjordSchema Schema { get; } = new(
        [
            new FjordPredicate("code.File", FjordType.String, null),
            new FjordPredicate(
                "code.Decl",
                FjordType.Rec(("file", FjordType.Reference(0)), ("name", FjordType.String)),
                FjordType.Rec(("line", FjordType.Integer))),
        ],
        0x03678fcd1e7924e3);

    /// <summary>A target that keeps what it was handed, and says nothing was interned.</summary>
    private sealed class Recording : IBlockTarget
    {
        private readonly Lock _gate = new();

        public List<(uint Predicate, int Facts)> Blocks { get; } = [];

        public BlockWritten Write(uint predicate, IReadOnlyList<FjordFact> facts)
        {
            lock (_gate)
            {
                Blocks.Add((predicate, facts.Count));
            }

            return new BlockWritten(0, 0, 0);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// <b>A fact stream, batched by predicate and written, from outside the indexer.</b>
    /// </summary>
    [Fact]
    public void A_consumer_outside_the_indexer_can_write_a_fact_stream()
    {
        var target = new Recording();

        using (var sink = new FactSink(Schema, [target], batch: 4))
        {
            for (var n = 0; n < 10; n++)
            {
                var file = new FjordFact(0, FjordValue.Of($"src/F{n}.cs"));

                sink.Add(0, file);
                sink.Add(1, new FjordFact(
                    1,
                    FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), FjordValue.Of($"T{n}")),
                    FjordValue.Rec(FjordValue.Of((long)n))));
            }

            sink.Drain();
        }

        // Ten of each, in blocks of four: three blocks per predicate, the last short.
        Assert.Equal(20, target.Blocks.Sum(block => block.Facts));
        Assert.Equal(6, target.Blocks.Count);
        Assert.Equal([4, 4, 2], target.Blocks.Where(b => b.Predicate == 0).Select(b => b.Facts));
    }

    /// <summary>
    /// <b>A target that throws stops the run rather than being swallowed.</b>
    /// </summary>
    /// <remarks>
    /// The latched-failure rule, which is the part of this seam that was learned the
    /// expensive way: a producer that keeps filling blocks nobody is draining spends an
    /// hour reading as progress and writing nothing. A consumer taking the seam inherits
    /// the rule, so it is checked from out here too.
    /// </remarks>
    [Fact]
    public void A_target_that_fails_stops_the_producer()
    {
        var refused = new Refusing();

        var thrown = Assert.ThrowsAny<Exception>(() =>
        {
            using var sink = new FactSink(Schema, [refused], batch: 1);

            // More than one, because the failure reaches the producer at the next flush
            // rather than at the one that failed.
            for (var n = 0; n < 100; n++)
            {
                sink.Add(0, new FjordFact(0, FjordValue.Of($"src/F{n}.cs")));
            }

            sink.Drain();
        });

        // **Named, not just reported.** A database rejects a fact and can say which
        // predicate; it has never heard of the declaration behind it. Without the
        // predicate the answer to a multi-hour run that ended in a refusal is "one of
        // eighteen million facts", which is not an answer.
        Assert.Contains("code.File", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("refused a fact", thrown.InnerException!.Message, StringComparison.Ordinal);
    }

    /// <summary>A target that refuses everything, as a server rejecting a fact would.</summary>
    private sealed class Refusing : IBlockTarget
    {
        public BlockWritten Write(uint predicate, IReadOnlyList<FjordFact> facts) =>
            throw new InvalidOperationException(
                $"the database refused a fact of predicate {predicate}");

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// <b>A block written to a file is a block, and the sink says how many bytes.</b>
    /// </summary>
    /// <remarks>
    /// The other half of what a consumer gets for free: the same batching, encoded with
    /// the schema it was handed. A producer writing a file of blocks does not need a
    /// database to exist yet, which is what makes a converter an ingestion path rather
    /// than a second client.
    /// </remarks>
    [Fact]
    public void A_consumer_can_write_blocks_to_a_file_with_no_target_at_all()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fjord-seam-{Guid.NewGuid():N}.bin");

        try
        {
            long bytes;

            using (var sink = new FactSink(Schema, [], emit: path))
            {
                sink.Add(0, new FjordFact(0, FjordValue.Of("src/Only.cs")));
                sink.Drain();
                bytes = sink.Bytes;
            }

            Assert.True(bytes > 0, "nothing was encoded");
            Assert.Equal(bytes, new FileInfo(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
