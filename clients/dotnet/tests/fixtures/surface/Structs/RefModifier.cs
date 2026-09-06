// Clause 16.2.3 — Ref modifier. A struct declared with `ref` is a ref struct: an instance
// of it can be stored only where a ref safe context permits, it may declare `ref` fields,
// and it may not be boxed, be a type argument (before the post-standard
// `allows ref struct`), be a field of a class or of a non-ref struct, implement an
// interface (before C# 13), or be captured by a lambda or an iterator.
//
// The `ref` in a ref struct declaration is not part of any name, so a `ref struct` and a
// `struct` with the same members mint the same shape of identity. That is this file's
// hazard: the kind has to be recorded somewhere, or `StRefCursor` and a plain struct with
// one field are indistinguishable in the index.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.2.3 — a `ref struct` with a `ref` field. The field aliases storage the struct
/// does not own, which is the whole reason the type has to be a ref struct.
/// </summary>
public ref struct StRefCursor
{
    /// <summary>
    /// Clause 16.2.3 — a `ref` field. Its declaration carries a modifier that is part of
    /// the type, not of the name, so a field identity taken from the name alone cannot
    /// tell it from an <c>int</c> field called <c>Slot</c>.
    /// </summary>
    public ref int Slot;

    /// <summary>Clause 16.2.3 — a `ref readonly` field beside the writable one.</summary>
    public ref readonly int Origin;

    /// <summary>Clause 16.4.9 / 16.4.15.8 — a constructor that takes both by reference.</summary>
    public StRefCursor(ref int slot, ref readonly int origin)
    {
        Slot = ref slot;
        Origin = ref origin;
    }

    /// <summary>Clause 16.4.11 — a property over a `ref` field, which reads through it.</summary>
    public int Offset => Slot - Origin;

    /// <summary>Clause 16.4.12 — a method that writes through the `ref` field.</summary>
    public void Advance(int by) => Slot += by;
}

/// <summary>
/// Clause 16.2.3 — `readonly ref struct`, both modifiers on one declaration. A window over
/// a span that cannot reassign its own fields.
/// </summary>
public readonly ref struct StReadOnlyRefWindow
{
    private readonly ReadOnlySpan<int> _values;

    /// <summary>Clause 16.4.9 — the only constructor.</summary>
    public StReadOnlyRefWindow(ReadOnlySpan<int> values) => _values = values;

    /// <summary>Clause 16.4.11 — a property on a readonly ref struct.</summary>
    public int Length => _values.Length;

    /// <summary>Clause 16.4.12 — a method that returns a `ref readonly` to borrowed storage.</summary>
    public ref readonly int First() => ref _values[0];
}

/// <summary>
/// Clause 16.2.3, post-standard — a `ref struct` that implements an interface, which C# 13
/// permits and the standard's clause 16.2.3 forbids. The instance still cannot be
/// converted to the interface type, so the implementation is reachable only through a
/// generic constrained with `allows ref struct`.
/// </summary>
public interface IStStepper
{
    /// <summary>Advances by one and answers where it got to.</summary>
    int Step();
}

/// <summary>
/// Clause 16.2.3, post-standard — the implementing ref struct. Its interface member is an
/// implementation with no boxing conversion to reach it.
/// </summary>
public ref struct StRefStepper : IStStepper
{
    private ref int _slot;

    /// <summary>Clause 16.4.9 — a constructor taking the storage to step.</summary>
    public StRefStepper(ref int slot) => _slot = ref slot;

    /// <summary>Clause 16.2.5 — the interface implementation.</summary>
    public int Step() => ++_slot;
}

/// <summary>
/// Clause 16.2.3, post-standard — `allows ref struct`, the anti-constraint that lets a
/// type parameter be substituted with a ref struct. A struct with a field of such a type
/// parameter must itself be a ref struct.
/// </summary>
/// <typeparam name="T">A type that may be a ref struct.</typeparam>
public ref struct StRefCapableBox<T>
    where T : allows ref struct
{
    /// <summary>The held value, whose type may be a ref struct.</summary>
    public T Item;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StRefCapableBox(T item) => Item = item;
}

/// <summary>
/// Clause 16.2.3 — the uses. Every restriction the ref modifier imposes shows up here as
/// the shape of a use rather than as a declaration: a local, a `scoped` parameter, a
/// by-ref argument, and a generic call that had to be told ref structs are allowed.
/// </summary>
public static class StRefModifierUse
{
    /// <summary>Clause 16.4.15.3 — a ref struct in a local, the only variable kind it fits.</summary>
    public static int WalkFour()
    {
        int slot = 0;
        int origin = 0;
        StRefCursor cursor = new StRefCursor(ref slot, ref origin);
        cursor.Advance(4);
        return cursor.Offset;
    }

    /// <summary>
    /// Clause 16.4.15.2 — a `scoped` parameter of ref struct type, which promises the
    /// callee will not let the value outlive the call.
    /// </summary>
    public static int WindowLength(scoped StReadOnlyRefWindow window) => window.Length;

    /// <summary>Clause 16.2.3 — the generic call the anti-constraint makes legal.</summary>
    public static int StepOnce<T>(scoped T stepper)
        where T : IStStepper, allows ref struct => stepper.Step();

    /// <summary>Clause 16.2.3 — a ref struct as a type argument, which needs the anti-constraint.</summary>
    public static int BoxedStep()
    {
        int slot = 7;
        StRefStepper stepper = new StRefStepper(ref slot);
        StRefCapableBox<StRefStepper> box = new StRefCapableBox<StRefStepper>(stepper);
        return StepOnce(box.Item);
    }
}
