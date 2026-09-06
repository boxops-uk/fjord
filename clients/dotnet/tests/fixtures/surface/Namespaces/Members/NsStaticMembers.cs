// Clause 14.5.4 (using static directives) needs a target, and clause 14.7 (type declarations)
// needs a static class among its forms. This file is both: the static types the Using/ files
// import, declared as members of the same namespace the rest of clause 14.7's forms are
// declared in, from a third file.
//
// `NsStaticSignals` holds one of everything a using static directive can bring into scope —
// a constant, a static field, a static property, a static method, a static event's accessor
// pair reached through a method, and a nested type — because the directive imports the
// accessible static members *and* the nested types, and the difference between those two is
// invisible at the use site.

namespace Surface.Namespaces.Members
{
    /// <summary>14.5.4: the static class the using static directives name.</summary>
    public static class NsStaticSignals
    {
        /// <summary>14.5.4: a constant, imported by a using static directive.</summary>
        public const string NsSignalClause = "14.5.4";

        /// <summary>14.5.4: a static field, imported by a using static directive.</summary>
        public static readonly int NsSignalWidth = 4;

        /// <summary>14.5.4: a static property, imported by a using static directive.</summary>
        public static string NsSignalLabel => "signal";

        /// <summary>14.5.4: a static method, imported by a using static directive.</summary>
        /// <param name="row">Which row to name.</param>
        /// <returns>The row's label.</returns>
        public static string NsSignalFor(int row) => NsSignalLabel + row;

        /// <summary>
        /// 14.5.4: a nested type, which a using static directive imports alongside the static
        /// members — so `NsSignalKind` is nameable unqualified wherever the directive is in
        /// force, without ever having been a namespace member.
        /// </summary>
        public enum NsSignalKind
        {
            /// <summary>The quiet one.</summary>
            Quiet = 0,

            /// <summary>The loud one.</summary>
            Loud = 1,
        }
    }

    /// <summary>
    /// 14.5.4: a static class of extension methods. A using static directive of this type
    /// makes the methods available *as extensions*, which is the one import a using namespace
    /// directive and a using static directive do differently.
    /// </summary>
    public static class NsStaticExtensions
    {
        /// <summary>Labels a row number.</summary>
        /// <param name="row">The row to label.</param>
        /// <returns>The label.</returns>
        public static string NsRowLabel(this int row) => "row" + row;

        /// <summary>Labels a cell.</summary>
        /// <typeparam name="TValue">What the cell holds.</typeparam>
        /// <param name="cell">The cell to label.</param>
        /// <returns>The label.</returns>
        public static string NsCellLabelOf<TValue>(this NsCell<TValue> cell)
            where TValue : notnull => cell.Value.ToString() ?? string.Empty;
    }
}
