using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>Roslyn's classifier, encoded the way LSP transmits semantic tokens.</b>
/// </para>
/// <para>
/// <c>src.FileLineStyles</c> holds opaque bytes and fjord has no opinion about them. This
/// is what a real client does with that freedom: take the tokeniser the platform already
/// ships — here <see cref="Classifier"/>, which is what Visual Studio colours with — and
/// write its output in a format the consumer already understands, rather than inventing a
/// vocabulary and folding a good one down into it.
/// </para>
/// <para>
/// The payload is LSP's <c>SemanticTokens.data</c>: five integers per token,
/// <c>[deltaLine, deltaStart, length, tokenType, tokenModifiers]</c>, all deltas. A
/// browser decodes it into a <c>Uint32Array</c> and hands it to Monaco's
/// <c>DocumentSemanticTokensProvider</c> unchanged. Integers are LEB128 varints rather
/// than fixed 32-bit words, which is most of the size: a delta, a length and a legend
/// index are all small, so a token costs about five bytes instead of twenty.
/// </para>
/// <para>
/// Facts are per line, so <c>deltaLine</c> is always 0 and <c>deltaStart</c> is relative
/// to the previous token on that line. That is a deliberate divergence from LSP, which
/// encodes a whole file: a windowed viewer seeks a line range, and a file-sized blob
/// would have to be fetched and decoded whole to draw forty lines.
/// </para>
/// <para>
/// The legend is <see cref="Legend"/> — Roslyn's own <see cref="ClassificationTypeNames"/>,
/// in a fixed order, published rather than folded. It is named by
/// <c>config.Setting {dimension = "style-encoding"}</c> as <see cref="Encoding"/>, so a
/// consumer knows both the byte format and the vocabulary from one value.
/// </para>
/// </summary>
internal static class SemanticTokens
{
    /// <summary>What <c>config.Setting {dimension = "style-encoding"}</c> carries.</summary>
    public const string Encoding = "roslyn-lsp-1";

    /// <summary>
    /// The token types, by legend index. Roslyn's <see cref="ClassificationTypeNames"/>
    /// verbatim, in a fixed order.
    /// </summary>
    /// <remarks>
    /// <b>Fixed, not discovered.</b> A legend built from the names a run happened to
    /// encounter would differ between two indexes of the same repository, and a sealed
    /// identity hashes the facts — so the order is stated here and never sorted at run
    /// time. Appending is safe and renumbering is not, exactly as for a schema union.
    /// </remarks>
    public static readonly string[] Legend =
    [
        ClassificationTypeNames.Text,                       // 0 — also the fallback
        ClassificationTypeNames.Keyword,
        ClassificationTypeNames.ControlKeyword,
        ClassificationTypeNames.Identifier,
        ClassificationTypeNames.ClassName,
        ClassificationTypeNames.RecordClassName,
        ClassificationTypeNames.StructName,
        ClassificationTypeNames.RecordStructName,
        ClassificationTypeNames.InterfaceName,
        ClassificationTypeNames.EnumName,
        ClassificationTypeNames.DelegateName,
        ClassificationTypeNames.TypeParameterName,
        ClassificationTypeNames.ModuleName,
        ClassificationTypeNames.NamespaceName,
        ClassificationTypeNames.MethodName,
        ClassificationTypeNames.ExtensionMethodName,
        ClassificationTypeNames.PropertyName,
        ClassificationTypeNames.FieldName,
        ClassificationTypeNames.EventName,
        ClassificationTypeNames.ConstantName,
        ClassificationTypeNames.LocalName,
        ClassificationTypeNames.ParameterName,
        ClassificationTypeNames.EnumMemberName,
        ClassificationTypeNames.LabelName,
        ClassificationTypeNames.Comment,
        ClassificationTypeNames.StringLiteral,
        ClassificationTypeNames.VerbatimStringLiteral,
        ClassificationTypeNames.StringEscapeCharacter,
        ClassificationTypeNames.NumericLiteral,
        ClassificationTypeNames.Operator,
        ClassificationTypeNames.OperatorOverloaded,
        ClassificationTypeNames.Punctuation,
        ClassificationTypeNames.PreprocessorKeyword,
        ClassificationTypeNames.PreprocessorText,
        ClassificationTypeNames.ExcludedCode,
        ClassificationTypeNames.XmlDocCommentText,
        ClassificationTypeNames.XmlDocCommentName,
        ClassificationTypeNames.XmlDocCommentDelimiter,
        ClassificationTypeNames.XmlDocCommentAttributeName,
        ClassificationTypeNames.XmlDocCommentAttributeValue,
        ClassificationTypeNames.XmlDocCommentAttributeQuotes,
        ClassificationTypeNames.XmlDocCommentComment,
        ClassificationTypeNames.XmlDocCommentCDataSection,
        ClassificationTypeNames.XmlDocCommentEntityReference,
        ClassificationTypeNames.XmlDocCommentProcessingInstruction,
        ClassificationTypeNames.RegexComment,
        ClassificationTypeNames.RegexCharacterClass,
        ClassificationTypeNames.RegexAnchor,
        ClassificationTypeNames.RegexQuantifier,
        ClassificationTypeNames.RegexGrouping,
        ClassificationTypeNames.RegexAlternation,
        ClassificationTypeNames.RegexText,
        ClassificationTypeNames.RegexSelfEscapedCharacter,
        ClassificationTypeNames.RegexOtherEscape,
    ];

    /// <summary>
    /// The modifier bits, by bit position — LSP's <c>tokenModifiers</c>.
    /// </summary>
    /// <remarks>
    /// Roslyn expresses a modifier as a <i>second, overlapping</i> classified span over the
    /// same text: a static method's name comes back as both <c>method name</c> and
    /// <c>static symbol</c>. A flat list of spans cannot hold that, and it is not a token
    /// type — it is exactly what LSP's modifier bitfield is for, which is the second reason
    /// to use LSP's encoding rather than a run-length one.
    /// </remarks>
    public static readonly string[] ModifierLegend = [ClassificationTypeNames.StaticSymbol];

    private static readonly Dictionary<string, int> Types =
        Legend.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.Ordinal);

    private static readonly Dictionary<string, int> Modifiers =
        ModifierLegend.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.Ordinal);

    /// <summary>One line's tokens, already encoded.</summary>
    internal readonly record struct LineTokens(int Line, byte[] Payload);

    /// <summary>
    /// Encode every classified span into one payload per line, skipping lines with no
    /// tokens on them.
    /// </summary>
    /// <remarks>
    /// Three things this has to get right, each of which Roslyn's output forces:
    /// <list type="bullet">
    /// <item>A span may cross a line — a block comment, a raw string — and an LSP token may
    /// not, so it is split at each line boundary.</item>
    /// <item>Spans overlap where a modifier applies, so a modifier span folds into the bits
    /// of the token it covers rather than becoming a token of its own.</item>
    /// <item>Roslyn emits nothing for whitespace, and LSP wants no filler, so gaps are
    /// simply absent — a consumer renders uncovered text as plain.</item>
    /// </list>
    /// </remarks>
    public static List<LineTokens> Encode(IEnumerable<ClassifiedSpan> spans, SourceText text)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(text);

        // Fold the modifier spans out first, so what remains is one type per span.
        var typed = new List<(TextSpan Span, int Type)>();
        var modifierSpans = new List<(TextSpan Span, int Bit)>();
        foreach (var span in spans)
        {
            if (Modifiers.TryGetValue(span.ClassificationType, out var bit))
            {
                modifierSpans.Add((span.TextSpan, bit));
            }
            else if (Types.TryGetValue(span.ClassificationType, out var type))
            {
                typed.Add((span.TextSpan, type));
            }
            // A name this build does not know is dropped rather than guessed at: a wrong
            // legend index would colour the token as something else, which is worse than
            // leaving it plain.
        }

        var byLine = new SortedDictionary<int, List<(int Start, int Length, int Type, int Mods)>>();
        foreach (var (span, type) in typed)
        {
            var mods = 0;
            foreach (var (modSpan, bit) in modifierSpans)
            {
                if (modSpan.OverlapsWith(span) || modSpan == span)
                {
                    mods |= 1 << bit;
                }
            }

            // Split at line boundaries: an LSP token never crosses one.
            var first = text.Lines.GetLineFromPosition(span.Start);
            var last = text.Lines.GetLineFromPosition(Math.Max(span.Start, span.End - 1));
            for (var n = first.LineNumber; n <= last.LineNumber; n++)
            {
                var line = text.Lines[n];
                var start = Math.Max(span.Start, line.Start);
                var end = Math.Min(span.End, line.End);
                if (end <= start)
                {
                    continue;
                }
                if (!byLine.TryGetValue(n, out var list))
                {
                    byLine[n] = list = [];
                }
                list.Add((start - line.Start, end - start, type, mods));
            }
        }

        var result = new List<LineTokens>(byLine.Count);
        foreach (var (line, tokens) in byLine)
        {
            tokens.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));

            var output = new List<byte>(tokens.Count * 5);
            var previousStart = 0;
            foreach (var (start, length, type, mods) in tokens)
            {
                // deltaLine is always 0: facts are per line, so a payload never spans one.
                PutVarint(output, 0);
                PutVarint(output, (uint)(start - previousStart));
                PutVarint(output, (uint)length);
                PutVarint(output, (uint)type);
                PutVarint(output, (uint)mods);
                previousStart = start;
            }
            result.Add(new LineTokens(line + 1, [.. output]));
        }
        return result;
    }

    private static void PutVarint(List<byte> output, uint value)
    {
        while (value >= 0x80)
        {
            output.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        output.Add((byte)value);
    }
}
