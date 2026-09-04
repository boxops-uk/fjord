using Census.Core;

namespace Census.App;

/// <summary>Uses the core types, so the index has references as well as declarations.</summary>
public sealed class Report
{
    private readonly Store<Rectangle> _store = new();

    public void Fill()
    {
        var wide = new Rectangle(2, 3);

        _store.Add(wide);
        _store.Add(new Rectangle(4, 5));
    }

    public double Total()
    {
        var total = Rectangle.Unit;

        for (var index = 0; index < _store.Count; index++)
        {
            total += _store[index].Area;
        }

        return total;
    }

    public static string Named(Entry entry) => entry.Key;

    public static double Across(Corner corner) => corner.X;
}
