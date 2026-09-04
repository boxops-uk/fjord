using System;
using System.Threading.Tasks;

namespace Surface.Expressions.Operators.Anonymous;

/// <summary>
/// The anonymous method expressions of 12.8.24 — the <c>delegate</c> form, which predates
/// lambdas and keeps one shape lambdas do not have: the parameter list may be omitted
/// entirely, in which case the anonymous function declares no parameters at all and is
/// still convertible to a delegate type that has some.
/// </summary>
public static class OpAnonymousMethods
{
    /// <summary>
    /// 12.8.24 — the omitted parameter list. The delegate type has one parameter and the
    /// anonymous method declares none, so there is nothing at all here for a parameter
    /// declaration to be recorded from.
    /// </summary>
    public static Action<int> OmittedParameterList() => delegate
    {
    };

    /// <summary>12.8.24 — an omitted parameter list on a delegate with a return value.</summary>
    public static Func<int, int> OmittedWithReturn() => delegate
    {
        return 11;
    };

    /// <summary>12.8.24 — an explicit parameter list, which an anonymous method must type.</summary>
    public static Func<int, int, int> ExplicitParameterList() => delegate(int left, int right)
    {
        return left + right;
    };

    /// <summary>12.8.24 — an empty explicit parameter list, which is not the same as omitting it.</summary>
    public static Action EmptyParameterList() => delegate()
    {
    };

    /// <summary>12.8.24 — an anonymous method with a by-reference parameter.</summary>
    public static OpRefMutator ByReferenceParameter() => delegate(ref int value)
    {
        value += 5;
    };

    /// <summary>12.8.24 — an anonymous method with a parameter array.</summary>
    public static OpParamsCounter VariadicParameter() => delegate(int[] values)
    {
        return values.Length;
    };

    /// <summary>12.8.24 — an async anonymous method.</summary>
    public static Func<Task<int>> AsyncAnonymousMethod() => async delegate
    {
        await Task.Yield();

        return 13;
    };

    /// <summary>
    /// 12.8.24 / 12.21.6.2 — an anonymous method that captures an outer variable, which is
    /// the same capture machinery a lambda uses.
    /// </summary>
    public static Func<int> Capturing(int seed)
    {
        int captured = seed;

        return delegate
        {
            captured++;

            return captured;
        };
    }

    /// <summary>
    /// 12.8.24 — two anonymous methods in one method whose parameters share a name, which
    /// is the anonymous-method half of the shape
    /// <see cref="OpLambdaExpressions.RepeatedParameterName"/> shows for lambdas.
    /// </summary>
    public static int RepeatedParameterName(int seed)
    {
        Func<int, int> first = delegate(int value)
        {
            return value + 1;
        };

        Func<int, int> second = delegate(int value)
        {
            return value + 2;
        };

        return first(seed) + second(seed);
    }

    /// <summary>
    /// 12.23.6 — an anonymous method used as an event handler, which is the position the
    /// form was introduced for.
    /// </summary>
    public static void SubscribedInline(OpAnonymousMethodEventSource source)
    {
        source.Fired += delegate(object? sender, EventArgs args)
        {
            _ = sender;
            _ = args;
        };
    }
}

/// <summary>An event source, so an anonymous method has something to subscribe to.</summary>
public sealed class OpAnonymousMethodEventSource
{
    /// <summary>Raised for the anonymous method above to handle.</summary>
    public event EventHandler? Fired;

    /// <summary>Raises the event.</summary>
    public void Raise() => Fired?.Invoke(this, EventArgs.Empty);
}
