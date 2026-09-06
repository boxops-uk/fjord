// Annex D.1 (general) and D.2 (introduction) — how a documentation comment is written, and
// where it is allowed to sit. Two facts live here and nowhere else in the project.
//
// D.1 gives the mechanism: a comment of a certain form directs a tool to produce XML from the
// comment and the source elements around it. There are two lexical forms and the compiler
// attaches the same XML for either — the single-line form (three slashes) and the delimited
// form (`/** … */`, whose leading `*` column is stripped before the XML is parsed). The form
// is invisible in the documentation file, so an index that records a symbol's documentation
// has to answer the same for both spellings.
//
// D.2 requires the comment to appear *immediately before* a user-defined type or member. That
// is a source-position rule, while the documentation file it feeds is keyed by ID string —
// and the two do not agree about what a name is. A type and its constructor are written with
// the same identifier token: `DocPoint` and `DocPoint(int, int)` both spell `DocPoint` in
// source, both carry a `<summary>`, and their ID strings are `T:Surface.Docs.DocPoint` and
// `M:Surface.Docs.DocPoint.#ctor(System.Int32,System.Int32)`. That is D.2's hazard: an index
// that attaches documentation to "the name the comment precedes" merges the two, and an index
// that attaches it to the declaration keeps them apart.

namespace Surface.Docs;

/// <summary>
/// D.2 — the annex's introduction, as it is written there: class <c>DocPoint</c> models a
/// point in a two-dimensional plane. The comment sits immediately before the declaration,
/// which is what makes it a documentation comment rather than a comment.
/// </summary>
/// <remarks>
/// D.2 hazard — this <c>&lt;summary&gt;</c> belongs to <c>T:Surface.Docs.DocPoint</c>, and the
/// one below it belongs to <c>M:Surface.Docs.DocPoint.#ctor(System.Int32,System.Int32)</c>.
/// The identifier token before each is the same word.
/// </remarks>
public class DocPoint
{
    /// <summary>D.2 — the x coordinate, documented where it is declared.</summary>
    private readonly int _x;

    /// <summary>D.2 — the y coordinate.</summary>
    private readonly int _y;

    /// <summary>
    /// D.2 hazard — the constructor's documentation. Written <c>DocPoint</c>, exactly as the
    /// type is; distinguished from the type only by the ID string's <c>M:</c> prefix and its
    /// <c>#ctor</c> member name.
    /// </summary>
    /// <param name="x">The x coordinate of the new point.</param>
    /// <param name="y">The y coordinate of the new point.</param>
    public DocPoint(int x, int y)
    {
        _x = x;
        _y = y;
    }

    /// <summary>D.2 — the point's x coordinate.</summary>
    public int X => _x;

    /// <summary>D.2 — the point's y coordinate.</summary>
    public int Y => _y;

    /// <summary>
    /// D.2 — the annex's own second example: method <c>Draw</c> renders the point. A
    /// documentation comment on a member, immediately before it.
    /// </summary>
    public string Draw() => $"({_x},{_y})";
}

/**
 * <summary>
 * D.1 — the delimited documentation-comment form, <c>/**</c> to a closing delimiter. Each
 * line's leading <c>*</c> and the whitespace up to it are stripped before the XML is parsed,
 * so what reaches the documentation file is the same shape a single-line comment would have
 * produced. Nothing in <c>Docs.xml</c> records which form was used.
 * </summary>
 * <remarks>D.1 — the same tags are available in either form; only the delimiters differ.</remarks>
 */
public sealed class DocDelimitedForm
{
    /**
     * <summary>D.1 — a member documented in the delimited form.</summary>
     * <returns>The number of documentation-comment forms the annex names, which is two.</returns>
     */
    public int FormCount() => 2;

    /// <summary>D.1 — a member of the same type documented in the single-line form.</summary>
    /// <returns>The same number, reached through the other spelling.</returns>
    public int FormCountAgain() => 2;
}

/// <summary>
/// D.2 — where a documentation comment may not sit. The compiler accepts the text and warns
/// (CS1587) that it is not on a language element; nothing reaches the documentation file, so
/// an index has no fact to hold about it.
/// </summary>
public static class DocMisplacedComment
{
    /// <summary>D.2 — this one is in the right place, and is emitted.</summary>
    /// <returns>The count of comments in this method that reach <c>Docs.xml</c>, which is one:
    /// this one. The comment inside the body is not a documentation comment.</returns>
    public static int Count()
    {
        /// <summary>D.2 — a triple-slash comment before a statement is not immediately
        /// before a type or member declaration, so it documents nothing.</summary>
        return 1;
    }
}
