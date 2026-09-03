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
    private long _lines, _styled;

    public int Files => Volatile.Read(ref _files_);

    public int Declarations => Volatile.Read(ref _declarations);

    public int References => Volatile.Read(ref _references);

    /// <summary>References to something declared outside the index — the BCL, a package.</summary>
    public int External => Volatile.Read(ref _external);

    /// <summary>Names the compiler could not bind at all: missing references, broken code.</summary>
    public int Unresolved => Volatile.Read(ref _unresolved);

    /// <summary>Types the layer cannot express — `dynamic`, an unresolved name.</summary>
    /// <remarks>
    /// `csharp.AType` has no alternative for either and it sits in the key of `Method`,
    /// `Field` and `Parameter`, so the declaration is dropped rather than recorded under a
    /// fabricated type. Counted because a layer that silently loses declarations is worse
    /// than one that says how many.
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

                case SimpleNameSyntax name when options.References:
                    Reference(model, name, file, offsets);
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
    /// <b>Identity and location are separate now</b>, which is the shape of the whole
    /// rewrite: the entity is keyed on what the compiler knows, and this adds one
    /// `DefinitionLocation` beside it. A declaration whose type the layer cannot express
    /// has no entity and so no location either — counted by
    /// <see cref="Inexpressible"/> rather than written under a fabricated type.
    /// </remarks>
    private void Declare(
        SemanticModel model,
        SyntaxNode node,
        FjordFact file,
        SourceLayer.Offsets offsets)
    {
        if (model.GetDeclaredSymbol(node) is not { } symbol)
        {
            return;
        }

        // Built outside the lock: all of this walks the symbol graph or the syntax, and
        // none of it needs the sink.
        var scip = ScipSymbols.Of(symbol);
        var span = NameLocation(node).SourceSpan;
        var (start, length) = offsets.Span(span);
        var line = offsets.Line(span.Start);
        var kind = CodeMarkup.Kind(symbol);
        var signature = CodeMarkup.Signature(symbol);
        var modifiers = CodeMarkup.Modifiers(symbol);
        var doc = options.Docs ? DocComment(symbol) : string.Empty;

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

            Markup(symbol, named, file, start, length, line, kind, signature, modifiers, doc);
        }

        Interlocked.Increment(ref _declarations);
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
        var info = model.GetSymbolInfo(name);

        // A single candidate is an ambiguity the compiler declined to resolve but a
        // reader would read straight through — an inaccessible member, a failed
        // overload. Several candidates is a genuine ambiguity, and guessing would put a
        // wrong edge in the graph.
        var symbol = info.Symbol
            ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);

        if (symbol is null)
        {
            Interlocked.Increment(ref _unresolved);

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
        var scip = symbol.Kind is SymbolKind.Local ? null : ScipSymbols.Of(symbol);
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
    /// The <c>codemarkup</c> projection of one declaration: the same facts, re-keyed for
    /// the questions a UI asks.
    /// </summary>
    /// <remarks>
    /// Every one of these is redundant with the <c>csharp</c> facts beside it by
    /// construction — while <c>nyi/derivation</c> stands, a producer is what states the
    /// second keying, and the query that *would* derive each is a comment in the schema.
    /// </remarks>
    private void Markup(
        ISymbol symbol,
        FjordFact named,
        FjordFact file,
        long start,
        long length,
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
                named, file, start, length, kind, name, symbol.ToDisplayString()));

        sink.Add(
            DotnetIndex.FileDefinition,
            DotnetIndex.FileDefinitionFact(file, start, length, named, kind, name));

        sink.Add(
            DotnetIndex.SymbolInfo,
            DotnetIndex.SymbolInfoFact(named, signature, doc, modifiers));

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
        void Edge(ISymbol? other, uint kind)
        {
            if (other is null || ScipSymbols.Of(other) is not { } text)
            {
                return;
            }

            var target = DotnetIndex.SymbolFact(text);
            var value = DotnetIndex.Tagged(kind);

            sink.Add(DotnetIndex.Symbol, target);
            sink.Add(DotnetIndex.Relation, DotnetIndex.RelationFact(target, value, named));
            sink.Add(DotnetIndex.RelationOf, DotnetIndex.RelationOfFact(named, value, target));
        }

        // `contains`, written from the container's side: the argument order above is
        // (from, kind, to), so the container is `from`.
        Edge(symbol.ContainingSymbol as INamedTypeSymbol, 1u);

        if (symbol is INamedTypeSymbol type)
        {
            if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                Edge(baseType, 2u);
            }

            foreach (var iface in type.Interfaces)
            {
                Edge(iface, 3u);
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
                4u);
        }
    }

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

        // Inner tags — `<see cref="X"/>`, `<c>x</c>` — become their text, and the
        // line-wrapped source becomes one line.
        var text = System.Text.RegularExpressions.Regex.Replace(summary, "<[^>]*>", string.Empty);

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
