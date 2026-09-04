// Clause 14.8 (qualified alias member) and 14.8.1 (general): the `::` production,
//
//     qualified_alias_member: identifier '::' identifier type_argument_list?
//
// whose left identifier is never a type and never a namespace — it is an extern alias, a using
// alias, or the keyword `global`. All three appear below, and each is used in the one situation
// that makes it necessary rather than decorative:
//
//   * `NsGen::` — the alias reading of a name that the enclosing namespace also gives to a
//     namespace member. Inside `Surface.Namespaces.Qualified`, plain `NsGen` is the namespace
//     declared in NsQualifiedNamespaceNamedLikeAnAlias.cs, because a namespace member hides a
//     compilation-unit alias. `NsGen::List<int>` is the only spelling that reaches the alias.
//   * `global::` — the root reading. `global::Surface...NsGen.NsGenNamespaceType` reaches the
//     namespace member while the alias holds the simple name, and `global::System.String`
//     reaches a framework type from a body that could otherwise resolve `System` locally.
//   * `NsPing::` — the extern alias reading, which is the only way to name anything in an
//     assembly the compiler was handed under an alias.
//
// The hazard is that `NsGen` means two different things eleven lines apart, and neither
// meaning is visible at the use site: one is a namespace with two declarations and no
// location, the other is an alias with one declaration and no target of its own name. Every
// row below is a reference whose answer depends on which of the two the resolver picked.

extern alias NsPing;

using NsGen = System.Collections.Generic;

namespace Surface.Namespaces.Qualified
{
    /// <summary>14.8.1: one member per reading of the `::` operator's left-hand identifier.</summary>
    public static class NsQualifiedAliasHost
    {
        /// <summary>
        /// 14.8.1: the alias reading. `NsGen::` skips the namespace member of the same name
        /// and goes to the compilation unit's alias, so this is a framework list.
        /// </summary>
        public static NsGen::List<int> ThroughTheAlias() => new NsGen::List<int> { 1 };

        /// <summary>
        /// 14.8.1: the alias reading with a type argument list, which is the optional tail of
        /// the production.
        /// </summary>
        public static NsGen::Dictionary<string, int> ThroughTheAliasGeneric() =>
            new NsGen::Dictionary<string, int>();

        /// <summary>
        /// 14.8.1: the plain reading of the same identifier. No `::`, so `NsGen` is the
        /// namespace this project declares, and this is a corpus type.
        /// </summary>
        public static string ThroughTheNamespace() => NsGen.NsGenNamespaceType.Clause;

        /// <summary>
        /// 14.8.1: the `global::` reading, which reaches the same corpus type from the root of
        /// the namespace hierarchy rather than through the enclosing namespace.
        /// </summary>
        public static string ThroughGlobal() =>
            global::Surface.Namespaces.Qualified.NsGen.NsGenNamespaceType.Clause;

        /// <summary>14.8.1: `global::` at a generic corpus type, with a type argument list.</summary>
        public static string ThroughGlobalGeneric() =>
            new global::Surface.Namespaces.Qualified.NsGen.NsGenBox<int>(8).Value.ToString();

        /// <summary>14.8.1: `global::` at a framework type.</summary>
        public static string ThroughGlobalToFramework() => global::System.String.Empty;

        /// <summary>
        /// 14.8.1 and 14.4: the extern alias reading. Nothing but `NsPing::` reaches this
        /// type — see Extern/NsExternAlias.cs for why.
        /// </summary>
        public static string ThroughTheExternAlias() =>
            typeof(NsPing::System.Net.NetworkInformation.IPStatus).Name;

        /// <summary>
        /// 14.8.1: an extern alias followed by a *namespace* rather than a type, which is the
        /// same production reaching a different kind of entity.
        /// </summary>
        public static string ThroughTheExternAliasNamespace() =>
            typeof(NsPing::System.Net.NetworkInformation.PingReply).Namespace ?? string.Empty;
    }
}
