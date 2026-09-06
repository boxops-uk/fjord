// Clause 11.2.7 — the discard pattern: `_`.
//
// It matches everything, declares nothing and references nothing, which makes it the one
// pattern form an index is *right* to hold no fact about. It is here so that "no rows" can
// be asserted rather than assumed: `_` appears twelve times below — eleven as a discard
// pattern or a discard designation, which name nothing and bind nothing, and once as the
// discard expression of a deconstructing assignment, which is not a pattern at all. A
// walk that mistook any of the eleven for a name would have eleven unresolved names.
//
// Two things about it do not compile, and are recorded here because a corpus that only
// holds what compiles cannot say where the edge is:
//
//   * `input is _` — CS0246. As the whole pattern of an `is` expression, `_` is read as a
//     type name, so the discard pattern is not admitted there at all.
//   * `case _:` — CS0103. Nor is it admitted as a switch *statement*'s case label; the
//     switch *expression*'s `_ =>` arm below is the only place a bare `_` may stand as a
//     complete pattern.

using System;

namespace Surface.Patterns;

/// <summary>
/// The discard pattern of 11.2.7 in each position that admits it, and the discard
/// designation of 11.2.2 and 11.2.4 beside it.
/// </summary>
public static class PatDiscardPatterns
{
    /// <summary>
    /// The discard as a <c>switch</c> expression's final arm, which is the only position a
    /// bare <c>_</c> may be a complete pattern.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>What the input is.</returns>
    public static string Name(object? input) => input switch
    {
        PatCircle => "circle",
        PatSquare => "square",
        null => "nothing",
        _ => "other",
    };

    /// <summary>
    /// The discard as a property pattern's subpattern: the member is read and thrown away.
    /// </summary>
    /// <remarks>
    /// <c>{ Tag: _ }</c> is not the same as <c>{ }</c>: the member is still looked up, so
    /// the <i>reference</i> to <c>Tag</c> is real and only the value is discarded. A
    /// nullable member makes the difference visible — this matches a blob whatever its tag,
    /// including none.
    /// </remarks>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Whether there is a weighed thing at all.</returns>
    public static bool HasTagSlot(PatWeighedBase weighed) => weighed is { Tag: _ };

    /// <summary>The discard as a positional pattern's subpattern, in both slots.</summary>
    /// <param name="square">The pattern input value.</param>
    /// <returns>Whether the square deconstructs, which it always does.</returns>
    public static bool Deconstructs(PatSquare square) => square is (_, _);

    /// <summary>
    /// The discard in one slot of a positional pattern, with a constant in the other.
    /// </summary>
    /// <param name="square">The pattern input value.</param>
    /// <returns>Whether the square is labelled as one, whatever its side.</returns>
    public static bool AnySizeSquare(PatSquare square) => square is (_, "square");

    /// <summary>The discard as an element of a list pattern, and again inside a slice.</summary>
    /// <param name="run">The pattern input value.</param>
    /// <returns>Whether the run has at least three elements.</returns>
    public static bool AtLeastThree(PatRun run) => run is [_, _, _, ..];

    /// <summary>
    /// The discard <b>designation</b> of a declaration pattern: <c>is int _</c>, which tests
    /// the type and names nothing.
    /// </summary>
    /// <remarks>
    /// Written <c>is int _</c> rather than <c>is int</c> to keep the designation in the
    /// corpus: the two mean the same thing, and only one of them has a
    /// <c>DiscardDesignation</c> node for a walk to trip over.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a boxed integer.</returns>
    public static bool IsInteger(object input) => input is int _;

    /// <summary>
    /// The discard designation of a var pattern: <c>is var _</c>, which tests nothing.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Always true, which is what 11.2.4 promises.</returns>
    public static bool Always(object? input) => input is var _;

    /// <summary>
    /// The discard in a nested pattern, where it stands for a whole subtree.
    /// </summary>
    /// <param name="crate">The crate to look inside.</param>
    /// <returns>Whether the crate holds anything at all.</returns>
    public static bool HoldsSomething(PatCrate crate) => crate is { Held: not null and _ };

    /// <summary>
    /// A deconstructing <b>assignment</b> with a discard, which is not a pattern and is here
    /// to be told apart from one.
    /// </summary>
    /// <remarks>
    /// <c>(var label, _) = blob;</c> is a deconstruction (clause 12), not a positional
    /// pattern: it declares by declaration syntax, and its <c>_</c> is a discard
    /// <i>expression</i> whose symbol is a discard rather than nothing at all. The two
    /// spellings look alike and reach the index by different routes, which is the reason to
    /// have both in one corpus.
    /// </remarks>
    /// <param name="blob">The blob to take apart.</param>
    /// <returns>The blob's label.</returns>
    public static string LabelOf(PatBlob blob)
    {
        (var label, _) = blob;

        return label;
    }
}
