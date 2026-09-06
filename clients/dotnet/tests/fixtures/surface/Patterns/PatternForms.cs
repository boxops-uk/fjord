// Clause 11.2 — pattern forms, all of them in one place.
//
// The clause is a container: its content is 11.2.1 to 11.2.7, and each of those has a file
// of its own here. What this file is for is the *set* — every form the language admits,
// written as arms of one switch expression, so that a reader can see the whole grammar at
// once and a query can be pointed at one member and told what it must find in it.
//
// It is also where the shape 11.2 is flagged for lives. Seven of the arms below declare a
// pattern variable named `form`. Each is a different type, each is scoped to its own arm,
// and none of them has a declaration node, a position in a member list, or a name a symbol
// table could hold. So one member contains seven declarations that answer to one string,
// and the only thing that separates them is a span.

using System;

namespace Surface.Patterns;

/// <summary>
/// Every pattern form of clause 11.2 — and the post-standard forms that joined them — as
/// arms of a single switch expression.
/// </summary>
public static class PatFormBoard
{
    /// <summary>
    /// Names the form that matched the input.
    /// </summary>
    /// <remarks>
    /// The arms are ordered so that none is subsumed by an earlier one, which is 11.1's
    /// rule and the reason the order here is not the order of the clauses.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Which pattern form matched.</returns>
    public static string Of(object? input) => input switch
    {
        // 11.2.3 — constant pattern, twice: `null` and a literal.
        null => "constant: null",
        0 => "constant: literal",

        // Post-standard — relational pattern, which needs a type in front of it because the
        // input is `object` and `>` is not applicable to one.
        int and > 100 => "relational",

        // 11.2.2 — declaration pattern. The first of seven `form`s.
        int form => $"declaration: {form}",

        // Post-standard — logical `or`, which may not declare, and `not`, which may not
        // either. Neither side of an `or` can introduce a name, so this arm has none.
        string or char => "logical: or",

        // Post-standard — logical `and` with a declaration on its right, which *is*
        // allowed: only `or` and `not` forbid a designation.
        not null and PatCircle form => $"logical: and, {form.Label}",

        // 11.2.5 — positional pattern, binding `PatSquare.Deconstruct`.
        PatSquare(var form, _) => $"positional: {form}",

        // 11.2.6 — property pattern, with 11.2.4's var pattern as its subpattern.
        PatBlob { Tag: var form } => $"property: {form}",

        // 11.2.5 again, over a record whose `Deconstruct` the compiler wrote.
        PatExtent(0, var form) => $"positional: record, {form}",

        // Post-standard — list pattern with a slice, reached through `and` because a type
        // may not be written in front of a list pattern.
        PatRun and [_, .. var form] => $"slice: {form.Length}",

        // Post-standard — parenthesised pattern, which changes nothing and is in the
        // grammar so that `and` and `or` can be grouped.
        (PatCounter or PatNoted) => "parenthesised",

        // 11.2.4 — var pattern with a guard, which is the last arm that can declare.
        var form when form.GetHashCode() is 0 => $"var: {form}",

        // 11.2.7 — discard pattern, which is only a complete pattern here.
        _ => "discard",
    };

    /// <summary>
    /// The same board as a <c>switch</c> statement, so that every form appears in a case
    /// label as well as in an arm.
    /// </summary>
    /// <remarks>
    /// Two spellings of one grammar. A case label and a switch arm are different syntax
    /// nodes holding the same pattern, and the pattern variables they declare are scoped
    /// differently — a section rather than an arm — so a corpus that held only one of the
    /// two would leave half of clause 11.2's declarations untested.
    /// </remarks>
    /// <param name="input">The pattern input value.</param>
    /// <returns>Which pattern form matched.</returns>
    public static string InLabels(object? input)
    {
        switch (input)
        {
            case null:
                return "constant: null";

            case 0:
                return "constant: literal";

            case int and > 100:
                return "relational";

            case int form:
                return $"declaration: {form}";

            case string or char:
                return "logical: or";

            case not null and PatCircle form:
                return $"logical: and, {form.Label}";

            case PatSquare(var form, _):
                return $"positional: {form}";

            case PatBlob { Tag: var form }:
                return $"property: {form}";

            case PatExtent(0, var form):
                return $"positional: record, {form}";

            case PatRun and [_, .. var form]:
                return $"slice: {form.Length}";

            case (PatCounter or PatNoted):
                return "parenthesised";

            case var form when form.GetHashCode() is 0:
                return $"var: {form}";

            default:
                return "discard";
        }
    }
}
