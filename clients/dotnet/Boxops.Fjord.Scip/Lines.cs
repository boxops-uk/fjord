using System.Text;

namespace Boxops.Fjord.Scip;

/// <summary>
/// A file's lines, in the three units that have to agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three, and not two.</b> A byte offset is what this schema counts; a UTF-16 offset is
/// what a consumer counting code units needs and what the line table carries beside it;
/// and a character offset within a line is what SCIP hands over. A converter that assumed
/// any two of the three were the same would be right on ASCII and wrong on the first
/// comment anybody wrote in their own language.
/// </para>
/// <para>
/// <b>The last line has no row unless there is something on it.</b> A file ending in a
/// newline has no line after it — that is the terminator, not an empty line — and a table
/// with a row for it sends every offset-past-the-end lookup to a line that is not there.
/// </para>
/// </remarks>
internal sealed class Lines
{
    /// <summary>One line: where it starts, how long it is, and what is on it.</summary>
    internal readonly record struct Row(string Text, long Start, long Bytes, long CStart);

    private readonly List<Row> _rows = [];

    /// <summary>The whole file's length in UTF-8 bytes.</summary>
    public long Bytes { get; private set; }

    public bool EndsInNewline { get; private set; }

    public int Count => _rows.Count;

    public Row this[int index] => _rows[index];

    /// <summary>Read a file's text into its lines.</summary>
    public static Lines Of(string text)
    {
        var lines = new Lines
        {
            Bytes = Encoding.UTF8.GetByteCount(text),
            EndsInNewline = text.Length > 0 && text[^1] == '\n',
        };

        long start = 0;
        long cstart = 0;
        var at = 0;

        while (at < text.Length)
        {
            var newline = text.IndexOf('\n', at);
            var end = newline < 0 ? text.Length : newline;
            var content = text[at..end];

            // The line's own bytes, without the terminator: `bytes` is the length of the
            // text, and a consumer adding it to `start` lands on the newline.
            var bytes = Encoding.UTF8.GetByteCount(content);

            lines._rows.Add(new Row(content, start, bytes, cstart));

            // Past the terminator, which is one byte and one code unit.
            var stride = bytes + (newline < 0 ? 0 : 1);
            start += stride;
            cstart += content.Length + (newline < 0 ? 0 : 1);
            at = end + 1;
        }

        return lines;
    }

    /// <summary>
    /// A SCIP <c>(line, character)</c> as a UTF-8 byte offset from the start of the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The encoding is the index's to declare, and unspecified is refused by being
    /// read as UTF-16.</b> SCIP's own text says a new indexer must not leave it
    /// unspecified; the ones that do are the ones written before the field existed, and
    /// those were JVM and TypeScript indexers counting code units. Guessing UTF-8 there
    /// would be right for the ASCII half of every file and wrong for the rest.
    /// </para>
    /// <para>
    /// A character past the end of its line clamps to the line's end rather than running
    /// into the next one: an index that says so is wrong about that occurrence, and a
    /// span reaching into another line would be wrong about two.
    /// </para>
    /// </remarks>
    public long Offset(int line, int character, int encoding)
    {
        if (line < 0 || _rows.Count == 0)
        {
            return 0;
        }

        if (line >= _rows.Count)
        {
            return Bytes;
        }

        var row = _rows[line];

        if (character <= 0)
        {
            return row.Start;
        }

        return row.Start + encoding switch
        {
            // UTF8CodeUnitOffsetFromLineStart — already bytes.
            1 => Math.Min(character, row.Bytes),

            // UTF32CodeUnitOffsetFromLineStart — scalar values.
            3 => BytesForScalars(row.Text, character),

            // UTF16CodeUnitOffsetFromLineStart, and unspecified, which is what the
            // indexers that leave it unset were counting.
            _ => Encoding.UTF8.GetByteCount(
                row.Text[..Math.Min(character, row.Text.Length)]),
        };
    }

    private static long BytesForScalars(string text, int scalars)
    {
        var taken = 0;
        var at = 0;

        while (at < text.Length && taken < scalars)
        {
            at += char.IsHighSurrogate(text[at]) && at + 1 < text.Length ? 2 : 1;
            taken++;
        }

        return Encoding.UTF8.GetByteCount(text[..at]);
    }
}
