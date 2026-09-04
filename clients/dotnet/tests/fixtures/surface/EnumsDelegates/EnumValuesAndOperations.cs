// Clause 20.6 — enum values and operations. Each enum type defines a distinct type whose set
// of values is the set of values of its underlying type; the predefined operators `==`, `!=`,
// `<`, `>`, `<=`, `>=`, `+`, `-`, `^`, `&`, `|`, `~`, `++`, `--` and `sizeof` are available
// on it; and explicit conversions exist between an enum and every integral type and between
// any two enum types. None of these operators is declared anywhere: the language supplies
// them per enum type, so an operator use site here has no declaration to point at.

namespace Surface.EnumsDelegates;

/// <summary>20.6 — every predefined operation on an enum type, one method each.</summary>
public static class EdEnumOperations
{
    /// <summary>20.6 hazard — equality, which binds to an operator no source declares.</summary>
    public static bool Equal(EdColor left, EdColor right) => left == right;

    /// <summary>20.6 — inequality.</summary>
    public static bool Different(EdColor left, EdColor right) => left != right;

    /// <summary>20.6 — the four relational operators, in declaration order of the clause.</summary>
    public static bool Ordered(EdVoltage left, EdVoltage right) =>
        left < right && left <= right && right > left && right >= left;

    /// <summary>20.6 — bitwise or, the operation a flags enum exists for.</summary>
    public static EdAccess Or(EdAccess left, EdAccess right) => left | right;

    /// <summary>20.6 — bitwise and.</summary>
    public static EdAccess And(EdAccess left, EdAccess right) => left & right;

    /// <summary>20.6 — bitwise exclusive or.</summary>
    public static EdAccess Xor(EdAccess left, EdAccess right) => left ^ right;

    /// <summary>20.6 — the complement, whose result is usually no declared member.</summary>
    public static EdAccess Complement(EdAccess value) => ~value;

    /// <summary>20.6 — enum plus underlying, whose result is an enum.</summary>
    public static EdColor Next(EdColor value) => value + 1;

    /// <summary>20.6 — underlying plus enum, the same operator with its operands swapped.</summary>
    public static EdColor Shifted(EdColor value) => 1 + value;

    /// <summary>20.6 — enum minus underlying.</summary>
    public static EdColor Previous(EdColor value) => value - 1;

    /// <summary>20.6 — enum minus enum, whose result is the *underlying* type, not the enum.</summary>
    public static int Distance(EdColor left, EdColor right) => left - right;

    /// <summary>20.6 — increment, which is the compound form of enum plus one.</summary>
    public static EdColor Increment(EdColor value)
    {
        value++;
        return value;
    }

    /// <summary>20.6 — decrement.</summary>
    public static EdColor Decrement(EdColor value)
    {
        --value;
        return value;
    }

    /// <summary>20.6 — compound assignment through a bitwise operator.</summary>
    public static EdAccess Grant(EdAccess value)
    {
        value |= EdAccess.Write;
        value &= ~EdAccess.Append;
        return value;
    }

    /// <summary>20.6 — the size of an enum's storage, which is its underlying type's size.</summary>
    public static int Size() => sizeof(EdColor) + sizeof(EdBaseU64);

    /// <summary>20.6 — the explicit conversion out of an enum type.</summary>
    public static int ToUnderlying(EdColor value) => (int)value;

    /// <summary>20.6 — the explicit conversion into an enum type, from any integral type.</summary>
    public static EdColor FromUnderlying(int raw) => (EdColor)raw;

    /// <summary>
    /// 20.6 hazard — the conversion into an enum from a value no member declares. The result
    /// is a value of <c>EdColor</c> that is not <c>Red</c>, <c>Green</c> or <c>Blue</c>.
    /// </summary>
    public static EdColor Undeclared() => (EdColor)97;

    /// <summary>20.6 — a narrowing conversion out of an enum, in a checked context.</summary>
    public static byte Narrow(EdBaseI64 value) => checked((byte)(long)value);

    /// <summary>20.6 — the same narrowing where wrapping is the intent.</summary>
    public static byte Wrap(EdBaseU64 value) => unchecked((byte)value);

    /// <summary>
    /// 20.6 hazard — the explicit conversion between two *enum* types. One cast token, and
    /// both enums are named; the conversion itself is declared by neither of them.
    /// </summary>
    public static EdBearing Across(EdColor value) => (EdBearing)value;

    /// <summary>20.6 — an enum-typed constant, folded into every site that reads it.</summary>
    public const EdColor DefaultColor = EdColor.Green;

    /// <summary>20.6 — a constant expression over enum members, folded the same way.</summary>
    public const EdAccess DefaultAccess = EdAccess.Read | EdAccess.Append;

    /// <summary>20.6 hazard — a read of a folded constant. The value arrives, the reference may not.</summary>
    public static EdColor ReadConstant() => DefaultColor;

    /// <summary>20.6 — an enum member as a default argument, which is a constant position.</summary>
    public static string WithDefault(EdColor value = EdColor.Blue) => value.ToString();

    /// <summary>20.6 — the call that supplies no argument, so the default's member is used.</summary>
    public static string Defaulted() => WithDefault();

    /// <summary>20.6 — an enum as the governing type of a switch statement, one case per member.</summary>
    public static int Rank(EdBearing heading)
    {
        switch (heading)
        {
            case EdBearing.North:
                return 0;
            case EdBearing.East:
                return 1;
            case EdBearing.South:
                return 2;
            case EdBearing.West:
                return 3;
            default:
                return -1;
        }
    }

    /// <summary>20.6 — the same enum in a switch expression with constant patterns.</summary>
    public static string Describe(EdBearing heading) => heading switch
    {
        EdBearing.North => "up",
        EdBearing.East => "right",
        EdBearing.South => "down",
        EdBearing.West => "left",
        _ => "nowhere",
    };

    /// <summary>
    /// 20.6 hazard — the two members that share the value 1, in one switch. Only one of them
    /// can be a case label, because the labels are compared by value.
    /// </summary>
    public static bool IsThin(EdStrokeWeight weight) => weight switch
    {
        EdStrokeWeight.Thin => true,
        EdStrokeWeight.Thick => false,
        _ => false,
    };
}

/// <summary>
/// 20.6 — an attribute with enum-typed arguments, which is the strictest constant position
/// the language has: the arguments are encoded into metadata, and the member names that
/// produced them survive only as numbers.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
public sealed class EdMarkAttribute : System.Attribute
{
    /// <summary>20.6 — the positional parameter, typed by an enum declared in this project.</summary>
    public EdMarkAttribute(EdColor color) => Color = color;

    /// <summary>20.6 — the value the positional argument was written with.</summary>
    public EdColor Color { get; }

    /// <summary>20.6 — a settable property, so a named argument can carry a flags expression.</summary>
    public EdAccess Required { get; init; }
}

/// <summary>
/// 20.6 hazard — the attribute applied. <c>EdMark</c> and <c>EdMarkAttribute</c> are two
/// spellings of one type and must merge; <c>EdColor.Blue</c> and the two flags members are
/// references that constant folding erases.
/// </summary>
[EdMark(EdColor.Blue, Required = EdAccess.Read | EdAccess.Write)]
public sealed class EdMarked
{
    /// <summary>20.6 — the same attribute on a method, with the long spelling of its name.</summary>
    [EdMarkAttribute(EdColor.Red)]
    public static int Tagged() => 1;
}
