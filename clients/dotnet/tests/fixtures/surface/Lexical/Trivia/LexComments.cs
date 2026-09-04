// Clause 6.3.3 (comments): single-line comments run to the next line terminator, delimited
// comments run to the first `*/` and do not nest. Documentation comments are the form an
// index normally keeps as content, so both spellings of those are here too.

/* A delimited comment before the namespace. The sequence /* inside it opens nothing,
   because 6.3.3 says delimited comments do not nest — this comment ends at the first
   close delimiter and not at a matching one. */

namespace Surface.Lexical.Trivia;

/// <summary>Every kind of comment 6.3.3 admits, attached to something declared.</summary>
/// <remarks>
/// A three-slash documentation comment, which is the spelling that survives into an index
/// as a member's documentation.
/// </remarks>
public sealed class LexComments
{
    /**
     * <summary>The other documentation comment spelling: a delimited comment opened with
     * two asterisks. 6.3.3 admits it and the leading asterisks are stripped.</summary>
     */
    public const int DocumentedByDelimitedForm = 1;

    /// <summary>A member whose initialiser has a comment inside the expression.</summary>
    public const int CommentInsideExpression = /* the delimited form as white space */ 2;

    /// <summary>A member declared after a comment that is the whole of its line.</summary>
    // a single-line comment between the documentation comment and the declaration
    public const int AfterInterveningComment = 3;

    /// <summary>A member whose declaration a delimited comment splits in two.</summary>
    public const /* between the modifier and the type */ int SplitByComment = 4;

    /// <summary>
    /// Reads the constants, and holds a comment with the characters a lexer must not treat
    /// as an opener: a <c>//</c> inside a string, and a <c>/*</c> inside a string.
    /// </summary>
    public int Total()
    {
        // The two strings below contain comment delimiters that are ordinary characters.
        string notAComment = "// this is data";
        string notADelimiter = "/* this is data too */";
        return DocumentedByDelimitedForm
            + CommentInsideExpression
            + AfterInterveningComment
            + SplitByComment
            + notAComment.Length
            + notADelimiter.Length;
    }
}

// A single-line comment as the last thing in the file, with no terminator after it.
