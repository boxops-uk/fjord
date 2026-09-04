// Clause 14.8.2 (uniqueness of aliases): the fourth declaration of `NsUniqueName`, in a second
// compilation unit, bound to a fourth target. Uniqueness is per scope, and a compilation unit
// is a scope, so this file's declaration and NsAliasUniqueness.cs's are both legal and neither
// is visible to the other.
//
// The namespace this file declares is the same one NsAliasUniqueness.cs declares. So the two
// files agree about the namespace and disagree about the alias, which is the cleanest statement
// of the difference between the two: a namespace is open and its declarations merge, an alias
// is scoped and its declarations do not.

using NsUniqueName = System.Guid;

namespace Surface.Namespaces.Uniqueness;

/// <summary>14.8.2: the alias name as the second compilation unit declared it.</summary>
public static class NsUniquenessTwin
{
    /// <summary>14.8.2: `NsUniqueName` is a Guid in this file.</summary>
    public static NsUniqueName Empty() => NsUniqueName.Empty;

    /// <summary>
    /// 14.8.2: the sibling file's body-level alias is not in force here, so this reaches the
    /// type that file's alias named by spelling it out.
    /// </summary>
    public static string SiblingTarget() => typeof(System.IO.MemoryStream).Name;

    /// <summary>14: and the namespace both files declare is one namespace.</summary>
    public static string SharedNamespaceMember() => NsUniquenessInBody.ShadowedTarget();
}
