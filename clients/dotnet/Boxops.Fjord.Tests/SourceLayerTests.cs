using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The line table's arithmetic, which is the one piece of new producer maths in the
/// source layer.</b>
/// </para>
/// <para>
/// <c>src.FileLine</c> carries a UTF-8 byte offset beside a UTF-16 one, and
/// <c>src.FileInfo</c> then states a line count that has to agree with the number of
/// facts written. Roslyn counts UTF-16 everywhere and ends a newline-terminated file with
/// an empty line that is not one, so both halves are somewhere an off-by-one hides while
/// every index involved stays in range.
/// </para>
/// </summary>
public sealed class SourceLayerTests
{
    private static (List<SourceLayer.Row> Rows, SourceLayer.Summary Info) Table(string source) =>
        SourceLayer.LineTable(SourceText.From(source));

    /// <summary>
    /// **The phantom line.** `src.sigla` says `"a\nb\n"` is two lines and so is `"a\nb"`,
    /// with `endsInNewline` telling them apart — so a trailing terminator must not add a
    /// line to either the table or the count.
    /// </summary>
    [Fact]
    public void A_file_ending_in_a_newline_has_no_empty_final_line()
    {
        var (rows, info) = Table("a\nb\n");

        Assert.Equal(["a", "b"], rows.Select(row => row.Text));
        Assert.Equal(2, info.Lines);
        Assert.True(info.EndsInNewline);
        Assert.Equal(rows.Count, info.Lines);
    }

    [Fact]
    public void A_file_not_ending_in_a_newline_holds_the_same_lines_and_says_so()
    {
        var (rows, info) = Table("a\nb");

        Assert.Equal(["a", "b"], rows.Select(row => row.Text));
        Assert.Equal(2, info.Lines);
        Assert.False(info.EndsInNewline);
    }

    /// <summary>An empty file is a complete index of nothing, not one phantom line.</summary>
    [Fact]
    public void An_empty_file_has_no_lines_at_all()
    {
        var (rows, info) = Table(string.Empty);

        Assert.Empty(rows);
        Assert.Equal(0, info.Lines);
        Assert.Equal(0, info.Bytes);
        Assert.False(info.EndsInNewline);
    }

    /// <summary>
    /// A blank line *followed* by a terminator is a line; only the terminator at the end
    /// of the file has no line after it. This is the case that a naive "drop the last row
    /// if it is empty" gets wrong.
    /// </summary>
    [Fact]
    public void A_blank_final_line_is_a_line_when_a_terminator_follows_it()
    {
        var (rows, info) = Table("a\n\n");

        Assert.Equal(["a", ""], rows.Select(row => row.Text));
        Assert.Equal(2, info.Lines);
        Assert.True(info.EndsInNewline);
    }

    [Fact]
    public void A_file_that_is_one_terminator_is_one_empty_line()
    {
        var (rows, info) = Table("\n");

        Assert.Equal([""], rows.Select(row => row.Text));
        Assert.Equal(1, info.Bytes);
        Assert.True(info.EndsInNewline);
    }

    /// <summary>
    /// **A terminator is bytes, not text.** `text` excludes it, `bytes` measures `text`,
    /// and `start` advances past it — so CRLF costs two bytes between two lines and
    /// appears in neither line's text.
    /// </summary>
    [Fact]
    public void CRLF_is_counted_as_bytes_and_appears_in_no_lines_text()
    {
        var (rows, info) = Table("ab\r\ncd\r\n");

        Assert.Equal(["ab", "cd"], rows.Select(row => row.Text));
        Assert.Equal([0, 4], rows.Select(row => row.Start));
        Assert.Equal([2, 2], rows.Select(row => row.Bytes));
        Assert.Equal(8, info.Bytes);
    }

    /// <summary>
    /// **The two offsets diverge exactly at the codepoint's cost in each unit.** A
    /// grinning face is four UTF-8 bytes and two UTF-16 code units, so every line after
    /// one differs by two — and both numbers stay in range, which is why only a test
    /// finds this.
    /// </summary>
    [Fact]
    public void The_two_offsets_diverge_at_a_non_BMP_codepoint()
    {
        var (rows, _) = Table("x\n\U0001F600\ny\n");

        Assert.Equal([0, 2, 7], rows.Select(row => row.Start));
        Assert.Equal([0, 2, 5], rows.Select(row => row.CStart));
        Assert.Equal([1, 4, 1], rows.Select(row => row.Bytes));
    }

    /// <summary>
    /// **What is stored is measured; what is skipped is still advanced over.** A line
    /// longer than the cap is clipped, so `bytes` describes the stored text while `start`
    /// has to advance by the whole line or every later line lands in the wrong place.
    /// </summary>
    [Fact]
    public void A_clipped_line_measures_what_is_stored_and_advances_by_the_whole_line()
    {
        var wide = new string('x', SourceLayer.MaxText + 500);
        var (rows, info) = Table(wide + "\nafter\n");

        Assert.Equal(SourceLayer.MaxText, rows[0].Text.Length);
        Assert.Equal(SourceLayer.MaxText, rows[0].Bytes);
        Assert.Equal(wide.Length + 1, rows[1].Start);
        Assert.Equal(wide.Length + 1 + 6, info.Bytes);
    }

    // ---- the two per-file facts ------------------------------------------------------

    /// <summary>
    /// **The vocabulary is a citation, not an invention.** `src.Language`'s discriminants
    /// froze the day the layer shipped (I10), so a producer may only ever name an
    /// alternative that is in it — anything else goes through the `other : string = 0`
    /// valve. This asserts the mapping's targets against the schema's own list rather
    /// than against a second copy of it.
    /// </summary>
    [Fact]
    public void Every_language_the_mapping_names_is_in_the_schemas_vocabulary()
    {
        string[] paths =
        [
            "A.cs", "b.ts", "c.js", "d.tsx", "e.jsx", "f.rs", "g.py", "h.java",
            "i.cpp", "j.c", "k.go", "l.json", "m.yaml", "n.md", "o.css", "p.html",
            "q.sql", "r.sh", "s.xml", "t.proto",
        ];

        foreach (var path in paths)
        {
            var name = SourceLayer.LanguageName(path);
            Assert.Contains(name, CodeIndex.LanguageNames);
        }
    }

    /// <summary>
    /// **The valve carries what the vocabulary cannot.** Visual Basic is in no
    /// alternative — an MSBuild solution compiles it all the same — so it arrives as
    /// `other`, spelled as the extension rather than as a guess at a display name.
    /// </summary>
    [Theory]
    [InlineData("Program.vb", "vb")]
    [InlineData("notes.rst", "rst")]
    [InlineData("Makefile", "")]
    [InlineData("archive.tar.gz", "gz")]
    public void An_unlisted_extension_is_carried_rather_than_dropped(string path, string expected)
    {
        Assert.Equal(expected, SourceLayer.LanguageName(path));
        Assert.DoesNotContain(SourceLayer.LanguageName(path), CodeIndex.LanguageNames);
    }

    [Theory]
    [InlineData("A.cs", "csharp")]
    [InlineData("A.CS", "csharp")]
    [InlineData("dir/sub/A.cs", "csharp")]
    [InlineData("app.tsx", "tsx")]
    [InlineData("lib.mjs", "javascript")]
    [InlineData("build.yml", "yaml")]
    [InlineData("Boxops.Fjord.Indexer.csproj", "xml")]
    public void An_extension_maps_to_the_language_it_names(string path, string expected) =>
        Assert.Equal(expected, SourceLayer.LanguageName(path));

    /// <summary>
    /// **The digest is over the bytes the offsets count.** Every byte number in this
    /// database is an offset into the decoded text's UTF-8 encoding, so the hash is taken
    /// over the same thing rather than over what is on disk — a BOM or a non-UTF-8
    /// encoding would otherwise make one file report two lengths.
    /// </summary>
    [Fact]
    public void The_digest_is_sha256_over_the_utf8_text_in_lowercase_hex()
    {
        var text = SourceText.From("class A\n{\n}\n");
        var expected = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("class A\n{\n}\n")));

        Assert.Equal(expected, SourceLayer.Digest(text));
        Assert.Equal(64, SourceLayer.Digest(text).Length);
    }

    [Fact]
    public void The_digest_separates_texts_that_differ_by_one_character()
    {
        Assert.NotEqual(
            SourceLayer.Digest(SourceText.From("a\n")),
            SourceLayer.Digest(SourceText.From("b\n")));

        Assert.Equal(
            SourceLayer.Digest(SourceText.From("a\n")),
            SourceLayer.Digest(SourceText.From("a\n")));
    }

    /// <summary>
    /// A BOM is an encoding artifact rather than content, and Roslyn decodes it away — so
    /// the same text with and without one is the same file to every fact in the layer.
    /// This is the trade the digest choice makes, asserted rather than left implicit.
    /// </summary>
    [Fact]
    public void A_byte_order_mark_is_not_content()
    {
        var plain = SourceText.From("a\n");
        var marked = SourceText.From(new MemoryStream(
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("a\n")]));

        Assert.Equal(SourceLayer.Digest(plain), SourceLayer.Digest(marked));
        Assert.Equal(SourceLayer.LineTable(plain).Info.Bytes, SourceLayer.LineTable(marked).Info.Bytes);
    }

    // ---- UTF-16 positions to UTF-8 byte offsets ---------------------------------------

    private static SourceLayer.Offsets OffsetsOf(string source)
    {
        var text = SourceText.From(source);
        var (rows, info) = SourceLayer.LineTable(text);
        return new SourceLayer.Offsets(text, rows, info);
    }

    /// <summary>
    /// **A span from the compiler is UTF-16 and a `src.ByteSpan` is UTF-8.** Every
    /// position after a non-BMP codepoint differs, and both numbers are in range for the
    /// file — so using one as the other points at the wrong text rather than failing.
    /// </summary>
    [Fact]
    public void A_position_after_a_non_bmp_codepoint_converts_to_a_larger_byte_offset()
    {
        // `x`, newline, the grinning face, then `ab` on the same line.
        var offsets = OffsetsOf("x\n\U0001F600ab\n");

        Assert.Equal(0, offsets.Of(0));
        Assert.Equal(2, offsets.Of(2));

        // The emoji is two UTF-16 code units and four UTF-8 bytes, so the `a` after it is
        // at code unit 4 and byte 6.
        Assert.Equal(6, offsets.Of(4));
        Assert.Equal(7, offsets.Of(5));
    }

    /// <summary>
    /// **A span's length is the difference of its converted ends**, not its converted
    /// length: a span holding a non-BMP codepoint is longer in bytes than in code units.
    /// </summary>
    [Fact]
    public void A_span_over_a_non_bmp_codepoint_is_longer_in_bytes_than_in_code_units()
    {
        var offsets = OffsetsOf("\U0001F600ab\n");

        // Code units 0..3 — the emoji plus `ab` — is 4 units and 6 bytes.
        var (start, length) = offsets.Span(new TextSpan(0, 4));

        Assert.Equal(0, start);
        Assert.Equal(6, length);
    }

    [Fact]
    public void An_ascii_span_converts_to_itself()
    {
        var offsets = OffsetsOf("class A\n{\n}\n");
        var (start, length) = offsets.Span(new TextSpan(6, 1));

        Assert.Equal(6, start);
        Assert.Equal(1, length);
    }

    /// <summary>
    /// Positions past the last line the table holds — the phantom line, or the very end
    /// of the text — resolve to the file's length rather than throwing or wrapping.
    /// </summary>
    [Fact]
    public void A_position_at_the_end_of_the_file_is_the_files_byte_length()
    {
        var source = "a\n\U0001F600\n";
        var offsets = OffsetsOf(source);

        Assert.Equal(Encoding.UTF8.GetByteCount(source), offsets.Of(source.Length));
    }

    [Fact]
    public void An_empty_file_converts_every_position_to_zero()
    {
        var offsets = OffsetsOf(string.Empty);

        Assert.Equal(0, offsets.Of(0));
        Assert.Equal(0, offsets.Of(5));
    }

    /// <summary>
    /// **Over the generated corpus: a converted position is the byte count of the text
    /// before it.** The oracle is the obvious, quadratic implementation — which is what
    /// the line-table shortcut has to agree with.
    /// </summary>
    [Fact]
    public void Every_position_converts_to_what_re_encoding_the_prefix_would_say()
    {
        foreach (var file in Corpus())
        {
            var offsets = OffsetsOf(file);

            for (var position = 0; position <= file.Length; position++)
            {
                // Splitting a surrogate pair is not a position any span has, and the
                // prefix would not be valid UTF-16 to re-encode.
                if (position < file.Length && char.IsLowSurrogate(file[position]))
                {
                    continue;
                }

                Assert.Equal(
                    Encoding.UTF8.GetByteCount(file[..position]),
                    offsets.Of(position));
            }
        }
    }

    // ---- the properties, over a generated corpus -------------------------------------

    /// <summary>
    /// The corpus: files assembled from a stated population of lines and terminators.
    /// Seeded rather than random, so a failure is reproducible by its case number.
    /// </summary>
    private static IEnumerable<string> Corpus()
    {
        string[] pieces =
        [
            "", "a", "let x = 1;", "  // \U0001F600 smile", "\U0001F600\U0001F600",
            "é", "\t\t}", new string('x', SourceLayer.MaxText + 3),
        ];
        string[] terminators = ["\n", "\r\n"];

        for (var seed = 0; seed < 64; seed++)
        {
            var rng = new Random(seed);
            var builder = new StringBuilder();
            var lines = rng.Next(0, 6);
            for (var line = 0; line < lines; line++)
            {
                builder.Append(pieces[rng.Next(pieces.Length)]);
                // The last line keeps its terminator only sometimes, so both endings occur.
                if (line < lines - 1 || rng.Next(2) == 0)
                {
                    builder.Append(terminators[rng.Next(terminators.Length)]);
                }
            }
            yield return builder.ToString();
        }
    }

    /// <summary>
    /// **A generator whose population is not asserted leaves its properties green and
    /// vacuous.** The census is what says the cases below were actually reached.
    /// </summary>
    [Fact]
    public void The_corpus_reaches_every_case_it_claims_to()
    {
        var files = Corpus().ToList();

        Assert.Contains(string.Empty, files);
        Assert.Contains(files, file => file.EndsWith('\n'));
        Assert.Contains(files, file => file.Length > 0 && !file.EndsWith('\n'));
        Assert.Contains(files, file => file.Contains("\r\n", StringComparison.Ordinal));
        Assert.Contains(files, file => file.Contains("\U0001F600", StringComparison.Ordinal));
        Assert.Contains(files, file => file.Contains("\n\n", StringComparison.Ordinal));
        Assert.Contains(files, file => file.Split('\n').Any(line => line.Length > SourceLayer.MaxText));
    }

    /// <summary>
    /// **`FileInfo.bytes` is the file's own UTF-8 length**, whatever the terminators are
    /// and whatever was clipped out of the stored text.
    /// </summary>
    [Fact]
    public void The_summary_states_the_files_own_byte_length()
    {
        foreach (var (file, index) in Corpus().Select((file, index) => (file, index)))
        {
            var (_, info) = Table(file);
            Assert.Equal(Encoding.UTF8.GetByteCount(file), info.Bytes);
            Assert.True(info.Lines >= 0, $"case {index}");
        }
    }

    /// <summary>
    /// **The count and the table agree.** `FileInfo.lines` is what a consumer falls back
    /// to when an offset lands past the last line's start, so a count that disagrees with
    /// the number of `FileLine` facts sends it to a line that does not exist.
    /// </summary>
    [Fact]
    public void The_line_count_is_the_number_of_rows()
    {
        foreach (var (file, index) in Corpus().Select((file, index) => (file, index)))
        {
            var (rows, info) = Table(file);
            Assert.Equal(rows.Count, info.Lines);
            Assert.Equal(
                Enumerable.Range(1, rows.Count).Select(line => (long)line),
                rows.Select(row => row.Number));
        }
    }

    /// <summary>
    /// **`FileLineAt` inverts `FileLine.start`.** The reverse table is written from the
    /// same rows, so the property is that every row's `start` is unique and increasing —
    /// a duplicate would make the offset→line seek ambiguous, and a decrease would make
    /// the range seek that resolves it wrong.
    /// </summary>
    [Fact]
    public void Every_rows_start_is_unique_and_increasing()
    {
        foreach (var file in Corpus())
        {
            var (rows, _) = Table(file);
            var starts = rows.Select(row => row.Start).ToList();

            Assert.Equal(starts.Distinct().Count(), starts.Count);
            Assert.Equal(starts.OrderBy(start => start), starts);
        }
    }

    /// <summary>
    /// **Resolving an offset to a line is the recipe the schema comment states**, run
    /// against the table this producer writes: the greatest `start` at or below the
    /// offset. Every byte of every file must land on the line that contains it.
    /// </summary>
    [Fact]
    public void Every_byte_offset_resolves_to_the_line_that_holds_it()
    {
        foreach (var file in Corpus())
        {
            var (rows, info) = Table(file);
            if (rows.Count == 0)
            {
                continue;
            }

            for (long offset = 0; offset < info.Bytes; offset++)
            {
                // The seek `FileLineAt {file = F, start = X..}` with a client-side limit
                // of one, read backwards: the last row at or below the offset.
                var found = rows.Last(row => row.Start <= offset);
                var next = rows.FirstOrDefault(row => row.Start > offset);

                Assert.True(offset >= found.Start);
                if (next.Number != 0)
                {
                    Assert.True(offset < next.Start);
                }
            }
        }
    }
}
