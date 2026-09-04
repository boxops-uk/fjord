// Clause 8.3.10 — enumeration types. An enum is a value type whose implicit base is
// System.Enum and whose members are constants of the enum type itself, so every member
// declaration below has the enum as its own type.

namespace Surface.Types;

/// <summary>
/// 8.3.10 — an enum with an implicit underlying type of int and explicit member values.
/// </summary>
public enum TyStroke
{
    /// <summary>8.3.10 — a member with an explicit value.</summary>
    Thin = 1,

    /// <summary>
    /// 8.3.10 hazard — a second member with the same value as <see cref="Thin"/>. Two
    /// declarations, one constant value; an identity keyed on the value merges them.
    /// </summary>
    Slim = 1,

    Thick = 2,
}

/// <summary>8.3.10 — an enum with no explicit values, so the members number from zero.</summary>
public enum TyCardinal
{
    North,
    East,
    South,
    West,
}

/// <summary>8.3.10 — an enum with an explicit underlying integral type (8.3.6).</summary>
public enum TyPriority : byte
{
    Low = 0,
    Normal = 1,
    High = 255,
}

/// <summary>8.3.10 — an enum whose underlying type is signed and negative-valued.</summary>
public enum TyDrift : short
{
    Backwards = -1,
    Still = 0,
    Forwards = 1,
}

/// <summary>8.3.10 — a bit-flags enum, whose members combine.</summary>
[System.Flags]
public enum TyAccessFlags : uint
{
    None = 0,
    Read = 1,
    Write = 2,

    /// <summary>8.3.10 — a member whose value is a constant expression over other members.</summary>
    ReadWrite = Read | Write,
}

/// <summary>8.3.10 — enumeration types in use, and the members of System.Enum they inherit.</summary>
public static class TyEnumerationUse
{
    /// <summary>8.3.10 — an enum-typed field.</summary>
    public static TyStroke Pen = TyStroke.Thin;

    /// <summary>8.3.10 — the aliased member, which is the same value as the field above.</summary>
    public static TyStroke Alias = TyStroke.Slim;

    /// <summary>8.3.2 — an enum boxes to System.Enum, its implicit base, and no token names it.</summary>
    public static System.Enum AsBase() => Pen;

    /// <summary>8.3.10 — the explicit conversions to and from the underlying type.</summary>
    public static int ToUnderlying(TyStroke stroke) => (int)stroke;

    public static TyStroke FromUnderlying(int value) => (TyStroke)value;

    /// <summary>8.3.10 — flags combination and testing at the use site.</summary>
    public static bool CanWrite(TyAccessFlags flags) => (flags & TyAccessFlags.Write) != 0;

    public static TyAccessFlags Grant() => TyAccessFlags.Read | TyAccessFlags.Write;

    /// <summary>8.3.10 — an enum as a switch governing type, with a member per arm (9.4.4.7).</summary>
    public static string Describe(TyCardinal heading) => heading switch
    {
        TyCardinal.North => "up",
        TyCardinal.East => "right",
        TyCardinal.South => "down",
        TyCardinal.West => "left",
        _ => "nowhere",
    };

    /// <summary>8.3.10 — an enum nested inside a class, so the enum's container is a type.</summary>
    public sealed class TyNestedHost
    {
        /// <summary>8.3.10 — the nested enum declaration.</summary>
        public enum TyNestedState
        {
            Idle,
            Busy,
        }

        public TyNestedState State { get; set; } = TyNestedState.Idle;
    }
}
