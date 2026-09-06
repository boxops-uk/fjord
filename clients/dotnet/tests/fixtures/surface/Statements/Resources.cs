// The vocabulary clauses 13.13 and 13.14 bind to, declared here so the binding target is
// inside the corpus.
//
// `using` and `lock` both reach a member without writing its name. A `using` statement binds
// `Dispose`, a `using` over a ref struct binds it *by pattern* rather than through
// `IDisposable`, an `await using` binds `DisposeAsync`, and a `lock` over a
// `System.Threading.Lock` binds `EnterScope` and the `Dispose` of the scope it returns. None
// of those four names appears at any use site in this project, so every one of them has to be
// declared here for a reference edge to have somewhere to land.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>
/// A resource for clause 13.14's <c>resource_acquisition</c>: an ordinary
/// <see cref="IDisposable"/>, which is what the <c>using</c> statement requires of a class.
/// </summary>
public sealed class StmtHandle : IDisposable
{
    /// <summary>Opens a handle under a name a caller can read back.</summary>
    /// <param name="name">What the handle is called.</param>
    public StmtHandle(string name) => Name = name;

    /// <summary>What the handle is called.</summary>
    public string Name { get; }

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    public bool Closed { get; private set; }

    /// <summary>Reads the handle, so a <c>using</c> body has something to do.</summary>
    /// <returns>The length of the name.</returns>
    public int Measure() => Name.Length;

    /// <summary>
    /// The member clause 13.14.1 binds without naming. Every <c>using</c> statement in
    /// <c>UsingStatements.cs</c> is a reference to this method with no identifier at the use
    /// site.
    /// </summary>
    public void Dispose() => Closed = true;
}

/// <summary>
/// A resource whose <c>Dispose</c> is found by *pattern* rather than through an interface —
/// which clause 13.14.1's post-standard rule permits for a <c>ref struct</c> and for nothing
/// else. This type implements no interface at all.
/// </summary>
public ref struct StmtScopedHandle
{
    private readonly int _weight;

    /// <summary>Opens a scoped handle.</summary>
    /// <param name="weight">A number the body can read.</param>
    public StmtScopedHandle(int weight)
    {
        _weight = weight;
        Closed = false;
    }

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    public bool Closed { get; private set; }

    /// <summary>Reads the handle.</summary>
    /// <returns>The weight it was opened with.</returns>
    public int Measure() => _weight;

    /// <summary>The pattern <c>Dispose</c>. Nothing declares that this is one.</summary>
    public void Dispose() => Closed = true;
}

/// <summary>
/// A resource for the post-standard <c>await using</c> form of clause 13.14: it implements
/// <see cref="IAsyncDisposable"/>, so the binding goes through the interface.
/// </summary>
public sealed class StmtAsyncHandle : IAsyncDisposable
{
    /// <summary>Opens an asynchronous handle.</summary>
    /// <param name="name">What the handle is called.</param>
    public StmtAsyncHandle(string name) => Name = name;

    /// <summary>What the handle is called.</summary>
    public string Name { get; }

    /// <summary>Whether <see cref="DisposeAsync"/> has run.</summary>
    public bool Closed { get; private set; }

    /// <summary>Reads the handle.</summary>
    /// <returns>The length of the name.</returns>
    public int Measure() => Name.Length;

    /// <summary>The member <c>await using</c> binds without naming it.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        Closed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The two things clause 13.13 can lock on, side by side: an ordinary object, which binds
/// <c>Monitor.Enter</c>/<c>Monitor.Exit</c> in the framework, and a
/// <see cref="System.Threading.Lock"/>, which binds <c>EnterScope</c> and its scope's
/// <c>Dispose</c> instead. Neither name is written by a <c>lock</c> statement.
/// </summary>
public sealed class StmtGuardedCounter
{
    /// <summary>The plain object a <c>lock</c> statement may use as its monitor.</summary>
    private readonly object _monitor = new();

    /// <summary>The dedicated lock type, which changes what a <c>lock</c> compiles into.</summary>
    private readonly Lock _gate = new();

    private int _count;

    /// <summary>How many increments have been counted.</summary>
    public int Count => _count;

    /// <summary>The monitor, exposed so another file can lock on the same object twice.</summary>
    public object Monitor => _monitor;

    /// <summary>The lock object, exposed for the same reason.</summary>
    public Lock Gate => _gate;

    /// <summary>Adds one, unguarded, so a <c>lock</c> body has a statement worth guarding.</summary>
    public void Bump() => _count++;
}
