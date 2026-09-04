// Clause 8.2.1 — the general rules for reference types, exercised as a naming hazard rather
// than as a list of kinds. Two class declarations here have the same simple name and
// different enclosing namespaces, so only the namespace separates their identities.

namespace Surface.Types.Alpha
{
    /// <summary>
    /// 8.2.1 hazard — simple name <c>TyHomonym</c>, namespace <c>Surface.Types.Alpha</c>.
    /// </summary>
    public sealed class TyHomonym
    {
        /// <summary>Distinguishes the two at runtime, so a wrong merge is observable.</summary>
        public int Tag => 1;
    }
}

namespace Surface.Types.Beta
{
    /// <summary>
    /// 8.2.1 hazard — the same simple name under <c>Surface.Types.Beta</c>. Two declarations,
    /// one simple name, different values behind it.
    /// </summary>
    public sealed class TyHomonym
    {
        public int Tag => 2;
    }
}

namespace Surface.Types
{
    /// <summary>8.2.1 — both homonyms referenced from one place, each by its full name.</summary>
    public static class TyHomonymUse
    {
        /// <summary>8.2.1 — a reference to the Alpha declaration.</summary>
        public static int FromAlpha => new Alpha.TyHomonym().Tag;

        /// <summary>8.2.1 — and to the Beta one.</summary>
        public static int FromBeta => new Beta.TyHomonym().Tag;

        /// <summary>8.2.1 — null is assignable to every reference type, whatever its kind.</summary>
        public static bool EveryReferenceTakesNull()
        {
            object? asObject = null;
            string? asString = null;
            int[]? asArray = null;
            ITyNamed? asInterface = null;
            TyAdder? asDelegate = null;
            return asObject is null && asString is null && asArray is null
                && asInterface is null && asDelegate is null;
        }
    }
}
