// Clause 14.8.1 (qualified alias member, general) needs a collision to be about, and this file
// is half of it: a namespace called `NsGen`, declared as a member of
// `Surface.Namespaces.Qualified`. The other half is NsQualifiedAliasMember.cs, which declares
// a using alias of the same name bound to `System.Collections.Generic`.
//
// The namespace is declared in a separate file, and the alias at *compilation-unit* level, on
// purpose — the two cannot share a scope. A using alias declared in a namespace body whose
// declaration space already has a member of that name compiles until the name is used and is
// then CS0576, "Namespace 'Surface.Namespaces.Qualified' contains a definition conflicting
// with alias 'NsGen'", and the conflicting member may be a namespace as readily as a type.
// Put the alias at compilation-unit level instead and both survive: inside the body the
// namespace member hides the alias, which is the situation `::` exists to get out of.

namespace Surface.Namespaces.Qualified.NsGen
{
    /// <summary>14.8.1: the type reached by treating `NsGen` as the namespace it is.</summary>
    public sealed class NsGenNamespaceType
    {
        /// <summary>The clause this type was written for.</summary>
        public const string Clause = "14.8.1.namespace";
    }

    /// <summary>
    /// 14.8.1: a generic type in that namespace, so the namespace reading and the alias
    /// reading of `NsGen::` can both be followed by a type argument list.
    /// </summary>
    /// <typeparam name="TValue">What the box holds.</typeparam>
    public sealed class NsGenBox<TValue>
    {
        /// <summary>Builds a box.</summary>
        /// <param name="value">What to hold.</param>
        public NsGenBox(TValue value) => Value = value;

        /// <summary>What the box holds.</summary>
        public TValue Value { get; }
    }
}
