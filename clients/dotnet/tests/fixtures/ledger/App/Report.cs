using System.Collections.Generic;

using Ledger.Core;

namespace Ledger.App;

/// <summary>Reads the core types, so the index has edges that cross a project.</summary>
public sealed class Report
{
    private readonly Store<Rectangle> _store = new();

    public void Fill()
    {
        _store.Add(new Rectangle(2, 3));
        _store.Add(new Rectangle(4, 5) { Stroke = Stroke.Thick });
    }

    public double Total()
    {
        var total = 0d;

        for (var index = 0; index < _store.Count; index++)
        {
            total += _store[index].Area;
        }

        return total;
    }

    public static Entry? Best(IEnumerable<Entry> entries) => Store<Rectangle>.Heaviest(entries);
}
