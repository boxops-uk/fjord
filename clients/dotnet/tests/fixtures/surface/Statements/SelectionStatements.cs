// Clause 13.8.1 (selection statements in general) and 13.8.2 (the `if` statement).
//
// 13.8.1's content is that there are two selection statements and what "select one of a number
// of possible statements" means; both of them appear in `Both` below, in one member, so a
// reader looking for the clause finds the pair rather than a cross-reference.
//
// 13.8.2 is the `if` statement, whose whole surface is: a boolean condition, an
// embedded_statement, and an optional `else` with another. The interesting part is that the
// condition may *declare* — a pattern in an `if` condition puts its variable in the enclosing
// block's scope, not the branch's, which is why two sibling `if`s cannot both declare `t` and
// two switch sections can.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.8 — the two selection statements.</summary>
public static class StmtSelection
{
    /// <summary>
    /// Both selection statements, in one member.
    /// </summary>
    /// <remarks>
    /// 13.8.1. The `if` selects between two statements by a boolean; the `switch` selects
    /// among many by a value. Everything else in the clause is about those two.
    /// </remarks>
    /// <param name="gate">What to select on.</param>
    /// <returns>A label for what was selected.</returns>
    public static string Both(int gate)
    {
        string chosen;

        if (gate < 0)
        {
            chosen = "negative";
        }
        else
        {
            switch (gate)
            {
                case 0:
                    chosen = "zero";
                    break;

                default:
                    chosen = "positive";
                    break;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Every shape the <c>if</c> statement has.
    /// </summary>
    /// <remarks>
    /// 13.8.2. An `if` with no `else`; an `if`/`else`; an `else if` chain, which the grammar
    /// has no production for — it is an `if` in the `else` position, and the fact that the
    /// language has no `elif` is a fact about the *tree* a walk descends; and a dangling
    /// `else`, which binds to the nearest `if`.
    /// </remarks>
    /// <param name="gate">A number to test.</param>
    /// <param name="flag">A second condition.</param>
    /// <returns>A label for the branch taken.</returns>
    public static string EveryIfShape(int gate, bool flag)
    {
        string label = "none";

        // 13.8.2 — an `if` with no else.
        if (gate == 0)
        {
            label = "zero";
        }

        // 13.8.2 — an `if`/`else` with braces on both arms.
        if (gate > 0)
        {
            label += " positive";
        }
        else
        {
            label += " non-positive";
        }

        // 13.8.2 — an `else if` chain: three `if` statements nested through their `else`
        // branches, which is what the grammar makes of this and what a reader does not see.
        if (gate > 100)
        {
            label += " huge";
        }
        else if (gate > 10)
        {
            label += " big";
        }
        else if (gate > 1)
        {
            label += " small";
        }
        else
        {
            label += " tiny";
        }

        // 13.8.2 — a dangling `else`. It belongs to the inner `if`, and the indentation here
        // is deliberately the indentation that says so.
        if (flag)
        {
            if (gate < 0)
            {
                label += " flagged-negative";
            }
            else
            {
                label += " flagged-non-negative";
            }
        }

        return label;
    }

    /// <summary>
    /// An <c>if</c> whose condition declares.
    /// </summary>
    /// <remarks>
    /// 13.8.2 with clause 11. The pattern variable `text` is scoped to the *enclosing block*
    /// and not to the branch, so it is still in scope after the `if` and a second sibling
    /// `if` could not declare it again (CS0128). That is why the second condition below
    /// declares `number` and not `text`, and it is the reason clause 13.8.2 is worth a note
    /// in a corpus that is otherwise about statement forms.
    /// </remarks>
    /// <param name="input">Something to test.</param>
    /// <returns>What the input turned out to be.</returns>
    public static string ConditionDeclares(object? input)
    {
        if (input is string text)
        {
            return $"string of {text.Length}";
        }

        if (input is int number and > 0)
        {
            return $"positive {number}";
        }

        if (input is not null && input.GetHashCode() != 0)
        {
            return "something";
        }

        return "nothing";
    }
}
