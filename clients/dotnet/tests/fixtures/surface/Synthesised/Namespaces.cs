using SynAliasedInt = System.Int32;

namespace Surface.Synthesised.Deep
{
    /// <summary><c>SymbolKind.NamedType</c> — a name declared in one namespace.</summary>
    /// <remarks>
    /// <c>SynTwice</c> is declared here and in <c>Surface.Synthesised.Deeper</c> below.
    /// Both are legal, both have the same arity, and only the containing namespace tells
    /// them apart — so an index that keys a type by its simple name, or by (file, name),
    /// mints one identity for two types. The second alias declaration of
    /// <c>SynAliasedInt</c> at the top of this file is the same hazard one kind down.
    /// </remarks>
    public sealed class SynTwice
    {
        /// <summary>Which one this is.</summary>
        public SynAliasedInt Which => 1;
    }

    // A nested namespace declaration, inside a block-bodied one (14.3). A namespace
    // declaration takes no documentation comment — CS1587 — so this note is a plain one.
    namespace Nested
    {
        /// <summary>A type whose full name is assembled from two declarations.</summary>
        public sealed class SynNestedHome
        {
            /// <summary>Where it lives.</summary>
            public string Where => "Surface.Synthesised.Deep.Nested";
        }
    }
}

namespace Surface.Synthesised.Deeper
{
    /// <summary>The other <c>SynTwice</c>.</summary>
    public sealed class SynTwice
    {
        /// <summary>Which one this is.</summary>
        public SynAliasedInt Which => 2;
    }
}

namespace Surface.Synthesised.Deep
{
    /// <summary>
    /// A second declaration of the namespace <c>Surface.Synthesised.Deep</c>, in the same
    /// file as the first.
    /// </summary>
    /// <remarks>
    /// One namespace symbol, two declaring syntax references, and a third in
    /// <c>Namespaces.cs</c>'s first block — a namespace is the one symbol kind in C# that
    /// is <em>expected</em> to be declared many times, so an index has to merge here where
    /// everywhere else it must not. Getting that backwards produces either a duplicate
    /// namespace row or a refused write, and both are silent.
    /// </remarks>
    public sealed class SynReopened
    {
        /// <summary>Reaches the sibling declared in the earlier block.</summary>
        public SynTwice Sibling { get; } = new();
    }
}
