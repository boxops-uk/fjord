using System.Runtime.CompilerServices;

namespace Surface.Modern.Attributes;

/// <summary>C# 10 — CallerArgumentExpression attribute.</summary>
public static class CallerExpressions
{
    /// <summary>
    /// C# 10 — <c>[CallerArgumentExpression]</c>: the parameter it names is filled in by the
    /// compiler with the *source text* of the argument passed for another parameter. The
    /// attribute argument is a string that has to resolve to a parameter of the same method,
    /// which is the interesting reference — a name in an attribute that binds to a parameter.
    /// </summary>
    public static void Require(
        bool condition,
        [CallerArgumentExpression(nameof(condition))] string? conditionText = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"{conditionText} at {path}:{line} in {member}");
        }
    }

    /// <summary>Calls it, so the compiler has to synthesize the argument.</summary>
    public static void Check(int count) => Require(count > 0);
}
