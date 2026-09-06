using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The reference client's answer to an opaque schema field.</b>
/// </para>
/// <para>
/// <c>src.FileLineStyles</c> holds bytes and fjord asks no questions. These assert that
/// what this indexer puts there is LSP semantic-tokens data over Roslyn's own legend —
/// decodable by anything that already speaks LSP, with no fjord-specific vocabulary.
/// </para>
/// </summary>
public sealed class SemanticTokensTests
{
    private static (IReadOnlyList<ClassifiedSpan> Spans, SourceText Text) Classify(string source)
    {
        using var ws = new AdhocWorkspace();
        var proj = ws.AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var doc = ws.AddDocument(proj.Id, "A.cs", SourceText.From(source));
        var spans = Classifier
            .GetClassifiedSpansAsync(doc, new TextSpan(0, source.Length), CancellationToken.None)
            .GetAwaiter().GetResult();
        return (spans.ToList(), SourceText.From(source));
    }

    /// <summary>Read a payload back the way a browser would.</summary>
    private static List<(uint DeltaLine, uint DeltaStart, uint Length, uint Type, uint Mods)> Decode(byte[] payload)
    {
        var at = 0;
        uint Next()
        {
            uint value = 0;
            var shift = 0;
            while (true)
            {
                var b = payload[at++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
            }
        }

        var tokens = new List<(uint, uint, uint, uint, uint)>();
        while (at < payload.Length)
        {
            tokens.Add((Next(), Next(), Next(), Next(), Next()));
        }
        return tokens;
    }

    /// <summary>The legend is a published list, not one discovered per run.</summary>
    [Fact]
    public void The_legend_is_fixed_and_has_no_duplicates()
    {
        Assert.Equal(SemanticTokens.Legend.Length, SemanticTokens.Legend.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ClassificationTypeNames.Text, SemanticTokens.Legend[0]);
        Assert.Contains(ClassificationTypeNames.Keyword, SemanticTokens.Legend);
        Assert.Contains(ClassificationTypeNames.MethodName, SemanticTokens.Legend);
        Assert.Equal("roslyn-lsp-1", SemanticTokens.Encoding);
    }

    /// <summary>
    /// Tokens decode to LSP's five-integer shape, and the deltas reconstruct the columns
    /// Roslyn reported.
    /// </summary>
    [Fact]
    public void A_line_encodes_to_lsp_semantic_tokens()
    {
        var (spans, text) = Classify("int x = 1;");
        var lines = SemanticTokens.Encode(spans, text);

        var line = Assert.Single(lines);
        Assert.Equal(1, line.Line);

        var tokens = Decode(line.Payload);
        Assert.NotEmpty(tokens);

        // Every token is on the line, and deltaLine is always 0 because facts are per line.
        Assert.All(tokens, t => Assert.Equal(0u, t.DeltaLine));

        // Reconstruct absolute columns and compare against the classifier's own spans.
        var columns = new List<uint>();
        uint at = 0;
        foreach (var t in tokens)
        {
            at += t.DeltaStart;
            columns.Add(at);
        }
        var expected = spans
            .Where(s => SemanticTokens.Legend.Contains(s.ClassificationType, StringComparer.Ordinal))
            .Select(s => (uint)s.TextSpan.Start)
            .OrderBy(x => x);
        Assert.Equal(expected, columns);
    }

    /// <summary>
    /// A static member's modifier rides in the bitfield rather than becoming a second
    /// token — the overlap a flat run list could not have represented.
    /// </summary>
    [Fact]
    public void A_modifier_becomes_a_bit_not_a_token()
    {
        var (spans, text) = Classify("static int F() => 1;");

        // Roslyn really does report the overlap this is about.
        Assert.Contains(spans, s => s.ClassificationType == ClassificationTypeNames.StaticSymbol);

        var tokens = Decode(Assert.Single(SemanticTokens.Encode(spans, text)).Payload);
        var methodType = (uint)Array.IndexOf(SemanticTokens.Legend, ClassificationTypeNames.MethodName);

        var method = Assert.Single(tokens, t => t.Type == methodType);
        Assert.Equal(1u, method.Mods);
        // And it did not also become a token of its own.
        Assert.DoesNotContain(tokens, t => t.Mods != 0 && t.Type != methodType);
    }

    /// <summary>A span crossing a line is split, because an LSP token may not cross one.</summary>
    [Fact]
    public void A_multiline_comment_is_split_per_line()
    {
        var (spans, text) = Classify("/* one\ntwo\nthree */ int x = 1;");
        var lines = SemanticTokens.Encode(spans, text);

        Assert.Equal([1, 2, 3], lines.Select(l => l.Line));
        var commentType = (uint)Array.IndexOf(SemanticTokens.Legend, ClassificationTypeNames.Comment);
        foreach (var line in lines)
        {
            Assert.Contains(Decode(line.Payload), t => t.Type == commentType);
        }
    }

    /// <summary>A line with nothing on it writes no fact at all.</summary>
    [Fact]
    public void A_blank_line_produces_no_fact()
    {
        var (spans, text) = Classify("int x = 1;\n\nint y = 2;");
        Assert.Equal([1, 3], SemanticTokens.Encode(spans, text).Select(l => l.Line));
    }

    /// <summary>
    /// The payload is meaningfully smaller than the fixed-width array LSP describes,
    /// which is the whole reason for varints.
    /// </summary>
    [Fact]
    public void Varints_beat_fixed_width_words()
    {
        var (spans, text) = Classify(
            "public sealed class Order { public int Total() => 1 + 2; } // a comment");
        var line = Assert.Single(SemanticTokens.Encode(spans, text));
        var tokens = Decode(line.Payload).Count;

        Assert.Equal(tokens * 5 * sizeof(uint), tokens * 20);
        Assert.True(line.Payload.Length < tokens * 20,
            $"{line.Payload.Length} bytes for {tokens} tokens is no better than fixed width");
    }
}
