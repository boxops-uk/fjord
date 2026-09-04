// Clause 15.14.1 (async functions — general). `async` is a modifier on a function member whose
// body the compiler rewrites into a state machine, so this is the clause where one source
// declaration becomes a member *and a type*: an async method `Load` in `MemLoader` produces a
// nested type called `<Load>d__0`, and that name is not a C# identifier and does contain the
// method's name.
//
// Three hazards, and all three are written here.
//
//   1. **A member and a type from one declaration.** Every async method below has a generated
//      nested state machine whose demangled name is the method's name. `MemLoader` declares
//      `Load()` and `Load<TItem>(TItem)`, so two of those nested types demangle to `Load` and
//      are separated only by the ordinal in the mangled name — an ordinal that counts *all*
//      lambdas and state machines in the type, not the overloads of one name.
//   2. **`await` binds by name and shows no name.** `await thing` calls `GetAwaiter()`,
//      `IsCompleted` and `GetResult()` — three members, no identifiers at the call site. The
//      binding is by well-known name and not through an interface, which
//      `MemTaskBuilders.cs` makes visible by writing a task type that implements nothing.
//   3. **`async void` is a different member from `async Task`.** `Fire()` and `FireAsync()`
//      differ in return type only, which is not part of a signature — so they need different
//      names, and the pair that *would* collide is exactly what cannot be written.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Classes.Members.Async;

/// <summary>15.14.1: one async method per return type the pattern admits.</summary>
public class MemLoader
{
    private int _loaded;

    /// <summary>15.14.1: `async Task` — no result, and the state machine's `SetResult` takes no
    /// argument.</summary>
    public async Task Load()
    {
        await Task.Yield();
        _loaded++;
    }

    /// <summary>15.14.1: `async Task&lt;T&gt;` at a different arity of the same name, so two
    /// state machines in this type demangle to `Load`.</summary>
    public async Task<TItem> Load<TItem>(TItem seed)
    {
        await Task.Yield();
        _loaded++;
        return seed;
    }

    /// <summary>15.14.1: `async ValueTask`, whose builder is a struct rather than a class — a
    /// different task type through the same clause.</summary>
    public async ValueTask Drain()
    {
        await Task.Delay(0).ConfigureAwait(false);
        _loaded = 0;
    }

    /// <summary>15.14.1: `async ValueTask&lt;T&gt;`.</summary>
    public async ValueTask<int> Count()
    {
        await Task.Yield();
        return _loaded;
    }

    /// <summary>15.14.1: `async void` — a fire-and-forget member whose exceptions have nowhere
    /// to go. Its builder's `SetException` posts to the synchronization context instead of
    /// storing on a task.</summary>
    public async void Fire()
    {
        await Task.Yield();
        _loaded++;
    }

    /// <summary>15.14.1: the `async Task` sibling of <see cref="Fire"/>. The two differ only in
    /// return type, which no signature contains — so they cannot share a name, and this is why
    /// the `Async` suffix exists.</summary>
    public async Task FireAsync()
    {
        await Task.Yield();
        _loaded++;
    }

    /// <summary>15.14.1: an async method with a `using` and an `await using`, so the state
    /// machine holds a `finally` across a suspension point.</summary>
    public async Task<int> Scoped()
    {
        await using var gate = new MemAsyncGate();
        using var handle = new MemSyncGate();
        await gate.OpenAsync().ConfigureAwait(false);
        return handle.Ticket + gate.Ticket;
    }

    /// <summary>15.14.1: an async method with a `try`/`catch`/`finally` around an `await`, which
    /// is the shape the rewrite handles least like ordinary code.</summary>
    public async Task<string> Guarded()
    {
        try
        {
            await Task.Yield();
            return "ok";
        }
        catch (InvalidOperationException)
        {
            return "caught";
        }
        finally
        {
            _loaded++;
        }
    }

    /// <summary>15.14.1: an async lambda and an async local function, so a state machine is
    /// generated for a member that has no declaration in a member list.</summary>
    public async Task<int> Nested()
    {
        Func<int, Task<int>> doubler = async value =>
        {
            await Task.Yield();
            return value * 2;
        };

        async Task<int> Tripler(int value)
        {
            await Task.Yield();
            return value * 3;
        }

        return await doubler(2).ConfigureAwait(false) + await Tripler(3).ConfigureAwait(false);
    }

    /// <summary>15.14.1: an async method whose builder is chosen by an attribute on the *method*
    /// rather than by its return type — the pooling builder for `ValueTask`, which is the one
    /// place clause 15.14.2's attribute appears on a member.</summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask Pooled()
    {
        await Task.Yield();
        _loaded++;
    }

    /// <summary>15.14.1: every async member above, awaited. `Fire()` is the one that cannot
    /// be — an `async void` method's call is a statement and its completion is unobservable.</summary>
    public async Task<string> UseAll()
    {
        await Load().ConfigureAwait(false);
        var item = await Load(3).ConfigureAwait(false);
        await Drain().ConfigureAwait(false);
        var count = await Count().ConfigureAwait(false);
        Fire();
        await FireAsync().ConfigureAwait(false);
        await Pooled().ConfigureAwait(false);
        return $"{item} {count} {await Scoped().ConfigureAwait(false)} " +
               $"{await Guarded().ConfigureAwait(false)} {await Nested().ConfigureAwait(false)}";
    }
}

/// <summary>15.14.1: something `await using` can bind to — an async disposable, whose
/// `DisposeAsync` is found through the interface rather than by name.</summary>
public sealed class MemAsyncGate : IAsyncDisposable
{
    /// <summary>Which ticket the gate handed out.</summary>
    public int Ticket { get; private set; } = 1;

    /// <summary>15.14.1: an async method returning `ValueTask` that is itself awaited inside
    /// another async method.</summary>
    public async ValueTask OpenAsync()
    {
        await Task.Yield();
        Ticket++;
    }

    /// <summary>15.14.1: the interface member `await using` calls.</summary>
    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Ticket = 0;
    }
}

/// <summary>15.14.1: the synchronous counterpart, so one method holds a `using` and an
/// `await using` over two different interfaces.</summary>
public sealed class MemSyncGate : IDisposable
{
    /// <summary>Which ticket the gate handed out.</summary>
    public int Ticket { get; private set; } = 2;

    /// <summary>15.14.1: the interface member `using` calls.</summary>
    public void Dispose() => Ticket = 0;
}
