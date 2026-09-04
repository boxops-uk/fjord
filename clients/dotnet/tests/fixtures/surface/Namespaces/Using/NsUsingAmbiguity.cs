// Clause 14.5.3 (using namespace directives): two rules that only show themselves when a
// directive is asked for something it does not give.
//
// The first is ambiguity. A using namespace directive imports the types of the named namespace
// into its scope, and two directives importing a type of the same simple name make that name
// ambiguous — not an error at the directive, an error at the *use*. `Left` and `Right` below
// each declare `NsAmbiguousSignal`, `Both` imports both, and the unqualified name is therefore
// unwritable there; the comment in `NsAmbiguityHost` records the diagnostic, and the two
// members resolve the two types the two ways C# leaves open — a qualifier, and an alias.
//
// The second is depth. A using namespace directive imports the *types* of the named namespace
// and does not import its nested namespaces — so `using Surface.Namespaces.Ambiguity;` imports
// nothing whatsoever, because `Ambiguity` declares no types of its own, only namespaces. It
// does not put `NsAmbiguousSignal` in scope and it does not even put `Left` in scope as a
// qualifier. `NsShallowImportHost` is declared *outside* the `Ambiguity` hierarchy so that
// this is visible: from a body nested inside it, `Left` would be in scope as an enclosing
// namespace's member and the directive would be proving nothing.
//
// The hazard is that both `NsAmbiguousSignal` declarations are real, both are indexed, and
// their simple names are equal — so every fact about them has to be keyed by the namespace
// that contains them, which is a container declared in this file twice and located nowhere.

namespace Surface.Namespaces.Ambiguity.Left
{
    /// <summary>14.5.3: the left half of an ambiguous simple name.</summary>
    public sealed class NsAmbiguousSignal
    {
        /// <summary>Which side this one is.</summary>
        public const string Side = "left";
    }
}

namespace Surface.Namespaces.Ambiguity.Right
{
    /// <summary>14.5.3: the right half of an ambiguous simple name.</summary>
    public sealed class NsAmbiguousSignal
    {
        /// <summary>Which side this one is.</summary>
        public const string Side = "right";
    }
}

namespace Surface.Namespaces.Ambiguity.Both
{
    using Surface.Namespaces.Ambiguity.Left;
    using Surface.Namespaces.Ambiguity.Right;
    using NsLeftSignal = Surface.Namespaces.Ambiguity.Left.NsAmbiguousSignal;

    /// <summary>
    /// 14.5.3: a body where one simple name has two meanings and so has none.
    /// </summary>
    public static class NsAmbiguityHost
    {
        /// <summary>
        /// 14.5.3: the unqualified name cannot be written in this body —
        ///
        ///     public static string Ambiguous() =&gt; NsAmbiguousSignal.Side;
        ///     // CS0104: 'NsAmbiguousSignal' is an ambiguous reference between
        ///     // 'Surface.Namespaces.Ambiguity.Left.NsAmbiguousSignal' and
        ///     // 'Surface.Namespaces.Ambiguity.Right.NsAmbiguousSignal'
        ///
        /// so both directives above are live and neither is usable on its own.
        /// </summary>
        public static string ResolvedByQualifying() => Right.NsAmbiguousSignal.Side;

        /// <summary>
        /// 14.5.3 with 14.5.2: the other way out. The alias names one of the two, and inside
        /// this body `NsLeftSignal` is unambiguous where `NsAmbiguousSignal` is not.
        /// </summary>
        public static string ResolvedByAlias() => NsLeftSignal.Side;
    }
}

namespace Surface.Namespaces.ShallowImport
{
    using Surface.Namespaces.Ambiguity;
    using Surface.Namespaces.Ambiguity.Left;

    /// <summary>
    /// 14.5.3: a body with two using namespace directives, one of which does nothing at all.
    /// </summary>
    public static class NsShallowImportHost
    {
        /// <summary>
        /// 14.5.3: resolved by the second directive and only the second. The first names
        /// `Ambiguity`, whose members are three namespaces and no types, and a using namespace
        /// directive imports types — so it contributes no name to this body. Both of these are
        /// errors here, and between them they are the whole rule:
        ///
        ///     public static string ViaTheParent() =&gt; Left.NsAmbiguousSignal.Side;
        ///     // CS0103: The name 'Left' does not exist in the current context
        ///
        ///     // ... and with the second directive removed:
        ///     public static string Direct() =&gt; NsAmbiguousSignal.Side;
        ///     // CS0103: The name 'NsAmbiguousSignal' does not exist in the current context
        /// </summary>
        public static string ThroughTheDeeperDirective() => NsAmbiguousSignal.Side;

        /// <summary>
        /// 14.5.3: the other side of the ambiguity, reached with no directive's help. A fully
        /// qualified name needs no using directive and gets none — which is what makes it the
        /// control case for every reference in this file.
        /// </summary>
        public static string FullyQualified() =>
            Surface.Namespaces.Ambiguity.Right.NsAmbiguousSignal.Side;
    }
}
