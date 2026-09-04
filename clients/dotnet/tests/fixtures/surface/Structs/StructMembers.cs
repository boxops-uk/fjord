// Clause 16.3 — Struct members — and 16.3.1, General, and 16.2.6, Struct body. The
// members of a struct are the members introduced by its struct_member_declarations plus
// the members inherited from System.ValueType. A struct_member_declaration may be a
// constant, a field, a method, a property, an event, an indexer, an operator, an instance
// constructor, a static constructor or a type declaration — and, from the post-standard
// grammar, a finalizer is NOT among them: a struct cannot declare one, which is the only
// member kind class and struct bodies differ on syntactically.
//
// `StStructBody` below declares one of every permitted kind, once. It declares exactly one
// indexer, because a type with two is one of the five shapes that refuse a write.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.2.6 / 16.3.1 — one struct body holding every member kind a struct may
/// declare. The member list an index takes from this file is exactly what is written
/// below; the member list the compiler holds is that plus a parameterless constructor
/// (16.4.9), one auto-property backing field (16.4.11), one event backing field
/// (16.4.14), the accessor methods of the properties, the indexer and the event, and
/// everything inherited from System.ValueType.
/// </summary>
public struct StStructBody
{
    /// <summary>Clause 16.3.1 — a constant, which is a static member with no storage.</summary>
    public const int Capacity = 8;

    /// <summary>Clause 16.3.1 — an instance field.</summary>
    public int Used;

    /// <summary>Clause 16.3.1 — a static field, which lives once for the whole type.</summary>
    public static int Instances;

    /// <summary>Clause 16.3.1 — a static readonly field, initialized by the static constructor.</summary>
    public static readonly string Origin;

    /// <summary>Clause 16.4.10 — the static constructor.</summary>
    static StStructBody() => Origin = "16.2.6";

    /// <summary>Clause 16.4.9 — the instance constructor.</summary>
    public StStructBody(int used)
    {
        Used = used;
        Instances++;
    }

    /// <summary>Clause 16.4.11 — an automatically implemented property, whose field is unwritten.</summary>
    public string? Label { get; set; }

    /// <summary>Clause 16.4.11 — a property with a body, which has no backing field at all.</summary>
    public int Free => Capacity - Used;

    /// <summary>Clause 16.4.12 — an instance method.</summary>
    public bool Fits(int more) => Used + more <= Capacity;

    /// <summary>Clause 16.4.12 — a static method.</summary>
    public static StStructBody Empty() => new StStructBody(0);

    /// <summary>Clause 16.4.14 — a field-like event, whose delegate field is unwritten.</summary>
    public event EventHandler? Filled;

    /// <summary>Clause 16.4.13 — the one indexer this type declares.</summary>
    public int this[int index] => index < Used ? index : -1;

    /// <summary>Clause 16.4.15.5 — an operator declaration.</summary>
    public static StStructBody operator +(StStructBody left, int more) =>
        new StStructBody(left.Used + more);

    /// <summary>Clause 16.3.1 — a user-defined conversion, which is an operator declaration too.</summary>
    public static explicit operator int(StStructBody body) => body.Used;

    /// <summary>Clause 16.3.1 — a nested class.</summary>
    public sealed class StBodyPolicy
    {
        /// <summary>The largest value the policy permits.</summary>
        public int Ceiling { get; init; }
    }

    /// <summary>Clause 16.3.1 — a nested struct.</summary>
    public struct StBodySlot
    {
        /// <summary>Which slot.</summary>
        public int Index;
    }

    /// <summary>Clause 16.3.1 — a nested interface.</summary>
    public interface IStBodyVisitor
    {
        /// <summary>Visits a slot.</summary>
        void Visit(StBodySlot slot);
    }

    /// <summary>Clause 16.3.1 — a nested enum.</summary>
    public enum StBodyState
    {
        /// <summary>Nothing used.</summary>
        Empty,

        /// <summary>Some used.</summary>
        Partial,

        /// <summary>All used.</summary>
        Full,
    }

    /// <summary>Clause 16.3.1 — a nested delegate.</summary>
    /// <param name="state">The state reached.</param>
    public delegate void StBodyReached(StBodyState state);

    /// <summary>
    /// Clause 16.3.1 — the only member that raises the event, so the event is not merely
    /// declared. A field-like event of a struct can only be raised from inside the struct.
    /// </summary>
    public void Fill()
    {
        Used = Capacity;
        Filled?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Clause 16.3 — the inherited half of the member list. Every struct's members include
/// <c>System.ValueType</c>'s, and this class reaches four of them on a struct that
/// overrides none: two of them box, and the source names no declaration for any.
/// </summary>
public static class StInheritedMemberUse
{
    /// <summary>Clause 16.3 — <c>object.ToString</c>, inherited through ValueType.</summary>
    public static string ToStringOf(StStructBody body) => body.ToString() ?? string.Empty;

    /// <summary>Clause 16.3 — <c>ValueType.Equals</c>, which compares field by field.</summary>
    public static bool EqualsOf(StStructBody left, StStructBody right) => left.Equals(right);

    /// <summary>Clause 16.3 — <c>ValueType.GetHashCode</c>.</summary>
    public static int HashOf(StStructBody body) => body.GetHashCode();

    /// <summary>Clause 16.3 — <c>object.GetType</c>, which is not virtual and always boxes.</summary>
    public static Type TypeOf(StStructBody body) => body.GetType();

    /// <summary>Clause 16.3.1 — reaches the nested types, the constant and the static members.</summary>
    public static int NestedReach()
    {
        StStructBody.StBodySlot slot = new StStructBody.StBodySlot { Index = 1 };
        StStructBody.StBodyPolicy policy = new StStructBody.StBodyPolicy { Ceiling = StStructBody.Capacity };
        StStructBody.StBodyReached reached = static state => StStructBody.Instances += (int)state;
        reached(StStructBody.StBodyState.Full);
        return slot.Index + policy.Ceiling + StStructBody.Origin.Length;
    }
}
