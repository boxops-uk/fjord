// Clause 18.4.1 — the resolution order. `E[I]` with an `Index` or a `Range` index is tried as
// an array access, then a string access, then an indexer access, and only then as the
// pattern. The clause introduces no member of its own: it decides which declaration — every
// one of them declared under 18.1 — a use site binds to. So the census row says reference.
//
// One syntax, four resolutions, and two of them cannot be written as a reference at all:
//
//   * `Numbers[^1]` and `Numbers[1..^1]` are array accesses. `GetSymbolInfo` returns *null*
//     (the operation is an `ArrayElementReference`), so a producer that adds the missing
//     `ElementAccessExpressionSyntax` case and treats a null symbol as unresolved will
//     increment its unresolved tally on perfectly legal code, poisoning the number that is
//     supposed to signal real defects.
//   * `Text[1..^1]` binds `string.Substring(int, int)`, whose identity string is
//     `System/String#Substring(+1).` — the token `Substring` appears nowhere in this file, so
//     a reference written for that span spells a name no character of the source contains,
//     and it carries an ordinal decided by an overload set in metadata the corpus never
//     declares.
//     `Text[^1]` binds `string.this[int]`, which is the collapsing `this[]` descriptor again,
//     now for a type nobody here wrote.
//
// The fourth arm of the order — an indexer winning over a conforming pattern — is written here
// on the *range* side only. `RngRangePrecedence` declares `this[Range]` while also being
// countable and sliceable, so `[1..2]` binds the indexer and `Slice` is never reached from an
// element access, and the competing member is a method whose descriptor cannot collide with
// the indexer's. The index side of the same rule needs `this[Index]` beside `this[int]` in one
// type — two indexers, one identity string, a refused write — and is quarantined.

using System;

namespace Surface.Ranges;

/// <summary>
/// Countable, indexable by <c>Range</c> *and* sliceable: the indexer wins, so <c>Slice</c> is
/// live code with no element access that reaches it (18.4.1).
/// </summary>
public sealed class RngRangePrecedence
{
    private readonly int[] _items = { 1, 2, 3, 4, 5 };

    /// <summary>The countable property, which the pattern would have needed.</summary>
    public int Count => _items.Length;

    /// <summary>
    /// The declared indexer, which is tried before the pattern and therefore always wins.
    /// </summary>
    /// <param name="range">What to take.</param>
    /// <returns>Those elements.</returns>
    public int[] this[Range range] => _items[range];

    /// <summary>
    /// The pattern member, which conforms perfectly and is never what a range access binds
    /// to. Its only callers are the ones that spell its name.
    /// </summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>Those elements.</returns>
    public int[] Slice(int start, int length) => _items[start..(start + length)];
}

/// <summary>
/// The same two expressions written over each of the four kinds of receiver the fallthrough
/// order distinguishes (18.4.1).
/// </summary>
public static class RngFallthrough
{
    /// <summary>The array arm's receiver.</summary>
    public static readonly int[] Numbers = { 3, 1, 4, 1, 5 };

    /// <summary>The string arm's receiver.</summary>
    public static readonly string Text = "surface";

    /// <summary>The indexer arm's receiver — its only indexer takes an <c>Index</c>.</summary>
    public static readonly RngIndexed Indexed = new();

    /// <summary>The indexer arm's other receiver — its only indexer takes a <c>Range</c>.</summary>
    public static readonly RngRanged Ranged = new();

    /// <summary>The pattern arm's receiver, which declares an <c>int</c> indexer and a countable property.</summary>
    public static readonly RngSequence Pattern = new();

    /// <summary>The precedence receiver: a declared <c>Range</c> indexer beside a conforming <c>Slice</c>.</summary>
    public static readonly RngRangePrecedence Precedence = new();

    /// <summary>
    /// 18.4.1, first arm — array access. The index is applied by the language itself; there is
    /// no member and no symbol.
    /// </summary>
    /// <returns>The last number, and the interior of the array.</returns>
    public static (int Last, int[] Interior) ArrayArm() => (Numbers[^1], Numbers[1..^1]);

    /// <summary>
    /// 18.4.1, second arm — string access. The from-end form binds <c>string.this[int]</c>
    /// and the range form binds <c>string.Substring(int, int)</c>, a name this file does not
    /// contain.
    /// </summary>
    /// <returns>The last character, and the interior of the string.</returns>
    public static (char Last, string Interior) StringArm() => (Text[^1], Text[1..^1]);

    /// <summary>
    /// 18.4.1, third arm — indexer access, where a declared indexer takes the <c>Index</c> or
    /// the <c>Range</c> and no rewriting happens at all.
    /// </summary>
    /// <returns>What each declared indexer answered.</returns>
    public static (int Element, RngRanged Part) IndexerArm() => (Indexed[^1], Ranged[1..^1]);

    /// <summary>
    /// 18.4.1, fourth arm — the pattern, reached only because <see cref="RngSequence"/>
    /// declares no <c>Index</c> indexer for the third arm to have matched.
    /// </summary>
    /// <returns>The last element.</returns>
    public static int PatternArm() => Pattern[^1];

    /// <summary>
    /// 18.4.1 — the order stated as a choice: this receiver could be resolved by either the
    /// third arm or the fourth, and the third is what fires. <c>Slice</c> is not reached.
    /// </summary>
    /// <returns>The interior, through the declared indexer.</returns>
    public static int[] IndexerBeatsPattern() => Precedence[1..3];

    /// <summary>
    /// 18.4.1 — the loser of that choice, called by name. Without this call
    /// <see cref="RngRangePrecedence.Slice"/> would have no reference in the corpus at all,
    /// and a dead-member query would be right about it for the wrong reason.
    /// </summary>
    /// <returns>The same elements, through the method the pattern would have used.</returns>
    public static int[] TheLoser() => Precedence.Slice(1, 2);

    /// <summary>
    /// 18.4.1 — the arms again with an <c>Index</c> value rather than a <c>^</c> expression,
    /// which is the form the clause is written in terms of.
    /// </summary>
    /// <param name="index">Where to read.</param>
    /// <returns>One element from each receiver that admits a bare <c>Index</c>.</returns>
    public static (int Array, char String, int Indexer, int Pattern) ByValue(Index index) =>
        (Numbers[index], Text[index], Indexed[index], Pattern[index]);

    /// <summary>
    /// 18.4.1 — and with a <c>Range</c> value, which is where the string arm's rewriting onto
    /// a two-argument method is least visible.
    /// </summary>
    /// <param name="range">What to take.</param>
    /// <returns>One slice from each receiver that admits a bare <c>Range</c>.</returns>
    public static (int[] Array, string String, RngRanged Indexer, int[] Precedence) RangeByValue(Range range) =>
        (Numbers[range], Text[range], Ranged[range], Precedence[range]);
}
