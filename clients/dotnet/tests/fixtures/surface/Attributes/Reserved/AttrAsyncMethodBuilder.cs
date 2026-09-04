// Clause 23.5.5 (the AsyncMethodBuilder attribute).
//
// This is the reserved attribute whose argument the compiler *dispatches on*. Applying
// `[AsyncMethodBuilder(typeof(AttrTicketBuilder))]` to a type makes that type task-like,
// and an `async AttrTicket` method is then rewritten into a state machine that calls
// `Create`, `Start`, `Task`, `SetResult`, `SetException`, `SetStateMachine`,
// `AwaitOnCompleted` and `AwaitUnsafeOnCompleted` — eight members bound by name and
// signature, from generated code, with not one call to any of them written in this project.
//
// So the hazard here is a reference count. Every member of both builders below is called by
// the assembly and by nothing an index can see; a query for "who calls AttrTicketBuilder.
// SetResult" answers nobody, and the answer is wrong in a way no diagnostic reports. The
// single `typeof` in the attribute argument is the whole visible edge between the async
// method and the eight members it uses.
//
// `AttrReceiptBuilder<>` adds the other half of that: an *unbound* generic type in a
// `typeof`, which names a declaration that has no type arguments and can therefore not be
// the type any instantiation actually uses.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.5.5: a task-like type. Its only qualification is the attribute and a
/// <c>GetAwaiter</c>; nothing about the declaration says "awaitable".
/// </summary>
[AsyncMethodBuilder(typeof(AttrTicketBuilder))]
public readonly struct AttrTicket
{
    private readonly Task _completion;

    /// <summary>Wraps the builder's own task.</summary>
    /// <param name="completion">What the builder is driving.</param>
    internal AttrTicket(Task completion) => _completion = completion;

    /// <summary>Whether the work has finished.</summary>
    public bool IsCompleted => _completion.IsCompleted;

    /// <summary>13.15.1: the awaitable pattern's one required member.</summary>
    /// <returns>An awaiter over the underlying task.</returns>
    public TaskAwaiter GetAwaiter() => _completion.GetAwaiter();
}

/// <summary>
/// 23.5.5: the builder the attribute names. Every member is required by the pattern and
/// called only from compiler-generated code.
/// </summary>
public struct AttrTicketBuilder
{
    private AsyncTaskMethodBuilder _inner;

    /// <summary>The pattern's factory.</summary>
    /// <returns>A fresh builder.</returns>
    public static AttrTicketBuilder Create() => new() { _inner = AsyncTaskMethodBuilder.Create() };

    /// <summary>The task-like value handed back to the caller of the async method.</summary>
    public AttrTicket Task => new(_inner.Task);

    /// <summary>Runs the state machine to its first suspension.</summary>
    /// <typeparam name="TStateMachine">The generated state machine's type.</typeparam>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        _inner.Start(ref stateMachine);

    /// <summary>Hands the boxed state machine over on the first suspension.</summary>
    /// <param name="stateMachine">The boxed state machine.</param>
    public void SetStateMachine(IAsyncStateMachine stateMachine) =>
        _inner.SetStateMachine(stateMachine);

    /// <summary>22.4 meets 23.5.5: an exception that escapes the body lands here.</summary>
    /// <param name="exception">What escaped.</param>
    public void SetException(Exception exception) => _inner.SetException(exception);

    /// <summary>Completes the ticket.</summary>
    public void SetResult() => _inner.SetResult();

    /// <summary>Suspends on an ordinary awaiter.</summary>
    /// <typeparam name="TAwaiter">The awaiter's type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine's type.</typeparam>
    /// <param name="awaiter">The awaiter, by reference.</param>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);

    /// <summary>Suspends on an awaiter that opts out of the execution context flow.</summary>
    /// <typeparam name="TAwaiter">The awaiter's type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine's type.</typeparam>
    /// <param name="awaiter">The awaiter, by reference.</param>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}

/// <summary>
/// 23.5.5: the generic case, whose attribute argument is an unbound generic type.
/// </summary>
/// <typeparam name="TResult">What the receipt carries.</typeparam>
[AsyncMethodBuilder(typeof(AttrReceiptBuilder<>))]
public readonly struct AttrReceipt<TResult>
{
    private readonly Task<TResult> _completion;

    /// <summary>Wraps the builder's own task.</summary>
    /// <param name="completion">What the builder is driving.</param>
    internal AttrReceipt(Task<TResult> completion) => _completion = completion;

    /// <summary>13.15.1: the awaitable pattern, with a result this time.</summary>
    /// <returns>An awaiter that yields the result.</returns>
    public TaskAwaiter<TResult> GetAwaiter() => _completion.GetAwaiter();
}

/// <summary>
/// 23.5.5: the generic builder. <c>SetResult</c> takes a value here, which is the one
/// signature difference from the void form.
/// </summary>
/// <typeparam name="TResult">What the receipt carries.</typeparam>
public struct AttrReceiptBuilder<TResult>
{
    private AsyncTaskMethodBuilder<TResult> _inner;

    /// <summary>The pattern's factory.</summary>
    /// <returns>A fresh builder.</returns>
    public static AttrReceiptBuilder<TResult> Create() =>
        new() { _inner = AsyncTaskMethodBuilder<TResult>.Create() };

    /// <summary>The task-like value handed back to the caller.</summary>
    public AttrReceipt<TResult> Task => new(_inner.Task);

    /// <summary>Runs the state machine to its first suspension.</summary>
    /// <typeparam name="TStateMachine">The generated state machine's type.</typeparam>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        _inner.Start(ref stateMachine);

    /// <summary>Hands the boxed state machine over.</summary>
    /// <param name="stateMachine">The boxed state machine.</param>
    public void SetStateMachine(IAsyncStateMachine stateMachine) =>
        _inner.SetStateMachine(stateMachine);

    /// <summary>An exception that escaped the body.</summary>
    /// <param name="exception">What escaped.</param>
    public void SetException(Exception exception) => _inner.SetException(exception);

    /// <summary>Completes the receipt with its value.</summary>
    /// <param name="result">The value the async method returned.</param>
    public void SetResult(TResult result) => _inner.SetResult(result);

    /// <summary>Suspends on an ordinary awaiter.</summary>
    /// <typeparam name="TAwaiter">The awaiter's type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine's type.</typeparam>
    /// <param name="awaiter">The awaiter, by reference.</param>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);

    /// <summary>Suspends on a critical awaiter.</summary>
    /// <typeparam name="TAwaiter">The awaiter's type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine's type.</typeparam>
    /// <param name="awaiter">The awaiter, by reference.</param>
    /// <param name="stateMachine">The state machine, by reference.</param>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}

/// <summary>
/// 23.5.5: the async methods that make the two builders load-bearing. Without these the
/// attribute is an inert `typeof` and the compiler never checks the pattern at all.
/// </summary>
public static class AttrTicketOffice
{
    /// <summary>
    /// 23.5.5: an async method whose return type is task-like only because of the
    /// attribute. The whole body is rewritten around the builder.
    /// </summary>
    /// <param name="ledger">Which ledger the ticket is for.</param>
    /// <returns>A ticket.</returns>
    public static async AttrTicket IssueAsync(string ledger)
    {
        await Task.Yield();
        AttrConditionalMethods.Trace(ledger);
    }

    /// <summary>
    /// 23.5.5 and 22.4: an async method that throws, so <c>SetException</c> is the member
    /// the generated code reaches rather than <c>SetResult</c>.
    /// </summary>
    /// <param name="ledger">Which ledger.</param>
    /// <returns>A receipt carrying the ledger's weight.</returns>
    public static async AttrReceipt<int> WeighAsync(string ledger)
    {
        await Task.Yield();

        if (ledger.Length == 0)
        {
            throw new Exceptions.AttrLedgerFault("a ledger needs a name");
        }

        return ledger.Length;
    }

    /// <summary>Awaits both, so the awaitable half of each type is referenced too.</summary>
    /// <returns>The weight, or zero if the ledger was refused.</returns>
    public static async Task<int> RunAsync()
    {
        await IssueAsync("main");

        try
        {
            return await WeighAsync(string.Empty);
        }
        catch (Exceptions.AttrLedgerFault)
        {
            return 0;
        }
    }
}
