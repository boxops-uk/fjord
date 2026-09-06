using System.Linq;

using Boxops.Fjord.Client;
using Boxops.Fjord.Scip;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>A SCIP index, converted, and asked the four questions a code UI asks.</b>
/// </para>
/// <para>
/// The claim is that a language with a SCIP indexer reaches a Fjord database with no
/// per-language work here. The fixture is TypeScript — a language this repository has no
/// compiler for and never will — and nothing in the converter knows that.
/// </para>
/// <para>
/// <b>The gate is the converter's output, asserted directly.</b> Revision 2 accepted R9 on
/// the viewer answering <c>/symbol/{name}</c> against a converted index; there is no
/// viewer to answer it, and hanging a converter's acceptance on a browser replacement's
/// schedule would be a dependency nobody chose. Queries are better on their own terms:
/// they test the converter rather than a UI, they fail inside this suite rather than
/// through a web request, and they do not go red because somebody changed a stylesheet.
/// </para>
/// </summary>
public sealed class ScipConverterTests
{
    private const string Package = "scip-typescript npm fixture 1.0.0";
    private const string Greet = Package + " src/`greet.ts`/greet().";

    /// <summary>A row rendered flat, so an assertion can name what it expects.</summary>
    private static string Rendered(FjordValue value) => value switch
    {
        FjordValue.Str text => text.Value,
        FjordValue.Int number => number.Value.ToString(),
        FjordValue.Record record => $"{{{string.Join(", ", record.Fields.Select(Rendered))}}}",
        FjordValue.Union union => $"{union.Disc}:{Rendered(union.Value)}",
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Convert the fixture index into a fresh database, and hand back a connection.</summary>
    private static FjordConnection Converted(
        FjordServer server, string database, string fixture = "index.scip")
    {
        var index = Path.Combine(
            FjordServer.RepositoryRoot, "clients", "dotnet", "tests", "fixtures", "scip",
            fixture);

        var code = Program.Main([
            "--input", index,
            "--at", $"{server.Socket}//{database}",
        ]);

        Assert.Equal(0, code);

        return FjordConnection.Connect(server.Socket, database, ScipFacts.Schema);
    }

    /// <summary>
    /// <b>The four questions, against one converted index.</b>
    /// </summary>
    /// <remarks>
    /// One test rather than four because the conversion is the expensive part and the
    /// questions are cheap; a failure names which question by its assertion message.
    /// </remarks>
    [Fact]
    public void A_converted_index_answers_what_a_code_ui_asks()
    {
        using var server = FjordServer.Serving("scip", "index.sigla");
        using var connection = Converted(server, "scip");

        // **Go to definition.** The reference at byte 76 of `main.ts` is the call to
        // `greet`, and it lands on the definition in `greet.ts` — across a file, through a
        // symbol string, with no fact id between them.
        var definition = connection.Query(
            """
            {file = P, at = S} where
              codemarkup.FileXRef {file = _, span = {start = 76, length = 5}, target = T};
              codemarkup.FileDefinition {file = D, span = S, symbol = T};
              D = src.File P
            """).Rows;

        Assert.Single(definition);
        Assert.Contains("src/greet.ts", Rendered(definition[0]), StringComparison.Ordinal);

        // **Every reference in one file, in position order.** The key leads with the file
        // and then the span, so this is a seek into one file's run rather than a scan —
        // and the order is the key's, which is the order a reader reads in.
        var inFile = connection.Query(
            """
            S where
              codemarkup.FileXRef {file = F, span = S, target = _};
              F = src.File "src/greet.ts"
            """).Rows;

        Assert.Equal(3, inFile.Count);

        var starts = inFile
            .Select(row => Assert.IsType<FjordValue.Int>(
                Assert.IsType<FjordValue.Record>(row).Fields[0]).Value)
            .ToList();

        Assert.Equal(starts.Order(), starts);

        // **Find references across files.** Keyed by what it points at, which is the
        // second ordering `SymbolXRef` exists for: three uses of `greet`, in two files.
        var references = connection.Query(
            $$"""
            {file = P, at = S} where
              codemarkup.SymbolXRef {target = T, file = F, span = S};
              T = src.Symbol "{{Greet}}";
              F = src.File P
            """).Rows;

        Assert.Equal(3, references.Count);
        Assert.Contains(references, row => Rendered(row).Contains("src/main.ts", StringComparison.Ordinal));
        Assert.Contains(references, row => Rendered(row).Contains("src/greet.ts", StringComparison.Ordinal));

        // **A prefix search**, on the case-folded name the search index leads with.
        var search = connection.Query(
            """
            N where
              codemarkup.SearchEntry
                {nameLowercase = "g".., name = N, kind = _, symbol = _, file = _, line = _}
            """).Rows;

        Assert.Single(search);
        Assert.Contains("greet", Rendered(search[0]), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A document that states one symbol twice is refused, and says which symbol.</b>
    /// </summary>
    /// <remarks>
    /// Two <c>SymbolInformation</c> entries for one symbol disagree about the kind and the
    /// display name, so there is no entry to prefer and the input is malformed. The trap is
    /// the obvious reading: <c>ToDictionary</c> raises an <c>ArgumentException</c>, which
    /// the CLI's catch filter does not name — so a malformed file leaves a stack trace
    /// where a person needs the path and the symbol.
    /// </remarks>
    [Fact]
    public void A_document_that_states_one_symbol_twice_is_refused()
    {
        using var sink = new FactSink(ScipFacts.Schema, []);

        var document = new Document(
            "src/greet.ts",
            "TypeScript",
            "export function greet(): string {\n  return \"hi\";\n}\n",
            1,
            [new Occurrence([0, 16, 21], Greet, 1, 0)],
            [
                new SymbolInformation(Greet, 17, "greet"),
                new SymbolInformation(Greet, 26, "greeting"),
            ]);

        var failure = Assert.Throws<FormatException>(
            () => new Converter(sink, ".", TextWriter.Null).Convert(document));

        Assert.Contains("src/greet.ts", failure.Message, StringComparison.Ordinal);
        Assert.Contains(Greet, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A SCIP `local` is scoped to its document and never becomes a `src.Symbol`.</b>
    /// </summary>
    /// <remarks>
    /// The specification is explicit — "Local symbols MUST only be used for entities which
    /// are local to a Document, and cannot be accessed from outside the Document" — and the
    /// number is an occurrence ordinal, so `local 1` in two files names two different
    /// variables. Interning it joins them: "find every use" in one file answers a span in
    /// the other, and a search offers `1` as a name to jump to. `codemarkup.FileLocalXRef`
    /// answers them span to span inside one document instead, which is the decision
    /// `docs/unified-plan/15-retire-code-sigla.md` records and the .NET indexer already
    /// writes.
    /// </remarks>
    [Fact]
    public void A_local_is_answered_span_to_span_and_never_interned()
    {
        using var server = FjordServer.Serving("locals", "index.sigla");
        using var connection = Converted(server, "locals", "locals.scip");

        // **Nothing anywhere targets a `local 1`.** Four rows here — two per file — is
        // the converter having made one identity out of two variables.
        var interned = connection.Query(
            """
            {file = P, at = S} where
              codemarkup.SymbolXRef {target = T, file = F, span = S};
              T = src.Symbol "local 1";
              F = src.File P
            """).Rows;

        Assert.Empty(interned);

        // Nor is it a name anybody can search for, or jump to by name.
        var names = connection.Query(
            """
            N where
              codemarkup.SearchEntry
                {nameLowercase = _, name = N, kind = _, symbol = _, file = _, line = _}
            """).Rows.Select(Rendered).ToList();

        Assert.Equal(["count", "label"], names.Order(StringComparer.Ordinal));

        // **The uses are answered span to span, and only within the file.** `total` is
        // declared at byte 42 of `count.ts` and read at byte 62; the declaration counts
        // as a use of itself for the reason every definition is a cross-reference.
        var uses = connection.Query(
            """
            {at = S, to = D} where
              codemarkup.FileLocalXRef {file = F, span = S, target = D, role = _};
              F = src.File "src/count.ts"
            """).Rows.Select(Rendered).ToList();

        Assert.Equal(["{{42, 5}, {42, 5}}", "{{62, 5}, {42, 5}}"], uses);

        // `label.ts` names its local `text`, so its spans are its own — which is what a
        // shared `local 1` would have made impossible to tell.
        var other = connection.Query(
            """
            {at = S, to = D} where
              codemarkup.FileLocalXRef {file = F, span = S, target = D, role = _};
              F = src.File "src/label.ts"
            """).Rows.Select(Rendered).ToList();

        Assert.Equal(["{{42, 4}, {42, 4}}", "{{63, 4}, {42, 4}}"], other);
    }

    /// <summary>
    /// <b>The positions are byte offsets, and the file that proves it has an é in it.</b>
    /// </summary>
    /// <remarks>
    /// SCIP counts characters — UTF-8, UTF-16 or UTF-32 code units, depending on what the
    /// indexer was written in — and this schema counts UTF-8 bytes from the start of the
    /// file. The fixture's second line is <c>return `héllo ${who}`</c>, so the reference to
    /// <c>who</c> on it has a character offset one less than its byte offset: an index of
    /// this file that was right about ASCII and wrong about everything else would place it
    /// at 63.
    /// </remarks>
    [Fact]
    public void A_character_offset_becomes_a_byte_offset()
    {
        using var server = FjordServer.Serving("positions", "index.sigla");
        using var connection = Converted(server, "positions");

        var uses = connection.Query(
            $$"""
            S where
              codemarkup.SymbolXRef {target = T, file = _, span = S};
              T = src.Symbol "{{Package}} src/`greet.ts`/greet().(who)"
            """).Rows;

        Assert.Equal(2, uses.Count);

        // The declaration at character 22 of line 1 — all ASCII, so 22 — and the use at
        // character 18 of line 2, which is 19 bytes in because of the é before it. Line 2
        // starts at byte 45.
        var text = string.Join(" ", uses.Select(Rendered));

        Assert.Contains("{22, 3}", text, StringComparison.Ordinal);
        Assert.Contains("{64, 3}", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The style layer comes free with the cross-references.</b>
    /// </summary>
    /// <remarks>
    /// A SCIP <c>Occurrence</c> carries a symbol and a syntax kind over one span, so the
    /// pass that fills the reference layer has already read what the highlighting needs.
    /// A line with no syntax on it writes no fact, which is why there are two rather than
    /// three.
    /// </remarks>
    [Fact]
    public void A_converted_index_carries_the_syntax_it_was_given()
    {
        using var server = FjordServer.Serving("styles", "index.sigla");
        using var connection = Converted(server, "styles");

        var styled = connection.Query(
            """
            L where
              src.FileLineStyles {file = F, line = L};
              F = src.File "src/greet.ts"
            """).Rows;

        Assert.Equal(2, styled.Count);

        // And the database says how to read them, which is the whole contract for an
        // opaque payload: a consumer that does not know this name renders those lines
        // plain rather than guessing.
        var encoding = connection.Query(
            """V where config.Setting {dimension = "style-encoding", value = V}""").Rows;

        Assert.Single(encoding);
        Assert.Contains("scip-syntax-1", Rendered(encoding[0]), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The <c>scip-syntax-1</c> payload means what the format says it means.</b>
    /// </summary>
    /// <remarks>
    /// <c>styles</c> is opaque to fjord — it stores the bytes and defines nothing about
    /// them — so nothing but a decoder here can say that the producer and the name it
    /// published agree. Three unsigned varints per token, in the order the format names:
    /// the byte start from the line's own start, the byte length, and SCIP's
    /// <c>SyntaxKind</c>. A producer that wrote them in another order, or in UTF-16
    /// columns, would round-trip, publish the same encoding name, and render every line
    /// wrong.
    /// </remarks>
    [Fact]
    public void The_style_payload_is_three_varints_per_token()
    {
        using var server = FjordServer.Serving("style-bytes", "index.sigla");
        using var connection = Converted(server, "style-bytes");

        // `export function greet(who: string): string {` is all ASCII, so the byte
        // columns are the character ones: two keywords, the function name, the parameter.
        Assert.Equal(
            [(0L, 6L, 4L), (7L, 8L, 4L), (16L, 5L, 15L), (22L, 3L, 6L)],
            Styles(connection, "src/greet.ts", 1));

        // The template-literal line: `who` is character 18 of it and byte 19 of it,
        // because the accented letter before it is two UTF-8 bytes. A column copied
        // rather than converted would say 18 here and agree everywhere else.
        Assert.Equal([(19L, 3L, 6L)], Styles(connection, "src/greet.ts", 2));
    }

    /// <summary>One line's <c>scip-syntax-1</c> payload, decoded to its triples.</summary>
    private static List<(long Start, long Length, long Kind)> Styles(
        FjordConnection connection, string path, long line)
    {
        var value = Assert.Single(connection.Query(
            $$"""
            X.value where
              F = src.File "{{path}}";
              X = src.FileLineStyles {file = F, line = {{line}}}
            """).Rows);

        var payload = Assert.IsType<FjordValue.Bytes>(
            Assert.IsType<FjordValue.Record>(value).Fields[0]).Value.Span;

        var tokens = new List<(long, long, long)>();
        var at = 0;

        while (at < payload.Length)
        {
            tokens.Add((
                (long)Varint.Read(payload, ref at),
                (long)Varint.Read(payload, ref at),
                (long)Varint.Read(payload, ref at)));
        }

        return tokens;
    }

    /// <summary>
    /// <b>A name is recovered from a descriptor when the index gives none.</b>
    /// </summary>
    /// <remarks>
    /// <c>display_name</c> is preferred wherever it is carried, because a producer knows
    /// its own language's spelling. Where it is absent the last descriptor is the name —
    /// and the backticks are why that is a scan rather than a search for the last dot: a
    /// file descriptor is <c>`greet.ts`</c>, and the dot inside it is part of the name.
    /// </remarks>
    [Theory]
    [InlineData("scip-typescript npm fixture 1.0.0 src/`greet.ts`/greet().", "greet")]
    [InlineData("scip-typescript npm fixture 1.0.0 src/`greet.ts`/greet().(who)", "who")]
    [InlineData("scip-typescript npm fixture 1.0.0 src/`greet.ts`/", "greet.ts")]
    [InlineData("scip-csharp nuget Fixture 1.0.0 Ledger/Core/Rectangle#", "Rectangle")]
    [InlineData("scip-csharp nuget Fixture 1.0.0 Ledger/Core/Store#[T]", "T")]
    [InlineData("scip-csharp nuget Fixture 1.0.0 Ledger/Core/Store#Add(+1).", "Add")]
    [InlineData("local 4", "4")]
    public void The_last_descriptor_is_the_name(string symbol, string expected) =>
        Assert.Equal(expected, Descriptors.NameOf(symbol));

    /// <summary>
    /// <b>A doubled space is an escaped space, not a field separator.</b>
    /// </summary>
    /// <remarks>
    /// SCIP's grammar says the scheme, manager, package name and version are "any UTF-8,
    /// escape spaces with double space", so a package whose name holds a space arrives as
    /// five fields with six spaces between the first five words. Counting single spaces
    /// takes the version for a descriptor and answers a name that is really a version
    /// number — in range, plausible, and wrong.
    /// </remarks>
    [Theory]
    [InlineData("scip-csharp nuget My  Package 1.0.0 Rectangle#", "Rectangle")]
    [InlineData("scip-csharp my  manager Fixture 1.0.0 Store#", "Store")]
    [InlineData("my  scheme nuget Fixture 1.0.0 Store#", "Store")]
    [InlineData("scip-csharp nuget Fixture 1.0  beta Store#", "Store")]
    public void An_escaped_space_is_not_a_field_separator(string symbol, string expected) =>
        Assert.Equal(expected, Descriptors.NameOf(symbol));
}
