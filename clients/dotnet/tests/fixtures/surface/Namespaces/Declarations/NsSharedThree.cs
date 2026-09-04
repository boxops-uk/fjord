// Clause 14 (namespaces) and 14.3 (namespace declarations): file three of three. The same
// namespace again, spelled as one dotted block declaration — the third of the three syntaxes,
// and the one that neither of the other two files uses.
//
// The type here declares a nested type as well, so the shared namespace's member list is not
// flat: `NsSharedFromDottedBlock.Inner` is a member of a type that is a member of a namespace
// three files declare.

namespace Surface.Namespaces.Shared
{
    /// <summary>14: contributed to the shared namespace by the dotted block spelling.</summary>
    public sealed class NsSharedFromDottedBlock
    {
        /// <summary>Which file contributed this type.</summary>
        public const string Origin = "three";

        /// <summary>14: reaches both of the other files' contributions by simple name.</summary>
        public static string Neighbours() =>
            NsSharedFromFileScoped.Origin + NsSharedFromNestedBlocks.Origin;

        /// <summary>
        /// 14.7: a nested type — a type declaration that is a member of a type, not of the
        /// namespace, and so is not a namespace_member_declaration however deeply the
        /// namespace above it was declared.
        /// </summary>
        public readonly struct Inner
        {
            /// <summary>Which file contributed the type this one nests in.</summary>
            public const string Origin = "three.inner";
        }
    }
}
