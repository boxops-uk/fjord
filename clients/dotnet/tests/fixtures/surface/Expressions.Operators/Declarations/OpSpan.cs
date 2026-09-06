using System;

namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A window over a run of integers, shaped so that the index-from-end operator (12.9.6)
/// and the range operator (12.10) both apply to it without either being declared.
/// </summary>
/// <remarks>
/// <para>Clause 12.9.6 makes <c>^e</c> a <see cref="Index"/>, and 12.10 makes
/// <c>e1..e2</c> a <see cref="Range"/>. Neither is a member of this type. What makes
/// <c>window[^1]</c> and <c>window[1..3]</c> legal is the implicit indexer support
/// pattern: a countable member (<see cref="Length"/>) plus, for the range case, a
/// <see cref="Slice(int, int)"/> method with the right signature.</para>
/// <para>So <c>window[^1]</c> is a reference to <see cref="Length"/> and to the single
/// <c>int</c> indexer, and <c>window[1..3]</c> is a reference to <see cref="Length"/> and
/// to <see cref="Slice(int, int)"/> — four member references behind two tokens that name
/// none of them. This type declares exactly one indexer, so nothing here needs an
/// <c>Index</c> or <c>Range</c> overload of it.</para>
/// </remarks>
public sealed class OpSpan
{
    private readonly int[] _items;
    private readonly int _start;

    /// <summary>Constructs a window over a copy of the given items.</summary>
    public OpSpan(params int[] items)
    {
        _items = items;
        _start = 0;
        Length = items.Length;
    }

    private OpSpan(int[] items, int start, int length)
    {
        _items = items;
        _start = start;
        Length = length;
    }

    /// <summary>The countable member the implicit indexer support pattern requires.</summary>
    public int Length { get; }

    /// <summary>The one indexer this type declares.</summary>
    public int this[int index] => _items[_start + index];

    /// <summary>The slicing member the range form of the pattern requires.</summary>
    public OpSpan Slice(int start, int length) => new(_items, _start + start, length);

    /// <inheritdoc/>
    public override string ToString()
    {
        var parts = new string[Length];

        for (int i = 0; i < Length; i++)
        {
            parts[i] = this[i].ToString();
        }

        return string.Join(",", parts);
    }
}
