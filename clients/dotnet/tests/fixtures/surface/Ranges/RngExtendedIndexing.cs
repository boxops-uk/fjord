// Clause 18 — extended indexing and slicing, the clause heading. Its normative text is 18.1,
// and what belongs here is the shape the whole clause is about: one type that is countable,
// indexable and sliceable at once, and the two element accesses that reach all three of its
// members while naming none of them.
//
// Two facts an index must answer about this file, and today it answers neither:
//
//   * `Sequence[1..^1]` binds `RngSeq.Slice(int, int)`, and `Sequence[^1]` binds
//     `RngSeq.this[int]`. The walk has no `ElementAccessExpressionSyntax` case, so no
//     reference is written for either — the one fact the expression produces points at the
//     receiver, and both members read as dead.
//   * The countable property is worse than missed: it is not reachable from
//     `GetSymbolInfo` at all. `RngSeq.Length` appears only as
//     `IImplicitIndexerReferenceOperation.LengthSymbol`, so a producer that walks symbol
//     info can never write a reference to it, however many syntax cases it adds.
//   * The `^` and `..` tokens are not operators here. `^1` is a `PrefixUnaryExpressionSyntax`
//     whose symbol is `System.Index..ctor(int, bool)`, and `1..^1` is
//     `System.Range..ctor(Index, Index)` — constructors this producer writes only from object
//     creation syntax, so both are missed, and a fix must not double-count them against the
//     `new Index(…)` and `new Range(…)` calls in `RngIndexType.cs` and `RngRangeType.cs`
//     which mint the identical symbols at sites that do name them.

namespace Surface.Ranges;

/// <summary>
/// A sequence that satisfies both patterns at once: a countable property, an <c>int</c>
/// indexer, and a two-<c>int</c> <c>Slice</c> (18, 18.4).
/// </summary>
/// <remarks>
/// Exactly one indexer, and that is a constraint on the corpus rather than on the clause.
/// A second `this[...]` here would mint the same identity string as the first — a property
/// descriptor is `Name(symbol) + '.'` and every indexer's name is `this[]` — and two
/// declarations demanding one string is the refused write that ends an indexing run. The
/// clause's own precondition for 18.4.2 ("`E[0]` is valid and uses the same indexer") is
/// therefore not exercised here: see `README.md`.
/// </remarks>
public sealed class RngSeq
{
    private readonly int[] _items;

    /// <summary>Builds a sequence over a copy of the values given.</summary>
    /// <param name="values">The values.</param>
    public RngSeq(params int[] values) => _items = values;

    /// <summary>
    /// The countable property the clause requires — <c>Length</c> rather than <c>Count</c>,
    /// which is the half of 18.1's tie-break a <c>Count</c>-only type cannot show.
    /// </summary>
    public int Length => _items.Length;

    /// <summary>The <c>int</c> indexer implicit <c>Index</c> support rewrites onto (18.4.2).</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The element there.</returns>
    public int this[int index] => _items[index];

    /// <summary>The <c>Slice(int, int)</c> implicit <c>Range</c> support rewrites onto (18.4.3).</summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>A new sequence over those elements.</returns>
    public RngSeq Slice(int start, int length) => new(_items[start..(start + length)]);
}

/// <summary>
/// The use sites clause 18 is about: a from-end index, a range, and the two composed (18).
/// </summary>
public static class RngSeqUses
{
    /// <summary>The sequence every access below reads.</summary>
    public static readonly RngSeq Sequence = new(3, 1, 4, 1, 5);

    /// <summary>
    /// 18 — a from-end index. Binds <c>RngSeq.this[int]</c> and <c>RngSeq.Length</c>, and
    /// spells neither.
    /// </summary>
    /// <returns>The last element.</returns>
    public static int Last() => Sequence[^1];

    /// <summary>
    /// 18 — a range. Binds <c>RngSeq.Slice(int, int)</c> and <c>RngSeq.Length</c>, and spells
    /// neither.
    /// </summary>
    /// <returns>Everything but the ends.</returns>
    public static RngSeq Interior() => Sequence[1..^1];

    /// <summary>
    /// 18 — the two composed, which is the clause's own example: slice, then index the slice
    /// from its end. Four bindings, one written name.
    /// </summary>
    /// <returns>The last element of the interior.</returns>
    public static int LastOfInterior() => Sequence[1..^1][^1];

    /// <summary>
    /// 18 — the same two members reached by naming them, so the corpus holds a site where
    /// each *is* written. A find-references query that answers only these has silently lost
    /// every access above.
    /// </summary>
    /// <returns>The same two answers, spelled out.</returns>
    public static (int Last, RngSeq Interior) Named() =>
        (Sequence[Sequence.Length - 1], Sequence.Slice(1, Sequence.Length - 2));
}
