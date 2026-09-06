using System;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Statements;

// The switch-label kinds, the three designation kinds, the two recursive-pattern clauses,
// and `DeclarationExpression`.
//
// A designation is where a pattern declares something, and none of the designation kinds is
// one of the six bases the walk switches on — so `o is SfLedgerRecord entry` declares a
// local the index holds no definition for while the `SfLedgerRecord` beside it is a
// reference the index does hold. `PositionalPatternClauseSyntax` is stronger still: it
// references a `Deconstruct` method, or a tuple's element fields, with no identifier
// anywhere in the clause.
//
// `DiscardDesignation` binds to a `SymbolKind.Discard`, which the walk drops before it
// writes anything and before it counts anything — the one identifier form that is neither a
// fact nor a miss.

/// <summary>A type whose <c>Deconstruct</c> the positional patterns reference.</summary>
public sealed class SfPatternSubject
{
    /// <summary>Which slot.</summary>
    public int Slot { get; init; }

    /// <summary>What is in it.</summary>
    public string Name { get; init; } = "unnamed";

    /// <summary>What the positional pattern clause calls, with no name in the clause itself.</summary>
    /// <param name="slot">Which slot.</param>
    /// <param name="name">What is in it.</param>
    public void Deconstruct(out int slot, out string name)
    {
        slot = Slot;
        name = Name;
    }
}

/// <summary>Every pattern-side declaration form.</summary>
public static class SfPatternForms
{
    /// <summary>
    /// CaseSwitchLabel, CasePatternSwitchLabel and DefaultSwitchLabel — the three labels a
    /// <c>switch</c> statement can carry, which are different kinds and not different
    /// spellings.
    /// </summary>
    /// <param name="total">What to classify.</param>
    /// <returns>A word for it.</returns>
    public static string Classify(int total)
    {
        const int Threshold = 10;

        object boxed = total;

        switch (boxed)
        {
            // CaseSwitchLabel — a constant, which is a reference to `Threshold` and not a
            // pattern at all.
            case Threshold:
                return "threshold";

            // CasePatternSwitchLabel with a `when` clause, whose pattern declares `counted`
            // through a SingleVariableDesignation.
            case int counted when counted > Threshold:
                return $"over:{counted}";

            // CasePatternSwitchLabel with no guard, and a DiscardDesignation instead of a
            // name, so the pattern declares nothing.
            case int _:
                return "under";

            // CasePatternSwitchLabel over a type declared in this corpus.
            case SfLedgerRecord entry:
                return entry.Label;

            // DefaultSwitchLabel.
            default:
                return "other";
        }
    }

    /// <summary>
    /// SingleVariableDesignation, DiscardDesignation and ParenthesizedVariableDesignation —
    /// the three ways a pattern or a deconstruction names what it matched.
    /// </summary>
    /// <param name="subject">What to match.</param>
    /// <returns>A rendering of what was named.</returns>
    public static string Designations(object subject)
    {
        // SingleVariableDesignation, in a declaration pattern.
        if (subject is SfPatternSubject named)
        {
            // ParenthesizedVariableDesignation, in a deconstructing declaration — and a
            // DeclarationExpression, since `var (…)` is an expression here and not a
            // statement's declaration.
            var (slot, label) = named;

            // DiscardDesignation inside a parenthesized one, so only half the deconstruction
            // is bound.
            var (_, onlyName) = named;

            return $"{slot}{label}{onlyName}";
        }

        // SingleVariableDesignation in a `var` pattern rather than a declaration pattern.
        if (subject is var anything)
        {
            // DeclarationExpression as an `out` argument, in both its named and discarded
            // forms.
            if (int.TryParse(anything?.ToString(), out var parsed)
                && int.TryParse("0", out int _))
            {
                return parsed.ToString();
            }
        }

        return "none";
    }

    /// <summary>
    /// PositionalPatternClause and PropertyPatternClause with its Subpatterns — the two
    /// clauses of a recursive pattern, which reference `Deconstruct` and a property
    /// respectively.
    /// </summary>
    /// <param name="subject">What to match.</param>
    /// <returns>A word for what matched.</returns>
    public static string Recursive(SfPatternSubject subject)
    {
        // A positional pattern clause with two subpatterns, which references
        // `SfPatternSubject.Deconstruct` — a call with no identifier in the syntax.
        if (subject is SfPatternSubject(1, "first"))
        {
            return "first";
        }

        // A positional pattern clause whose subpatterns declare, so the clause both
        // references and declares.
        if (subject is SfPatternSubject(var slot, var name) && slot > 1)
        {
            return $"{slot}:{name}";
        }

        // A property pattern clause, whose subpatterns each reference a property by name —
        // the one recursive-pattern form a `SimpleNameSyntax` dispatch can see.
        if (subject is { Slot: > 0, Name.Length: > 3 })
        {
            return "long";
        }

        // A property pattern clause with a nested one, and with a designation of its own.
        if (subject is SfPatternSubject { Name: { Length: 0 } } empty)
        {
            return empty.Slot.ToString();
        }

        // A positional pattern over a tuple, where the clause references the tuple's element
        // fields rather than a `Deconstruct` method.
        var pair = (Slot: subject.Slot, Name: subject.Name);
        return pair is (0, "") ? "zero" : "other";
    }

    /// <summary>
    /// The switch *expression*, whose arms are neither switch labels nor `case` clauses —
    /// the same patterns under different syntax kinds.
    /// </summary>
    /// <param name="subject">What to match.</param>
    /// <returns>A word for it.</returns>
    public static string Arms(object? subject) => subject switch
    {
        SfPatternSubject { Slot: 0 } => "origin",
        SfPatternSubject(var slot, _) => slot.ToString(),
        int and > 0 and < 10 => "small",
        int or long => "integral",
        not null => "something",
        null => "nothing",
    };
}
