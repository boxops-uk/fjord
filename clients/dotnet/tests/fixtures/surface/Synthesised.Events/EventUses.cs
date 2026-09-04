// M20's reference side — 12.23.6, event assignment.
//
// Every event declared in this project is used here, from OUTSIDE its declaring type, where
// the only two operators the language admits are `+=` and `-=`. `Reference` writes a
// `codemarkup.FileXRef` and a `codemarkup.SymbolXRef` for each of these, with the target
// spelled the way `ScipSymbols.Of` spells an event — and `Declare` wrote no
// `codemarkup.Definition` at that symbol, so each one is an xref whose target joins to
// nothing. That join is the gate: one query for a `SymbolXRef.target` with no `Definition`,
// and this file is the reason it returns rows.
namespace Surface.Synthesised.Events;

/// <summary>Subscribes to, and unsubscribes from, every event the project declares.</summary>
public static class EvUses
{
    /// <summary>The readings this handler was told about, newest last.</summary>
    private static readonly System.Collections.Generic.List<int> Seen = new();

    /// <summary>The handler every subscription in this file uses.</summary>
    /// <param name="reading">The reading that was reported.</param>
    public static void Record(int reading) => Seen.Add(reading);

    /// <summary>The label every generic subscription in this file uses.</summary>
    /// <param name="payload">The payload that arrived.</param>
    public static void Label(string payload) => Seen.Add(payload.Length);

    /// <summary>
    /// Wires the four field-like shapes of <see cref="EvGauge"/>, including
    /// <see cref="EvGauge.Read"/> — the only reference to an event in this project that is
    /// neither `+=`, `-=` nor an invocation, because a documentation comment's `cref` names
    /// a member without using it.
    /// </summary>
    /// <param name="gauge">The gauge to subscribe to.</param>
    public static string WireGauge(EvGauge gauge)
    {
        gauge.Read += Record;
        gauge.First += Record;
        gauge.Second += Record;
        gauge.Third += Record;
        gauge.Labelled += Label;
        EvGauge.Discarded += Record;

        gauge.Sweep(1);

        gauge.Read -= Record;
        gauge.Second -= Record;
        EvGauge.Discarded -= Record;

        // `nameof` of an event: a reference with no invocation and no assignment at all.
        return nameof(EvGauge.Read) + nameof(EvGauge.First) + nameof(EvGauge.Labelled);
    }

    /// <summary>Wires both accessor-formed events of <see cref="EvBroker"/>.</summary>
    /// <param name="broker">The broker to subscribe to.</param>
    public static int WireBroker(EvBroker broker)
    {
        broker.Routed += Record;
        EvBroker.Watched += Record;

        broker.Route(2);

        broker.Routed -= Record;
        EvBroker.Watched -= Record;

        return broker.RoutedCount;
    }

    /// <summary>
    /// Wires the interface event twice: once on a type that implements it implicitly, and
    /// once through the interface on the type that implements it explicitly. Both uses
    /// target <see cref="IEvNotifier.Notified"/>, because that is the member the expression
    /// binds to — the explicit implementation is named by no expression in any program.
    /// </summary>
    public static void WireNotifiers()
    {
        EvImplicitNotifier implicitly = new();
        implicitly.Notified += Record;
        implicitly.Notify(3);
        implicitly.Notified -= Record;

        IEvNotifier throughInterface = new EvExplicitNotifier();
        throughInterface.Notified += Record;
        throughInterface.Defaulted += Record;
        ((EvExplicitNotifier)throughInterface).Notify(4);
        throughInterface.Notified -= Record;
    }

    /// <summary>Wires a static abstract event through a type parameter (C# 11).</summary>
    /// <typeparam name="TOrigin">The type whose own event is subscribed to.</typeparam>
    public static void WireOrigin<TOrigin>()
        where TOrigin : IEvOriginated
    {
        TOrigin.Originated += Record;
        TOrigin.Originated -= Record;
    }

    /// <summary>Wires the struct's event and the overridden abstract one.</summary>
    public static int WireTheRest()
    {
        EvTicket ticket = default;
        ticket.Punched += Record;
        ticket.Punch(5);
        ticket.Punched -= Record;

        EvChannel channel = new EvUrgentChannel();
        channel.Escalated += Record;
        channel.Escalate(6);
        channel.Escalated -= Record;

        EvUses.WireOrigin<EvOrigin>();
        EvOrigin.Originated += Record;
        EvOrigin.Originate(7);
        EvOrigin.Originated -= Record;

        return Seen.Count;
    }
}
