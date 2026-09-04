using System;
using System.Linq.Expressions;
using Surface.Conversions.UserDefined;

namespace Surface.Conversions.Functions;

/// <summary>
/// Clause 10.7.3 — the conversion of a lambda expression to an expression tree type. It is the
/// same conversion as 10.7.2's up to the point where code is emitted: instead of a method, the
/// compiler emits calls that build a tree describing the lambda. Only expression-bodied
/// lambdas convert, which is why nothing here has a block body.
/// </summary>
public static class ConvExpressionTrees
{
    /// <summary>Clause 10.7.3 — an arithmetic lambda, as a tree.</summary>
    public static Expression<Func<int, int>> Increment() => value => value + 1;

    /// <summary>Clause 10.7.3 — a property access, which becomes a <see cref="MemberExpression"/>.</summary>
    public static Expression<Func<string, int>> Length() => text => text.Length;

    /// <summary>Clause 10.7.3 — two parameters, implicitly typed from the target.</summary>
    public static Expression<Func<int, int, int>> Sum() => (left, right) => left + right;

    /// <summary>Clause 10.7.3 — explicitly typed parameters, which convert the same way.</summary>
    public static Expression<Func<int, bool>> Positive() => (int value) => value > 0;

    /// <summary>Clause 10.7.3 — a method call inside the tree.</summary>
    public static Expression<Func<string, string>> Upper() => text => text.ToUpperInvariant();

    /// <summary>Clause 10.7.3 — an <c>Action</c>-shaped tree, whose body is a statement expression.</summary>
    public static Expression<Action<int>> Report() => value => Console.Write(value);

    /// <summary>
    /// Clause 10.7.3 — a user-defined conversion inside a tree, which becomes a
    /// <see cref="UnaryExpression"/> naming the operator rather than emitting a call.
    /// </summary>
    public static Expression<Func<ConvReading, ConvCelsius>> ToCelsius() => reading => reading;

    /// <summary>Clause 10.7.3 — an explicit user-defined conversion in a tree.</summary>
    public static Expression<Func<ConvReading, ConvKelvin>> ToKelvin() =>
        reading => (ConvKelvin)reading;

    /// <summary>Clause 10.7.3 — a boxing conversion in a tree (clause 10.2.9 under 10.7.3).</summary>
    public static Expression<Func<int, object>> Box() => value => value;

    /// <summary>Clause 10.7.3 — the conversion applied through a cast, giving the untyped form.</summary>
    public static LambdaExpression Untyped() => (Expression<Func<int, bool>>)(value => value > 0);

    /// <summary>Clause 10.7.3 — an object creation, so the tree holds a constructor reference.</summary>
    public static Expression<Func<double, ConvReading>> Make() => celsius => new ConvReading(celsius);

    /// <summary>Clause 10.7.3 — a lambda with no parameters, whose tree has an empty list.</summary>
    public static Expression<Func<int>> Zero() => () => 0;
}
