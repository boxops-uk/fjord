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

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 A/B/T#", Symbol(compilation, "A.B.T"));
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 C/B/T#", Symbol(compilation, "C.B.T"));
        Assert.NotEqual(Symbol(compilation, "A.B.T"), Symbol(compilation, "C.B.T"));
    }

    [Fact]
    public void A_type_in_the_global_namespace_has_no_namespace_descriptor()
    {
        var compilation = Compile("public class T {}");

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 T#", Symbol(compilation, "T"));
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

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 N/T#Field.", Symbol(compilation, "N.T", "Field"));
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 N/T#Property.", Symbol(compilation, "N.T", "Property"));
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 N/T#Method().", Symbol(compilation, "N.T", "Method"));
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 N/T#Nested#", Symbol(compilation, "N.T+Nested"));
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
            "scip-csharp-2 nuget Walked 0.0.0.0 N/T#`.ctor`().",
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

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 N/`Ünïcode`#", Symbol(compilation, "N.Ünïcode"));
    }

    [Fact]
    public void A_type_parameter_and_a_parameter_take_their_bracket_forms()
    {
        var compilation = Compile("namespace N { public class T { public void M<X>(int a) {} } }");

        var method = Members(compilation, "N.T").OfType<IMethodSymbol>().First(m => m.Name == "M");

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/T#M().[X]",
            ScipSymbols.Of(method.TypeParameters[0]));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/T#M().(a)",
            ScipSymbols.Of(method.Parameters[0]));
    }

    /// <summary>
    /// <para>
    /// **`class Result` beside `class Result&lt;T&gt;` is two symbols**, which is the
    /// everyday C# idiom and was one string until the arity went into the descriptor.
    /// </para>
    /// <para>
    /// It killed the run rather than conflating quietly: `codemarkup.SymbolInfo` is keyed
    /// `{symbol}` with the signature on the value side, the signature always differs, and
    /// ingest refuses one key with two values — so no repository containing such a pair
    /// could be indexed at all.
    /// </para>
    /// </summary>
    /// <remarks>
    /// **`NotEqual` is not the assertion**, because two arbitrary distinct strings would
    /// satisfy it. The spellings are asserted, and the non-generic one is asserted to be
    /// the string it always was: arity 0 keeps the bare name, which is what keeps every
    /// non-generic symbol in every index byte-identical.
    /// </remarks>
    [Fact]
    public void Two_arities_of_one_type_name_are_two_symbols()
    {
        var compilation = Compile(
            "namespace N { public class Result {} public class Result<T> { public T Value; } }");

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result#", Symbol(compilation, "N.Result"));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#", Symbol(compilation, "N.Result`1"));
    }

    /// <summary>
    /// **The suffix is on the containing type's descriptor, so everything under it
    /// inherits the arity by construction.** A member, a member's parameter and the
    /// type's own type parameter each spell the parent once — which is the reason the
    /// arity is not a second rule applied per descriptor kind.
    /// </summary>
    [Fact]
    public void Everything_nested_under_a_generic_type_carries_its_arity()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public class Result<T>
                {
                    public Result(T value) => Value = value;

                    public T Value { get; }

                    public bool Holds(T other) => true;

                    public class Inner {}
                }
            }
            """);

        var type = compilation.GetTypeByMetadataName("N.Result`1")!;
        var holds = type.GetMembers("Holds").OfType<IMethodSymbol>().Single();

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#Value.",
            Symbol(compilation, "N.Result`1", "Value"));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#[T]",
            ScipSymbols.Of(type.TypeParameters[0]));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#Holds().(other)",
            ScipSymbols.Of(holds.Parameters[0]));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#Inner#",
            Symbol(compilation, "N.Result`1+Inner"));
    }

    /// <summary>
    /// **A reference to a constructed generic type spells the definition's descriptor**,
    /// so a use of `Result&lt;int&gt;` joins to the declaration of `Result&lt;T&gt;`.
    /// Roslyn's `Arity` is the same for both, which is why this needs no second rule —
    /// and why it needs a test saying so.
    /// </summary>
    [Fact]
    public void A_reference_to_a_constructed_generic_type_carries_the_definitions_arity()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public class Result<T> {}

                public class Uses
                {
                    public Result<int> Held = null;
                }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var use = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax>()
            .First();

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/Result+1#",
            ScipSymbols.Of(model.GetSymbolInfo(use).Symbol!));
    }

    /// <summary>
    /// <para>
    /// **A reference to a constructed generic method is its own overload, not the first
    /// one.** `ContainingType.GetMembers()` answers with definitions and `M&lt;int&gt;` is
    /// not `Equals` to `M&lt;T&gt;`, so the search for the reference itself missed — and a
    /// miss returning the empty string is the *plain* overload's spelling.
    /// </para>
    /// <para>
    /// This was a **wrong** edge rather than a missing one: find-references on the generic
    /// overload answered nothing, and find-references on the plain one answered a call
    /// that was not its. Nothing in a row count notices either.
    /// </para>
    /// </summary>
    /// <remarks>
    /// **The definition's own symbol is asserted beside the reference's**, because that is
    /// what makes this a claim about the mapping. A test asserting only that the reference
    /// is `M(+1).` is green with the plain overload deleted, where the ordinal is `+0` for
    /// a different reason.
    /// </remarks>
    [Fact]
    public void A_reference_to_a_constructed_generic_method_is_its_own_overload()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public static class C
                {
                    public static void M() {}

                    public static void M<T>() {}
                }

                public static class Calls
                {
                    public static void Both()
                    {
                        C.M();
                        C.M<int>();
                    }
                }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var calls = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Select(call => ScipSymbols.Of(model.GetSymbolInfo(call).Symbol!))
            .ToList();

        Assert.Equal(
            [
                "scip-csharp-2 nuget Walked 0.0.0.0 N/C#M().",
                "scip-csharp-2 nuget Walked 0.0.0.0 N/C#M(+1).",
            ],
            calls);

        // And the two definitions are those same two strings, which is what makes the
        // pair above a join rather than two spellings that merely differ.
        Assert.Equal(
            calls,
            Members(compilation, "N.C")
                .Where(member => member.Name == "M")
                .OrderBy(member => member.GetDocumentationCommentId(), StringComparer.Ordinal)
                .Select(member => ScipSymbols.Of(member))
                .ToList());
    }

    /// <summary>
    /// **An extension method reduced to its instance form is counted as its
    /// declaration.** `items.Where(...)` binds to a symbol whose parameter list has lost
    /// the receiver, so it is not `Equals` to anything `GetMembers()` answers either — the
    /// same miss as a constructed generic method, and the reason the ordinal is counted
    /// for `ReducedFrom` before `OriginalDefinition`.
    /// </summary>
    /// <remarks>
    /// This is also what keeps the refusal in `Disambiguator` unreachable: it is the other
    /// way a method can fail to appear among its own containing type's members.
    /// </remarks>
    [Fact]
    public void A_reduced_extension_method_is_counted_as_its_declaration()
    {
        var compilation = Compile(
            """
            namespace N
            {
                public static class Ext
                {
                    public static int Sum(this int[] items) => 0;

                    public static int Sum(this int[] items, int seed) => seed;
                }

                public static class Calls
                {
                    public static int Both(int[] items) => items.Sum() + items.Sum(1);
                }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var calls = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Select(call => ScipSymbols.Of(model.GetSymbolInfo(call).Symbol!))
            .ToList();

        Assert.Equal(
            [
                "scip-csharp-2 nuget Walked 0.0.0.0 N/Ext#Sum().",
                "scip-csharp-2 nuget Walked 0.0.0.0 N/Ext#Sum(+1).",
            ],
            calls);
    }

    /// <summary>
    /// **The arity goes inside the escape, not after it.** A backtick-escaped name ends at
    /// its closing backtick, so `` `Ünïcode`+1 `` is two things and no strict parser reads
    /// it as one name. For a simple identifier the question does not arise, because `+` is
    /// itself a simple-identifier character — which is what makes the suffix need no
    /// escaping at all.
    /// </summary>
    [Fact]
    public void A_non_ascii_generic_name_carries_its_arity_inside_the_escape()
    {
        var compilation = Compile("namespace N { public class Ünïcode<T> {} }");

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 N/`Ünïcode+1`#",
            Symbol(compilation, "N.Ünïcode`1"));
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
        Assert.Contains("scip-csharp-2 nuget Walked 0.0.0.0 N/T#M().", symbols);
        Assert.Contains("scip-csharp-2 nuget Walked 0.0.0.0 N/T#M(+1).", symbols);
        Assert.Contains("scip-csharp-2 nuget Walked 0.0.0.0 N/T#M(+2).", symbols);
    }

    /// <summary>
    /// **The ordinal does not depend on the order the compiler was handed the files.**
    /// A partial class's members arrive in syntax-tree order, so the reference indexer's
    /// count gives one commit two different symbol sets depending on the walk — and a
    /// sealed identity hashes the facts. Counting a `docId`-sorted order removes it.
    /// </summary>
    /// <remarks>
    /// **Each member carries its own identity into the comparison**, which is what makes
    /// this a claim about the mapping rather than the set. Under declaration-order
    /// counting the two overloads swap which is bare and which is `+1`, so the set of
    /// strings is identical either way and an assertion over sorted symbols alone is
    /// green with the sort in `Disambiguator` deleted.
    /// </remarks>
    [Fact]
    public void A_partial_classs_overloads_do_not_depend_on_file_order()
    {
        const string First = "namespace N { public partial class T { public void M(int a) {} } }";
        const string Second = "namespace N { public partial class T { public void M(string a) {} } }";

        List<string> Mapping(params string[] sources) =>
            [.. Members(Compile(sources), "N.T")
                .Where(member => member.Name == "M")
                .Select(member => $"{member.GetDocumentationCommentId()} => {ScipSymbols.Of(member)}")
                .OrderBy(pair => pair, StringComparer.Ordinal)];

        var forwards = Mapping(First, Second);
        var backwards = Mapping(Second, First);

        Assert.Equal(
            [
                "M:N.T.M(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 N/T#M().",
                "M:N.T.M(System.String) => scip-csharp-2 nuget Walked 0.0.0.0 N/T#M(+1).",
            ],
            forwards);
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

        Assert.StartsWith("scip-csharp-2 nuget System.", symbol, StringComparison.Ordinal);
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

    /// <summary>
    /// **A local function has no global name either.** Nothing outside the method that
    /// declares it can reach it, so a global string for it is wrong on the grounds a
    /// local's is — and two of one name in two methods of one type would be one string.
    /// </summary>
    [Fact]
    public void A_local_function_has_no_symbol()
    {
        var compilation = Compile(
            """
            public class T
            {
                void One() { int Helper() => 1; Helper(); }
                void Two() { int Helper() => 2; Helper(); }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var declarations = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax>()
            .ToList();

        Assert.Equal(2, declarations.Count);
        Assert.All(
            declarations,
            declaration => Assert.Null(ScipSymbols.Of(model.GetDeclaredSymbol(declaration)!)));
    }

    /// <summary>
    /// **An empty Roslyn name is not a descriptor.** The grammar escapes a name by
    /// wrapping it in backticks, and a bare pair is no name at all — so a lambda, whose
    /// name is empty, would take one string for every lambda in its method.
    /// </summary>
    [Fact]
    public void A_lambda_has_no_symbol()
    {
        var compilation = Compile(
            """
            public class T
            {
                void M()
                {
                    System.Func<int> one = () => 1;
                    System.Func<int> two = () => 2;
                }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);

        var lambdas = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ParenthesizedLambdaExpressionSyntax>()
            .ToList();

        Assert.Equal(2, lambdas.Count);
        Assert.All(
            lambdas,
            lambda => Assert.Null(ScipSymbols.Of(model.GetSymbolInfo(lambda).Symbol!)));
    }

    [Fact]
    public void A_namespace_is_a_symbol_of_its_own_and_the_global_one_is_not()
    {
        var compilation = Compile("namespace A.B { public class T {} }");
        var inner = compilation.GetTypeByMetadataName("A.B.T")!.ContainingNamespace;

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 A/B/", ScipSymbols.Of(inner));
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 A/", ScipSymbols.Of(inner.ContainingNamespace));
        Assert.Null(ScipSymbols.Of(compilation.GlobalNamespace));
    }

    /// <summary>The symbol an expression binds to, found by the text it is written as.</summary>
    /// <remarks>
    /// An element access as well as an invocation, because an indexer's reference is on
    /// the `[...]` and there is no name node for it — which is why a walk that collects
    /// only `SimpleNameSyntax` never sees one.
    /// </remarks>
    private static ISymbol Bound(Compilation compilation, string text)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var node = tree.GetRoot()
                .DescendantNodes()
                .FirstOrDefault(candidate =>
                    candidate is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax
                        or Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax
                    && candidate.ToString() == text);

            if (node is not null
                && compilation.GetSemanticModel(tree).GetSymbolInfo(node).Symbol is { } symbol)
            {
                return symbol;
            }
        }

        throw new ArgumentException($"nothing in this compilation is written `{text}`", nameof(text));
    }

    /// <summary>The `docId => symbol` mapping of every same-named member, sorted.</summary>
    /// <remarks>
    /// **Each member carries its own identity into the comparison.** Two overloads that
    /// swap which is bare and which is `+1` produce the *same set* of strings, so an
    /// assertion over sorted symbols alone is green with the ordering defect intact.
    /// </remarks>
    private static List<string> Mapping(Compilation compilation, string type, string name) =>
        [.. Members(compilation, type)
            .Where(member => member.Name == name)
            .Select(member => $"{member.GetDocumentationCommentId()} => {ScipSymbols.Of(member)}")
            .OrderBy(pair => pair, StringComparer.Ordinal)];

    /// <summary>
    /// <para>
    /// **A reference reached through a *constructed* generic type does not depend on the
    /// order the compiler was handed the files.** `GetMembers()` on `Box&lt;int&gt;`
    /// answers *constructed* members, and substitution makes `M(T)` and `M(int)` share
    /// **both** sort keys — one docId `M:P.Box{System.Int32}.M(System.Int32)` and one
    /// display string `P.Box&lt;int&gt;.M(int)`. A stable sort over them therefore
    /// preserved `GetMembers()` order, which for a partial class is the order the files
    /// arrived in: one commit had two symbol sets, and in one of them the call named the
    /// wrong overload.
    /// </para>
    /// <para>
    /// `src.sigla`'s charter rests on the opposite — two indexes of one commit must not
    /// disagree — and a sealed identity hashes the facts.
    /// </para>
    /// </summary>
    /// <remarks>
    /// **The call is asserted equal to the declaration it bound to, not merely stable
    /// across the two orders.** Stability alone is satisfied by both orders being wrong
    /// together, which is what counting on the canonical definition buys: a reference's
    /// ordinal is its declaration's by construction, because the two are counted over one
    /// list.
    /// </remarks>
    [Fact]
    public void A_reference_through_a_constructed_type_does_not_depend_on_file_order()
    {
        const string First = "namespace P { public partial class Box<T> { public void M(T x) {} } }";
        const string Second = "namespace P { public partial class Box<T> { public void M(int x) {} } }";
        const string Uses = "namespace P { public class Uses { public void Go() { var b = new Box<int>(); b.M(1); } } }";

        (List<string> Definitions, string Call, string ItsDeclaration) Walk(params string[] sources)
        {
            var compilation = Compile(sources);
            var call = (IMethodSymbol)Bound(compilation, "b.M(1)");

            // The declaration the compiler says the call bound to, spelled by this
            // producer — which is the string find-references has to be asked for.
            return (
                Mapping(compilation, "P.Box`1", "M"),
                ScipSymbols.Of(call)!,
                ScipSymbols.Of(call.OriginalDefinition)!);
        }

        var forwards = Walk(First, Second, Uses);
        var backwards = Walk(Second, First, Uses);

        Assert.Equal(
            [
                "M:P.Box`1.M(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#M().",
                "M:P.Box`1.M(`0) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#M(+1).",
            ],
            forwards.Definitions);
        Assert.Equal(forwards.Definitions, backwards.Definitions);

        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#M().", forwards.Call);
        Assert.Equal(forwards.Call, backwards.Call);
        Assert.Equal(forwards.ItsDeclaration, forwards.Call);
        Assert.Equal(backwards.ItsDeclaration, backwards.Call);
    }

    /// <summary>
    /// **The non-partial form of that shape was a plain wrong edge, on every build.** One
    /// file, so nothing is order-dependent — and `b.M(1)` on `Box&lt;int&gt;` still minted
    /// `M(+1).` while the declaration of `M(int)` minted `M().`. Find-references on
    /// `M(int)` answered nothing and find-references on `M(T)` answered a call that was
    /// not its.
    /// </summary>
    [Fact]
    public void A_reference_through_a_constructed_type_is_the_overload_it_bound_to()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Box<T>
                {
                    public void M(T x) {}

                    public void M(int x) {}
                }

                public class Uses
                {
                    public void Go() { var b = new Box<int>(); b.M(1); }
                }
            }
            """);

        var call = (IMethodSymbol)Bound(compilation, "b.M(1)");

        Assert.Equal("P.Box<int>.M(int)", call.ToDisplayString());
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#M().", ScipSymbols.Of(call));
        Assert.Equal(ScipSymbols.Of(call.OriginalDefinition), ScipSymbols.Of(call));
    }

    /// <summary>
    /// **Nesting a generic inside a generic changes nothing**, because the ordinal is
    /// counted on the innermost containing type either way — and the arity of both links
    /// is spelled by the chain rather than by this rule.
    /// </summary>
    [Fact]
    public void A_reference_through_a_generic_nested_in_a_generic_is_its_own_overload()
    {
        const string First =
            "namespace P { public partial class Outer<T> { public partial class Box<U> { public void M(U x) {} } } }";
        const string Second =
            "namespace P { public partial class Outer<T> { public partial class Box<U> { public void M(int x) {} } } }";
        const string Uses =
            "namespace P { public class Uses { public void Go() { var b = new Outer<string>.Box<int>(); b.M(1); } } }";

        string Call(params string[] sources)
        {
            var compilation = Compile(sources);
            var call = (IMethodSymbol)Bound(compilation, "b.M(1)");

            Assert.Equal(ScipSymbols.Of(call.OriginalDefinition), ScipSymbols.Of(call));

            return ScipSymbols.Of(call)!;
        }

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 P/Outer+1#Box+1#M().",
            Call(First, Second, Uses));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 P/Outer+1#Box+1#M().",
            Call(Second, First, Uses));
    }

    /// <summary>
    /// **Three overloads collapsing onto one constructed signature, rather than two.** Two
    /// members tying is enough to make a stable sort keep file order; three is what shows
    /// the count is a property of the code and not of which pair happened to tie.
    /// </summary>
    [Fact]
    public void Three_overloads_colliding_under_substitution_keep_their_own_ordinals()
    {
        const string First = "namespace P { public partial class Box<T, U> { public void M(T x) {} } }";
        const string Second = "namespace P { public partial class Box<T, U> { public void M(U x) {} } }";
        const string Third = "namespace P { public partial class Box<T, U> { public void M(int x) {} } }";
        const string Uses = "namespace P { public class Uses { public void Go() { var b = new Box<int, int>(); b.M(1); } } }";

        (List<string> Definitions, string Call) Walk(params string[] sources)
        {
            var compilation = Compile(sources);
            var call = (IMethodSymbol)Bound(compilation, "b.M(1)");

            Assert.Equal(ScipSymbols.Of(call.OriginalDefinition), ScipSymbols.Of(call));

            return (Mapping(compilation, "P.Box`2", "M"), ScipSymbols.Of(call)!);
        }

        var forwards = Walk(First, Second, Third, Uses);
        var backwards = Walk(Third, Second, First, Uses);

        Assert.Equal(
            [
                "M:P.Box`2.M(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+2#M().",
                "M:P.Box`2.M(`0) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+2#M(+1).",
                "M:P.Box`2.M(`1) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+2#M(+2).",
            ],
            forwards.Definitions);
        Assert.Equal(forwards.Definitions, backwards.Definitions);
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Box+2#M().", forwards.Call);
        Assert.Equal(forwards.Call, backwards.Call);
    }

    /// <summary>
    /// **An interface member reached through a constructed interface is the same arm.**
    /// `h.M(1)` on `IHold&lt;int&gt;` is a reference through a constructed type, so it was
    /// filed under whichever overload `GetMembers()` answered first — and an explicit
    /// implementation's own descriptor is an escaped name, which the ordinal has to sit
    /// outside of because it is a *method* there.
    /// </summary>
    [Fact]
    public void An_explicit_interface_member_on_a_constructed_type_is_its_own_overload()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public interface IHold<T>
                {
                    void M(T x);

                    void M(int x);
                }

                public class Box<T> : IHold<T>
                {
                    void IHold<T>.M(T x) {}

                    void IHold<T>.M(int x) {}
                }

                public class Uses
                {
                    public void Go(IHold<int> h) { h.M(1); }
                }
            }
            """);

        var call = (IMethodSymbol)Bound(compilation, "h.M(1)");

        Assert.Equal("P.IHold<int>.M(int)", call.ToDisplayString());
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/IHold+1#M().", ScipSymbols.Of(call));
        Assert.Equal(ScipSymbols.Of(call.OriginalDefinition), ScipSymbols.Of(call));

        // The implementations are the escaped-name arm, and each carries its own ordinal
        // in the method disambiguator slot rather than inside the escape.
        Assert.Equal(
            [
                "M:P.Box`1.P#IHold{T}#M(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`P.IHold<T>.M`().",
                "M:P.Box`1.P#IHold{T}#M(`0) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`P.IHold<T>.M`(+1).",
            ],
            Mapping(compilation, "P.Box`1", "P.IHold<T>.M"));
    }

    /// <summary>
    /// <para>
    /// **`this[int]` beside `this[int, int]` is two symbols.** Roslyn names every indexer
    /// of a type `this[]`, and a term descriptor is `&lt;name&gt; '.'` with no
    /// disambiguator slot — so both minted `Box+1#`this[]`.` and the run died on
    /// `codemarkup.Definition`'s conflict, exactly as two arities of a type name did.
    /// </para>
    /// <para>
    /// The ordinal goes inside the name, and inside the backticks: `this[]` is escaped
    /// because a bracket is not an identifier character, and an escaped name ends at its
    /// closing backtick.
    /// </para>
    /// </summary>
    [Fact]
    public void Overloaded_indexers_are_distinct_symbols()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Box<T>
                {
                    public T this[int i] => default!;

                    public T this[int i, int j] => default!;
                }
            }
            """);

        Assert.Equal(
            [
                "P:P.Box`1.Item(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]`.",
                "P:P.Box`1.Item(System.Int32,System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]+1`.",
            ],
            Mapping(compilation, "P.Box`1", "this[]"));
    }

    /// <summary>
    /// **A count of parameters would not have done.** `this[int]` beside `this[string]`
    /// differ only in parameter *type*, which is why this reuses the ordinal methods
    /// already have — ordered by documentation id, which encodes them — rather than
    /// inventing a second scheme.
    /// </summary>
    [Fact]
    public void Indexers_differing_only_in_parameter_type_are_distinct_symbols()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Box
                {
                    public int this[int i] => 0;

                    public int this[string s] => 0;
                }
            }
            """);

        Assert.Equal(
            [
                "P:P.Box.Item(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box#`this[]`.",
                "P:P.Box.Item(System.String) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box#`this[]+1`.",
            ],
            Mapping(compilation, "P.Box", "this[]"));
    }

    /// <summary>
    /// **Only an overloaded indexer moves.** The ordinal is empty for the first or only
    /// sibling, exactly as `M().` is for a method — the same asymmetry that made arity 0
    /// free, and what keeps this off every property in every index rather than only off
    /// the ones nobody overloads.
    /// </summary>
    /// <remarks>
    /// The spelling is asserted rather than compared against a second build, because the
    /// claim is about a string a shipped index already holds.
    /// </remarks>
    [Fact]
    public void A_lone_indexer_keeps_the_string_it_had()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Box<T>
                {
                    public T this[int i] => default!;

                    public T Item2 => default!;
                }
            }
            """);

        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]`.",
            Symbol(compilation, "P.Box`1", "this[]"));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#Item2.",
            Symbol(compilation, "P.Box`1", "Item2"));
    }

    /// <summary>
    /// **The arity suffix and the indexer ordinal appear in one symbol**, on their own
    /// descriptors: the arity belongs to the containing type's name and the ordinal to the
    /// term's, so neither has to know about the other.
    /// </summary>
    [Fact]
    public void An_indexer_on_a_generic_type_carries_the_arity_and_its_own_ordinal()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Ünïcode<T>
                {
                    public T this[int i] => default!;

                    public T this[string s] => default!;
                }
            }
            """);

        Assert.Equal(
            [
                "P:P.Ünïcode`1.Item(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/`Ünïcode+1`#`this[]`.",
                "P:P.Ünïcode`1.Item(System.String) => scip-csharp-2 nuget Walked 0.0.0.0 P/`Ünïcode+1`#`this[]+1`.",
            ],
            Mapping(compilation, "P.Ünïcode`1", "this[]"));
    }

    /// <summary>
    /// **An explicit-interface indexer carries the ordinal inside the backticks it was
    /// already escaped by.** Its Roslyn name is `P.IShelf.this[]` — escaped for the dots
    /// and the brackets — so appending after the escape would spell
    /// `` `P.IShelf.this[]`+1 ``, which is two things and no name a parser reads back.
    /// </summary>
    [Fact]
    public void An_explicit_interface_indexer_carries_its_ordinal_inside_the_escape()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public interface IShelf
                {
                    int this[int i] { get; }

                    int this[string s] { get; }
                }

                public class Box : IShelf
                {
                    int IShelf.this[int i] => 0;

                    int IShelf.this[string s] => 0;
                }
            }
            """);

        Assert.Equal(
            [
                "P:P.Box.P#IShelf#Item(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box#`P.IShelf.this[]`.",
                "P:P.Box.P#IShelf#Item(System.String) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box#`P.IShelf.this[]+1`.",
            ],
            Mapping(compilation, "P.Box", "P.IShelf.this[]"));
    }

    /// <summary>
    /// **An indexer's reference is its declaration, through a constructed type too.** The
    /// ordinal is counted on the canonical definition, so this needs no rule of its own —
    /// which is the reason the two halves of this commit are one mechanism and not two.
    /// </summary>
    [Fact]
    public void A_reference_to_an_overloaded_indexer_is_its_own_declaration()
    {
        var compilation = Compile(
            """
            namespace P
            {
                public class Box<T>
                {
                    public T this[T key] => default!;

                    public T this[int i] => default!;

                    public T this[string s] => default!;
                }

                public class Uses
                {
                    public int Named(Box<int> b) => b["a"];

                    public int Numbered(Box<int> b) => b[1];
                }
            }
            """);

        var use = (IPropertySymbol)Bound(compilation, "b[\"a\"]");

        // `this[T]` and `this[int]` are one signature on `Box<int>` — the indexer form of
        // the tie that put a method's ordinal back on file order.
        var collided = (IPropertySymbol)Bound(compilation, "b[1]");

        Assert.Equal("P.Box<int>.this[int]", collided.ToDisplayString());
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]`.", ScipSymbols.Of(collided));
        Assert.Equal(ScipSymbols.Of(collided.OriginalDefinition), ScipSymbols.Of(collided));

        Assert.Equal(
            [
                "P:P.Box`1.Item(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]`.",
                "P:P.Box`1.Item(System.String) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]+1`.",
                "P:P.Box`1.Item(`0) => scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]+2`.",
            ],
            Mapping(compilation, "P.Box`1", "this[]"));

        Assert.Equal("P.Box<int>.this[string]", use.ToDisplayString());
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Box+1#`this[]+1`.", ScipSymbols.Of(use));
        Assert.Equal(ScipSymbols.Of(use.OriginalDefinition), ScipSymbols.Of(use));
    }

    /// <summary>
    /// <para>
    /// **The bound on the indexer arm is the language's, and this is the table.** C#
    /// permits two same-named members of one type only for methods and indexers, so a
    /// field, an event and a non-indexer property cannot collide and need no ordinal —
    /// which is what keeps this change off every term descriptor in every index.
    /// </para>
    /// <para>
    /// Established rather than assumed, because the arm is only sound if the bound holds:
    /// every other pair a term descriptor could be minted from is CS0102, including the
    /// two that look like exceptions — an `IndexerName`'d indexer beside a property of
    /// that name (Roslyn's `Name` stays `this[]` and the *metadata* name is what moves),
    /// and two halves of a partial class.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A type the compiler rejects is outside the bound: these snippets mint one string
    /// for two members and always did, and no build reaches the walk.
    /// </remarks>
    [Theory]
    [InlineData("two properties", "public int X => 0;", "public string X => null;")]
    [InlineData("a field beside a property", "public int X;", "public int X => 0;")]
    [InlineData("a field beside an event", "public int X;", "public event System.Action X;")]
    [InlineData("a property beside an event", "public int X => 0;", "public event System.Action X;")]
    [InlineData("a property beside a method", "public int X => 0;", "public void X() {}")]
    [InlineData("a property beside a nested type", "public int X => 0;", "public class X {}")]
    [InlineData(
        "an IndexerName'd indexer beside a property of that name",
        "[System.Runtime.CompilerServices.IndexerName(\"X\")] public int this[int i] => 0;",
        "public int X => 0;")]
    public void Only_a_method_or_an_indexer_can_be_two_same_named_members(
        string shape, string one, string other)
    {
        var together = Compile($"namespace P {{ public class Box {{ {one} {other} }} }}");
        var split = Compile(
            $"namespace P {{ public partial class Box {{ {one} }} }}",
            $"namespace P {{ public partial class Box {{ {other} }} }}");

        foreach (var compilation in new[] { together, split })
        {
            Assert.Contains(
                "CS0102",
                compilation.GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.Id));
        }

        Assert.NotEqual(string.Empty, shape);
    }

    /// <summary>
    /// <para>
    /// <b>A `file`-scoped type still collides with another file's, and that is the one
    /// shape left in the collision class.</b> C# 11's `file class Hidden` may be declared
    /// once per file in one namespace — the compiler mangles only the *metadata* name, as
    /// it does for an indexer — so two of them mint one `P/Hidden#`, and so do their
    /// members. The run dies on `codemarkup.SymbolInfo`, keyed `{symbol}` with the
    /// signature on the value side, whenever the two differ in any member: exit 134,
    /// `Conflict: predicate PredicateId(8) already holds a different fact under this key`.
    /// </para>
    /// <para>
    /// **Not fixed here, and not for lack of a slot.** A `file` type is file-local by the
    /// language's own word, so the question is whether it has a global name at all —
    /// `HasGlobalName` already answers no for everything else that is, and
    /// `codemarkup.FileLocalXRef` is where a span-to-span answer lives. That is a decision
    /// about what a consumer may join on, not a spelling, and it belongs to whoever takes
    /// it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>A gate on a known collision, so that closing it is deliberate.</b> This is
    /// asserted as the *current* behaviour and goes red the moment somebody separates the
    /// two — which is the point: a reader must not take "the collision class is closed"
    /// from the arity and the indexer and believe it of this.
    /// </remarks>
    [Fact]
    public void A_file_local_type_still_takes_one_symbol_for_two_declarations()
    {
        var compilation = Compile(
            "namespace P { file class Hidden { public int Value; } }",
            "namespace P { file class Hidden { public string Value = \"x\"; } }");

        Assert.Empty(
            compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var hidden = compilation.GlobalNamespace
            .GetNamespaceMembers()
            .Single(space => space.Name == "P")
            .GetTypeMembers("Hidden");

        // Two distinct types — Roslyn mangles the metadata name and not `Name`.
        Assert.Equal(2, hidden.Length);
        Assert.NotEqual(hidden[0].MetadataName, hidden[1].MetadataName);

        // One symbol for both, and one for each pair of members under them.
        Assert.Equal("scip-csharp-2 nuget Walked 0.0.0.0 P/Hidden#", ScipSymbols.Of(hidden[0]));
        Assert.Equal(ScipSymbols.Of(hidden[0]), ScipSymbols.Of(hidden[1]));
        Assert.Equal(
            "scip-csharp-2 nuget Walked 0.0.0.0 P/Hidden#Value.",
            ScipSymbols.Of(hidden[0].GetMembers("Value").Single()));
        Assert.Equal(
            ScipSymbols.Of(hidden[0].GetMembers("Value").Single()),
            ScipSymbols.Of(hidden[1].GetMembers("Value").Single()));
    }

    /// <summary>
    /// <para>
    /// <b>A containing type does not list the implementing half of a partial member, and
    /// the walk is handed exactly that symbol.</b> This is the claim two rounds of this
    /// change got wrong — the first from `ReducedFrom`, the second from canonicality — so
    /// it is asserted here rather than argued in a comment: `GetMembers()` answers the
    /// declaring half alone, `GetDeclaredSymbol` on the second declaration answers the
    /// other one, and `Equals` says the two are not the same symbol.
    /// </para>
    /// <para>
    /// Every per-member fact rests on it. A sibling search for the unlisted half misses,
    /// and a per-symbol value read from it is the other half's key filled twice.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("a method", "partial void Ping();", "partial void Ping() {}", "Ping")]
    [InlineData("a property", "public partial int V { get; }", "public partial int V => 1;", "V")]
    [InlineData(
        "an indexer",
        "public partial int this[int i] { get; }",
        "public partial int this[int i] => i;",
        "this[]")]
    public void An_implementing_half_is_not_listed_by_its_own_containing_type(
        string shape, string declaring, string implementing, string name)
    {
        var compilation = Compile(
            $"namespace P {{ public partial class Split {{ {declaring} }} }}",
            $"namespace P {{ public partial class Split {{ {implementing} }} }}");

        Assert.Empty(
            compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var type = compilation.GetTypeByMetadataName("P.Split")!;
        var listed = type.GetMembers(name).Single();

        var declared = compilation.SyntaxTrees
            .Select(tree => (Tree: tree, Model: compilation.GetSemanticModel(tree)))
            .SelectMany(pair => pair.Tree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax>()
                .Select(node => pair.Model.GetDeclaredSymbol(node))
                .Where(symbol => symbol is not null && symbol.Name == name))
            .ToList();

        // Two declarations, two symbols, and only one of them in the type's own list.
        Assert.Equal(2, declared.Count);
        Assert.Single(declared, symbol => symbol!.Equals(listed, SymbolEqualityComparer.Default));
        Assert.NotEqual(string.Empty, shape);
    }

    /// <summary>
    /// <b>Both halves of a partial member mint one string, in either layout.</b> They are
    /// one member: one signature, one docId, one entity — so a second string for the
    /// implementing half would be a symbol nothing else in the index agrees exists, and
    /// the ordinal that produced it would be counted against a list the half is not in.
    /// </summary>
    /// <remarks>
    /// <b>Both halves in one file as well as across two</b>, which C# permits and which is
    /// the layout that reached `codemarkup.Definition`'s conflict rather than the sibling
    /// search's miss. The string is the same either way, and has to be.
    /// </remarks>
    [Theory]
    [InlineData("a method", "partial void Ping();", "partial void Ping() {}", "Ping", "Ping().")]
    [InlineData("a property", "public partial int V { get; }", "public partial int V => 1;", "V", "V.")]
    [InlineData(
        "an indexer",
        "public partial int this[int i] { get; }",
        "public partial int this[int i] => i;",
        "this[]",
        "`this[]`.")]
    public void Both_halves_of_a_partial_member_mint_one_symbol(
        string shape, string declaring, string implementing, string name, string descriptor)
    {
        const string Prefix = "scip-csharp-2 nuget Walked 0.0.0.0 P/Split#";

        foreach (var sources in new[]
        {
            new[]
            {
                $"namespace P {{ public partial class Split {{ {declaring} }} }}",
                $"namespace P {{ public partial class Split {{ {implementing} }} }}",
            },
            [$"namespace P {{ public partial class Split {{ {declaring} {implementing} }} }}"],
        })
        {
            var compilation = Compile(sources);

            Assert.Empty(
                compilation.GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

            var halves = compilation.SyntaxTrees
                .Select(tree => (Tree: tree, Model: compilation.GetSemanticModel(tree)))
                .SelectMany(pair => pair.Tree.GetRoot()
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax>()
                    .Select(node => pair.Model.GetDeclaredSymbol(node))
                    .Where(symbol => symbol is not null && symbol.Name == name)
                    .Select(symbol => ScipSymbols.Of(symbol!)))
                .ToList();

            Assert.Equal(2, halves.Count);
            Assert.Equal([$"{Prefix}{descriptor}", $"{Prefix}{descriptor}"], halves);
        }

        Assert.NotEqual(string.Empty, shape);
    }

    /// <summary>
    /// <b>An implementing half takes its declaring half's ordinal, not the first
    /// overload's.</b> The sibling list is the containing type's, which holds declaring
    /// halves — so the half that is not in it is placed by being reduced to the one that
    /// is. Guessing the empty ordinal here would file the second overload under the
    /// first's symbol, which is a wrong edge rather than a missing one.
    /// </summary>
    [Fact]
    public void A_partial_members_implementing_half_takes_its_declaring_halfs_ordinal()
    {
        var compilation = Compile(
            "namespace P { public partial class Split { public partial void Ping();"
            + " public partial void Ping(int times); } }",
            "namespace P { public partial class Split { public partial void Ping(int times) {}"
            + " public partial void Ping() {} } }");

        Assert.Empty(
            compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        // The declaring halves, keyed by the docId that decides their order — so a swap
        // between the two is visible rather than hidden by a set comparison.
        Assert.Equal(
            [
                "M:P.Split.Ping => scip-csharp-2 nuget Walked 0.0.0.0 P/Split#Ping().",
                "M:P.Split.Ping(System.Int32) => scip-csharp-2 nuget Walked 0.0.0.0 P/Split#Ping(+1).",
            ],
            Mapping(compilation, "P.Split", "Ping"));

        // And each implementing half spells the declaring half it belongs to, which is
        // the file the *implementations* are written in reversed order in.
        foreach (var implementing in compilation
            .GetTypeByMetadataName("P.Split")!
            .GetMembers("Ping")
            .Cast<IMethodSymbol>()
            .Select(method => method.PartialImplementationPart!))
        {
            Assert.Equal(
                ScipSymbols.Of(implementing.PartialDefinitionPart!),
                ScipSymbols.Of(implementing));
        }
    }

    /// <summary>
    /// <para>
    /// <b>A symbol this producer cannot spell has no string, and does not take the run
    /// down with it.</b> A built-in operator is the shape that provokes it: <c>a + b</c>
    /// binds <c>int.operator +(int, int)</c>, a synthesised <c>IMethodSymbol</c> whose
    /// containing type is <c>int</c> — and <c>int</c> lists no <c>op_Addition</c> member,
    /// so the sibling search finds nothing to place it among.
    /// </para>
    /// <para>
    /// <b>Where the name does exist the identity still does not</b>, which is the other
    /// branch: <c>string</c> declares <c>op_Equality</c>, and the symbol
    /// <c>x == y</c> binds is not that member. So a lone sibling of the right name is not
    /// evidence, and answering the empty ordinal on the strength of it would spell a real
    /// member's symbol for something that is not it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>This is the branch two rounds of this change called unreachable.</b> The first
    /// argued it from <c>ReducedFrom</c>, the second from canonicality, and an everyday
    /// partial member falsified both — so it is a refusal that is counted rather than an
    /// exception, and this is the test that provokes it.
    /// </remarks>
    [Fact]
    public void A_symbol_this_producer_cannot_spell_is_no_symbol_rather_than_an_exception()
    {
        var compilation = Compile(
            "namespace P { public class T { public int Add(int a, int b) => a + b;"
            + " public bool Same(string x, string y) => x == y;"
            // A local, for the flag-clear half below. Initialised from a literal rather
            // than an expression, so it adds no `BinaryExpressionSyntax` to the two the
            // operator assertions count.
            + " public int One() { int t = 1; return t; } } }");

        Assert.Empty(
            compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);

        var operators = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax>()
            .Select(node => (IMethodSymbol)model.GetSymbolInfo(node).Symbol!)
            .ToList();

        Assert.Equal(2, operators.Count);
        Assert.All(operators, method => Assert.Equal(MethodKind.BuiltinOperator, method.MethodKind));

        // The name is there for one of them and not the other, and neither is spellable.
        Assert.Empty(operators[0].ContainingType.GetMembers(operators[0].Name));
        Assert.Single(operators[1].ContainingType.GetMembers(operators[1].Name));

        Assert.All(
            operators,
            method =>
            {
                Assert.Null(ScipSymbols.Of(method, out var unspellable));
                Assert.True(unspellable);
            });

        // **The two nulls are kept apart on purpose**, so both halves are asserted. A
        // parameter has a global name and answers one; a local has none by decision and
        // answers null with the flag *clear*, which is what distinguishes "this producer
        // declines to name it" from "this producer could not".
        var parameter = model.GetDeclaredSymbol(
            tree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax>()
                .First())!;

        Assert.NotNull(ScipSymbols.Of(parameter, out var named));
        Assert.False(named);

        var local = model.GetDeclaredSymbol(
            tree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax>()
                .First(declarator => model.GetDeclaredSymbol(declarator) is
                    { Kind: Microsoft.CodeAnalysis.SymbolKind.Local }))!;

        Assert.Equal(Microsoft.CodeAnalysis.SymbolKind.Local, local.Kind);
        Assert.Null(ScipSymbols.Of(local, out var counted));
        Assert.False(counted);
    }

    /// <summary>
    /// **And the one pair the compiler does accept is the one the arm covers**, which is
    /// what makes the table above a bound rather than a list.
    /// </summary>
    [Fact]
    public void Two_indexers_are_the_pair_the_compiler_accepts()
    {
        var compilation = Compile(
            "namespace P { public class Box { public int this[int i] => 0; public int this[string s] => 0; } }");

        Assert.Empty(
            compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Equal(
            2,
            Members(compilation, "P.Box")
                .Where(member => member.Name == "this[]")
                .Select(member => ScipSymbols.Of(member))
                .Distinct()
                .Count());
    }
}
