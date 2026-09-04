// Clause 6.3.2 (line terminators): C# ends a line on U+000D alone, U+000A, the
// U+000D U+000A pair, U+0085 NEXT LINE, U+2028 LINE SEPARATOR or U+2029 PARAGRAPH
// SEPARATOR. This file uses all six, one per member, so that every declared line
// number below is wrong by a countable amount for a reader that splits on LF alone.
// The terminator after each member is named in that member's own summary.

namespace Surface.Lexical.Trivia;

/// <summary>Members separated by each of the six line terminators of 6.3.2.</summary>
public sealed class LexTerminators{
    /// <summary>Followed by U+000D alone.</summary>
    public const int BeforeCarriageReturn = 1;    /// <summary>Followed by U+000A alone.</summary>
    public const int BeforeLineFeed = 2;
    /// <summary>Followed by the U+000D U+000A pair.</summary>
    public const int BeforeCarriageReturnLineFeed = 3;
    /// <summary>Followed by U+0085 NEXT LINE.</summary>
    public const int BeforeNextLine = 4;    /// <summary>Followed by U+2028 LINE SEPARATOR.</summary>
    public const int BeforeLineSeparator = 5;     /// <summary>Followed by U+2029 PARAGRAPH SEPARATOR.</summary>
    public const int BeforeParagraphSeparator = 6;     // a single-line comment that U+2028 closes, not U+000A     /// <summary>Declared after a comment that a U+2028 closed.</summary>
    public const int AfterCommentClosedByLineSeparator = 7;

    /// <summary>The member whose line number the terminator census is stated against.</summary>
    public int LastMember() => BeforeCarriageReturn + AfterCommentClosedByLineSeparator;
}
