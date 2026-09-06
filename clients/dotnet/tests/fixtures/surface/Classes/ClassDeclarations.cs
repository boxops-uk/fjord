// Clause 15 — classes. 15.1 introduces the kind in prose; 15.2.1 gives `class_declaration`,
// and this file writes that production in the shapes that need no modifier, base or member of
// their own. 15.2.6's `class_body` is the brace pair every declaration below closes with: an
// index holds facts about the members inside it, never about the delimiters.
//
// Clause 15's own hazard is at the foot of the file — one simple name, two declarations,
// separated by namespace and by nothing else. The arity pair that would kill an indexing run
// (`ClsPlain` beside `ClsPlain<T>`) is deliberately absent; it lives in a quarantine project.

namespace Surface.Classes
{
    /// <summary>
    /// 15.2.1 — the smallest `class_declaration` the grammar allows: one modifier, the
    /// keyword, an identifier, and an empty `class_body`. Its base class is written nowhere
    /// (15.2.4.2) and it declares no member (15.2.6).
    /// </summary>
    public class ClsPlain
    {
    }

    /// <summary>
    /// 15.2.1 — one of every optional part of `class_declaration` at once: an access
    /// modifier, `sealed`, a `type_parameter_list`, a `class_base` naming both a base class
    /// and an interface, a `type_parameter_constraints_clause`, and a body that declares
    /// members of two kinds.
    /// </summary>
    /// <typeparam name="TEntry">15.2.3 — the one type parameter.</typeparam>
    public sealed class ClsFullyDressed<TEntry> : ClsPlain, IClsTagged
        where TEntry : notnull
    {
        /// <summary>15.4 — a constant member, so the body is not all fields.</summary>
        public const int Capacity = 4;

        /// <summary>15.5.1 — an instance field whose type is the type parameter.</summary>
        public TEntry? Entry;
    }

    /// <summary>
    /// 15.2.1 — `internal` is what a top-level class gets when no access modifier is
    /// written (15.3.6), spelled out here so a query can tell a declared modifier from an
    /// implied one: this declaration and <see cref="ClsImpliedAccess"/> have the same
    /// declared accessibility and differ in whether anything was written.
    /// </summary>
    internal class ClsDeclaredInternal
    {
        internal int Slot;
    }

    /// <summary>15.2.1 — the same accessibility, implied rather than written.</summary>
    class ClsImpliedAccess
    {
        internal int Slot;
    }
}

namespace Surface.Classes.Alpha
{
    /// <summary>
    /// Clause 15 hazard — one simple name declared twice, here and in
    /// <c>Surface.Classes.Beta</c>. The two declarations differ in namespace and in nothing
    /// else: an identity built from the simple name alone would make them one type with two
    /// sets of members, and the members themselves (<c>Ordinal</c> against <c>Label</c>)
    /// have different types, which is what turns a merge into a wrong answer rather than a
    /// harmless one.
    /// </summary>
    public class ClsHomonym
    {
        /// <summary>15.5.1 — the field that distinguishes this half of the pair.</summary>
        public int Ordinal;
    }
}

namespace Surface.Classes.Beta
{
    /// <summary>Clause 15 hazard — the other half of the pair. See the Alpha declaration.</summary>
    public class ClsHomonym
    {
        /// <summary>15.5.1 — same container name, different member, different type.</summary>
        public string Label = "beta";
    }
}
