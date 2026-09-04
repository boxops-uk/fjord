// Clauses 15.8.1 (events — general), 15.8.2 (field-like events) and 15.8.4 (static and instance
// events). An event is a member whose only public operations are subscribe and unsubscribe, so
// like a property it is a pair of methods behind a variable-shaped name — `add_Tick(EventHandler)`
// and `remove_Tick(EventHandler)` — and a field-like event is a *third* thing besides, because
// the compiler also generates the delegate field the accessors combine into.
//
// That is the hazard, and it is sharper than the property one:
//
//   * A field-like event `Tick` produces `add_Tick`, `remove_Tick` and a private field also
//     called `Tick`. The field and the event have the *same source name* in the same declaration
//     space, and only the declaring type may see the field.
//   * `Tick += handler` and `Tick?.Invoke(...)` are two references to one identifier that reach
//     two different members: the first is a call to `add_Tick`, the second a read of the field.
//     Outside the declaring type only the first spelling compiles at all.
//
// `MemReservedEvent` in `Members/MemReservedNames.cs` writes the name collision this implies.

using System;

namespace Surface.Classes.Members.Events;

/// <summary>15.8.1: the delegate types the events here are declared over. A delegate declaration
/// is a type and not an event, and an event may not be declared over anything else.</summary>
public delegate void MemMeasured(int weight);

/// <summary>15.8.1: a generic delegate, so an event may be declared over a constructed type.</summary>
/// <typeparam name="TPayload">What the notification carries.</typeparam>
public delegate void MemMeasuredOf<TPayload>(TPayload payload);

/// <summary>15.8.2: field-like events — an event declaration with no accessor list, which the
/// compiler completes with a delegate field and two synchronised accessors.</summary>
public class MemFieldLikeEvents
{
    /// <summary>15.8.2: the plain case, over a framework delegate type.</summary>
    public event EventHandler? Tick;

    /// <summary>15.8.2: over a delegate declared in this file.</summary>
    public event MemMeasured? Measured;

    /// <summary>15.8.2: over a constructed generic delegate.</summary>
    public event MemMeasuredOf<string>? Labelled;

    /// <summary>15.8.2: two events in one declaration, sharing a type and a modifier and
    /// nothing else. This is one syntax node and two members.</summary>
    public event EventHandler? Opened, Closed;

    /// <summary>15.8.2: a field-like event with an initializer, so the generated field starts
    /// non-null and `?.` is never needed for it.</summary>
    public event EventHandler Ready = (_, _) => { };

    /// <summary>15.8.1: raising every event. Each `Tick?.Invoke` is a read of the generated
    /// *field*, which is a reference no other type can write — outside this class the only legal
    /// reference to `Tick` is `+=` or `-=`.</summary>
    public void RaiseAll(int weight)
    {
        Tick?.Invoke(this, EventArgs.Empty);
        Measured?.Invoke(weight);
        Labelled?.Invoke(weight.ToString());
        Opened?.Invoke(this, EventArgs.Empty);
        Closed?.Invoke(this, EventArgs.Empty);
        Ready(this, EventArgs.Empty);
    }

    /// <summary>15.8.1: the subscribe and unsubscribe references, which are calls to the two
    /// generated accessors.</summary>
    public void SubscribeAll()
    {
        void OnTick(object? sender, EventArgs args) => RaiseAll(0);

        Tick += OnTick;
        Tick -= OnTick;
        Measured += weight => _ = weight;
        Labelled += label => _ = label;
        Opened += OnTick;
        Closed += OnTick;
        Ready += OnTick;
    }
}

/// <summary>15.8.4: static events beside instance ones. A static event's generated field is
/// static, and its accessors take no `this` — so a subscription outlives every instance, which
/// is the whole practical difference.</summary>
public class MemStaticEvents
{
    /// <summary>15.8.4: a static field-like event.</summary>
    public static event EventHandler? Global;

    /// <summary>15.8.4: an instance field-like event with the same delegate type, so the pair
    /// differs in nothing but the modifier.</summary>
    public event EventHandler? Local;

    /// <summary>15.8.4: a static event over a delegate declared in this file, raised from a
    /// static method.</summary>
    public static event MemMeasured? Totalled;

    /// <summary>15.8.4: raising the static ones with no instance in scope.</summary>
    public static void RaiseGlobal(int weight)
    {
        Global?.Invoke(null, EventArgs.Empty);
        Totalled?.Invoke(weight);
    }

    /// <summary>15.8.4: raising the instance one, and subscribing to both kinds — the static
    /// through the type name, the instance through `this`.</summary>
    public void RaiseLocal()
    {
        Global += Handle;
        Totalled += _ => { };
        Local += Handle;
        Local?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>15.8.1: the handler both subscriptions use.</summary>
    private static void Handle(object? sender, EventArgs args) => _ = sender;
}

/// <summary>15.8.1: a subscriber outside the declaring type, which is where the asymmetry of an
/// event shows: `+=` and `-=` compile, and every other reference does not.</summary>
public static class MemEventSubscriber
{
    /// <summary>15.8.1: the two references an event permits from outside.</summary>
    public static void Attach(MemFieldLikeEvents source)
    {
        source.Tick += Handle;
        source.Tick -= Handle;
        source.Measured += weight => _ = weight;
        MemStaticEvents.Global += Handle;
    }

    /// <summary>15.8.1: the handler.</summary>
    private static void Handle(object? sender, EventArgs args) => _ = sender;
}
