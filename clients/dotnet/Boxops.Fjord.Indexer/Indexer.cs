using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Boxops.Fjord.Client;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// The walk: a compilation in, facts out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is a symbol question, not a syntax question.</b> A name in C#
/// means whatever the compiler says it means — an extension method invoked as an
/// instance method, a partial class continued in another file, a member reached through
/// a type inferred from a lambda's parameter. Roslyn has already answered all of it, so
/// this walk asks rather than guesses; that is the difference between this indexer and
/// <c>example/index.py</c>, which is honest about stopping at the line where types
/// would be needed.
/// </para>
/// <para>
/// <b>One function decides what a declaration is.</b> <see cref="CsharpEntities.Entity"/>
/// maps a symbol to the entity fact that names it, and both paths go through it: the walk
/// that emits declarations, and the reference that points at one. They cannot disagree,
/// because there is nothing for them to disagree with — the reference nests the very fact
/// the declaration emitted.
/// </para>
/// <para>
/// <b>No fact ids anywhere.</b> A reference carries its target inline and the server
/// interns it. So this class keeps no book of what the server has called things, and
/// the memoisation below is an optimisation rather than a correctness requirement:
/// forget all of it and the same index comes out, more slowly.
/// </para>
/// </remarks>
internal sealed class Indexer(Options options, FactSink sink, string root, ProjectIndex projects)
{
    /// <summary>Path to its `src.File` fact, and whether this run has emitted it.</summary>
    private readonly ConcurrentDictionary<string, FjordFact> _files = new(StringComparer.Ordinal);

    /// <summary>Files already walked — the same file is often in two projects.</summary>
    /// <remarks>
    /// Not concurrent, and it does not need to be: which files a project contributes is
    /// decided in <see cref="Index"/> before any thread is started, which is what keeps
    /// <c>--max-files 2000</c> the same two thousand files on every run.
    /// </remarks>
    private readonly HashSet<string> _walked = new(StringComparer.Ordinal);

    /// <summary>
    /// Symbol to declaration, for one compilation.
    /// </summary>
    /// <remarks>
    /// Per compilation because symbols are: the same type in two projects is two
    /// symbols, and <see cref="SymbolEqualityComparer"/> will say so. The facts they
    /// produce are identical, which is what makes it safe to throw this away between
    /// projects and what makes the duplicates dedup on the way in.
    /// </remarks>
    private CsharpEntities _entities = new((_, _) => { });

    // **Interlocked, and read as projections.** Every one of these is incremented from
    // whichever walker thread reached the thing being counted; a `++` on a shared int is
    // the classic lost update, and a fact count that is quietly low is a measurement
    // nobody can tell from a smaller repository.
    private int _files_, _declarations, _references, _external, _unresolved, _unattributed;
    private int _unspellable, _referenceAssemblies;
    private long _lines, _styled;

    // **Not interlocked, unlike the counters above, because `Index` is not concurrent.**
    // Files inside one project are walked in parallel; the projects themselves are handed
    // over one at a time, which is what makes "the first one wins" a fact about the
    // solution's order rather than about which thread got there first.
    private readonly HashSet<string> _assemblies = [];
    private readonly List<string> _duplicates = [];

    public int Files => Volatile.Read(ref _files_);

    /// <summary>Compilations left unwalked because they are reference assemblies.</summary>
    public int ReferenceAssemblies => Volatile.Read(ref _referenceAssemblies);

    /// <summary>
    /// Projects left unwalked because another project already produced their assembly,
    /// by the path each was declared at.
    /// </summary>
    /// <remarks>
    /// <b>Which one is kept is the order projects are walked in</b>, and that order is the
    /// solution's — chosen sequentially, like the files inside one, so two runs over a
    /// checkout leave out the same project. Arbitrary between the two and stable across
    /// runs is the most a producer can offer here: nothing in a build graph says which
    /// implementation of an assembly a reader meant.
    /// </remarks>
    public IReadOnlyList<string> DuplicateAssemblies => _duplicates;

    public int Declarations => Volatile.Read(ref _declarations);

    public int References => Volatile.Read(ref _references);

    /// <summary>References to something declared outside the index — the BCL, a package.</summary>
    public int External => Volatile.Read(ref _external);

    /// <summary>Names the compiler could not bind at all: missing references, broken code.</summary>
    public int Unresolved => Volatile.Read(ref _unresolved);

    /// <summary>
    /// Symbols this producer could not spell a <c>src.Symbol</c> for, counted rather than
    /// thrown on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not the same thing as a symbol with no global name.</b> A local, a lambda and a
    /// range variable have none by decision and there are as many of them as the checkout
    /// has; this counts a shape <see cref="ScipSymbols"/> anticipated and could not place —
    /// a member whose position among its type's same-named siblings it could not find, or
    /// a kind it has no descriptor arm for.
    /// </para>
    /// <para>
    /// <b>All three sites that ask for a spelling count, and they lose different
    /// things.</b> A <i>declaration</i> keeps its <c>csharp</c> entity and its span and
    /// loses the cross-database name. A <i>reference</i> keeps <c>EntityXRef</c> on the
    /// <c>csharp</c> layer and loses the <c>codemarkup</c> occurrence find-references
    /// reads — unless its target is declared in this same file, where <c>FileLocalXRef</c>
    /// answers it span to span regardless. A <i>relation edge</i> is lost whole, because a
    /// <c>Relation</c> row is a pair of symbols and half of one is no edge. So this is a
    /// count of spellings attempted and not of distinct symbols: one unspellable
    /// declaration referred to ten times reports eleven. None of the three throws, which
    /// is what makes a run printing a number here one somebody can fix rather than one
    /// that died.
    /// </para>
    /// <para>
    /// <b>Nothing this walk reaches provokes it today, and that is the claim rather than
    /// the excuse.</b> <c>LedgerTests</c> asserts the zero, and
    /// <c>ScipSymbolsTests.A_symbol_this_producer_cannot_spell_is_no_symbol_rather_than_an_exception</c>
    /// provokes the state itself with a built-in operator — a symbol a semantic model
    /// hands out that no walk here collects. Two rounds of this change asserted the same
    /// emptiness as an exception and were wrong about an everyday partial member, so the
    /// zero is a counter and not a throw.
    /// </para>
    /// </remarks>
    public int Unspellable => Volatile.Read(ref _unspellable);

    /// <summary>
    /// Types the layer cannot express — `dynamic`, a function pointer, an unresolved name.
    /// </summary>
    /// <remarks>
    /// `csharp.AType` has no alternative for a `dynamic` or an error type, and a function
    /// pointer's signature cannot be keyed as a `csharp.Method` — and the union sits in the
    /// key of `Method`, `Field` and `Parameter`, so the declaration is dropped rather than
    /// recorded under a fabricated type. Counted because a layer that silently loses
    /// declarations is worse than one that says how many.
    /// </remarks>
    public int InexpressibleTypes => _entities.InexpressibleTypes;

    /// <summary>Declarations of a kind with no `csharp` entity at all — an event.</summary>
    public int InexpressibleKinds => _entities.InexpressibleKinds;

    /// <summary>Declarations dropped for either reason.</summary>
    /// <remarks>
    /// The two causes are reported apart, because a run that says <c>dynamic</c> over a
    /// checkout containing none costs somebody a search for it.
    /// </remarks>
    public int Inexpressible => _entities.Inexpressible;

    /// <summary>Lines of source written as <c>src.FileLine</c> facts.</summary>
    public long Lines => Interlocked.Read(ref _lines);

    /// <summary>Lines carrying syntax highlighting, as <c>src.FileLineStyles</c> facts.</summary>
    public long Styled => Interlocked.Read(ref _styled);

    /// <summary>Files no project compiles — shared source, or a checkout with no project files.</summary>
    public int Unattributed => Volatile.Read(ref _unattributed);

    /// <summary>
    /// The most-referenced declaration's short name — something to query for.
    /// </summary>
    /// <remarks>
    /// <b>Approximate, and deliberately not in the ledger.</b> Picking the maximum over a
    /// concurrent count has no cheap deterministic form: two threads reading a count,
    /// comparing, and writing back will disagree about which of two near-equal names won.
    /// It appears in one console line and one smoke query, so first-past-the-post is the
    /// right trade — no fact carries it, and nothing that is asserted depends on it.
    /// </remarks>
    public string? SampleName { get; private set; }

    private int _sampleUses;

    private readonly ConcurrentDictionary<ISymbol, int> _uses = new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Which symbols outside this index have had their <c>codemarkup.SymbolInfo</c>
    /// written, so that the hundredth reference to <c>IDisposable</c> writes nothing.
    /// </summary>
    /// <remarks>
    /// <b>The key is the SCIP id, not the symbol.</b> <c>IReadOnlyList&lt;FjordFact&gt;</c>
    /// and <c>IReadOnlyList&lt;byte&gt;</c> are two <c>ISymbol</c>s and one id, and
    /// <c>SymbolInfo</c> is keyed <c>{symbol}</c> — so keying this by the symbol would let
    /// both through and offer ingest one key with two values, which it refuses
    /// (<c>ops-I4</c>) by killing the write stream part-way through. The value written is
    /// taken from <c>OriginalDefinition</c> for the same reason: it has to be a function
    /// of the id, and a constructed generic's signature is not.
    /// </remarks>
    private readonly ConcurrentDictionary<string, bool> _described = new(StringComparer.Ordinal);

    public bool Exhausted => options.MaxFiles > 0 && _claimed >= options.MaxFiles;

    /// <summary>Files handed to the walk, which is what `--max-files` counts.</summary>
    private int _claimed;

    /// <summary>
    /// Index one project, reporting each file as it is finished.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which files, decided in order; then walked in parallel.</b> Asking a symbol
    /// what it means is most of the cost of indexing, a <see cref="Compilation"/> is
    /// thread-safe, and separate semantic models over one compilation are exactly what
    /// an IDE runs concurrently all day. What must not become concurrent is <i>which</i>
    /// files: choosing them sequentially is what keeps <c>--max-files 2000</c> the same
    /// two thousand files on every run.
    /// </para>
    /// <para>
    /// Everything downstream of the symbol — the memos, the counters, the sink — is
    /// under one lock. The critical sections are dictionary work between binding calls
    /// that are far longer, and holding the sink's flush inside the lock is
    /// backpressure rather than a cost: a write stream is one at a time anyway.
    /// </para>
    /// </remarks>
    public void Index(Compilation compilation, Project? project, Action<string>? onFile = null)
    {
        // Before anything is claimed: an unwalked compilation must not consume the
        // `--max-files` budget, or which files a run reaches would depend on how many
        // reference assemblies happened to precede them.
        if (IsReferenceAssembly(compilation))
        {
            Interlocked.Increment(ref _referenceAssemblies);
            return;
        }

        // **A second project producing an assembly already walked is left out**, because
        // one database cannot hold both. Every symbol they declare in common mints one
        // `src.Symbol` — the package coordinate is the assembly identity, and theirs is
        // the same string — so `codemarkup.SymbolInfo`, keyed `{symbol}` with
        // `{signature, doc, modifiers}` on the value side, is one key wanted twice with
        // two values. Ingest refuses that (`ops-I4`) and the write stream dies part-way
        // through, which is what `src/coreclr/System.Private.CoreLib` beside
        // `src/mono/System.Private.CoreLib` does to a run over `dotnet/runtime`.
        //
        // **Not the same defect as a reference assembly, and not the same answer.** That
        // one is an API surface restated for the compiler and is dropped because nothing
        // is lost. These are two real implementations for two runtimes, and what is
        // dropped is source somebody wrote — so it is reported by name and `--strict`
        // fails the run, the same reading a project that would not build gets. Indexing
        // both means two databases, which is the decision `--framework` already makes for
        // a checkout that compiles twice.
        //
        // The reference-assembly test above runs first for a reason: a `ref/` project
        // never claims an identity, so the implementation beside it is still the one
        // walked however the solution ordered the pair.
        if (!_assemblies.Add(compilation.Assembly.Identity.GetDisplayName()))
        {
            _duplicates.Add(
                Relative(project?.FilePath) ?? project?.Name ?? compilation.AssemblyName ?? "?");
            return;
        }

        // **Rebuilt per project, and that is deliberate.** The entity facts two projects
        // produce for one symbol are identical, so throwing the memo away costs a rebuild
        // and the duplicates dedup on the way in — where keeping it would hold every
        // symbol of every project for the length of the run.
        _entities = new CsharpEntities((predicate, fact) => sink.Add(predicate, fact));

        var walking = new List<(SyntaxTree Tree, string Path)>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            if (Exhausted)
            {
                break;
            }

            var path = Relative(tree.FilePath);

            if (path is null || !_walked.Add(path))
            {
                continue;
            }

            walking.Add((tree, path));
            _claimed++;
        }

        Parallel.ForEach(
            walking,
            new ParallelOptions { MaxDegreeOfParallelism = options.Jobs },
            item =>
            {
                IndexTree(
                    compilation.GetSemanticModel(item.Tree),
                    item.Tree,
                    item.Path,
                    project?.GetDocument(item.Tree));

                Interlocked.Increment(ref _files_);
                onFile?.Invoke(item.Path);
            });
    }

    /// <summary>What an assembly carries to say it is a reference assembly.</summary>
    private const string ReferenceAssemblyMarker =
        "System.Runtime.CompilerServices.ReferenceAssemblyAttribute";

    /// <summary>
    /// Whether this compilation is a <b>reference assembly</b> — an API surface restated
    /// for the compiler rather than source anybody navigates to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Walking one kills the run.</b> A reference assembly restates the whole public API
    /// of the assembly it stands for, under that assembly's own identity, so every
    /// declaration in it mints the symbol its implementation already minted — correctly:
    /// they are one symbol. But it carries no documentation comments and spells its members
    /// <c>partial</c>, and <c>codemarkup.SymbolInfo</c> is keyed <c>{symbol}</c> with
    /// <c>{signature, doc, modifiers}</c> on the value side. Two facts then want one key
    /// with two values, ingest refuses it (<c>ops-I4</c>), <c>FactSink</c> latches the
    /// refusal and the write stream dies part-way through. Every library in
    /// <c>dotnet/runtime</c>'s shared framework ships such a pair.
    /// </para>
    /// <para>
    /// <b>Skipping is the answer rather than choosing a winner</b>, because a winner cannot
    /// be chosen without deciding it per run: projects are walked in solution order, so
    /// "first one wins" would answer every documentation query with whichever half the
    /// solution happened to list first. The implementation is the one with the docs, the
    /// bodies and the spans a reader wants, and it is the one that is kept.
    /// </para>
    /// <para>
    /// <b>The attribute rather than a path.</b> <c>ReferenceAssemblyAttribute</c> is what
    /// makes an assembly a reference assembly — the runtime refuses to load one carrying it
    /// — so it is the fact rather than a spelling of it; <c>ref/</c> as a directory name is
    /// <c>dotnet/runtime</c>'s convention and would say nothing about anybody else's tree.
    /// </para>
    /// <para>
    /// <b>Matched by its written name, because the type usually does not bind.</b> A
    /// design-time build resolves no metadata reference it did not need, so on a checkout
    /// that has not been built this attribute is an <c>IErrorTypeSymbol</c>: its
    /// <c>ContainingNamespace</c> is <c>System</c> — the deepest part that did resolve — and
    /// <c>GetTypeByMetadataName</c> answers <see langword="null"/>. A symbol comparison and
    /// a namespace check both therefore find *no* reference assembly on the very corpus this
    /// exists for, silently, while indexing every one of them. What survives not binding is
    /// <c>ToDisplayString</c>, the name as written — and the build writes it qualified,
    /// because it generates the attribute from an MSBuild <c>AssemblyAttribute</c> item.
    /// </para>
    /// <para>
    /// <b>Only the walk is skipped.</b> The build layer is emitted whole before any of
    /// this, so the project keeps its <c>msbuild.Project</c> and its compilation — the
    /// same decision <c>--max-files</c> already made, for the same reason: what projects a
    /// repository has is a fact about the repository and not about which files this run
    /// reached. Its *files* get no <c>src.File</c> fact unless something else names one,
    /// because that interning is what the walk does.
    /// </para>
    /// </remarks>
    private static bool IsReferenceAssembly(Compilation compilation) =>
        compilation.Assembly.GetAttributes().Any(
            attribute => attribute.AttributeClass?.ToDisplayString() == ReferenceAssemblyMarker);

    private void IndexTree(SemanticModel model, SyntaxTree tree, string path, Document? document)
    {
        var syntax = tree.GetRoot();
        var text = tree.GetText();

        // **The line table is built once per file and shared.** It is what the per-line
        // facts are written from *and* what converts every span below from Roslyn's UTF-16
        // positions into the UTF-8 offsets this schema counts in.
        var (rows, info) = SourceLayer.LineTable(text);
        var offsets = new SourceLayer.Offsets(text, rows, info);

        FjordFact file;

        file = FileOf(path);

        IndexFile(tree, file);
        IndexLines(text, rows, info, file, document);

        foreach (var node in syntax.DescendantNodes())
        {
            switch (node)
            {
                // Every form of declaration that has a symbol of its own. `GetDeclaredSymbol`
                // is what turns each into one, so the list is about *reaching* them.
                case BaseTypeDeclarationSyntax:
                case DelegateDeclarationSyntax:
                case BaseMethodDeclarationSyntax:
                case BasePropertyDeclarationSyntax:
                case EnumMemberDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    Declare(model, node, file, offsets);
                    break;

                // `int a, b;` is one field declaration and two fields, and the symbol
                // hangs off the declarator rather than the statement.
                case VariableDeclaratorSyntax declarator
                    when declarator.Parent?.Parent is BaseFieldDeclarationSyntax:
                    Declare(model, declarator, file, offsets);
                    break;

                // **A parameter is a declaration too, and was reaching none of this.**
                // `Reference` mints a global symbol for one — SCIP spells it
                // `…Connect().(address)` — so a use of it was a reference to a symbol
                // nothing declared, and a reader hovering it was told it came from
                // outside the index while looking at the line that declares it.
                case ParameterSyntax parameter:
                    DeclareParameter(model, parameter, file, offsets);
                    break;

                case SimpleNameSyntax name when options.References:
                    Reference(model, name, file, offsets);
                    break;

                // **The per-kind location facts, from the node whose kind decides them.**
                // In this visit rather than a second `DescendantNodes()` pass: asking a
                // symbol what it means is most of the cost of indexing, and a walk that
                // reached these nodes again would ask everything twice.
                case BaseObjectCreationExpressionSyntax creation when options.References:
                    Created(model, creation, file, offsets);
                    break;

                case InvocationExpressionSyntax invocation when options.References:
                    Invoked(model, invocation, file, offsets);
                    break;

                case MemberAccessExpressionSyntax access when options.References:
                    if (MemberAccess(model, access, file, offsets) is { } accessed)
                    {
                        sink.Add(DotnetIndex.MemberAccessLocation, accessed);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The two facts about a file that need no line table: what it is written in, and
    /// what its contents hash to.
    /// </summary>
    /// <remarks>
    /// None of them is gated by <c>--no-lines</c>: they are one fact each per file, and
    /// they are what makes a file's row in a search result renderable — a language to
    /// highlight by, and a digest to tell two checkouts of one path apart. Provenance
    /// joins them only when the run stated it, since it is the one thing here that is not
    /// in the code.
    /// </remarks>
    private void IndexFile(SyntaxTree tree, FjordFact file)
    {
        var language = DotnetIndex.FileLanguageFact(file, SourceLayer.LanguageName(tree.FilePath));
        var digest = DotnetIndex.FileDigestFact(file, SourceLayer.Digest(tree.GetText()));
        var origin = options.Repo is { } repo && options.Revision is { } revision
            ? DotnetIndex.FileOriginFact(file, repo, revision)
            : null;

        sink.Add(DotnetIndex.FileLanguage, language);
        sink.Add(DotnetIndex.FileDigest, digest);

        if (origin is not null)
        {
            sink.Add(DotnetIndex.FileOrigin, origin);
        }
    }

    /// <summary>
    /// The file's line table — <c>src.FileLine</c> and the <c>src.FileLineAt</c> that
    /// inverts it — and the <c>src.FileInfo</c> that summarises the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every line, including the blank ones.</b> A line table whose gaps mean
    /// "empty" is a table a consumer has to know a rule about, and the rule is
    /// indistinguishable from "that line was never indexed". Completeness is the
    /// property that makes it a table. The arithmetic that decides which lines those
    /// are lives in <see cref="SourceLayer"/>, where it is a property rather than a walk.
    /// </para>
    /// <para>
    /// <b><c>FileInfo</c> is written even with <c>--no-lines</c>.</b> It is one fact per
    /// file and it is what a consumer falls back to when an offset resolves past the last
    /// line's start; the switch is about the size of the per-line table, and a database
    /// that knows how many lines a file has but not what is on them is a coherent one.
    /// </para>
    /// <para>
    /// Built outside the lock and added inside it. A large file is a few thousand facts,
    /// and holding the sink — whose flush is a write stream — across the construction of
    /// all of them would serialise the walk behind the network.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A project file's own text, so a reader can open the thing the build layer describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A `.csproj` is named by the index and was never readable in it.</b> The build
    /// layer emits an `msbuild.Project` keyed on a `src.File`, so the path is there and a
    /// file tree lists it — but the walk only reads text for documents Roslyn compiles,
    /// and a project file is not one. Opening it showed a file with no lines.
    /// </para>
    /// <para>
    /// **No styles**: `IndexStyles` needs a `Document` to classify and there is none, so
    /// the file renders plain. That is the right answer rather than a gap — this producer
    /// has no XML classifier, and inventing one here would be a second highlighter to keep
    /// in step with nothing.
    /// </para>
    /// <para>
    /// Written once per project per run. A multi-targeting project is walked once per
    /// framework and offers the same lines again, which is one key with one value — a
    /// dedupe rather than the conflict `ops-I4` refuses.
    /// </para>
    /// </remarks>
    public void IndexProjectFile(string root, string relative)
    {
        var absolute = System.IO.Path.Combine(root, relative);

        if (!File.Exists(absolute))
        {
            return;
        }

        SourceText text;

        try
        {
            text = SourceText.From(File.ReadAllText(absolute));
        }
        catch (IOException)
        {
            // A project file that cannot be read is one fact fewer, not a failed run:
            // everything the build layer resolved about it still stands.
            return;
        }

        var (rows, info) = SourceLayer.LineTable(text);
        var file = DotnetIndex.FileFact(relative);

        sink.Add(DotnetIndex.File, file);
        sink.Add(
            DotnetIndex.FileLanguage,
            DotnetIndex.FileLanguageFact(file, SourceLayer.LanguageName(absolute)));
        sink.Add(DotnetIndex.FileDigest, DotnetIndex.FileDigestFact(file, SourceLayer.Digest(text)));

        IndexLines(text, rows, info, file, document: null);
    }

    private void IndexLines(
        SourceText text,
        List<SourceLayer.Row> rows,
        SourceLayer.Summary info,
        FjordFact file,
        Document? document)
    {
        var summary = DotnetIndex.FileInfoFact(file, info.Bytes, info.Lines, info.EndsInNewline);

        if (!options.Lines)
        {
            sink.Add(DotnetIndex.FileInfo, summary);

            return;
        }

        var lines = new List<FjordFact>(rows.Count);
        var offsets = new List<FjordFact>(rows.Count);

        foreach (var row in rows)
        {
            lines.Add(DotnetIndex.FileLineFact(
                file, row.Number, row.Text, row.Start, row.Bytes, row.CStart));
            offsets.Add(DotnetIndex.FileLineAtFact(file, row.Start, row.Number));
        }

        sink.Add(DotnetIndex.FileInfo, summary);

        foreach (var fact in lines)
        {
            sink.Add(DotnetIndex.FileLine, fact);
        }

        foreach (var fact in offsets)
        {
            sink.Add(DotnetIndex.FileLineAt, fact);
        }

        Interlocked.Add(ref _lines, lines.Count);

        IndexStyles(document, text, file);
    }

    /// <summary>
    /// <c>src.FileLineStyles</c> from Roslyn's own classifier — the reference client's
    /// answer to a schema field that is deliberately opaque.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A document, not a semantic model.</b> The classifier's semantic-model overload
    /// is obsolete, and the supported form takes a <see cref="Document"/> — so the
    /// workspace project Buildalyzer already built is carried down to here rather than
    /// a scratch one being invented. Where a compilation was built without a workspace
    /// <paramref name="document"/> is null, so no styles are written.
    /// </para>
    /// <para>
    /// A file with no tokens on a line writes no fact for it: absent means unhighlighted,
    /// which is the common case and the reason this costs so much less than markup.
    /// </para>
    /// </remarks>
    private void IndexStyles(Document? document, SourceText text, FjordFact file)
    {
        if (!options.Styles || document is null)
        {
            return;
        }

        var spans = Classifier
            .GetClassifiedSpansAsync(document, new TextSpan(0, text.Length), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var lines = SemanticTokens.Encode(spans, text);

        foreach (var line in lines)
        {
            sink.Add(
                DotnetIndex.FileLineStyles,
                DotnetIndex.FileLineStylesFact(file, line.Line, line.Payload));
        }

        Interlocked.Add(ref _styled, lines.Count);
    }

    /// <summary>
    /// A declaration: its entity, the edges its lists become, where it is written, and
    /// the global name it answers to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Identity and location are separate now</b>, which is the shape of the whole
    /// rewrite: the entity is keyed on what the compiler knows, and this adds one
    /// `DefinitionLocation` beside it. A declaration whose type the layer cannot express
    /// has no entity and so no location either — counted by
    /// <see cref="Inexpressible"/> rather than written under a fabricated type.
    /// </para>
    /// <para>
    /// <b>One member, however many declarations it is written across.</b>
    /// <see cref="ScipSymbols.Defining"/> is what the two halves of a partial member are
    /// read as, so every fact keyed on the symbol carries one value; the per-declaration
    /// facts beside them keep every span, which is how a partial type has answered "where
    /// is this written" all along.
    /// </para>
    /// </remarks>
    private void Declare(
        SemanticModel model,
        SyntaxNode node,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (model.GetDeclaredSymbol(node) is not { } declared)
        {
            return;
        }

        // **The member, not the declaration.** `GetDeclaredSymbol` on the implementing
        // half of a partial member answers a symbol its own containing type does not
        // list, whose signature is the other half's and whose documentation comment is
        // empty — so reading a per-symbol fact from it writes a second value under a key
        // the other half already filled.
        var symbol = ScipSymbols.Defining(declared);

        // Built outside the lock: all of this walks the symbol graph or the syntax, and
        // none of it needs the sink.
        var scip = ScipSymbols.Of(symbol, out var unspellable);
        var span = NameLocation(node).SourceSpan;
        var (start, length) = offsets.Span(span);
        var line = offsets.Line(span.Start);
        var kind = CodeMarkup.Kind(symbol);
        var signature = CodeMarkup.Signature(symbol);
        var modifiers = CodeMarkup.Modifiers(symbol);
        var doc = options.Docs ? DocComment(symbol) : string.Empty;

        if (unspellable)
        {
            Interlocked.Increment(ref _unspellable);
        }

        if (_entities.Definition(symbol) is not { } definition)
        {
            return;
        }

        _entities.Edges(symbol);

        sink.Add(
            DotnetIndex.DefinitionLocation,
            DotnetIndex.DefinitionLocationFact(definition, file, start, length));

        if (scip is not null)
        {
            var named = DotnetIndex.SymbolFact(scip);

            sink.Add(DotnetIndex.Symbol, named);
            sink.Add(DotnetIndex.SymbolOf, DotnetIndex.SymbolOfFact(definition, named));
            sink.Add(
                DotnetIndex.DefinitionBySymbol,
                DotnetIndex.DefinitionBySymbolFact(named, definition));

            var (definedStart, definedLength) =
                offsets.Span(NameLocation(FirstDeclaration(symbol, node)).SourceSpan);

            Markup(
                symbol, named, file, start, length, definedStart, definedLength, line,
                kind, signature, modifiers, doc);
        }

        Interlocked.Increment(ref _declarations);
    }

    /// <summary>
    /// Where a parameter is written, and what it is — but not what an outline lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two of the five facts a declaration writes, on purpose.</b>
    /// <c>codemarkup.Definition</c> is the symbol-to-location answer, so a use of a
    /// parameter can reach the line that declares it; <c>codemarkup.SymbolInfo</c> is
    /// what a hover card reads. The other three are deliberately skipped:
    /// <c>FileDefinition</c> is documented as "what a symbol outline or a sticky header
    /// needs", and no outline lists the parameters of every method — nor does a search
    /// over names want five more rows per signature, which is <c>SearchEntry</c> and
    /// <c>SymbolByName</c>.
    /// </para>
    /// <para>
    /// <b>Not routed through <see cref="Declare"/>, because a parameter has no
    /// <c>csharp</c> entity</b> — the language layer models types, methods, properties
    /// and fields, and `Declare` returns on a symbol with no entity before it reaches
    /// any of the `codemarkup` facts at all.
    /// </para>
    /// <para>
    /// <b>Only the defining half of a partial writes these.</b> Both halves declare
    /// parameters that spell one SCIP string, so walking both would offer
    /// <c>Definition</c> one key with two spans and <c>SymbolInfo</c> one key with two
    /// values — which ingest refuses (<c>ops-I4</c>) by killing the write stream
    /// part-way through. <see cref="ScipSymbols.Defining"/> is the same answer
    /// <see cref="Declare"/> uses for the member itself.
    /// </para>
    /// </remarks>
    private void DeclareParameter(
        SemanticModel model,
        ParameterSyntax node,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (model.GetDeclaredSymbol(node) is not { } symbol
            || symbol.ContainingSymbol is not { } member
            || !SymbolEqualityComparer.Default.Equals(ScipSymbols.Defining(member), member))
        {
            return;
        }

        var scip = ScipSymbols.Of(symbol, out var unspellable);

        if (unspellable)
        {
            Interlocked.Increment(ref _unspellable);
        }

        if (scip is null)
        {
            return;
        }

        var (start, length) = offsets.Span(node.Identifier.Span);
        var named = DotnetIndex.SymbolFact(scip);

        // **The member it belongs to, then its name.** `ToDisplayString()` on a parameter
        // is `Namespace.Type name` — the *type* and the name, which is a signature and not
        // a qualified name, and would leave a card repeating itself. The member's own
        // display string already carries its parameter list, so this reads the way a
        // method's `qualified` does with one more segment on the end.
        var qualified = $"{member.ToDisplayString()}.{symbol.Name}";

        sink.Add(DotnetIndex.Symbol, named);
        sink.Add(
            DotnetIndex.MarkupDefinition,
            DotnetIndex.MarkupDefinitionFact(
                named,
                file,
                start,
                length,
                CodeMarkup.Kind(symbol),
                symbol.Name,
                qualified));
        sink.Add(
            DotnetIndex.SymbolInfo,
            DotnetIndex.SymbolInfoFact(
                named,
                CodeMarkup.Signature(symbol),
                options.Docs ? DocComment(symbol) : string.Empty,
                CodeMarkup.Modifiers(symbol),
                qualified,
                Package(symbol),
                CodeMarkup.Kind(symbol)));

        // **A positional record declares two things with one name.** `record
        // WriteSummary(ulong Created, …)` writes a primary-constructor parameter *and* a
        // property, and only the parameter has syntax: the property is synthesised, so
        // `GetDeclaredSymbol` never answers with it from any node and the declaration
        // walk cannot reach it. A use of `summary.Created` was then a reference to a
        // symbol nothing declared — the same hole parameters were in, one layer along,
        // and it showed a reader a raw SCIP id.
        //
        // The span is this one: in a positional record that token *is* where both are
        // written, which is the honest answer to "where is this declared".
        if (node.Parent?.Parent is RecordDeclarationSyntax
            && member.ContainingType is { } holder)
        {
            foreach (var property in holder
                .GetMembers(symbol.Name)
                .OfType<IPropertySymbol>())
            {
                // **Only the one this token declares.** A record may write the property
                // out itself — `record R(int X) { public int X { get; init; } = X; }` —
                // and then nothing is synthesised: the property has a declaration of its
                // own, the ordinary walk reaches it there, and claiming it here as well
                // offers `codemarkup.Definition` one `{symbol, file}` key with two spans.
                // Ingest refuses that and the write stream dies part-way through, which
                // is what the surface corpus caught.
                if (!property.DeclaringSyntaxReferences.Any(
                        reference => reference.Span == node.Span))
                {
                    continue;
                }

                if (ScipSymbols.Of(property, out _) is not { } propertyScip)
                {
                    continue;
                }

                var asProperty = DotnetIndex.SymbolFact(propertyScip);

                sink.Add(DotnetIndex.Symbol, asProperty);
                sink.Add(
                    DotnetIndex.MarkupDefinition,
                    DotnetIndex.MarkupDefinitionFact(
                        asProperty,
                        file,
                        start,
                        length,
                        CodeMarkup.Kind(property),
                        property.Name,
                        property.ToDisplayString()));
                sink.Add(
                    DotnetIndex.SymbolInfo,
                    DotnetIndex.SymbolInfoFact(
                        asProperty,
                        CodeMarkup.Signature(property),
                        options.Docs ? DocComment(property) : string.Empty,
                        CodeMarkup.Modifiers(property),
                        property.ToDisplayString(),
                        Package(property),
                        CodeMarkup.Kind(property)));
            }
        }
    }

    /// <summary>
    /// The declaration <c>codemarkup.Definition</c> answers with for one file: the
    /// member's first in that file, whichever of its declarations the walk is at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The value has to be a function of the key, and the key is
    /// <c>{symbol, file}</c>.</b> Two declarations of one member in one file — both halves
    /// of a partial member, two <c>partial class</c> parts — otherwise fill that one key
    /// twice with two spans, ingest refuses one key with two values (<c>ops-I4</c>),
    /// <c>FactSink</c> latches the refusal and the write stream dies part-way through. So
    /// the span is asked of the member and the file rather than of the node.
    /// </para>
    /// <para>
    /// <b>First in the file rather than the defining half</b>, because a partial *type* has
    /// no defining half and needs the same rule — and because <c>codemarkup.Definition</c>
    /// is per file: taking the defining half's span would answer the implementing part's
    /// file with a span that is not in it. Position within one file is fixed, so this
    /// cannot depend on the order the compiler was handed the files, which
    /// <c>src.sigla</c>'s charter forbids. Nothing is lost either way:
    /// <c>csharp.DefinitionLocation</c> and <c>codemarkup.FileDefinition</c> are keyed per
    /// span and carry every declaration.
    /// </para>
    /// </remarks>
    private static SyntaxNode FirstDeclaration(ISymbol symbol, SyntaxNode node)
    {
        var first = node;

        foreach (var reference in Written(symbol))
        {
            if (reference.SyntaxTree == node.SyntaxTree && reference.Span.Start < first.Span.Start)
            {
                first = reference.GetSyntax();
            }
        }

        return first;
    }

    /// <summary>
    /// Every declaration a member is written across: every part of a partial type, and
    /// both halves of a partial member.
    /// </summary>
    /// <remarks>
    /// <b>A partial member's two halves are two symbols, and each knows only its own
    /// declaration.</b> A partial type's one symbol carries all of its parts in
    /// <c>DeclaringSyntaxReferences</c>; a partial member's defining half carries one
    /// reference and names the other half separately, so the union has to be taken by
    /// hand. The argument is already <see cref="ScipSymbols.Defining"/>'s answer, so the
    /// implementing part is the only half left to add.
    /// </remarks>
    private static IEnumerable<SyntaxReference> Written(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            yield return reference;
        }

        var implementing = symbol switch
        {
            IMethodSymbol method => (ISymbol?)method.PartialImplementationPart,
            IPropertySymbol property => property.PartialImplementationPart,
            _ => null,
        };

        if (implementing is null)
        {
            yield break;
        }

        foreach (var reference in implementing.DeclaringSyntaxReferences)
        {
            yield return reference;
        }
    }

    /// <summary>
    /// A reference: the span someone can click, and the definition it resolves to, in
    /// both directions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The identifier's extent, not the whole expression's.</b> A viewer draws the
    /// link over the name someone can click, so `Foo.Bar` is two references rather than
    /// one span covering both.
    /// </para>
    /// <para>
    /// <b>A target outside this index still gets an entity</b>, which the old model could
    /// not do: a reference to `System.String` targets a `csharp.Class` for it, interned
    /// through the same nesting as anything else, so "go to definition" answers for the
    /// BCL as far as the compiler knows it. <see cref="External"/> still counts them,
    /// because the entity exists and the *location* does not.
    /// </para>
    /// </remarks>
    private void Reference(
        SemanticModel model,
        SimpleNameSyntax name,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (Bound(model.GetSymbolInfo(name)) is not { } symbol)
        {
            if (!IsConstraintKeyword(name))
            {
                Interlocked.Increment(ref _unresolved);
            }

            return;
        }

        // Namespaces, labels and aliases have no `Definition` alternative at all.
        if (symbol.Kind is SymbolKind.Namespace or SymbolKind.Label
            or SymbolKind.RangeVariable or SymbolKind.Preprocessing or SymbolKind.Discard
            or SymbolKind.Alias)
        {
            return;
        }

        var outside = !symbol.Locations.Any(location => location.IsInSource);
        var (start, length) = offsets.Span(name.Identifier.Span);
        var role = CodeMarkup.Role(name, symbol);

        // **A local gets no global name, deliberately.** SCIP models one as an occurrence
        // ordinal that moves when the file is edited, so `FileLocalXRef` answers it span
        // to span instead — which needs the declaration's span, and only when it is in
        // *this* file.
        string? scip = null;

        if (symbol.Kind is not SymbolKind.Local)
        {
            scip = ScipSymbols.Of(symbol, out var unspellable);

            if (unspellable)
            {
                Interlocked.Increment(ref _unspellable);
            }
        }

        var local = scip is null ? Declared(symbol, name, offsets) : null;

        if (scip is null && local is null && symbol.Kind is SymbolKind.Local)
        {
            return;
        }

        // **The two layers are written independently, and that is not tidiness.** A
        // local has no `csharp.Definition` — this producer mints none, deliberately —
        // so a reference to one would be dropped entirely if the `codemarkup` facts
        // hung off the `csharp` one. They answer different questions and each is
        // written where it can be.
        if (_entities.Definition(symbol) is { } definition)
        {
            sink.Add(
                DotnetIndex.EntityXRef,
                DotnetIndex.EntityXRefFact(file, start, length, definition));

            // **A type written in source, taken from the value already built.**
            // `type = 0` of `Definition` carries the `AType` this predicate keys on, and
            // asking `CsharpEntities.Type` for it a second time would count a type the
            // layer cannot express twice — the tally a run reports as `Inexpressible`.
            if (definition is FjordValue.Union { Disc: 0u, Value: var written })
            {
                sink.Add(
                    DotnetIndex.TypeLocation,
                    DotnetIndex.TypeLocationFact(written, file, start, length));
            }

            // The same reference keyed by what it points at. Written twice because a
            // predicate leads with one field: find-references needs the target to
            // lead and a file view needs the file to, and until a derived predicate
            // can be declared the producer is what states the second order.
            sink.Add(
                DotnetIndex.EntityRef,
                DotnetIndex.EntityRefFact(definition, file, start, length));
        }

        if (scip is not null)
        {
            // The same reference on the language-independent surface, keyed by a
            // symbol rather than a `Definition` — which costs an interned string and
            // buys the ability to leave this database.
            var target = DotnetIndex.SymbolFact(scip);

            sink.Add(DotnetIndex.Symbol, target);
            sink.Add(
                DotnetIndex.FileXRef,
                DotnetIndex.FileXRefFact(file, start, length, target, role));
            sink.Add(
                DotnetIndex.SymbolXRef,
                DotnetIndex.SymbolXRefFact(target, file, start, length));

            // **What a hover card needs for something this index does not declare.**
            // `codemarkup.SymbolInfo` is keyed `{symbol}` and nothing else — there is no
            // file in that key — so it is answerable for a target with no declaration
            // site here, which is the whole difference between it and `Definition`. The
            // compiler already has the signature and, where the reference assembly ships
            // its XML beside it, the documentation comment too; without this the facts
            // exist in the compiler and nowhere in the index.
            //
            // Written once per id: a run meets `IDisposable` wherever it is used, and
            // every one of those would be the same key offered again.
            if (outside && _described.TryAdd(scip, true))
            {
                var described = symbol.OriginalDefinition;

                sink.Add(
                    DotnetIndex.SymbolInfo,
                    DotnetIndex.SymbolInfoFact(
                        target,
                        CodeMarkup.Signature(described),
                        options.Docs ? DocComment(described) : string.Empty,
                        CodeMarkup.Modifiers(described),
                        described.ToDisplayString(),
                        Package(described),
                        CodeMarkup.Kind(described)));
            }
        }
        else if (local is { } declared)
        {
            // **A file-local target, answered span to span.** No interned string: a
            // local has no global name worth minting, and a jump-to-declaration is
            // then one seek with no symbol table.
            sink.Add(
                DotnetIndex.FileLocalXRef,
                DotnetIndex.FileLocalXRefFact(
                    file, start, length, declared.Start, declared.Length, role));
        }

        Interlocked.Increment(ref _references);

        if (outside)
        {
            Interlocked.Increment(ref _external);
        }

        var canonical = symbol.OriginalDefinition;
        var uses = _uses.AddOrUpdate(canonical, 1, (_, seen) => seen + 1);

        // Racy by design, and the class comment says why: the winner of a near-tie is not
        // worth a lock, no fact carries this, and nothing asserted depends on it.
        if (uses > Volatile.Read(ref _sampleUses))
        {
            Volatile.Write(ref _sampleUses, uses);
            SampleName = canonical.Name;
        }
    }

    /// <summary>
    /// The symbol a name resolved to, or the one candidate the compiler declined to pick.
    /// </summary>
    /// <remarks>
    /// A single candidate is an ambiguity the compiler declined to resolve but a reader
    /// would read straight through — an inaccessible member, a failed overload. Several
    /// candidates is a genuine ambiguity, and guessing would put a wrong edge in the graph.
    /// </remarks>
    private static ISymbol? Bound(SymbolInfo info) =>
        info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);

    /// <summary>
    /// <c>csharp.ObjectCreationLocation</c> — where an object is constructed, and the
    /// constructor the compiler chose for it.
    /// </summary>
    /// <remarks>
    /// <b>The type as written, or the <c>new</c> keyword where the type is not written at
    /// all.</b> A target-typed <c>new()</c> has no type syntax to point at, and the whole
    /// expression's span would cover the argument list and the initialiser — which for
    /// <c>new T(a, b) { X = 1 }</c> is most of a line and nothing a viewer can draw a link
    /// over. The span is converted through the line table like every other span here, so
    /// it counts in the UTF-8 bytes <c>position-encoding</c> declares rather than in
    /// Roslyn's UTF-16 positions.
    /// </remarks>
    private void Created(
        SemanticModel model,
        BaseObjectCreationExpressionSyntax creation,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (Bound(model.GetSymbolInfo(creation)) is not IMethodSymbol constructor
            || constructor.ContainingType is not { } created
            || _entities.Type(created) is not { } type
            || _entities.Entity(constructor) is not { } method)
        {
            return;
        }

        var span = creation is ObjectCreationExpressionSyntax { Type: { } written }
            ? written.Span
            : creation.NewKeyword.Span;

        var (start, length) = offsets.Span(span);

        sink.Add(
            DotnetIndex.ObjectCreationLocation,
            DotnetIndex.ObjectCreationLocationFact(type, method, file, start, length));
    }

    /// <summary>
    /// <c>csharp.MethodInvocationLocation</c> — where a method is invoked, and the member
    /// access it was invoked through if there was one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The invoked name's own extent</b>, the way every other span this producer writes
    /// is an identifier's: <c>_store.Add(x)</c> points at <c>Add</c>, not at the statement
    /// around it. The optional carries the member access itself rather than a flag, so a
    /// consumer holding an invocation can reach that row and read its span; <c>nothing</c>
    /// is what tells <c>Add(x)</c> from <c>_store.Add(x)</c>.
    /// </para>
    /// <para>
    /// <b><c>nothing</c> means "through no member access this producer wrote".</b> A
    /// conditional call — <c>x?.M()</c> — is invoked through a member <i>binding</i>, a
    /// different syntax node with no <c>a.b</c> shape to write a row for, so the invocation
    /// is recorded and the optional is empty. Reading it as "the call had no receiver"
    /// would be wrong for exactly those calls.
    /// </para>
    /// </remarks>
    private void Invoked(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (Bound(model.GetSymbolInfo(invocation)) is not IMethodSymbol invoked
            || _entities.Entity(invoked) is not { } method)
        {
            return;
        }

        var named = InvokedName(invocation.Expression);
        var (start, length) = offsets.Span(
            named is null ? invocation.Expression.Span : named.Identifier.Span);

        // Built and nested, not emitted here: the node is reached by the walk in its own
        // right, which is where its row is written. A nested reference carries the whole
        // fact, so the two agree because they are the same fact.
        var through = invocation.Expression is MemberAccessExpressionSyntax access
            ? MemberAccess(model, access, file, offsets)
            : null;

        sink.Add(
            DotnetIndex.MethodInvocationLocation,
            DotnetIndex.MethodInvocationLocationFact(method, file, start, length, through));
    }

    /// <summary>The name an invocation invokes, where the expression has one.</summary>
    private static SimpleNameSyntax? InvokedName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax name => name,
        MemberAccessExpressionSyntax access => access.Name,
        MemberBindingExpressionSyntax binding => binding.Name,
        _ => null,
    };

    /// <summary>
    /// <c>csharp.MemberAccessLocation</c> — where a member is accessed, and what the
    /// accessed member resolves to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The accessed member, not the expression it was reached through.</b> The schema's
    /// field is named after Roslyn's own <c>Expression</c> property, which is the
    /// <i>receiver</i> — so the reading is worth stating rather than leaving to the field
    /// name. The comment declaring the predicate says "what the accessed member resolves
    /// to", and the location predicates around it are per kind: a type, a construction, a
    /// call, and this one — the field, property or method a <c>.</c> reaches. A row
    /// carrying the receiver instead would answer the position of <c>b</c> in <c>a.b</c>
    /// with <c>a</c>, and leave every field and property read answered by nothing.
    /// </para>
    /// <para>
    /// Returned rather than written, so <see cref="Invoked"/> can nest the same fact.
    /// </para>
    /// </remarks>
    private FjordFact? MemberAccess(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (Bound(model.GetSymbolInfo(access.Name)) is not { } member
            || _entities.Accessed(member) is not { } expression)
        {
            return null;
        }

        var (start, length) = offsets.Span(access.Name.Identifier.Span);

        return DotnetIndex.MemberAccessLocationFact(expression, file, start, length);
    }

    /// <summary>
    /// Whether a name is a <c>where</c> clause's constraint keyword rather than a type.
    /// </summary>
    /// <remarks>
    /// <b>There is no symbol to resolve, so this is not an unresolved name.</b>
    /// <c>notnull</c> and <c>unmanaged</c> are the two constraints C# spells as an
    /// identifier, and Roslyn parses each as a <c>TypeConstraint</c> whose type binds to
    /// nothing — counting them puts an indexing failure that did not happen into a number
    /// operators read as one. Every other keyword constraint (<c>class</c>,
    /// <c>struct</c>, <c>new()</c>, <c>default</c>, <c>allows ref struct</c>) has a syntax
    /// node of its own and never reaches this walk as a name. The position is checked as
    /// well as the spelling: outside a constraint both words are ordinary identifiers, and
    /// a type by either name that fails to bind is a real miss.
    /// </remarks>
    private static bool IsConstraintKeyword(SimpleNameSyntax name) =>
        name is IdentifierNameSyntax { Parent: TypeConstraintSyntax }
        && name.Identifier.Text is "notnull" or "unmanaged";

    /// <summary>
    /// The <c>codemarkup</c> projection of one declaration: the same facts, re-keyed for
    /// the questions a UI asks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every one of these is redundant with the <c>csharp</c> facts beside it by
    /// construction — while <c>nyi/derivation</c> stands, a producer is what states the
    /// second keying, and the query that *would* derive each is a comment in the schema.
    /// </para>
    /// <para>
    /// <b>Two spans, because two of these are keyed per member and the rest per
    /// declaration.</b> <c>codemarkup.Definition</c> is <c>{symbol, file}</c> and
    /// <c>codemarkup.SymbolInfo</c> is <c>{symbol}</c>, so both take
    /// <paramref name="definedStart"/> — the member's first declaration in this file —
    /// and are the same fact whichever of its declarations the walk is at.
    /// <c>FileDefinition</c> leads with a span and <c>SearchEntry</c>'s key carries a
    /// <c>line</c> — neither is <c>{symbol, file}</c>, so both take the declaration the
    /// walk is standing on and both get a row per declaration. A partial member declared
    /// twice in one file therefore appears twice in the search index, differing in
    /// <c>line</c>, and it is that field rather than a span that keeps the two apart.
    /// </para>
    /// </remarks>
    private void Markup(
        ISymbol symbol,
        FjordFact named,
        FjordFact file,
        long start,
        long length,
        long definedStart,
        long definedLength,
        long line,
        FjordValue kind,
        string signature,
        string modifiers,
        string doc)
    {
        var name = symbol.Name;

        sink.Add(
            DotnetIndex.MarkupDefinition,
            DotnetIndex.MarkupDefinitionFact(
                named, file, definedStart, definedLength, kind, name, symbol.ToDisplayString()));

        sink.Add(
            DotnetIndex.FileDefinition,
            DotnetIndex.FileDefinitionFact(file, start, length, named, kind, name));

        sink.Add(
            DotnetIndex.SymbolInfo,
            DotnetIndex.SymbolInfoFact(
                named,
                signature,
                doc,
                modifiers,
                symbol.ToDisplayString(),
                Package(symbol),
                kind));

        // The two search rows: the case-folded one a prefix or fuzzy match seeks on, and
        // the exact one, because "find exactly `Parse`" and "find anything spelled like
        // parse" are different questions.
        sink.Add(
            DotnetIndex.SearchEntry,
            DotnetIndex.SearchEntryFact(name, kind, named, file, line));
        sink.Add(DotnetIndex.SymbolByName, DotnetIndex.SymbolByNameFact(name, named));

        Relate(symbol, named);
    }

    /// <summary>The relation edges a declaration implies, in both directions.</summary>
    /// <remarks>
    /// Containment is a relation rather than a field on the definition, so that it is
    /// joinable both ways — <c>Relation {from = C, kind = {contains}, to = S}</c> answers
    /// "what is in this" and <c>RelationOf</c> answers "what contains this".
    /// </remarks>
    private void Relate(ISymbol symbol, FjordFact named)
    {
        // **`fromOther` is the direction, and getting it wrong is silent.**
        // `codemarkup.sigla` reads `Relation` as "`from` <kind> `to`", and `RelationOf`
        // carries the same edge reversed — so a transposed pair still answers both
        // queries with every symbol resolving, and says "Base extends Derived".
        void Edge(ISymbol? other, uint kind, bool fromOther)
        {
            if (other is null)
            {
                return;
            }

            // **The flagged overload here too, because a dropped edge is invisible.** A
            // `Relation` row is a pair of symbols and half of one is no edge, so an
            // unspellable target loses the whole edge — a larger loss than a declaration's
            // and the one with nothing else in the database pointing at it. The `as` casts
            // below mean every `other` reaching this line is a named type, which always
            // spells; the flag says so rather than assuming it.
            if (ScipSymbols.Of(other, out var unspellable) is not { } text)
            {
                if (unspellable)
                {
                    Interlocked.Increment(ref _unspellable);
                }

                return;
            }

            var target = DotnetIndex.SymbolFact(text);
            var value = DotnetIndex.Tagged(kind);
            var from = fromOther ? target : named;
            var to = fromOther ? named : target;

            sink.Add(DotnetIndex.Symbol, target);
            sink.Add(DotnetIndex.Relation, DotnetIndex.RelationFact(from, value, to));
            sink.Add(DotnetIndex.RelationOf, DotnetIndex.RelationOfFact(to, value, from));
        }

        Edge(symbol.ContainingSymbol as INamedTypeSymbol, 1u, fromOther: true);

        if (symbol is INamedTypeSymbol type)
        {
            if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                Edge(baseType, 2u, fromOther: false);
            }

            foreach (var iface in type.Interfaces)
            {
                Edge(iface, 3u, fromOther: false);
            }
        }

        if (symbol.IsOverride)
        {
            Edge(
                symbol switch
                {
                    IMethodSymbol method => method.OverriddenMethod,
                    IPropertySymbol property => property.OverriddenProperty,
                    IEventSymbol @event => @event.OverriddenEvent,
                    _ => null,
                },
                4u,
                fromOther: false);
        }
    }

    /// <summary>
    /// The assembly a symbol ships in, as a name and a version.
    /// </summary>
    /// <remarks>
    /// <b>The coordinate its symbol is minted under, said the way a card shows it.</b> A
    /// SCIP id already carries this — <c>nuget System.Runtime 10.0.0.0 …</c> — but reading
    /// it back out means parsing somebody else's identifier format in every consumer, and
    /// a hand-written parser of a foreign grammar is a thing that breaks quietly when the
    /// scheme moves. The assembly identity's full display name is the other option and is
    /// not it either: <c>Culture=neutral, PublicKeyToken=…</c> is not what a reader wants
    /// to be shown, so trimming it would just move the parsing.
    /// </remarks>
    private static string Package(ISymbol symbol) =>
        symbol.ContainingAssembly?.Identity is { } identity
            ? $"{identity.Name} {identity.Version}"
            : string.Empty;

    /// <summary>
    /// A declaration's doc comment as plain text — the summary, collapsed.
    /// </summary>
    /// <remarks>
    /// <b>The summary only, and no markup.</b> Roslyn hands back the whole XML block, and
    /// a hover card wants a sentence: the tags would have to be stripped by every
    /// consumer, and stripping them here means the fact is the same for a consumer that
    /// cannot parse XML. What is lost is <c>&lt;param&gt;</c> and <c>&lt;returns&gt;</c>,
    /// which a signature already carries.
    /// </remarks>
    private static string DocComment(ISymbol symbol)
    {
        if (symbol.GetDocumentationCommentXml() is not { Length: > 0 } xml)
        {
            return string.Empty;
        }

        var opened = xml.IndexOf("<summary>", StringComparison.Ordinal);
        var closed = xml.IndexOf("</summary>", StringComparison.Ordinal);

        if (opened < 0 || closed <= opened)
        {
            return string.Empty;
        }

        var summary = xml[(opened + "<summary>".Length)..closed];

        // **A cref is a word, not a tag.** `<see cref="T:System.UInt32"/>` is empty —
        // the name it stands for is in the attribute — so stripping tags outright turns
        // "Writes a <see cref="uint"/> into a span" into "Writes a into a span". The
        // reference assemblies lean on these, so this is most of a BCL summary's nouns.
        // The `T:`/`M:` prefix and the namespace go, leaving what a reader would say.
        var named = System.Text.RegularExpressions.Regex.Replace(
            summary,
            """<(?:see|seealso)\s+cref="(?:[A-Za-z]:)?([^"]*)"\s*/?>""",
            match =>
            {
                var cref = match.Groups[1].Value;
                var cut = cref.LastIndexOfAny(['.', '#']);
                return cut >= 0 && cut < cref.Length - 1 ? cref[(cut + 1)..] : cref;
            });

        // `<paramref name="x"/>` and `<typeparamref name="T"/>` say their name the same way.
        named = System.Text.RegularExpressions.Regex.Replace(
            named,
            """<(?:paramref|typeparamref)\s+name="([^"]*)"\s*/?>""",
            match => match.Groups[1].Value);

        // What is left — `<c>x</c>`, `<para>` — becomes its text, and the line-wrapped
        // source becomes one line.
        var text = System.Text.RegularExpressions.Regex.Replace(named, "<[^>]*>", string.Empty);

        return SourceLayer.Clip(
            string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>
    /// Where a file-local target is declared, as a byte span in this file.
    /// </summary>
    /// <remarks>
    /// Null when the declaration is in another file, which for a local cannot happen and
    /// for anything else means there is no span in *this* file to point at.
    /// </remarks>
    private static (long Start, long Length)? Declared(
        ISymbol symbol,
        SimpleNameSyntax name,
        SourceLayer.Offsets offsets)
    {
        var here = name.SyntaxTree;

        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource && location.SourceTree == here)
            {
                return offsets.Span(location.SourceSpan);
            }
        }

        return null;
    }

    /// <summary>The file fact for a path, and the project edges that go with it.</summary>
    private FjordFact FileOf(string path)
    {
        if (_files.TryGetValue(path, out var known))
        {
            return known;
        }

        // **Built outside, published with `TryAdd`, and only the winner writes.** The fact
        // is a function of the path, so two threads reaching one file build the same one —
        // but the project edges beside it, and the count of files nobody compiles, must
        // happen once. A `GetOrAdd` factory would not do: it runs on the losers too.
        var fact = DotnetIndex.FileFact(path);

        if (!_files.TryAdd(path, fact))
        {
            return _files[path];
        }

        sink.Add(DotnetIndex.File, fact);

        // **What compiles this file** — here rather than in the walk, because a file fact
        // is also created for a file nobody walked: a declaration in another project,
        // reached through a reference, names one. Both directions are stored because
        // neither is derivable in a seek from the other.
        var owners = projects.Owners(path);

        foreach (var project in owners)
        {
            sink.Add(
                DotnetIndex.SourceFileToProject,
                DotnetIndex.SourceFileToProjectFact(fact, project.Fact));
            sink.Add(
                DotnetIndex.ProjectToSourceFile,
                DotnetIndex.ProjectToSourceFileFact(project.Fact, fact));
        }

        if (owners.Count == 0)
        {
            Interlocked.Increment(ref _unattributed);
        }

        return fact;
    }

    /// <summary>The path a fact names it by, or nothing if this file is not indexed.</summary>
    private string? Relative(string? absolute)
    {
        if (string.IsNullOrEmpty(absolute))
        {
            // Generated syntax with no file behind it — a source generator's output in
            // memory, or a tree parsed from a string.
            return null;
        }

        // **Outside the root is not a name.** `Paths.Relative` refuses a path that climbs
        // out — it would come back as `../../../elsewhere`, which depends on where the
        // root happens to be, so two runs of one repository would disagree about it — and
        // this walk used to do its own arithmetic without that check. A test project
        // referencing a package with source in it therefore put
        // `../../../.nuget/packages/…/Program.cs` in the index, where it named nothing a
        // consumer could open and nothing a second run would agree with.
        //
        // Build output is not source either. `obj/` in particular holds the generated
        // assembly attributes every project has, which would be the same six declarations
        // in every project and none of them anything anyone wants to find.
        return Paths.Relative(root, absolute) is { } relative && !Paths.IsBuildOutput(relative)
            ? relative
            : null;
    }

    /// <summary>Where a declaration's name is written, rather than where its syntax starts.</summary>
    /// <remarks>
    /// A declaration's syntax node begins at its first attribute or modifier, so a
    /// documented method's span would start at its <c>[Obsolete]</c>. A location in this
    /// index is somewhere a person is meant to be able to open, so it points at the
    /// identifier.
    /// </remarks>
    private static Location NameLocation(SyntaxNode node) => node switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.GetLocation(),
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.GetLocation(),
        MethodDeclarationSyntax method => method.Identifier.GetLocation(),
        ConstructorDeclarationSyntax constructor => constructor.Identifier.GetLocation(),
        DestructorDeclarationSyntax destructor => destructor.Identifier.GetLocation(),
        OperatorDeclarationSyntax @operator => @operator.OperatorToken.GetLocation(),
        ConversionOperatorDeclarationSyntax conversion => conversion.Type.GetLocation(),
        PropertyDeclarationSyntax property => property.Identifier.GetLocation(),
        IndexerDeclarationSyntax indexer => indexer.ThisKeyword.GetLocation(),
        EventDeclarationSyntax @event => @event.Identifier.GetLocation(),
        EnumMemberDeclarationSyntax member => member.Identifier.GetLocation(),
        VariableDeclaratorSyntax declarator => declarator.Identifier.GetLocation(),
        LocalFunctionStatementSyntax local => local.Identifier.GetLocation(),
        ParameterSyntax parameter => parameter.Identifier.GetLocation(),
        _ => node.GetLocation(),
    };
}
