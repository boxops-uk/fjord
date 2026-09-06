// Clause 14 (namespaces) and 14.3 (namespace declarations): file one of three, all three
// declaring `Surface.Namespaces.Shared`.
//
// This is the shape clause 14 exists to state — a namespace is open: any number of
// declarations in any number of files contribute to one declaration space, and no declaration
// is the namespace's definition. The three files spell it three ways on purpose:
//
//   * here, file-scoped;
//   * NsSharedTwo.cs, as two nested block declarations;
//   * NsSharedThree.cs, as one dotted block declaration.
//
// Each of the three types below reaches the other two by simple name, which is the compiler
// agreeing that the three files landed in one place. For an index the question is the reverse
// one, and it is the hazard: three declarations mint the name
// `Surface.Namespaces.Shared`, they carry no location that could distinguish them, and so
// either one row absorbs all three or three rows fight over one identity.

namespace Surface.Namespaces.Shared;

/// <summary>14: contributed to the shared namespace by the file-scoped spelling.</summary>
public sealed class NsSharedFromFileScoped
{
    /// <summary>Which file contributed this type.</summary>
    public const string Origin = "one";

    /// <summary>14: reaches both of the other files' contributions by simple name.</summary>
    public static string Neighbours() =>
        NsSharedFromNestedBlocks.Origin + NsSharedFromDottedBlock.Origin;
}
