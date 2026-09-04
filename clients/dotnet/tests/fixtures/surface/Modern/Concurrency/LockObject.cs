using System.Threading;

namespace Surface.Modern.Concurrency;

/// <summary>
/// C# 13 — New lock type and semantics. When the operand of a <c>lock</c> statement is a
/// <see cref="Lock"/>, the statement no longer lowers to <see cref="Monitor"/>: it calls
/// <c>EnterScope()</c> and disposes the returned scope. The source is identical to a
/// <c>lock</c> on any other object, so what a reference from this statement points at is
/// decided entirely by the operand's *type*.
/// </summary>
public sealed class LockedCounter
{
    private readonly Lock _gate = new();

    private readonly object _legacyGate = new();

    private int _count;

    /// <summary>C# 13 — <c>lock</c> over a <see cref="Lock"/>, which uses <c>EnterScope</c>.</summary>
    public int Next()
    {
        lock (_gate)
        {
            return ++_count;
        }
    }

    /// <summary>The pre-C# 13 lowering, over a plain object, which uses <see cref="Monitor"/>.</summary>
    public int NextLegacy()
    {
        lock (_legacyGate)
        {
            return ++_count;
        }
    }

    /// <summary>C# 13 — the same thing written out by hand, which is what the statement means.</summary>
    public int NextExplicitly()
    {
        using (_gate.EnterScope())
        {
            return ++_count;
        }
    }

    /// <summary>C# 13 — <see cref="Lock.TryEnter()"/>, which the statement form cannot express.</summary>
    public bool TryNext()
    {
        if (!_gate.TryEnter())
        {
            return false;
        }

        try
        {
            _count++;

            return true;
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>The count so far.</summary>
    public int Count => _count;
}
