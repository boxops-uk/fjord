// Clause 11.2's grammar as the language actually has it: the forms added after the
// standard's clause 11 was written.
//
// The type pattern, the relational patterns, the logical patterns `and`/`or`/`not`, the
// parenthesised pattern, and the list and slice patterns are all *pattern forms* — they
// belong in 11.2 beside the declaration and property patterns, and a corpus for clause 11
// that stopped at 11.2.7 would leave the grammar half-covered. They are recorded on their
// own census rows under the post-standard features, so this file is here for the reader and
// for the shape of the surface rather than to claim those rows.
//
// The two that matter most to an index are the list and slice patterns, because their
// bindings are the least visible in the whole language: `[1, 2]` binds a `Length` or a
// `Count` and an indexer, `[.. var rest]` binds a `Slice` or a range indexer, and the
// pattern writes none of the three names.

using System;
using System.Collections.Generic;

namespace Surface.Patterns;

/// <summary>
/// The type, relational, logical and parenthesised patterns.
/// </summary>
public static class PatLogicalForms
{
    /// <summary>
    /// The <b>type pattern</b>: a declaration pattern with the designation left off, which
    /// references a type and declares nothing.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a shape.</returns>
    public static bool IsShape(object input) => input is PatShape;

    /// <summary>The four <b>relational patterns</b>, over a value type.</summary>
    /// <param name="value">The pattern input value.</param>
    /// <returns>A band for the value.</returns>
    public static string Band(int value) => value switch
    {
        < 0 => "negative",
        <= PatConstants.Limit => "small",
        > 100 => "large",
        >= 8 => "middling",
    };

    /// <summary>
    /// A relational pattern over a <c>double</c> and over a <c>char</c>, so the form is not
    /// only exercised on <c>int</c>.
    /// </summary>
    /// <param name="radius">A radius.</param>
    /// <param name="initial">An initial.</param>
    /// <returns>Whether the pair is in range.</returns>
    public static bool InRange(double radius, char initial) =>
        radius is > 0.0 and < 100.0 && initial is >= 'a' and <= 'z';

    /// <summary>
    /// The three <b>logical patterns</b>, including a <c>not</c> around a property pattern
    /// and an <c>or</c> over two type patterns.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>What the input is, roughly.</returns>
    public static string Sort(object? input) => input switch
    {
        not null and (PatCircle or PatSquare) => "a shape with corners known",
        PatBlob and not { Tag: 0 } => "a tagged blob",
        string or char or int => "a scalar",
        not null => "something else",
        _ => "nothing",
    };

    /// <summary>
    /// A <b>parenthesised pattern</b>, which exists so that <c>and</c> and <c>or</c> can be
    /// grouped against their precedence.
    /// </summary>
    /// <remarks>
    /// <c>and</c> binds tighter than <c>or</c>, so the parentheses below change the meaning:
    /// without them the first operand of the <c>or</c> would be
    /// <c>PatCircle and { Label: "circle" }</c> and the second a bare
    /// <c>PatSquare</c> — which is what the second method says explicitly.
    /// </remarks>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>Whether the shape is a labelled one of either kind.</returns>
    public static bool LabelledEither(PatShape shape) =>
        shape is (PatCircle or PatSquare) and { Label.Length: > 3 };

    /// <summary>The same pattern without the parentheses, which means something else.</summary>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>Whether the shape is a labelled circle or any square.</returns>
    public static bool LabelledCircleOrAnySquare(PatShape shape) =>
        shape is PatCircle and { Label.Length: > 3 } or PatSquare;

    /// <summary>
    /// A <c>not null</c> pattern, which is the most-written pattern in C# and declares
    /// nothing.
    /// </summary>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>The shape's label, or a stand-in.</returns>
    public static string LabelOr(PatShape? shape) =>
        shape is not null ? shape.Label : "none";
}

/// <summary>
/// The list and slice patterns, and the four kinds of member they bind without naming.
/// </summary>
public static class PatListForms
{
    /// <summary>
    /// A <b>list pattern</b> over an array, which binds the array's length and its element
    /// access — neither of which is a member the pattern could have named.
    /// </summary>
    /// <param name="values">The pattern input value.</param>
    /// <returns>Whether the array is exactly one, two, three.</returns>
    public static bool IsOneTwoThree(int[] values) => values is [1, 2, 3];

    /// <summary>A list pattern with a <b>slice</b>, and a designation on the slice.</summary>
    /// <remarks>
    /// The slice binds a range indexer on an array; on <see cref="PatRun"/> below it binds a
    /// <c>Slice</c> method instead, and the pattern is written the same way both times.
    /// </remarks>
    /// <param name="values">The pattern input value.</param>
    /// <returns>The sum of everything after the first element.</returns>
    public static int TailSum(int[] values)
    {
        // Written as a positive test on purpose: `is not [_, .. var tail]` does not
        // compile, because a `not` pattern may not declare — which is the same rule that
        // keeps `or` from declaring, and one of the few places the grammar of 11.2 forbids
        // a designation outright.
        if (values is [_, .. var tail])
        {
            var total = 0;

            foreach (var value in tail)
            {
                total += value;
            }

            return total;
        }

        return 0;
    }

    /// <summary>
    /// A list pattern over a type that supplies <c>Length</c>, an indexer and
    /// <c>Slice</c> itself.
    /// </summary>
    /// <remarks>
    /// <see cref="PatRun"/>'s three members are what this pattern binds, and the pattern
    /// names none of them. This is the widest gap in the language between what a construct
    /// references and what it writes: three members reached, zero names at the use site.
    /// </remarks>
    /// <param name="run">The pattern input value.</param>
    /// <returns>What the run looks like.</returns>
    public static string Shape(PatRun run) => run switch
    {
        [] => "empty",
        [var only] => $"one: {only}",
        [var first, var second] => $"two: {first},{second}",
        [1, .. var middle, 9] => $"bracketed by {middle.Length}",
        [.., var last] => $"ends with {last}",
    };

    /// <summary>
    /// A list pattern over a <see cref="List{T}"/>, which binds <c>Count</c> rather than
    /// <c>Length</c> — the other of the two names a list pattern will accept.
    /// </summary>
    /// <remarks>
    /// No slice here: <see cref="List{T}"/> has no <c>Slice</c> and no range indexer, so
    /// <c>[.. var rest]</c> does not compile against one. The nearest legal pattern is a
    /// fixed length, which is what this is.
    /// </remarks>
    /// <param name="values">The pattern input value.</param>
    /// <returns>Whether the list holds exactly two ordered values.</returns>
    public static bool IsOrderedPair(List<int> values) =>
        values is [var low, var high] && low < high;

    /// <summary>A list pattern with a <b>nested</b> pattern in an element position.</summary>
    /// <param name="shapes">The pattern input value.</param>
    /// <returns>Whether the shapes lead with a big circle.</returns>
    public static bool LeadsWithBigCircle(PatShape[] shapes) =>
        shapes is [PatCircle { Radius: > 1.0 }, ..];

    /// <summary>A list pattern nested inside a list pattern.</summary>
    /// <param name="rows">The pattern input value.</param>
    /// <returns>Whether the first row leads with zero.</returns>
    public static bool FirstRowLeadsWithZero(int[][] rows) => rows is [[0, ..], ..];

    /// <summary>
    /// A <b>constant pattern against a span</b>, which is C# 11's one pattern that compares
    /// a value type to a string constant.
    /// </summary>
    /// <param name="text">The pattern input value.</param>
    /// <returns>Whether the span reads as the round label.</returns>
    public static bool SpanIsRoundLabel(ReadOnlySpan<char> text) => text is PatConstants.RoundLabel;
}
