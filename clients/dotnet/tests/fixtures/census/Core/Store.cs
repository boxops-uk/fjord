using System.Collections.Generic;

namespace Census.Core;

/// <summary>Somewhere to put shapes, with an indexer and a generic method.</summary>
public sealed class Store<T>
    where T : IShape
{
    private readonly List<T> _items = [];

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public void Add(T item) => _items.Add(item);

    /// <summary>The first of a set, or nothing when there are none.</summary>
    public static TShape? First<TShape>(IEnumerable<TShape> items)
        where TShape : class, IShape
    {
        foreach (var item in items)
        {
            return item;
        }

        return null;
    }
}
