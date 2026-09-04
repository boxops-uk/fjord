using System;
using System.Collections.Generic;

namespace Surface.Conversions.Functions;

/// <summary>Scores a piece of text. The delegate type clause 10.7 converts lambdas into.</summary>
/// <param name="text">What to score.</param>
public delegate int ConvScorer(string text);

/// <summary>
/// A delegate whose parameter is narrower and whose return is wider than
/// <see cref="ConvScorer"/>'s, so that a variance-compatible conversion has a target.
/// </summary>
/// <param name="text">What to widen.</param>
public delegate object ConvWidener(string text);

/// <summary>Takes a reference. A lambda with a <c>ref</c> parameter needs somewhere to go.</summary>
/// <param name="total">Added to.</param>
public delegate void ConvAccumulator(ref int total);

/// <summary>
/// Clauses 10.7.1, 10.7.2 and 10.2.15 — the anonymous function conversions. An anonymous
/// function has no type of its own, so every one of these is a conversion and not an
/// assignment of a value; the delegate type on the left is what gives the lambda's parameters
/// their types.
///
/// The deliberate hazard is <see cref="Several"/>: four function bodies inside one member,
/// three of them anonymous and therefore nameless in the source. Whatever an index calls
/// them, it has to call them four different things.
/// </summary>
public static class ConvAnonymousFunctions
{
    /// <summary>Clause 10.7.1 — an implicitly typed lambda with one parameter.</summary>
    public static ConvScorer ImplicitLambda() => text => text.Length;

    /// <summary>Clause 10.7.1 — the same, with the parameter list parenthesised.</summary>
    public static ConvScorer ParenthesisedLambda() => (text) => text.Length;

    /// <summary>Clause 10.7.1 — an explicitly typed lambda.</summary>
    public static ConvScorer TypedLambda() => (string text) => text.Length;

    /// <summary>Clause 10.7.1 — a statement-bodied lambda.</summary>
    public static ConvScorer StatementLambda() => text =>
    {
        int length = text.Length;
        return length;
    };

    /// <summary>Clause 10.7.1 — an anonymous method, which is the older syntax.</summary>
    public static ConvScorer AnonymousMethod() => delegate (string text) { return text.Length; };

    /// <summary>Clause 10.7.1 — an anonymous method with no parameter list at all.</summary>
    public static ConvScorer ParameterlessAnonymousMethod() => delegate { return 0; };

    /// <summary>Clause 10.7.1 — a <c>static</c> lambda, which cannot capture.</summary>
    public static ConvScorer StaticLambda() => static text => text.Length;

    /// <summary>Clause 10.7.1 — a lambda with an explicit return type.</summary>
    public static Func<int, int> ExplicitReturnType() => int (int value) => value + 1;

    /// <summary>Clause 10.7.1 — a lambda with a default parameter value.</summary>
    public static Func<int, int> DefaultedParameter()
    {
        var increment = (int value, int step = 1) => value + step;
        return value => increment(value);
    }

    /// <summary>Clause 10.7.1 — a lambda with a <c>ref</c> parameter, which needs the type stated.</summary>
    public static ConvAccumulator RefParameter() => static (ref int total) => total += 1;

    /// <summary>Clause 10.7.1 — a lambda whose parameters are discarded.</summary>
    public static Func<int, int, int> Discards() => (_, _) => 0;

    /// <summary>Clause 10.7.1 — a lambda converted to <see cref="Action"/>, returning nothing.</summary>
    public static Action Nothing() => () => { };

    /// <summary>Clause 10.7.2 — a lambda that captures a parameter, so a closure is built.</summary>
    public static ConvScorer Capturing(int bonus) => text => text.Length + bonus;

    /// <summary>Clause 10.7.2 — a lambda that captures nothing, so the closure is cached.</summary>
    public static ConvScorer NotCapturing() => text => text.Length;

    /// <summary>Clause 10.7.2 — an <c>async</c> lambda, whose conversion also builds a state machine.</summary>
    public static Func<string, System.Threading.Tasks.Task<int>> AsyncLambda() =>
        async text =>
        {
            await System.Threading.Tasks.Task.Yield();
            return text.Length;
        };

    /// <summary>Clause 10.7.2 — a lambda's natural type, inferred rather than given.</summary>
    public static Func<string, int> NaturalType()
    {
        var scorer = (string text) => text.Length;
        return scorer;
    }

    /// <summary>Clause 10.2.15 — the same lambda text converted to a second delegate type.</summary>
    public static ConvWidener ToWidener() => text => text.Length;

    /// <summary>
    /// Clauses 10.7.1 and 10.8, hazard — three anonymous functions and one local function in
    /// a single member body. The local function is named and is reached by a method group
    /// conversion; the other three have no name in the source at all.
    ///
    /// The return type is an array rather than <see cref="IReadOnlyList{T}"/> on purpose: a
    /// collection expression targeting a read-only interface makes the compiler synthesise a
    /// helper type carrying three indexers, and a synthesised type is not this project's to
    /// put into the corpus.
    /// </summary>
    public static ConvScorer[] Several()
    {
        ConvScorer first = text => text.Length;
        ConvScorer second = text => text.Length * 2;
        ConvScorer third = delegate (string text) { return text.Length * 3; };

        int Fourth(string text) => text.Length * 4;

        return [first, second, third, Fourth];
    }
}
