// M20 at its interface shapes — 19.4.5, an interface event; 19.6.2, an explicit
// implementation.
//
// The explicit form is the sharpest case in the project. `event EvReadingHandler
// IEvNotifier.Notified` is the ONE event declaration here whose `symbol.Name` is not an
// identifier but the dotted string `Surface.Synthesised.Events.IEvNotifier.Notified`, and a
// SCIP descriptor is `Name(symbol)` plus the term suffix `.` with `Name` running every name
// through `Escaped` — which backtick-quotes anything outside `[A-Za-z0-9_+$-]`. So this is
// also the only event in the project whose descriptor is a *quoted* one, and the only place
// the escaping path is reached by an event at all. Nothing is written for it on the entity
// side either way. Its only reachable use is through the interface (19.6.2: an explicit
// implementation cannot be named on the implementing type), so the xref at that use site
// targets the INTERFACE's event, and the implementing declaration ends up unreferenced as
// well as undefined.
namespace Surface.Synthesised.Events;

/// <summary>Something that reports readings to whoever subscribed (19.4.5).</summary>
public interface IEvNotifier
{
    /// <summary>Raised when a reading is worth reporting. Abstract: no accessor bodies.</summary>
    event EvReadingHandler? Notified;

    /// <summary>
    /// An interface event with a default implementation (C# 8): accessor bodies in an
    /// interface, which do nothing and can still be inherited.
    /// </summary>
    event EvReadingHandler? Defaulted
    {
        add { }
        remove { }
    }
}

/// <summary>Something whose TYPE reports, not its instances: a static abstract event (C# 11).</summary>
public interface IEvOriginated
{
    /// <summary>Raised by the implementing type itself, reachable only through a type parameter.</summary>
    static abstract event EvReadingHandler? Originated;
}

/// <summary>Implements <see cref="IEvNotifier.Notified"/> implicitly, field-like.</summary>
public sealed class EvImplicitNotifier : IEvNotifier
{
    /// <inheritdoc/>
    public event EvReadingHandler? Notified;

    /// <summary>Reports a reading to whoever subscribed to the implicit implementation.</summary>
    /// <param name="reading">The reading to report.</param>
    public void Notify(int reading) => Notified?.Invoke(reading);
}

/// <summary>
/// Implements <see cref="IEvNotifier.Notified"/> EXPLICITLY (19.6.2). An explicit
/// implementation must use accessor syntax (15.8.3) — the field-like spelling is CS0071 —
/// so this is also the one event here that is both explicitly implemented and accessor-formed.
/// </summary>
public sealed class EvExplicitNotifier : IEvNotifier
{
    private EvReadingHandler? _notified;

    /// <inheritdoc/>
    event EvReadingHandler? IEvNotifier.Notified
    {
        add => _notified += value;
        remove => _notified -= value;
    }

    /// <summary>Reports a reading through the explicitly implemented event's own delegate field.</summary>
    /// <param name="reading">The reading to report.</param>
    public void Notify(int reading) => _notified?.Invoke(reading);
}

/// <summary>Implements the static abstract event with a static field-like event of its own.</summary>
public sealed class EvOrigin : IEvOriginated
{
    /// <inheritdoc/>
    public static event EvReadingHandler? Originated;

    /// <summary>Raises the type's own event.</summary>
    /// <param name="reading">The reading to originate.</param>
    public static void Originate(int reading) => Originated?.Invoke(reading);
}
