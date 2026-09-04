using System;
using System.Runtime.CompilerServices;

namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// The awaiter half of the await pattern: <c>IsCompleted</c>, <c>OnCompleted</c> and
/// <c>GetResult</c>, reached by 12.9.9.2 from an <c>await</c> that names none of them.
/// </summary>
/// <remarks>
/// Clause 12.9.9.2 calls an expression awaitable when <c>GetAwaiter()</c> is available on
/// it and the result implements <see cref="INotifyCompletion"/>, has an accessible
/// readable <c>IsCompleted</c> of type <c>bool</c> and an accessible <c>GetResult</c>.
/// The awaiter is found structurally, so the three members below are referenced by the
/// six characters <c>await</c> plus a space.
/// </remarks>
public readonly struct OpAwaiter : INotifyCompletion
{
    private readonly int _result;

    /// <summary>Constructs an awaiter over an already-known result.</summary>
    public OpAwaiter(int result) => _result = result;

    /// <summary>Whether the operation has already finished; always true here.</summary>
    public bool IsCompleted => true;

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => continuation();

    /// <summary>The result the <c>await</c> expression evaluates to (12.9.9.4).</summary>
    public int GetResult() => _result;
}

/// <summary>An awaitable whose <c>GetAwaiter</c> is an instance method.</summary>
public readonly struct OpAwaitable
{
    private readonly int _result;

    /// <summary>Constructs an awaitable over an already-known result.</summary>
    public OpAwaitable(int result) => _result = result;

    /// <summary>The pattern member 12.9.9.2 looks for.</summary>
    public OpAwaiter GetAwaiter() => new(_result);
}

/// <summary>An awaitable with no members at all.</summary>
/// <remarks>
/// Clause 12.9.9.2 admits an extension <c>GetAwaiter</c>, so this type is awaitable
/// entirely from outside itself — see <see cref="OpAwaitableExtensions"/>.
/// </remarks>
public readonly struct OpBorrowedAwaitable
{
    /// <summary>Constructs the awaitable.</summary>
    public OpBorrowedAwaitable(int result) => Result = result;

    /// <summary>The value the extension awaiter hands back.</summary>
    public int Result { get; }
}

/// <summary>The extension <c>GetAwaiter</c> that makes <see cref="OpBorrowedAwaitable"/> awaitable.</summary>
public static class OpAwaitableExtensions
{
    /// <summary>Found by extension member lookup, not by member lookup on the type.</summary>
    public static OpAwaiter GetAwaiter(this OpBorrowedAwaitable value) => new(value.Result);
}
