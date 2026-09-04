using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Boxops.Fjord.Client;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Run 4.0's conflict census, re-aimed at the key that replaced the candidate one.</b>
/// </para>
/// <para>
/// The question is unchanged and was always the point of the run: <i>do two declarations
/// reach one entity key?</i> `src.Decl` was keyed `{module, name, line}`, so two
/// declarations on one line collided and reformatting a file changed what its declarations
/// *were*. `csharp.*` keys on what the compiler knows, so both failures should be
/// impossible — and after the walk is restructured there is no other detector, which is
/// why this is a gate rather than an exercise.
/// </para>
/// <para>
/// The fixture is the one review #34 asked for: two overloads on one line, a
/// conversion-operator pair, and a type with a same-line constructor — plus a pair of
/// types overloaded on <b>arity</b>, where the census reports a collision that is not
/// this unit's to remove. See
/// <see cref="Two_arities_of_one_type_name_reach_one_entity_key"/>.
/// </para>
/// </summary>
public sealed class EntityKeyCensusTests
{
    /// <summary>Everything on as few lines as the language allows.</summary>
    private const string Crammed = """
        namespace Fixture { public class Thing { public void M(int a) {} public void M(string a) {}
        public static implicit operator int(Thing t) => 0; public static implicit operator long(Thing t) => 0L;
        public int Count; public string Label { get; set; } }
        public class SameLine { public SameLine() {} public SameLine(int a) {} }
        public class Result { public bool Ok; } public class Result<T> { public T Value; } }
        """;

    /// <summary>The same code, formatted the way a person would write it.</summary>
    private const string Spread = """
        namespace Fixture
        {
            public class Thing
            {
                public void M(int a)
                {
                }

                public void M(string a)
                {
                }

                public static implicit operator int(Thing t) => 0;

                public static implicit operator long(Thing t) => 0L;

                public int Count;

                public string Label { get; set; }
            }

            public class SameLine
            {
                public SameLine()
                {
                }

                public SameLine(int a)
                {
                }
            }

            public class Result
            {
                public bool Ok;
            }

            public class Result<T>
            {
                public T Value;
            }
        }
        """;

    private static Compilation Compile(string source) =>
        CSharpCompilation.Create(
            "Fixture",
            [CSharpSyntaxTree.ParseText(SourceText.From(source), path: "Fixture.cs")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// A fact's key as a comparable string.
    /// </summary>
    /// <remarks>
    /// <c>FjordValue.Record</c> holds a list, so two structurally identical values are not
    /// equal by <c>record</c> equality — and this comparison is the whole test. Rendered
    /// rather than hashed so a failure names the key that collided.
    /// </remarks>
    private static string Render(uint predicate, FjordValue value)
    {
        var text = new StringBuilder(DotnetIndex.NameOf(predicate)).Append('(');
        Write(text, value);
        return text.Append(')').ToString();
    }

    private static void Write(StringBuilder text, FjordValue value)
    {
        switch (value)
        {
            case FjordValue.Int number:
                text.Append(number.Value);
                break;
            case FjordValue.Str str:
                text.Append('"').Append(str.Value).Append('"');
                break;
            case FjordValue.Bytes bytes:
                text.Append(Convert.ToHexStringLower(bytes.Value.Span));
                break;
            case FjordValue.Record record:
                text.Append('{');
                for (var index = 0; index < record.Fields.Count; index++)
                {
                    if (index > 0)
                    {
                        text.Append(", ");
                    }

                    Write(text, record.Fields[index]);
                }

                text.Append('}');
                break;
            case FjordValue.Union union:
                text.Append(union.Disc).Append(':');
                Write(text, union.Value);
                break;
            case FjordValue.Ref reference:
                // Every reference this producer writes is the target fact nested inline,
                // so the key it contributes is the target's own key — recursively.
                if (reference.Value is FjordRef.Nested nested)
                {
                    text.Append(Render(nested.Fact.Predicate, nested.Fact.Key));
                }
                else if (reference.Value is FjordRef.Id id)
                {
                    text.Append('#').Append(id.FactId);
                }

                break;
            default:
                throw new ArgumentException($"unrendered value {value.GetType().Name}");
        }
    }

    /// <summary>Every symbol this assembly declares, including parameters.</summary>
    private static IEnumerable<ISymbol> Declarations(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            yield return member;

            if (member is INamespaceOrTypeSymbol nested)
            {
                foreach (var inner in Declarations(nested))
                {
                    yield return inner;
                }
            }

            if (member is IMethodSymbol method)
            {
                foreach (var parameter in method.Parameters)
                {
                    yield return parameter;
                }

                foreach (var parameter in method.TypeParameters)
                {
                    yield return parameter;
                }
            }
        }
    }

    /// <summary>
    /// The predicates that carry an <b>identity</b>, which are the ones the census is
    /// about.
    /// </summary>
    /// <remarks>
    /// <b>`Parameter` and `TypeParameter` are deliberately structural and are excluded.</b>
    /// Neither key names its containing method — `TypeParameter`'s comment says so in as
    /// many words, and `Parameter`'s key is name, type and modifiers — so two parameters
    /// agreeing on all of them *are* one fact, and the ordered edge
    /// (`MethodParameter {method, index, parameter}`) is what ties one to a method. Asking
    /// "do two declarations reach one key" of them is asking the wrong question: sharing
    /// is the design, and it is asserted separately below rather than excused here.
    /// </remarks>
    private static readonly uint[] Identities =
    [
        DotnetIndex.Class, DotnetIndex.Interface, DotnetIndex.Record, DotnetIndex.Struct,
        DotnetIndex.Method, DotnetIndex.Field, DotnetIndex.Property, DotnetIndex.Namespace,
        DotnetIndex.FullName,
    ];

    private static Dictionary<string, List<ISymbol>> Census(Compilation compilation)
    {
        var entities = new CsharpEntities((_, _) => { });
        var byKey = new Dictionary<string, List<ISymbol>>(StringComparer.Ordinal);

        foreach (var symbol in Declarations(compilation.Assembly.GlobalNamespace))
        {
            if (entities.Entity(symbol) is not { } fact
                || !Identities.Contains(fact.Predicate))
            {
                continue;
            }

            var key = Render(fact.Predicate, fact.Key);

            if (!byKey.TryGetValue(key, out var reached))
            {
                byKey[key] = reached = [];
            }

            if (!reached.Any(seen => seen.OriginalDefinition.Equals(
                    symbol.OriginalDefinition, SymbolEqualityComparer.Default)))
            {
                reached.Add(symbol.OriginalDefinition);
            }
        }

        return byKey;
    }

    /// <summary>
    /// **Zero conflicts, apart from the arity pair.** Two declarations reaching one key is
    /// the defect Run 4 existed to remove, and the fixture is built out of the three
    /// shapes that caused it.
    /// </summary>
    /// <remarks>
    /// The one exception is stated by name rather than filtered by shape, so a *second*
    /// collision fails here even though it would be one entry in the same list — and
    /// <see cref="Two_arities_of_one_type_name_reach_one_entity_key"/> is what makes the
    /// exception a claim about the layer rather than a hole in this assertion.
    /// </remarks>
    [Fact]
    public void No_two_declarations_reach_one_entity_key_except_the_arity_pair()
    {
        var collisions = Census(Compile(Crammed))
            .Where(entry => entry.Value.Count > 1)
            .Select(entry => $"{entry.Key} <- {string.Join(", ", entry.Value.Select(s => s.ToDisplayString()))}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        var collision = Assert.Single(collisions);

        // The predicate, the name in its key and the two declarations that reached it —
        // not the whole rendered key, which carries every modifier field and would move
        // when one is added for a reason that has nothing to do with this.
        Assert.StartsWith(
            "csharp.Class({csharp.FullName({csharp.Name(\"Result\")",
            collision,
            StringComparison.Ordinal);
        Assert.EndsWith("<- Fixture.Result, Fixture.Result<T>", collision, StringComparison.Ordinal);
    }

    /// <summary>
    /// <para>
    /// **Two arities of one type name reach one entity key, and that is an open
    /// maintainer decision rather than a defect this unit took.**
    /// </para>
    /// <para>
    /// `csharp.Class` is key-only and its key leads with a `csharp.FullName` of
    /// `{name, containingNamespace}` — a simple name with the arity stripped. So
    /// `Result` and `Result&lt;T&gt;` are **one** fact with **two**
    /// `csharp.DefinitionLocation` rows beside it, which is indistinguishable from a
    /// partial class. `Interface`, `Record`, `Struct` and `csharp.FullName` itself have
    /// the same shape.
    /// </para>
    /// <para>
    /// **Fixing the symbol string did not fix this, and it is what made it visible**: the
    /// run used to die on the `codemarkup.SymbolInfo` conflict before anything could
    /// merge. What it needs is either a schema change — a fingerprint move, so a flag day
    /// — or a redefinition of what `csharp.Name` holds. `docs/unified-plan/15-retire-code-sigla.md`
    /// §S3 carries it as the open decision, and the indexer README carries it as a
    /// limitation a consumer will meet.
    /// </para>
    /// </summary>
    /// <remarks>
    /// **The symbols are asserted beside the merged key**, because the two halves are what
    /// a reader has to keep apart: `src.Symbol` distinguishes the arities and the entity
    /// layer does not. A test asserting only the collision reads as "arity is broken",
    /// and one asserting only the symbols reads as "arity is fixed".
    /// </remarks>
    [Fact]
    public void Two_arities_of_one_type_name_reach_one_entity_key()
    {
        var compilation = Compile(Crammed);
        var entities = new CsharpEntities((_, _) => { });

        var arities = new[] { "Fixture.Result", "Fixture.Result`1" }
            .Select(name => compilation.GetTypeByMetadataName(name)!)
            .ToList();

        var keys = arities
            .Select(type => Render(DotnetIndex.Class, entities.Entity(type)!.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var symbols = arities.Select(type => ScipSymbols.Of(type)!).ToList();

        Assert.Single(keys);
        Assert.Equal(
            [
                "scip-csharp-2 nuget Fixture 0.0.0.0 Fixture/Result#",
                "scip-csharp-2 nuget Fixture 0.0.0.0 Fixture/Result+1#",
            ],
            symbols);
    }

    /// <summary>
    /// **Reformatting produces an identical set of keys.** No key holds a line, a column
    /// or an offset, so what a file's declarations *are* no longer depends on how it is
    /// laid out — the property `src.Decl` could not have.
    /// </summary>
    [Fact]
    public void Reformatting_the_fixture_does_not_move_a_single_key()
    {
        var crammed = Census(Compile(Crammed)).Keys.OrderBy(key => key, StringComparer.Ordinal);
        var spread = Census(Compile(Spread)).Keys.OrderBy(key => key, StringComparer.Ordinal);

        Assert.Equal(crammed, spread);
    }

    /// <summary>
    /// **The conversion-operator pair is the case that needs `docId`.** They overload on
    /// *return type*, so a name-plus-parameters signature is not enough — and a SCIP
    /// method descriptor has no return-type slot either, which is why the disambiguator is
    /// Roslyn's own documentation id.
    /// </summary>
    [Fact]
    public void Two_conversion_operators_differing_only_in_return_type_are_two_keys()
    {
        var entities = new CsharpEntities((_, _) => { });
        var thing = Compile(Crammed).GetTypeByMetadataName("Fixture.Thing")!;

        var conversions = thing.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Conversion)
            .Select(method => Render(DotnetIndex.Method, entities.Entity(method)!.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, conversions.Count);
    }

    /// <summary>
    /// **Two overloads written on one line are two keys**, which is the collision the old
    /// `{module, name, line}` key produced on ordinary code.
    /// </summary>
    [Fact]
    public void Two_overloads_on_one_line_are_two_keys()
    {
        var entities = new CsharpEntities((_, _) => { });
        var thing = Compile(Crammed).GetTypeByMetadataName("Fixture.Thing")!;

        var overloads = thing.GetMembers("M")
            .OfType<IMethodSymbol>()
            .Select(method => Render(DotnetIndex.Method, entities.Entity(method)!.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, overloads.Count);
    }

    /// <summary>
    /// **Two local functions of one name in two methods of one type are two keys.**
    /// A local function is not a member, so the census's walk above never reaches one —
    /// and its documentation id is `M:Fixture.Host.Helper`, which names the containing
    /// *type* the key already holds and not the method that declares it.
    /// </summary>
    /// <remarks>
    /// **Two of one name in one method still reach one key**, and that is not fixed here:
    /// separating sibling scopes needs the scope path
    /// `docs/unified-plan/13-indexer-runs-amended.md` reserves for a local that genuinely
    /// needs a name, and an ordinal within the method would renumber on every insertion —
    /// the instability Run 4 exists to remove.
    /// </remarks>
    [Fact]
    public void Two_local_functions_of_one_name_in_two_methods_are_two_keys()
    {
        var compilation = Compile(
            """
            namespace Fixture
            {
                public class Host
                {
                    public void One() { int Helper() => 1; Helper(); }
                    public void Two() { int Helper() => 2; Helper(); }
                }
            }
            """);

        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var entities = new CsharpEntities((_, _) => { });

        var keys = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax>()
            .Select(node => entities.Entity(model.GetDeclaredSymbol(node)!)!)
            .Select(fact => Render(fact.Predicate, fact.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, keys.Count);
    }

    /// <summary>
    /// **Two parameters of the same shape are one fact, and that is the schema's
    /// intent.** `csharp.Parameter`'s key is name, type and modifiers with no containing
    /// method, exactly as `csharp.TypeParameter`'s is name and constraints — so
    /// `M(int a)` and `SameLine(int a)` share their parameter, and what distinguishes them
    /// is the ordered edge. The census excludes both predicates for this reason, and this
    /// is the assertion that makes the exclusion a claim rather than an excuse.
    /// </summary>
    [Fact]
    public void Two_parameters_of_the_same_shape_are_one_fact_tied_by_different_edges()
    {
        var emitted = new List<(uint Predicate, FjordFact Fact)>();
        var entities = new CsharpEntities((predicate, fact) => emitted.Add((predicate, fact)));
        var compilation = Compile(Crammed);

        var method = compilation.GetTypeByMetadataName("Fixture.Thing")!
            .GetMembers("M").OfType<IMethodSymbol>()
            .First(m => m.Parameters[0].Type.SpecialType == SpecialType.System_Int32);
        var constructor = compilation.GetTypeByMetadataName("Fixture.SameLine")!
            .Constructors.First(c => c.Parameters.Length == 1);

        var first = entities.Entity(method.Parameters[0])!;
        var second = entities.Entity(constructor.Parameters[0])!;

        Assert.Equal(
            Render(first.Predicate, first.Key),
            Render(second.Predicate, second.Key));

        // The edges are what differ, and both exist.
        entities.Edges(method);
        entities.Edges(constructor);

        var edges = emitted
            .Where(entry => entry.Predicate == DotnetIndex.MethodParameter)
            .Select(entry => Render(entry.Predicate, entry.Fact.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, edges.Count);
    }

    /// <summary>
    /// **A type and its same-line constructor are different entities of different kinds**,
    /// where the old model reached one key for both when they shared a line and the
    /// constructor was synthesised.
    /// </summary>
    [Fact]
    public void A_type_and_its_same_line_constructors_are_distinct_entities()
    {
        var entities = new CsharpEntities((_, _) => { });
        var type = Compile(Crammed).GetTypeByMetadataName("Fixture.SameLine")!;

        var typeKey = Render(DotnetIndex.Class, entities.Entity(type)!.Key);
        var constructors = type.Constructors
            .Select(method => Render(DotnetIndex.Method, entities.Entity(method)!.Key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, constructors.Count);
        Assert.DoesNotContain(typeKey, constructors);
    }
}
