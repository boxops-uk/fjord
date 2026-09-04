using System;

namespace Surface.Locals.Parameters;

/// <summary>
/// M11 — <c>csharp.Parameter</c>'s key holds everything about a parameter and nothing
/// about what declares it, so <c>int item</c> is one fact however many methods, lambdas
/// and local functions declare it.
/// </summary>
/// <remarks>
/// <para>
/// The key is <c>{name, type, refKind, isThis, isParams, isOptional}</c> —
/// <c>CsharpEntities.ParameterEntity</c> builds exactly those six fields and no container.
/// A method's parameter is at least reachable from its owner through
/// <c>csharp.MethodParameter {method, index, parameter}</c>, which <c>Edges</c> writes from
/// <c>Declare</c>; a <b>lambda's</b> parameter has no such edge, because the lambda is not
/// a declaration the walk visits and <c>ScipSymbols.HasGlobalName</c> rejects it anyway
/// (<c>MethodKind.AnonymousFunction</c>). So there is nothing anywhere in the index that
/// says which of the declarations below a given <c>csharp.Parameter</c> row came from.
/// </para>
/// <para>
/// <b>The visible wrong answer is on <c>csharp.EntityRef</c>.</b> Every use of any of the
/// three <c>int item</c>s writes <c>EntityRef {target = &lt;the one fused row&gt;}</c>, so
/// find-references on <see cref="Take"/>'s parameter answers spans inside
/// <see cref="Lambda"/> and <see cref="Held"/> as well. It merges rather than conflicting —
/// <c>csharp.Parameter</c> is all-key — so the run completes and the answer is a superset
/// with no marker on the parts that do not belong.
/// </para>
/// <para>
/// <b>Which fields do separate is the other half of the fixture.</b> The four methods after
/// the fused three change one key field each, so the file states the key by exhibiting it:
/// a different type, a different <c>refKind</c>, <c>isOptional</c>, <c>isParams</c>. Six
/// parameters named <c>item</c> are declared here and the predicate holds five rows.
/// </para>
/// </remarks>
public static class LocalsParameterFusion
{
    /// <summary>Declaration 1 of <c>int item</c>: an ordinary method parameter.</summary>
    public static int Take(int item) => item + 1;

    /// <summary>
    /// Declaration 2 of <c>int item</c>: a lambda parameter, written with its type so a
    /// reader can see it is the same declaration and not an inferred one.
    /// </summary>
    /// <remarks>
    /// The type is explicit for the reader; inference would produce the same
    /// <c>IParameterSymbol</c> and the same fact. What differs from <see cref="Take"/> is
    /// only that no <c>csharp.MethodParameter</c> row points at this one.
    /// </remarks>
    public static Func<int, int> Lambda() => (int item) => item + 2;

    /// <summary>
    /// Declaration 3 of <c>int item</c>: a local function's parameter, which <i>does</i>
    /// get a <c>MethodParameter</c> edge because <c>LocalFunctionStatementSyntax</c> is in
    /// the walk's declaration switch.
    /// </summary>
    /// <remarks>
    /// So the fused row has two incoming <c>MethodParameter</c> edges — from
    /// <see cref="Take"/> and from <c>Held</c> — and a consumer that tries to recover the
    /// owner by joining backwards gets two answers for one row and none at all for the
    /// lambda's.
    /// </remarks>
    public static int Held()
    {
        int Inner(int item) => item + 3;

        return Inner(4);
    }

    /// <summary>Separated by <c>type</c>: a different row for <c>long item</c>.</summary>
    public static long Wide(long item) => item + 4;

    /// <summary>Separated by <c>refKind</c>: <c>ref int item</c> is its own row.</summary>
    public static int Aliased(ref int item) => item + 5;

    /// <summary>Separated by <c>isOptional</c>: a default makes a different row.</summary>
    public static int Defaulted(int item = 6) => item;

    /// <summary>Separated by <c>isParams</c>, and by <c>type</c> as well.</summary>
    public static int Several(params int[] item) => item.Length;

    /// <summary>Uses every declaration above, so none of them is dead.</summary>
    public static long Use()
    {
        var slot = 7;

        return Take(1)
            + Lambda()(2)
            + Held()
            + Wide(3L)
            + Aliased(ref slot)
            + Defaulted()
            + Several(4, 5);
    }
}
