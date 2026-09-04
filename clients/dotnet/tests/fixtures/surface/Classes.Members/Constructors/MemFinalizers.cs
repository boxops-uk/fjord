// Clause 15.13 (finalizers). A finalizer is spelled `~T()`, takes no parameters, no
// accessibility modifier and no `static`, cannot be overloaded, cannot be inherited, cannot be
// called, and is emitted as an override of `object.Finalize()`.
//
// So a finalizer's source name and its emitted name have no characters in common, and it is the
// only member in the language that is *both* nameless in source (it is a token plus the type
// name) and named in metadata by something the source never writes. Three consequences, all
// written below:
//
//   1. **`~MemHandle` is `Finalize()`.** `MemReservedFinalizer` in
//      `Members/MemReservedNames.cs` puts a method called `Finalize(int)` beside one, so a type
//      holds two members whose emitted names are both `Finalize` and whose source names are
//      `~MemReservedFinalizer` and `Finalize`.
//   2. **A finalizer is an override that the source cannot spell as one.** `~MemHandle()` and
//      `~MemHandleHeir()` are two overrides of one base member — and neither carries `override`,
//      and neither may carry it (CS0106).
//   3. **A finalizer has no callers, ever.** `GC.SuppressFinalize` is the only reference to the
//      *mechanism* the source can write, and it names no finalizer.
//
// The dispose pattern is here because it is the only shape in which a finalizer's body is worth
// walking: `Dispose(bool)` is called from two places, one of which is the finalizer and one of
// which is a method — and only one of the two is a call an index can see.

using System;

namespace Surface.Classes.Members.Constructors;

/// <summary>15.13: a finalizer in the dispose pattern, which is where finalizers occur.</summary>
public class MemHandle : IDisposable
{
    private bool _closed;

    /// <summary>15.11.1: an ordinary constructor, so the type has both ends of a lifetime.</summary>
    public MemHandle(string name) => Name = name;

    /// <summary>15.13: the finalizer. Emitted as `Finalize()`, an override of
    /// <see cref="object"/>'s, and `override` may not be written on it.</summary>
    ~MemHandle() => Release(false);

    /// <summary>What the handle is called.</summary>
    public string Name { get; }

    /// <summary>Whether it has been released.</summary>
    public bool Closed => _closed;

    /// <summary>15.13: the disposer, whose reference to <c>SuppressFinalize</c> is the only way
    /// the source can mention the finalizer at all — and it mentions it by naming
    /// <c>this</c>.</summary>
    public void Dispose()
    {
        Release(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>15.13: the shared body, called from a method and from the finalizer. The second
    /// call site is in a member with no name in metadata terms.</summary>
    protected virtual void Release(bool managed)
    {
        if (!_closed)
        {
            _closed = true;
        }
    }
}

/// <summary>15.13: a finalizer in a derived class. The runtime chains it to the base one; the
/// source does not, and cannot — there is no `base.~MemHandle()` to write, so this is an
/// override relation with no reference expressing it.</summary>
public sealed class MemHandleHeir : MemHandle
{
    private int _generation;

    /// <summary>15.11.2: chains to the base constructor, which the source *can* express — the
    /// contrast with the finalizer below is the point.</summary>
    public MemHandleHeir(string name)
        : base(name)
    {
    }

    /// <summary>15.13: the second finalizer on the chain. It runs after this one's body, and
    /// nothing here says so.</summary>
    ~MemHandleHeir() => _generation = -1;

    /// <summary>15.13: the override that <em>is</em> spelled as one, for contrast.</summary>
    protected override void Release(bool managed)
    {
        _generation = managed ? 1 : 2;
        base.Release(managed);
    }

    /// <summary>15.13: which generation released it.</summary>
    public int Generation => _generation;
}

/// <summary>15.13: a finalizer with a block body and a `try`/`finally`, which is the shape the
/// guidance asks for and which makes the member's body worth walking.</summary>
public sealed class MemBuffer
{
    private readonly int[] _slots;
    private bool _returned;

    /// <summary>15.11.1: takes the size.</summary>
    public MemBuffer(int size) => _slots = new int[size];

    /// <summary>15.13: a finalizer with statements rather than an expression body.</summary>
    ~MemBuffer()
    {
        try
        {
            _returned = true;
        }
        finally
        {
            Array.Clear(_slots);
        }
    }

    /// <summary>How many slots there are.</summary>
    public int Size => _slots.Length;

    /// <summary>Whether the finalizer ran.</summary>
    public bool Returned => _returned;
}

/// <summary>15.13: references to the types that have finalizers. There is no reference to a
/// finalizer here, because there is no way to write one.</summary>
public static class MemFinalizerUse
{
    /// <summary>15.13: constructs each finalizable type and disposes the one that can be
    /// disposed. Every finalizer above has zero call sites after this method, and that is
    /// correct.</summary>
    public static string UseAll()
    {
        using var handle = new MemHandle("plain");
        var heir = new MemHandleHeir("heir");
        heir.Dispose();
        var buffer = new MemBuffer(4);
        return $"{handle.Closed} {heir.Generation} {buffer.Size} {buffer.Returned}";
    }
}
