using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    /// File extension to the language it names — the vocabulary's spelling where there is
    /// one, and the extension itself where there is not.
    /// </summary>
    /// <remarks>
    /// The values are resolved against <see cref="DotnetIndex.LanguageNames"/> by
    /// <see cref="DotnetIndex.FileLanguageFact"/>, so a name misspelled here becomes an
    /// <c>other</c> fact rather than a wrong one — and a test asserts every value in this
    /// table is in that vocabulary.
    /// </remarks>
    private static readonly Dictionary<string, string> Languages = new(StringComparer.Ordinal)
    {
        ["cs"] = "csharp",
        ["ts"] = "typescript",
        ["js"] = "javascript",
        ["mjs"] = "javascript",
        ["cjs"] = "javascript",
        ["tsx"] = "tsx",
        ["jsx"] = "jsx",
        ["rs"] = "rust",
        ["py"] = "python",
        ["java"] = "java",
        ["cpp"] = "cpp",
        ["cc"] = "cpp",
        ["cxx"] = "cpp",
        ["hpp"] = "cpp",
        ["hh"] = "cpp",
        ["hxx"] = "cpp",
        // `.h` is a coin toss between C and C++ that no extension can settle; C is the
        // reading that is right for a header no C++ file includes, and the wrong one here
        // is a filter's answer rather than a broken join.
        ["c"] = "c",
        ["h"] = "c",
        ["go"] = "go",
        ["json"] = "json",
        ["yaml"] = "yaml",
        ["yml"] = "yaml",
        ["md"] = "markdown",
        ["markdown"] = "markdown",
        ["css"] = "css",
        ["html"] = "html",
        ["htm"] = "html",
        ["sql"] = "sql",
        ["sh"] = "shell",
        ["bash"] = "shell",
        // A project file is XML, and the project *graph* is `msbuild`'s layer rather than
        // this one — so this says what the bytes are and claims nothing more.
        ["xml"] = "xml",
        ["csproj"] = "xml",
        ["props"] = "xml",
        ["targets"] = "xml",
        ["slnx"] = "xml",
        ["proto"] = "proto",
    };

    /// <summary>
    /// What <paramref name="path"/> is written in, by extension: a name from
    /// <c>src.Language</c> where one fits, the extension itself where none does, and the
    /// empty string for a file with no extension.
    /// </summary>
    /// <remarks>
    /// By extension and not by the compiler that parsed it, so that the answer is the same
    /// for a file no compilation reached. A name that is not in the vocabulary is not a
    /// failure: it is what <c>other : string = 0</c> exists for, and a consumer filtering
    /// on language can still see the file.
    /// </remarks>
    public static string LanguageName(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Length <= 1)
        {
            return string.Empty;
        }

        var bare = extension[1..].ToLowerInvariant();
        return Languages.TryGetValue(bare, out var language) ? language : bare;
    }

    /// <summary>
    /// A content hash of the file: <b>SHA-256 over the UTF-8 encoding of the decoded
    /// text</b>, lowercase hex.
    /// </summary>
    /// <remarks>
    /// <b>Over the text, not over what is on disk</b>, because every byte number in this
    /// database — <c>FileInfo.bytes</c>, <c>FileLine.start</c>, every <c>ByteSpan</c> — is
    /// an offset into exactly these bytes. A byte-order mark and a non-UTF-8 encoding are
    /// decoded away before any of them is counted, so hashing the raw file would make one
    /// file report two lengths and a consumer could not tell which its offsets belonged
    /// to. The cost is that <c>sha256sum</c> disagrees for a file with a BOM, which is why
    /// the algorithm owes <c>config.Setting {dimension = "digest"}</c> a value that says
    /// so rather than just naming the hash.
    /// </remarks>
    public static string Digest(SourceText text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));

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

    /// <summary>
    /// <b>UTF-16 positions to UTF-8 byte offsets, for one file.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Roslyn counts UTF-16 code units everywhere and every offset in this database is a
    /// count of UTF-8 bytes, so a span from the compiler has to be converted before it can
    /// be a <c>src.ByteSpan</c>. Doing it by re-encoding the prefix per span is quadratic
    /// in the file; the line table already holds each line's byte start, so a conversion is
    /// that plus the bytes of one line's prefix.
    /// </para>
    /// <para>
    /// <b>The failure this exists to prevent is silent.</b> A UTF-16 offset used as a byte
    /// offset is in range for any file and points at the wrong text, which reads as a
    /// rendering bug three layers away.
    /// </para>
    /// </remarks>
    internal sealed class Offsets(SourceText text, List<Row> rows, Summary info)
    {
        /// <summary>The UTF-8 byte offset of a UTF-16 position.</summary>
        public long Of(int position)
        {
            if (position <= 0 || rows.Count == 0)
            {
                return 0;
            }

            var line = text.Lines.GetLinePosition(Math.Min(position, text.Length));

            // **Past the last line the table holds** — the phantom line Roslyn reports at
            // the end of a newline-terminated file. The answer is the file's own length,
            // *including* that terminator: reconstructing it from the last row's start
            // plus its text loses exactly the newline, which is the off-by-one this
            // conversion exists to avoid.
            if (line.Line >= rows.Count)
            {
                return info.Bytes;
            }

            // **The line *including* its terminator.** A position can land inside a CRLF
            // pair, and measuring only the line's text would drop the `\r` — exact here
            // rather than narrowed to "positions a token boundary can take", because the
            // set of representable positions is not something a caller should have to
            // reason about.
            var whole = LineText(line.Line);
            var characters = Math.Min(line.Character, whole.Length);

            return rows[line.Line].Start + Encoding.UTF8.GetByteCount(whole[..characters]);
        }

        /// <summary>The one-based line a UTF-16 position is on.</summary>
        /// <remarks>
        /// <para>
        /// One-based to match <c>src.FileLine</c>, which every other line number in this
        /// database is keyed by — Roslyn counts from zero.
        /// </para>
        /// <para>
        /// <b>Clamped to the last line the table holds.</b> Roslyn reports a phantom empty
        /// line at the end of a newline-terminated file and no row is written for it, so
        /// the unclamped number is one a <c>src.FileLine</c> join answers nothing for —
        /// in range for the file, wrong for the database, and silent at every layer that
        /// carries it.
        /// </para>
        /// </remarks>
        public long Line(int position)
        {
            if (rows.Count == 0)
            {
                return 1;
            }

            var line = text.Lines.GetLinePosition(Math.Min(position, text.Length)).Line + 1;

            return Math.Min(line, rows.Count);
        }

        /// <summary>
        /// A Roslyn span as a <c>src.ByteSpan</c> — a byte start and a byte length.
        /// </summary>
        /// <remarks>
        /// The length is the difference of the two converted ends rather than a converted
        /// length: a span holding a non-BMP codepoint is longer in bytes than in code
        /// units, and converting the length alone would keep the UTF-16 number.
        /// </remarks>
        public (long Start, long Length) Span(TextSpan span)
        {
            var start = Of(span.Start);
            return (start, Of(span.End) - start);
        }

        private int _cachedLine = -1;
        private string _cached = string.Empty;

        /// <summary>
        /// The line and its terminator, unclipped — what the offsets were counted over.
        /// </summary>
        /// <remarks>
        /// Cached one deep, because the walk converts many spans per line and each miss
        /// materialises the line. A single entry is enough: references arrive in document
        /// order.
        /// </remarks>
        private string LineText(int line)
        {
            if (line != _cachedLine)
            {
                _cached = text.ToString(text.Lines[line].SpanIncludingLineBreak);
                _cachedLine = line;
            }

            return _cached;
        }
    }
}
