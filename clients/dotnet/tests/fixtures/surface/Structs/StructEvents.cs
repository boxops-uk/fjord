// Clause 16.4.14 — Events. An event of a struct follows the rules for an event of a class,
// with one difference the standard spells out: a field-like event of a struct is stored in
// an instance field, so it is part of the value, and copying the struct copies the
// subscription list. A readonly struct may therefore not declare a field-like event at
// all.
//
// The hazard is as sharp as it gets: the compiler emits a private FIELD WITH THE EVENT'S
// OWN NAME. `Filled` below is an event and a field, in one type, with one name, and only
// one of the two appears in this file. If an identity is minted from kind-plus-name the
// two do not collide; if it is minted from name alone, in one type, they are one key with
// two values.

using System;

namespace Surface.Structs;

/// <summary>Clause 16.4.14 — a delegate type for the events below to be declared over.</summary>
/// <param name="reading">The reading that triggered the event.</param>
public delegate void StGaugeHandler(int reading);

/// <summary>
/// Clause 16.4.14 — the three event forms a struct may declare: a field-like instance
/// event, an event with declared accessors, and a static event.
/// </summary>
public struct StEventfulGauge
{
    /// <summary>The reading, which raising the events reports.</summary>
    public int Reading;

    /// <summary>Clause 16.3.1 — the field the accessor-declared event stores its delegate in.</summary>
    private StGaugeHandler? _emptiedStore;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StEventfulGauge(int reading)
    {
        Reading = reading;
        _emptiedStore = null;
    }

    /// <summary>
    /// Clause 16.4.14 hazard — a field-like event. The compiler adds a private field named
    /// <c>Filled</c> and two accessor methods, <c>add_Filled</c> and <c>remove_Filled</c>,
    /// none of which is written here.
    /// </summary>
    public event StGaugeHandler? Filled;

    /// <summary>
    /// Clause 16.4.14 — a second field-like event, of a framework delegate type, so the
    /// type has two unwritten fields.
    /// </summary>
    public event EventHandler<EventArgs>? Changed;

    /// <summary>
    /// Clause 16.4.14 — an event with declared accessors, which stores its delegate in the
    /// field above. Nothing is unwritten here except the event's own storage decision.
    /// </summary>
    public event StGaugeHandler? Emptied
    {
        add => _emptiedStore += value;
        remove => _emptiedStore -= value;
    }

    /// <summary>
    /// Clause 16.4.14 — a static field-like event. Its backing field is static, so unlike
    /// the instance ones it is not part of any value.
    /// </summary>
    public static event StGaugeHandler? AnyFilled;

    /// <summary>
    /// Clause 16.4.14 — raising the events. The `Filled?.Invoke` reads the unwritten field,
    /// not the event, which is why only code inside the type may do it.
    /// </summary>
    public void Fill(int reading)
    {
        Reading = reading;
        Filled?.Invoke(Reading);
        Changed?.Invoke(this, EventArgs.Empty);
        AnyFilled?.Invoke(Reading);
    }

    /// <summary>Clause 16.4.14 — raising the accessor-declared event through its own field.</summary>
    public void Empty()
    {
        Reading = 0;
        _emptiedStore?.Invoke(0);
    }
}

/// <summary>
/// Clause 16.4.14 — a readonly struct may declare a static field-like event and an event
/// with accessors, and may not declare an instance field-like one: the storage would have
/// to be a mutable instance field.
/// </summary>
public readonly struct StFrozenEvents
{
    private static StGaugeHandler? _readStore;

    /// <summary>Clause 16.4.14 — a static event on a readonly struct.</summary>
    public static event StGaugeHandler? AnyRead
    {
        add => _readStore += value;
        remove => _readStore -= value;
    }

    /// <summary>Clause 16.4.12 — the only raiser.</summary>
    public static void Report(int reading) => _readStore?.Invoke(reading);
}

/// <summary>
/// Clause 16.4.14 — the subscriptions, and the copy that loses them. Subscribing to a
/// field-like event of a struct writes a field of that struct value, so the copy made by
/// passing it to a method has a subscription list of its own.
/// </summary>
public static class StEventUse
{
    private static int _seen;

    /// <summary>Clause 16.4.14 — an add accessor and a remove accessor, on a local.</summary>
    public static int SubscribeAndRaise()
    {
        _seen = 0;
        StEventfulGauge gauge = new StEventfulGauge(0);
        StGaugeHandler handler = static reading => _seen += reading;
        gauge.Filled += handler;
        gauge.Fill(3);
        gauge.Filled -= handler;
        gauge.Fill(4);
        return _seen;
    }

    /// <summary>Clause 16.4.14 — the accessor-declared event, subscribed and raised.</summary>
    public static int SubscribeEmptied()
    {
        _seen = 0;
        StEventfulGauge gauge = new StEventfulGauge(5);
        gauge.Emptied += static reading => _seen -= 1;
        gauge.Empty();
        return _seen;
    }

    /// <summary>Clause 16.4.14 — the static event, which no instance owns.</summary>
    public static int SubscribeStatic()
    {
        _seen = 0;
        StEventfulGauge.AnyFilled += static reading => _seen += reading;
        new StEventfulGauge(0).Fill(7);
        return _seen;
    }

    /// <summary>
    /// Clause 16.4.14 / 16.4.2 — subscribing to a copy. The handler is added to the local,
    /// and the caller's value never had it, so raising through the caller's value calls
    /// nothing.
    /// </summary>
    public static int SubscribeOnCopy(StEventfulGauge gauge)
    {
        _seen = 0;
        gauge.Filled += static reading => _seen += reading;
        gauge.Fill(1);
        return _seen;
    }

    /// <summary>Clause 16.4.14 — the readonly struct's static event.</summary>
    public static int SubscribeFrozen()
    {
        _seen = 0;
        StFrozenEvents.AnyRead += static reading => _seen += reading;
        StFrozenEvents.Report(2);
        return _seen;
    }
}
