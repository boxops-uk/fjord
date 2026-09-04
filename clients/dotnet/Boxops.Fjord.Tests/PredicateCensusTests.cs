using System;
using System.Collections.Generic;
using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Every predicate this client declares, and whether anything writes it.</b>
/// </para>
/// <para>
/// A schema is a promise a consumer plans against, so a predicate that is declared and
/// empty is a dead feature in every UI that reads it — and nothing else in this suite can
/// see one. The layer gates write a fact of every shape by hand and prove the server
/// decodes it; the walk's tests assert the facts one fixture happens to need. Between them
/// sits the failure this exists for: four location predicates that had an id, a batch entry
/// and a declared type, and no emit site anywhere.
/// </para>
/// <para>
/// <b>An audit table as data, because "every predicate is non-empty" is not the claim.</b>
/// Some predicates cannot be filled by this producer at all, and a gate that asserted rows
/// for those would either be red or be weakened until it proved nothing. So each is
/// classified, and each is asserted to behave as classified: a predicate that stops being
/// written fails, and a predicate that starts being written where the table says it is not
/// fails too — the second half is what stops the table from drifting into a list of excuses
/// nobody rereads.
/// </para>
/// </summary>
public sealed class PredicateCensusTests
{
    /// <summary>How a predicate is expected to behave over the <c>census</c> fixture.</summary>
    private enum Fill
    {
        /// <summary>The fixture fills it, so the gate asserts rows.</summary>
        Written,

        /// <summary>
        /// Written for some runs and empty for others, and which is which is a property of
        /// the run rather than of the corpus — the reason says what decides it.
        /// </summary>
        /// <remarks>
        /// <b>Asserted over both cases, or it proves half of what it claims.</b> A
        /// classification checked only against the run that fills the predicate cannot tell
        /// "conditional" from "written", and one checked only against the run that leaves it
        /// empty cannot tell it from <see cref="Owed"/>. So
        /// <see cref="Every_predicate_behaves_as_the_audit_classifies_it"/> asserts rows over
        /// a solution run and
        /// <see cref="A_conditional_predicate_is_empty_over_the_run_its_condition_excludes"/>
        /// asserts none over a project-only one.
        /// </remarks>
        Conditional,

        /// <summary>This producer does not write it, and that is settled — the reason says why.</summary>
        Excused,

        /// <summary>It could be written and nothing writes it — the reason says what it needs.</summary>
        Owed,
    }

    /// <summary>
    /// The audit table: every predicate <see cref="DotnetIndex.Predicates"/> declares,
    /// classified, with the reason wherever the classification is not <see cref="Fill.Written"/>.
    /// </summary>
    /// <remarks>
    /// <b>A row here is a claim, not a note.</b> Moving one from <see cref="Fill.Owed"/> to
    /// <see cref="Fill.Written"/> is how a producer says it now fills a predicate, and the
    /// gate below refuses the claim unless the fixture proves it.
    /// </remarks>
    private static readonly (uint Predicate, Fill Fill, string Why)[] Audit =
    [
        // ---- src: every indexer fills this layer whatever language it reads ----------

        (DotnetIndex.File, Fill.Written, ""),
        (DotnetIndex.Symbol, Fill.Written, ""),
        (DotnetIndex.FileLanguage, Fill.Written, ""),
        (DotnetIndex.FileDigest, Fill.Written, ""),
        (DotnetIndex.FileOrigin, Fill.Written, ""),
        (DotnetIndex.FileInfo, Fill.Written, ""),
        (DotnetIndex.FileLine, Fill.Written, ""),
        (DotnetIndex.FileLineAt, Fill.Written, ""),
        (DotnetIndex.FileLineStyles, Fill.Written, ""),

        // ---- config -------------------------------------------------------------------

        (DotnetIndex.Setting, Fill.Written, ""),

        // ---- msbuild ------------------------------------------------------------------

        (DotnetIndex.Solution, Fill.Conditional,
            "the solution this index was built from, written when the run resolved one — "
            + "`--source` naming a `.slnx` or `.sln`, or a directory the loader picks one "
            + "out of. A run that fell through to a `.csproj`, or was handed one, writes "
            + "nothing: MSBuild's relationship is one-way, so there is nothing to resolve "
            + "from a project file and searching for a solution that lists it would be a "
            + "claim the build system does not make"),
        (DotnetIndex.Project, Fill.Written, ""),
        (DotnetIndex.Assembly, Fill.Written, ""),
        (DotnetIndex.Package, Fill.Written, ""),
        (DotnetIndex.SolutionToProject, Fill.Conditional,
            "one edge per project the solution lists that this index has an "
            + "`msbuild.Project` for, written with the solution and empty without one. A "
            + "listed project the index cannot key gets no edge and is named and counted "
            + "instead — `ProjectIndex.Unlinked`"),
        (DotnetIndex.ProjectToSolution, Fill.Conditional,
            "the same edge reversed, because neither direction is a seek from the other; "
            + "written and empty under the same condition"),
        (DotnetIndex.ProjectToSourceFile, Fill.Written, ""),
        (DotnetIndex.SourceFileToProject, Fill.Written, ""),
        (DotnetIndex.ProjectReference, Fill.Written, ""),
        (DotnetIndex.ProjectReferencedBy, Fill.Written, ""),
        (DotnetIndex.PackageReference, Fill.Written, ""),
        (DotnetIndex.PackageDependent, Fill.Written, ""),
        (DotnetIndex.Compilation, Fill.Written, ""),
        (DotnetIndex.ProjectCompilation, Fill.Written, ""),

        // ---- csharp -------------------------------------------------------------------

        (DotnetIndex.Name, Fill.Written, ""),
        (DotnetIndex.NameLowerCase, Fill.Written, ""),
        (DotnetIndex.Namespace, Fill.Written, ""),
        (DotnetIndex.FullName, Fill.Written, ""),
        (DotnetIndex.Class, Fill.Written, ""),
        (DotnetIndex.Interface, Fill.Written, ""),
        (DotnetIndex.Record, Fill.Written, ""),
        (DotnetIndex.Struct, Fill.Written, ""),
        (DotnetIndex.Implements, Fill.Written, ""),
        (DotnetIndex.TypeTypeParameter, Fill.Written, ""),
        (DotnetIndex.Method, Fill.Written, ""),
        (DotnetIndex.MethodParameter, Fill.Written, ""),
        (DotnetIndex.MethodTypeParameter, Fill.Written, ""),
        (DotnetIndex.Parameter, Fill.Written, ""),
        (DotnetIndex.Field, Fill.Written, ""),
        (DotnetIndex.TypeParameter, Fill.Written, ""),
        (DotnetIndex.Local, Fill.Excused,
            "deliberate: SCIP models a local as an occurrence ordinal that moves when the "
            + "file is edited, so a local gets no global name and no entity — "
            + "`codemarkup.FileLocalXRef` answers a file-local jump span to span instead"),
        (DotnetIndex.Property, Fill.Written, ""),
        (DotnetIndex.PropertyParameter, Fill.Written, ""),
        (DotnetIndex.ArrayType, Fill.Written, ""),
        (DotnetIndex.PointerType, Fill.Written, ""),
        (DotnetIndex.FunctionPointerType, Fill.Excused,
            "it cannot be keyed. `signature : Method` needs a `csharp.Method`, whose key "
            + "leads with a containing type, and a function pointer's signature symbol has "
            + "no containing type, namespace or symbol at all — so the member typed as one "
            + "is dropped and counted instead "
            + "(`A_function_pointer_in_a_signature_is_dropped_and_counted`)"),
        (DotnetIndex.DefinitionLocation, Fill.Written, ""),
        (DotnetIndex.ObjectCreationLocation, Fill.Written, ""),
        (DotnetIndex.MethodInvocationLocation, Fill.Written, ""),
        (DotnetIndex.MemberAccessLocation, Fill.Written, ""),
        (DotnetIndex.TypeLocation, Fill.Written, ""),
        (DotnetIndex.EntityXRef, Fill.Written, ""),
        (DotnetIndex.EntityRef, Fill.Written, ""),
        (DotnetIndex.SymbolOf, Fill.Written, ""),
        (DotnetIndex.DefinitionBySymbol, Fill.Written, ""),

        // ---- codemarkup: the surface a UI reads, and the reason this gate exists ------

        (DotnetIndex.MarkupDefinition, Fill.Written, ""),
        (DotnetIndex.SymbolInfo, Fill.Written, ""),
        (DotnetIndex.FileDefinition, Fill.Written, ""),
        (DotnetIndex.FileXRef, Fill.Written, ""),
        (DotnetIndex.SymbolXRef, Fill.Written, ""),
        (DotnetIndex.FileLocalXRef, Fill.Written, ""),
        (DotnetIndex.SearchEntry, Fill.Written, ""),
        (DotnetIndex.SymbolByName, Fill.Written, ""),
        (DotnetIndex.Relation, Fill.Written, ""),
        (DotnetIndex.RelationOf, Fill.Written, ""),
    ];

    /// <summary>
    /// <b>The table covers the schema, so a predicate cannot arrive unclassified.</b>
    /// </summary>
    /// <remarks>
    /// The gate below iterates the table, so a predicate missing from it would be a
    /// predicate nothing checked — which is the shape of the defect this class exists to
    /// catch, one level up. A reason is required wherever the classification is not
    /// <see cref="Fill.Written"/>: an empty predicate with no recorded reason is exactly
    /// what went wrong.
    /// </remarks>
    [Fact]
    public void Every_declared_predicate_is_classified_once_and_every_excuse_has_a_reason()
    {
        // By name rather than by id, because the failure has to be readable: a diff of
        // predicate numbers says `52` where a reader needs `csharp.TypeLocation`.
        //
        // **The population is the schema statement, not the id array.** `DotnetIndex.Predicates`
        // is kept by hand beside it, so a predicate declared in one and forgotten in the other
        // would escape this census — which is the defect this census exists to catch, one layer
        // up. The next assertion is what keeps the two the same set.
        Assert.Equal(
            [.. DotnetIndex.Schema.Predicates.Select(predicate => predicate.Name).Order(StringComparer.Ordinal)],
            Audit.Select(entry => DotnetIndex.NameOf(entry.Predicate)).Order(StringComparer.Ordinal));

        Assert.Equal(
            [.. Enumerable.Range(0, DotnetIndex.Schema.Predicates.Count).Select(id => (uint)id).Order()],
            DotnetIndex.Predicates.Order());

        Assert.Empty(Audit
            .Where(entry => (entry.Fill is Fill.Written) != (entry.Why.Length == 0))
            .Select(entry => DotnetIndex.NameOf(entry.Predicate)));
    }

    /// <summary>
    /// <b>Every predicate behaves as the table classifies it, over one real run.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counted from the database rather than from the sink, and that is not fussiness: a
    /// nested reference is interned by the server, so `csharp.FullName` has rows in the
    /// database and never appears at the write seam at all. A gate that counted what was
    /// queued would report it empty and be wrong.
    /// </para>
    /// <para>
    /// The whole `Program.Main` path, so the run is the one an operator gets — including
    /// the two switches that decide whether a predicate is written at all: `--repo` and
    /// `--revision` for `src.FileOrigin`, and `--styles` for `src.FileLineStyles`.
    /// </para>
    /// <para>
    /// <b><c>--source</c> is the solution</b>, which is what makes this the case a
    /// <see cref="Fill.Conditional"/> predicate is filled by. The other case is the test
    /// below, and both are needed: over this run alone, <c>Conditional</c> and
    /// <c>Written</c> are the same assertion.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_predicate_behaves_as_the_audit_classifies_it()
    {
        using var fixture = Fixture.Copy("census");
        using var server = FjordServer.Serving("census", "dotnet.sigla");

        var code = Program.Main([
            "--source", fixture.Path("Census.slnx"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//census",
            "--repo", "github.com/boxops-uk/fjord",
            "--revision", "census",
            "--styles",
            "--no-smoke",
        ]);

        Assert.Equal(0, code);

        using var connection = FjordConnection.Connect(server.Socket, "census", DotnetIndex.Schema);

        var wrong = new List<string>();

        foreach (var (predicate, fill, why) in Audit)
        {
            var name = DotnetIndex.NameOf(predicate);
            var rows = connection.Query($"X where X = {name} _").Rows.Count;

            if (fill is Fill.Written or Fill.Conditional && rows == 0)
            {
                wrong.Add($"{name}: classified {fill}, no rows — {why}");
            }

            // Exhaustive rather than two named variants: a sixth `Fill` added above and
            // forgotten here would escape both arms and be classified by nothing.
            if (fill is not (Fill.Written or Fill.Conditional) && rows > 0)
            {
                wrong.Add($"{name}: {rows} row(s), classified {fill} — {why}");
            }
        }

        // **Both directions, in one report.** A predicate that stopped being written and a
        // predicate that started being written where the table excuses it are the same
        // defect seen from either side — the producer and the audit disagree — and failing
        // on the first would hide however many of the second there are.
        Assert.True(
            wrong.Count == 0,
            $"{wrong.Count} predicate(s) do not behave as the audit table classifies them, "
            + $"after a run over the census fixture:\n  {string.Join("\n  ", wrong)}");
    }

    /// <summary>
    /// <b>A conditional predicate is empty over the run its condition excludes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of <see cref="Fill.Conditional"/>, and the half that makes the
    /// classification mean something: asserted only where it is filled, it is
    /// indistinguishable from <see cref="Fill.Written"/>, and a producer that had quietly
    /// started writing a solution fact for every run would pass.
    /// </para>
    /// <para>
    /// The same fixture, entered at one of its <c>.csproj</c> files, so the difference
    /// between the two runs is the entry point and nothing else. <c>msbuild.Project</c> is
    /// asserted non-empty first: a run that resolved nothing would report every predicate
    /// empty and satisfy this test for the wrong reason.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_conditional_predicate_is_empty_over_the_run_its_condition_excludes()
    {
        using var fixture = Fixture.Copy("census");
        using var server = FjordServer.Serving("project-only", "dotnet.sigla");

        var code = Program.Main([
            "--source", fixture.Path("Core", "Core.csproj"),
            "--root", fixture.Root,
            "--at", $"{server.Socket}//project-only",
            "--no-smoke",
        ]);

        Assert.Equal(0, code);

        using var connection = FjordConnection.Connect(
            server.Socket, "project-only", DotnetIndex.Schema);

        long Rows(uint predicate) =>
            connection.Query($"X where X = {DotnetIndex.NameOf(predicate)} _").Rows.Count;

        Assert.NotEqual(0, Rows(DotnetIndex.Project));

        var written = Audit
            .Where(entry => entry.Fill is Fill.Conditional)
            .Where(entry => Rows(entry.Predicate) > 0)
            .Select(entry => DotnetIndex.NameOf(entry.Predicate))
            .ToList();

        Assert.True(
            written.Count == 0,
            "a run given a `.csproj` resolved no solution, so every predicate classified "
            + $"Conditional must be empty — {string.Join(", ", written)} has rows");

        // The table's own claim about which predicates those are, so a classification
        // moved out of `Conditional` cannot leave this test asserting nothing.
        Assert.Equal(
            ["msbuild.ProjectToSolution", "msbuild.Solution", "msbuild.SolutionToProject"],
            Audit
                .Where(entry => entry.Fill is Fill.Conditional)
                .Select(entry => DotnetIndex.NameOf(entry.Predicate))
                .Order(StringComparer.Ordinal));
    }
}
