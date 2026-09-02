using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>`src.Symbol`, the cross-language join key.</b>
/// </para>
/// <para>
/// Spec-conformant SCIP under this producer's own scheme token, which is a decision the
/// schema comment carries in full: the reference C# indexer emits only a type's innermost
/// namespace and an empty package coordinate for the code it is indexing, and neither is
/// usable as a key a cross-database join seeks. These assert the parts of the format that
/// a second implementation would get wrong, and the two that would be invisible — full
/// qualification, and an overload ordinal that does not depend on the order the compiler
/// was handed the files.
/// </para>
/// </summary>
public sealed class ScipSymbolsTests
{
    /// <summary>Compile <paramref name="sources"/> and hand back a symbol lookup.</summary>
    /// <remarks>
    /// Several sources, because the partial-class case is the one where declaration order
    /// is decided by something other than the text.
    /// </remarks>
    private static Compilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            "Walked",
            sources.Select((source, index) =>
                CSharpSyntaxTree.ParseText(SourceText.From(source), path: $"F{index}.cs")),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IEnumerable<ISymbol> Members(Compilation compilation, string type) =>
        compilation.GetTypeByMetadataName(type)!.GetMembers();

    private static string Symbol(Compilation compilation, string type) =>
        ScipSymbols.Of(compilation.GetTypeByMetadataName(type)!)!;

    private static string Symbol(Compilation compilation, string type, string member) =>
        ScipSymbols.Of(Members(compilation, type).First(m => m.Name == member))!;

    /// <summary>
    /// **Every namespace, not just the innermost.** `A.B.T` and `C.B.T` are different types
    /// and must be different strings; the reference indexer's output collapses them.
    /// </summary>
    [Fact]
    public void A_type_carries_every_namespace_above_it()
    {
        var compilation = Compile(
            "namespace A.B { public class T {} }",
            "namespace C.B { public class T {} }");

        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 A/B/T#", Symbol(compilation, "A.B.T"));
        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 C/B/T#", Symbol(compilation, "C.B.T"));
        Assert.NotEqual(Symbol(compilation, "A.B.T"), Symbol(compilation, "C.B.T"));
    }

    [Fact]
    public void A_type_in_the_global_namespace_has_no_namespace_descriptor()
    {
        var compilation = Compile("public class T {}");

        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 T#", Symbol(compilation, "T"));
    }

    /// <summary>The suffix says what a descriptor is — a type, a term, a method.</summary>
    [Fact]
    public void Each_kind_of_member_takes_its_own_suffix()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public class T
                {
                    public int Field;
                    public int Property { get; set; }
                    public void Method() {}
                    public class Nested {}
                }
            }
            """);

        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 N/T#Field.", Symbol(compilation, "N.T", "Field"));
        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 N/T#Property.", Symbol(compilation, "N.T", "Property"));
        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 N/T#Method().", Symbol(compilation, "N.T", "Method"));
        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 N/T#Nested#", Symbol(compilation, "N.T+Nested"));
    }

    /// <summary>
    /// A constructor is `.ctor`, which is not a simple identifier — so it arrives escaped
    /// rather than breaking the descriptor grammar at the dot.
    /// </summary>
    [Fact]
    public void A_constructor_is_escaped_because_its_name_holds_a_dot()
    {
        var compilation = Compile("namespace N { public class T { public T() {} } }");

        Assert.Equal(
            "scip-csharp nuget Walked 0.0.0.0 N/T#`.ctor`().",
            Symbol(compilation, "N.T", ".ctor"));
    }

    /// <summary>
    /// **A simple identifier is ASCII**, per the grammar — so a legal C# name outside it is
    /// escaped. The reference indexer's `\w` test treats these as simple, which produces a
    /// symbol a strict parser rejects.
    /// </summary>
    [Fact]
    public void A_non_ascii_name_is_escaped()
    {
        var compilation = Compile("namespace N { public class Ünïcode {} }");

        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 N/`Ünïcode`#", Symbol(compilation, "N.Ünïcode"));
    }

    [Fact]
    public void A_type_parameter_and_a_parameter_take_their_bracket_forms()
    {
        var compilation = Compile("namespace N { public class T { public void M<X>(int a) {} } }");

        var method = Members(compilation, "N.T").OfType<IMethodSymbol>().First(m => m.Name == "M");

        Assert.Equal(
            "scip-csharp nuget Walked 0.0.0.0 N/T#M().[X]",
            ScipSymbols.Of(method.TypeParameters[0]));
        Assert.Equal(
            "scip-csharp nuget Walked 0.0.0.0 N/T#M().(a)",
            ScipSymbols.Of(method.Parameters[0]));
    }

    /// <summary>
    /// **The overload ordinal is the format the reference indexer uses**, so a consumer
    /// that reads one reads the other: the first is bare, the rest are `+N`.
    /// </summary>
    [Fact]
    public void Overloads_are_numbered_after_the_first()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public class T
                {
                    public void M() {}
                    public void M(int a) {}
                    public void M(string a) {}
                }
            }
            """);

        var symbols = Members(compilation, "N.T")
            .Where(member => member.Name == "M")
            .Select(member => ScipSymbols.Of(member)!)
            .ToList();

        Assert.Equal(3, symbols.Distinct().Count());
        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/T#M().", symbols);
        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/T#M(+1).", symbols);
        Assert.Contains("scip-csharp nuget Walked 0.0.0.0 N/T#M(+2).", symbols);
    }

    /// <summary>
    /// **The ordinal does not depend on the order the compiler was handed the files.**
    /// A partial class's members arrive in syntax-tree order, so the reference indexer's
    /// count gives one commit two different symbol sets depending on the walk — and a
    /// sealed identity hashes the facts. Counting a `docId`-sorted order removes it.
    /// </summary>
    [Fact]
    public void A_partial_classs_overloads_do_not_depend_on_file_order()
    {
        const string First = "namespace N { public partial class T { public void M(int a) {} } }";
        const string Second = "namespace N { public partial class T { public void M(string a) {} } }";

        List<string> Symbols(params string[] sources) =>
            [.. Members(Compile(sources), "N.T")
                .Where(member => member.Name == "M")
                .Select(member => ScipSymbols.Of(member)!)
                .OrderBy(symbol => symbol, StringComparer.Ordinal)];

        var forwards = Symbols(First, Second);
        var backwards = Symbols(Second, First);

        Assert.Equal(2, forwards.Count);
        Assert.Equal(forwards, backwards);
    }

    /// <summary>
    /// **The same overload keeps its ordinal when a sibling is added *after* it in the
    /// sorted order.** This is the half of the instability that can be removed; the half
    /// that cannot is stated in the schema — inserting an overload that sorts earlier does
    /// renumber the ones after it, which is why nothing keys on this across revisions.
    /// </summary>
    [Fact]
    public void An_overload_keeps_its_ordinal_when_a_later_sorting_sibling_appears()
    {
        const string One = "namespace N { public class T { public void M(int a) {} } }";
        const string Two = """
            namespace N
            {
                public class T
                {
                    public void M(int a) {}
                    public void M(string a) {}
                }
            }
            """;

        var before = Symbol(Compile(One), "N.T", "M");
        var after = Members(Compile(Two), "N.T")
            .OfType<IMethodSymbol>()
            .First(method => method.Parameters.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32);

        Assert.Equal(before, ScipSymbols.Of(after));
    }

    /// <summary>
    /// **A referenced assembly names itself.** Its identity is the package coordinate, so a
    /// symbol from the BCL is the same string in every index that mentions it — which is
    /// what makes the fan-out's join worth doing.
    /// </summary>
    [Fact]
    public void A_symbol_from_another_assembly_carries_that_assemblys_identity()
    {
        var compilation = Compile("public class T {}");
        var symbol = ScipSymbols.Of(compilation.GetTypeByMetadataName("System.String")!)!;

        Assert.StartsWith("scip-csharp nuget System.", symbol, StringComparison.Ordinal);
        Assert.EndsWith(" System/String#", symbol, StringComparison.Ordinal);
    }

    /// <summary>
    /// **A local has no global name and is not given one.** SCIP models these as
    /// `local N`, an occurrence ordinal that changes when the file is edited;
    /// `codemarkup.FileLocalXRef` answers them span to span instead, so this producer
    /// declines to mint a string it would have to invalidate.
    /// </summary>
    [Fact]
    public void A_local_has_no_symbol()
    {
        var compilation = Compile("public class T { void M() { int local = 1; } }");
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var declarator = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax>()
            .First();

        Assert.Null(ScipSymbols.Of(model.GetDeclaredSymbol(declarator)!));
    }

    [Fact]
    public void A_namespace_is_a_symbol_of_its_own_and_the_global_one_is_not()
    {
        var compilation = Compile("namespace A.B { public class T {} }");
        var inner = compilation.GetTypeByMetadataName("A.B.T")!.ContainingNamespace;

        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 A/B/", ScipSymbols.Of(inner));
        Assert.Equal("scip-csharp nuget Walked 0.0.0.0 A/", ScipSymbols.Of(inner.ContainingNamespace));
        Assert.Null(ScipSymbols.Of(compilation.GlobalNamespace));
    }
}
