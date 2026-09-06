// Clause 18.4.3 — implicit `Range` support. When a type is countable and has an accessible
// `Slice(int, int)`, `E[R]` where `R` is a `Range` is rewritten onto the countable property
// and that method.
//
// This is the sharpest hazard in the clause, because the target is a *method* and a method
// carries an ordinal. `ScipSymbols.Disambiguator` counts same-named siblings ordered by
// documentation-comment id, so in `RngSliceOverloads` below the pattern's own
// `Slice(int, int)` mints `Slice(+1).` — `M:….Slice(System.Int32)` sorts before
// `M:….Slice(System.Int32,System.Int32)`, and `Slice(System.Int64,System.Int64)` sorts after
// both. Adding an unrelated `Slice` overload therefore renumbers the member the pattern binds,
// and the only site pointing at it — `Overloaded[1..3]` — contains no text from which the new
// ordinal could be re-derived. A stored reference and its declaration disagree with nothing at
// the use site having changed.
//
// `RngSliceable` is deliberately sliceable *without* being indexable: it declares no indexer
// at all, which is the case 18.1's closing note calls out, and it means the range path is
// exercised here with no `this[]` descriptor anywhere in the file to collapse.

namespace Surface.Ranges;

/// <summary>
/// Sliceable and not indexable: a countable property and a <c>Slice</c>, with no indexer
/// (18.4.3).
/// </summary>
public sealed class RngSliceable
{
    private readonly int[] _items;

    /// <summary>Builds a sliceable sequence over the values given.</summary>
    /// <param name="values">The values.</param>
    public RngSliceable(params int[] values) => _items = values;

    /// <summary>The countable property.</summary>
    public int Count => _items.Length;

    /// <summary>
    /// The slice method. Its only unnamed callers are the element accesses in
    /// <see cref="RngRangePatternUses"/>.
    /// </summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>A new sequence over those elements.</returns>
    public RngSliceable Slice(int start, int length) => new(_items[start..(start + length)]);

    /// <summary>
    /// Reading an element by name rather than by an indexer, so the type stays unindexable
    /// while still being readable.
    /// </summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The element there.</returns>
    public int ElementAt(int index) => _items[index];
}

/// <summary>
/// The overload set that moves the pattern member's ordinal: three <c>Slice</c> methods, of
/// which only the middle one is what a range access binds to (18.4.3).
/// </summary>
public sealed class RngSliceOverloads
{
    private readonly int[] _items = { 1, 2, 3, 4, 5, 6 };

    /// <summary>The countable property, spelled <c>Length</c> in this one.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// The overload that sorts *before* the pattern's, and so pushes the pattern's ordinal to
    /// <c>+1</c>. Nothing about it is related to slicing by a range.
    /// </summary>
    /// <param name="start">Where to slice from, to the end.</param>
    /// <returns>The elements from there on.</returns>
    public int[] Slice(int start) => _items[start..];

    /// <summary>
    /// The overload the pattern binds to. Written second, counted second, and referenced by
    /// an expression that spells neither its name nor its arity.
    /// </summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>Those elements.</returns>
    public int[] Slice(int start, int length) => _items[start..(start + length)];

    /// <summary>The overload that sorts after both, so it does not move the ordinal of either.</summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>Those elements.</returns>
    public int[] Slice(long start, long length) => Slice((int)start, (int)length);
}

/// <summary>
/// Every operand-omission form of a range applied to a pattern-conforming type, and the named
/// calls that make the ordinal drift observable (18.4.3).
/// </summary>
public static class RngRangePatternUses
{
    /// <summary>The sliceable, unindexable sequence.</summary>
    public static readonly RngSliceable Sliceable = new(1, 2, 3, 4);

    /// <summary>The sequence whose <c>Slice</c> is one of three.</summary>
    public static readonly RngSliceOverloads Overloaded = new();

    /// <summary>
    /// 18.4.3 — both bounds given, one of them from the end: the form whose desugaring needs
    /// the countable property.
    /// </summary>
    /// <returns>The interior.</returns>
    public static RngSliceable Interior() => Sliceable[1..^1];

    /// <summary>
    /// 18.4.3 — the bare range, whose argument list is two characters and which binds two
    /// declarations: <c>Slice</c> and <c>Count</c>.
    /// </summary>
    /// <returns>A copy of the whole sequence.</returns>
    public static RngSliceable All() => Sliceable[..];

    /// <summary>18.4.3 — no start, so the slice begins at zero.</summary>
    /// <returns>The first two elements.</returns>
    public static RngSliceable Head() => Sliceable[..2];

    /// <summary>18.4.3 — no end, so the slice runs to the countable property's answer.</summary>
    /// <returns>Everything from the second element on.</returns>
    public static RngSliceable Tail() => Sliceable[1..];

    /// <summary>18.4.3 — a <c>Range</c> arriving as a value, with no <c>..</c> at the site.</summary>
    /// <param name="range">What to take.</param>
    /// <returns>That part of the sequence.</returns>
    public static RngSliceable Part(System.Range range) => Sliceable[range];

    /// <summary>
    /// 18.4.3 — the range access over the overload set. This binds
    /// <c>RngSliceOverloads.Slice(int, int)</c>, whose identity string carries an ordinal the
    /// two other overloads decide.
    /// </summary>
    /// <returns>Two elements out of the middle.</returns>
    public static int[] Middle() => Overloaded[1..3];

    /// <summary>
    /// 18.4.3 — all three overloads called by name, so each has a use site that resolves by
    /// argument list rather than by shape. A query that answers `Middle` correctly and these
    /// correctly is a query whose ordinals agree with the compiler's.
    /// </summary>
    /// <returns>The three results' lengths added up.</returns>
    public static int Named() =>
        Overloaded.Slice(1).Length
        + Overloaded.Slice(1, 2).Length
        + Overloaded.Slice(1L, 2L).Length
        + Overloaded.Length
        + Sliceable.Slice(0, 1).Count
        + Sliceable.ElementAt(0);
}
