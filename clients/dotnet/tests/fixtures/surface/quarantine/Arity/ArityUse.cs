using System;

namespace Surface.Quarantine.Arity;

/// <summary>M1 — the arity-0 attribute class. Clause 22.2 and 22.3.</summary>
public sealed class ArityMarkAttribute : Attribute
{
}

/// <summary>
/// M1 — the arity-1 attribute class (C# 11, generic attributes).
/// </summary>
/// <remarks>
/// An attribute application references the <i>constructor</i>, never the attribute class,
/// and a constructor's descriptor is the containing type's path plus
/// <c>`.ctor`()</c> — so <c>[ArityMark]</c> and <c>[ArityMark&lt;string&gt;]</c> both
/// spell <c>ArityMarkAttribute#`.ctor`().</c> and two applied attributes become one
/// reference target.
/// </remarks>
/// <typeparam name="T">The attribute's type argument, absent from every descriptor.</typeparam>
public sealed class ArityMarkAttribute<T> : Attribute
{
}

/// <summary>
/// M1 — the reference side: eight uses that fan in onto the arity-merged descriptors.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 12.8.18 (<c>typeof</c>), 12.8.23 (<c>nameof</c>), 12.5.1 (member access) and
/// 21.2 (attribute specification). The two unbound spellings are the trap for a fix that
/// counts written type arguments instead of asking the symbol: <c>typeof(ArityStore&lt;&gt;)</c>
/// writes no type argument at all, yet Roslyn reports <c>Arity</c> 1 for it, and
/// <c>typeof(ArityStore&lt;,&gt;)</c> reports 2. Arity must come from the symbol.
/// </para>
/// <para>
/// This type is also where the <i>counting</i> assertion lives: seeked on the arity-1
/// <c>ArityStore</c> symbol, the cross-reference relation should hold exactly the
/// <c>new ArityStore&lt;int&gt;()</c> use and the <c>typeof(ArityStore&lt;&gt;)</c> use,
/// and none of the arity-0 or arity-2 ones.
/// </para>
/// </remarks>
[ArityMark]
[ArityMark<string>]
public class ArityUse
{
    /// <summary>The unbound arity-1 spelling. Clause 12.8.18.</summary>
    public Type Open = typeof(ArityStore<>);

    /// <summary>The unbound arity-2 spelling. Clause 12.8.18.</summary>
    public Type Two = typeof(ArityStore<,>);

    /// <summary>The arity-0 spelling, which writes no type argument because it has none.</summary>
    public Type Zero = typeof(ArityStore);

    /// <summary>
    /// The unbound arity-1 spelling in a <c>nameof</c> (C# 14). Clause 12.8.23.
    /// </summary>
    /// <remarks>
    /// <c>nameof</c> answers <c>"ArityStore"</c> for every arity, which is the language
    /// agreeing with the descriptor — and the reason the merge reads as plausible.
    /// </remarks>
    public string Named = nameof(ArityStore<>);

    /// <summary>One call to each arity's <c>Put</c>, all three onto one descriptor.</summary>
    public void Go()
    {
        new ArityStore().Put(1);
        new ArityStore<int>().Put(2);
        new ArityStore<int, long>().Put(3);
    }

    /// <summary>Both nested spellings, whose descriptors agree in both segments.</summary>
    public int Nested()
    {
        var one = new ArityOuter<int>.ArityInner<string>();
        var two = new ArityOuter.ArityInner<string, int>();
        return one.Depth + two.Depth;
    }
}
