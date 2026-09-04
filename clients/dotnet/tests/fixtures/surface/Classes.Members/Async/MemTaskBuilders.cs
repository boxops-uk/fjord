// Clause 15.14.2 (task-type builder pattern). An `async` method's return type may be *any* type
// carrying `[AsyncMethodBuilder]`, provided the named builder has the eight members the compiler
// looks for. It looks for them **by name**: there is no interface, no base class and no
// constraint anywhere in this file tying `MemFutureBuilder` to the compiler's expectations.
//
// So this clause is the corpus's canonical case of a declaration that is used and never
// referenced. Every member of both builders below is called by generated code and by nothing in
// the source, and the two `Task` properties are read by a rewrite that no expression in this
// project performs. A query for callers of `MemFutureBuilder.SetResult` must answer **none**,
// and a query for whether `MemFuture` is used must answer **yes** — through `MemFutureUse`,
// where the only mention of the type is a return type.
//
// The pattern is written twice because the compiler requires two different shapes:
// a void-like task type's builder has `SetResult()` and a result-carrying one's has
// `SetResult(TResult)`. Getting that wrong is CS0656, "missing compiler required member" — the
// only diagnostic in this project that names a member the source never wrote.
//
// The names differ by more than a type parameter — `MemFuture` and `MemFutureOf<TResult>`, not
// `MemFuture` and `MemFuture<TResult>` — because one name at two arities is a shape that kills
// the indexing run.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Classes.Members.Async;

/// <summary>15.14.2: a void-like task type. The attribute is the whole of its contract with the
/// compiler, and the awaiter is found by the name <c>GetAwaiter</c>.</summary>
[AsyncMethodBuilder(typeof(MemFutureBuilder))]
public sealed class MemFuture
{
    /// <summary>15.14.2: whether the builder has completed it.</summary>
    public bool Completed { get; internal set; }

    /// <summary>15.14.2: what `await` looks for, by name. No interface declares it.</summary>
    public MemFutureAwaiter GetAwaiter() => new MemFutureAwaiter(this);
}

/// <summary>15.14.2: the awaiter. `IsCompleted`, `GetResult` and one of the two `OnCompleted`
/// members are what `await` calls — the first two by name, the third through
/// <see cref="ICriticalNotifyCompletion"/>, which is the one part of the pattern that is an
/// interface.</summary>
public readonly struct MemFutureAwaiter : ICriticalNotifyCompletion
{
    private readonly MemFuture _future;

    /// <summary>Wraps the future being awaited.</summary>
    public MemFutureAwaiter(MemFuture future) => _future = future;

    /// <summary>15.14.2: found by name.</summary>
    public bool IsCompleted => _future.Completed;

    /// <summary>15.14.2: found by name. A void-like awaiter's `GetResult` returns void.</summary>
    public void GetResult() => _future.Completed = true;

    /// <summary>15.14.2: the interface half of the pattern.</summary>
    public void OnCompleted(Action continuation) => continuation();

    /// <summary>15.14.2: the other interface half, which the compiler prefers.</summary>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>15.14.2: the builder for a void-like task type. Eight members, none of them
/// referenced by any expression in this corpus, all eight called by the rewrite of
/// <see cref="MemFutureUse.Run"/>.</summary>
public struct MemFutureBuilder
{
    private MemFuture _future;

    /// <summary>15.14.2: the factory the rewrite calls first.</summary>
    public static MemFutureBuilder Create() => new MemFutureBuilder { _future = new MemFuture() };

    /// <summary>15.14.2: the task the async method returns, read once by the rewrite.</summary>
    public MemFuture Task => _future;

    /// <summary>15.14.2: the entry point, which runs the state machine to its first
    /// suspension.</summary>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
        => stateMachine.MoveNext();

    /// <summary>15.14.2: called for a boxed state machine, which is what happens when the
    /// machine survives its first suspension.</summary>
    public void SetStateMachine(IAsyncStateMachine stateMachine) => _ = stateMachine;

    /// <summary>15.14.2: completion. A void-like builder's `SetResult` takes nothing — taking a
    /// parameter here is CS0656 at every `async MemFuture` method.</summary>
    public void SetResult() => _future.Completed = true;

    /// <summary>15.14.2: failure.</summary>
    public void SetException(Exception exception) => throw exception;

    /// <summary>15.14.2: the suspension hook for an ordinary awaiter.</summary>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => awaiter.OnCompleted(stateMachine.MoveNext);

    /// <summary>15.14.2: the suspension hook the compiler prefers, for an awaiter that opts out
    /// of flowing execution context.</summary>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
}

/// <summary>15.14.2: a result-carrying task type. Its builder is named as an open generic type
/// in the attribute — <c>typeof(MemFutureOfBuilder&lt;&gt;)</c> — which the compiler closes with
/// this type's own type argument.</summary>
/// <typeparam name="TResult">What the future carries.</typeparam>
[AsyncMethodBuilder(typeof(MemFutureOfBuilder<>))]
public sealed class MemFutureOf<TResult>
{
    /// <summary>15.14.2: the value the builder set.</summary>
    public TResult? Value { get; internal set; }

    /// <summary>15.14.2: found by name, as before.</summary>
    public MemFutureOfAwaiter<TResult> GetAwaiter() => new MemFutureOfAwaiter<TResult>(this);
}

/// <summary>15.14.2: the awaiter for a result-carrying task type, whose `GetResult` returns the
/// result rather than void.</summary>
/// <typeparam name="TResult">What the future carries.</typeparam>
public readonly struct MemFutureOfAwaiter<TResult> : ICriticalNotifyCompletion
{
    private readonly MemFutureOf<TResult> _future;

    /// <summary>Wraps the future being awaited.</summary>
    public MemFutureOfAwaiter(MemFutureOf<TResult> future) => _future = future;

    /// <summary>15.14.2: found by name.</summary>
    public bool IsCompleted => true;

    /// <summary>15.14.2: found by name, and this time it has a return type.</summary>
    public TResult? GetResult() => _future.Value;

    /// <summary>15.14.2: the interface half.</summary>
    public void OnCompleted(Action continuation) => continuation();

    /// <summary>15.14.2: the preferred interface half.</summary>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>15.14.2: the builder for a result-carrying task type. The same eight members, and
/// `SetResult` takes the result — which is the whole difference between the two builders and the
/// reason the pattern cannot be expressed once.</summary>
/// <typeparam name="TResult">What the future carries.</typeparam>
public struct MemFutureOfBuilder<TResult>
{
    private MemFutureOf<TResult> _future;

    /// <summary>15.14.2: the factory.</summary>
    public static MemFutureOfBuilder<TResult> Create()
        => new MemFutureOfBuilder<TResult> { _future = new MemFutureOf<TResult>() };

    /// <summary>15.14.2: the task the async method returns.</summary>
    public MemFutureOf<TResult> Task => _future;

    /// <summary>15.14.2: the entry point.</summary>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
        => stateMachine.MoveNext();

    /// <summary>15.14.2: the boxed-machine hook.</summary>
    public void SetStateMachine(IAsyncStateMachine stateMachine) => _ = stateMachine;

    /// <summary>15.14.2: completion with a result.</summary>
    public void SetResult(TResult result) => _future.Value = result;

    /// <summary>15.14.2: failure.</summary>
    public void SetException(Exception exception) => throw exception;

    /// <summary>15.14.2: the suspension hook.</summary>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => awaiter.OnCompleted(stateMachine.MoveNext);

    /// <summary>15.14.2: the preferred suspension hook.</summary>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
        => awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
}

/// <summary>15.14.2: the async methods that put the two builders to work, and the awaits that
/// put the two awaiters to work. These four members are the only source references to the
/// custom task types anywhere, and they reference them as return types alone.</summary>
public static class MemFutureUse
{
    /// <summary>15.14.2: an async method returning a custom void-like task type. Its rewrite
    /// calls all eight members of <see cref="MemFutureBuilder"/>.</summary>
    public static async MemFuture Run()
    {
        await Task.Yield();
    }

    /// <summary>15.14.2: an async method returning a custom result-carrying task type.</summary>
    public static async MemFutureOf<int> Compute(int seed)
    {
        await Task.Yield();
        return seed * 2;
    }

    /// <summary>15.14.2: awaiting a custom task type, which calls
    /// <see cref="MemFuture.GetAwaiter"/>'s result's three members with no identifier at
    /// the call site.</summary>
    public static async Task<string> Await()
    {
        await Run();
        var computed = await Compute(3);
        return $"{computed}";
    }

    /// <summary>15.14.2: the awaiters reached explicitly, which is the only spelling in this
    /// project where the pattern's member names actually appear as identifiers.</summary>
    public static string ByName()
    {
        var awaiter = Compute(4).GetAwaiter();
        return $"{awaiter.IsCompleted} {awaiter.GetResult()}";
    }
}
