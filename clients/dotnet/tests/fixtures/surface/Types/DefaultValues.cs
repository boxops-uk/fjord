// Clause 9.3 — default values. Every variable of a type that is not initially assigned
// starts at its type's default value, and the `default` literal is how that value is
// written without naming the type. Nothing here is a declaration the clause invents: the
// interest is that a field with no initializer and a field initialized to `default` are
// indistinguishable at run time and are two different things in source.

namespace Surface.Types;

/// <summary>
/// 9.3 — the default value of each category of type, written three ways: left implicit,
/// written as the bare <c>default</c> literal, and written as <c>default(T)</c>.
/// </summary>
public sealed class VarDefaultValues
{
    /// <summary>9.3 — an integral type, whose default is zero. No initializer.</summary>
    public int ImplicitInt;

    /// <summary>9.3 — the same value from the target-typed `default` literal.</summary>
    public int LiteralInt = default;

    /// <summary>9.3 — and from `default(T)`, which names the type and so is a type reference.</summary>
    public int TypedInt = default(int);

    /// <summary>9.3 — bool defaults to false, char to the null character.</summary>
    public bool ImplicitBool;

    public char ImplicitChar = default(char);

    /// <summary>9.3 — a floating-point type, whose default is positive zero.</summary>
    public double ImplicitDouble;

    /// <summary>9.3 — decimal, whose default is zero with scale zero.</summary>
    public decimal ImplicitDecimal = default;

    /// <summary>9.3 — an enum, whose default is zero whether or not a member has that value.</summary>
    public TyCardinal ImplicitEnum;

    /// <summary>9.3 — an enum whose members start at one, so its default names no member.</summary>
    public TyStroke UnnamedDefault = default;

    /// <summary>9.3 — a struct, whose default is every field at its own default, recursively.</summary>
    public TyPoint2D ImplicitStruct;

    public VarInstanceInStruct TypedStruct = default(VarInstanceInStruct);

    /// <summary>9.3 — a nullable value type, whose default is the null value and not the
    /// default of its element type.</summary>
    public int? ImplicitNullable;

    /// <summary>9.3 — a reference type, whose default is null however the context reads it.</summary>
    public string? ImplicitReference;

    /// <summary>9.3 — an array type, whose default is null and not an empty array.</summary>
    public int[]? ImplicitArray;

    /// <summary>9.3 — a delegate type and an interface type, both null by default.</summary>
    public TyAdder? ImplicitDelegate;

    public ITyNamed? ImplicitInterface;

    /// <summary>9.3 — a tuple type, whose default is elementwise.</summary>
    public (int Row, string? Label) ImplicitTuple;

    /// <summary>9.3 — `default` reaching a struct that declares a parameterless constructor,
    /// which it does not run (8.3.3).</summary>
    public TySeeded UnseededByDefault = default;

    /// <summary>9.3 — the same struct constructed, which does run it. The two differ.</summary>
    public TySeeded SeededByNew = new();
}

/// <summary>9.3 — default values reached through a type parameter, where the type is not
/// known and so `default` is the only way to write the value.</summary>
public static class VarGenericDefaults
{
    /// <summary>9.3 — `default(TValue)` for an unconstrained type parameter, which may be a
    /// null reference or an all-zeroes struct.</summary>
    public static TValue? Nothing<TValue>() => default;

    /// <summary>9.3 — the same for a value-type-constrained parameter, where it is never null.</summary>
    public static TValue Zeroed<TValue>()
        where TValue : struct => default;

    /// <summary>9.3 — an array creation expression, whose elements are set to the default.</summary>
    public static TValue[] Blank<TValue>(int length) => new TValue[length];

    /// <summary>9.3 — a comparison against the default value, which needs the equality
    /// comparer rather than `==` for an unconstrained parameter.</summary>
    public static bool IsDefault<TValue>(TValue candidate) =>
        System.Collections.Generic.EqualityComparer<TValue>.Default.Equals(candidate, default!);
}
