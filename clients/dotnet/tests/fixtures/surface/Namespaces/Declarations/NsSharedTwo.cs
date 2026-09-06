// Clause 14 (namespaces) and 14.3 (namespace declarations): file two of three. The same
// namespace as NsSharedOne.cs and NsSharedThree.cs, spelled as two nested block declarations
// rather than one qualified name.
//
// `Surface.Namespaces` is declared here as a declaration in its own right — it is the outer
// brace pair — which no other file in this project does. It has no members of its own from
// this file, so it is a namespace declaration whose entire content is another namespace
// declaration.

namespace Surface.Namespaces
{
    namespace Shared
    {
        /// <summary>14: contributed to the shared namespace by the nested spelling.</summary>
        public sealed class NsSharedFromNestedBlocks
        {
            /// <summary>Which file contributed this type.</summary>
            public const string Origin = "two";

            /// <summary>14: reaches both of the other files' contributions by simple name.</summary>
            public static string Neighbours() =>
                NsSharedFromFileScoped.Origin + NsSharedFromDottedBlock.Origin;
        }
    }
}
