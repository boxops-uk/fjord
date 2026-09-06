// Clause 18.1 — the definitions the rest of clause 18 is written in terms of: countable,
// indexable and sliceable types, a from-start and a from-end index, a range, a slice. The
// definitions themselves declare nothing; the members they are defined over are the countable
// property, the `int` indexer and `Slice(int, int)`, and this file declares them across an
// inheritance chain because that is where they are hardest to attribute.
//
// Three hazards live here, and all three are about a fact that cannot be attached to a
// declaration at all:
//
//   * `Leaf[1..^1]` binds `RngSliceableLeaf.Slice` and `RngCountableBase.Length` — one use
//     site, two target types, neither of them the type named at the use site, neither in the
//     same declaration as the other. A producer keying the target on the static receiver type
//     would spell the reference `RngSliceableLeaf#Length.` for a declaration whose only
//     symbol is `RngCountableBase#Length.`.
//   * `RngBothCounts` declares `Count` and `Length` side by side. `Length` wins, so `Count` is
//     never bound from an element access — two structurally identical declarations of which
//     exactly one is live, and nothing in either symbol records which.
//   * `RngHiddenCount.Length` is protected, so `this[^1]` compiles *inside* the type and the
//     identical expression outside it is CS1503. "Is a sequence" is a property of a use site
//     and not of a declaration, and any fact written once per declaration is wrong for one of
//     the two sites. The outside site is a compile error, so it exists in this comment only.

namespace Surface.Ranges;

/// <summary>18.1 — a countable type: it has an accessible <c>Length</c> or <c>Count</c>
/// property of type <c>int</c>, and nothing else.</summary>
public class RngCountableBase
{
    /// <summary>The countable property the whole chain inherits.</summary>
    public int Length => 4;
}

/// <summary>
/// 18.1 — a type is indexable when it is countable and has an accessible <c>int</c> indexer.
/// The countable half is inherited and the indexer is declared here, so neither type on its
/// own is indexable.
/// </summary>
public class RngIndexableMiddle : RngCountableBase
{
    /// <summary>The <c>int</c> indexer that makes the chain indexable.</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The index, so the type needs no storage.</returns>
    public int this[int index] => index;
}

/// <summary>
/// 18.1 — a type is sliceable when it is countable and has an accessible
/// <c>Slice(int, int)</c>. This leaf adds the slice; it inherits countability from its
/// grandparent and indexability from its parent.
/// </summary>
public sealed class RngSliceableLeaf : RngIndexableMiddle
{
    /// <summary>The slice method that makes the chain sliceable.</summary>
    /// <param name="index">Where the slice starts.</param>
    /// <param name="count">How long it is.</param>
    /// <returns>An array of that length.</returns>
    public int[] Slice(int index, int count) => new int[count];
}

/// <summary>
/// 18.1 — <c>Length</c> is preferred when a type has both. <c>Count</c> is declared here and
/// is never what an element access binds to.
/// </summary>
public sealed class RngBothCounts
{
    /// <summary>The countable property that loses the tie-break, in every context.</summary>
    public int Count => 9;

    /// <summary>The countable property that wins it.</summary>
    public int Length => 2;

    /// <summary>The indexer both of them could serve.</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The index.</returns>
    public int this[int index] => index;

    /// <summary>
    /// A from-end index inside the type, so the winning property is bound from somewhere.
    /// </summary>
    /// <returns>The last element, by whichever property the rule chose.</returns>
    public int Last() => this[^1];

    /// <summary>
    /// The loser, read by name, so that <c>Count</c> has one reference in the corpus and the
    /// difference between "unused" and "unused by the pattern" is measurable.
    /// </summary>
    /// <returns>What <see cref="Count"/> says.</returns>
    public int Counted() => Count;
}

/// <summary>
/// 18.1 — conformance is decided "in the same context", so a countable property that is not
/// accessible outside the type makes the type a sequence only inside it.
/// </summary>
public class RngHiddenCount
{
    /// <summary>The countable property, reachable only from inside the type and its heirs.</summary>
    protected int Length => 2;

    /// <summary>The indexer, which is public and useless from outside on its own.</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The index.</returns>
    public int this[int index] => index;

    /// <summary>
    /// A from-end index written where the countable property is accessible. The identical
    /// expression written outside this type does not compile.
    /// </summary>
    /// <returns>The last element.</returns>
    public int Last() => this[^1];
}

/// <summary>
/// The use sites for 18.1: the inheritance chain indexed and sliced from outside, and the
/// definitions of from-start, from-end and past-end index stated as code.
/// </summary>
public static class RngGeneral
{
    /// <summary>The middle of the chain — countable by inheritance, indexable here.</summary>
    public static readonly RngIndexableMiddle Middle = new();

    /// <summary>The leaf of the chain — countable, indexable and sliceable all by different types.</summary>
    public static readonly RngSliceableLeaf Leaf = new();

    /// <summary>
    /// 18.1 — a from-start index needs no countable property at all: it is the ordinary
    /// indexer access, and the only reason this compiles for a type with a protected
    /// <c>Length</c> is that no from-end index is involved.
    /// </summary>
    /// <returns>The first element of a type whose countability is hidden.</returns>
    public static int FromStart() => new RngHiddenCount()[0];

    /// <summary>
    /// 18.1 — a from-end index over the chain. Binds <c>RngIndexableMiddle.this[int]</c> and
    /// <c>RngCountableBase.Length</c>: two types, one expression, and neither name written.
    /// </summary>
    /// <returns>The last element.</returns>
    public static int FromEnd() => Middle[^1];

    /// <summary>
    /// 18.1 — a slice over the chain. Binds <c>RngSliceableLeaf.Slice</c> and
    /// <c>RngCountableBase.Length</c>, skipping the middle type entirely.
    /// </summary>
    /// <returns>The interior of the leaf.</returns>
    public static int[] Sliced() => Leaf[1..^1];

    /// <summary>
    /// 18.1 — a past-end index is a legal <c>Index</c> value and a run-time failure, which is
    /// why the clause defines it separately from the two that are in range.
    /// </summary>
    /// <returns>An index one past the end, unapplied.</returns>
    public static System.Index PastEnd() => ^0;

    /// <summary>
    /// 18.1 — an empty slice, whose start and end are the same offset. Legal, and the reason
    /// the clause says a range's bounds only have to be ordered.
    /// </summary>
    /// <returns>Nothing, sliced out of the middle.</returns>
    public static int[] Empty() => Leaf[2..2];

    /// <summary>
    /// 18.1 — the tie-break, from outside the type that declares both properties, so the
    /// binding is chosen in the widest context there is.
    /// </summary>
    /// <returns>The last element of a type with a <c>Count</c> and a <c>Length</c>.</returns>
    public static int TieBreak() => new RngBothCounts()[^1];
}
