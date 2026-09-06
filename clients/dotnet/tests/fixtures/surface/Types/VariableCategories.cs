// Clause 9.2 — the variable categories. The standard names seven: static variables (9.2.2),
// instance variables (9.2.3), array elements (9.2.4), value parameters (9.2.5), reference
// parameters (9.2.6), output parameters (9.2.7), input parameters (9.2.8) and local
// variables (9.2.9). A constant is not among them, which is why one is declared here beside
// the fields for contrast.

namespace Surface.Types;

/// <summary>
/// 9.2.1 hazard — one simple name, three variable categories. <c>value</c> is an instance
/// variable, a value parameter and a local variable in this one type; all three are
/// declarations and none of them hides another type's member.
/// </summary>
public sealed class VarOneName
{
    /// <summary>9.2.1 / 9.2.3.2 — the instance variable named <c>value</c>.</summary>
    private int value;

    /// <summary>9.2.1 hazard — the value parameter named <c>value</c>, which hides the field.</summary>
    public void Store(int value) => this.value = value;

    /// <summary>9.2.1 hazard — and the local variable named <c>value</c>, in a third scope.</summary>
    public int Doubled()
    {
        int value = this.value * 2;
        return value;
    }

    /// <summary>
    /// 9.2.1 — a property's implicit <c>value</c> parameter, which is a fourth declaration of
    /// the name and is written nowhere at all.
    /// </summary>
    public int Held
    {
        get => value;
        set => this.value = value;
    }
}

/// <summary>9.2.2 — static variables, in every form a field may take.</summary>
public class VarStaticsBase
{
    /// <summary>9.2.2 — a static variable: one storage location for the whole program.</summary>
    public static int Tally;

    /// <summary>9.2.2 — a static variable with an initializer, run by the static constructor.</summary>
    public static string Origin = "base";

    /// <summary>9.2.2 — a readonly static variable, assignable only here or in a static constructor.</summary>
    public static readonly int Ceiling;

    /// <summary>
    /// 9.2.2 — a constant, which the standard does not count as a variable at all: it has no
    /// storage location, and every use of it is a copy of its value.
    /// </summary>
    public const int Floor = 0;

    /// <summary>9.2.2 — a static variable per thread rather than per program.</summary>
    [System.ThreadStatic]
    public static int PerThread;

    /// <summary>9.6 — a static variable whose reads and writes are ordered, not merely atomic.</summary>
    public static volatile bool Stopping;

    /// <summary>9.2.2 — the static constructor, which is where <c>Ceiling</c> is assigned.</summary>
    static VarStaticsBase() => Ceiling = 100;
}

/// <summary>
/// 9.2.2 hazard — a derived class hiding the base's static variable with one of the same
/// name. Two static variable declarations, one simple name, and <c>new</c> is what makes it
/// legal rather than a warning.
/// </summary>
public sealed class VarStaticsDerived : VarStaticsBase
{
    /// <summary>9.2.2 hazard — the hiding declaration.</summary>
    public new static int Tally;

    /// <summary>9.2.2 — the hidden one is still reachable through the base type's name.</summary>
    public static int BothTallies() => Tally + VarStaticsBase.Tally;
}

/// <summary>
/// 9.2.2 hazard — a static variable in a generic type. There is one declaration and one
/// storage location per constructed type, so <c>Count</c> below is three variables at run
/// time and one row in any index.
/// </summary>
public static class VarStaticInGeneric<TSubject>
{
    /// <summary>9.2.2 — the per-instantiation static variable.</summary>
    public static int Count;

    public static void Bump() => Count++;
}

/// <summary>9.2.2 — the three instantiations, which share nothing but the declaration.</summary>
public static class VarStaticInGenericUse
{
    public static (int OfInt, int OfString, int OfPoint) Separate()
    {
        VarStaticInGeneric<int>.Bump();
        VarStaticInGeneric<string>.Bump();
        VarStaticInGeneric<string>.Bump();
        VarStaticInGeneric<TyPoint2D>.Bump();
        return (VarStaticInGeneric<int>.Count,
            VarStaticInGeneric<string>.Count,
            VarStaticInGeneric<TyPoint2D>.Count);
    }
}

/// <summary>9.2.3.1 / 9.2.3.2 — instance variables in a class: one storage location per
/// instance, initialized to their default values (9.3) before any initializer runs.</summary>
public sealed class VarInstanceInClass
{
    /// <summary>9.2.3.2 — an instance variable with no initializer, so 9.3 gives it its value.</summary>
    public int Unset;

    /// <summary>9.2.3.2 — an instance variable with an initializer.</summary>
    public int Set = 7;

    /// <summary>9.2.3.2 — a readonly instance variable, assignable only in a constructor.</summary>
    public readonly string Label;

    /// <summary>9.2.3.2 — an instance variable of a reference type, whose default is null.</summary>
    public string? Reference;

    /// <summary>9.2.3.2 — an instance variable of a value type, whose default is all-zeroes.</summary>
    public TyPoint2D Position;

    public VarInstanceInClass(string label) => Label = label;
}

/// <summary>
/// 9.2.3.3 — instance variables in a struct. Each one is part of the struct's own storage,
/// so a struct variable's lifetime is its container's rather than the heap's.
/// </summary>
public struct VarInstanceInStruct
{
    /// <summary>9.2.3.3 — an instance variable in a struct.</summary>
    public int Left;

    public int Right;

    /// <summary>9.2.3.3 — a readonly instance variable in a struct.</summary>
    public readonly int Fixed;

    /// <summary>9.2.3.3 — an instance variable whose type is itself a struct, so the storage nests.</summary>
    public TyPoint2D Anchor;

    public VarInstanceInStruct(int left, int right)
    {
        Left = left;
        Right = right;
        Fixed = left + right;
        Anchor = default;
    }
}

/// <summary>
/// 9.2.4 hazard — array elements. An array element is a variable with no declaration of its
/// own: it is created by the array creation expression, and the only way to name one is an
/// element access. A <c>ref</c> local can alias one, which is what makes it a variable
/// rather than a value.
/// </summary>
public static class VarArrayElements
{
    /// <summary>9.2.4 — a single-dimensional array, whose elements are int variables.</summary>
    public static readonly int[] Vector = [10, 20, 30];

    /// <summary>9.2.4 — a two-dimensional array, whose element access takes two indices.</summary>
    public static readonly int[,] Grid = new int[2, 3];

    /// <summary>9.2.4 — a jagged array, whose elements are themselves array variables.</summary>
    public static readonly int[][] Jagged = [[1], [2, 3]];

    /// <summary>9.2.4 — an array of a value type, so each element holds the struct itself.</summary>
    public static readonly TyPoint2D[] Track = new TyPoint2D[4];

    /// <summary>9.2.4 hazard — a ref local aliasing an array element, so a write through the
    /// alias changes the array.</summary>
    public static int BumpFirst()
    {
        ref int slot = ref Vector[0];
        slot++;
        return Vector[0];
    }

    /// <summary>9.2.4 — assigning through element accesses of each shape.</summary>
    public static void FillAll()
    {
        Vector[1] = 21;
        Grid[1, 2] = 6;
        Jagged[1][0] = 4;
        Track[0].X = 1.5;
    }

    /// <summary>9.2.4 — an element of a value-type array assigned in place, which needs the
    /// element to be a variable and not a copy.</summary>
    public static double MoveFirst()
    {
        Track[0].X += 1;
        return Track[0].X;
    }

    /// <summary>9.2.4 — index-from-end and range access (post-standard), over the same elements.</summary>
    public static (int Last, int[] Tail) FromEnd() => (Vector[^1], Vector[1..]);
}

/// <summary>
/// 9.2.5 — value parameters. Each is a variable initialized from its argument, so assigning
/// to one changes nothing the caller can see.
/// </summary>
public static class VarValueParameters
{
    /// <summary>9.2.5 — an ordinary value parameter, assigned to inside the body.</summary>
    public static int Consume(int count)
    {
        count += 1;
        return count;
    }

    /// <summary>9.2.5 hazard — an optional value parameter with a default. Together with
    /// <see cref="Consume(int)"/> this makes two methods callable with one argument.</summary>
    public static int Consume(int count, int step = 1) => count + step;

    /// <summary>9.2.5 — one default value of each kind the grammar admits.</summary>
    public static string Defaults(
        int number = 42,
        double ratio = 1.5,
        char letter = 'x',
        bool flag = true,
        string text = "none",
        string? absent = null,
        TyStroke stroke = TyStroke.Thin,
        int? maybe = null,
        TyPoint2D origin = default,
        object? nothing = default) =>
        $"{number}{ratio}{letter}{flag}{text}{absent}{stroke}{maybe}{origin.X}{nothing}";

    /// <summary>9.2.5 — a params array parameter, which is a value parameter of an array type.</summary>
    public static int SumAll(params int[] values)
    {
        int running = 0;
        foreach (int value in values)
        {
            running += value;
        }

        return running;
    }

    /// <summary>9.2.5 — a params span parameter (post-standard), which allocates nothing.</summary>
    public static int CountAll(params System.ReadOnlySpan<char> letters) => letters.Length;

    /// <summary>9.2.5 — a params parameter of a collection type (post-standard).</summary>
    public static int CountList(params System.Collections.Generic.List<string> names) => names.Count;

    /// <summary>9.2.5 — calls that exercise the defaults, named arguments, and both params forms.</summary>
    public static string CallThem() =>
        $"{Consume(1)}{Consume(1, 2)}{Defaults(ratio: 2.5, flag: false)}{SumAll(1, 2, 3)}" +
        $"{SumAll()}{CountAll('a', 'b')}{CountList("one")}";
}

/// <summary>
/// 9.2.6 hazard — reference parameters. <c>Take(int)</c> and <c>Take(ref int)</c> differ
/// only in the ref kind of one parameter, which is enough for the language and may not be
/// enough for an identity string.
/// </summary>
public static class VarReferenceParameters
{
    /// <summary>9.2.6 hazard — the value overload.</summary>
    public static int Take(int slot) => slot + 1;

    /// <summary>9.2.6 hazard — the by-reference overload, which writes through the alias.</summary>
    public static int Take(ref int slot)
    {
        slot += 1;
        return slot;
    }

    /// <summary>9.2.6 — a ref parameter of a value type, and one of a reference type.</summary>
    public static void Swap(ref TyPoint2D left, ref TyPoint2D right) =>
        (left, right) = (right, left);

    public static void Replace(ref string text) => text = text.ToUpperInvariant();

    /// <summary>9.2.6 — the call site must repeat `ref`, so the reference is written twice.</summary>
    public static int CallByReference()
    {
        int local = 1;
        int byValue = Take(local);
        int byReference = Take(ref local);
        return byValue + byReference + local;
    }
}

/// <summary>
/// 9.2.7 hazard — output parameters. <c>Fill(int)</c> and <c>Fill(out int)</c> differ only
/// in the ref kind, and an out parameter is initially unassigned (9.4.3) inside the body
/// and definitely assigned at every return.
/// </summary>
public static class VarOutputParameters
{
    /// <summary>9.2.7 hazard — the value overload.</summary>
    public static bool Fill(int seed) => seed > 0;

    /// <summary>9.2.7 hazard — the out overload, which must assign before returning.</summary>
    public static bool Fill(out int seed)
    {
        seed = 1;
        return true;
    }

    /// <summary>9.2.7 — two out parameters, both assigned on every path (9.4.1).</summary>
    public static bool Split(string text, out string head, out string tail)
    {
        int space = text.IndexOf(' ');
        if (space < 0)
        {
            head = text;
            tail = string.Empty;
            return false;
        }

        head = text[..space];
        tail = text[(space + 1)..];
        return true;
    }

    /// <summary>9.2.7 — an out parameter of a value type and of a nullable reference type.</summary>
    public static void Reset(out TyPoint2D point, out string? label)
    {
        point = default;
        label = null;
    }

    /// <summary>9.4.4.24 — out-variable declarations at the call site, which is where the
    /// declaration of an out variable lives.</summary>
    public static string CallWithOutVariables(string text)
    {
        if (Split(text, out string head, out var tail))
        {
            return head + tail;
        }

        Reset(out TyPoint2D point, out _);
        return point.X.ToString();
    }
}

/// <summary>
/// 9.2.8 hazard — input parameters. <c>in</c> and <c>ref readonly</c> both pass a
/// read-only alias, and the two forms cannot be overloads of each other: they are the same
/// signature with different diagnostics, so any index that keeps only one of the two words
/// has lost the difference.
/// </summary>
public static class VarInputParameters
{
    /// <summary>9.2.8 — an in parameter, which may be passed with or without the keyword.</summary>
    public static double Measure(in TyPoint2D point) => point.X + point.Y;

    /// <summary>9.2.8 hazard — the same aliasing written as `ref readonly` (post-standard),
    /// which warns at a call site that omits the keyword.</summary>
    public static double MeasureStrictly(ref readonly TyPoint2D point) => point.X - point.Y;

    /// <summary>9.2.8 — an in parameter of a simple type, where the alias costs more than a copy.</summary>
    public static int Negate(in int value) => -value;

    /// <summary>9.2.8 — the call sites: with the keyword, and without it for `in` only.</summary>
    public static double CallThem()
    {
        TyPoint2D point = new(3, 4);
        double implicitly = Measure(point);
        double explicitly = Measure(in point);
        double strict = MeasureStrictly(in point);
        return implicitly + explicitly + strict + Negate(1);
    }
}

/// <summary>
/// 9.2.9.1 hazard — local variables. Two declarators in one declaration statement are two
/// declarations sharing a type and a statement, and an implicitly typed local has no type
/// token at all.
/// </summary>
public static class VarLocals
{
    /// <summary>9.2.9.1 — the shapes a local declaration can take.</summary>
    public static int Declare()
    {
        // 9.2.9.1 hazard — one statement, two variable declarations.
        int first = 1, second = 2;

        // 9.2.9.1 — an implicitly typed local, whose type is inferred from the initializer.
        var inferred = first + second;

        // 9.2.9.1 — a local with no initializer, which is initially unassigned (9.4.3).
        int later;
        later = inferred;

        // 9.2.9.1 — a local constant, which like a static one is not a variable (9.4.4.21).
        const int scale = 10;

        // 9.2.9.1 — a local of a value type, a reference type and a nullable value type.
        TyPoint2D point = new(1, 2);
        string? text = null;
        int? maybe = null;

        // 9.2.9.1 — a local whose type is a tuple, declared rather than deconstructed.
        (int Row, int Column) cell = (1, 2);

        return (first + second + inferred + later) * scale
            + (int)point.X + (text?.Length ?? 0) + (maybe ?? 0) + cell.Row;
    }

    /// <summary>
    /// 9.2.9.2 hazard — discards. Every <c>_</c> below is a discard rather than a variable,
    /// and there are five of them in one method: none declares storage, and the last one is
    /// a real variable that shadows the discard form.
    /// </summary>
    public static int Discard()
    {
        // 9.2.9.2 — a standalone discard assignment.
        _ = VarValueParameters.SumAll(1, 2);

        // 9.2.9.2 — an out discard, which needs no variable to receive the value.
        _ = VarOutputParameters.Split("a b", out _, out _);

        // 9.2.9.2 — discards in a deconstruction.
        var (row, _) = TyTupleSpellings.Named;

        // 9.2.9.2 — a discard pattern in a switch expression (9.4.4.7).
        string classified = row switch
        {
            1 => "one",
            _ => "other",
        };

        // 9.2.9.2 — a lambda whose parameters are discarded (post-standard).
        System.Func<int, int, int> ignoreBoth = (_, _) => 0;

        return row + classified.Length + ignoreBoth(1, 2);
    }

    /// <summary>
    /// 9.2.9.2 — a local actually named <c>_</c>, which stops the discard form working in
    /// its scope. One name, and here it is storage after all.
    /// </summary>
    public static int UnderscoreAsVariable()
    {
        int _ = 5;
        return _;
    }
}
