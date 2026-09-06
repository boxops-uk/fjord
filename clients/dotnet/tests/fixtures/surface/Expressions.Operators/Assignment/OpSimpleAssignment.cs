using System;
using System.Collections.Generic;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Assignment;

/// <summary>A target with one of every kind of assignable member.</summary>
public sealed class OpTarget
{
    private readonly Dictionary<string, int> _slots = [];

    /// <summary>A writable field.</summary>
    public int Field;

    /// <summary>A writable property.</summary>
    public int Property { get; set; }

    /// <summary>A property with an init-only setter, assignable in an object initializer.</summary>
    public int Initialized { get; init; }

    /// <summary>The one indexer this type declares, so an element access can be assigned to.</summary>
    public int this[string slot]
    {
        get => _slots.TryGetValue(slot, out int value) ? value : 0;
        set => _slots[slot] = value;
    }
}

/// <summary>
/// The assignment forms of 12.23.1 and the simple assignment of 12.23.2, one target kind
/// at a time.
/// </summary>
public static class OpSimpleAssignment
{
    /// <summary>
    /// 12.23.2 — simple assignment to every kind of target the clause admits: a local, a
    /// parameter, a field, a property, an array element and an indexer element.
    /// </summary>
    public static int EveryTargetKind(OpTarget target, int[] items, int parameter)
    {
        int local;

        local = 1;
        parameter = 2;
        target.Field = 3;
        target.Property = 4;
        items[0] = 5;
        target["slot"] = 6;

        var initialized = new OpTarget { Initialized = 7, Property = 8 };

        return local + parameter + target.Field + target.Property + items[0] + target["slot"]
            + initialized.Initialized;
    }

    /// <summary>
    /// 12.23.2 — assignment is an expression, so it has a value and can be chained. The
    /// three targets are written to right to left.
    /// </summary>
    public static int Chained(out int first, out int second)
    {
        int third;

        first = second = third = 9;

        return third;
    }

    /// <summary>
    /// 12.23.2 — assignment where the right operand needs a conversion, and where it needs
    /// a user-defined one.
    /// </summary>
    public static (long Widened, OpMoney Converted) WithConversion(int narrow)
    {
        long widened;
        OpMoney converted;

        widened = narrow;
        converted = 500L;

        return (widened, converted);
    }

    /// <summary>
    /// 12.23.1 — the discard, which is a target that declares nothing and stores nothing.
    /// It appears as an assignment target, as a deconstruction element and as an
    /// <c>out</c> argument.
    /// </summary>
    public static bool Discards(string text)
    {
        _ = text.Length;

        (int kept, _) = (1, 2);

        bool parsed = int.TryParse(text, out _);

        return parsed && kept == 1;
    }

    /// <summary>
    /// 12.23.1 — the four assignment forms in one method: simple, compound, deconstructing
    /// and null-coalescing.
    /// </summary>
    public static string FourForms(string? text)
    {
        int simple = 1;

        simple += 2;

        (int left, int right) = (simple, simple * 2);

        text ??= "assigned";

        return $"{simple}{left}{right}{text}";
    }

    /// <summary>
    /// 12.23.2 — assignment to an event's backing delegate from inside its declaring type
    /// is a plain simple assignment, not the event assignment of 12.23.6.
    /// </summary>
    public static Action? AssignedDelegate(Action handler)
    {
        Action? slot = null;

        slot = handler;

        return slot;
    }
}
