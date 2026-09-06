// Clause 14.3 (namespace declarations): the block-scoped form, three ways in one file.
//
//   * `Surface.Namespaces.Declarations` — the same namespace NsFileScoped.cs declares with the
//     file-scoped form, declared here with braces. Two syntaxes, one declaration space.
//   * `Nested` inside it — a namespace declaration whose containing declaration is another
//     namespace declaration, which is the only nesting 14.3 permits (a namespace cannot be
//     declared inside a type).
//   * `Deeper.Still` inside that — a qualified_identifier used at a nested position, so the
//     namespace `Surface.Namespaces.Declarations.Nested.Deeper.Still` is arrived at by three
//     declarations of three different shapes.
//
// The hazard is that none of these declarations owns a location that distinguishes it. The
// name `Surface.Namespaces.Declarations` is minted twice in this project from two files and
// two syntaxes, and both mint the same thing — which is exactly why it is safe, and exactly
// why an index that tried to give it a definition site would have to pick one arbitrarily.

namespace Surface.Namespaces.Declarations
{
    /// <summary>14.3: declared by a block-scoped namespace declaration.</summary>
    public sealed class NsBlockScopedHost
    {
        /// <summary>How this file spells its namespace declaration.</summary>
        public const string Spelling = "block-scoped";

        /// <summary>
        /// 14.3: reaches the file-scoped sibling with no qualification — the proof that the two
        /// spellings landed in one declaration space rather than two namespaces that look alike.
        /// </summary>
        public static string Sibling() => NsFileScopedHost.Spelling;

        /// <summary>14.3: reaches into the nested namespace, which needs qualification.</summary>
        public static string Inner() => Nested.NsNestedHost.Depth;
    }

    namespace Nested
    {
        /// <summary>14.3: declared one namespace declaration deep.</summary>
        public sealed class NsNestedHost
        {
            /// <summary>How deep this type sits below the project's root namespace.</summary>
            public const string Depth = "one";

            /// <summary>
            /// 14.3: an enclosing namespace's members are in scope without qualification, so
            /// the outer host is reachable from in here by its simple name.
            /// </summary>
            public static string Outer() => NsBlockScopedHost.Spelling;
        }

        namespace Deeper.Still
        {
            /// <summary>
            /// 14.3: declared by a qualified_identifier at a nested position, so this type's
            /// namespace is spelled across three declarations.
            /// </summary>
            public sealed class NsDeeperStillHost
            {
                /// <summary>How deep this type sits.</summary>
                public const string Depth = "three";

                /// <summary>14.3: every enclosing namespace is in scope, at every level.</summary>
                public static string Ancestors() =>
                    NsNestedHost.Depth + "/" + NsBlockScopedHost.Spelling + "/" + NsFileScopedHost.Spelling;
            }
        }
    }
}
