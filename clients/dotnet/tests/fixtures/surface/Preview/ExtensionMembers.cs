using System;
using System.Collections.Generic;
using System.Linq;

namespace Surface.Preview;

/// <summary>
/// C# 14 — Extension members: an <c>extension</c> block declares extension methods,
/// properties and static members against a receiver named once, in the block's header.
/// </summary>
/// <remarks>
/// This is the construct this indexer was found to drop <b>with every member inside it</b>.
/// Each member below is therefore two claims at once: that the member exists, and that it is
/// reachable — the receiver parameter is declared on the block rather than on the member, so a
/// walk that expects a method's first parameter to be its receiver finds nothing to attach.
/// Every member here has a call site in <see cref="ExtensionMemberUses"/>, so an index that
/// drops the declarations still has the references, and the two disagree.
/// </remarks>
public static class TextExtensions
{
    /// <summary>C# 14 — an extension block over <see cref="string"/>.</summary>
    extension(string text)
    {
        /// <summary>C# 14 — an extension *property*, which no earlier version could declare.</summary>
        public bool IsBlank => text.Trim().Length == 0;

        /// <summary>C# 14 — an extension method inside a block, which is the familiar shape.</summary>
        public string Twice() => text + text;

        /// <summary>C# 14 — an extension method with a parameter of its own.</summary>
        public string Repeat(int times) => string.Concat(Enumerable.Repeat(text, times));

        // An extension *indexer* is CS9282, "this member is not allowed in an extension
        // block" — so the corpus cannot hold one, and the quarantine projects are not the
        // reason. Recorded here because the row for extension members says "properties and
        // static members" and a reader will ask about the third kind.
    }

    /// <summary>C# 14 — a *static* extension block: the receiver is a type, not a value.</summary>
    extension(string)
    {
        /// <summary>C# 14 — a static extension property on <see cref="string"/>.</summary>
        public static string Marker => "surface";

        /// <summary>C# 14 — a static extension method on <see cref="string"/>.</summary>
        public static string Of(char letter, int count) => new(letter, count);
    }
}

/// <summary>C# 14 — extension blocks over a generic receiver.</summary>
public static class SequenceExtensions
{
    /// <summary>C# 14 — a generic extension block, with a constraint on the block.</summary>
    /// <typeparam name="TItem">The element type.</typeparam>
    extension<TItem>(IEnumerable<TItem> items)
        where TItem : notnull
    {
        /// <summary>C# 14 — an extension property on a constructed generic receiver.</summary>
        public int DoubledCount => items.Count() * 2;

        /// <summary>C# 14 — an extension method whose own type parameter is not the block's.</summary>
        /// <typeparam name="TKey">What to key by.</typeparam>
        public Dictionary<TKey, TItem> ById<TKey>(Func<TItem, TKey> key)
            where TKey : notnull
            => items.ToDictionary(key, item => item);
    }

    /// <summary>C# 14 — a second block in the same type, over a different receiver.</summary>
    extension(IReadOnlyList<int> numbers)
    {
        /// <summary>The sum, as an extension property over a narrower receiver.</summary>
        public int Total => numbers.Sum();
    }
}

/// <summary>
/// Calls every extension member declared above. If the block's members are dropped from the
/// index, these call sites are references with no declaration to point at — which is exactly
/// the disagreement the corpus is built to expose.
/// </summary>
public static class ExtensionMemberUses
{
    /// <summary>Reads the four instance members of the <see cref="string"/> block.</summary>
    public static string Instance()
    {
        const string text = "ab";

        return $"{text.IsBlank}{text.Twice()}{text.Repeat(3)}";
    }

    /// <summary>Reads the two static extension members.</summary>
    public static string Static() => string.Marker + string.Of('x', 2);

    /// <summary>Reads the generic block's members.</summary>
    public static int Generic()
    {
        IEnumerable<string> items = ["a", "bb"];

        return items.DoubledCount + items.ById(item => item.Length).Count;
    }

    /// <summary>Reads the narrower block's member.</summary>
    public static int Narrow()
    {
        IReadOnlyList<int> numbers = [1, 2, 3];

        return numbers.Total;
    }
}
