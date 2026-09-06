namespace Surface.Quarantine.Partial;

/// <summary>
/// M3 — the defining half of a partial member whose parameter names are its own.
/// </summary>
/// <remarks>
/// <para>
/// Clause 15.6.9: the two halves of a partial method must have the same signature
/// <i>modulo</i> the names of their type parameters and parameters — the names are
/// explicitly allowed to differ, and 15.6.9 says a warning is reported when they do,
/// not an error. So this is conforming C#, and it is how a partial member is normally
/// written.
/// </para>
/// <para>
/// <b>Why this face does not need one file.</b> <c>CodeMarkup.Signature</c> is
/// <c>symbol.ToDisplayString(Hover)</c>, and the <c>Hover</c> format sets
/// <c>SymbolDisplayParameterOptions.IncludeName</c> and
/// <c>SymbolDisplayGenericsOptions.IncludeTypeParameters</c>. <c>SymbolInfo</c> is keyed
/// <c>{symbol}</c> alone with <c>{signature, doc, modifiers}</c> on the value side, so the
/// two halves hand one key two values —
/// <c>void PartialHandler.Handle&lt;TItem&gt;(TItem item)</c> from this file and
/// <c>void PartialHandler.Handle&lt;TValue&gt;(TValue value)</c> from the other — and the
/// refusal fires across files. The one-file rule that keeps
/// <see cref="PartialSplit"/>'s <c>Definition</c> pairs together does not save this one.
/// </para>
/// <para>
/// <b>The doc comment is not a second discriminator.</b> Roslyn resolves
/// <c>GetDocumentationCommentXml</c> to the <i>implementing</i> part for both symbols, so
/// both halves report the same <c>doc</c> and only <c>signature</c> differs. This summary
/// is on the defining half and is the one the compiler discards.
/// </para>
/// <para>
/// <b>The contrast row.</b> The type <c>PartialHandler</c> itself has its two parts in two
/// files, which the schema sanctions as two <c>Definition</c> facts with different
/// <c>file</c> keys. A gate over this project has to separate a member with two halves
/// from a type with two parts, and this pair of files is what makes that separation
/// testable.
/// </para>
/// </remarks>
public partial class PartialHandler
{
    /// <summary>The defining half, naming its type parameter and parameter one way.</summary>
    /// <typeparam name="TItem">Spelled <c>TItem</c> here and <c>TValue</c> in the other half.</typeparam>
    /// <param name="item">Spelled <c>item</c> here and <c>value</c> in the other half.</param>
    public partial void Handle<TItem>(TItem item);
}
