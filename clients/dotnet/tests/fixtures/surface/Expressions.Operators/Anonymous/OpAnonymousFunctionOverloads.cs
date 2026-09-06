using System;

namespace Surface.Expressions.Operators.Anonymous;

/// <summary>
/// The overload resolution of 12.21.4: an anonymous function has no type of its own until
/// a parameter type gives it one, so the argument's shape — how many parameters, whether
/// the body produces a value — is what picks the overload.
/// </summary>
public static class OpAnonymousFunctionOverloads
{
    /// <summary>Takes something that does not produce a value.</summary>
    public static string Run(Action action)
    {
        action();

        return "action";
    }

    /// <summary>Takes something that produces an <c>int</c>.</summary>
    public static string Run(Func<int> function) => $"func:{function()}";

    /// <summary>Takes a one-parameter function over <c>int</c>.</summary>
    public static string Apply(Func<int, int> function) => $"int:{function(1)}";

    /// <summary>Takes a one-parameter function over <c>string</c>.</summary>
    public static string Apply(Func<string, int> function) => $"string:{function("x")}";

    /// <summary>
    /// 12.21.4 — the two <c>Run</c> declarations, selected by whether the lambda body is a
    /// statement or an expression that produces a value. Both call sites are
    /// <c>Run(() =&gt; ...)</c>.
    /// </summary>
    public static (string Statement, string Value) SelectedByBody() =>
        (Run(() => { }), Run(() => 4));

    /// <summary>
    /// 12.21.4 — the two <c>Apply</c> declarations, selected by the explicit type of the
    /// lambda's parameter. Neither call site names a delegate type.
    /// </summary>
    public static (string Integral, string Textual) SelectedByParameterType() =>
        (Apply((int value) => value), Apply((string value) => value.Length));

    /// <summary>
    /// 12.21.4 — an anonymous method in the same two positions, resolved the same way.
    /// </summary>
    public static (string Statement, string Value) AnonymousMethodOverloads() =>
        (Run(delegate { }), Run(delegate { return 5; }));

    /// <summary>
    /// 12.21.4 — a cast that supplies the target type directly, which is what a use has to
    /// fall back on when the overload set cannot decide.
    /// </summary>
    public static string CastSuppliesTheTarget() => Run((Func<int>)(() => 6));
}
