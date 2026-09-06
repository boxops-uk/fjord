using System;
using System.Collections.Generic;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Statements;

// The statement kinds that declare something, and the two that the census marks `neither`
// because the symbol hangs off a child.
//
//   LocalDeclarationStatement  the statement; the symbol is on its `VariableDeclarator`
//   VariableDeclaration        the type plus the declarator list inside it — a container
//   VariableDeclarator         where a local's, a field's or an event's symbol actually is
//   ForEachStatement           declares its loop variable on the statement itself
//   CatchDeclaration           declares the caught exception
//   LabeledStatement           declares a `SymbolKind.Label`
//   LocalFunctionStatement     declares a method — and is the one row here the walk sees
//
// The walk's declarator case is guarded by `declarator.Parent?.Parent is
// BaseFieldDeclarationSyntax`, so a *local*'s declarator falls through it: a local variable
// is declared by the same syntax kind as a field and reached by neither the declaration
// switch nor anything else. `LocalFunctionStatementSyntax` is named explicitly, which makes
// a local function the only thing declared inside a method body that the index holds a
// definition for.

/// <summary>Every declaring statement form, in one walk over a small ledger.</summary>
public static class SfStatementForms
{
    /// <summary>
    /// LocalDeclarationStatement and VariableDeclaration in each shape: one declarator,
    /// several declarators, <c>const</c>, <c>using</c>, <c>ref</c>, and <c>scoped ref</c>.
    /// </summary>
    /// <returns>A fold of every local.</returns>
    public static int Locals()
    {
        // One declaration, three declarators — three local symbols.
        int first = 1, second = 2, third;
        third = first + second;

        // A `const` local, whose declarator carries a constant value.
        const int ceiling = 100;

        // A `using` local declaration, which declares a local and a disposal.
        using var disposable = new System.IO.MemoryStream();

        // A `ref` local, which aliases storage rather than copying it.
        var window = new[] { 1, 2, 3 };
        ref int aliased = ref window[0];
        aliased = 9;

        // A `scoped ref` local, which narrows the ref safe context.
        scoped ref readonly int borrowed = ref window[1];

        return first + second + third + ceiling + (int)disposable.Length
            + window[0] + borrowed;
    }

    /// <summary>
    /// ForEachStatement in each of its forms — over an array, over an interface, with
    /// <c>var</c>, with a stated type, with a tuple deconstruction, and asynchronously.
    /// </summary>
    /// <param name="entries">What to walk.</param>
    /// <returns>A fold of the walk.</returns>
    public static int Loops(IReadOnlyList<SfLedgerRecord> entries)
    {
        var total = 0;

        // A stated element type, so the loop variable's type is a name and not `var`.
        foreach (SfLedgerRecord entry in entries)
        {
            total += entry.Seed;
        }

        // `var`, and an array rather than an interface, so the pattern-based enumerator is
        // the array one.
        var window = new[] { 1, 2, 3 };
        foreach (var element in window)
        {
            total += element;
        }

        // A deconstructing `foreach`, whose loop variable is a
        // `ParenthesizedVariableDesignation` rather than an identifier.
        var pairs = new List<(int Slot, string Name)> { (1, "one"), (2, "two") };
        foreach (var (slot, name) in pairs)
        {
            total += slot + name.Length;
        }

        return total;
    }

    /// <summary>
    /// ForEachStatement in its <c>await foreach</c> form — the same syntax kind with an
    /// <c>await</c> keyword, binding to `GetAsyncEnumerator`, `MoveNextAsync` and
    /// `DisposeAsync` instead of their synchronous counterparts. Four calls, none of them
    /// named by an identifier in the statement.
    /// </summary>
    /// <returns>The sum of what the sequence yielded.</returns>
    public static async System.Threading.Tasks.Task<int> LoopsAsync()
    {
        var total = 0;
        await foreach (var slot in Counted())
        {
            total += slot;
        }

        return total;
    }

    /// <summary>An async iterator, so <c>await foreach</c> has a source.</summary>
    /// <returns>Three slots, asynchronously.</returns>
    private static async IAsyncEnumerable<int> Counted()
    {
        for (var slot = 1; slot <= 3; slot++)
        {
            await System.Threading.Tasks.Task.Yield();
            yield return slot;
        }
    }

    /// <summary>
    /// LabeledStatement and the jumps that reference its label — a `SymbolKind.Label`, which
    /// the walk drops before writing anything, so the label is declared and referenced and
    /// the index holds neither fact.
    /// </summary>
    /// <param name="rounds">How many times round.</param>
    /// <returns>How many rounds ran.</returns>
    public static int Labels(int rounds)
    {
        var ran = 0;

    again:
        ran++;
        if (ran < rounds)
        {
            goto again;
        }

        switch (ran)
        {
            case 0:
                goto done;
            default:
                goto case 0;
        }

    done:
        return ran;
    }

    /// <summary>
    /// CatchDeclaration — the one place a local is declared by something other than a
    /// declarator or a loop — with a filter, without one, and a bare <c>catch</c> that
    /// declares nothing.
    /// </summary>
    /// <returns>What was caught.</returns>
    public static string Handlers()
    {
        try
        {
            throw new InvalidOperationException("provoked");
        }
        catch (ArgumentException caught) when (caught.ParamName is not null)
        {
            // A catch declaration with an exception filter, which is the form whose
            // declared local is in scope in the `when` clause as well as the block.
            return caught.ParamName;
        }
        catch (InvalidOperationException caught)
        {
            return caught.Message;
        }
        catch
        {
            // A catch clause with no declaration at all.
            return "unknown";
        }
        finally
        {
            GC.KeepAlive(null);
        }
    }

    /// <summary>
    /// LocalFunctionStatement — the only declaration inside a method body the walk reaches.
    /// Written in four shapes: expression-bodied, block-bodied, generic with a constraint,
    /// and <c>static</c>.
    /// </summary>
    /// <returns>A fold of what they returned.</returns>
    public static int LocalFunctions()
    {
        var captured = 2;

        int Doubled(int value) => value * captured;

        int Summed(int left, int right)
        {
            return left + right;
        }

        static int Tripled(int value) => value * 3;

        int Counted<TItem>(IReadOnlyList<TItem> items)
            where TItem : notnull => items.Count;

        return Doubled(1) + Summed(2, 3) + Tripled(4) + Counted(new[] { 5, 6 });
    }

    /// <summary>Runs every form above, so the entry point has one call to make.</summary>
    /// <returns>A rendering of the whole walk.</returns>
    public static string Walk()
    {
        var entries = new List<SfLedgerRecord> { new SfLedgerRecord(1, "one") };
        return $"{Locals()}{Loops(entries)}{Labels(3)}{Handlers()}{LocalFunctions()}"
            + LoopsAsync().GetAwaiter().GetResult();
    }
}
