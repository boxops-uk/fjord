// Clause 7.8.2 (unqualified names): every namespace-or-type-name has an unqualified name,
// which is its last identifier with any type argument list dropped. Two types in two
// namespaces may share one, and this file is three of them: `LexShared` is declared in
// `Names.Left`, in `Names.Right` and in `Names.Left.Deeper`.
//
// Three declarations, one unqualified name. Whatever tells them apart is not the name.

namespace Surface.Lexical.Names.Left
{
    /// <summary>7.8.2: the first of three types whose unqualified name is <c>LexShared</c>.</summary>
    public sealed class LexShared
    {
        /// <summary>Which namespace this one is in.</summary>
        public const int Tag = 1;

        /// <summary>Its own fully qualified name, written out for a reader.</summary>
        public const string Fqn = "Surface.Lexical.Names.Left.LexShared";
    }

    namespace Deeper
    {
        /// <summary>
        /// 7.8.2: the third of them, nested inside the first's namespace — so
        /// <c>LexShared</c> written here resolves to *this* one and the enclosing
        /// namespace's is reachable only by qualifying it.
        /// </summary>
        public sealed class LexShared
        {
            /// <summary>Which namespace this one is in.</summary>
            public const int Tag = 3;

            /// <summary>Its own fully qualified name.</summary>
            public const string Fqn = "Surface.Lexical.Names.Left.Deeper.LexShared";

            /// <summary>Resolves the bare name, which reaches this type and not the outer one.</summary>
            public static int NearerName() => LexShared.Tag;

            /// <summary>Reaches the enclosing namespace's type, which needs qualification.</summary>
            public static int FurtherName() => Left.LexShared.Tag;
        }
    }
}

namespace Surface.Lexical.Names.Right
{
    /// <summary>7.8.2: the second of three types whose unqualified name is <c>LexShared</c>.</summary>
    public sealed class LexShared
    {
        /// <summary>Which namespace this one is in.</summary>
        public const int Tag = 2;

        /// <summary>Its own fully qualified name.</summary>
        public const string Fqn = "Surface.Lexical.Names.Right.LexShared";
    }
}

namespace Surface.Lexical.Names
{
    /// <summary>
    /// 7.8.2: reaches all three from a namespace that encloses none of them, so every
    /// reference has to be qualified and no two of the three references are alike.
    /// </summary>
    public static class LexUnqualifiedNameUses
    {
        /// <summary>Reaches the left one.</summary>
        public static int Left() => Names.Left.LexShared.Tag;

        /// <summary>Reaches the right one.</summary>
        public static int Right() => Names.Right.LexShared.Tag;

        /// <summary>Reaches the deeper one.</summary>
        public static int Deeper() => Names.Left.Deeper.LexShared.Tag;

        /// <summary>Reads the three declared fully qualified names, so each is referenced.</summary>
        public static int Lengths() =>
            Names.Left.LexShared.Fqn.Length
            + Names.Right.LexShared.Fqn.Length
            + Names.Left.Deeper.LexShared.Fqn.Length;
    }
}
