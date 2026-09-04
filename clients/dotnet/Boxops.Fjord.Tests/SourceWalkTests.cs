using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>What the walk actually emits, taken at the write seam.</b>
/// </para>
/// <para>
/// <see cref="SourceLayerTests"/> covers the arithmetic; this covers the wiring, which
/// is the half that fails silently. A predicate the schema declares and nothing writes is
/// a name in a file, and a line table one row longer than the count beside it sends every
/// offset-past-the-end lookup to a line that does not exist — neither shows up in a build.
/// </para>
/// <para>
/// No server: <c>IBlockTarget</c> is the seam a target implements, so the facts are
/// recorded in memory and read back positionally, the way the codec sends them.
/// </para>
/// </summary>
public sealed class SourceWalkTests
{
    /// <summary>A target that keeps what it was handed.</summary>
    internal sealed class Recorder : IBlockTarget
    {
        private readonly Lock _gate = new();
        private readonly List<(uint Predicate, FjordFact Fact)> _facts = [];

        public IReadOnlyList<FjordFact> Of(uint predicate)
        {
            lock (_gate)
            {
                return [.. _facts.Where(entry => entry.Predicate == predicate).Select(entry => entry.Fact)];
            }
        }

        public BlockWritten Write(uint predicate, IReadOnlyList<FjordFact> facts)
        {
            lock (_gate)
            {
                foreach (var fact in facts)
                {
                    _facts.Add((predicate, fact));
                }
            }

            return new BlockWritten(0, 0, 0);
        }

        public void Dispose()
        {
        }
    }

    private static long Int(FjordValue value) => Assert.IsType<FjordValue.Int>(value).Value;

    private static string Str(FjordValue value) => Assert.IsType<FjordValue.Str>(value).Value;

    private static IReadOnlyList<FjordValue> Fields(FjordValue? value) =>
        Assert.IsType<FjordValue.Record>(value).Fields;

    /// <summary>Walk one file's source through the real indexer and keep what it wrote.</summary>
    internal static Recorder Walk(
        string source,
        bool lines = true,
        string? repo = null,
        string? revision = null) =>
        Walked(source, lines, repo, revision).Recorder;

    /// <summary>The same walk, keeping the tallies the run reports beside the facts.</summary>
    internal static (Recorder Recorder, Boxops.Fjord.Indexer.Indexer Indexer) Walked(
        string source,
        bool lines = true,
        string? repo = null,
        string? revision = null)
    {
        var directory = Directory.CreateTempSubdirectory("fjord-source-walk");
        try
        {
            var path = Path.Combine(directory.FullName, "A.cs");
            File.WriteAllText(path, source);

            var options = new Options
            {
                Source = directory.FullName,
                Lines = lines,
                Repo = repo,
                Revision = revision,
            };
            var projects = ProjectIndex.Build(directory.FullName, directory.FullName, [], TextWriter.Null);
            var recorder = new Recorder();

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            var compilation = CSharpCompilation.Create(
                "Walked",
                [tree],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

            // Fully qualified: from `Boxops.Fjord.Tests`, the bare name `Indexer`
            // resolves to the sibling *namespace* rather than the type in it.
            Boxops.Fjord.Indexer.Indexer indexer;

            using (var sink = new FactSink(DotnetIndex.Schema, [recorder]))
            {
                indexer = new Boxops.Fjord.Indexer.Indexer(
                    options, sink, directory.FullName, projects);
                indexer.Index(compilation, null);
                sink.Drain();
            }

            return (recorder, indexer);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// **A declaration this layer cannot express is counted, not lost in silence.**
    /// `csharp` has no event entity, and both event forms reach the walk — so `Declare`
    /// writes nothing for one, which is correct, and the run's own tally is the only
    /// thing that can say so. A zero here is an index whose cross-references point at
    /// definitions that were never written, reading as a clean run.
    /// </summary>
    [Fact]
    public void An_event_declaration_this_layer_cannot_express_is_counted()
    {
        var (written, indexer) = Walked("""
            namespace Fixture
            {
                public delegate void Handler();

                public class Thing
                {
                    public event Handler Changed;

                    public event Handler Renamed { add { } remove { } }
                }
            }
            """);

        // The field-like form and the accessor form, both of them.
        Assert.Equal(2, indexer.Inexpressible);

        var named = written.Of(DotnetIndex.SymbolByName)
            .Select(fact => Str(Fields(fact.Key)[0]))
            .ToList();

        Assert.Contains("Thing", named);
        Assert.DoesNotContain("Changed", named);
        Assert.DoesNotContain("Renamed", named);
    }

    /// <summary>
    /// **The run says which cause dropped a declaration, not just how many.**
    /// The counter has two causes — a type with no `csharp.AType` alternative, and an
    /// event, which this layer has no entity for at all — and the whole point of counting
    /// rather than dropping in silence is that somebody can act on the number. A line
    /// naming `dynamic` over a checkout that contains none sends them looking for it.
    /// </summary>
    [Fact]
    public void An_event_is_reported_as_an_event_and_not_as_a_dynamic()
    {
        var (_, indexer) = Walked("""
            namespace Fixture
            {
                public delegate void Handler();

                public class Thing
                {
                    public event Handler Changed;
                }
            }
            """);

        var dropped = Assert.Single(Program.Dropped(indexer));

        Assert.Contains("event", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("dynamic", dropped, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The other cause still reads as itself.** The census for the test above, which a
    /// report that never names `dynamic` at all would satisfy.
    /// </summary>
    [Fact]
    public void A_dynamic_signature_is_reported_as_a_type_the_layer_cannot_express()
    {
        var (_, indexer) = Walked("""
            namespace Fixture
            {
                public class Thing
                {
                    public dynamic Loose(dynamic value) => value;
                }
            }
            """);

        var dropped = Assert.Single(Program.Dropped(indexer));

        Assert.Contains("dynamic", dropped, StringComparison.Ordinal);
        Assert.DoesNotContain("event", dropped, StringComparison.Ordinal);
    }

    /// <summary>
    /// **A constraint keyword is not a name the compiler failed to bind.**
    /// `Unresolved` is read as a health signal — the count of names a broken workspace
    /// cost the index — so a `where` clause spelling a constraint as an identifier must
    /// not land in it. There is no symbol to resolve, and reporting one says an indexing
    /// failure happened where none did.
    /// </summary>
    [Fact]
    public void A_constraint_keyword_is_not_an_unresolved_name()
    {
        var (_, indexer) = Walked("""
            namespace Fixture
            {
                public interface IFace
                {
                }

                public static class Bounds
                {
                    public static string NotNull<T>(T value)
                        where T : notnull => value.ToString() ?? string.Empty;

                    public static string Unmanaged<T>(T value)
                        where T : unmanaged => value.ToString() ?? string.Empty;

                    public static string Faced<T>(T value)
                        where T : IFace => value.ToString() ?? string.Empty;
                }
            }
            """);

        Assert.Equal(0, indexer.Unresolved);

        // The interface constraint is a real type reference and stays one: an exclusion
        // drawn around `where` clauses rather than around the two keywords would drop
        // every declared bound out of the index and still report zero unresolved. Five
        // bindable names in each of the first two methods — `T` twice, `value`,
        // `ToString`, `Empty` — and six in the third, which also names `IFace`.
        Assert.Equal(16, indexer.References);
    }

    /// <summary>
    /// **A name that really does not bind is still counted.**
    /// The census for the exclusion above, which a counter that excluded everything would
    /// satisfy. Both keywords are here beside one identifier nothing declares, so the
    /// one is the miss and not the constraints.
    /// </summary>
    [Fact]
    public void A_name_the_compiler_cannot_bind_is_counted()
    {
        var (_, indexer) = Walked("""
            namespace Fixture
            {
                public static class Bounds
                {
                    public static string NotNull<T>(T value)
                        where T : notnull => value.ToString() ?? string.Empty;

                    public static string Unmanaged<T>(T value)
                        where T : unmanaged => value.ToString() ?? string.Empty;

                    public static object Loose() => nope;
                }
            }
            """);

        Assert.Equal(1, indexer.Unresolved);
    }

    /// <summary>
    /// **The phantom line, at the seam.** Roslyn ends a newline-terminated file with an
    /// empty line, so the walk used to write a fact for a line that `src.sigla` says is
    /// not there — in nearly every file of every index.
    /// </summary>
    [Fact]
    public void The_line_table_has_no_row_for_the_terminator_at_the_end_of_a_file()
    {
        var written = Walk("class A\n{\n}\n");

        var lines = written.Of(DotnetIndex.FileLine);
        Assert.Equal(["class A", "{", "}"], lines.Select(fact => Str(Fields(fact.Value)[0])));
    }

    /// <summary>
    /// **`FileInfo.lines` is the number of rows**, which is the agreement a consumer
    /// relies on when an offset resolves past the last line's start.
    /// </summary>
    [Fact]
    public void The_summary_agrees_with_the_table_it_summarises()
    {
        var written = Walk("class A\n{\n}\n");

        var info = Assert.Single(written.Of(DotnetIndex.FileInfo));
        var fields = Fields(info.Value);

        Assert.Equal(12, Int(fields[0]));
        Assert.Equal(written.Of(DotnetIndex.FileLine).Count, Int(fields[1]));
        // `endsInNewline`: `false_ = 0 | true_ = 1`.
        Assert.Equal(1u, Assert.IsType<FjordValue.Union>(fields[2]).Disc);
    }

    [Fact]
    public void A_file_with_no_final_terminator_says_so()
    {
        var written = Walk("class A\n{\n}");

        var info = Assert.Single(written.Of(DotnetIndex.FileInfo));
        Assert.Equal(0u, Assert.IsType<FjordValue.Union>(Fields(info.Value)[2]).Disc);
        Assert.Equal(3, written.Of(DotnetIndex.FileLine).Count);
    }

    /// <summary>
    /// **`FileLineAt` is written from the same rows**, so the reverse lookup answers for
    /// every line rather than for the ones a second pass happened to reach.
    /// </summary>
    [Fact]
    public void Every_line_has_the_offset_fact_that_inverts_it()
    {
        var written = Walk("class A\n{\n    // \U0001F600\n}\n");

        var lines = written.Of(DotnetIndex.FileLine);
        var at = written.Of(DotnetIndex.FileLineAt);

        Assert.Equal(lines.Count, at.Count);

        // `FileLine` is keyed {file, line} with `start` on the value; `FileLineAt` is
        // keyed {file, start, line}, all key. The pairs must agree.
        var byLine = lines.ToDictionary(
            fact => Int(Fields(fact.Key)[1]),
            fact => Int(Fields(fact.Value)[1]));

        foreach (var fact in at)
        {
            var key = Fields(fact.Key);
            Assert.Null(fact.Value);
            Assert.Equal(byLine[Int(key[2])], Int(key[1]));
        }
    }

    /// <summary>
    /// **`--no-lines` drops the per-line facts and keeps the per-file ones.** The switch
    /// is about the size of the line table; a file's length, language and digest are one
    /// fact each.
    /// </summary>
    [Fact]
    public void Without_the_line_table_the_per_file_facts_are_still_written()
    {
        var written = Walk("class A\n{\n}\n", lines: false);

        Assert.Single(written.Of(DotnetIndex.FileInfo));
        Assert.Single(written.Of(DotnetIndex.FileLanguage));
        Assert.Single(written.Of(DotnetIndex.FileDigest));
        Assert.Empty(written.Of(DotnetIndex.FileLine));
        Assert.Empty(written.Of(DotnetIndex.FileLineAt));
    }

    /// <summary>
    /// **The language is resolved to a discriminant, not written as a string.** A C# file
    /// is alternative 1 of `src.Language`, and reaching the `other` valve here would mean
    /// the extension table and the vocabulary had drifted apart.
    /// </summary>
    [Fact]
    public void A_walked_file_is_the_language_its_extension_names()
    {
        var written = Walk("class A\n{\n}\n");

        var language = Assert.Single(written.Of(DotnetIndex.FileLanguage));
        var alternative = Assert.IsType<FjordValue.Union>(Fields(language.Value)[0]);

        Assert.Equal((uint)Array.IndexOf(DotnetIndex.LanguageNames, "csharp") + 1, alternative.Disc);
        Assert.Empty(Assert.IsType<FjordValue.Record>(alternative.Value).Fields);
    }

    /// <summary>
    /// **Provenance is written only where a run states it.** A file's origin is not in the
    /// code, and an index that guessed one would be asserting something it cannot know.
    /// </summary>
    [Fact]
    public void A_file_carries_its_origin_when_the_run_states_one()
    {
        var written = Walk(
            "class A\n{\n}\n",
            repo: "github.com/boxops-uk/fjord",
            revision: "3fa4961");

        var origin = Assert.Single(written.Of(DotnetIndex.FileOrigin));
        var fields = Fields(origin.Value);

        Assert.Equal("github.com/boxops-uk/fjord", Str(fields[0]));
        Assert.Equal("3fa4961", Str(fields[1]));
    }

    [Fact]
    public void A_run_that_states_no_provenance_writes_none()
    {
        Assert.Empty(Walk("class A\n{\n}\n").Of(DotnetIndex.FileOrigin));
    }

    /// <summary>
    /// **A declaration gets its global name.** `src.Symbol` is the key a cross-database
    /// fan-out seeks, and it is written by the walk rather than derived later — so the gate
    /// is that a walked declaration has one, spelled the way `ScipSymbols` says.
    /// </summary>
    [Fact]
    public void A_walked_declaration_carries_its_scip_symbol()
    {
        var written = Walk(
            """
            namespace N.Deep
            {
                public class T
                {
                    public void M() {}
                    public void M(int a) {}
                }
            }
            """);

        var symbols = written.Of(DotnetIndex.Symbol)
            .Select(fact => Assert.IsType<FjordValue.Str>(fact.Key).Value)
            .ToList();

        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/Deep/T#", symbols);
        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/Deep/T#M().", symbols);
        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/Deep/T#M(+1).", symbols);
    }

    /// <summary>
    /// **The digest is over the text the offsets count**, which is the thing a second
    /// implementation would most easily get wrong — and it is one fact per file, so a
    /// walk that wrote none would look exactly like a walk that wrote them all.
    /// </summary>
    [Fact]
    public void A_walked_file_carries_the_digest_of_its_own_text()
    {
        const string Source = "class A\n{\n}\n";
        var written = Walk(Source);

        var digest = Assert.Single(written.Of(DotnetIndex.FileDigest));

        Assert.Equal(
            SourceLayer.Digest(Microsoft.CodeAnalysis.Text.SourceText.From(Source)),
            Str(Fields(digest.Value)[0]));
    }

    /// <summary>
    /// <b>A file outside the index root is not indexed under a name that climbs out.</b>
    /// </summary>
    /// <remarks>
    /// A compilation's source list is MSBuild's, not the walk's, and it reaches wherever
    /// the project points — a test project referencing a package with source in it puts
    /// files from the NuGet cache on the list. Named relative to the root, those come back
    /// as <c>../../../.nuget/packages/…</c>, which depends on where the root happens to be:
    /// two runs of one repository disagree about it, and nothing downstream can open it.
    /// </remarks>
    [Fact]
    public void A_file_outside_the_root_is_not_given_a_name_that_climbs_out()
    {
        var directory = Directory.CreateTempSubdirectory("fjord-outside");

        try
        {
            var inside = Path.Combine(directory.FullName, "root");
            Directory.CreateDirectory(inside);

            var here = Path.Combine(inside, "Here.cs");
            var elsewhere = Path.Combine(directory.FullName, "Elsewhere.cs");

            File.WriteAllText(here, "namespace N;\n\npublic class Here { }\n");
            File.WriteAllText(elsewhere, "namespace N;\n\npublic class Elsewhere { }\n");

            var options = new Options { Source = inside };
            var projects = ProjectIndex.Build(inside, inside, [], TextWriter.Null);
            var recorder = new Recorder();

            var compilation = CSharpCompilation.Create(
                "Walked",
                [
                    CSharpSyntaxTree.ParseText(File.ReadAllText(here), path: here),
                    CSharpSyntaxTree.ParseText(File.ReadAllText(elsewhere), path: elsewhere),
                ],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

            using (var sink = new FactSink(DotnetIndex.Schema, [recorder]))
            {
                new Boxops.Fjord.Indexer.Indexer(options, sink, inside, projects)
                    .Index(compilation, null);
                sink.Drain();
            }

            var files = recorder.Of(DotnetIndex.File)
                .Select(fact => Assert.IsType<FjordValue.Str>(fact.Key).Value)
                .ToList();

            Assert.Equal(["Here.cs"], files);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
