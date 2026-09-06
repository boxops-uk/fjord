namespace Surface.Modern.Names;

/// <summary>An attribute that carries a name, so the <c>nameof</c>s below have somewhere to sit.</summary>
/// <param name="note">The name.</param>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class NoteAttribute(string note) : Attribute
{
    /// <summary>The name the attribute was given.</summary>
    public string Note => note;
}

/// <summary>
/// C# 11 — Extended nameof scope. A method's parameters and type parameters are in scope
/// inside an attribute *on* that method or on one of its parameters, which they were not
/// before. The names below therefore bind to declarations that, syntactically, come after
/// them — an attribute argument referring forward into the signature it is attached to.
/// </summary>
public static class NameofScope
{
    /// <summary>C# 11 — <c>nameof(parameter)</c> in an attribute on the method.</summary>
    [Note(nameof(input))]
    public static int Length(string input) => input.Length;

    /// <summary>C# 11 — <c>nameof(parameter)</c> in an attribute on the parameter itself.</summary>
    public static int Width([Note(nameof(input))] string input) => input.Length;

    /// <summary>C# 11 — <c>nameof(parameter)</c> in an attribute on the return value.</summary>
    [return: Note(nameof(input))]
    public static int Depth(string input) => input.Length;

    /// <summary>C# 11 — <c>nameof(T)</c> for a type parameter, in an attribute on the method.</summary>
    /// <typeparam name="TItem">The item type, named by the attribute above.</typeparam>
    [Note(nameof(TItem))]
    public static string Describe<TItem>(TItem value) => $"{nameof(TItem)}={value}";

    /// <summary>C# 11 — <c>nameof</c> of a type parameter in an attribute on a parameter.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    public static string DescribeParameter<TItem>([Note(nameof(TItem))] TItem value) => $"{value}";

    /// <summary>The pre-C# 11 position, where a parameter's name was a string literal.</summary>
    [Note("input")]
    public static int Literal(string input) => input.Length;
}

/// <summary>
/// C# 12 — Nameof accessing instance members. <c>nameof(Text.Length)</c> reads a member *of*
/// an instance member with no instance in hand: the expression is never evaluated, so the
/// receiver need not exist. What an index has to decide is whether that is a reference to
/// <c>Text</c>, to <c>Length</c>, to both, or to neither.
/// </summary>
public sealed class Reader
{
    /// <summary>The text, whose <c>Length</c> the constants below name.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>C# 12 — a constant naming a member of an instance property.</summary>
    public const string LengthMemberName = nameof(Text.Length);

    /// <summary>C# 12 — the same, qualified through the containing type.</summary>
    public const string QualifiedMemberName = nameof(Reader.Text.Length);

    /// <summary>C# 12 — a method group named through an instance member.</summary>
    public const string MethodName = nameof(Text.ToString);

    /// <summary>C# 12 — the same form in an attribute argument.</summary>
    [Note(nameof(Text.Length))]
    public int Count => Text.Length;

    /// <summary>The pre-C# 12 form: <c>nameof</c> of the member itself.</summary>
    public const string OwnName = nameof(Text);
}
