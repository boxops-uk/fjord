namespace Surface.SyntaxForms.Declarations;

/// <summary>
/// ExtensionDeclaration — a C# 14 extension block, whose members declare against a receiver
/// rather than against the enclosing static class.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one type declaration here that the entity layer cannot express.</b> An extension
/// block is a <c>TypeDeclarationSyntax</c>, so the walk reaches it and asks for its declared
/// symbol like any other type — but its <c>TypeKind</c> has no <c>csharp.NamedType</c>
/// alternative, so the block is dropped and every member inside it is dropped with it.
/// Those losses are counted rather than silent, which is the whole distinction the
/// declaration census exists to hold.
/// </para>
/// <para>
/// <b>It is present only because the project sets <c>LangVersion</c>.</b> The pinned Roslyn
/// declares the node and refuses to parse it at C# 13 or below; the walk's parse options
/// come from MSBuild, so a fixture that left the default would exercise this row in the
/// SDK's compiler and not in the indexer's.
/// </para>
/// </remarks>
public static class SfExtensionForms
{
    /// <summary>An extension block over <see cref="int"/>, with a property and two methods.</summary>
    extension(int value)
    {
        /// <summary>The value, doubled.</summary>
        public int Doubled => value * 2;

        /// <summary>Whether the value is even.</summary>
        /// <returns><see langword="true"/> when it is.</returns>
        public bool IsEven() => (value % 2) == 0;

        /// <summary>A static member of an extension block.</summary>
        public static int Origin => 0;
    }

    /// <summary>A second extension block, over a generic receiver.</summary>
    /// <typeparam name="TItem">The element type.</typeparam>
    extension<TItem>(System.Collections.Generic.IReadOnlyList<TItem> items)
    {
        /// <summary>The last element, or the default when there is none.</summary>
        public TItem? Final => items.Count == 0 ? default : items[items.Count - 1];
    }
}
