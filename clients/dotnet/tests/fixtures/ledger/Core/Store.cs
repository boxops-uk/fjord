using System.Collections.Generic;

namespace Ledger.Core;

/// <summary>A record with positional members.</summary>
public record Entry(string Key, int Weight);

/// <summary>Somewhere to put shapes, with an indexer over them.</summary>
public class Store<T>
    where T : IShape
{
    private readonly List<T> _items = [];

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public void Add(T item) => _items.Add(item);

    /// <summary>The heaviest of a set of entries, or nothing when there are none.</summary>
    public static Entry? Heaviest(IEnumerable<Entry> entries)
    {
        Entry? best = null;

        foreach (var entry in entries)
        {
            if (best is null || entry.Weight > best.Weight)
            {
                best = entry;
            }
        }

        return best;
    }
}
