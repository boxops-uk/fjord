using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Anonymous;

/// <summary>
/// The lambda expressions of 12.21: every signature form of 12.21.2, both body forms of
/// 12.21.3, and the parameters they declare inside an expression.
/// </summary>
/// <remarks>
/// A lambda parameter is a declaration whose containing declaration is the member the
/// lambda sits in, not a member of its own. <see cref="RepeatedParameterName"/> is the
/// shape that matters: two lambdas in one method whose parameters share a name, so an
/// identity minted from (containing member, parameter name) has two declarations to hold.
/// </remarks>
public static class OpLambdaExpressions
{
    /// <summary>
    /// 12.21.2 — an implicitly typed single parameter, with and without parentheses, and
    /// an implicitly typed parameter list.
    /// </summary>
    public static (Func<int, int> Bare, Func<int, int> Parenthesised, Func<int, int, int> Pair) Implicit()
    {
        Func<int, int> bare = value => value + 1;
        Func<int, int> parenthesised = (value) => value + 2;
        Func<int, int, int> pair = (left, right) => left + right;

        return (bare, parenthesised, pair);
    }

    /// <summary>12.21.2 — explicitly typed parameters, and an empty parameter list.</summary>
    public static (Func<int, int> Typed, Func<int> Empty, Func<OpMoney, long> UserDefined) Explicit()
    {
        Func<int, int> typed = (int value) => value * 2;
        Func<int> empty = () => 7;
        Func<OpMoney, long> userDefined = (OpMoney money) => money.Minor;

        return (typed, empty, userDefined);
    }

    /// <summary>
    /// 12.21.2 — the by-reference parameter modifiers. Each one needs a delegate type to
    /// convert to, because no <c>Func</c> or <c>Action</c> has such a parameter.
    /// </summary>
    public static (OpRefMutator Mutate, OpOutProducer Produce, OpInReader Read, OpRefReadonlyReader Peek) ByReference()
    {
        OpRefMutator mutate = (ref int value) => value++;
        OpOutProducer produce = (out int value) =>
        {
            value = 3;

            return true;
        };
        OpInReader read = (in int value) => value;
        OpRefReadonlyReader peek = (ref readonly int value) => value;

        return (mutate, produce, read, peek);
    }

    /// <summary>
    /// 12.21.2 — a parameter array and a default argument, both of which a lambda may
    /// declare since C# 12.
    /// </summary>
    public static (OpParamsCounter Count, OpDefaultedScale Scale) OptionalAndVariadic()
    {
        OpParamsCounter count = (params int[] values) => values.Length;
        OpDefaultedScale scale = (int factor = 2) => factor * 10;

        return (count, scale);
    }

    /// <summary>
    /// 12.21.2 — a lambda with an explicit return type, one with attributes on itself and
    /// on its parameter, and one that is <c>static</c> and so captures nothing.
    /// </summary>
    public static (Func<int, long> Widened, Func<int, int> Marked, Func<int, int> Pure) Annotated()
    {
        var widened = long (int value) => value;
        var marked = [OpLambdaMarker] ([OpLambdaMarker] int value) => value + 1;
        var pure = static (int value) => value * 3;

        return (widened, marked, pure);
    }

    /// <summary>
    /// 12.21.2 — discarded parameters, which are declarations whose name is the discard
    /// and so bind to nothing at all.
    /// </summary>
    public static Func<int, int, int> Discards() => (_, _) => 0;

    /// <summary>
    /// 12.21.3 — the two body forms: an expression body, and a block body with statements
    /// and a local of its own.
    /// </summary>
    public static (Func<int, int> Expression, Func<int, int> Block) Bodies()
    {
        Func<int, int> expression = value => value * value;
        Func<int, int> block = value =>
        {
            int doubled = value * 2;

            return doubled - 1;
        };

        return (expression, block);
    }

    /// <summary>
    /// 12.21.3 — a lambda whose body returns by reference, so the body is a ref expression
    /// (12.23.4's neighbour) rather than a value.
    /// </summary>
    public static OpRefSelector RefReturningBody() => (items, index) => ref items[index];

    /// <summary>
    /// 12.21.1 — nested lambdas, where the inner one's body refers to the outer one's
    /// parameter, so one expression contains two parameter scopes.
    /// </summary>
    public static Func<int, Func<int, int>> Nested() => outer => inner => outer + inner;

    /// <summary>
    /// 12.21.2 — four lambdas in one method, all declaring a parameter named
    /// <c>value</c>. They are four distinct declarations in one containing member, told
    /// apart only by which lambda they belong to.
    /// </summary>
    public static int RepeatedParameterName(int seed)
    {
        Func<int, int> first = value => value + 1;
        Func<int, int> second = (int value) => value + 2;
        Func<int, int> third = static value => value + 3;
        Func<int, int> fourth = value =>
        {
            int value2 = value + 4;

            return value2;
        };

        return first(seed) + second(seed) + third(seed) + fourth(seed);
    }

    /// <summary>
    /// 12.21.1 — a lambda in a field initializer and a lambda in a property initializer,
    /// whose containing declaration is a field rather than a method.
    /// </summary>
    public static readonly Func<int, int> InitializerLambda = value => value - 1;

    /// <summary>A property whose initializer is a lambda.</summary>
    public static Func<int, int> InitializedProperty { get; } = value => value - 2;
}
