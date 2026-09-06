// Clause 11.2.2 — the declaration pattern: `E is T identifier`.
//
// It both *references* and *declares*: the type is a name written at the use site, and the
// designation is a local variable declared by a syntax node that is not a declaration
// statement, a parameter list or a member. Every other kind of local in C# is introduced by
// something a walk over declarations recognises; this one is introduced by a pattern.

using System;
using System.Collections.Generic;

namespace Surface.Patterns;

/// <summary>
/// Every form the declaration pattern of 11.2.2 takes, in every position that admits one.
/// </summary>
public static class PatDeclarationPatterns
{
    /// <summary>
    /// The plain form, in an <c>is</c> expression: <c>o is PatCircle circle</c>.
    /// </summary>
    /// <remarks>
    /// Two names are written and one is not. <c>PatCircle</c> is a type reference at the
    /// use site; <c>circle</c> is a declaration with no declaration syntax, and the only
    /// evidence of it in the file is the <i>use</i> on the right of the <c>&amp;&amp;</c>.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a circle bigger than the unit circle.</returns>
    public static bool IsLargeCircle(object input) =>
        input is PatCircle circle && circle.Radius > 1.0;

    /// <summary>
    /// The same pattern with a <b>constructed generic</b> type, which is the type reference
    /// worth having: <c>List&lt;int&gt;</c> is two names, and only one of them is the type
    /// the pattern narrows to.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>How many integers the input holds, or <c>-1</c>.</returns>
    public static int CountOfIntegers(object input) =>
        input is List<int> numbers ? numbers.Count : -1;

    /// <summary>
    /// The declaration pattern in a <c>switch</c> statement's case label, twice, with the
    /// <b>same designation name</b> in each section.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the hazard 11.2.2 is marked for. A pattern variable in a case label is
    /// scoped to its own switch <i>section</i>, so <c>held</c> below is three locals, of
    /// three types, in one method — and none of the three has a declaration node, a
    /// position in a member list, or a name a symbol table could hold. Anything that
    /// identifies a local by the pair (enclosing member, name) has one key for three
    /// declarations here.
    /// </para>
    /// <para>
    /// Sibling <c>if</c> statements cannot do this: a pattern variable in an
    /// <c>if</c> condition is scoped to the enclosing <i>block</i>, so a second
    /// <c>if (o is string held)</c> beside the first is CS0128. The switch section is what
    /// makes the shape legal, which is why it is written here and not there.
    /// </para>
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A description of the input.</returns>
    public static string Describe(object input)
    {
        switch (input)
        {
            case PatCircle held:
                return $"circle of {held.Radius}";

            case PatSquare held:
                return $"square of {held.Side}";

            case string held:
                return held;

            default:
                return "nothing";
        }
    }

    /// <summary>
    /// The declaration pattern with a <c>when</c> guard, and again without one, so that the
    /// same type appears in two sections and only the guard tells them apart.
    /// </summary>
    /// <remarks>
    /// 11.2.1's ordering is visible here and nowhere in the index: the guard runs only after
    /// the pattern has matched and the designation has been assigned, which is why the guard
    /// may read <c>blob</c> at all.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A rank for the input.</returns>
    public static int Rank(object input)
    {
        switch (input)
        {
            case PatBlob blob when blob.Tag > PatConstants.Limit:
                return 2;

            case PatBlob blob:
                return 1;

            default:
                return 0;
        }
    }

    /// <summary>
    /// The declaration pattern in a <c>switch</c> expression arm, where the designation is
    /// scoped to the arm.
    /// </summary>
    /// <param name="shape">The pattern input value, already narrowed to a shape.</param>
    /// <returns>The shape's extent, doubled for a square.</returns>
    public static double Weigh(PatShape shape) => shape switch
    {
        PatCircle narrowed => narrowed.Extent,
        PatSquare narrowed => narrowed.Extent * 2.0,
        PatBlob narrowed => narrowed.Tag,

        // 11.1: the arms above cover the whole closed hierarchy, and the compiler still
        // cannot prove it — `PatShape` is not sealed, so exhaustiveness needs this arm.
        _ => 0.0,
    };

    /// <summary>
    /// A declaration pattern against a <b>type parameter</b>, which is a name that resolves
    /// to a type parameter rather than to a type.
    /// </summary>
    /// <typeparam name="T">What to narrow to.</typeparam>
    /// <param name="input">The pattern input value.</param>
    /// <returns>The narrowed input, or nothing.</returns>
    public static T? AsShape<T>(object input)
        where T : PatShape =>
        input is T narrowed ? narrowed : null;

    /// <summary>
    /// A declaration pattern that unboxes: the type is a value type, and the designation is
    /// a copy rather than a reference.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>The boxed integer, or zero.</returns>
    public static int Unboxed(object input) => input is int number ? number : 0;

    /// <summary>
    /// A declaration pattern <b>nested</b> inside a property pattern, so the designation is
    /// declared two levels down from the <c>is</c>.
    /// </summary>
    /// <param name="crate">The crate to look inside.</param>
    /// <returns>The label of the held circle, or nothing.</returns>
    public static string? HeldCircleLabel(PatCrate crate) =>
        crate is { Held: PatCircle inner } ? inner.Label : null;

    /// <summary>
    /// A declaration pattern whose designation is <b>never read</b>.
    /// </summary>
    /// <remarks>
    /// The other half of 11.2.2's hazard, and the quieter half. A pattern variable reaches
    /// an index only through a use — there is no declaration node to walk — so this local
    /// exists in the compilation, is a genuine declaration by 11.2.2, and leaves nothing at
    /// all behind. It is not a wrong answer; it is a missing one, and it is invisible unless
    /// something counts what the compiler declared against what was recorded.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a blob.</returns>
    public static bool IsBlob(object input) => input is PatBlob ignored;

    /// <summary>
    /// The same test written without a designation at all — a <i>type</i> pattern, which is
    /// C# 9's spelling and declares nothing.
    /// </summary>
    /// <remarks>
    /// Kept beside <see cref="IsBlob(object)"/> deliberately: the two produce the same type
    /// reference and differ only in whether a local was declared, so a query that can tell
    /// them apart is a query that saw the designation.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a blob.</returns>
    public static bool IsBlobByType(object input) => input is PatBlob;
}
