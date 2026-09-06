using System;

namespace Surface.Preview;

/// <summary>A delegate with an <c>out</c> parameter, which the lambdas below implement.</summary>
/// <param name="text">What to read.</param>
/// <param name="value">What was read.</param>
/// <returns>Whether the read succeeded.</returns>
public delegate bool TryRead(string text, out int value);

/// <summary>A delegate with a <c>ref</c> parameter.</summary>
/// <param name="total">Accumulated into.</param>
/// <param name="amount">How much.</param>
public delegate void Accumulate(ref int total, int amount);

/// <summary>A delegate with an <c>in</c> parameter and a <c>ref readonly</c> one.</summary>
/// <param name="left">Read only.</param>
/// <param name="right">Read only, by reference.</param>
/// <returns>The comparison.</returns>
public delegate bool CompareBoth(in int left, ref readonly int right);

/// <summary>
/// C# 14 — Modifiers on simple lambda parameters. A lambda parameter could carry <c>ref</c>,
/// <c>out</c> or <c>in</c> only when it also carried an explicit type; C# 14 allows the
/// modifier with the type inferred. So the parameter's *ref kind* is written and its type is
/// not, which is a combination no other declaration in the language has.
/// </summary>
public static class LambdaParameterModifiers
{
    /// <summary>C# 14 — <c>out</c> on a simple lambda parameter.</summary>
    public static readonly TryRead Reader = (text, out value) =>
    {
        value = text.Length;

        return value > 0;
    };

    /// <summary>C# 14 — <c>ref</c> on a simple lambda parameter.</summary>
    public static readonly Accumulate Accumulator = (ref total, amount) => total += amount;

    /// <summary>C# 14 — <c>in</c> and <c>ref readonly</c> on simple lambda parameters.</summary>
    public static readonly CompareBoth Comparer = (in left, ref readonly right) => left > right;

    /// <summary>The pre-C# 14 spelling, which needs every type written out.</summary>
    public static readonly TryRead TypedReader = (string text, out int value) =>
    {
        value = text.Length;

        return value > 0;
    };

    /// <summary>Invokes each of them.</summary>
    public static string All()
    {
        var read = Reader("abcd", out var length);
        var typed = TypedReader("ab", out var shorter);

        var total = 1;
        Accumulator(ref total, 5);

        var left = 3;
        var right = 2;

        return $"{read}{length}{typed}{shorter}{total}{Comparer(in left, in right)}";
    }
}
