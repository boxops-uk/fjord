using System;
using System.Collections.Generic;
using System.Linq;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Every declaration the walk reaches is expressed as a <c>csharp</c> entity or
/// counted as one this layer cannot express — never neither.</b>
/// </para>
/// <para>
/// The entity builder ends in <c>_ =&gt; null</c>, twice: once over the symbol kinds and
/// once over the type kinds a named type can have. A declaration that falls into either
/// gets no definition, no location, and — unless the arm it fell into is one of the two
/// routed through the counter — no tally either, so the run reports a clean index whose
/// cross-references point at definitions nothing wrote. That is the failure this census
/// exists to make loud, and it has already happened once: an event declaration used to
/// be dropped in exactly this silence.
/// </para>
/// <para>
/// <b>The population comes from Roslyn, not from a list here.</b> <c>Forms</c> is every
/// concrete declaration syntax the compiler derives from the seven bases
/// <c>Indexer.IndexTree</c>'s switch names, so a form a newer compiler adds enters the
/// population by itself and <see cref="The_fixture_holds_every_declaration_form_roslyn_declares"/>
/// is what refuses to pass until somebody has decided which side of the seam it falls
/// on. A census rather than a list of kinds, because a list is exhaustive only until the
/// next language version.
/// </para>
/// </summary>
public sealed class DeclarationCensusTests
{
    /// <summary>
    /// The seven bases <c>Indexer.IndexTree</c> switches on, which is what "the walk
    /// reaches it" means.
    /// </summary>
    /// <remarks>
    /// A field declaration is reached through its declarators — <c>int a, b;</c> is one
    /// declaration and two fields — so the base is named here and
    /// <see cref="Reached"/> takes the same step the walk does.
    /// </remarks>
    private static readonly Type[] Roots =
    [
        typeof(BaseTypeDeclarationSyntax),
        typeof(DelegateDeclarationSyntax),
        typeof(BaseMethodDeclarationSyntax),
        typeof(BasePropertyDeclarationSyntax),
        typeof(EnumMemberDeclarationSyntax),
        typeof(LocalFunctionStatementSyntax),
        typeof(BaseFieldDeclarationSyntax),
    ];

    /// <summary>Every concrete form Roslyn derives from one of <see cref="Roots"/>.</summary>
    private static readonly HashSet<Type> Forms =
    [
        .. typeof(ClassDeclarationSyntax).Assembly.GetTypes()
            .Where(type => type.IsPublic
                && !type.IsAbstract
                && Roots.Any(root => root.IsAssignableFrom(type))),
    ];

    /// <summary>
    /// One of every form in <see cref="Forms"/>, and an <c>extension</c> block whose own
    /// members are declarations inside a type that has no <c>csharp.NamedType</c>
    /// alternative.
    /// </summary>
    /// <remarks>
    /// <b>Parsed as <c>Preview</c>, which is what the extension form costs.</b> The
    /// pinned Roslyn declares <c>ExtensionDeclarationSyntax</c> and parses it only under
    /// that language version — but the walk's parse options come from MSBuild, so a
    /// checkout that opts in hands the real run one of these, and the next compiler bump
    /// hands it one either way.
    /// </remarks>
    private const string Fixture = """
        namespace Fixture
        {
            public delegate void Handler();

            public interface IFace
            {
                int Size { get; }
            }

            public enum Colour
            {
                Red,
            }

            public record Pair(int Left, int Right);

            public struct Point
            {
                public int X;
            }

            public class Thing : IFace
            {
                public int Total;

                public event Handler Changed;

                public event Handler Renamed { add { } remove { } }

                public int Size { get; set; }

                public int this[int index] => index;

                public Thing()
                {
                }

                ~Thing()
                {
                }

                public void Act()
                {
                    int Helper() => 1;
                    Helper();
                }

                public static Thing operator +(Thing left, Thing right) => left;

                public static implicit operator int(Thing thing) => 0;
            }

            public static class Ext
            {
                extension(int value)
                {
                    public int Doubled => value * 2;

                    public int Twice() => value * 2;
                }
            }
        }
        """;

    private static (SyntaxTree Tree, SemanticModel Model) Compile()
    {
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(Fixture),
            new CSharpParseOptions(LanguageVersion.Preview),
            path: "Fixture.cs");

        var compilation = CSharpCompilation.Create(
            "Fixture",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // A fixture that does not compile declares nothing, and every assertion below
        // would then be over an empty population.
        Assert.Empty(compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString()));

        return (tree, compilation.GetSemanticModel(tree));
    }

    /// <summary>
    /// Every declaration in the fixture the walk would hand to <c>Declare</c>, paired
    /// with the form that reached it.
    /// </summary>
    private static IEnumerable<(Type Form, SyntaxNode Node)> Reached(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is BaseFieldDeclarationSyntax field)
            {
                foreach (var declarator in field.Declaration.Variables)
                {
                    yield return (field.GetType(), declarator);
                }
            }
            else if (Forms.Contains(node.GetType()))
            {
                yield return (node.GetType(), node);
            }
        }
    }

    /// <summary>
    /// What the producer did with one declaration: the definition it wrote, and whether
    /// dropping it moved a counter.
    /// </summary>
    /// <remarks>
    /// <b>A builder per declaration, because the tallies are per cause and memoised.</b>
    /// A member of a type this layer cannot express is dropped by that type, which is
    /// counted once — so asking a shared builder about the second member would answer
    /// "already counted, not by me". One builder per declaration makes each answer for
    /// itself.
    /// </remarks>
    private static (bool Expressed, bool Counted) Verdict(SemanticModel model, SyntaxNode node)
    {
        var symbol = model.GetDeclaredSymbol(node);
        Assert.NotNull(symbol);

        var entities = new CsharpEntities((_, _) => { });
        var expressed = entities.Definition(symbol) is not null;

        return (expressed, entities.Inexpressible > 0);
    }

    /// <summary>
    /// <b>The fixture holds one of every declaration form the compiler can hand the
    /// walk.</b>
    /// </summary>
    /// <remarks>
    /// The census, and the reason the guard below is not vacuous: a fixture missing a
    /// form leaves that form's verdict unasserted, and a form Roslyn adds is one nobody
    /// has decided the seam's answer for. Red names it.
    /// </remarks>
    [Fact]
    public void The_fixture_holds_every_declaration_form_roslyn_declares()
    {
        var (tree, _) = Compile();

        var present = Reached(tree.GetRoot())
            .Select(reached => reached.Form.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(
            Forms.Select(form => form.Name).OrderBy(name => name, StringComparer.Ordinal),
            present);
    }

    /// <summary>
    /// <b>No declaration the walk reaches is dropped without being counted.</b>
    /// </summary>
    /// <remarks>
    /// The guard. A declaration is expressed — a <c>csharp.Definition</c> the walk can
    /// hang a location and a symbol off — or the run's own tally says one was lost.
    /// Neither is the silence, and no arm of the entity builder gets to answer that way.
    /// </remarks>
    [Fact]
    public void Every_declaration_the_walk_reaches_is_expressed_or_counted()
    {
        var (tree, model) = Compile();

        var silent = Reached(tree.GetRoot())
            .Where(reached => Verdict(model, reached.Node) is (false, false))
            .Select(reached => $"{reached.Form.Name}: "
                + $"{model.GetDeclaredSymbol(reached.Node)!.ToDisplayString()}")
            .ToList();

        Assert.Empty(silent);
    }

    /// <summary>
    /// <b>The declarations the counter answers for are these, and the rest are
    /// expressed.</b>
    /// </summary>
    /// <remarks>
    /// What <c>InexpressibleKinds</c>'s doc names, as a test rather than as prose: an
    /// event, which has no <c>csharp</c> entity at all, and a type kind with no
    /// <c>csharp.NamedType</c> alternative — an <c>extension</c> block, which takes its
    /// own members down with it. Red either way round: a declaration that stops being
    /// expressed lands here, and one that starts being expressed leaves.
    /// </remarks>
    [Fact]
    public void The_declarations_the_counter_answers_for_are_the_ones_the_doc_names()
    {
        var (tree, model) = Compile();

        var counted = Reached(tree.GetRoot())
            .Where(reached => Verdict(model, reached.Node) is (false, true))
            .Select(reached => $"{reached.Form.Name}: "
                + $"{model.GetDeclaredSymbol(reached.Node)!.ToDisplayString()}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "EventDeclarationSyntax: Fixture.Thing.Renamed",
                "EventFieldDeclarationSyntax: Fixture.Thing.Changed",
                "ExtensionDeclarationSyntax: Fixture.Ext.extension(int)",
                "MethodDeclarationSyntax: Fixture.Ext.extension(int).Twice()",
                "PropertyDeclarationSyntax: Fixture.Ext.extension(int).Doubled",
            ],
            counted);
    }
}
