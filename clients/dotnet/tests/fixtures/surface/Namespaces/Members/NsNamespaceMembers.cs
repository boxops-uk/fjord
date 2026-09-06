// Clause 14.6 (namespace member declarations): a namespace member is a namespace declaration
// or a type declaration, and nothing else. There is no field, no method, no constant and no
// property directly in a namespace — the grammar has no production for it, so the negative
// half of this clause is not writable and is stated in the README instead of in code.
//
// The body below holds one of each of the two things that are permitted, side by side: a
// nested namespace declaration and a set of type declarations. The nested namespace holds
// members of its own, so the member list of `Surface.Namespaces.Members` contains a namespace
// whose member list is not empty — a namespace member that is itself a container.
//
// The hazard is the *ordering* of a namespace's member list. Members arrive from several
// files (this one, NsTypeDeclarations.cs, NsStaticMembers.cs) and from two spellings, and no
// declaration is first: an index that numbers a namespace's members off a single declaration
// has no single declaration to count from here.

namespace Surface.Namespaces.Members
{
    /// <summary>14.6: a type declaration as a namespace member.</summary>
    public sealed class NsMembersRoster
    {
        /// <summary>The clause this type was written for.</summary>
        public const string Clause = "14.6";

        /// <summary>14.6: reaches the namespace member that is a namespace.</summary>
        public static string FromNestedNamespace() => Roster.NsRosterEntry.Clause;

        /// <summary>14.6: reaches a member that arrived from another file's declaration.</summary>
        public static string FromAnotherFile() => NsMemberClass.Clause;
    }

    // 14.6: a namespace declaration as a namespace member. A namespace declaration takes no
    // doc comment — there is no member for one to document, which is the first hint that the
    // declaration is not a thing an index can hang a fact on.
    namespace Roster
    {
        /// <summary>14.6: a type declared in the nested member namespace.</summary>
        public sealed class NsRosterEntry
        {
            /// <summary>The clause this type was written for.</summary>
            public const string Clause = "14.6.nested";

            /// <summary>14.6: the enclosing namespace's members are in scope from here.</summary>
            public static string Enclosing() => NsMembersRoster.Clause;
        }
    }
}
