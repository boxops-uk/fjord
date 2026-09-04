using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.9.9.2 — awaitability is a pattern, and a pattern can be satisfied by an *extension*
/// method. <c>await 3</c> is legal wherever this class is in scope, and the binding target is a
/// static method on a class the await site never mentions.
/// </summary>
public static class PxAwaitableExtensions
{
    /// <summary>Makes every <c>int</c> awaitable, by extension.</summary>
    public static PxAwaiter GetAwaiter(this int value) => new(value);

    /// <summary>Makes the declared awaitable's awaiter reachable by extension too.</summary>
    public static PxCriticalAwaiter GetAwaiter(this PxCriticalAwaitable awaitable) => new(awaitable.Value);
}

/// <summary>
/// 12.9.9.2 — a second awaitable, whose awaiter implements <c>ICriticalNotifyCompletion</c>, so
/// the state machine binds <c>UnsafeOnCompleted</c> instead of <c>OnCompleted</c>.
/// </summary>
public sealed class PxCriticalAwaitable
{
    /// <summary>Builds one over a value.</summary>
    public PxCriticalAwaitable(int value) => Value = value;

    /// <summary>The value its awaiter hands back.</summary>
    public int Value { get; }
}

/// <summary>
/// 12.9.9.4 — the critical awaiter. Three of its four members are bound by an <c>await</c> that
/// names none of them.
/// </summary>
public readonly struct PxCriticalAwaiter : ICriticalNotifyCompletion
{
    private readonly int _value;

    /// <summary>Builds one over a value.</summary>
    public PxCriticalAwaiter(int value) => _value = value;

    /// <summary>Read first.</summary>
    public bool IsCompleted => true;

    /// <summary>Read to produce the await expression's value.</summary>
    public int GetResult() => _value;

    /// <inheritdoc />
    public void OnCompleted(Action continuation) => continuation();

    /// <inheritdoc />
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>
/// 12.9.9 — await expressions. Every <c>await</c> below binds <c>GetAwaiter</c>, then
/// <c>IsCompleted</c>, <c>OnCompleted</c> or <c>UnsafeOnCompleted</c>, and <c>GetResult</c> —
/// four bindings from one keyword, none of them written at the use site.
/// </summary>
public static class PxAwait
{
    /// <summary>A task-returning method, so an await has something ordinary to await.</summary>
    public static Task<int> ValueLater(int value) => Task.FromResult(value);

    /// <summary>A void-returning task, so an await can be classified as producing nothing.</summary>
    public static Task NothingLater() => Task.CompletedTask;

    /// <summary>A value task, whose awaiter is a struct rather than a class.</summary>
    public static ValueTask<int> ValueTaskLater(int value) => new(value);

    /// <summary>
    /// 12.9.9.2 and 12.9.9.4 — the framework awaitables: a task with a value, a task without
    /// one, a value task, and a configured awaitable whose awaiter is a third type again.
    /// </summary>
    public static async Task<string> FrameworkAwaitables()
    {
        var withValue = await ValueLater(1);
        await NothingLater();
        var fromValueTask = await ValueTaskLater(2);
        var configured = await ValueLater(3).ConfigureAwait(false);
        var combined = await Task.WhenAll(ValueLater(4), ValueLater(5));
        var nested = await ValueLater(await ValueLater(6));
        var yielded = Task.Yield();
        await yielded;

        return $"{withValue} {fromValueTask} {configured} {combined.Length} {nested}";
    }

    /// <summary>
    /// 12.9.9.2 — the declared awaitables: one whose <c>GetAwaiter</c> is an instance method,
    /// one whose <c>GetAwaiter</c> is an extension method, and an <c>int</c>, made awaitable by
    /// extension.
    /// </summary>
    public static async Task<string> DeclaredAwaitables()
    {
        var fromInstancePattern = await new PxAwaitable(7);
        var fromExtensionPattern = await new PxCriticalAwaitable(8);
        var fromExtensionOnInt = await 9;

        return $"{fromInstancePattern} {fromExtensionPattern} {fromExtensionOnInt}";
    }

    /// <summary>
    /// 12.9.9.3 — the two classifications: an await of a value-producing awaitable is a value,
    /// and an await of a value-less one is classified as nothing, so it can only be a statement.
    /// </summary>
    public static async Task<string> Classifications()
    {
        // Classified as nothing: a statement, with no value to bind to anything.
        await NothingLater();

        // Classified as a value: usable as an operand, an argument, and an interpolation hole.
        var asOperand = await ValueLater(1) + 1;
        var asArgument = Math.Max(await ValueLater(2), 0);
        var inHole = $"{await ValueLater(3)}";
        var asReceiver = (await ValueLater(4)).ToString();

        return $"{asOperand} {asArgument} {inHole} {asReceiver}";
    }

    /// <summary>
    /// 12.9.9 — awaits in the other function-body forms: an async lambda, an async local
    /// function, and an async method that returns a value task.
    /// </summary>
    public static async Task<string> AwaitInEveryBody()
    {
        Func<Task<int>> asyncLambda = async () => await ValueLater(1);
        var fromLambda = await asyncLambda();
        var fromLocal = await AsyncLocal();
        var fromValueTaskMethod = await AsyncValueTask();

        return $"{fromLambda} {fromLocal} {fromValueTaskMethod}";

        static async Task<int> AsyncLocal() => await ValueLater(2);
    }

    /// <summary>An async method returning a value task, awaited above.</summary>
    public static async ValueTask<int> AsyncValueTask() => await ValueTaskLater(3);
}
