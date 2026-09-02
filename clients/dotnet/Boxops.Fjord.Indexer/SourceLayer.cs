using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>The source layer's arithmetic: a file's line table and the summary of it.</b>
/// </para>
/// <para>
/// Everything here is countable from the text alone — no compiler, no workspace — which
/// is what <c>src.sigla</c> promises about the layer. It is separated from the walk
/// because it is the only new maths a producer of this schema owes, and because a walk
/// needs a workspace while a property does not.
/// </para>
/// </summary>
internal static class SourceLayer
{
    /// <summary>
    /// The cap on any stored string. A single pathological line — a generated blob, a
    /// minified bundle — otherwise sets the widest row in the database.
    /// </summary>
    public const int MaxText = 4096;

    /// <summary>One row of <c>src.FileLine</c>, and the <c>src.FileLineAt</c> beside it.</summary>
    internal readonly record struct Row(long Number, string Text, long Start, long Bytes, long CStart);

    /// <summary>What <c>src.FileInfo</c> carries.</summary>
    internal readonly record struct Summary(long Bytes, long Lines, bool EndsInNewline);

    /// <summary>Clip a string to <see cref="MaxText"/>.</summary>
    public static string Clip(string text) => text.Length <= MaxText ? text : text[..MaxText];

    /// <summary>
    /// The line table: one row per line, one-based, with the two offsets that locate it
    /// and the file's own totals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Roslyn ends a newline-terminated file with a line that is not one.</b>
    /// <c>SourceText.Lines</c> reports an empty final line starting at the end of the
    /// text, and writing a fact for it would put a phantom last line in nearly every file
    /// of every index — while <c>src.sigla</c> states the opposite reading in as many
    /// words: <c>"a\nb\n"</c> is two lines, and so is <c>"a\nb"</c>. That empty line is
    /// also the only evidence in <c>Lines</c> that the file ends in a terminator at all,
    /// so it is what <c>endsInNewline</c> is read from rather than sniffed from the last
    /// character — a lone <c>\r</c> and U+2028 are terminators to Roslyn too, and a rule
    /// that disagreed with its splitting would put the count out by one on exactly the
    /// files nobody tests with.
    /// </para>
    /// <para>
    /// <b>Two offsets, and only one is free.</b> <c>cstart</c> is Roslyn's own position;
    /// <c>start</c> is UTF-8 and is accumulated, because <c>GetByteCount</c> over a prefix
    /// per line is quadratic in the file. <c>bytes</c> measures what is <i>stored</i> after
    /// clipping while <c>start</c> advances by the whole line, since a clipped line still
    /// occupies its full width and an offset that pretended otherwise would put every
    /// later line in the wrong place.
    /// </para>
    /// </remarks>
    public static (List<Row> Rows, Summary Info) LineTable(SourceText text)
    {
        var rows = new List<Row>(text.Lines.Count);

        // An empty file is a complete index of nothing. Roslyn still reports one line for
        // it, and that line would be indistinguishable from a file holding one blank line.
        if (text.Length == 0)
        {
            return (rows, new Summary(0, 0, false));
        }

        var endsInNewline = text.Lines[^1].Start == text.Length;
        var count = endsInNewline ? text.Lines.Count - 1 : text.Lines.Count;

        long start = 0;
        for (var index = 0; index < count; index++)
        {
            var line = text.Lines[index];
            var stored = Clip(line.ToString());

            rows.Add(new Row(
                index + 1,
                stored,
                start,
                Encoding.UTF8.GetByteCount(stored),
                line.Start));

            // The line, plus its terminator, re-measured in bytes: the span's own length
            // is UTF-16 and cannot be added to a byte offset.
            start += Encoding.UTF8.GetByteCount(text.ToString(line.SpanIncludingLineBreak));
        }

        return (rows, new Summary(start, count, endsInNewline));
    }
}
