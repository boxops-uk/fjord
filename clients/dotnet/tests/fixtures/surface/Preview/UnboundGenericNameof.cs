using System;
using System.Collections.Generic;

namespace Surface.Preview;

/// <summary>
/// C# 14 — <c>nameof</c> supports unbound generic types. <c>nameof(List&lt;&gt;)</c> is a
/// reference to a generic type with no type arguments supplied — a construct that previously
/// existed only inside <c>typeof</c>. The constant it produces drops the arity, so
/// <c>nameof(List&lt;&gt;)</c> and <c>nameof(List&lt;int&gt;)</c> are the same string and
/// name different things.
/// </summary>
public static class UnboundGenericNameof
{
    /// <summary>C# 14 — <c>nameof</c> of an unbound framework generic.</summary>
    public const string ListName = nameof(List<>);

    /// <summary>C# 14 — <c>nameof</c> of an unbound generic of arity two.</summary>
    public const string MapName = nameof(Dictionary<,>);

    /// <summary>C# 14 — <c>nameof</c> of an unbound generic declared in this corpus.</summary>
    public const string PairName = nameof(UnboundPair<,>);

    /// <summary>C# 14 — <c>nameof</c> of a nested unbound generic.</summary>
    public const string NestedName = nameof(UnboundPair<,>.Cell<>);

    /// <summary>The bound spelling, which produces the same string.</summary>
    public const string BoundListName = nameof(List<int>);

    /// <summary>C# 14 — the same unbound type, in an attribute argument.</summary>
    [Obsolete("superseded by " + nameof(Dictionary<,>))]
    public static string Retired() => ListName + MapName + PairName + NestedName + BoundListName;

    /// <summary>The <c>typeof</c> spelling of an unbound generic, which C# has always had.</summary>
    public static Type UnboundType() => typeof(List<>);
}

/// <summary>A generic type of arity two, so the corpus owns two of the names above.</summary>
/// <typeparam name="TFirst">The first thing.</typeparam>
/// <typeparam name="TSecond">The second thing.</typeparam>
public sealed class UnboundPair<TFirst, TSecond>
{
    /// <summary>The first thing.</summary>
    public TFirst? First { get; init; }

    /// <summary>The second thing.</summary>
    public TSecond? Second { get; init; }

    /// <summary>A nested generic, named unbound above through its containing type.</summary>
    /// <typeparam name="TSlot">What the slot holds.</typeparam>
    public sealed class Cell<TSlot>
    {
        /// <summary>What is in the slot.</summary>
        public TSlot? Held { get; init; }
    }
}
