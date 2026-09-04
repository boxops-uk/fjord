namespace Surface.SyntaxForms.Names;

// The six cref kinds — every reference C# can write inside a documentation comment.
//
// **These are the references the walk cannot reach, and the reason is structural rather
// than a missing case.** A cref lives inside documentation-comment trivia, and
// `SyntaxNode.DescendantNodes()` does not descend into trivia unless it is asked to. So a
// walk that enumerates descendants and dispatches on `SimpleNameSyntax` never sees a
// `NameMemberCrefSyntax` — not because the kind is unhandled, but because the node is not in
// the sequence. Every fact about a cref is missing, silently, and the count of unresolved
// names does not move.
//
// Every cref here binds: `GenerateDocumentationFile` is on, so a cref the compiler cannot
// resolve is CS1574 and a cref it cannot parse is CS1584, which makes this file's own
// correctness checkable by the build rather than by inspection.

/// <summary>
/// The target every cref in <see cref="SfCrefForms"/> points at.
/// </summary>
public sealed class SfCrefTarget
{
    /// <summary>A property, for a cref naming a member.</summary>
    public int Total { get; set; }

    /// <summary>An indexer, for an IndexerMemberCref.</summary>
    /// <param name="slot">Which slot.</param>
    /// <returns>The slot.</returns>
    public int this[int slot] => slot;

    /// <summary>A method with a parameter list, for a cref that has to pick an overload.</summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The conversion.</returns>
    public static string Convert(int value) => value.ToString();

    /// <summary>The other overload, which is what makes the parameter list load-bearing.</summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The conversion.</returns>
    public static string Convert(string value) => value;

    /// <summary>A generic method, for a cref written with braces.</summary>
    /// <typeparam name="TItem">What to wrap.</typeparam>
    /// <param name="item">The item.</param>
    /// <returns>A one-element array.</returns>
    public static TItem[] Wrap<TItem>(TItem item) => [item];

    /// <summary>A binary operator, for an OperatorMemberCref.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Their sum.</returns>
    public static SfCrefTarget operator +(SfCrefTarget left, SfCrefTarget right) =>
        new() { Total = left.Total + right.Total };

    /// <summary>A unary operator, for an unqualified OperatorMemberCref.</summary>
    /// <param name="target">The target.</param>
    /// <returns>Its negation.</returns>
    public static SfCrefTarget operator -(SfCrefTarget target) =>
        new() { Total = -target.Total };

    /// <summary>An implicit conversion, for a ConversionOperatorMemberCref.</summary>
    /// <param name="target">The target.</param>
    public static implicit operator int(SfCrefTarget target) => target.Total;

    /// <summary>An explicit conversion, so the cref has to say which direction.</summary>
    /// <param name="total">The total.</param>
    public static explicit operator SfCrefTarget(int total) => new() { Total = total };
}

/// <summary>
/// Each cref kind, written once.
/// </summary>
/// <remarks>
/// <para>
/// NameMemberCref, bare: <see cref="SfCrefTarget"/> names a type,
/// <see cref="SfCrefTarget.Total"/> names a member through its container, and
/// <see cref="SfCrefTarget.Convert(int)"/> picks one of two overloads by its parameter
/// list. <see cref="SfCrefTarget.Wrap{TItem}"/> is the generic spelling, whose type
/// parameter list uses braces because angle brackets are XML.
/// </para>
/// <para>
/// QualifiedCref: <see cref="System.Text.StringBuilder.Append(char)"/> — a container
/// written as a type, a <c>.</c>, and a member cref inside it. The container here is a
/// framework type, so the reference has a definition and no location.
/// </para>
/// <para>
/// TypeCref: <see cref="int"/> and <see cref="bool"/> — a cref whose whole content is a
/// type that is not a name, which is the only way a predefined type is ever the *subject* of
/// a reference rather than the type of one. The census row names <c>int*</c>; the pinned
/// compiler rejects that spelling and <c>int[]</c> with CS1584 and CS1658, so a
/// <c>TypeCrefSyntax</c> over a pointer or array type is unreachable from a file that
/// compiles clean, and a predefined type is the whole of what this kind can carry here.
/// </para>
/// <para>
/// IndexerMemberCref: <see cref="SfCrefTarget.this[int]"/> — the one form of member
/// reference that names no identifier at all.
/// </para>
/// <para>
/// OperatorMemberCref: <see cref="SfCrefTarget.operator +(SfCrefTarget, SfCrefTarget)"/>
/// and <see cref="SfCrefTarget.operator -(SfCrefTarget)"/>, binary and unary.
/// </para>
/// <para>
/// ConversionOperatorMemberCref:
/// <see cref="SfCrefTarget.implicit operator int(SfCrefTarget)"/> and
/// <see cref="SfCrefTarget.explicit operator SfCrefTarget(int)"/> — the two halves, which
/// differ only in a keyword and would otherwise share a cref.
/// </para>
/// </remarks>
public static class SfCrefForms
{
    /// <summary>
    /// A member whose own doc comment carries the unqualified spellings, which parse to the
    /// same kinds without a QualifiedCref wrapper: <see cref="Anchor"/>,
    /// <see cref="SfCrefTarget.Convert(string)"/>.
    /// </summary>
    /// <returns>Something so the member is not empty.</returns>
    public static string Anchor() => SfCrefTarget.Convert(1);
}
