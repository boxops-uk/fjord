using NamesAliasedText = System.String;
using NamesAliasedList = System.Collections.Generic.List<int>;

namespace Surface.Names.Keywords;

/// <summary>
/// M14 — the name on the left of a using alias: an identifier the walk visits, in a
/// position where <c>GetSymbolInfo</c> has nothing to give it.
/// </summary>
/// <remarks>
/// <para>
/// <c>using NamesAliasedText = System.String;</c> is a <c>UsingDirectiveSyntax</c> whose
/// <c>Alias</c> is a <c>NameEqualsSyntax</c> holding an <c>IdentifierNameSyntax</c>. The
/// walk descends into it like any other node and dispatches it as a reference; the symbol
/// the directive <i>declares</i> is reached by <c>GetDeclaredSymbol</c>, which
/// <c>Reference</c> does not call, and <c>GetSymbolInfo</c> on a declaration's own name
/// answers nothing. So each alias directive in a compilation unit costs one
/// <c>Unresolved</c> — two in this file, and none anywhere else in the project.
/// </para>
/// <para>
/// <b>The right-hand side is resolved twice over.</b> <c>System</c> binds to a namespace
/// and is dropped by <c>Reference</c>'s kind filter before the counter;
/// <c>String</c> binds to the type and writes a full set of reference rows, keyed
/// <c>import_</c> because <c>CodeMarkup.Role</c> tests <c>UsingDirectiveSyntax</c>
/// ancestry first. So the directive contributes one unresolved name, one reference row per
/// named type, and no declaration row at all — a using alias has no symbol the
/// <c>codemarkup</c> layer can key on, which is M22's subject in the
/// <c>Namespaces</c> project.
/// </para>
/// </remarks>
public static class NamesAliasDirective
{
    /// <summary>The aliased reference type, used so the alias is not dead.</summary>
    public static NamesAliasedText Text() => "aliased";

    /// <summary>The aliased constructed generic, whose two type names both resolve.</summary>
    public static NamesAliasedList Numbers() => [1, 2, 3];
}
