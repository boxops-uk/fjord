using System;
using System.Threading.Tasks;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The await expressions of 12.9.9: an awaitable found by the pattern on the type, one
/// found by an extension method, and the framework's own.
/// </summary>
public static class OpAwaitUses
{
    /// <summary>
    /// 12.9.9.2 / 12.9.9.4 — awaiting a user-defined awaitable. The <c>await</c> reaches
    /// <c>GetAwaiter</c>, <c>IsCompleted</c>, <c>OnCompleted</c> and <c>GetResult</c>, and
    /// names none of them.
    /// </summary>
    public static async Task<int> AwaitUserDefined(OpAwaitable awaitable)
    {
        int result = await awaitable;

        return result;
    }

    /// <summary>
    /// 12.9.9.2 — awaiting a type whose <c>GetAwaiter</c> is an extension method, so the
    /// pattern is satisfied from outside the awaited type entirely.
    /// </summary>
    public static async Task<int> AwaitThroughExtension(OpBorrowedAwaitable awaitable) =>
        await awaitable;

    /// <summary>
    /// 12.9.9.4 — an await as an operand of another operator, so the operator's evaluation
    /// is suspended in the middle.
    /// </summary>
    public static async Task<int> AwaitAsOperand(OpAwaitable left, OpAwaitable right) =>
        await left + await right;

    /// <summary>12.9.9.2 — the framework awaitables, for the contrast.</summary>
    public static async Task<string> AwaitFrameworkAwaitables()
    {
        await Task.Yield();
        int number = await Task.FromResult(7);
        await Task.CompletedTask;

        return number.ToString();
    }

    /// <summary>
    /// 12.9.9.2 — an async lambda (12.21.1) whose body contains an await, which makes the
    /// anonymous function itself the enclosing scope of the suspension.
    /// </summary>
    public static Func<OpAwaitable, Task<int>> AsyncLambda() =>
        async awaitable => await awaitable + 1;
}
