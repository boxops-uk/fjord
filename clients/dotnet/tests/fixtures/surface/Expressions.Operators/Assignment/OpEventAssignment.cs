using System;

namespace Surface.Expressions.Operators.Assignment;

/// <summary>The delegate the events below are declared over.</summary>
/// <param name="reading">The reading that was taken.</param>
public delegate void OpReadingTaken(double reading);

/// <summary>
/// A source with one of each event shape, so that the event assignment of 12.23.6 has all
/// of its cases: a field-like event, an event with declared accessors, a static event and
/// a virtual event an override replaces.
/// </summary>
public class OpEventSource
{
    private OpReadingTaken? _guarded;

    /// <summary>A field-like event, whose accessors the compiler supplies.</summary>
    public event OpReadingTaken? Taken;

    /// <summary>
    /// An event with declared accessors, so <c>+=</c> on it is a reference to a method
    /// written out in this file rather than to a generated one.
    /// </summary>
    public event OpReadingTaken? Guarded
    {
        add
        {
            // 12.23.5 — inside the accessor, `+=` on the backing field is an ordinary
            // compound assignment on a delegate variable, not an event assignment.
            _guarded += value;
        }

        remove => _guarded -= value;
    }

    /// <summary>A static event, so the left operand of an event assignment can be a type.</summary>
    public static event Action? Reset;

    /// <summary>A virtual event, for an override to replace.</summary>
    public virtual event OpReadingTaken? Calibrated;

    /// <summary>Raises everything, so no event is write-only.</summary>
    public void Raise(double reading)
    {
        Taken?.Invoke(reading);
        _guarded?.Invoke(reading);
        Calibrated?.Invoke(reading);
        Reset?.Invoke();
    }

    /// <summary>
    /// 12.23.6 — event assignment from inside the declaring type, where the simple name of
    /// a field-like event is in scope. The same three characters as
    /// <see cref="OpEventAssignment.FromOutside"/> writes, at a use site with a different
    /// referent available to it.
    /// </summary>
    public void FromInside(OpReadingTaken handler)
    {
        Taken += handler;
        Taken -= handler;
        Guarded += handler;
        Reset += () => { };
    }
}

/// <summary>A derived source that overrides the virtual event with declared accessors.</summary>
public sealed class OpDerivedEventSource : OpEventSource
{
    private OpReadingTaken? _calibrated;

    /// <summary>An override of a field-like event by one with declared accessors.</summary>
    public override event OpReadingTaken? Calibrated
    {
        add => _calibrated += value;
        remove => _calibrated -= value;
    }

    /// <summary>
    /// 12.23.6 — event assignment through <c>base</c>, which names the base type's
    /// accessor rather than the override's.
    /// </summary>
    public void SubscribeToBase(OpReadingTaken handler) => base.Calibrated += handler;

    /// <summary>Raises the overridden event.</summary>
    public void RaiseCalibrated(double reading) => _calibrated?.Invoke(reading);
}

/// <summary>
/// The event assignment of 12.23.6: <c>+=</c> and <c>-=</c> whose left operand is an event
/// access are the only compound assignments that are not rewritten into a binary operator
/// and a store. They are calls to the event's <c>add</c> and <c>remove</c> accessors, and
/// the source names neither.
/// </summary>
public static class OpEventAssignment
{
    /// <summary>
    /// 12.23.6 — subscription and unsubscription from outside the declaring type, where
    /// only the accessors are accessible.
    /// </summary>
    public static void FromOutside(OpEventSource source, OpReadingTaken handler)
    {
        source.Taken += handler;
        source.Taken -= handler;
    }

    /// <summary>12.23.6 — the same on an event whose accessors are written out.</summary>
    public static void ToDeclaredAccessors(OpEventSource source, OpReadingTaken handler)
    {
        source.Guarded += handler;
        source.Guarded -= handler;
    }

    /// <summary>
    /// 12.23.6 — event assignment on a static event, whose left operand is a type name.
    /// </summary>
    public static void ToAStaticEvent()
    {
        OpEventSource.Reset += Clear;
        OpEventSource.Reset -= Clear;
    }

    /// <summary>
    /// 12.23.6 — event assignment whose right operand is a lambda (12.21.1) and an
    /// anonymous method, so the handler is a declaration inside the assignment.
    /// </summary>
    public static void WithAnonymousHandlers(OpEventSource source)
    {
        source.Taken += reading => Record(reading);
        source.Taken += delegate(double reading)
        {
            Record(reading * 2);
        };
    }

    /// <summary>
    /// 12.23.6 — event assignment on a virtual event through a derived reference, which
    /// reaches the override's accessor.
    /// </summary>
    public static void ToAnOverride(OpDerivedEventSource source, OpReadingTaken handler) =>
        source.Calibrated += handler;

    /// <summary>
    /// 12.23.6 — a method group as the right operand, which is a reference to a method by
    /// name in a position where a delegate value is required.
    /// </summary>
    public static void WithAMethodGroup(OpEventSource source) => source.Taken += Record;

    private static void Record(double reading) => _ = reading;

    private static void Clear() => _ = 0;
}
