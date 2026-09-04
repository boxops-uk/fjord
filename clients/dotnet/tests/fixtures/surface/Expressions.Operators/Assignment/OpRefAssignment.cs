using System;

namespace Surface.Expressions.Operators.Assignment;

/// <summary>
/// A slot that holds a reference rather than a value, so that the ref assignment of
/// 12.23.4 has a field to write to as well as a local.
/// </summary>
/// <remarks>
/// A <c>ref</c> field can only be declared in a <c>ref struct</c>, and only a ref
/// assignment can write one: <c>_slot = ref slot</c> rebinds what the field refers to,
/// while <c>_slot = slot</c> would write through it.
/// </remarks>
public ref struct OpRefSlot
{
    private ref int _slot;

    /// <summary>12.23.4 — a ref assignment to a ref field.</summary>
    public OpRefSlot(ref int slot) => _slot = ref slot;

    /// <summary>Reads and writes through the reference the field holds.</summary>
    public int Value
    {
        get => _slot;
        set => _slot = value;
    }
}

/// <summary>
/// The ref assignment of 12.23.4: the one assignment form whose left operand is a
/// reference and whose right operand is a <c>ref</c> expression.
/// </summary>
/// <remarks>
/// The hazard here is aliasing. After <c>ref int alias = ref first</c> the method has two
/// names for one storage location, and after <c>alias = ref second</c> it has two names
/// for a different one. Every read and write through <c>alias</c> is a reference to a
/// declaration that is not the one whose storage it touches.
/// </remarks>
public static class OpRefAssignment
{
    /// <summary>
    /// 12.23.4 — a ref local initialised by a ref assignment, then rebound by a second
    /// one. The second <c>= ref</c> is the assignment form; the first is a declaration
    /// with a ref initializer.
    /// </summary>
    public static int Rebind(ref int first, ref int second, bool takeSecond)
    {
        ref int alias = ref first;

        if (takeSecond)
        {
            alias = ref second;
        }

        alias = 42;

        return first + second;
    }

    /// <summary>
    /// 12.23.4 — a <c>ref readonly</c> local, which may be rebound but not written
    /// through, so the two operations the token <c>=</c> can mean come apart cleanly.
    /// </summary>
    public static int ReadOnlyAlias(ref readonly int first, ref readonly int second, bool takeSecond)
    {
        ref readonly int alias = ref first;

        if (takeSecond)
        {
            alias = ref second;
        }

        return alias;
    }

    /// <summary>
    /// 12.23.4 — a ref local aliasing an array element and a field, which are the two
    /// storage kinds beyond a local that a ref expression may name.
    /// </summary>
    public static int AliasElementsAndFields(int[] items, OpTarget target)
    {
        ref int element = ref items[0];
        ref int field = ref target.Field;

        element = 1;
        field = 2;

        element = ref items[1];
        element = 3;

        return items[0] + items[1] + target.Field;
    }

    /// <summary>
    /// 12.23.4 — a ref assignment whose right operand is a ref-returning call, so the
    /// reference comes from another member rather than from a variable in scope.
    /// </summary>
    public static int FromARefReturn(int[] items)
    {
        ref int chosen = ref Pick(items, 0);

        chosen = 7;

        chosen = ref Pick(items, 1);
        chosen = 8;

        return items[0] + items[1];
    }

    /// <summary>A ref-returning method, so there is a ref expression to assign from.</summary>
    public static ref int Pick(int[] items, int index) => ref items[index];

    /// <summary>
    /// 12.23.4 / 12.20 — a ref assignment from a ref conditional, where which storage the
    /// alias lands on is decided at run time.
    /// </summary>
    public static int FromARefConditional(ref int first, ref int second, bool takeFirst)
    {
        ref int alias = ref takeFirst ? ref first : ref second;

        alias = 11;

        return first + second;
    }

    /// <summary>
    /// 12.23.4 — a ref assignment to a ref field, made through a <c>ref struct</c> whose
    /// constructor is the only place the field can be bound.
    /// </summary>
    public static int ThroughARefField(int seed)
    {
        int storage = seed;
        var slot = new OpRefSlot(ref storage);

        slot.Value = slot.Value + 1;

        return storage;
    }

    /// <summary>
    /// 12.23.4 — <c>ref</c> in the two neighbouring positions that are not assignments:
    /// an argument and a return. The same keyword, and no assignment at all.
    /// </summary>
    public static ref int NotAnAssignment(int[] items)
    {
        Mutate(ref items[0]);

        return ref items[0];
    }

    private static void Mutate(ref int value) => value++;
}
