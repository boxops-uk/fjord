// Clause 18.2 — the `Index` type. The clause requires three things of a conforming
// implementation: a type `System.Index`, an implicit conversion from `int` to it, and the
// ability to *declare* an indexer whose parameter is an `Index`. The first two are metadata
// and the third is the only declaration in the clause, which is why the census row says both.
//
// Everything named in this file that is not `RngIndexed` resolves into the core library, and
// the corpus declares not one member of it. So the package coordinate is the identity, and
// `ScipSymbols.Package` builds it out of `ContainingAssembly.Identity` — `System.Runtime` at
// one version under a reference pack, `System.Private.CoreLib` for a compilation over the
// runtime assemblies. One source file, several symbol strings for `Index.GetOffset`, and no
// declaration anywhere in this corpus that matches any of them.
//
// Two shapes here have no name at the use site at all:
//
//   * `^1` is not an operator invocation. It is a `PrefixUnaryExpressionSyntax` of kind
//     `IndexExpression` whose symbol is `System.Index..ctor(int, bool)` — the same symbol
//     `new Index(1, true)` below mints at a site that spells it out.
//   * `Index i = 0;` is a *user-defined conversion*, `System.Index.op_Implicit(int)`, bound at
//     a use site consisting of the token `0`. No node named `op_Implicit` exists to hang it
//     on.

using System;

namespace Surface.Ranges;

/// <summary>
/// The declaration 18.2's third bullet requires: an indexer whose parameter type is
/// <c>Index</c>, so a from-end access binds to a member rather than to the pattern (18.2).
/// </summary>
/// <remarks>
/// One indexer. Adding `this[int]` beside it would make this type the fixture for 18.4.1's
/// fallthrough order — the indexer wins over the pattern — but the two would mint one
/// identity string and the run would refuse the second write. `RngRangePrecedence` in
/// `RngFallthrough.cs` demonstrates the same rule on the range side, where the competing
/// pattern member is a *method* and the collision does not arise.
/// </remarks>
public sealed class RngIndexed
{
    private readonly int[] _items = { 3, 1, 4, 1, 5 };

    /// <summary>How many elements there are.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// An indexer taking an <c>Index</c> directly, applied by hand with
    /// <c>GetOffset</c> — which is what the clause says implicit support would have generated.
    /// </summary>
    /// <param name="index">Where to read, from either end.</param>
    /// <returns>The element there.</returns>
    public int this[Index index] => _items[index.GetOffset(Length)];
}

/// <summary>
/// Every member of <c>System.Index</c> reachable from source, and every syntax that produces
/// one without naming it (18.2).
/// </summary>
public static class RngIndexUses
{
    /// <summary>The type whose indexer takes an <c>Index</c>.</summary>
    public static readonly RngIndexed Indexed = new();

    /// <summary>
    /// 18.2 — the <c>^</c> operator, whose symbol is <c>Index</c>'s two-argument constructor.
    /// </summary>
    /// <returns>An index one from the end.</returns>
    public static Index FromEnd() => ^1;

    /// <summary>
    /// 18.2 — the same constructor, named. This mints the identical symbol string as
    /// <see cref="FromEnd"/> at a site that actually spells it, which is what makes a
    /// double-count possible for anyone fixing the missed reference above.
    /// </summary>
    /// <returns>The same index, constructed explicitly.</returns>
    public static Index Constructed() => new Index(1, true);

    /// <summary>
    /// 18.2 — the implicit conversion from <c>int</c>, which is a user-defined conversion
    /// operator bound at a literal.
    /// </summary>
    /// <returns>A from-start index that was written as a number.</returns>
    public static Index Converted()
    {
        Index index = 0;

        return index;
    }

    /// <summary>18.2 — the conversion again, at an argument position rather than an assignment.</summary>
    /// <returns>The first element, reached by passing an <c>int</c> where an <c>Index</c> is wanted.</returns>
    public static int ConvertedArgument() => Indexed[0];

    /// <summary>18.2 — <c>GetOffset</c>, the member the clause defines the whole type in terms of.</summary>
    /// <param name="index">Any index.</param>
    /// <param name="length">The length to resolve it against.</param>
    /// <returns>The from-start offset.</returns>
    public static int Offset(Index index, int length) => index.GetOffset(length);

    /// <summary>
    /// 18.2 — <c>Value</c> and <c>IsFromEnd</c>, the two properties that make an <c>Index</c>
    /// readable without a length.
    /// </summary>
    /// <param name="index">Any index.</param>
    /// <returns>What it holds and which end it counts from.</returns>
    public static (int Value, bool FromEnd) Parts(Index index) => (index.Value, index.IsFromEnd);

    /// <summary>
    /// 18.2 — the two static factories, which are the named spellings of <c>0</c> and
    /// <c>^0</c>, and the two static properties beside them.
    /// </summary>
    /// <returns>Four indices that name what the operators do not.</returns>
    public static (Index Start, Index End, Index Third, Index ThirdLast) Factories() =>
        (Index.Start, Index.End, Index.FromStart(3), Index.FromEnd(3));

    /// <summary>
    /// 18.2 — <c>Equals(Index)</c>, the strongly typed half of <c>IEquatable&lt;Index&gt;</c>,
    /// and the inherited <c>object.Equals</c> beside it so both overloads are bound.
    /// </summary>
    /// <param name="left">One index.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they agree, twice over.</returns>
    public static bool Same(Index left, Index right) =>
        left.Equals(right) && left.Equals((object)right);

    /// <summary>
    /// 18.2 — a from-end index applied to the type that declares an <c>Index</c> indexer, so
    /// the element access binds to a member with a parameter list rather than to a pattern.
    /// </summary>
    /// <returns>The last element.</returns>
    public static int Last() => Indexed[^1];

    /// <summary>
    /// 18.2 — an <c>Index</c> that arrives as a value, so the indexer is reached with no
    /// <c>^</c> and no conversion at the site.
    /// </summary>
    /// <param name="index">Where to read.</param>
    /// <returns>The element there.</returns>
    public static int At(Index index) => Indexed[index];

    /// <summary>
    /// 18.2 — an <c>Index</c> in every other position a type can appear in: a field, a
    /// parameter's default, an array's element type, and a nullable local.
    /// </summary>
    /// <returns>How many of the indices count from the end.</returns>
    public static int Positions()
    {
        Index[] indices = { 0, ^1, Index.Start };
        Index? absent = null;
        var seen = 0;

        foreach (var index in indices)
        {
            if (index.IsFromEnd)
            {
                seen++;
            }
        }

        return absent is null ? seen : seen + 1;
    }
}
