// Clause 8.3 — value types, except enumerations (8.3.10), tuples (8.3.11) and nullable
// value types (8.3.12), which have files of their own. Covers 8.3.1 general, 8.3.2
// System.ValueType, 8.3.3 default constructors, 8.3.4 struct types, 8.3.5 simple types,
// 8.3.6 integral types, 8.3.7 floating-point types, 8.3.8 the Decimal type, 8.3.9 the Bool
// type, and 8.3.13 boxing and unboxing.

namespace Surface.Types;

/// <summary>
/// 8.3.1 — a value type. It has no null value of its own, so the nullable form
/// <c>TyPoint2D?</c> below is a second type built from this one declaration; an identity
/// that strips the <c>?</c> merges the two.
/// </summary>
public struct TyPoint2D
{
    /// <summary>9.2.3.3 — an instance variable in a struct.</summary>
    public double X;

    public double Y;

    /// <summary>8.3.3 — a declared constructor beside the parameterless one the struct always has.</summary>
    public TyPoint2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>8.3.2 — boxing this struct to its implicit base type, which no token here names.</summary>
    public System.ValueType AsValueType() => this;

    /// <summary>8.3.13 — the unboxing conversion back.</summary>
    public static TyPoint2D FromBox(object boxed) => (TyPoint2D)boxed;
}

/// <summary>
/// 8.3.1 hazard — the value type above referenced bare, as its nullable form, and as a
/// type argument. Three references, one declaration.
/// </summary>
public static class TyValueTypeUse
{
    /// <summary>8.3.1 — the bare value type.</summary>
    public static TyPoint2D Origin = default;

    /// <summary>8.3.1 / 8.3.12 — the nullable form, which is a distinct constructed type.</summary>
    public static TyPoint2D? MaybeOrigin = null;

    /// <summary>8.3.1 — as a type argument, so the struct appears inside a constructed type.</summary>
    public static System.Collections.Generic.List<TyPoint2D> Track = [];

    /// <summary>8.3.13 — boxing a value type to object, then unboxing it.</summary>
    public static double RoundTrip(TyPoint2D point)
    {
        object boxed = point;
        TyPoint2D back = (TyPoint2D)boxed;
        return back.X + back.Y;
    }

    /// <summary>8.3.13 — boxing to an interface, which is the other boxing destination.</summary>
    public static string BoxToInterface(int value)
    {
        System.IFormattable formattable = value;
        return formattable.ToString(null, null);
    }
}

/// <summary>
/// 8.3.3 — a struct that declares a parameterless constructor of its own (post-standard).
/// Every struct also has the implicit parameterless one that <c>default</c> reaches, and
/// only one of the two is written here.
/// </summary>
public struct TySeeded
{
    /// <summary>8.3.3 — the explicit parameterless constructor.</summary>
    public TySeeded()
    {
        Seed = 7;
    }

    public int Seed;

    /// <summary>8.3.3 — `default` bypasses the declared constructor and gives all-zeroes.</summary>
    public static TySeeded Zeroed => default;

    /// <summary>8.3.3 — `new()` runs it, so the two differ observably.</summary>
    public static TySeeded Constructed => new();
}

/// <summary>
/// 8.3.3 hazard — a struct with a primary constructor (post-standard). The primary
/// constructor is <c>.ctor(int)</c>; the implicit parameterless constructor is still there
/// and is written nowhere.
/// </summary>
public struct TyCounted(int start)
{
    /// <summary>9.2.3.3 — an instance variable initialized from a primary constructor parameter.</summary>
    private int _count = start;

    public int Count => _count;

    public void Advance() => _count++;
}

/// <summary>8.3.4 — a readonly struct: every instance member is implicitly readonly.</summary>
public readonly struct TyTemperature
{
    public TyTemperature(double celsius) => Celsius = celsius;

    public double Celsius { get; }

    public double Fahrenheit => (Celsius * 9 / 5) + 32;
}

/// <summary>
/// 8.3.4 hazard — a record struct (post-standard). Each primary constructor parameter is a
/// declaration, and each also generates a public property of exactly the same name, so
/// <c>Amount</c> and <c>Unit</c> each name two declarations inside this one type.
/// </summary>
public record struct TyMeasure(double Amount, string Unit)
{
    /// <summary>8.3.4 — a member added beside the generated ones.</summary>
    public string Rendered => $"{Amount}{Unit}";
}

/// <summary>8.3.4 — a readonly record struct, which is the immutable form of the above.</summary>
public readonly record struct TyRatio(int Numerator, int Denominator);

/// <summary>8.3.4 — a ref struct: a value type that may only live on the stack. Its ref
/// safe context rules are clause 9.7.2.</summary>
public ref struct TyStackWindow
{
    public TyStackWindow(System.ReadOnlySpan<char> text) => Text = text;

    /// <summary>8.3.4 — a span-typed instance variable, legal only in a ref struct.</summary>
    public System.ReadOnlySpan<char> Text;

    public int Length => Text.Length;
}

/// <summary>8.3.4 — a struct with a nested struct, so struct nesting is indexed too.</summary>
public struct TyEnvelope
{
    public TyStamp Postmark;

    /// <summary>8.3.4 — the nested value type.</summary>
    public struct TyStamp
    {
        public int Pence;
    }
}

/// <summary>
/// 8.3.5 hazard — the simple types, each written twice: once as its keyword and once as
/// the framework struct it aliases. Every pair is one type with two spellings.
/// </summary>
public static class TySimpleTypeSpellings
{
    /// <summary>8.3.6 — integral: sbyte.</summary>
    public static sbyte ByKeywordSByte;

    public static System.SByte ByFrameworkSByte;

    /// <summary>8.3.6 — integral: byte.</summary>
    public static byte ByKeywordByte;

    public static System.Byte ByFrameworkByte;

    /// <summary>8.3.6 — integral: short.</summary>
    public static short ByKeywordShort;

    public static System.Int16 ByFrameworkShort;

    /// <summary>8.3.6 — integral: ushort.</summary>
    public static ushort ByKeywordUShort;

    public static System.UInt16 ByFrameworkUShort;

    /// <summary>8.3.6 — integral: int.</summary>
    public static int ByKeywordInt;

    public static System.Int32 ByFrameworkInt;

    /// <summary>8.3.6 — integral: uint.</summary>
    public static uint ByKeywordUInt;

    public static System.UInt32 ByFrameworkUInt;

    /// <summary>8.3.6 — integral: long.</summary>
    public static long ByKeywordLong;

    public static System.Int64 ByFrameworkLong;

    /// <summary>8.3.6 — integral: ulong.</summary>
    public static ulong ByKeywordULong;

    public static System.UInt64 ByFrameworkULong;

    /// <summary>8.3.6 — integral: char, whose alias is not obvious from its name.</summary>
    public static char ByKeywordChar;

    public static System.Char ByFrameworkChar;

    /// <summary>8.3.7 — floating-point: float.</summary>
    public static float ByKeywordFloat;

    public static System.Single ByFrameworkFloat;

    /// <summary>8.3.7 — floating-point: double.</summary>
    public static double ByKeywordDouble;

    public static System.Double ByFrameworkDouble;

    /// <summary>8.3.8 — the Decimal type, which is a simple type but not floating-point.</summary>
    public static decimal ByKeywordDecimal;

    public static System.Decimal ByFrameworkDecimal;

    /// <summary>8.3.9 — the Bool type.</summary>
    public static bool ByKeywordBool;

    public static System.Boolean ByFrameworkBool;
}

/// <summary>
/// 8.3.6 hazard — the native-sized integers. <c>nint</c> and <c>System.IntPtr</c> are the
/// same type in this language version, so the pair below is one identity written two ways;
/// the same for <c>nuint</c> and <c>System.UIntPtr</c>.
/// </summary>
public static class TyNativeIntegers
{
    /// <summary>8.3.6 — nint by keyword.</summary>
    public static nint ByKeywordNInt;

    /// <summary>8.3.6 hazard — and the framework name it aliases.</summary>
    public static System.IntPtr ByFrameworkNInt;

    /// <summary>8.3.6 — nuint by keyword.</summary>
    public static nuint ByKeywordNUInt;

    public static System.UIntPtr ByFrameworkNUInt;

    /// <summary>
    /// 8.3.6 — Int128 has no keyword and is not in the standard's list of integral types,
    /// which is what makes the list above closed rather than illustrative.
    /// </summary>
    public static System.Int128 OutsideTheList;

    /// <summary>8.3.7 — Half likewise: a floating-point type the clause does not name.</summary>
    public static System.Half NarrowFloat;

    /// <summary>8.3.6 — checked and unchecked contexts over integral arithmetic (9.4.4.3).</summary>
    public static int Wrapped(int seed)
    {
        int wrapped = unchecked(seed * 2);
        int guarded;
        try
        {
            guarded = checked(seed + 1);
        }
        catch (System.OverflowException)
        {
            guarded = 0;
        }

        return wrapped + guarded;
    }
}

/// <summary>8.3.7 / 8.3.9 — the special values the floating-point and bool clauses name.</summary>
public static class TyNumericFacts
{
    /// <summary>8.3.7 — the values a floating-point type has beyond the finite ones.</summary>
    public static readonly double[] NonFinite =
        [double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon];

    /// <summary>8.3.7 — float has the same set at its own precision.</summary>
    public static readonly float[] NonFiniteSingle = [float.NaN, float.PositiveInfinity];

    /// <summary>8.3.8 — Decimal has no infinities, and its range and scale are its own.</summary>
    public static readonly decimal[] DecimalEdges = [decimal.MinValue, decimal.MaxValue, 0.0000000001m];

    /// <summary>8.3.9 — the Bool type has exactly two values, and no conversion to integer.</summary>
    public static readonly bool[] BothBools = [true, false];

    /// <summary>8.3.9 — bool as the type of a condition, which is the only place it is required.</summary>
    public static string Choose(bool flag) => flag ? "yes" : "no";
}

/// <summary>8.8 — unmanaged types, named by the constraint that admits exactly them.</summary>
public static class TyUnmanaged
{
    /// <summary>8.8 — `unmanaged` is a constraint, not a type; no name here resolves to a declaration.</summary>
    public static int SizeOfUnmanaged<TValue>()
        where TValue : unmanaged => System.Runtime.CompilerServices.Unsafe.SizeOf<TValue>();

    /// <summary>8.8 — a struct of unmanaged fields is itself an unmanaged type.</summary>
    public static int SizeOfPoint() => SizeOfUnmanaged<TyPoint2D>();

    /// <summary>8.8 — an enum is unmanaged, whatever its underlying type.</summary>
    public static int SizeOfEnum() => SizeOfUnmanaged<TyStroke>();
}
