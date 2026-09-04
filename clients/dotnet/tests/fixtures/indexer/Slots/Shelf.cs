namespace Slots;

/// <summary>
/// Three indexers on a generic type: the shape whose symbol carries both the containing
/// type's arity and the term's own sibling ordinal.
/// </summary>
public class Shelf<T>
{
    private readonly T[] slots = new T[8];

    /// <summary>By position — the sibling that sorts first, so its ordinal is empty.</summary>
    public T this[int index] => slots[index];

    /// <summary>By page and position — a second indexer differing in parameter count.</summary>
    public T this[int page, int index] => slots[(page * 2) + index];

    /// <summary>By name — differing from the first only in parameter <i>type</i>.</summary>
    public T this[string name] => slots[name.Length];
}

/// <summary>
/// One indexer and nothing else: the control whose symbol must be the string it always
/// was, because the ordinal is empty for the only sibling.
/// </summary>
public class Single
{
    public int this[int index] => index;
}

/// <summary>Two indexers, so an implementation of both is two same-named members.</summary>
public interface IShelf
{
    int this[int index] { get; }

    int this[string name] { get; }
}

/// <summary>
/// Two explicit implementations of one interface's indexers. Roslyn names each
/// <c>Slots.IShelf.this[]</c>, which is backtick-escaped for the dots and the brackets —
/// so the ordinal has to land inside the escape rather than after it.
/// </summary>
public class Explicit : IShelf
{
    int IShelf.this[int index] => index;

    int IShelf.this[string name] => name.Length;
}
