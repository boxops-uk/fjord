using Boxops.Fjord.Client;

namespace Boxops.Fjord.Scip;

/// <summary>
/// The style layer, which a SCIP index carries whether or not anybody wanted it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Free, and that is the whole argument for writing it.</b> A SCIP <c>Occurrence</c>
/// carries a symbol <i>and</i> a syntax kind over one span, so the pass that fills the
/// cross-reference layer has already read everything the highlighting needs. A separate
/// classifier run would be a second pass over the same bytes.
/// </para>
/// <para>
/// <b>The format is this producer's, and it is named.</b> <c>src.FileLineStyles.styles</c>
/// is opaque: fjord stores the bytes and defines nothing about them, so a database says
/// how to read them through <c>config.Setting {dimension = "style-encoding"}</c> and a
/// consumer that does not recognise the name renders those lines plain. This one is three
/// unsigned varints per token — start column in bytes from the line's start, length in
/// bytes, and SCIP's own <c>SyntaxKind</c> number — which keeps the vocabulary a citation
/// rather than an invention.
/// </para>
/// </remarks>
internal static class Styles
{
    /// <summary>What <c>config.Setting {dimension = "style-encoding"}</c> carries.</summary>
    public const string Encoding = "scip-syntax-1";

    /// <summary>One fact per line that has any syntax on it, and none for the rest.</summary>
    /// <remarks>
    /// A line with no tokens writes no fact: absent means unhighlighted, which is the
    /// common case and the reason this costs so much less than the cross-reference layer.
    /// </remarks>
    public static int Emit(FactSink sink, FjordFact file, Document document, Lines lines)
    {
        var byLine = new Dictionary<int, List<(long Start, long Length, int Kind)>>();

        foreach (var occurrence in document.Occurrences)
        {
            // A token with no syntax kind is one the indexer declined to classify, and a
            // run of zeroes is not highlighting.
            if (occurrence.SyntaxKind == 0)
            {
                continue;
            }

            // Only single-line tokens. A multi-line one — a block comment, a raw string —
            // would need splitting at each line's end, and SCIP emits those as one
            // occurrence; a payload keyed by line cannot hold half of one, so it is left
            // out rather than truncated to its first line.
            if (occurrence.StartLine != occurrence.EndLine)
            {
                continue;
            }

            var line = occurrence.StartLine;

            if (line < 0 || line >= lines.Count)
            {
                continue;
            }

            var row = lines[line];
            var start = lines.Offset(line, occurrence.StartCharacter, document.PositionEncoding)
                - row.Start;
            var end = lines.Offset(line, occurrence.EndCharacter, document.PositionEncoding)
                - row.Start;

            if (end <= start)
            {
                continue;
            }

            if (!byLine.TryGetValue(line, out var tokens))
            {
                tokens = [];
                byLine[line] = tokens;
            }

            tokens.Add((start, end - start, occurrence.SyntaxKind));
        }

        var written = 0;

        foreach (var line in byLine.Keys.Order())
        {
            var tokens = byLine[line];

            // Sorted, because a payload a consumer has to sort is a payload every consumer
            // sorts. SCIP's occurrences are conventionally in order and this does not rely
            // on it.
            tokens.Sort((left, right) => left.Start.CompareTo(right.Start));

            var buffer = new ByteBuffer();

            foreach (var (start, length, kind) in tokens)
            {
                Varint.Write(buffer, (ulong)start);
                Varint.Write(buffer, (ulong)length);
                Varint.Write(buffer, (ulong)kind);
            }

            sink.Add(
                ScipFacts.FileLineStyles,
                ScipFacts.FileLineStylesFact(file, line + 1, buffer.ToArray()));

            written++;
        }

        return written;
    }
}
