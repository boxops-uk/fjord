// Clause 18.4 — pattern-based implicit support for `Index` and `Range`. The heading's own
// normative text is 18.4.1 to 18.4.3; what belongs to the heading is the property it names:
// the binding is by *shape*. A type that declares a countable property, an `int` indexer and
// a `Slice(int, int)` supports `[^1]` and `[..]` with nothing at the use site pointing at any
// of the three.
//
// The identity consequence is that an ordinary member becomes load-bearing retroactively.
// Rename `RngPatternBoth.Count` to `Total` and `Pattern[^1]` below stops compiling — with no
// edit at the use site, and no text there from which a new identity could be re-derived.
// Delete the last named call to `Slice` and its only remaining use is spelled `[..]`, so a
// dead-member query keyed on written references reports a live member as unused: wrong in the
// dangerous direction.
//
// This type uses `Count` where `RngSeq` (clause 18) uses `Length`, which is the other half of
// 18.1's tie-break: with no `Length` present the countable property is still found. Which one
// was found is recorded in one place only — `IImplicitIndexerReferenceOperation.LengthSymbol`
// — and `GetSymbolInfo` at the same span answers the indexer or `Slice`, never the countable
// property. So a producer keyed on symbol info cannot write that half of the binding at all.

namespace Surface.Ranges;

/// <summary>
/// One type conforming to both patterns at once, through <c>Count</c> rather than
/// <c>Length</c> (18.4).
/// </summary>
public sealed class RngPatternBoth
{
    private readonly int[] _items;

    /// <summary>Builds a sequence over the values given.</summary>
    /// <param name="values">The values.</param>
    public RngPatternBoth(params int[] values) => _items = values;

    /// <summary>
    /// The countable property, named <c>Count</c>. Nothing in this type's own code depends on
    /// the name; every element access in <see cref="RngPatternUses"/> does.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>The <c>int</c> indexer the from-end form rewrites onto.</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The element there.</returns>
    public int this[int index] => _items[index];

    /// <summary>The slice method the range form rewrites onto.</summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>A new sequence over those elements.</returns>
    public RngPatternBoth Slice(int start, int length) => new(_items[start..(start + length)]);
}

/// <summary>
/// Both patterns exercised from one declaration set, and the same members named directly
/// beside them so the difference between "unused" and "used without being spelled" is
/// measurable (18.4).
/// </summary>
public static class RngPatternUses
{
    /// <summary>The sequence every access below reads.</summary>
    public static readonly RngPatternBoth Pattern = new(2, 7, 1, 8);

    /// <summary>18.4 — implicit <c>Index</c> support, binding the indexer and <c>Count</c>.</summary>
    /// <returns>The last element.</returns>
    public static int Last() => Pattern[^1];

    /// <summary>18.4 — implicit <c>Range</c> support, binding <c>Slice</c> and <c>Count</c>.</summary>
    /// <returns>The whole sequence, copied.</returns>
    public static RngPatternBoth All() => Pattern[..];

    /// <summary>
    /// 18.4 — the from-start form, which is the ordinary indexer access and needs no
    /// countable property at all.
    /// </summary>
    /// <returns>The first element.</returns>
    public static int First() => Pattern[0];

    /// <summary>
    /// 18.4 — the three members named, which is the only place in this file where any of
    /// their names is written.
    /// </summary>
    /// <returns>What each answers.</returns>
    public static (int Count, int Element, RngPatternBoth Slice) Named() =>
        (Pattern.Count, Pattern[1], Pattern.Slice(1, 2));
}
