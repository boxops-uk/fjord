// Clause 11.2.4 — the var pattern: `var designation`.
//
// It always matches, so it tests nothing; all it does is declare. That makes it the purest
// case of the problem clause 11 poses: a declaration whose only syntax is an identifier in
// an expression, whose type is inferred from the input rather than written, and which the
// compiler is free to give a null value even where the input's type says otherwise.

using System;
using System.Collections.Generic;

namespace Surface.Patterns;

/// <summary>
/// The var pattern of 11.2.4 in each of its positions, including the two designations it
/// admits — a single variable and a parenthesised list.
/// </summary>
public static class PatVarPatterns
{
    /// <summary>
    /// A field whose simple name a var pattern below also declares.
    /// </summary>
    /// <remarks>
    /// The hazard of 11.2.4. A pattern variable may take the name of a field of the
    /// enclosing type — there is no shadowing rule against it, unlike the enclosing-block
    /// rule that stops two sibling <c>if</c>s from doing the same. So <c>_depth</c> names
    /// two declarations in this file: a static field, which has a global name, a container
    /// and a declaration node, and a pattern local, which has none of the three. A query
    /// keyed on the simple name finds one thing; the compilation has two.
    /// </remarks>
    private static int _depth;

    /// <summary>
    /// The plain form: <c>o is var held</c>, which matches anything, including null.
    /// </summary>
    /// <remarks>
    /// 11.2.4's inference is worth stating: <c>held</c> has the <i>static</i> type of the
    /// input, so it is <c>object?</c> here and not the run-time type — a var pattern
    /// narrows nothing, and a reader who expects it to has read a declaration pattern.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>The input's type name, or a stand-in.</returns>
    public static string TypeNameOf(object? input) =>
        input is var held && held is not null ? held.GetType().Name : "null";

    /// <summary>
    /// A var pattern declaring the same simple name as <see cref="_depth"/>.
    /// </summary>
    /// <remarks>
    /// Inside this method every <c>_depth</c> is the local; the field is unreachable by
    /// simple name here, and is read by <see cref="Depth"/> instead. The two are one string
    /// and two declarations.
    /// </remarks>
    /// <param name="crate">The crate to measure.</param>
    /// <returns>How deep the crate nests.</returns>
    public static int Measure(PatCrate? crate)
    {
        if (crate is var _depth && _depth is not null)
        {
            return _depth.Nested is null ? 1 : 2;
        }

        return 0;
    }

    /// <summary>The field, read where no pattern has taken its name.</summary>
    /// <returns>The recorded depth.</returns>
    public static int Depth() => _depth;

    /// <summary>Sets the field, so that its own reference rows are not all reads.</summary>
    /// <param name="value">The depth to record.</param>
    public static void Record(int value) => _depth = value;

    /// <summary>
    /// A var pattern as a <c>switch</c> expression's catch-all, with a guard — which is the
    /// idiom it exists for, since <c>_</c> cannot be guarded and read at once.
    /// </summary>
    /// <param name="value">The pattern input value.</param>
    /// <returns>A bucket for the value.</returns>
    public static string Bucket(int value) => value switch
    {
        0 => "zero",
        var negative when negative < 0 => "negative",
        var large when large > PatConstants.Limit => "large",
        var other => other.ToString(),
    };

    /// <summary>
    /// A var pattern nested in a property pattern — the form in which it is most often
    /// written, because there it is the only way to name a member's value.
    /// </summary>
    /// <param name="shape">The shape to look at.</param>
    /// <returns>The length of the shape's label.</returns>
    public static int LabelLength(PatShape shape) =>
        shape is { Label: var label } ? label.Length : 0;

    /// <summary>
    /// The <b>parenthesised designation</b>: <c>var (start, count)</c>, which is a var
    /// pattern that declares two variables and binds a deconstruction to do it.
    /// </summary>
    /// <remarks>
    /// This is 11.2.4 and 11.2.5 at once, and it is the reason the two clauses are hard to
    /// tell apart in an index: there is no <c>var</c> type to reference, no member name
    /// written, and two declarations — and the <c>Deconstruct</c> it reaches for is the
    /// record's, which the compiler wrote.
    /// </remarks>
    /// <param name="extent">The extent to take apart.</param>
    /// <returns>One past the end of the extent.</returns>
    public static int EndOf(PatExtent extent) =>
        extent is var (start, count) ? start + count : 0;

    /// <summary>
    /// A nested parenthesised designation, so a var pattern declares three variables at two
    /// depths.
    /// </summary>
    /// <param name="pair">A pair of extents.</param>
    /// <returns>The span the two extents together cover.</returns>
    public static int SpanOf((PatExtent First, int Gap) pair) =>
        pair is var ((start, count), gap) ? start + count + gap : 0;

    /// <summary>
    /// The <c>var _</c> designation: a var pattern that declares nothing.
    /// </summary>
    /// <remarks>
    /// 11.2.4 admits a discard designation, which makes <c>var _</c> and the discard pattern
    /// <c>_</c> of 11.2.7 mean the same thing in different words. Neither leaves a
    /// declaration behind; only one of them is spelled with a keyword.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input matched, which it always did.</returns>
    public static bool Anything(object? input) => input is var _;

    /// <summary>
    /// A var pattern whose designation is never read, beside one that is.
    /// </summary>
    /// <param name="values">The values to count.</param>
    /// <returns>How many values there are.</returns>
    public static int CountOf(IReadOnlyCollection<int> values)
    {
        // `unread` is a declaration by 11.2.4 with no use anywhere, so nothing in the file
        // points at it and nothing outside the compiler knows it was declared.
        if (values is var unread)
        {
            return values.Count;
        }

        return 0;
    }
}
