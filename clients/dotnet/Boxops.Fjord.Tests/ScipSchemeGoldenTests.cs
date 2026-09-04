using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boxops.Fjord.Indexer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The symbol format, pinned against the scheme token that declares it.</b>
/// </para>
/// <para>
/// <c>config.Setting {dimension = "symbol-scheme"}</c> is the only check a
/// cross-repository fan-out has before it trusts a string match between two databases. So
/// a format that moves under an unchanged token makes a fan-out join identities that are
/// no longer comparable and answer wrongly — no exception, no empty result, no row that
/// looks odd. Nothing in a suite of per-shape assertions notices, because each of them
/// moves with the format it is asserting.
/// </para>
/// <para>
/// <b>This file is what ties the two.</b> The golden's first line is the token, the rest
/// are symbols this producer mints over one fixture, and the test asserts the declared
/// token *is* <see cref="ScipSymbols.Scheme"/>. Changing the format alone turns it red;
/// changing the token alone turns it red; changing both is one deliberate edit of one
/// file, made by whoever also has to say so in a commit message.
/// </para>
/// <para>
/// <b>A golden only guards the shapes its fixture carries</b>, so the fixture is chosen by
/// the hazard each shape catches rather than for variety — <see cref="Sources"/> names
/// them one by one. A shape missing from here is a format move that lands green.
/// </para>
/// </summary>
public sealed class ScipSchemeGoldenTests
{
    /// <summary>
    /// <b>The shapes the golden has to carry, and the hazard each one catches.</b>
    /// <list type="bullet">
    /// <item><c>Plain</c> — a non-generic type with three method overloads, one of them
    /// generic: the method disambiguator's format, and the arity-0 asymmetry that keeps
    /// every non-generic descriptor byte-identical.</item>
    /// <item><c>Plain.this[int]</c> — a **lone** indexer: appending the sibling ordinal
    /// unconditionally would move every indexer in every index, so the empty ordinal is
    /// what keeps the change to the members that actually collide.</item>
    /// <item><c>Box&lt;T&gt;</c> — a generic type with a member, a constructor and its
    /// parameter, a type parameter and a nested type: the arity rides on the containing
    /// type's descriptor, so everything under it inherits one.</item>
    /// <item><c>Shelf&lt;T&gt;.Put</c>, declared across two partial halves — an overload
    /// pair that **collides under substitution**: on <c>Shelf&lt;int&gt;</c> the two share
    /// one docId and one display string, so counting the sibling order on the constructed
    /// member left both sort keys tied and the ordinal fell back to the order the files
    /// arrived in. A future change to sibling ordering cannot land quietly with this
    /// pinned.</item>
    /// <item><c>Shelf&lt;T&gt;</c>'s three indexers — an **overloaded indexer**, which
    /// minted one symbol for every declaration until the ordinal went inside the name:
    /// one pair differing only in parameter *type*, one colliding under substitution, and
    /// the arity suffix and the ordinal appearing in one string.</item>
    /// <item><c>Explicit</c> — two explicit-interface indexers of one interface: the
    /// ordinal has to land inside the backticks a name was already escaped by.</item>
    /// <item><c>Shelf&lt;T&gt;.Fill</c>, and <c>Shelf&lt;T&gt;.this[string]</c> beside it,
    /// each declared in one half and implemented in the other — a **partial member**,
    /// whose implementing half is a declaration its own containing type does not list. The
    /// sibling search for it missed and the ordinal refused to guess, so this shape was an
    /// exception and a dead run; both halves have to mint one string, and a golden over
    /// both files is what says so.</item>
    /// <item><c>Ünïcode&lt;T&gt;</c> — the arity appended *before* escaping, so the
    /// escaped name is one identifier a strict parser reads back. Referenced as well as
    /// declared: the reference is where a *constructed* form of it is spelled, which is
    /// the half a declaration alone leaves unguarded.</item>
    /// <item><c>Uses</c> — a reference to each of those: a constructed generic type, a
    /// constructed generic method, a member of a constructed type through the
    /// substitution collision, a partial member, an escaped generic name, and an indexer,
    /// whose reference is an element access with no name node of its own.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <b>Several files, and <c>Shelf&lt;T&gt;</c> split across two of them</b>, because a
    /// partial class's members arrive in the order the compiler was handed the trees —
    /// which is the input <see cref="The_format_does_not_depend_on_the_order_of_the_files"/>
    /// varies.
    /// </remarks>
    private static readonly string[] Sources =
    [
        """
        namespace Pinned
        {
            public class Plain
            {
                public int Count;

                public void Do() {}

                public void Do(int times) {}

                public void Do<T>() {}

                public int this[int slot] => Count;
            }

            public class Box<T>
            {
                public T Item { get; }

                public Box(T item) => Item = item;

                public class Inner {}
            }

            public class Ünïcode<T> {}
        }
        """,
        """
        namespace Pinned
        {
            public partial class Shelf<T>
            {
                public void Put(T item) {}

                public partial void Fill(T item);

                public T this[T key] => default!;

                public T this[int index] => default!;

                public partial T this[string name] { get; }
            }
        }
        """,
        """
        namespace Pinned
        {
            public partial class Shelf<T>
            {
                public void Put(int index) {}

                public partial void Fill(T item) {}

                public partial T this[string name] => default!;
            }
        }
        """,
        """
        namespace Pinned
        {
            public interface IShelf
            {
                int this[int index] { get; }

                int this[string name] { get; }
            }

            public class Explicit : IShelf
            {
                int IShelf.this[int index] => 0;

                int IShelf.this[string name] => 0;
            }
        }
        """,
        """
        namespace Pinned
        {
            public class Uses
            {
                public Box<int> Wrapped = new Box<int>(1);

                public Plain Bare = new Plain();

                public Shelf<int> Stack = new Shelf<int>();

                public Ünïcode<int> Odd = new Ünïcode<int>();

                public void Call()
                {
                    Bare.Do();
                    Bare.Do<int>();
                    int held = Wrapped.Item;
                    int slot = Bare[0];
                    Stack.Put(1);
                    Stack.Fill(2);
                    int named = Stack["a"];
                }
            }
        }
        """,
    ];

    private static string GoldenPath => Path.Combine(
        FjordServer.RepositoryRoot, "clients", "dotnet", "golden", "symbol-scheme.txt");

    /// <summary>
    /// <b>Every symbol the fixture mints, and the token they are minted under.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declarations and references are pinned as separate lines</b>, prefixed
    /// <c>def</c> and <c>ref</c>, because the two are minted from different symbols and a
    /// reference is where a *constructed* one is resolved: <c>Box&lt;int&gt;</c> must
    /// spell its definition's descriptor, <c>Do&lt;int&gt;()</c> must spell its own
    /// overload's ordinal rather than the first one's, and <c>Stack.Put(1)</c> must spell
    /// the overload it bound to rather than whichever sibling sorted first under
    /// substitution. Merging the two into one set would hide exactly those, since a
    /// correct reference is byte-identical to the declaration it points at.
    /// </para>
    /// <para>
    /// <b>An element access as well as a name</b>, because an indexer's reference is the
    /// <c>[...]</c> and there is no name node for it — so a walk collecting only
    /// <c>SimpleNameSyntax</c> pins no indexer reference at all.
    /// </para>
    /// </remarks>
    private static List<string> Minted(params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "Pinned",
            sources.Select((source, index) =>
                CSharpSyntaxTree.ParseText(SourceText.From(source), path: $"Pinned{index}.cs")),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // A fixture that stopped compiling would go on minting symbols, and a shape it no
        // longer declares is one this golden silently stops guarding.
        Assert.Empty(
            compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var minted = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (model.GetDeclaredSymbol(node) is { } declared
                    && ScipSymbols.Of(declared) is { } declaration)
                {
                    minted.Add($"def {declaration}");
                }

                if (node is Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax
                        or Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax
                    && model.GetSymbolInfo(node).Symbol is { } bound
                    && bound.Kind is not SymbolKind.Namespace
                    && ScipSymbols.Of(bound) is { } reference)
                {
                    minted.Add($"ref {reference}");
                }
            }
        }

        // The type parameters, which no syntax node above declares as a symbol of its own
        // in every position, and which are the descriptor that inherits the arity.
        foreach (var generic in new[] { "Pinned.Box`1", "Pinned.Shelf`1", "Pinned.Ünïcode`1" })
        {
            foreach (var parameter in compilation.GetTypeByMetadataName(generic)!.TypeParameters)
            {
                minted.Add($"def {ScipSymbols.Of(parameter)}");
            }
        }

        return [.. minted];
    }

    /// <summary>
    /// <b>The golden, and the token it is a golden for.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FJORD_GOLDEN=update</c> rewrites the file rather than asserting against it, the
    /// way <c>emit-golden.sh</c> rewrites the wire goldens: a format this producer states
    /// is regenerated on purpose and never to make a test pass.
    /// </para>
    /// <para>
    /// <b>Regeneration is what would otherwise defeat the tie, so it refuses.</b> Pinning
    /// strings beside a token catches a format that moved and a golden that did not — but
    /// somebody who moves the format and regenerates is green again with the token still
    /// saying revision 2. So the update path checks first: a minted set that differs from
    /// a golden declaring this token is refused, naming the rule.
    /// </para>
    /// <para>
    /// <b>Two ways past that refusal, and the second is what adding a shape takes.</b>
    /// Move <see cref="ScipSymbols.Scheme"/>, which is the answer when the format moved;
    /// or delete the golden and let the update path write a new one, which is the answer
    /// when the *fixture* gained a shape and no symbol moved. The refusal is deliberately
    /// blunt about which happened, and a deleted-and-rewritten file is what a reviewer
    /// sees in the diff either way.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_symbol_format_is_the_one_this_scheme_token_declares()
    {
        var declared = $"scheme {ScipSymbols.Scheme}";
        var minted = Minted(Sources);
        var pinned = Pinned();

        if (Environment.GetEnvironmentVariable("FJORD_GOLDEN") == "update")
        {
            Assert.False(
                pinned.Count > 0
                    && pinned[0] == declared
                    && !pinned.Skip(1).SequenceEqual(minted),
                "the symbol format moved and `ScipSymbols.Scheme` did not. Move the token "
                + "in the same commit: `config.Setting {dimension = \"symbol-scheme\"}` is "
                + "the only check a fan-out has before it trusts a string match between two "
                + "databases, and a format that moves under an unchanged token makes it "
                + "join identities that are no longer comparable and answer wrongly, with "
                + "no error anywhere");

            File.WriteAllLines(GoldenPath, [.. Header, declared, .. minted]);
            pinned = Pinned();
        }

        Assert.NotEmpty(pinned);
        Assert.Equal(declared, pinned[0]);
        Assert.Equal(minted, pinned.Skip(1));
    }

    /// <summary>
    /// <b>The same commit mints the same strings however the walk was invoked.</b>
    /// <c>Shelf&lt;T&gt;</c> is partial, so its members arrive in whichever order the
    /// compiler was handed the trees — and a reference reached through
    /// <c>Shelf&lt;int&gt;</c> is counted over *constructed* members, whose signatures
    /// substitution can make identical. `src.sigla`'s charter is that two indexes of one
    /// commit must not disagree, and a sealed identity hashes the facts.
    /// </summary>
    /// <remarks>
    /// <b>The two references the collision moves are named, not just compared.</b> Equality
    /// between the orders is satisfied by both of them being wrong together — which is the
    /// state a walk that counted sibling order on the constructed member was in for the
    /// non-partial form of this shape. <c>Put().</c> is the declaration of
    /// <c>Put(int)</c>, the overload <c>Stack.Put(1)</c> binds; <c>`this[]+1`.</c> is the
    /// declaration of <c>this[string]</c>, the one <c>Stack["a"]</c> binds.
    /// </remarks>
    [Fact]
    public void The_format_does_not_depend_on_the_order_of_the_files()
    {
        var forwards = Minted(Sources);

        Assert.Equal(forwards, Minted([.. Sources.Reverse()]));
        Assert.Contains("ref scip-csharp-2 nuget Pinned 0.0.0.0 Pinned/Shelf+1#Put().", forwards);
        Assert.Contains("def scip-csharp-2 nuget Pinned 0.0.0.0 Pinned/Shelf+1#Put().(index)", forwards);
        Assert.Contains("ref scip-csharp-2 nuget Pinned 0.0.0.0 Pinned/Shelf+1#`this[]+1`.", forwards);
        Assert.Contains("def scip-csharp-2 nuget Pinned 0.0.0.0 Pinned/Shelf+1#`this[]+1`.(name)", forwards);
    }

    /// <summary>
    /// The golden's content, without what is there for a reader — and empty rather than
    /// throwing when there is no file, which is the only state the update path may create
    /// one from.
    /// </summary>
    private static List<string> Pinned() =>
        File.Exists(GoldenPath)
            ? [.. File.ReadAllLines(GoldenPath).Where(line => line.Length > 0 && !line.StartsWith('#'))]
            : [];

    /// <summary>What the file says about itself, for whoever opens it first.</summary>
    private static readonly string[] Header =
    [
        "# The `src.Symbol` format, pinned against the scheme token that declares it.",
        "#",
        "# `config.Setting {dimension = \"symbol-scheme\"}` is the only check a fan-out has",
        "# before it trusts a string match between two databases, so a format that moved",
        "# under an unchanged token would make it join identities that are not comparable",
        "# and answer wrongly, silently. The first line is the token; the rest are the",
        "# symbols `ScipSchemeGoldenTests` mints over its own fixture — `def` where a",
        "# declaration minted it, `ref` where a reference did.",
        "#",
        "# The fixture's shapes are chosen by the hazard each one catches, and that list is",
        "# `Sources`' own doc comment: a shape missing from it is a format move that lands",
        "# green.",
        "#",
        "# Regenerate with FJORD_GOLDEN=update, and move the token in the same commit.",
        "",
    ];

    /// <summary>
    /// <b>The token in the constant is the token in the strings</b>, which is what makes
    /// the golden's first line a claim about the symbols rather than a label above them.
    /// A constant moved without the producer following it — a second spelling somewhere
    /// in the walk — leaves the golden self-consistent and every symbol wrong.
    /// </summary>
    [Fact]
    public void The_declared_token_is_the_one_every_pinned_symbol_carries()
    {
        var symbols = Minted(Sources);

        Assert.NotEmpty(symbols);
        Assert.All(
            symbols,
            symbol => Assert.Contains($" {ScipSymbols.Scheme} ", symbol, StringComparison.Ordinal));
    }
}
