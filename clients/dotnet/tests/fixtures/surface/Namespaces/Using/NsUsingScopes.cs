// Clause 14.5.1 (using directives, general): scope. A using directive is scoped to the
// compilation unit or the namespace body that immediately contains it, and it affects no
// nested namespace body's *siblings* and no other file. This file puts three bodies side by
// side to make the boundaries visible:
//
//   * `Scopes.Imported` imports `System.Text` and names `StringBuilder` unqualified;
//   * `Scopes.NotImported` imports nothing and has to spell `System.Text.StringBuilder` out,
//     though it sits in the same file three lines below;
//   * `Scopes.Imported.Deeper` imports nothing of its own and still sees `StringBuilder`,
//     because an enclosing body's directives are in force in a body nested inside it.
//
// Two more facts are here. The directive in `Scopes.Imported` is *the same directive* as the
// one at the top of Using/NsUsingForms.cs — the same target, imported twice in one project —
// so a reference fact for `System.Text` has two directives to have come from. And
// `Scopes.Relative` names its target *relatively*: `using Members;` resolves to
// `Surface.Namespaces.Members` only because the directive sits in a body nested in
// `Surface.Namespaces`. Moved to the top of the file, where a using directive's target is
// resolved against the global namespace alone, the identical line is CS0246 — "The type or
// namespace name 'Members' could not be found".

namespace Surface.Namespaces.Scopes
{
    namespace Imported
    {
        using System.Text;

        /// <summary>14.5.1: inside the body that holds the directive.</summary>
        public static class NsScopedImportHost
        {
            /// <summary>14.5.1: the imported simple name, used where the directive is in force.</summary>
            public static StringBuilder Builder() => new StringBuilder("14.5.1");
        }

        namespace Deeper
        {
            /// <summary>14.5.1: nested inside the body that holds the directive.</summary>
            public static class NsNestedScopedImportHost
            {
                /// <summary>
                /// 14.5.1: the enclosing body's directive is in force here too, so the simple
                /// name resolves with no directive in this body at all.
                /// </summary>
                public static StringBuilder Builder() => new StringBuilder("14.5.1.nested");
            }
        }
    }

    namespace NotImported
    {
        /// <summary>14.5.1: a sibling body, which the directive above does not reach.</summary>
        public static class NsUnscopedImportHost
        {
            /// <summary>
            /// 14.5.1: fully qualified, because `using System.Text;` three lines up is scoped
            /// to its own body and this is not that body.
            /// </summary>
            public static System.Text.StringBuilder Builder() =>
                new System.Text.StringBuilder("14.5.1.sibling");
        }
    }

    namespace Relative
    {
        using Members;

        /// <summary>14.5.1: reached through a using directive with a relative target.</summary>
        public static class NsRelativeImportHost
        {
            /// <summary>
            /// 14.5.1: `Members` was resolved against the enclosing namespace, so this names a
            /// type from `Surface.Namespaces.Members` by its simple name.
            /// </summary>
            public static string Clause() => NsMembersRoster.Clause;
        }
    }
}
