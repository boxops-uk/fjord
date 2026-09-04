using System;

// NamespaceDeclaration — the block-bodied spelling, which is a different syntax kind from
// the file-scoped `namespace X;` every other file in this project uses.
//
// **A namespace is declared by neither kind, as far as the index is concerned.**
// `BaseNamespaceDeclarationSyntax` derives from none of the six bases the walk switches on,
// so no `csharp` definition is written for a namespace — and on the reference side the walk
// drops `SymbolKind.Namespace` explicitly before writing anything. The identifiers in
// `namespace Surface.SyntaxForms.Namespaces` are `SimpleNameSyntax`es that the walk visits,
// binds to namespace symbols, and then discards: three names visited, no facts, no tally.
namespace Surface.SyntaxForms.Namespaces
{
    /// <summary>A type inside a block-bodied namespace.</summary>
    public sealed class SfBlockScoped
    {
        /// <summary>What the clock said, through the global alias.</summary>
        public SfClock Observed { get; init; } = SfClock.UnixEpoch;

        /// <summary>A vector, through the other global alias, so both are used.</summary>
        public SfNumerics::Vector2 Direction { get; init; }

        /// <summary>Renders both.</summary>
        /// <returns>A rendering.</returns>
        public override string ToString() => $"{Observed:O} {Direction}";
    }

    // A nested block-bodied namespace, which is a second NamespaceDeclaration — and the
    // only declaration form in C# that cannot carry a documentation comment (CS1587).
    namespace Inner
    {
        /// <summary>A type inside the nested namespace, reached as a longer qualified name.</summary>
        public sealed class SfNestedNamespaceMember
        {
            /// <summary>What the member holds.</summary>
            public int Depth { get; init; } = 2;
        }
    }
}
