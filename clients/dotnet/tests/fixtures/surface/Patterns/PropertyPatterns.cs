// Clause 11.2.6 — the property pattern: `T { member : subpattern, ... }`.
//
// The one pattern form that writes a member name at the use site, and writes it with no
// receiver: `{ Radius: > 1 }` names a property of whatever the input's static type turns
// out to be, resolved by the same lookup rules as a member access with none of the syntax.
// So it is both the pattern form an index can most nearly see and the one where a hidden
// member is most nearly invisible.

using System;

namespace Surface.Patterns;

/// <summary>
/// The property pattern of 11.2.6 binding a property, a field, an interface member, a
/// nullable value type, a nested member, and the two same-named properties a <c>new</c>
/// declaration puts in reach of one another.
/// </summary>
public static class PatPropertyPatterns
{
    /// <summary>
    /// The empty property pattern <c>{ }</c>, which names nothing and tests only for
    /// non-null.
    /// </summary>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>Whether there is a shape at all.</returns>
    public static bool IsSomething(PatShape? shape) => shape is { };

    /// <summary>One member, matched against a constant: <c>{ Label: "circle" }</c>.</summary>
    /// <param name="shape">The pattern input value.</param>
    /// <returns>Whether the shape calls itself a circle.</returns>
    public static bool CallsItselfRound(PatShape shape) => shape is { Label: "circle" };

    /// <summary>
    /// A member matched relationally, after a type: <c>PatCircle { Radius: &gt; 1 }</c>.
    /// </summary>
    /// <remarks>
    /// The type is what makes <c>Radius</c> resolvable at all — a property pattern's member
    /// name is looked up on the input's static type, so the same three characters bind
    /// nothing if the type is dropped.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a circle bigger than the unit circle.</returns>
    public static bool IsLarge(object input) => input is PatCircle { Radius: > 1.0 };

    /// <summary>
    /// Two members in one pattern, which is where the comma-separated list matters.
    /// </summary>
    /// <param name="square">The pattern input value.</param>
    /// <returns>Whether the square is a big boxed one.</returns>
    public static bool IsBigBox(PatSquare square) =>
        square is { Side: > 2.0, Kind: PatKind.Boxed };

    /// <summary>
    /// A property pattern binding a <b>field</b> rather than a property — 11.2.6 says
    /// "member", and a field is one.
    /// </summary>
    /// <param name="blob">The pattern input value.</param>
    /// <returns>Whether the blob is tagged past the limit.</returns>
    public static bool IsTagged(PatBlob blob) => blob is { Tag: > PatConstants.Limit };

    /// <summary>
    /// A property pattern binding a member declared on an <b>interface</b>, through an
    /// interface-typed input.
    /// </summary>
    /// <param name="noted">The pattern input value.</param>
    /// <returns>Whether the note is long enough to read.</returns>
    public static bool IsNoted(IPatNoted noted) => noted is { Note.Length: > 2 };

    /// <summary>
    /// A property pattern against a <b>nullable value type</b>, where the subpattern is a
    /// constant of the underlying type.
    /// </summary>
    /// <remarks>
    /// <c>{ Tag: 5 }</c> tests both that the nullable has a value and that the value is
    /// five, which is two tests written as one and a good reason not to read a property
    /// pattern as an equality check.
    /// </remarks>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Whether the tag is exactly five.</returns>
    public static bool IsTagFive(PatWeighedBase weighed) => weighed is { Tag: 5 };

    /// <summary>
    /// The same nullable member tested for absence, which is the <c>null</c> constant
    /// pattern of 11.2.3 in a subpattern position.
    /// </summary>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Whether the tag is missing.</returns>
    public static bool IsUntagged(PatWeighedBase weighed) => weighed is { Tag: null };

    /// <summary>
    /// A <b>nested</b> property pattern: <c>{ Inner: { Weight: 3 } }</c>, which resolves two
    /// member names at two depths against two different types.
    /// </summary>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Whether the inner weight is three.</returns>
    public static bool InnerWeighsThree(PatWeighedDerived weighed) =>
        weighed is { Inner: { Weight: 3 } };

    /// <summary>
    /// The same test written as an <b>extended property pattern</b>:
    /// <c>{ Inner.Weight: 3 }</c>.
    /// </summary>
    /// <remarks>
    /// C# 10's spelling of the nesting above. The member names are now written as a member
    /// access with no receiver, which is a different syntax for the same two bindings — so
    /// anything that records member accesses sees this one and not
    /// <see cref="InnerWeighsThree(PatWeighedDerived)"/>, though the two mean the same thing.
    /// </remarks>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Whether the inner weight is three.</returns>
    public static bool InnerWeighsThreeExtended(PatWeighedDerived weighed) =>
        weighed is { Inner.Weight: 3 };

    /// <summary>
    /// The hazard of 11.2.6: <b>two properties with one simple name</b>, and only the
    /// input's static type to tell them apart.
    /// </summary>
    /// <remarks>
    /// <see cref="PatWeighedDerived"/> declares <c>new int Weight</c> over
    /// <see cref="PatWeighedBase"/>'s. Both patterns below are written <c>Weight</c>, at two
    /// spans, in one method; the first binds the derived property and the second binds the
    /// base one, because the cast changed the static type and nothing else. Two
    /// declarations, one name, and the disambiguating information is in the receiver
    /// expression rather than in the pattern.
    /// </remarks>
    /// <param name="weighed">The pattern input value.</param>
    /// <returns>Which weight matched, if either.</returns>
    public static string WhichWeight(PatWeighedDerived weighed)
    {
        var hiding = weighed is { Weight: 1 };
        var hidden = (PatWeighedBase)weighed is { Weight: 2 };

        return (hiding, hidden) switch
        {
            (true, true) => "both",
            (true, false) => "derived",
            (false, true) => "base",
            _ => "neither",
        };
    }

    /// <summary>
    /// A property pattern in a <c>switch</c> expression, with a declaration pattern nested
    /// inside a subpattern — so one arm both references a member and declares a variable.
    /// </summary>
    /// <param name="crate">The crate to look inside.</param>
    /// <returns>What the crate holds.</returns>
    public static string Holding(PatCrate crate) => crate switch
    {
        { Held: PatCircle circle } => $"circle of {circle.Radius}",
        { Held: PatSquare square } => $"square of {square.Side}",
        { Held: null, Kind: PatKind.Unknown } => "empty and unlabelled",
        { Held: null } => "empty",
        _ => "something",
    };

    /// <summary>
    /// A property pattern whose subpattern is a <c>var</c> pattern, which is 11.2.4 reached
    /// through 11.2.6 and the only way to name a member's value in a pattern.
    /// </summary>
    /// <param name="crate">The crate to look inside.</param>
    /// <returns>The kind the crate declares, however deeply nested.</returns>
    public static PatKind KindOf(PatCrate crate) =>
        crate is { Nested: { Kind: var nested } } ? nested
            : crate is { Kind: var kind } ? kind
            : PatKind.Unknown;
}
