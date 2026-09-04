using System.Diagnostics.CodeAnalysis;

namespace Surface.Modern.Structs;

/// <summary>
/// C# 11 — ref fields: a <c>ref</c> field in a <c>ref struct</c>, assigned with
/// <c>= ref</c>. A ref field is a *variable* whose storage lives somewhere else, so the
/// declaration and every read of it are about two different pieces of storage at once.
/// </summary>
public ref struct RefSlot
{
    private ref int _target;

    private readonly ref readonly int _origin;

    /// <summary>Binds both ref fields; <c>= ref</c> is the only way to assign one.</summary>
    public RefSlot(ref int target, ref readonly int origin)
    {
        _target = ref target;
        _origin = ref origin;
    }

    /// <summary>Reads and writes through the ref field, not into the struct.</summary>
    public int Value
    {
        get => _target;
        set => _target = value;
    }

    /// <summary>Reads the <c>ref readonly</c> field.</summary>
    public int Origin => _origin;

    /// <summary>Hands the referent back out by reference.</summary>
    public ref int Target => ref _target;
}

/// <summary>
/// C# 11 — <c>[UnscopedRef]</c>: without it, a <c>ref</c> to a field of a struct may not
/// escape the member that produced it, because <c>this</c> is implicitly <c>scoped</c>. The
/// attribute is the opt-out, and it changes the ref-safe-to-escape of the return value rather
/// than anything about the type.
/// </summary>
public struct Accumulator
{
    private int _total;

    /// <summary>C# 11 — <c>[UnscopedRef]</c> on a ref-returning property.</summary>
    [UnscopedRef]
    public ref int Total => ref _total;

    /// <summary>C# 11 — <c>[UnscopedRef]</c> on a ref-returning method.</summary>
    [UnscopedRef]
    public ref int Slot() => ref _total;

    /// <summary>The scoped default: a copy, which may escape.</summary>
    public int Read() => _total;
}

/// <summary>An interface a <c>ref struct</c> implements below.</summary>
public interface ISurfaceCursor
{
    /// <summary>Advances the cursor, reporting whether it moved.</summary>
    bool MoveNext();

    /// <summary>The element the cursor is on.</summary>
    int Current { get; }
}

/// <summary>
/// C# 13 — ref struct interfaces: a <c>ref struct</c> may implement an interface. The
/// implementation is real and callable, but no conversion to the interface exists — the type
/// cannot be boxed — so the only way to reach it through the interface is the constrained
/// generic in <see cref="RefFieldUses.Drain{TCursor}"/>.
/// </summary>
public ref struct SpanCursor : ISurfaceCursor, IDisposable
{
    private readonly ReadOnlySpan<int> _items;

    private int _index;

    /// <summary>Starts a cursor before the first element.</summary>
    public SpanCursor(ReadOnlySpan<int> items)
    {
        _items = items;
        _index = -1;
    }

    /// <inheritdoc/>
    public int Current => _items[_index];

    /// <inheritdoc/>
    public bool MoveNext() => ++_index < _items.Length;

    /// <inheritdoc/>
    public void Dispose() => _index = _items.Length;
}

/// <summary>C# 11 and C# 13 — the uses that make the modifiers above observable.</summary>
public static class RefFieldUses
{
    /// <summary>
    /// C# 13 — the <c>allows ref struct</c> anti-constraint. It is not a constraint that
    /// narrows what may be substituted but one that *widens* it, and in exchange the body may
    /// not box <paramref name="cursor"/> — so this signature could not be written before
    /// C# 13 and cannot be satisfied by <see cref="SpanCursor"/> without it.
    /// </summary>
    /// <typeparam name="TCursor">The cursor type, which may be a ref struct.</typeparam>
    public static int Drain<TCursor>(TCursor cursor)
        where TCursor : ISurfaceCursor, IDisposable, allows ref struct
    {
        var total = 0;

        while (cursor.MoveNext())
        {
            total += cursor.Current;
        }

        cursor.Dispose();

        return total;
    }

    /// <summary>Substitutes a ref struct for the anti-constrained parameter.</summary>
    public static int DrainSpan()
    {
        ReadOnlySpan<int> items = [1, 2, 3, 4];

        return Drain(new SpanCursor(items));
    }

    /// <summary>
    /// C# 11 — the <c>scoped</c> modifier on a parameter: it promises the reference does not
    /// escape, which is what lets the caller pass a reference to its own stack.
    /// </summary>
    public static int Peek(scoped ref int value) => value;

    /// <summary>C# 11 — <c>scoped in</c> and <c>scoped out</c>, the other two ref kinds.</summary>
    public static void Split(scoped in int source, out int copy) => copy = source;

    /// <summary>C# 11 — <c>scoped</c> on a ref struct parameter, which is a different rule.</summary>
    public static int PeekSlot(scoped RefSlot slot) => slot.Value;

    /// <summary>C# 11 — a <c>scoped</c> local, whose referent may not outlive the block.</summary>
    public static int Local()
    {
        var storage = 7;
        var origin = 1;

        scoped var slot = new RefSlot(ref storage, ref origin);
        slot.Value = 9;

        scoped Span<int> scratch = stackalloc int[2];
        scratch[0] = slot.Target;

        var accumulator = default(Accumulator);
        accumulator.Total = storage + slot.Origin;
        accumulator.Slot() += scratch[0];

        return accumulator.Read() + Peek(ref storage);
    }
}
