// Clause 14.3 (namespace declarations): the file-scoped form. One declaration, no braces, and
// its body is the rest of the compilation unit — so the members below are members of
// `Surface.Namespaces.Declarations` and the declaration itself spans to the end of the file.
//
// The name is a qualified_identifier, which per 14.3 is shorthand: `namespace A.B.C;` declares
// A, then B in A, then C in B. Three namespaces are declared by this one line, and two of them
// (`Surface` and `Surface.Namespaces`) have no members of their own from this file. An index
// that records a declaration per namespace has three to record here and one syntax node to
// record them from.

namespace Surface.Namespaces.Declarations;

/// <summary>14.3: declared by a file-scoped namespace declaration.</summary>
public sealed class NsFileScopedHost
{
    /// <summary>How this file spells its namespace declaration.</summary>
    public const string Spelling = "file-scoped";

    /// <summary>14.3: the fully qualified name this type ended up with.</summary>
    public static string QualifiedName() => typeof(NsFileScopedHost).FullName ?? Spelling;

    /// <summary>
    /// 14.3: reaches the block-scoped sibling with no qualification, because a file-scoped
    /// declaration and a block-scoped one of the same name are one declaration space.
    /// </summary>
    public static string Sibling() => NsBlockScopedHost.Spelling;
}
