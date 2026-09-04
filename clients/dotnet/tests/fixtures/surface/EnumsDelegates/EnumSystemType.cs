// Clause 20.5 — the System.Enum type. System.Enum is the abstract base class of every enum
// type, and it is not itself an enum type: it is a class, derived from System.ValueType, so
// converting an enum value to it boxes. Every member System.Enum declares is available on
// every enum value in the corpus, and no enum declaration anywhere names it.

namespace Surface.EnumsDelegates;

/// <summary>
/// 20.5 — the inherited and static members of System.Enum, reached through enum values
/// declared in this project. The base class these calls resolve through is implicit in every
/// one of those declarations.
/// </summary>
public static class EdSystemEnumUse
{
    /// <summary>
    /// 20.5 hazard — the implicit base, made explicit at one site. The conversion is a
    /// boxing conversion to the same type that every enum in the corpus derives from, so the
    /// single token <c>Enum</c> here and the N implicit heritage edges must name one type.
    /// </summary>
    public static System.Enum AsBase(EdColor value) => value;

    /// <summary>20.5 — <c>ToString</c>, overridden by System.Enum to print the member name.</summary>
    public static string Name(EdColor value) => value.ToString();

    /// <summary>20.5 — the format-string overload, which prints a flags set as a list.</summary>
    public static string Formatted(EdAccess value) => value.ToString("G");

    /// <summary>20.5 — <c>Equals</c> and <c>GetHashCode</c>, inherited and overridden there.</summary>
    public static bool Same(EdColor left, object right) => left.Equals(right);

    /// <summary>20.5 — <c>GetHashCode</c> on an enum value.</summary>
    public static int Hash(EdColor value) => value.GetHashCode();

    /// <summary>20.5 — <c>CompareTo</c>, from the IComparable that System.Enum implements.</summary>
    public static int Order(EdColor left, EdColor right) => left.CompareTo(right);

    /// <summary>20.5 — <c>HasFlag</c>, which is why a flags enum needs no helper of its own.</summary>
    public static bool Allows(EdAccess value) => value.HasFlag(EdAccess.Read);

    /// <summary>20.5 — the underlying type, recovered at run time from the type object.</summary>
    public static System.Type Underlying() => System.Enum.GetUnderlyingType(typeof(EdColor));

    /// <summary>20.5 — every declared value of an enum, as values of the enum type.</summary>
    public static EdColor[] Values() => System.Enum.GetValues<EdColor>();

    /// <summary>
    /// 20.5 hazard — every declared *name*. <see cref="EdStrokeWeight"/> has three members
    /// and two of them share a value, so the array this returns is longer than the array
    /// <c>GetValues</c> returns distinct entries for.
    /// </summary>
    public static string[] Names() => System.Enum.GetNames<EdStrokeWeight>();

    /// <summary>20.5 — parsing a member out of its own name.</summary>
    public static EdColor Parse(string text) => System.Enum.Parse<EdColor>(text);

    /// <summary>20.5 — the non-throwing form, which declares its result in an out parameter.</summary>
    public static bool TryParse(string text, out EdColor value) =>
        System.Enum.TryParse(text, out value);

    /// <summary>20.5 — whether an underlying value corresponds to a declared member.</summary>
    public static bool Declared(int raw) => System.Enum.IsDefined(typeof(EdColor), raw);

    /// <summary>20.5 — boxing an underlying value into the enum type reflectively.</summary>
    public static object FromObject(int raw) => System.Enum.ToObject(typeof(EdColor), raw);

    /// <summary>
    /// 20.5 — System.Enum implements IConvertible and IFormattable, so an enum value converts
    /// to either interface. Neither interface is named by any enum declaration.
    /// </summary>
    public static System.IConvertible AsConvertible(EdColor value) => value;

    /// <summary>20.5 — the IFormattable side of the same boxing conversion.</summary>
    public static System.IFormattable AsFormattable(EdAccess value) => value;

    /// <summary>
    /// 20.5 hazard — <c>System.Enum</c> as a type parameter constraint. This is the one
    /// position where the base class of every enum is written as a bound rather than
    /// inherited, and the type it names is the same one <see cref="AsBase"/> converts to.
    /// </summary>
    public static bool IsZero<T>(T value)
        where T : struct, System.Enum
        => value.Equals(default(T));

    /// <summary>20.5 — a second constrained method, calling an inherited member generically.</summary>
    public static string Describe<T>(T value)
        where T : struct, System.Enum
        => value.ToString();

    /// <summary>20.5 — the constrained methods instantiated at two of the project's enums.</summary>
    public static string Both() => Describe(EdColor.Blue) + Describe(EdAccess.ReadWrite);
}
