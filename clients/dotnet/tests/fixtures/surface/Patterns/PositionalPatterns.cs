// Clause 11.2.5 — the positional pattern: `T ( subpattern, ... )`.
//
// This is the pattern form with the largest gap between what it does and what it says. A
// positional pattern binds a *member* — `Deconstruct`, or `ITuple`'s length and indexer, or
// a tuple's elements — and writes no name for it at all. The subpattern count is the whole
// of the selection: two `Deconstruct` overloads that differ only in arity are told apart by
// counting commas.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Patterns;

/// <summary>
/// Every way a positional pattern of 11.2.5 can find its members: two <c>Deconstruct</c>
/// overloads, an extension <c>Deconstruct</c>, a compiler-written one, <see cref="ITuple"/>
/// and a tuple type.
/// </summary>
public static class PatPositionalPatterns
{
    /// <summary>
    /// One subpattern, which binds <c>PatCircle.Deconstruct(out double)</c>.
    /// </summary>
    /// <remarks>
    /// A one-subpattern positional pattern needs the type written — without it,
    /// <c>(var r)</c> is a parenthesised pattern rather than a deconstruction, which is
    /// 11.2.1's grammar doing the disambiguation.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>The circle's radius, or zero.</returns>
    public static double RadiusOf(object input) =>
        input is PatCircle(var radius) ? radius : 0.0;

    /// <summary>
    /// Two subpatterns, which binds <c>PatCircle.Deconstruct(out double, out string)</c> —
    /// the <b>other</b> overload, chosen by the comma.
    /// </summary>
    /// <remarks>
    /// The hazard of 11.2.5. Both overloads are named <c>Deconstruct</c>, both are declared
    /// on <see cref="PatCircle"/>, and the two patterns that pick them are separated by one
    /// token. Whatever tells the two declarations apart has to be a signature, because the
    /// name and the container are the same for both — and whatever answers "what does this
    /// pattern use" has to reach a member that the pattern never names.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A description of the circle, or nothing.</returns>
    public static string DescribeCircle(object input) =>
        input is PatCircle(var radius, var label) ? $"{label} of {radius}" : "none";

    /// <summary>
    /// A positional pattern with a <b>constant</b> subpattern, so the deconstruction runs
    /// and only one of its outputs is tested.
    /// </summary>
    /// <param name="square">The square to test.</param>
    /// <returns>Whether the square is labelled as one.</returns>
    public static bool IsLabelledSquare(PatSquare square) => square is (_, "square");

    /// <summary>
    /// A positional pattern binding an <b>extension</b> <c>Deconstruct</c>, declared in
    /// <see cref="PatBlobDeconstruction"/> and not on the type at all.
    /// </summary>
    /// <remarks>
    /// The member this reaches is not a member of <see cref="PatBlob"/>, so answering "what
    /// does this pattern bind" from the input's type alone answers nothing. There is still
    /// no name at the use site.
    /// </remarks>
    /// <param name="blob">The blob to take apart.</param>
    /// <returns>The blob's label and tag, rendered.</returns>
    public static string Split(PatBlob blob) =>
        blob is (var label, var tag) ? $"{label}:{tag}" : "none";

    /// <summary>
    /// A positional pattern over a <c>record</c>, whose <c>Deconstruct</c> the compiler
    /// wrote.
    /// </summary>
    /// <remarks>
    /// The member bound here exists and was never written down: it has no source location,
    /// so a definition-location question about it has no answer that is a position in a
    /// file. That is not a defect in the walk — it is a member with nowhere to point.
    /// </remarks>
    /// <param name="extent">The extent to take apart.</param>
    /// <returns>Whether the extent starts at the origin and is short.</returns>
    public static bool IsShortFromOrigin(PatExtent extent) => extent is (0, < 4);

    /// <summary>
    /// A positional pattern with a designation on the whole, so the pattern both
    /// deconstructs and declares.
    /// </summary>
    /// <param name="extent">The extent to take apart.</param>
    /// <returns>The extent, if it starts at the origin.</returns>
    public static PatExtent? FromOrigin(PatExtent extent) =>
        extent is (0, _) atOrigin ? atOrigin : null;

    /// <summary>
    /// A positional pattern reached through <see cref="ITuple"/>, which is how a type with
    /// no <c>Deconstruct</c> is still deconstructable.
    /// </summary>
    /// <remarks>
    /// <see cref="PatPair"/> implements <see cref="ITuple"/> explicitly, so the length and
    /// the indexer this binds are not on its public surface. Note also what does
    /// <i>not</i> compile: adding a designation — <c>pair is (1, _) whole</c> — takes the
    /// <see cref="ITuple"/> path away and asks for a <c>Deconstruct</c> instead (CS8129).
    /// The designation is written on the record above, where there is one.
    /// </remarks>
    /// <param name="pair">The pair to test.</param>
    /// <returns>Whether the pair leads with one.</returns>
    public static bool LeadsWithOne(PatPair pair) => pair is (1, _);

    /// <summary>
    /// The same pattern applied to an <c>object</c>, where <see cref="ITuple"/> is found on
    /// the run-time type rather than the static one.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a pair of one and "a".</returns>
    public static bool IsOneAndA(object input) => input is (1, "a");

    /// <summary>
    /// A positional pattern over a <b>tuple type</b>, whose elements are not members at all.
    /// </summary>
    /// <param name="pair">The tuple to test.</param>
    /// <returns>What the tuple is.</returns>
    public static string TupleKind((int Count, string Label) pair) => pair switch
    {
        (0, _) => "empty",
        (1, "circle") => "one circle",
        (var count, var label) when count > 1 => $"{count} of {label}",
        _ => "some",
    };

    /// <summary>
    /// A positional pattern <b>nested</b> in another, so one pattern binds two
    /// deconstructions at two depths.
    /// </summary>
    /// <param name="pair">A tuple holding an extent.</param>
    /// <returns>Whether the held extent starts at the origin.</returns>
    public static bool NestedAtOrigin((PatExtent Extent, int Gap) pair) =>
        pair is ((0, var count), var gap) && count > gap;

    /// <summary>
    /// A positional pattern with a property pattern after it, which is 11.2.5 and 11.2.6 in
    /// one pattern and the only place the two are written together.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a big circle labelled as one.</returns>
    public static bool IsBigLabelledCircle(object input) =>
        input is PatCircle(var radius) { Label: PatConstants.RoundLabel } && radius > 1.0;

    /// <summary>
    /// A positional pattern in a <c>switch</c> statement's case label, with a guard, so the
    /// deconstruction's outputs are read by the guard.
    /// </summary>
    /// <param name="shape">The shape to rank.</param>
    /// <returns>A rank for the shape.</returns>
    public static int Rank(PatShape shape)
    {
        switch (shape)
        {
            case PatCircle(var radius, _) when radius > 1.0:
                return 3;

            case PatCircle(var radius):
                return radius > 0.0 ? 2 : 1;

            case PatSquare(var side, _):
                return side > 0.0 ? 2 : 1;

            default:
                return 0;
        }
    }
}
