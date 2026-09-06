using System;

namespace Surface.Locals.Functions;

/// <summary>
/// M12 — a local function's entity key is a display string, and three of the five
/// declarations below reach a key another one already holds.
/// </summary>
/// <remarks>
/// <para>
/// <c>CsharpEntities.MethodEntity</c> keys on
/// <c>{name, containingType, returnType, isStatic, access, disambiguator}</c>, and for a
/// local function <c>CsharpEntities.Disambiguator</c> is
/// <c>ContainingSymbol.ToDisplayString() + "." + ToDisplayString()</c> rather than a
/// documentation id — because Roslyn answers <c>M:…LocalsFunctionKeys.Pick</c> for a local
/// function, naming the containing <i>type</i>, which <c>containingType</c> already holds.
/// </para>
/// <para>
/// <b>The substitute works for the case the gate holds and fails for two it does not.</b>
/// <c>EntityKeyCensusTests.Two_local_functions_of_one_name_in_two_methods_are_two_keys</c>
/// asserts <see cref="First"/> and <see cref="Second"/> below reach two keys, and they do:
/// <c>ContainingSymbol</c> is the enclosing method and its display string names it. Move
/// the same two local functions inside lambdas and <c>ContainingSymbol.ToDisplayString()</c>
/// is the literal text <c>"lambda expression"</c> for both, so <see cref="Third"/> and
/// <see cref="Fourth"/> reach one key. Put them in two sibling blocks of one method and the
/// containing symbol is genuinely the same symbol, so <see cref="Fifth"/> reaches that one
/// key twice on its own.
/// </para>
/// <para>
/// <b>A gate that passes while its own claim is false is the finding here.</b> The census
/// test will keep passing after this file is indexed; what the corpus adds is the shape the
/// test does not hold, so that "two local functions of one name are two keys" becomes
/// false in the same run that asserts it.
/// </para>
/// <para>
/// <b>Silent, and the reason it is silent is worth stating.</b> A local function has no
/// global name — <c>ScipSymbols.HasGlobalName</c> rejects
/// <c>MethodKind.LocalFunction</c> — so <c>scip</c> is null in <c>Declare</c>,
/// <c>Markup</c> is never called, and neither <c>codemarkup.Definition</c> nor
/// <c>codemarkup.SymbolInfo</c> is written. Those are the only two predicates in play with
/// a value side, so nothing can conflict: <c>csharp.Method</c> merges, and
/// <c>csharp.DefinitionLocation</c> carries the span in its key and holds one row per
/// declaration. The observable defect is exactly that — one entity with several
/// <c>DefinitionLocation</c> rows, and a jump-to-definition that cannot choose between
/// them.
/// </para>
/// </remarks>
public sealed class LocalsFunctionKeys
{
    /// <summary>Key 1: <c>ContainingSymbol</c> is this method, and names it.</summary>
    public int First()
    {
        int Pick() => 1;

        return Pick();
    }

    /// <summary>Key 2: a different method, so a different display string.</summary>
    public int Second()
    {
        int Pick() => 2;

        return Pick();
    }

    /// <summary>
    /// Key 3, first declaration: inside a lambda, where <c>ContainingSymbol</c> renders as
    /// <c>lambda expression</c>.
    /// </summary>
    public Func<int> Third() => () =>
    {
        int Pick() => 3;

        return Pick();
    };

    /// <summary>
    /// Key 3, second declaration: a different lambda in a different method, and the same
    /// six key fields.
    /// </summary>
    public Func<int> Fourth() => () =>
    {
        int Pick() => 4;

        return Pick();
    };

    /// <summary>
    /// Key 4, twice: two sibling blocks of one method, where the containing symbol really
    /// is one symbol and the disambiguator has nothing left to separate on.
    /// </summary>
    /// <remarks>
    /// Legal C#: a local function is scoped to its enclosing block, so two sibling blocks
    /// may each declare <c>Pick</c>. Each call resolves to the one in its own block —
    /// <c>codemarkup.FileLocalXRef</c> gets both right, because it is answered from
    /// <c>symbol.Locations</c> per symbol — while the entity they share cannot say which.
    /// </remarks>
    public int Fifth()
    {
        var total = 0;

        {
            int Pick() => 5;

            total += Pick();
        }

        {
            int Pick() => 6;

            total += Pick();
        }

        return total;
    }

    /// <summary>
    /// The separating field, exhibited: a local function whose return type differs reaches
    /// a different key even inside a lambda.
    /// </summary>
    public Func<long> Sixth() => () =>
    {
        long Pick() => 7L;

        return Pick();
    };
}
