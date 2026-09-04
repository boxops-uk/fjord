// An event is the member kind that exists to hold a delegate, so clause 21's types meet
// clause 15.8's member here: every event below is declared with a delegate type from
// DelegateDeclarations.cs, subscribed to with `+=`, unsubscribed from with `-=`, and raised
// through the `Invoke` that clause 21.6 says an invocation binds to. A field-like event is
// one declaration that produces four things — the event, an `add` accessor, a `remove`
// accessor, and a backing field that carries the event's own name.

namespace Surface.EnumsDelegates;

/// <summary>
/// 21.3 / 15.8 — events of a custom delegate type, in both the field-like and the
/// accessor-bearing form.
/// </summary>
public sealed class EdWatched
{
    /// <summary>
    /// 15.8.2 hazard — a field-like event. The compiler makes a private field also called
    /// <c>Changed</c> in this same type, so two declarations of different member kinds carry
    /// one name in one container; only a kind discriminator keeps them apart.
    /// </summary>
    public event EdChanged? Changed;

    /// <summary>15.8.3 — the field the hand-written accessors below maintain themselves.</summary>
    private EdChanged? _renamed;

    /// <summary>
    /// 15.8.3 — an event with `add` and `remove` accessors, so no field is synthesized. The
    /// accessors' `value` parameter is declared by neither of them.
    /// </summary>
    public event EdChanged Renamed
    {
        add => _renamed += value;
        remove => _renamed -= value;
    }

    /// <summary>15.8.2 — a static field-like event, of the parameterless delegate type.</summary>
    public static event EdSignal? Reset;

    /// <summary>21.6 — raising the field-like event, which reads the backing field.</summary>
    public void Change(string field) => Changed?.Invoke(this, field);

    /// <summary>21.6 — raising the accessor-bearing event, which reads the explicit field.</summary>
    public void Rename(string field) => _renamed?.Invoke(this, field);

    /// <summary>21.6 — raising the static event.</summary>
    public static void Restart() => Reset?.Invoke();

    /// <summary>
    /// 15.8 — subscription. `+=` on an event is not the `+` of clause 21.5: it is a call to
    /// the event's `add` accessor, which is a member no source line here names.
    /// </summary>
    public void Subscribe(EdChanged handler)
    {
        Changed += handler;
        Renamed += handler;
        Reset += EdSignalBoard.Log;
    }

    /// <summary>15.8 — unsubscription, through the `remove` accessor.</summary>
    public void Unsubscribe(EdChanged handler)
    {
        Changed -= handler;
        Renamed -= handler;
        Reset -= EdSignalBoard.Log;
    }

    /// <summary>
    /// 15.8 — subscription with a lambda, which is the case that cannot be unsubscribed
    /// because the source holds no reference to the instance it made.
    /// </summary>
    public void SubscribeInline() => Changed += static (source, field) => { };
}

/// <summary>
/// 15.8.1 / 21.2 — an event declared in an interface and implemented explicitly, so the
/// implementing member's name contains the interface's.
/// </summary>
public interface IEdObservable
{
    /// <summary>15.8.1 — an event as an interface member, which has no body.</summary>
    event EdChanged Touched;
}

/// <summary>15.8 — the explicit implementation of that event.</summary>
public sealed class EdObserved : IEdObservable
{
    private EdChanged? _touched;

    /// <summary>
    /// 15.8 hazard — an explicitly implemented event. The declared name is
    /// <c>IEdObservable.Touched</c>, which is not an identifier, and it must not merge with
    /// the interface member it implements.
    /// </summary>
    event EdChanged IEdObservable.Touched
    {
        add => _touched += value;
        remove => _touched -= value;
    }

    /// <summary>21.6 — the raise, through the field the accessors maintain.</summary>
    public void Touch(string field) => _touched?.Invoke(this, field);

    /// <summary>15.8 — subscription through the interface, which is the only way in.</summary>
    public static EdObserved Subscribed(EdChanged handler)
    {
        EdObserved observed = new EdObserved();
        ((IEdObservable)observed).Touched += handler;
        return observed;
    }
}
