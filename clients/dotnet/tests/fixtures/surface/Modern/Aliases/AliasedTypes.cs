// C# 12 — Alias any type. Before C# 12 a using alias could only name a *named* type, so a
// tuple, an array, a pointer and a function pointer had no alias at all. The aliases below
// name types that have no declaration anywhere — an alias is the only place their name exists,
// and every reference through one resolves to a type nobody declared.
using Coordinate = (double Latitude, double Longitude);
using Grid = int[,];
using Jagged = int[][];
using Callback = System.Action<int>;
using Readings = System.Collections.Generic.List<double>;
using Fraction = System.ValueTuple<int, int>;

// C# 12 — an alias for a pointer type, which must be declared `using unsafe`.
using unsafe BytePointer = byte*;

// C# 12 — an alias for a function pointer type, the other shape that needs `unsafe`.
using unsafe Transform = delegate* managed<int, int>;

namespace Surface.Modern.Aliases;

/// <summary>C# 12 — Alias any type, used.</summary>
public static class AliasedTypes
{
    /// <summary>C# 12 — a tuple alias as a return type, with its element names intact.</summary>
    public static Coordinate Origin() => (0.0, 0.0);

    /// <summary>C# 12 — a tuple alias as a parameter type; the element names come from the alias.</summary>
    public static double Latitude(Coordinate where) => where.Latitude;

    /// <summary>C# 12 — an alias for a multidimensional array.</summary>
    public static int Corner(Grid grid) => grid[0, 0];

    /// <summary>C# 12 — an alias for a jagged array, which is a different type entirely.</summary>
    public static int FirstOfFirst(Jagged rows) => rows[0][0];

    /// <summary>C# 12 — an alias for a constructed generic type, which C# 11 already allowed.</summary>
    public static double Total(Readings readings)
    {
        var total = 0.0;

        foreach (var reading in readings)
        {
            total += reading;
        }

        return total;
    }

    /// <summary>C# 12 — an alias for a delegate type, invoked.</summary>
    public static void Notify(Callback callback) => callback(1);

    /// <summary>C# 12 — an alias for <see cref="ValueTuple{T1, T2}"/>, spelled generically.</summary>
    public static int Numerator(Fraction fraction) => fraction.Item1;

    /// <summary>C# 12 — the pointer alias, in an unsafe method.</summary>
    public static unsafe byte FirstByte(BytePointer bytes) => bytes[0];

    /// <summary>C# 12 — the function pointer alias, called through.</summary>
    public static unsafe int Apply(Transform transform, int value) => transform(value);

    /// <summary>Reads every alias above, so each has a use as well as a declaration.</summary>
    public static unsafe double All()
    {
        // An array alias cannot be the element type of an array-creation expression:
        // `new Grid[1, 1]` would be a *two-dimensional array of* `Grid`. The alias is usable
        // as the variable's type and nowhere else in this statement.
        Grid grid = new int[1, 1];
        Jagged jagged = new int[1][];
        jagged[0] = [7];

        var readings = new Readings { 1.5, 2.5 };
        var counted = 0;
        Notify(value => counted += value);

        var scratch = stackalloc byte[2];
        scratch[0] = 3;

        return Latitude(Origin())
            + Corner(grid)
            + FirstOfFirst(jagged)
            + Total(readings)
            + Numerator(new Fraction(4, 5))
            + FirstByte(scratch)
            + counted;
    }
}
