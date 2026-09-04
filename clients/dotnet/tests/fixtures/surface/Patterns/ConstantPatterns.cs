// Clause 11.2.3 — the constant pattern: `E is constant_expression`.
//
// It declares nothing and references whatever the constant expression names, which is the
// only pattern form whose reference is an *expression* rather than a type or a member name:
// a literal (nothing to reference), a const field, an enum member, a const local, or `null`.
// So this is the pattern form where the reference is written the most plainly and is
// therefore the easiest to get right — and the one that puts two same-named constants in
// reach of one another.

using System;

namespace Surface.Patterns;

/// <summary>
/// Every constant a pattern can be, and the two same-named constants a qualifier tells
/// apart (11.2.3).
/// </summary>
public static class PatConstantPatterns
{
    /// <summary>
    /// A literal constant pattern: nothing is referenced, because nothing is named.
    /// </summary>
    /// <param name="value">The pattern input value.</param>
    /// <returns>Whether the value is exactly one.</returns>
    public static bool IsOne(int value) => value is 1;

    /// <summary>A negative literal, which is a unary expression the compiler folds.</summary>
    /// <param name="value">The pattern input value.</param>
    /// <returns>Whether the value is minus one.</returns>
    public static bool IsMinusOne(int value) => value is -1;

    /// <summary>A string literal, and a character literal beside it.</summary>
    /// <param name="text">The pattern input value.</param>
    /// <returns>What the text is.</returns>
    public static string Classify(string text) => text switch
    {
        "circle" => "the round one",
        "square" => "the boxed one",
        "" => "empty",
        _ => "other",
    };

    /// <summary>
    /// A <c>bool</c> constant pattern, which is legal and reads oddly on purpose.
    /// </summary>
    /// <param name="flag">The pattern input value.</param>
    /// <returns>The flag, the long way round.</returns>
    public static bool IsSet(bool flag) => flag is true;

    /// <summary>
    /// The <c>null</c> constant pattern, and its negation — the one constant pattern that
    /// changes the compiler's null state for the input.
    /// </summary>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>The shape's label, or a stand-in.</returns>
    public static string LabelOf(PatShape? shape) =>
        shape is null ? "none" : shape.Label;

    /// <summary>
    /// A constant pattern naming a <c>const</c> field: <c>PatConstants.Limit</c>.
    /// </summary>
    /// <remarks>
    /// The name is written, so this is the constant pattern an index can see — a member
    /// access whose target is a constant field, at a span a reader can click.
    /// </remarks>
    /// <param name="value">The pattern input value.</param>
    /// <returns>Whether the value is at the limit.</returns>
    public static bool AtLimit(int value) => value is PatConstants.Limit;

    /// <summary>A constant pattern naming a <c>const string</c> field.</summary>
    /// <param name="label">The pattern input value.</param>
    /// <returns>Whether the label is the round one's.</returns>
    public static bool IsRoundLabel(string label) => label is PatConstants.RoundLabel;

    /// <summary>
    /// A constant pattern naming an <b>enum member</b>, in both an <c>is</c> and a
    /// <c>switch</c> — which is the same reference written twice at two spans.
    /// </summary>
    /// <param name="kind">The pattern input value.</param>
    /// <returns>A number for the kind.</returns>
    public static int Score(PatKind kind)
    {
        if (kind is PatKind.Unknown)
        {
            return 0;
        }

        return kind switch
        {
            PatKind.Round => 1,
            PatKind.Boxed => 2,

            // 11.1: an enum's declared members do not exhaust its value space, so the
            // catch-all is required even though every member above is covered.
            _ => -1,
        };
    }

    /// <summary>
    /// The hazard of 11.2.3: <b>two constants with one simple name</b>, told apart only by
    /// the type that qualifies them.
    /// </summary>
    /// <remarks>
    /// <see cref="PatThresholdDerived"/> declares <c>new const int Threshold</c> over
    /// <see cref="PatThresholdBase"/>'s. Both constant patterns below are written
    /// <c>Threshold</c>; they are two different fields, with two declarations and two
    /// values, and the only thing that separates them is the qualifier to the left of the
    /// dot. A reference recorded by name alone answers both with one field and the wrong
    /// value for one of them.
    /// </remarks>
    /// <param name="value">The pattern input value.</param>
    /// <returns>Which threshold the value sits on, if either.</returns>
    public static string Threshold(int value) => value switch
    {
        PatThresholdDerived.Threshold => "derived",
        PatThresholdBase.Threshold => "base",
        _ => "neither",
    };

    /// <summary>
    /// A constant pattern naming a <b>const local</b>, which is a constant with no global
    /// name at all.
    /// </summary>
    /// <remarks>
    /// The reference here can only be answered span-to-span: a local constant has no
    /// container to qualify it and no symbol worth minting, so "where is
    /// <c>quorum</c> declared" is a question about two positions in one file.
    /// </remarks>
    /// <param name="votes">The pattern input value.</param>
    /// <returns>Whether the vote count is exactly the quorum.</returns>
    public static bool AtQuorum(int votes)
    {
        const int quorum = 4;

        return votes is quorum;
    }

    /// <summary>
    /// The same const local in a <c>switch</c> statement, once as a constant pattern and
    /// once inside a relational pattern.
    /// </summary>
    /// <param name="votes">The pattern input value.</param>
    /// <returns>A description of the vote count.</returns>
    public static string Quorum(int votes)
    {
        const int quorum = 4;

        switch (votes)
        {
            case quorum:
                return "exactly";

            case < quorum:
                return "short";

            default:
                return "over";
        }
    }

    /// <summary>
    /// What is <b>not</b> a constant pattern, kept here so the boundary is written down.
    /// </summary>
    /// <remarks>
    /// 11.2.3 requires a constant expression. <see cref="PatConstants.NearLimit"/> is
    /// <c>static readonly</c> and so cannot be a pattern — <c>value is NearLimit</c> is
    /// CS0150 — and the field is read normally instead. The reference below is an ordinary
    /// field read at a span, indistinguishable from a constant pattern's reference unless
    /// something recorded which syntax it came from.
    /// </remarks>
    /// <param name="value">The value to test.</param>
    /// <returns>Whether the value is near the limit.</returns>
    public static bool NearLimit(int value) => value == PatConstants.NearLimit;
}
