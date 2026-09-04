// Clause 14.8.2 (uniqueness of aliases): the name of an extern alias or using alias must be
// unique within the compilation unit or namespace body that immediately declares it — and only
// there. Across scopes the same alias name may be declared again, bound to something else,
// and this file declares `NsUniqueName` three times:
//
//   * at compilation-unit level, bound to `System.Text.StringBuilder`;
//   * in the `Uniqueness` namespace body, bound to `System.IO.MemoryStream`;
//   * in the `Uniqueness.Inner` body nested inside it, bound to `System.Collections.ArrayList`.
//
// Each inner declaration shadows the outer one for its own body, and the compiler accepts all
// three with no diagnostic. Using/NsAliasUniquenessTwin.cs then declares the same name a
// fourth time in a second compilation unit.
//
// This is the sharpest hazard in clause 14. Four declarations mint the alias name
// `NsUniqueName`, all four are legal, all four target different types, and the only thing
// separating them is the scope that declares them — which for two of the four is a namespace
// body, and a namespace body belongs to a declaration that has no location of its own.
//
// `extern alias NsPing;` is declared twice for the same reason, once per scope. The grammar
// allows extern_alias_directives at the head of a namespace body as well as at the head of a
// compilation unit, and this file is the only one in the corpus that uses the second position.
//
// The last shape is worse and legal: `NsMemberClass` is a real type declared in
// Members/NsTypeDeclarations.cs, and the alias in `Uniqueness.Shadowing` binds that exact name
// to `System.Uri`. Inside that body the name means the framework type; three files away it
// means the corpus type; and nothing at either use site says which.

extern alias NsPing;

using NsUniqueName = System.Text.StringBuilder;

namespace Surface.Namespaces.Uniqueness
{
    using NsUniqueName = System.IO.MemoryStream;

    /// <summary>14.8.2: the alias name as this body redeclared it.</summary>
    public static class NsUniquenessInBody
    {
        /// <summary>14.8.2: `NsUniqueName` is a MemoryStream here, and only here.</summary>
        public static NsUniqueName Open() => new NsUniqueName();

        /// <summary>14.8.2: what the shadowed compilation-unit alias named, spelled out.</summary>
        public static string ShadowedTarget() => typeof(System.Text.StringBuilder).Name;
    }

    namespace Inner
    {
        extern alias NsPing;

        using NsUniqueName = System.Collections.ArrayList;

        /// <summary>14.8.2: the alias name as the innermost body redeclared it.</summary>
        public static class NsUniquenessInNestedBody
        {
            /// <summary>14.8.2: `NsUniqueName` is an ArrayList here.</summary>
            public static NsUniqueName Collect() => new NsUniqueName();

            /// <summary>
            /// 14.4 and 14.8.2: the extern alias this body declared for itself, which names
            /// the same assembly the compilation unit's own declaration names.
            /// </summary>
            public static string AliasedFromBody() =>
                typeof(NsPing::System.Net.NetworkInformation.PingOptions).Name;
        }
    }

    namespace Shadowing
    {
        using NsMemberClass = System.Uri;

        /// <summary>
        /// 14.5.2 and 14.8.2: a body where a corpus type's name means a framework type.
        /// </summary>
        public static class NsShadowingHost
        {
            /// <summary>14.5.2: `NsMemberClass` here is `System.Uri`.</summary>
            public static NsMemberClass Address() => new NsMemberClass("https://example.invalid/14.5.2");

            /// <summary>
            /// 14.5.2: and the type that really is called `NsMemberClass`, which this body can
            /// only reach by qualifying it, because its own alias took the simple name.
            /// </summary>
            public static string TheRealOne() => Surface.Namespaces.Members.NsMemberClass.Clause;
        }
    }
}
