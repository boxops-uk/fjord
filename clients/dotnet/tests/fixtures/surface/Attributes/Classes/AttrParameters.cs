// Clause 23.2.3 (positional and named parameters) and 23.2.4 (attribute parameter types).
//
// 23.2.3 is where the two halves of an application point at different declarations. A
// positional argument is an argument to a *constructor* and binds to a constructor
// parameter; a named argument is an assignment to a public read-write *field or property*.
// So `[AttrDetail("audit", Level = 2)]` is one reference to a constructor, one to the
// parameter list it matched, and one to a property — three declarations from one
// bracketed clause, and only one of them is the attribute class.
//
// The hazard is deliberate: `AttrDetailAttribute` declares a constructor parameter `note`
// and a settable property `Note`. They differ only in case, they are both named at the
// same application site, and an identity string that is case-insensitive — or that is
// minted from the name without the kind — is one string for a parameter and a property.
// `Level` repeats the trick with a field rather than a property, and `Order` is named as
// an argument on two applications, which is two references to one property declaration.
//
// 23.2.4 fixes the closed list of legal parameter types. Every one of them appears below,
// as a constructor parameter and again as a named parameter, plus the four argument forms
// that are not simply a literal: an enum, a `typeof`, a `nameof`, and an array.
//
// Three of those four lose something on the way into metadata, which the built assembly
// says and the clause does not:
//
//   * **An enum argument is stored as its underlying integer.** `AttrSeverity.Stop`
//     becomes `2`, and the array `{ Note, Stop }` becomes `{ 0, 2 }`. The enum *type* is
//     retained; the reference to the enum *member* is not, so a metadata index finds no
//     use of `AttrSeverity.Stop` anywhere and a syntactic one finds three.
//   * **A `nameof` argument is stored as its string.** `nameof(Counted)` becomes
//     `"Counted"`, so the reference to the property is source-only in exactly the same way.
//   * **A constant expression is folded.** `Level = 1 + 2 * 3` becomes `7`.
//
// A `typeof` argument is the exception: it survives as a type reference, which is why the
// `Shapes` array below is worth having.

using System;

namespace Surface.Attributes.Classes;

/// <summary>23.2.4: a public enum type, which is a legal attribute parameter type.</summary>
public enum AttrSeverity
{
    /// <summary>Worth noting.</summary>
    Note = 0,

    /// <summary>Worth fixing.</summary>
    Warn = 1,

    /// <summary>Worth stopping for.</summary>
    Stop = 2,
}

/// <summary>
/// 23.2.3: one positional parameter and three named ones — two properties and a field.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class AttrDetailAttribute : Attribute
{
    /// <summary>23.2.3: the positional parameter, whose name is <c>note</c>.</summary>
    public AttrDetailAttribute(string note) => Note = note;

    /// <summary>
    /// 23.2.3: a named parameter differing from the constructor parameter only in case.
    /// </summary>
    public string Note { get; }

    /// <summary>23.2.3: a named parameter that is a public read-write field.</summary>
    public int Level;

    /// <summary>23.2.3: a named parameter that is a public read-write property.</summary>
    public AttrSeverity Order { get; set; }

    /// <summary>
    /// 23.2.3: read-only, so it is *not* a named parameter and no application may assign
    /// it — the one public property here that an application cannot reference.
    /// </summary>
    public bool Fixed => Level > 1;
}

/// <summary>
/// 23.2.4: every permitted attribute parameter type, once as a constructor parameter and
/// once as a named parameter.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class AttrParameterTypesAttribute : Attribute
{
    /// <summary>
    /// 23.2.4: the fifteen simple types the clause admits, positionally. Note the absent
    /// ones — <c>decimal</c>, <c>nint</c> and any struct — which cannot appear here at all.
    /// </summary>
    public AttrParameterTypesAttribute(
        bool flag,
        byte small,
        sbyte signedSmall,
        char letter,
        short shortish,
        ushort unsignedShortish,
        int number,
        uint unsignedNumber,
        long big,
        ulong unsignedBig,
        float single,
        double twice,
        string text,
        object boxed,
        Type shape,
        AttrSeverity severity)
    {
        Flag = flag;
        Small = small;
        SignedSmall = signedSmall;
        Letter = letter;
        Shortish = shortish;
        UnsignedShortish = unsignedShortish;
        Number = number;
        UnsignedNumber = unsignedNumber;
        Big = big;
        UnsignedBig = unsignedBig;
        Single = single;
        Twice = twice;
        Text = text;
        Boxed = boxed;
        Shape = shape;
        Severity = severity;
    }

    /// <summary>The positional values, read back.</summary>
    public bool Flag { get; }

    /// <summary>A byte.</summary>
    public byte Small { get; }

    /// <summary>An sbyte.</summary>
    public sbyte SignedSmall { get; }

    /// <summary>A char.</summary>
    public char Letter { get; }

    /// <summary>A short.</summary>
    public short Shortish { get; }

    /// <summary>A ushort.</summary>
    public ushort UnsignedShortish { get; }

    /// <summary>An int.</summary>
    public int Number { get; }

    /// <summary>A uint.</summary>
    public uint UnsignedNumber { get; }

    /// <summary>A long.</summary>
    public long Big { get; }

    /// <summary>A ulong.</summary>
    public ulong UnsignedBig { get; }

    /// <summary>A float.</summary>
    public float Single { get; }

    /// <summary>A double.</summary>
    public double Twice { get; }

    /// <summary>A string.</summary>
    public string Text { get; }

    /// <summary>An object, which any of the others boxes into.</summary>
    public object Boxed { get; }

    /// <summary>23.2.4: <c>System.Type</c>, whose argument is always a <c>typeof</c>.</summary>
    public Type Shape { get; }

    /// <summary>An enum type.</summary>
    public AttrSeverity Severity { get; }

    /// <summary>23.2.4: a single-dimensional array of a simple type.</summary>
    public int[]? Numbers { get; set; }

    /// <summary>23.2.4: a single-dimensional array of string.</summary>
    public string[]? Texts { get; set; }

    /// <summary>23.2.4: a single-dimensional array of <c>System.Type</c>.</summary>
    public Type[]? Shapes { get; set; }

    /// <summary>23.2.4: a single-dimensional array of an enum type.</summary>
    public AttrSeverity[]? Severities { get; set; }

    /// <summary>23.2.4: a single-dimensional array of object.</summary>
    public object[]? Boxes { get; set; }
}

/// <summary>
/// 23.2.3 and 23.2.4: the applications. Each bracketed clause names a constructor, its
/// parameters, and the properties and fields its named arguments assign.
/// </summary>
[AttrDetail("on the type", Level = 1, Order = AttrSeverity.Note)]
[AttrDetail("a second application, same named parameters", Level = 2, Order = AttrSeverity.Warn)]
[AttrParameterTypes(
    true,
    (byte)1,
    (sbyte)-1,
    'a',
    (short)-2,
    (ushort)2,
    3,
    4u,
    5L,
    6UL,
    7.5f,
    8.25d,
    "positional text",
    "a string, boxed into object",
    typeof(AttrDetailAttribute),
    AttrSeverity.Stop,
    Numbers = new[] { 1, 2, 3 },
    Texts = new[] { "one", "two" },
    Shapes = new[] { typeof(int), typeof(AttrSeverity), typeof(int[]) },
    Severities = new[] { AttrSeverity.Note, AttrSeverity.Stop },
    Boxes = new object[] { 1, "two", AttrSeverity.Warn, typeof(string) })]
public sealed class AttrArgumentForms
{
    /// <summary>
    /// 23.2.4: <c>nameof</c> is a constant string expression, so it is a legal argument —
    /// and it is a *reference* to the member it names, inside an attribute argument, which
    /// is a position an index is apt not to walk at all.
    /// </summary>
    [AttrDetail(nameof(Counted), Level = 3)]
    public int Counted { get; set; }

    /// <summary>23.2.4: a constant expression, folded before it ever reaches metadata.</summary>
    [AttrDetail("folded", Level = 1 + 2 * 3)]
    public int Folded { get; set; }

    /// <summary>
    /// 23.2.4: <c>nameof</c> of a type and of this method, plus a <c>typeof</c> of a type
    /// declared in another namespace of this project.
    /// </summary>
    [AttrDetail(nameof(AttrArgumentForms) + "." + nameof(Named))]
    [AttrParameterTypes(
        false,
        0,
        0,
        '\0',
        0,
        0,
        0,
        0u,
        0L,
        0UL,
        0f,
        0d,
        nameof(Named),
        null,
        typeof(Exceptions.AttrLedgerFault),
        AttrSeverity.Note,
        Shapes = new[] { typeof(Exceptions.AttrPostingFault) })]
    public void Named()
    {
    }
}
