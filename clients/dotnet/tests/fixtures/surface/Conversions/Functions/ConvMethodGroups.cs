using System;

namespace Surface.Conversions.Functions;

/// <summary>
/// A parser with three overloads. The overloads exist so that a method group conversion has a
/// choice to make: <c>ConvParsers.Parse</c> names all three, and the delegate type on the
/// other side of the conversion picks exactly one.
/// </summary>
public static class ConvParsers
{
    /// <summary>Clause 10.8 — the one-parameter overload.</summary>
    public static int Parse(string text) => text.Length;

    /// <summary>Clause 10.8 — the two-parameter overload, in the same group.</summary>
    public static int Parse(string text, int fallback) => text.Length + fallback;

    /// <summary>Clause 10.8 — an overload whose parameter type differs.</summary>
    public static int Parse(int value) => value;

    /// <summary>Clause 10.8 — a generic member, whose type argument the target infers.</summary>
    public static T Echo<T>(T value) => value;

    /// <summary>Clause 10.8 — a generic member whose return type is not its parameter's.</summary>
    public static string Render<T>(T value) => value?.ToString() ?? string.Empty;
}

/// <summary>An instance that can score, so that an instance method group has a receiver.</summary>
public sealed class ConvDispatcher
{
    private readonly int _bonus;

    /// <summary>A dispatcher that adds <paramref name="bonus"/> to every score.</summary>
    public ConvDispatcher(int bonus) => _bonus = bonus;

    /// <summary>Clause 10.8 — an instance member, whose group carries <c>this</c> with it.</summary>
    public int Score(string text) => text.Length + _bonus;

    /// <summary>Clause 10.8 — a static member of the same class, with only one overload.</summary>
    public static int StaticScore(string text) => text.Length;

    /// <summary>Clause 10.8 — a virtual member, whose group resolves at run time.</summary>
    public override string ToString() => $"dispatcher +{_bonus}";
}

/// <summary>
/// Clauses 10.8 and 10.2.15 — the method group conversions. A method group is not a value and
/// has no type; the conversion is the only thing that makes one usable, and it always resolves
/// to a single member. That resolution is the whole content of this class: every method below
/// is a reference to exactly one overload, and to nothing else in the group.
/// </summary>
public static class ConvMethodGroups
{
    /// <summary>Clause 10.8 — the group of three, resolved to <c>Parse(string)</c>.</summary>
    public static Func<string, int> OneParameter() => ConvParsers.Parse;

    /// <summary>Clause 10.8 — the same group, resolved to <c>Parse(string, int)</c>.</summary>
    public static Func<string, int, int> TwoParameters() => ConvParsers.Parse;

    /// <summary>Clause 10.8 — the same group again, resolved to <c>Parse(int)</c>.</summary>
    public static Func<int, int> IntOverload() => ConvParsers.Parse;

    /// <summary>Clause 10.8 — the same group, resolved against a named delegate type.</summary>
    public static ConvScorer ToNamedDelegate() => ConvParsers.Parse;

    /// <summary>Clause 10.8 — a generic group whose type argument is inferred from the target.</summary>
    public static Func<int, int> GenericInferred() => ConvParsers.Echo;

    /// <summary>Clause 10.8 — the same group with the type argument written out.</summary>
    public static Func<string, string> GenericExplicit() => ConvParsers.Echo<string>;

    /// <summary>Clause 10.8 — a conversion that is variance-compatible, not exact.</summary>
    public static ConvWidener VarianceCompatible() => ConvParsers.Render<string>;

    /// <summary>Clause 10.8 — a covariant return type, through <see cref="Func{T, TResult}"/>.</summary>
    public static Func<string, object> CovariantReturn() => ConvParsers.Render<string>;

    /// <summary>Clause 10.8 — an instance method group, which captures the receiver.</summary>
    public static Func<string, int> InstanceMethod(ConvDispatcher dispatcher) => dispatcher.Score;

    /// <summary>Clause 10.8 — a static method group named through its type.</summary>
    public static Func<string, int> StaticMethod() => ConvDispatcher.StaticScore;

    /// <summary>Clause 10.8 — a virtual method group, which resolves to the override at run time.</summary>
    public static Func<string> VirtualMethod(ConvDispatcher dispatcher) => dispatcher.ToString;

    /// <summary>Clause 10.8 — a method group's natural type, which needs the group unambiguous.</summary>
    public static Delegate NaturalType()
    {
        var scorer = ConvDispatcher.StaticScore;
        return scorer;
    }

    /// <summary>Clause 10.8 — a local function's group, converted inside the same body.</summary>
    public static ConvScorer LocalFunction()
    {
        int Score(string text) => text.Length;
        return Score;
    }

    /// <summary>Clause 10.2.15 — a group as an argument, where the parameter type is the target.</summary>
    public static int Apply() => Invoke(ConvParsers.Parse, "four");

    private static int Invoke(Func<string, int> scorer, string text) => scorer(text);
}
