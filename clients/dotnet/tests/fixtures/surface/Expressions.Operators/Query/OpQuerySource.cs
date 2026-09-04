using System;
using System.Collections;
using System.Collections.Generic;

namespace Surface.Expressions.Operators.Query;

/// <summary>
/// A sequence the query expressions of 12.22 run over. It exists so that the corpus's
/// query clauses bind to declarations inside the corpus rather than to
/// <c>System.Linq.Enumerable</c>: no file in this project imports <c>System.Linq</c>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public class OpQuerySource<T> : IEnumerable<T>
{
    private readonly List<T> _items;

    /// <summary>Constructs a sequence over the given elements.</summary>
    public OpQuerySource(params T[] items) => _items = new List<T>(items);

    internal OpQuerySource(List<T> items) => _items = items;

    /// <summary>How many elements the sequence holds.</summary>
    public int Count => _items.Count;

    /// <summary>The elements, for a derived type's own enumeration order.</summary>
    protected List<T> Elements => _items;

    /// <inheritdoc/>
    public virtual IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The result of an <c>orderby</c> clause: a sequence that remembers its comparison chain
/// so that a further ordering key can be appended to it.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Clause 12.22.3.5 translates <c>orderby a, b descending</c> into
/// <c>OrderBy(...).ThenByDescending(...)</c>, and only the first of those is available on
/// an unordered sequence. That is why <c>ThenBy</c> is declared on this type and not on
/// <see cref="OpQuerySource{T}"/>.
/// </remarks>
public sealed class OpOrderedQuerySource<T> : OpQuerySource<T>
{
    private readonly List<Comparison<T>> _comparisons;

    internal OpOrderedQuerySource(List<T> items, List<Comparison<T>> comparisons)
        : base(items) =>
        _comparisons = comparisons;

    internal OpOrderedQuerySource<T> Append(Comparison<T> comparison)
    {
        var extended = new List<Comparison<T>>(_comparisons) { comparison };

        return new OpOrderedQuerySource<T>(new List<T>(Elements), extended);
    }

    /// <inheritdoc/>
    public override IEnumerator<T> GetEnumerator()
    {
        var ordered = new List<T>(Elements);

        ordered.Sort(Compare);

        return ordered.GetEnumerator();
    }

    private int Compare(T left, T right)
    {
        foreach (Comparison<T> comparison in _comparisons)
        {
            int verdict = comparison(left, right);

            if (verdict != 0)
            {
                return verdict;
            }
        }

        return 0;
    }
}

/// <summary>
/// The result element of a <c>group</c> clause: a keyed sub-sequence.
/// </summary>
/// <typeparam name="TKey">The grouping key's type.</typeparam>
/// <typeparam name="TElement">The grouped element's type.</typeparam>
public sealed class OpQueryGrouping<TKey, TElement> : OpQuerySource<TElement>
{
    internal OpQueryGrouping(TKey key, List<TElement> items)
        : base(items) =>
        Key = key;

    /// <summary>The key every element in this group shares.</summary>
    public TKey Key { get; }
}
