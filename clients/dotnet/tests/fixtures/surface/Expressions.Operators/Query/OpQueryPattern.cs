using System;
using System.Collections.Generic;

namespace Surface.Expressions.Operators.Query;

/// <summary>
/// The query-expression pattern of 12.22.4, declared for <see cref="OpQuerySource{T}"/>.
/// </summary>
/// <remarks>
/// <para>Clause 12.22.4 says a type supports query expressions by providing methods of
/// these names and shapes; the pattern is structural, not an interface. Every method here
/// is what one clause of a query expression is translated into, and the translation
/// (12.22.3.1) is why a query expression is a set of method references whose names the
/// source never writes: <c>where</c> is <see cref="Where"/>, <c>select</c> is
/// <see cref="Select"/>, a second <c>from</c> is <see cref="SelectMany"/>, and so on.</para>
/// <para>These are extension methods, so each reference is also an extension method
/// invocation whose receiver is the query's source expression.</para>
/// </remarks>
public static class OpQueryPattern
{
    /// <summary>12.22.3.6 — what a <c>select</c> clause is translated into.</summary>
    public static OpQuerySource<TResult> Select<TSource, TResult>(
        this OpQuerySource<TSource> source,
        Func<TSource, TResult> selector)
    {
        var projected = new List<TResult>();

        foreach (TSource item in source)
        {
            projected.Add(selector(item));
        }

        return new OpQuerySource<TResult>(projected);
    }

    /// <summary>12.22.3.5 — what a <c>where</c> clause is translated into.</summary>
    public static OpQuerySource<TSource> Where<TSource>(
        this OpQuerySource<TSource> source,
        Func<TSource, bool> predicate)
    {
        var kept = new List<TSource>();

        foreach (TSource item in source)
        {
            if (predicate(item))
            {
                kept.Add(item);
            }
        }

        return new OpQuerySource<TSource>(kept);
    }

    /// <summary>
    /// 12.22.3.5 — what a second <c>from</c> clause followed directly by a <c>select</c>
    /// is translated into.
    /// </summary>
    public static OpQuerySource<TResult> SelectMany<TSource, TResult>(
        this OpQuerySource<TSource> source,
        Func<TSource, OpQuerySource<TResult>> selector)
    {
        var flattened = new List<TResult>();

        foreach (TSource item in source)
        {
            foreach (TResult inner in selector(item))
            {
                flattened.Add(inner);
            }
        }

        return new OpQuerySource<TResult>(flattened);
    }

    /// <summary>
    /// 12.22.3.5 / 12.22.3.8 — what a second <c>from</c> clause with more clauses after it
    /// is translated into. The result selector is where the transparent identifier that
    /// carries both range variables is constructed.
    /// </summary>
    public static OpQuerySource<TResult> SelectMany<TSource, TCollection, TResult>(
        this OpQuerySource<TSource> source,
        Func<TSource, OpQuerySource<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector)
    {
        var flattened = new List<TResult>();

        foreach (TSource item in source)
        {
            foreach (TCollection inner in collectionSelector(item))
            {
                flattened.Add(resultSelector(item, inner));
            }
        }

        return new OpQuerySource<TResult>(flattened);
    }

    /// <summary>12.22.3.5 — the first ordering key of an <c>orderby</c> clause.</summary>
    public static OpOrderedQuerySource<TSource> OrderBy<TSource, TKey>(
        this OpQuerySource<TSource> source,
        Func<TSource, TKey> keySelector) =>
        new(Collect(source), [KeyComparison(keySelector, ascending: true)]);

    /// <summary>12.22.3.5 — the first ordering key with <c>descending</c>.</summary>
    public static OpOrderedQuerySource<TSource> OrderByDescending<TSource, TKey>(
        this OpQuerySource<TSource> source,
        Func<TSource, TKey> keySelector) =>
        new(Collect(source), [KeyComparison(keySelector, ascending: false)]);

    /// <summary>12.22.3.5 — a subsequent ordering key.</summary>
    public static OpOrderedQuerySource<TSource> ThenBy<TSource, TKey>(
        this OpOrderedQuerySource<TSource> source,
        Func<TSource, TKey> keySelector) =>
        source.Append(KeyComparison(keySelector, ascending: true));

    /// <summary>12.22.3.5 — a subsequent ordering key with <c>descending</c>.</summary>
    public static OpOrderedQuerySource<TSource> ThenByDescending<TSource, TKey>(
        this OpOrderedQuerySource<TSource> source,
        Func<TSource, TKey> keySelector) =>
        source.Append(KeyComparison(keySelector, ascending: false));

    /// <summary>12.22.3.7 — what <c>group x by k</c> is translated into.</summary>
    public static OpQuerySource<OpQueryGrouping<TKey, TSource>> GroupBy<TSource, TKey>(
        this OpQuerySource<TSource> source,
        Func<TSource, TKey> keySelector) =>
        source.GroupBy(keySelector, item => item);

    /// <summary>12.22.3.7 — what <c>group e by k</c> with a projection is translated into.</summary>
    public static OpQuerySource<OpQueryGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
        this OpQuerySource<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector)
    {
        var order = new List<TKey>();
        var buckets = new Dictionary<TKey, List<TElement>>();

        foreach (TSource item in source)
        {
            TKey key = keySelector(item);

            if (!buckets.TryGetValue(key, out List<TElement>? bucket))
            {
                bucket = [];
                buckets.Add(key, bucket);
                order.Add(key);
            }

            bucket.Add(elementSelector(item));
        }

        var groups = new List<OpQueryGrouping<TKey, TElement>>();

        foreach (TKey key in order)
        {
            groups.Add(new OpQueryGrouping<TKey, TElement>(key, buckets[key]));
        }

        return new OpQuerySource<OpQueryGrouping<TKey, TElement>>(groups);
    }

    /// <summary>12.22.3.5 — what a <c>join ... on ... equals ...</c> clause is translated into.</summary>
    public static OpQuerySource<TResult> Join<TOuter, TInner, TKey, TResult>(
        this OpQuerySource<TOuter> outer,
        OpQuerySource<TInner> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, TInner, TResult> resultSelector)
    {
        var joined = new List<TResult>();
        var comparer = EqualityComparer<TKey>.Default;

        foreach (TOuter left in outer)
        {
            foreach (TInner right in inner)
            {
                if (comparer.Equals(outerKeySelector(left), innerKeySelector(right)))
                {
                    joined.Add(resultSelector(left, right));
                }
            }
        }

        return new OpQuerySource<TResult>(joined);
    }

    /// <summary>
    /// 12.22.3.5 — what a <c>join ... into ...</c> clause is translated into, which hands
    /// the result selector a whole sub-sequence rather than one element.
    /// </summary>
    public static OpQuerySource<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(
        this OpQuerySource<TOuter> outer,
        OpQuerySource<TInner> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, OpQuerySource<TInner>, TResult> resultSelector)
    {
        var joined = new List<TResult>();
        var comparer = EqualityComparer<TKey>.Default;

        foreach (TOuter left in outer)
        {
            var matches = new List<TInner>();

            foreach (TInner right in inner)
            {
                if (comparer.Equals(outerKeySelector(left), innerKeySelector(right)))
                {
                    matches.Add(right);
                }
            }

            joined.Add(resultSelector(left, new OpQuerySource<TInner>(matches)));
        }

        return new OpQuerySource<TResult>(joined);
    }

    /// <summary>
    /// 12.22.3.3 — what an explicitly typed range variable is translated into. A
    /// <c>from int x in e</c> clause inserts this call before the rest of the query.
    /// </summary>
    public static OpQuerySource<TResult> Cast<TResult>(this OpQuerySource<object> source)
    {
        var converted = new List<TResult>();

        foreach (object item in source)
        {
            converted.Add((TResult)item);
        }

        return new OpQuerySource<TResult>(converted);
    }

    /// <summary>A convenience the queries use so an aggregate is available inside a group.</summary>
    public static int Total(this OpQuerySource<int> source)
    {
        int sum = 0;

        foreach (int item in source)
        {
            sum += item;
        }

        return sum;
    }

    private static List<TSource> Collect<TSource>(OpQuerySource<TSource> source)
    {
        var collected = new List<TSource>();

        foreach (TSource item in source)
        {
            collected.Add(item);
        }

        return collected;
    }

    private static Comparison<TSource> KeyComparison<TSource, TKey>(
        Func<TSource, TKey> keySelector,
        bool ascending)
    {
        var comparer = Comparer<TKey>.Default;

        return (left, right) =>
        {
            int verdict = comparer.Compare(keySelector(left), keySelector(right));

            return ascending ? verdict : -verdict;
        };
    }
}
