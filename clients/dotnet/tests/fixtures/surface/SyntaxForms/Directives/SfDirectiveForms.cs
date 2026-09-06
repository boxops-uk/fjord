#define SF_LEDGER_TRACE
#define SF_LEDGER_DEAD
#undef SF_LEDGER_DEAD

extern alias xdoc;

using System;
using System.Collections.Generic;
using static System.Math;
using SfBuilder = System.Text.StringBuilder;
using SfSlots = System.Collections.Generic.Dictionary<int, string>;

namespace Surface.SyntaxForms.Directives;

// `ExternAliasDirective`, `UsingDirective` in all four spellings, and the 20 kinds
// `SyntaxFacts.IsPreprocessorDirective` names.
//
// **Every directive here is trivia, and trivia is not in `DescendantNodes()`.** A walk that
// enumerates descendants and never passes `descendIntoTrivia: true` cannot see a directive
// at all — so `extern alias xdoc;` declares a `SymbolKind.Alias` the index holds nothing
// about, and the `#if` that decides which half of this file is even compiled leaves no
// trace. The using directives are the exception, and only in part: a `UsingDirectiveSyntax`
// is a real node, and the *name* inside it is a `QualifiedName` built from
// `SimpleNameSyntax`es, so `System.Collections.Generic` is three references the walk sees
// while the alias `SfBuilder` it introduces is a declaration it drops.
//
// Two of the 20 directive kinds cannot appear in a file that compiles:
// `BadDirectiveTrivia` is CS1024 even inside an inactive region, and
// `ShebangDirectiveTrivia` is CS9314 outside a script or a file-based program. The other 18
// are all below, the script-only ones inside an inactive region where the compiler parses
// them into directive nodes without binding them.

/// <summary>An attribute, so the Attribute row has a target to apply.</summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class SfMarkAttribute : Attribute
{
    /// <summary>Marks something with an ordinal.</summary>
    /// <param name="ordinal">Which mark.</param>
    public SfMarkAttribute(int ordinal) => Ordinal = ordinal;

    /// <summary>Which mark this is.</summary>
    public int Ordinal { get; }

    /// <summary>A named argument's target, so an attribute argument list has both forms.</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Every conditional-compilation form, and the using and alias forms that need a body to be
/// used from.
/// </summary>
[SfMark(1)]
[SfMark(2, Note = "second")]
public static class SfDirectiveForms
{
    #region The region directive's two halves

    /// <summary>A field inside a <c>#region</c>, so the region encloses a declaration.</summary>
    public static readonly int Ordinal = 1;

    #endregion

    /// <summary>
    /// IfDirectiveTrivia, ElifDirectiveTrivia, ElseDirectiveTrivia and EndIfDirectiveTrivia
    /// — the four branching directives, over the symbol this file defines and the one it
    /// undefines.
    /// </summary>
    /// <returns>Which branch the compiler took.</returns>
    [return: SfMark(3)]
    public static string Branch()
    {
#if SF_LEDGER_DEAD
        // Undefined by the `#undef` at the top of the file, so this text is
        // DisabledTextTrivia.
        return "dead";
#elif SF_LEDGER_TRACE && !SF_LEDGER_DEAD
        return "trace";
#elif DEBUG
        return "debug";
#else
        return "release";
#endif
    }

    /// <summary>
    /// PragmaWarningDirectiveTrivia, NullableDirectiveTrivia and LineDirectiveTrivia in its
    /// <c>default</c> form — the three directives that change how the compiler reads the
    /// code around them without changing which code it reads.
    /// </summary>
    /// <param name="name">A name that may be null.</param>
    /// <returns>Its length.</returns>
    public static int Suppressed(string? name)
    {
#pragma warning disable CS0219 // A variable is assigned but never used
        var unused = 0;
#pragma warning restore CS0219

#nullable disable
        // Outside the annotation context the project sets, so `string` here is neither
        // annotated nor not.
        string unannotated = name;
#nullable restore

#line default

        return unannotated?.Length ?? 0;
    }

    /// <summary>
    /// ExternAliasDirective — the alias `xdoc` declared at the top of this file, used in the
    /// only position an extern alias can be used: as the qualifier of an
    /// AliasQualifiedName.
    /// </summary>
    /// <returns>The aliased type's name.</returns>
    public static string Aliased()
    {
        // `xdoc::` resolves through the `Aliases` metadata the project attaches to the
        // `System.Xml.XDocument` reference. Without that the directive is CS0430 and this
        // row is unreachable from source alone.
        var document = typeof(xdoc::System.Xml.Linq.XDocument);

        // The three using-directive forms this file declares, used so none of them is dead:
        // a plain using (`System.Collections.Generic` for `List<int>`), a static using
        // (`Abs` with no `Math.` in front of it), and two alias usings.
        var numbers = new List<int> { -1, 2 };
        var absolute = Abs(numbers[0]);
        var builder = new SfBuilder("aliased");
        var slots = new SfSlots { [0] = "zero" };

        return $"{document.Name}{absolute}{builder.Length}{slots.Count}{Ordinal}";
    }

#if false
    // **An inactive region, and the compiler still parses the directives inside it.** This
    // is the only way six of the twenty directive kinds can appear in a file that compiles:
    // `#error` and `#warning` are diagnostics when active, `#r` and `#load` are script-only,
    // `#pragma checksum` and a mapped `#line` would rewrite this file's own diagnostic
    // positions, and `#:` is for file-based programs. Roslyn lexes each into a
    // DirectiveTriviaSyntax with `IsActive` false and binds none of them.
#error ErrorDirectiveTrivia
#warning WarningDirectiveTrivia
#r "System.Console.dll"
#load "SfOther.csx"
#:sdk Microsoft.NET.Sdk
#pragma checksum "SfDirectiveForms.cs" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "ab007f1d23d9"
#line 200 "SfElsewhere.cs"
#line (1, 1) - (1, 9) 5 "SfMapped.cs"
#define SF_INACTIVE
#undef SF_INACTIVE
#nullable enable annotations
    private static string Unreachable() => "never compiled";
#endif
}
