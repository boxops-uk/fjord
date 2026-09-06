using System;

namespace Surface.SyntaxForms.Expressions;

// The three function-valued expression kinds: `delegate (…) { … }`, `x => …` and
// `(x, y) => …`.
//
// Each declares a method — `MethodKind.AnonymousFunction`, which Roslyn also spells
// `LambdaMethod` — and none of them is one of the six bases the walk's declaration switch
// names, so every one of these methods is a declaration the index holds nothing about. What
// it does hold is the *body*: a lambda's captures and calls are ordinary references, so the
// index records edges out of a method that has no definition to be out of.

/// <summary>Every anonymous-function form, in the smallest expression that needs it.</summary>
public static class SfLambdaForms
{
    /// <summary>A delegate type with two parameters, so the parenthesized forms have a target.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Something derived from both.</returns>
    public delegate int SfCombiner(int left, int right);

    /// <summary>
    /// AnonymousMethodExpression — the C# 2 spelling, in both of its shapes: with a
    /// parameter list, and with none at all, which is the only anonymous function in C# that
    /// can omit its parameters.
    /// </summary>
    /// <returns>A fold of what they computed.</returns>
    public static int AnonymousMethods()
    {
        Func<int, int> negate = delegate(int value) { return -value; };
        SfCombiner add = delegate(int left, int right) { return left + right; };

        // No parameter list: legal only for an anonymous method, and it still binds to a
        // delegate whose Invoke takes two arguments.
        SfCombiner ignored = delegate { return 0; };

        // `static`, so the body may not capture — a modifier on an anonymous method as well
        // as on a lambda.
        Func<int, int> doubled = static delegate(int value) { return value * 2; };

        return negate(1) + add(2, 3) + ignored(4, 5) + doubled(6);
    }

    /// <summary>
    /// SimpleLambdaExpression — one parameter, no parentheses — with an expression body, a
    /// block body, and the <c>static</c> modifier.
    /// </summary>
    /// <returns>A fold of what they computed.</returns>
    public static int SimpleLambdas()
    {
        Func<int, int> expression = value => value + 1;
        Func<int, int> block = value => { return value + 2; };
        Func<int, int> uncapturing = static value => value + 3;

        var captured = 4;
        Func<int, int> capturing = value => value + captured;

        return expression(1) + block(1) + uncapturing(1) + capturing(1);
    }

    /// <summary>
    /// ParenthesizedLambdaExpression in every shape the grammar allows: no parameters,
    /// implicitly typed parameters, explicitly typed parameters, a declared return type, a
    /// default parameter value, a <c>ref</c> parameter, an attribute on a parameter, and
    /// <c>static</c>.
    /// </summary>
    /// <returns>A fold of what they computed.</returns>
    public static int ParenthesizedLambdas()
    {
        Func<int> none = () => 1;
        SfCombiner implicitlyTyped = (left, right) => left + right;
        SfCombiner explicitlyTyped = (int left, int right) => left * right;
        var withReturnType = int (int left, int right) => left - right;
        var withDefault = (int left, int right = 2) => left + right;
        var withAttribute = ([System.Diagnostics.CodeAnalysis.AllowNull] string? text) => text?.Length ?? 0;
        SfCombiner uncapturing = static (left, right) => left ^ right;

        return none() + implicitlyTyped(1, 2) + explicitlyTyped(2, 3)
            + withReturnType(5, 1) + withDefault(1) + withAttribute(null) + uncapturing(1, 2);
    }

    /// <summary>
    /// A lambda in the two positions where its own body declares things the walk sees:
    /// a local function inside it, and a nested lambda.
    /// </summary>
    /// <returns>What the nesting computed.</returns>
    public static int Nested()
    {
        Func<int, int> outer = value =>
        {
            // A local function declared inside a lambda: a `LocalFunctionStatementSyntax`
            // whose containing method has no definition in the index, so the index holds an
            // edge out of nothing.
            int Inner(int inner) => inner + 1;

            Func<int, int> innermost = deeper => Inner(deeper) * 2;
            return innermost(value);
        };

        return outer(1);
    }
}
