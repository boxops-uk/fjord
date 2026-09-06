using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>The language-independent surface, from a Roslyn symbol.</b>
/// </para>
/// <para>
/// <c>codemarkup</c> is the same facts as <c>csharp</c> re-keyed for the question a UI
/// asks, so nothing here is new information — it is a projection, and every predicate
/// carries the query that would derive it as a comment in the schema. While
/// <c>nyi/derivation</c> stands, that comment is the specification this file is checked
/// against.
/// </para>
/// <para>
/// The two vocabularies are <b>citations</b>: LSP's <c>SymbolKind</c> and SCIP's
/// <c>SymbolRole</c>. They sit in keys, so their discriminants froze the day the layer
/// shipped and anything this producer cannot name goes through <c>other</c> rather than
/// being rounded to the nearest alternative.
/// </para>
/// </summary>
internal static class CodeMarkup
{
    /// <summary>
    /// A symbol's LSP <c>SymbolKind</c>.
    /// </summary>
    /// <remarks>
    /// <b>A delegate is a `class_`.</b> LSP has no alternative for one and a delegate is a
    /// type rather than a function, so `function_` would be the wrong end of the mistake.
    /// An enum member is `enumMember` and not `field`, even though Roslyn models it as a
    /// const field, because that is what a UI is filtering for.
    /// </remarks>
    public static FjordValue Kind(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.TypeKind switch
        {
            TypeKind.Interface => DotnetIndex.Tagged(11u),
            TypeKind.Struct => DotnetIndex.Tagged(23u),
            TypeKind.Enum => DotnetIndex.Tagged(10u),
            _ => DotnetIndex.Tagged(5u),
        },
        IMethodSymbol method => method.MethodKind switch
        {
            MethodKind.Constructor or MethodKind.StaticConstructor => DotnetIndex.Tagged(9u),
            MethodKind.UserDefinedOperator or MethodKind.Conversion
                or MethodKind.BuiltinOperator => DotnetIndex.Tagged(25u),
            _ => DotnetIndex.Tagged(6u),
        },
        IPropertySymbol => DotnetIndex.Tagged(7u),
        IFieldSymbol field => field.ContainingType?.TypeKind == TypeKind.Enum
            ? DotnetIndex.Tagged(22u)
            : field.IsConst ? DotnetIndex.Tagged(14u) : DotnetIndex.Tagged(8u),
        IEventSymbol => DotnetIndex.Tagged(24u),
        ITypeParameterSymbol => DotnetIndex.Tagged(26u),
        INamespaceSymbol => DotnetIndex.Tagged(3u),
        IParameterSymbol or ILocalSymbol => DotnetIndex.Tagged(13u),

        // Named rather than rounded: `other` carries what this producer saw, so a
        // consumer can tell "a kind you do not know" from "a kind nobody recorded".
        _ => DotnetIndex.Tagged(0u, symbol.Kind.ToString().ToLowerInvariant()),
    };

    /// <summary>
    /// What a reference is doing, as SCIP's <c>SymbolRole</c> projected onto one value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An approximation of the syntax, and it says which way it errs.</b> The mask SCIP
    /// carries cannot be expressed, so this picks the role a UI filters by — and where two
    /// would apply it prefers the more specific: a method name inside an object creation
    /// is <c>new_</c> rather than <c>call</c>.
    /// </para>
    /// <para>
    /// Everything unrecognised is <c>read</c>, which is the honest default: a reference
    /// that resolved is at minimum a use.
    /// </para>
    /// </remarks>
    public static FjordValue Role(SimpleNameSyntax name, ISymbol target)
    {
        // A `using` directive, which is an import however the names in it resolve.
        if (name.Ancestors().OfType<UsingDirectiveSyntax>().Any())
        {
            return DotnetIndex.Tagged(7u);
        }

        // A base list — `class T : Base, IFace` — is heritage rather than a type
        // reference, and it is the edge a UI draws an inheritance tree from.
        if (name.Ancestors().OfType<BaseListSyntax>().Any())
        {
            return DotnetIndex.Tagged(9u);
        }

        if (name.Ancestors().OfType<AttributeSyntax>().Any())
        {
            return DotnetIndex.Tagged(10u);
        }

        if (name.Ancestors().OfType<BaseObjectCreationExpressionSyntax>().Any())
        {
            return DotnetIndex.Tagged(6u);
        }

        if (target is ITypeSymbol)
        {
            return DotnetIndex.Tagged(5u);
        }

        if (target is IMethodSymbol)
        {
            return DotnetIndex.Tagged(4u);
        }

        // The left of an assignment is a write. `Parent` rather than an ancestor walk:
        // `a.b = c` writes `b`, and `f(a) = ...` is not a thing, so the immediate
        // relationship is the whole question.
        var expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name
            ? (ExpressionSyntax)access
            : name;

        return expression.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == expression
            ? DotnetIndex.Tagged(3u)
            : DotnetIndex.Tagged(2u);
    }

    /// <summary>What a hover card shows: the signature as a person would read it.</summary>
    public static string Signature(ISymbol symbol) =>
        SourceLayer.Clip(symbol.ToDisplayString(Hover));

    /// <summary>The modifiers, space-separated, as they are written in source.</summary>
    public static string Modifiers(ISymbol symbol)
    {
        var words = new System.Collections.Generic.List<string>(4)
        {
            symbol.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => string.Empty,
            },
        };

        if (symbol.IsStatic)
        {
            words.Add("static");
        }

        if (symbol.IsAbstract)
        {
            words.Add("abstract");
        }

        if (symbol.IsVirtual)
        {
            words.Add("virtual");
        }

        if (symbol.IsOverride)
        {
            words.Add("override");
        }

        if (symbol.IsSealed)
        {
            words.Add("sealed");
        }

        return string.Join(' ', words.Where(word => word.Length > 0));
    }

    /// <summary>The display format a hover uses — a signature, not a qualified name.</summary>
    private static readonly SymbolDisplayFormat Hover = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
}
