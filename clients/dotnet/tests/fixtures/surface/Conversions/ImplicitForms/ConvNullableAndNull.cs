namespace Surface.Conversions.ImplicitForms;

/// <summary>A slot number. A struct exists here so that <c>default</c> has somewhere to land.</summary>
public readonly struct ConvSlot
{
    /// <summary>Names the slot.</summary>
    public ConvSlot(int index) => Index = index;

    /// <summary>Which slot this is.</summary>
    public int Index { get; }
}

/// <summary>
/// Clauses 10.2.6, 10.2.7 and 10.2.16 — the conversions that come out of a value that is
/// absent: lifting a value into a nullable type, the null literal, and <c>default</c>.
/// </summary>
public static class ConvNullableAndNull
{
    /// <summary>Clause 10.2.6 — <c>S</c> to <c>S?</c>.</summary>
    public static int? IntToNullableInt(int value) => value;

    /// <summary>Clause 10.2.6 — <c>S</c> to <c>T?</c>, over an implicit numeric conversion.</summary>
    public static long? IntToNullableLong(int value) => value;

    /// <summary>Clause 10.2.6 — <c>S?</c> to <c>T?</c>, the lifted numeric widening.</summary>
    public static double? NullableIntToNullableDouble(int? value) => value;

    /// <summary>Clause 10.2.6 — an enum lifted into its nullable form.</summary>
    public static ConvStroke? StrokeToNullable(ConvStroke stroke) => stroke;

    /// <summary>Clause 10.2.6 — a user-visible struct lifted into its nullable form.</summary>
    public static ConvSlot? SlotToNullable(ConvSlot slot) => slot;

    /// <summary>Clause 10.2.7 — the null literal to a reference type.</summary>
    public static string? NullString() => null;

    /// <summary>Clause 10.2.7 — the null literal to a nullable value type.</summary>
    public static int? NullInt() => null;

    /// <summary>Clause 10.2.7 — the null literal to an array type.</summary>
    public static int[]? NullArray() => null;

    /// <summary>Clause 10.2.7 — the null literal to a nullable enum type.</summary>
    public static ConvStroke? NullStroke() => null;

    /// <summary>Clause 10.2.7 — the null literal to a delegate type.</summary>
    public static System.Func<int>? NullDelegate() => null;

    /// <summary>Clause 10.2.16 — <c>default</c> to a struct type.</summary>
    public static ConvSlot DefaultSlot() => default;

    /// <summary>Clause 10.2.16 — <c>default</c> to a nullable value type, which is null.</summary>
    public static int? DefaultNullable() => default;

    /// <summary>Clause 10.2.16 — <c>default</c> to a reference type, which is also null.</summary>
    public static string? DefaultString() => default;

    /// <summary>Clause 10.2.16 — <c>default</c> at a parameter default, where it is a constant.</summary>
    public static int Sum(int first, int second = default) => first + second;
}
