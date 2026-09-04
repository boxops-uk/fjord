// Clause 15.5.1 — the field declaration, at the one modifier a class may not carry. A `ref`
// field may be declared only in a `ref struct`, so the container below is a struct and the
// clause reached through it is still 15.5.1's: clause 16 says a struct's members are declared
// as a class's are, and this is the field form a class has no access to.

namespace Surface.Classes;

/// <summary>
/// 15.5.1 hazard — the four ways `ref` and `readonly` combine on a field. `ref readonly`
/// makes the referent read-only, `readonly ref` makes the reference itself unassignable, and
/// `readonly ref readonly` does both: three modifier lists over two keywords, and two of them
/// are the same two words in the other order.
/// </summary>
public ref struct ClsRefFieldHolder
{
    /// <summary>15.5.1 — a `ref` field: storage for a reference to an `int` elsewhere.</summary>
    public ref int Slot;

    /// <summary>15.5.1 — `ref readonly`: the reference may be reassigned, the referent may
    /// not be written through it.</summary>
    public ref readonly int Frozen;

    /// <summary>15.5.1 — `readonly ref`: the reference is fixed after construction, the
    /// referent is writable.</summary>
    public readonly ref int Fixed;

    /// <summary>15.5.1 — `readonly ref readonly`: both halves read-only.</summary>
    public readonly ref readonly int Both;

    /// <summary>15.5.1 — an ordinary field beside them, so the container is not all refs.</summary>
    public int Plain;

    /// <summary>15.5.2 — a static field in a byref-like type, which is permitted because it
    /// is not part of any instance.</summary>
    public static int Instances;
}
