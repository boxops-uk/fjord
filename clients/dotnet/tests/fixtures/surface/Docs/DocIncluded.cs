// Annex D.3.6 — <include>, the one recommended tag whose content is not in the source file.
// It names an external XML file and an XPath expression, and the compiler splices the
// selected elements into the documentation file *in place of the tag*.
//
// That makes it the only tag where the documentation an index reports depends on which
// artefact it read:
//
//   * Read from the syntax tree, `DocIncludedText.Compose` has one documentation element,
//     `<include>`, and no summary at all.
//   * Read from `Docs.xml`, it has the `<summary>` and `<returns>` that live in
//     `DocIncluded.xml`, and no trace that an include ever happened.
//
// The two failure modes are visible in the file too, and neither is an error: a file that
// cannot be found warns CS1589 and leaves the tag in place behind an XML comment, and an
// XPath that matches nothing warns nothing and leaves the tag in place behind a different XML
// comment. Both are documentation an index can hold and cannot resolve.

namespace Surface.Docs.Tags;

/// <summary>
/// D.3.6 — a type whose members are documented from <c>DocIncluded.xml</c>. The type itself
/// is documented here, so the file holds one hand-written summary and two included ones.
/// </summary>
public sealed class DocIncludedText
{
    private readonly string[] _parts = ["annex", "D", "include"];

    /// <include file="DocIncluded.xml" path="docs/member[@name='DocIncludedText.Compose']/*"/>
    public string Compose() => string.Join('.', _parts);

    /// <include file="DocIncluded.xml" path="docs/member[@name='DocIncludedText.Lines']/*"/>
    public int Lines() => _parts.Length;

    /// <include file="DocNoSuchFile.xml" path="docs/member/*"/>
    public string FromMissingFile() => "the file is not there";

    /// <include file="DocIncluded.xml" path="docs/member[@name='DocIncludedText.Absent']/*"/>
    public string FromMissingElement() => "the file is there and the element is not";

    /// <summary>
    /// D.3.6 — an <c>&lt;include&gt;</c> beside hand-written tags, so the splice is a
    /// sibling rather than the whole comment. The summary here survives into the file and
    /// the included <c>&lt;returns&gt;</c> joins it.
    /// </summary>
    /// <include file="DocIncluded.xml" path="docs/member[@name='DocIncludedText.Lines']/returns"/>
    public int LinesAgain() => _parts.Length;
}
