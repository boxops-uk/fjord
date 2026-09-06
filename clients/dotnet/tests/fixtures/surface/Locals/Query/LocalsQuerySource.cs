using System;

namespace Surface.Locals.Query;

/// <summary>
/// The query pattern, declared in this project so that the range-variable fixture needs no
/// package reference and no <c>System.Linq</c>.
/// </summary>
/// <remarks>
/// <para>
/// ECMA-334 draft-v9 12.20 defines a query expression by translation onto method calls
/// found by ordinary member lookup — it names no type and no assembly. Two instance
/// methods are enough for <c>from</c>, <c>where</c>, <c>let</c> and <c>select</c>:
/// <see cref="Where"/> and <see cref="Select"/>.
/// </para>
/// <para>
/// <b>Declaring them here is what makes the M23 anti-join non-vacuous.</b> If the query
/// called <c>Enumerable.Select</c>, "the method a query calls has no reference row" would
/// be true and unremarkable, because a declaration in a referenced assembly has no
/// definition row either. These two are declared in the corpus, have definition rows, and
/// are called three times by <c>LocalsRangeVariables</c> — and no row anywhere records a
/// call. That is the assertion.
/// </para>
/// <para>
/// One arity only. A non-generic <c>LocalsQuerySource</c> beside this one would be a type
/// name at two arities, which is the identity collision that kills an indexing run.
/// </para>
/// </remarks>
/// <typeparam name="TItem">What the source holds.</typeparam>
public sealed class LocalsQuerySource<TItem>
{
    private readonly TItem[] _items;

    /// <summary>Wraps an array as a query source.</summary>
    /// <param name="items">The items, taken as given.</param>
    public LocalsQuerySource(TItem[] items) => _items = items;

    /// <summary>The items, in the order they were given.</summary>
    public TItem[] Items => _items;

    /// <summary>
    /// The <c>select</c> half of the pattern. Called by every query in
    /// <c>LocalsRangeVariables.cs</c>, and referenced by none of them.
    /// </summary>
    /// <typeparam name="TResult">What the projection produces.</typeparam>
    /// <param name="project">The projection.</param>
    public LocalsQuerySource<TResult> Select<TResult>(Func<TItem, TResult> project)
    {
        var built = new TResult[_items.Length];

        for (var index = 0; index < _items.Length; index++)
        {
            built[index] = project(_items[index]);
        }

        return new LocalsQuerySource<TResult>(built);
    }

    /// <summary>The <c>where</c> half of the pattern.</summary>
    /// <param name="keep">Which items survive.</param>
    public LocalsQuerySource<TItem> Where(Func<TItem, bool> keep)
    {
        var kept = new System.Collections.Generic.List<TItem>(_items.Length);

        foreach (var item in _items)
        {
            if (keep(item))
            {
                kept.Add(item);
            }
        }

        return new LocalsQuerySource<TItem>([.. kept]);
    }
}
