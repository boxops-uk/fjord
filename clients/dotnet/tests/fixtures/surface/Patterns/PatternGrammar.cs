// Clause 11.2.1 — patterns in general: the grammar, "applicable to", and evaluation order.
//
// The clause says three things, and only the first leaves anything behind. The *grammar*
// decides where a pattern may be written, and a pattern can be written in far more places
// than the `is` and `switch` it is usually seen in — a field initialiser, a constructor
// initialiser's argument, a query clause, an argument of a `new`. Each of those positions
// puts a pattern's names somewhere a walk decides what a name means from its *ancestors*,
// so the position can change the answer without changing the pattern.
//
// "Applicable to" and evaluation order leave nothing. Whether a pattern can match a type is
// a compile-time check that either succeeds silently or is an error — `"text" is int` is
// CS8121 and never reaches a corpus — and the left-to-right, short-circuiting order in
// which subpatterns run is observable at run time and recorded nowhere.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Surface.Patterns;

/// <summary>
/// Every grammatical position that admits a pattern, one method each, plus the two
/// witnesses for what 11.2.1's other two paragraphs leave behind.
/// </summary>
public static class PatGrammarPositions
{
    /// <summary>
    /// A pattern in a <b>field initialiser</b>, which runs before any constructor body and
    /// declares a pattern variable in a scope with no enclosing statement.
    /// </summary>
    public static readonly bool StaticallyKnown = "circle" is string opening && opening.Length > 3;

    /// <summary>A pattern in an <c>is</c> expression — the position it is named after.</summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Whether the input is a shape.</returns>
    public static bool InIsExpression(object input) => input is PatShape;

    /// <summary>A pattern in a <c>switch</c> statement's case label.</summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A number for the input.</returns>
    public static int InSwitchStatement(object input)
    {
        switch (input)
        {
            case PatCircle:
                return 1;

            default:
                return 0;
        }
    }

    /// <summary>A pattern in a <c>switch</c> expression's arm.</summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A number for the input.</returns>
    public static int InSwitchExpression(object input) => input switch
    {
        PatCircle => 1,
        _ => 0,
    };

    /// <summary>
    /// A pattern in a <c>when</c> guard, which is a pattern inside a pattern's clause.
    /// </summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A number for the input.</returns>
    public static int InWhenClause(object input) => input switch
    {
        PatCrate crate when crate.Held is PatCircle => 2,
        PatCrate => 1,
        _ => 0,
    };

    /// <summary>
    /// A pattern in a <c>while</c> condition, so the pattern runs once per iteration.
    /// </summary>
    /// <param name="items">What to walk.</param>
    /// <returns>How many circles the walk saw.</returns>
    public static int InWhileCondition(IEnumerator<object> items)
    {
        var seen = 0;

        while (items.MoveNext() && items.Current is not null)
        {
            if (items.Current is PatCircle)
            {
                seen++;
            }
        }

        return seen;
    }

    /// <summary>A pattern in the operands of <c>&amp;&amp;</c> and <c>||</c>.</summary>
    /// <param name="left">The first pattern input value.</param>
    /// <param name="right">The second pattern input value.</param>
    /// <returns>Whether the pair is interesting.</returns>
    public static bool InBooleanOperands(object left, object right) =>
        (left is PatCircle || left is PatSquare) && right is not null;

    /// <summary>A pattern in a conditional expression's condition.</summary>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A label for the input.</returns>
    public static string InConditional(object input) =>
        input is PatShape shape ? shape.Label : "none";

    /// <summary>
    /// A pattern in an <b>argument of an object creation</b>.
    /// </summary>
    /// <remarks>
    /// The hazard of 11.2.1, and it is about position rather than about the pattern. What a
    /// name in source is *doing* — a type reference, a construction, a call, a read — is
    /// most cheaply decided by looking at the name's ancestors, and every name inside this
    /// expression has a <c>new</c> above it. So <c>PatCircle</c> here is the same type
    /// reference as <c>PatCircle</c> in <see cref="InIsExpression(object)"/> and sits under
    /// an object creation that has nothing to do with it. A query that counts type
    /// references by what they are doing must still find this one.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>A box holding one if the input is a circle.</returns>
    public static PatBox<int> InObjectCreationArgument(object input) =>
        new PatBox<int>(input is PatCircle ? 1 : 0);

    /// <summary>
    /// A pattern in a <b>constructor initialiser's</b> argument, which runs before the
    /// constructor body and before any field initialiser of the derived type.
    /// </summary>
    public sealed class Seeded
    {
        /// <param name="seed">What to start from.</param>
        public Seeded(int seed) => Seed = seed;

        /// <param name="input">The input, matched in the initialiser argument.</param>
        public Seeded(object input)
            : this(input is PatBlob { Tag: var tag } ? tag : 0)
        {
        }

        /// <summary>What the instance started from.</summary>
        public int Seed { get; }

        /// <summary>A pattern in a <b>property accessor body</b>.</summary>
        public bool Positive => Seed is > 0;
    }

    /// <summary>A pattern in a <b>query expression's</b> <c>where</c> clause.</summary>
    /// <remarks>
    /// The pattern's input is a range variable, which is the one kind of name in C# that is
    /// neither a local, a parameter nor a member — so the pattern's input value here has no
    /// definition an index models, even though the pattern itself is ordinary.
    /// </remarks>
    /// <param name="items">What to filter.</param>
    /// <returns>The large circles, in order.</returns>
    public static IEnumerable<object> InQueryClause(IEnumerable<object> items) =>
        from item in items
        where item is PatCircle { Radius: > 1.0 }
        select item;

    /// <summary>A pattern in a <b>lambda body</b>, which is a pattern in a nested scope.</summary>
    /// <returns>A predicate over shapes.</returns>
    public static Func<object, bool> InLambdaBody() =>
        input => input is PatSquare { Kind: PatKind.Boxed };

    /// <summary>A pattern in a <b>local function</b>, and one in the method around it.</summary>
    /// <param name="items">What to weigh.</param>
    /// <returns>The total weight.</returns>
    public static double InLocalFunction(IEnumerable<object> items)
    {
        var total = 0.0;

        foreach (var item in items)
        {
            total += Weigh(item);
        }

        return total;

        double Weigh(object item) => item is PatShape shape ? shape.Extent : 0.0;
    }

    /// <summary>
    /// A pattern nested inside another pattern to three levels, which is the grammar's
    /// recursion and the reason 11.2.1 is a clause rather than a sentence.
    /// </summary>
    /// <param name="crate">The crate to look inside.</param>
    /// <returns>The radius three levels down, or zero.</returns>
    public static double DeeplyNested(PatCrate crate) =>
        crate is { Nested: { Nested: { Held: PatCircle { Radius: var radius } } } } ? radius : 0.0;

    /// <summary>
    /// The evaluation order of 11.2.1, made observable: subpatterns run left to right and
    /// stop at the first that fails.
    /// </summary>
    /// <remarks>
    /// <see cref="PatCounter.Next"/> increments when it is read. The first pattern below
    /// fails on <c>Steady</c> before <c>Next</c> is reached, so the counter does not move;
    /// the second reads <c>Next</c> first and it does. Both patterns name both members, and
    /// nothing in an index distinguishes them — the order is in the run, not in the record.
    /// </remarks>
    /// <param name="counter">The witness.</param>
    /// <returns>How many times the counter was read.</returns>
    public static int EvaluationOrder(PatCounter counter)
    {
        _ = counter is { Steady: -1, Next: > 0 };
        _ = counter is { Next: > 0, Steady: -1 };

        return counter.Reads;
    }

    /// <summary>
    /// What "applicable to" (11.2.1) forbids, written down because it cannot be written in
    /// code.
    /// </summary>
    /// <remarks>
    /// A pattern must be applicable to the input's static type, so <c>"text" is int</c> is
    /// CS8121 and <c>1 is string</c> is too. The check below is the nearest legal
    /// neighbour: the input is <c>object</c>, every pattern is applicable to it, and the
    /// answer is decided at run time. A corpus can hold this and not the error, which is
    /// why the error is named here instead.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Which of the two applicable patterns matched.</returns>
    public static string Applicable(object input) => input switch
    {
        int => "integer",
        string => "text",
        _ => "neither",
    };
}
