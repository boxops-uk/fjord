using System;
using System.Collections.Generic;

namespace Surface.Preview;

/// <summary>Something with writable members, for the assignments below to target.</summary>
public sealed class Register
{
    /// <summary>A settable property.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A settable field.</summary>
    public int Count;

    /// <summary>An event, which a null-conditional assignment may also target.</summary>
    public event Action<int>? Changed;

    /// <summary>The slots, reached through the indexer below.</summary>
    private readonly Dictionary<string, int> _slots = [];

    /// <summary>The only indexer on this type.</summary>
    /// <param name="key">Which slot.</param>
    public int this[string key]
    {
        get => _slots.TryGetValue(key, out var value) ? value : 0;
        set => _slots[key] = value;
    }

    /// <summary>Raises the event, so the subscription has an effect.</summary>
    public void Raise() => Changed?.Invoke(Count);
}

/// <summary>
/// C# 14 — Null-conditional assignment: <c>?.</c> and <c>?[]</c> on the *left* of an
/// assignment or a compound assignment. The whole statement becomes conditional — when the
/// receiver is null, the right-hand side is never evaluated — so a write that looks
/// unconditional in source may not happen, and a call in the right operand may not either.
/// </summary>
public static class NullConditionalAssignment
{
    /// <summary>C# 14 — <c>?.</c> on the left of a simple assignment to a property.</summary>
    public static void Rename(Register? register, string name) => register?.Name = name;

    /// <summary>C# 14 — <c>?.</c> on the left of an assignment to a field.</summary>
    public static void Reset(Register? register) => register?.Count = 0;

    /// <summary>C# 14 — <c>?.</c> with a compound assignment, which reads and writes.</summary>
    public static void Bump(Register? register, int by) => register?.Count += by;

    /// <summary>C# 14 — <c>?[]</c> on the left of an assignment, through an indexer.</summary>
    public static void Seed(Register? register, string key) => register?[key] = 1;

    /// <summary>C# 14 — <c>?[]</c> with a compound assignment.</summary>
    public static void Add(Register? register, string key, int by) => register?[key] += by;

    /// <summary>C# 14 — <c>?.</c> on the left of an event subscription.</summary>
    public static void Subscribe(Register? register, Action<int> handler) => register?.Changed += handler;

    /// <summary>C# 14 — <c>?[]</c> over an array, whose element type is a value type.</summary>
    public static void Fill(int[]? values) => values?[0] = 9;

    /// <summary>
    /// C# 14 — the right-hand side is not evaluated when the receiver is null. The counter
    /// below is the observable difference between this and an <c>if</c> around an assignment
    /// that had already been evaluated.
    /// </summary>
    public static int Skipped(Register? register)
    {
        var evaluations = 0;

        register?.Count = Next(ref evaluations);

        return evaluations;
    }

    /// <summary>A chain of two conditionals, both of which must hold.</summary>
    public static void Chained(Register? register, Dictionary<string, Register>? map)
    {
        map?["first"] = register ?? new Register();
        register?.Name = "chained";
    }

    /// <summary>Runs every assignment against a real receiver and against <c>null</c>.</summary>
    public static string All()
    {
        var register = new Register();

        Rename(register, "named");
        Reset(register);
        Bump(register, 3);
        Seed(register, "slot");
        Add(register, "slot", 4);
        Subscribe(register, _ => { });
        Fill([0, 0]);
        register.Raise();

        Rename(null, "ignored");
        Bump(null, 1);
        Add(null, "slot", 1);
        Fill(null);

        return $"{register.Name}{register.Count}{register["slot"]}{Skipped(null)}{Skipped(register)}";
    }

    private static int Next(ref int evaluations) => ++evaluations;
}
