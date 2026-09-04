// Clause 18.3 — the `Range` type: a start `Index`, an end `Index`, the `..` operator that
// builds one, and the ability to declare an indexer taking one. As with 18.2 the type's own
// members are metadata and the declaration the clause requires is the indexer, so the census
// row says both.
//
// The `..` token does not bind to one thing. Which member it names is decided by which
// operand is *absent*:
//
//     1..2   ->  System.Range..ctor(Index, Index)
//     ..2    ->  System.Range.EndAt(Index)
//     1..    ->  System.Range.StartAt(Index)
//     ..     ->  System.Range.All  — a property getter
//
// So `Written()` below is one expression holding references to four distinct declarations,
// selected by missing syntax, and the bare `..` is a use site with no identifier tokens at all
// that refers to a property.
//
// `GetOffsetAndLength` carries the other trap. It returns `(int Offset, int Length)`, and a
// tuple element's `ISymbol.Name` is `Offset` while its `MetadataName` is `Item1`.
// `ScipSymbols.Name` uses `ISymbol.Name`, so `Offsets()` and `Unnamed()` below reference the
// same underlying field under two different spellings, and a find-references join across the
// two sites answers nothing. The containing type collapses too: `ValueTuple` is the
// `ISymbol.Name` of every arity, so the two-element and three-element tuples here mint one
// `ValueTuple#` descriptor between them. Both are metadata-only, so both merge silently
// rather than refusing a write — which is exactly why they belong in this project and not in
// a quarantine one.

using System;

namespace Surface.Ranges;

/// <summary>
/// The declaration 18.3's third bullet requires: an indexer whose parameter type is
/// <c>Range</c> (18.3).
/// </summary>
/// <remarks>
/// One indexer, as everywhere in this project. `RngIndexed` declares the <c>Index</c> one, in
/// its own type for that reason alone.
/// </remarks>
public sealed class RngRanged
{
    private readonly int[] _items = { 3, 1, 4, 1, 5 };

    /// <summary>How many elements there are.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// An indexer taking a <c>Range</c> directly, applied by hand with
    /// <c>GetOffsetAndLength</c> — the arithmetic the clause says implicit support performs.
    /// </summary>
    /// <param name="range">What to take.</param>
    /// <returns>A new sequence over those elements.</returns>
    public RngRanged this[Range range]
    {
        get
        {
            var (offset, length) = range.GetOffsetAndLength(Length);

            return new RngRanged(_items[offset..(offset + length)]);
        }
    }

    /// <summary>An empty sequence.</summary>
    public RngRanged()
    {
    }

    /// <summary>Builds a sequence over the values given.</summary>
    /// <param name="items">The values.</param>
    private RngRanged(int[] items) => _items = items;
}

/// <summary>
/// Every member of <c>System.Range</c> reachable from source, and every operand-omission form
/// of the <c>..</c> operator (18.3).
/// </summary>
public static class RngRangeUses
{
    /// <summary>The type whose indexer takes a <c>Range</c>.</summary>
    public static readonly RngRanged Ranged = new();

    /// <summary>
    /// 18.3 — every written form of the operator in one array, so the four-way binding is
    /// visible in a single expression: both operands, from-end operands, no end, no start,
    /// and neither.
    /// </summary>
    /// <returns>Seven ranges, referring to four different declarations.</returns>
    public static Range[] Written() => new[] { 0..4, 0..^1, ^2..^0, ..4, .., 1.., ^2.. };

    /// <summary>
    /// 18.3 — the constructor, named, which mints the same symbol as <c>1..2</c> at a site
    /// that spells it.
    /// </summary>
    /// <returns>A range over everything.</returns>
    public static Range Constructed() => new Range(0, ^0);

    /// <summary>
    /// 18.3 — the three static members the omission forms bind to, named: <c>All</c>,
    /// <c>StartAt</c> and <c>EndAt</c>.
    /// </summary>
    /// <returns>The three ranges the bare, start-only and end-only forms produce.</returns>
    public static (Range All, Range From, Range To) Statics() =>
        (Range.All, Range.StartAt(1), Range.EndAt(^1));

    /// <summary>18.3 — <c>Start</c> and <c>End</c>, the two <c>Index</c> values a range holds.</summary>
    /// <param name="range">Any range.</param>
    /// <returns>Its bounds.</returns>
    public static (Index Start, Index End) Bounds(Range range) => (range.Start, range.End);

    /// <summary>
    /// 18.3 — <c>GetOffsetAndLength</c> read through its element *names*, which is the
    /// spelling `Offset` and `Length`.
    /// </summary>
    /// <param name="range">Any range.</param>
    /// <param name="length">The length to resolve against.</param>
    /// <returns>Where the slice starts and how long it is.</returns>
    public static int Offsets(Range range, int length)
    {
        var resolved = range.GetOffsetAndLength(length);

        return resolved.Offset + resolved.Length;
    }

    /// <summary>
    /// 18.3 — the same two fields of the same constructed tuple type, read through their
    /// metadata names because this tuple was written without element names. One declaration,
    /// two spellings.
    /// </summary>
    /// <param name="range">Any range.</param>
    /// <param name="length">The length to resolve against.</param>
    /// <returns>Where the slice starts and how long it is.</returns>
    public static int Unnamed(Range range, int length)
    {
        (int, int) resolved = range.GetOffsetAndLength(length);

        return resolved.Item1 + resolved.Item2;
    }

    /// <summary>
    /// 18.3 — a three-element tuple beside the two-element one above, so both arities of
    /// <c>ValueTuple</c> are referenced from one file.
    /// </summary>
    /// <param name="range">Any range.</param>
    /// <param name="length">The length to resolve against.</param>
    /// <returns>The offset, the length, and the end.</returns>
    public static (int Offset, int Length, int End) Three(Range range, int length)
    {
        var (offset, taken) = range.GetOffsetAndLength(length);

        return (offset, taken, offset + taken);
    }

    /// <summary>
    /// 18.3 — the deconstruction form, which dodges the spelling problem entirely because a
    /// local is given no global symbol by design.
    /// </summary>
    /// <param name="range">Any range.</param>
    /// <param name="length">The length to resolve against.</param>
    /// <returns>Where the slice ends.</returns>
    public static int Deconstructed(Range range, int length)
    {
        var (offset, taken) = range.GetOffsetAndLength(length);

        return offset + taken;
    }

    /// <summary>18.3 — <c>Equals</c>, both overloads, as for <c>Index</c>.</summary>
    /// <param name="left">One range.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they agree.</returns>
    public static bool Same(Range left, Range right) =>
        left.Equals(right) && left.Equals((object)right);

    /// <summary>
    /// 18.3 — a range applied to the type that declares a <c>Range</c> indexer, so the
    /// element access binds to a member and not to a <c>Slice</c> method.
    /// </summary>
    /// <returns>The interior of the sequence.</returns>
    public static RngRanged Interior() => Ranged[1..^1];

    /// <summary>
    /// 18.3 — a <c>Range</c> that arrives as a value, so the indexer is reached with no
    /// <c>..</c> at the site.
    /// </summary>
    /// <param name="range">What to take.</param>
    /// <returns>That part of the sequence.</returns>
    public static RngRanged Part(Range range) => Ranged[range];

    /// <summary>
    /// 18.3 — a range in every other position a value can take: an array element, a field, a
    /// nullable local, and an argument.
    /// </summary>
    /// <returns>How many of the ranges start at the beginning.</returns>
    public static int Positions()
    {
        Range[] ranges = { .., 1.., ..1 };
        Range? absent = null;
        var seen = 0;

        foreach (var range in ranges)
        {
            if (range.Start.Equals(Index.Start))
            {
                seen++;
            }
        }

        return absent is null ? seen : seen + 1;
    }
}
