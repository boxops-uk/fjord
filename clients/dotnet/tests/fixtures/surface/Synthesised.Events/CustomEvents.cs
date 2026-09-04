// M20, the other half of the declaration side — 15.8.3, an event with declared accessors.
//
// An `EventDeclarationSyntax` is a `BasePropertyDeclarationSyntax`, so unlike the field-like
// form it IS reachable directly and `GetDeclaredSymbol` answers with the event. It is
// dropped all the same. Its accessors are a second, independent silence: an
// `AccessorDeclarationSyntax` is not in the declaration walk's switch at all, so
// `MethodKind.EventAdd` and `MethodKind.EventRemove` have no definition row even though they
// have real syntax and a real span here.
namespace Surface.Synthesised.Events;

using System.Collections.Generic;

/// <summary>An event broker: readings in, subscribers out. Both events have written accessors.</summary>
public sealed class EvBroker
{
    private readonly List<EvReadingHandler> _routed = new();

    private static EvReadingHandler? _watchers;

    /// <summary>
    /// A custom event whose accessors are expression-bodied (15.8.3). `value` is the
    /// accessor's implicit parameter, which has no syntax of its own and whose containing
    /// method the walk never declared.
    /// </summary>
    public event EvReadingHandler Routed
    {
        add => _routed.Add(value);
        remove => _routed.Remove(value);
    }

    /// <summary>A static custom event (15.8.4), with block-bodied accessors that compound-assign a delegate field.</summary>
    public static event EvReadingHandler Watched
    {
        add { _watchers += value; }
        remove { _watchers -= value; }
    }

    /// <summary>How many handlers are currently routed.</summary>
    public int RoutedCount => _routed.Count;

    /// <summary>Routes a reading to every subscriber of both events.</summary>
    /// <param name="reading">The reading to route.</param>
    public void Route(int reading)
    {
        foreach (EvReadingHandler handler in _routed)
        {
            handler(reading);
        }

        _watchers?.Invoke(reading);
    }
}
