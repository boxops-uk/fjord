using System;
using System.Collections.Generic;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Names;

// Identifier syntax that is not a reference anybody wrote.
//
// Each of these parses as an `IdentifierNameSyntax`, which is a `SimpleNameSyntax`, so a
// reference dispatch keyed on that base visits all four — and what it does with them differs
// per form:
//
//   `var`     binds to the *inferred* type, so the index gains a reference to `int` at a
//             span where the source says `var`. A reference nobody wrote, pointing at a
//             real definition.
//   `nameof`  binds to nothing, so it is counted as an unresolved name. A miss that is not
//             a miss.
//   `_`       binds to a `SymbolKind.Discard`, which the walk drops before writing
//             anything — neither a fact nor a tally.
//   `field`   binds to a synthesised backing field whose only location is the accessor that
//             named it. See `SfFieldKeywordHost`.
//
// A contextual keyword used as an ordinary name is the control for all four: `record`,
// `value`, `from` and `when` here are references like any other, and the index should hold
// them as such.

/// <summary>The four identifier occupants, and a contextual keyword used as a plain name.</summary>
public sealed class SfIdentifierOccupants
{
    /// <summary>A field whose name is a contextual keyword.</summary>
    public int when;

    /// <summary>A property whose name is a contextual keyword.</summary>
    public string? record { get; set; }

    /// <summary>
    /// <c>var</c> in each position it can occupy: a local, a <c>foreach</c> variable, a
    /// deconstruction, and a pattern.
    /// </summary>
    /// <param name="entries">What to walk.</param>
    /// <returns>What was found.</returns>
    public string Inferred(IReadOnlyList<SfLedgerRecord> entries)
    {
        // `var` as the type of a local: an identifier that binds to `SfLedgerRecord`.
        var first = entries.Count == 0 ? null : entries[0];

        // `var` as a `foreach` variable's type.
        var total = 0;
        foreach (var entry in entries)
        {
            total += entry.Seed;
        }

        // `var` as a deconstruction designation's type, which is the form that is a
        // `DeclarationExpression` rather than a local declaration.
        var (seed, label) = first ?? new SfLedgerRecord(0, "none");

        // `var` inside a pattern, where it matches anything and binds.
        if (first is var matched)
        {
            total += matched?.Seed ?? 0;
        }

        return $"{seed}{label}{total}";
    }

    /// <summary>
    /// <c>nameof</c> — an invocation whose callee identifier binds to nothing, whose
    /// argument is a real reference, and whose result is a constant string.
    /// </summary>
    /// <returns>The names of things in this corpus.</returns>
    public string Named()
    {
        // `nameof` itself resolves to no symbol. Its argument does: `Inferred` is a genuine
        // reference to a method, and `SfLedgerRecord.Seed` a genuine reference to a
        // property, even though neither is invoked or read.
        var self = nameof(Inferred);
        var other = nameof(SfLedgerRecord.Seed);
        var contextual = nameof(record);
        return $"{self}.{other}.{contextual}";
    }

    /// <summary>
    /// A contextual keyword used as an ordinary name — the control for the three rows above.
    /// </summary>
    /// <returns>A rendering built from all of them.</returns>
    public string Contextual()
    {
        // `value`, `from`, `where`, `select`, `yield`, `async` and `await` are contextual:
        // outside the constructs that give them meaning they are identifiers, and every use
        // below is a reference the index should hold like any other local.
        int value = 1;
        int from = 2;
        int where = 3;
        int select = 4;
        int yield = 5;
        int async = 6;
        int await = 7;
        when = value + from + where + select + yield + async + await;
        record = nameof(when);
        return $"{when}{record}";
    }
}
