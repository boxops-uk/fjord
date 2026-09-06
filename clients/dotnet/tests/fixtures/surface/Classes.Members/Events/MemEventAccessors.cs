// Clauses 15.8.3 (event accessors) and 15.8.5 (virtual, sealed, override and abstract event
// accessors). An event with an accessor list has *no* generated field: `add` and `remove` are
// written out, so the storage is whatever the author chose, and the declaring type reads that
// storage by its own name rather than by the event's.
//
// The pair of clauses is where an event stops resembling a field entirely:
//
//   * A custom event's `add` and `remove` are both required — one without the other is CS0065 —
//     so this is the only member in the language whose accessor list has no optional half.
//   * A custom event may be `abstract`, in which case both accessors are bodiless; an override
//     of a *field-like* event may be a field-like event again, in which case the override's
//     accessors are generated. `MemEventLeaf.AbstractTick` is exactly that: an override whose
//     two emitted accessors have no source at all, replacing two that also had none.
//
// The hazard: `Tick` is declared three times down this chain, each time with two accessors, and
// once with a generated field beside it and twice without.

using System;
using System.Collections.Generic;

namespace Surface.Classes.Members.Events;

/// <summary>15.8.3: custom event accessors over storage the author names.</summary>
public class MemCustomEvents
{
    private readonly List<EventHandler> _handlers = [];
    private EventHandler? _combined;
    private static EventHandler? _static;

    /// <summary>15.8.3: block-bodied accessors over a list, so `add` and `remove` do something
    /// a generated pair would not.</summary>
    public event EventHandler Listed
    {
        add
        {
            _handlers.Add(value);
        }

        remove
        {
            _handlers.Remove(value);
        }
    }

    /// <summary>15.8.3: expression-bodied accessors over a delegate field, which is what the
    /// generated pair does — written out.</summary>
    public event EventHandler Combined
    {
        add => _combined += value;
        remove => _combined -= value;
    }

    /// <summary>15.8.3 with 15.8.4: a static custom event, whose accessors take no `this`.</summary>
    public static event EventHandler Shared
    {
        add => _static += value;
        remove => _static -= value;
    }

    /// <summary>15.8.3: an accessor that refuses, so the member exists and no subscription can
    /// succeed.</summary>
    public event EventHandler Refused
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }

    /// <summary>15.8.3: `value` inside an accessor is the implicit parameter, and it is the one
    /// name in these six bodies that no declaration in the file introduces.</summary>
    public int Count => _handlers.Count;

    /// <summary>15.8.3: every accessor above, reached. A custom event has no field to read, so
    /// raising it goes through the author's own storage — and `Refused` shows why that is not a
    /// convention but a rule: a bare reference to a custom event is CS0079 ("can only appear on
    /// the left hand side of += or -=") *even inside the declaring type*, where a field-like
    /// event would have been readable. So the two event forms differ in what references to them
    /// are legal, not only in what the compiler generates.</summary>
    public string UseAll()
    {
        void Handle(object? sender, EventArgs args) => _ = sender;

        Listed += Handle;
        Combined += Handle;
        Shared += Handle;
        Listed -= Handle;
        Combined -= Handle;
        Shared -= Handle;
        _combined?.Invoke(this, EventArgs.Empty);
        _static?.Invoke(this, EventArgs.Empty);

        try
        {
            Refused += Handle;
        }
        catch (NotSupportedException)
        {
            Refused -= Handle;
        }

        return $"{Count}";
    }
}

/// <summary>15.8.5: abstract and virtual events. An abstract event's accessors are bodiless; a
/// virtual one's may be generated or written.</summary>
public abstract class MemEventRoot
{
    /// <summary>15.8.5: an abstract event — two abstract accessors, no field, no bodies.</summary>
    public abstract event EventHandler AbstractTick;

    /// <summary>15.8.5: a virtual field-like event, so the virtual accessors are the generated
    /// ones.</summary>
    public virtual event EventHandler? VirtualTick;

    /// <summary>15.8.5: a virtual custom event, so the virtual accessors are written.</summary>
    public virtual event EventHandler CustomTick
    {
        add => Written += value;
        remove => Written -= value;
    }

    /// <summary>15.8.5: the storage the custom accessors use, which a derived override may not
    /// see if it is private — this one is protected so the override below can chain.</summary>
    protected EventHandler? Written;

    /// <summary>15.8.5: raises what this level can raise.</summary>
    public void RaiseRoot()
    {
        VirtualTick?.Invoke(this, EventArgs.Empty);
        Written?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>15.8.5: the middle of the chain. The override of the abstract event is *field-like*,
/// so its two accessors are generated where the base's two were abstract.</summary>
public class MemEventMiddle : MemEventRoot
{
    /// <summary>15.8.5: an override that is a field-like event — a declaration with no accessor
    /// list overriding one with two bodiless accessors.</summary>
    public override event EventHandler? AbstractTick;

    /// <summary>15.8.5: an override that is a custom event, replacing generated accessors with
    /// written ones and calling neither base accessor, because a field-like base event has no
    /// accessors a `base.` reference may name.</summary>
    public override event EventHandler? VirtualTick
    {
        add => Written += value;
        remove => Written -= value;
    }

    /// <summary>15.8.5: a new virtual event introduced part-way down.</summary>
    public virtual event EventHandler? MiddleTick;

    /// <summary>15.8.5: raises the overrides.</summary>
    public void RaiseMiddle()
    {
        AbstractTick?.Invoke(this, EventArgs.Empty);
        MiddleTick?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>15.8.5: sealed overrides, which stop the chain.</summary>
public sealed class MemEventLeaf : MemEventMiddle
{
    /// <summary>15.8.5: `sealed override` on a custom event, whose accessors chain to the
    /// base's through `base.CustomTick`, the one event reference form that names an accessor
    /// rather than a field.</summary>
    public sealed override event EventHandler CustomTick
    {
        add => base.CustomTick += value;
        remove => base.CustomTick -= value;
    }

    /// <summary>15.8.5: a sealed override of the event introduced one level up, as a field-like
    /// event again.</summary>
    public sealed override event EventHandler? MiddleTick;

    /// <summary>15.8.5: every event on the chain, subscribed through the static type that
    /// selects it.</summary>
    public string UseAll()
    {
        void Handle(object? sender, EventArgs args) => _ = sender;

        MemEventRoot root = this;
        root.AbstractTick += Handle;
        root.VirtualTick += Handle;
        root.CustomTick += Handle;
        MiddleTick += Handle;
        MiddleTick?.Invoke(this, EventArgs.Empty);
        RaiseRoot();
        RaiseMiddle();
        root.AbstractTick -= Handle;
        return $"{root is not null}";
    }
}
