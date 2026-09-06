using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>A Roslyn symbol as a SCIP symbol string — what <c>src.Symbol</c> holds.</b>
/// </para>
/// <para>
/// The format is the SCIP specification's: a scheme token, a package coordinate of
/// manager, name and version, then suffix-encoded descriptors. It is the cross-language,
/// cross-database join key, so the two things that matter about it are that it is
/// unambiguous and that two runs over one commit produce the same string.
/// </para>
/// <para>
/// <b>Why this is not <c>scip-dotnet</c>'s output, and why the token says so.</b> The
/// reference C# indexer attaches a namespace descriptor to the package rather than to its
/// parent namespace, so <c>DiffPlex.DiffBuilder.Model.DiffPaneModel</c> comes out as
/// <c>Model/DiffPaneModel#</c> and <c>A.B.T</c> collides with <c>C.B.T</c>; and by default
/// it writes the package of the code it is indexing as <c>. .</c>, which collides across
/// repositories. Both are deliberate for its purpose and neither is usable as a key a
/// fan-out seeks, so this writes conformant symbols under its own scheme token. Claiming
/// <c>scip-dotnet</c> without matching it byte for byte would be worse than either, and
/// neither difference can be repaired downstream — nothing can recover a namespace a
/// producer never wrote.
/// </para>
/// <para>
/// <b>A named type's descriptor carries its arity, because C# overloads a type name on
/// it.</b> <c>class Result</c> beside <c>class Result&lt;T&gt;</c> is the everyday idiom
/// and Roslyn's <c>Name</c> strips the count that the metadata name spells, so a
/// descriptor built from the name alone would be one string for both. That does not
/// conflate quietly, it **kills the run**: <c>codemarkup.SymbolInfo</c> is keyed
/// <c>{symbol}</c> with the signature on the value side, the signature always differs,
/// and ingest refuses one key with two values part-way through a write — so no repository
/// declaring such a pair could be indexed at all. The arity goes inside the descriptor's
/// name, <c>N/Result+1#</c>, which is the only place the specification has for it: it has
/// no arity provision and its one disambiguator slot is a method's.
/// </para>
/// <para>
/// <b>An overloaded indexer's descriptor carries a sibling ordinal, for the same reason
/// in the same place.</b> Roslyn names every indexer of a type <c>this[]</c>, so
/// <c>this[int]</c> beside <c>this[int, int]</c> was one string for two declarations and
/// killed the run the same way. A term descriptor is <c>&lt;name&gt; '.'</c> with no
/// disambiguator slot, so the ordinal goes inside the name too:
/// <c>N/Box+1#`this[]+1`.</c>, empty for the first or only one. Nothing else can collide —
/// C# permits two same-named members of a type only for methods and indexers.
/// </para>
/// <para>
/// <b>Both halves of a partial member are one member and take one string.</b>
/// <c>partial void Ping();</c> and <c>partial void Ping() { }</c> are two declarations of
/// one thing, and <see cref="Defining"/> is what everything keyed per member reduces to
/// first — because a containing type's <c>GetMembers()</c> lists the *defining* part and
/// never the implementing one, while <c>GetDeclaredSymbol</c> on the second declaration
/// hands the walk exactly that unlisted symbol.
/// </para>
/// <para>
/// <b>Nothing here throws.</b> A symbol this producer cannot spell has no string, and the
/// run reports how many — the same shape a declaration the entity layer cannot express
/// already has. An exception on a shape nobody anticipated takes down a whole index over
/// one declaration in somebody else's checkout, which is the wrong failure mode for a tool
/// pointed at code it did not write; a counted gap is one somebody can see and fix.
/// </para>
/// </summary>
internal static class ScipSymbols
{
    /// <summary>
    /// This producer's scheme token. A database says which schemes it holds in
    /// <c>config.Setting {dimension = "symbol-scheme"}</c>, and a cross-database join is
    /// within one scheme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The token is the format's revision, and it moves whenever the format does.</b>
    /// That check is the only thing a cross-repository fan-out has before it trusts a
    /// string match between two databases, so a format that moved under an unchanged token
    /// makes it join identities that are no longer comparable and answer wrongly, with no
    /// error anywhere. <c>scip-csharp</c> was revision 1, whose named-type descriptors
    /// carried no arity; <c>-2</c> is this one. The next move is <c>-3</c>, so the rule
    /// needs no new noun each time.
    /// </para>
    /// <para>
    /// <b>What ties the two together is a test, not this comment.</b>
    /// <c>ScipSchemeGoldenTests</c> pins representative symbol strings *against* this
    /// value, so moving the format alone turns it red and moving both is one deliberate
    /// edit of one file.
    /// </para>
    /// </remarks>
    public const string Scheme = "scip-csharp-2";

    /// <summary>
    /// The SCIP symbol for <paramref name="symbol"/>, or <see langword="null"/> where it
    /// has no global name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null rather than a synthesised name for anything file-local — a local, a label, a
    /// range variable, or a member declared inside a method body. SCIP models these as
    /// <c>local N</c>, an occurrence ordinal that moves when the file is edited;
    /// <c>codemarkup.FileLocalXRef</c> answers them span to span, so minting a string here
    /// would only create one that has to be invalidated.
    /// </para>
    /// <para>
    /// Null for the global namespace too, which has no descriptor of its own: it is the
    /// root the walk stops at, not a namespace anything is in.
    /// </para>
    /// </remarks>
    public static string? Of(ISymbol symbol) => Of(symbol, out _);

    /// <summary>
    /// The SCIP symbol for <paramref name="symbol"/>, with <paramref name="unspellable"/>
    /// set where the reason there is none is a gap in this producer rather than a decision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two nulls are not the same thing and a run must be able to tell them
    /// apart.</b> A local, a lambda and the global namespace have no global name *by
    /// decision*, and there are as many of them as the checkout has; a descriptor this
    /// producer cannot spell is a shape nobody anticipated, and one of those is worth
    /// looking at. Only the second sets the flag, and the walk turns it into the number a
    /// run prints.
    /// </para>
    /// <para>
    /// <b>Nothing is thrown either way.</b> Two rounds of this change argued that the
    /// unspellable case was unreachable — from <c>ReducedFrom</c>, then from
    /// canonicality — and both arguments were wrong about the same everyday shape, a
    /// partial member's implementation part. The third argument is not worth more than a
    /// failure mode that cannot kill a run.
    /// </para>
    /// </remarks>
    public static string? Of(ISymbol symbol, out bool unspellable)
    {
        unspellable = false;

        if (symbol is INamespaceSymbol { IsGlobalNamespace: true })
        {
            return null;
        }

        var chain = new List<ISymbol>();

        for (var here = symbol; here is not null; here = here.ContainingSymbol)
        {
            if (here is INamespaceSymbol { IsGlobalNamespace: true } or IAssemblySymbol or IModuleSymbol)
            {
                break;
            }

            if (!HasGlobalName(here))
            {
                return null;
            }

            chain.Add(here);
        }

        chain.Reverse();

        var text = new StringBuilder();
        text.Append(Scheme).Append(' ').Append(Package(symbol)).Append(' ');

        foreach (var link in chain)
        {
            if (!Descriptor(text, link))
            {
                unspellable = true;
                return null;
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Whether a symbol is one SCIP gives a global name. Everything a method body
    /// introduces is excluded, and so is anything whose kind SCIP has no descriptor for.
    /// </summary>
    private static bool HasGlobalName(ISymbol symbol) =>
        symbol switch
        {
            // The grammar escapes a name by wrapping it in backticks, and a bare pair is
            // no name at all — so a lambda, whose Roslyn name is empty, would take one
            // string for every lambda in its method and emit one no parser accepts.
            { Name.Length: 0 } => false,

            // A local function and a lambda are introduced by a method body, so nothing
            // outside that body can reach either.
            IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction,
            } => false,

            _ => symbol.Kind switch
            {
                SymbolKind.Namespace or SymbolKind.NamedType or SymbolKind.Method
                    or SymbolKind.Property or SymbolKind.Field or SymbolKind.Event
                    or SymbolKind.Parameter or SymbolKind.TypeParameter => true,
                _ => false,
            },
        };

    /// <summary>
    /// The package coordinate: the containing assembly's own identity, or <c>. .</c> for a
    /// symbol that has no assembly.
    /// </summary>
    /// <remarks>
    /// <b>The assembly names itself even for the code being indexed.</b> The reference
    /// indexer writes <c>. .</c> there to avoid publishing symbols into a namespace shared
    /// by every repository in the world; this schema's `Symbol` is exactly a key for
    /// joining across databases, so an empty coordinate would make the join unsound —
    /// two repositories' <c>Main/Program#</c> are not the same symbol. Which checkout a
    /// database was built from is `config.Setting {dimension = "repo"}`'s answer, and
    /// `src.FileOrigin`'s per file.
    /// </remarks>
    private static string Package(ISymbol symbol)
    {
        var identity = symbol.ContainingAssembly?.Identity;

        return identity is null
            ? "nuget . ."
            : $"nuget {Spaced(identity.Name)} {Spaced(identity.Version.ToString())}";
    }

    /// <summary>
    /// A package field, escaped the way the package coordinate is escaped: a space is
    /// doubled, and nothing else is touched.
    /// </summary>
    /// <remarks>
    /// <b>Not a descriptor name.</b> The three package fields are delimited by single
    /// spaces, so a space inside one is what has to be escaped — where a descriptor is
    /// delimited by its suffix and escapes with backticks. A version like <c>10.0.0.0</c>
    /// is ordinary here and would be mangled by the other rule.
    /// </remarks>
    private static string Spaced(string field) => field.Replace(" ", "  ");

    /// <summary>
    /// Append one link's descriptor, or answer <see langword="false"/> where this producer
    /// cannot spell it.
    /// </summary>
    /// <remarks>
    /// <b>The last arm is a refusal and not an exception, and so is a missing ordinal.</b>
    /// It should be unreachable: <see cref="HasGlobalName"/> is a switch over the same enum
    /// admitting exactly the kinds this one spells, and every link of the chain has been
    /// through it. But a kind added to that list and not to this one would otherwise kill a
    /// whole run over one declaration, which is a cost nobody chose for a mistake somebody
    /// would make once. A refusal loses the symbol string, keeps the entity and the
    /// location, and is counted.
    /// </remarks>
    private static bool Descriptor(StringBuilder text, ISymbol symbol)
    {
        switch (symbol.Kind)
        {
            case SymbolKind.Namespace:
                text.Append(Name(symbol)).Append('/');
                return true;
            case SymbolKind.NamedType:
                text.Append(TypeName((INamedTypeSymbol)symbol)).Append('#');
                return true;
            case SymbolKind.Method:
                if (Ordinal(symbol) is not { } ordinal)
                {
                    return false;
                }

                text.Append(Name(symbol)).Append('(').Append(ordinal).Append(").");
                return true;
            // A property, a field and an event are all terms. The reference indexer files
            // an event under `#`, which reads as a type and is the one divergence here that
            // is a plain disagreement with the grammar rather than a scope decision.
            case SymbolKind.Property:
            case SymbolKind.Field:
            case SymbolKind.Event:
                if (TermName(symbol) is not { } term)
                {
                    return false;
                }

                text.Append(term).Append('.');
                return true;
            case SymbolKind.Parameter:
                text.Append('(').Append(Name(symbol)).Append(')');
                return true;
            case SymbolKind.TypeParameter:
                text.Append('[').Append(Name(symbol)).Append(']');
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Which of its same-named siblings this member is: empty for the first or only one,
    /// <c>+N</c> for the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counted over a sorted order, not over declaration order.</b> The reference
    /// indexer counts <c>ContainingType.GetMembers()</c>, which for a partial class follows
    /// the order the compiler was handed the files — so one commit produces different
    /// symbols depending on how the walk was invoked, and a sealed identity hashes the
    /// facts. Sorting by <c>docId</c> — Roslyn's own signature encoding, which is a total
    /// order over overloads and includes the return type for a conversion operator — makes
    /// the count a property of the code.
    /// </para>
    /// <para>
    /// It is still an ordinal, so **inserting an overload that sorts earlier renumbers the
    /// ones after it**. That is inherent to the format and is why nothing keys on a symbol
    /// across revisions: the language layer's structural entity does that, and carries
    /// `docId` itself.
    /// </para>
    /// <para>
    /// <b>Both the sort key and the match are the canonical definition, which is what
    /// makes a reference's ordinal equal its declaration's.</b> A caller reaches here
    /// holding something <c>GetMembers()</c> may not list — <c>M&lt;int&gt;</c> is not
    /// <c>Equals</c> to <c>M&lt;T&gt;</c>, neither is an extension method reduced to its
    /// instance form, and neither is the implementing half of a partial member, which is a
    /// *declaration* rather than a reference — and, when it is reached through a
    /// constructed type,
    /// <c>GetMembers()</c> answers *constructed* members whose signatures are the
    /// substituted ones. **Substitution can make two of those signatures identical**:
    /// <c>Box&lt;T&gt;.M(T)</c> and <c>Box&lt;T&gt;.M(int)</c> are one docId and one
    /// display string on <c>Box&lt;int&gt;</c>, so sorting the constructed members left
    /// both keys tied and the stable sort fell back to <c>GetMembers()</c> order — file
    /// order for a partial class, and the wrong overload in one of the two orders.
    /// Reducing every sibling to <see cref="Counted"/> before sorting removes the tie,
    /// because the count is then taken over the same list the declaration is counted over.
    /// </para>
    /// <para>
    /// <b>A miss is null and never the empty string.</b> The empty string is the first
    /// sibling's own symbol, so answering it for a member that is not that sibling mints an
    /// edge pointing at a member nobody used — wrong rather than missing, and invisible to
    /// every row count. <see cref="Of"/> turns the null into a symbol this producer does
    /// not spell, and the run says how many.
    /// </para>
    /// <para>
    /// <b>The residue, stated:</b> nothing here can separate two members whose canonical
    /// definitions have the same <see cref="Signature"/>. Valid C# has none — a docId
    /// encodes the parameter list, and the return type for a conversion — but a synthesised
    /// definition with a null docId falls back to a display string, and two of those
    /// tying would put the ordinal back on <c>GetMembers()</c> order.
    /// </para>
    /// </remarks>
    private static string? Ordinal(ISymbol member)
    {
        if (member.ContainingType is not { } containing)
        {
            return string.Empty;
        }

        var wanted = Counted(member);

        // **Asked for by name.** This runs for every method and every indexer in a
        // checkout, and `GetMembers()` with a filter walks all of a type's members to find
        // the one or two that share a name — where `GetMembers(name)` is the lookup Roslyn
        // already keeps a map for.
        var siblings = containing.GetMembers(member.Name)
            .Where(sibling => sibling.Kind == member.Kind)
            .ToList();

        // **A lone sibling is answered without the sort, which is where the cost is.**
        // Almost every method and indexer in real source is the only one of its name, and
        // a `docId` per sibling to order a list of one is work no symbol string depends
        // on. It is still checked to *be* this member rather than assumed: the whole point
        // of the paragraph above.
        if (siblings.Count <= 1)
        {
            return siblings.Count == 1 && Same(siblings[0], wanted) ? string.Empty : null;
        }

        var sorted = siblings
            .OrderBy(sibling => Signature(Counted(sibling)), StringComparer.Ordinal)
            .ToList();

        return sorted.FindIndex(sibling => Same(sibling, wanted)) switch
        {
            < 0 => null,
            0 => string.Empty,
            var index => $"+{index}",
        };
    }

    /// <summary>Whether a sibling is the member an ordinal is being counted for.</summary>
    private static bool Same(ISymbol sibling, ISymbol wanted) =>
        Counted(sibling).Equals(wanted, SymbolEqualityComparer.Default);

    /// <summary>
    /// The half of a partial member that stands for both: its defining part, or the symbol
    /// itself where it is not one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A containing type lists the defining part and never the implementing one.</b>
    /// <c>GetMembers()</c> answers <c>partial void Ping();</c> alone, and
    /// <c>GetDeclaredSymbol</c> on <c>partial void Ping() { }</c> hands the walk exactly
    /// the symbol that is not in that list — so anything counted or keyed per member has
    /// to reduce to this first, or it is looking for a member the type does not list.
    /// </para>
    /// <para>
    /// <b>The two halves are one member and one string, which is what they are.</b> A
    /// declaring part and an implementing part are not an overload pair: they have one
    /// signature, one docId and one entity, and only the defining half carries the
    /// documentation comment — so a per-symbol fact read from the other half is a second
    /// value under one key.
    /// </para>
    /// <para>
    /// A method and a property are the two arms, which is the whole of what C# splits:
    /// an indexer is a property and a constructor is a method, so each goes with its own
    /// arm by being one. A partial event's descriptor is its name with no ordinal in it,
    /// so both halves already spell one string, and it reaches no per-member fact at all —
    /// <c>csharp</c> has no event entity, which <c>Declare</c> returns on.
    /// </para>
    /// </remarks>
    public static ISymbol Defining(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { PartialDefinitionPart: { } defining } => defining,
        IPropertySymbol { PartialDefinitionPart: { } defining } => defining,
        _ => symbol,
    };

    /// <summary>
    /// The definition an ordinal is counted for: the defining, unreduced, unconstructed
    /// member behind whatever the caller is holding.
    /// </summary>
    private static ISymbol Counted(ISymbol member) =>
        Defining(member) switch
        {
            // `items.Where(...)` calls a static method whose first parameter is `items`,
            // and the reduced form is not `Equals` to anything `GetMembers()` answers.
            IMethodSymbol method => (method.ReducedFrom ?? method).OriginalDefinition,
            var defining => defining.OriginalDefinition,
        };

    /// <summary>The total order same-named siblings are counted in.</summary>
    /// <remarks>
    /// Always applied to <see cref="Counted"/>'s canonical definition, never to a
    /// constructed member: substitution makes two overloads' signatures equal and a tie
    /// here puts the ordinal back on <c>GetMembers()</c> order. `GetDocumentationCommentId`
    /// is null for a few symbols the compiler synthesises, so the display string keeps the
    /// sort from collapsing where it is.
    /// </remarks>
    private static string Signature(ISymbol symbol) =>
        symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString();

    /// <summary>
    /// A descriptor's name: as written when it is a simple identifier, backtick-escaped
    /// when it is not, with an embedded backtick doubled.
    /// </summary>
    /// <remarks>
    /// <b>Simple means ASCII</b>, per the grammar — letters, digits, <c>_</c>, <c>+</c>,
    /// <c>-</c> and <c>$</c>. A legal C# identifier outside that set (`Ünïcode`) is escaped
    /// rather than passed through, which is where a `\w`-based test would emit a symbol a
    /// strict parser rejects. `.ctor` is the everyday case: the dot would otherwise
    /// terminate the descriptor.
    /// </remarks>
    private static string Name(ISymbol symbol) => Escaped(symbol.Name);

    /// <summary>
    /// A term descriptor's name: the member's own, and — for an indexer — which of its
    /// same-named siblings it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An overloaded indexer is two declarations that took one string, and that killed
    /// the run.</b> Roslyn names every indexer of a type <c>this[]</c> (an
    /// <c>IndexerName</c> attribute moves the *metadata* name and not this one), so
    /// <c>this[int]</c> beside <c>this[int, int]</c> both minted
    /// <c>Box+1#`this[]`.</c> — and <c>codemarkup.Definition</c> is keyed
    /// <c>{symbol, file}</c>, so ingest refused one key with two values part-way through
    /// the write, exactly as two arities of a type name did.
    /// </para>
    /// <para>
    /// <b>The ordinal goes inside the name, because the grammar has no slot for it.</b> A
    /// term descriptor is <c>&lt;name&gt; '.'</c> and the disambiguator slot exists only on
    /// <c>&lt;method&gt;</c> — the same conclusion the type arity reached, for the same
    /// reason. And it is <see cref="Ordinal"/> rather than a parameter count, because
    /// <c>this[int]</c> beside <c>this[string]</c> differ only in parameter *type*: the
    /// sibling ordinal is ordered by documentation id, which encodes those.
    /// </para>
    /// <para>
    /// <b>Only an overloaded indexer moves.</b> The ordinal is empty for the first or only
    /// sibling, exactly as <c>M().</c> is for a method, so a type with one indexer keeps
    /// the string it has — which is what keeps this off every property in every index.
    /// </para>
    /// <para>
    /// <b>Bounded to indexers, and that bound is the language's.</b> C# permits two
    /// same-named members of one type only for methods and indexers: every other pair a
    /// term descriptor could be minted from — two properties, a field beside a property, a
    /// field beside an event, a property beside a method or a nested type, an
    /// <c>IndexerName</c>'d indexer beside a property of that name — is CS0102, and an
    /// explicit-interface implementation carries the interface in its name
    /// (<c>P.IShelf.this[]</c>), so it can only collide with another implementation of the
    /// *same* interface member, which is an overloaded indexer again.
    /// <c>ScipSymbolsTests.Only_a_method_or_an_indexer_can_be_two_same_named_members</c>
    /// holds the table; a type the compiler rejects is outside it.
    /// </para>
    /// <para>
    /// <b>Appended before escaping.</b> <c>this[]</c> is escaped because a bracket is not
    /// an identifier character, and a backtick-escaped name ends at its closing backtick —
    /// so the ordinal has to land inside them: <c>`this[]+1`</c> is one name and
    /// <c>`this[]`+1</c> is two things.
    /// </para>
    /// </remarks>
    private static string? TermName(ISymbol symbol)
    {
        if (symbol is not IPropertySymbol { IsIndexer: true })
        {
            return Name(symbol);
        }

        return Ordinal(symbol) is { } ordinal ? Escaped(symbol.Name + ordinal) : null;
    }

    /// <summary>
    /// A named type's descriptor name: the name it is written with, and its arity where
    /// it has one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Arity 0 keeps the bare name</b>, so <c>Result</c> stays <c>Result</c> and every
    /// non-generic symbol string is the one it always was. Appending <c>+0</c> would move
    /// every symbol in every index for no gain: nothing collides at arity 0.
    /// </para>
    /// <para>
    /// <b>Appended before escaping, not after.</b> A backtick-escaped name ends at its
    /// closing backtick, so <c>`Ünïcode`+1</c> is two things and no parser reads it as one
    /// name — where <c>`Ünïcode+1`</c> is the name a strict parser decodes back to
    /// <c>Ünïcode+1</c>. For a simple identifier the two agree, because <c>+</c> is itself
    /// a simple-identifier character.
    /// </para>
    /// <para>
    /// <b>The suffix belongs to the containing type's own descriptor</b>, which is what
    /// makes a member of <c>Result&lt;T&gt;</c>, its parameters and its type parameters
    /// inherit the arity by construction rather than by a second rule — the chain in
    /// <see cref="Of"/> spells each link once.
    /// </para>
    /// </remarks>
    private static string TypeName(INamedTypeSymbol type) =>
        Escaped(type.Arity == 0 ? type.Name : $"{type.Name}+{type.Arity}");

    private static string Escaped(string name) =>
        IsSimple(name) ? name : $"`{name.Replace("`", "``")}`";

    private static bool IsSimple(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var character in name)
        {
            var simple = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '_' or '+' or '-' or '$';

            if (!simple)
            {
                return false;
            }
        }

        return true;
    }
}
