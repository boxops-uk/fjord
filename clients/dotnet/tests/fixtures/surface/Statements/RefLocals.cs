// Clause 13.6.2.4 — explicitly typed ref local variable declarations.
//
// A ref local is an alias, not a variable: it has no storage of its own, it must be
// initialized in its declarator, and it cannot be reassigned to a different location without
// `= ref`. Three spellings exist — `ref`, `ref readonly` and post-standard `scoped ref` — and
// all three are below.
//
// The hazard is the pair in `AliasAndCopy`: `tracked` is declared twice in one member, once
// as a ref local and once as an ordinary one. Same name, same type, same containing member,
// and the difference between them is the whole meaning of the clause — one writes through to
// somebody else's storage and the other does not.

using System;

namespace Surface.Statements;

/// <summary>A cell with a field, so that a <c>ref</c> can alias something addressable.</summary>
public struct StmtCell
{
    /// <summary>The value, a field rather than a property so a <c>ref</c> can reach it.</summary>
    public int Value;

    /// <summary>Makes a cell.</summary>
    /// <param name="value">Its initial value.</param>
    public StmtCell(int value) => Value = value;
}

/// <summary>Clause 13.6.2.4 — the ref local, in its three forms.</summary>
public static class StmtRefLocals
{
    /// <summary>
    /// Hands back a <c>ref</c> to a cell's field, so a ref local has something to alias.
    /// </summary>
    /// <param name="cell">The cell, passed by reference so its field outlives the call.</param>
    /// <returns>An alias for the cell's field.</returns>
    public static ref int Slot(ref StmtCell cell) => ref cell.Value;

    /// <summary>
    /// Declares <c>tracked</c> twice: once as an alias, once as a copy.
    /// </summary>
    /// <remarks>
    /// 13.6.2.4, and its hazard. The two declarations sit in sibling blocks, so both are
    /// legal; they have one identifier and one declared type, and only the <c>ref</c> tells
    /// them apart. The first writes through to <c>cells[0]</c> and the second does not, which
    /// is why the returned total differs by one from the naive reading.
    /// </remarks>
    /// <param name="cells">Storage to alias.</param>
    /// <returns>A total that shows which of the two wrote through.</returns>
    public static int AliasAndCopy(int[] cells)
    {
        int total = 0;

        // 13.6.2.4 — a ref local. `tracked` *is* `cells[0]`.
        {
            ref int tracked = ref cells[0];
            tracked += 10;
            total += cells[0];
        }

        // 13.6.2.3 — an ordinary local with the same name and type. `tracked` is a copy, and
        // `cells[1]` does not change.
        {
            int tracked = cells[1];
            tracked += 10;
            total += cells[1];
        }

        return total;
    }

    /// <summary>
    /// All three ref forms, plus a re-aliasing assignment.
    /// </summary>
    /// <remarks>
    /// 13.6.2.4. `ref readonly` declares an alias through which the location cannot be
    /// written; `scoped ref` declares one whose safe-to-escape scope is the method, which is
    /// post-standard and the only way to say "this alias does not leave". Reassigning an
    /// alias needs `= ref` on the right of the `=`, which is an assignment and not a
    /// declaration — the one place the two are told apart by a keyword.
    /// </remarks>
    /// <param name="first">A cell to alias.</param>
    /// <param name="second">Another cell to re-alias to.</param>
    /// <returns>A total over every alias.</returns>
    public static int EveryRefForm(int first, int second)
    {
        StmtCell head = new(first);
        StmtCell tail = new(second);

        // 13.6.2.4 — a ref local aliasing a field through a ref-returning method. The
        // initializer is a reference to `Slot` with `ref` on both sides.
        ref int alias = ref Slot(ref head);
        alias += 1;

        // 13.6.2.4 — `ref readonly`. `peek = 0;` here would be CS8331.
        ref readonly int peek = ref tail.Value;

        // 13.6.2.4 — `scoped ref`, post-standard. The alias may not be returned or stored,
        // which is a declaration-level fact with no runtime trace at all.
        scoped ref int local = ref alias;
        local += 1;

        // Not a declaration: `= ref` re-points an existing alias at a different location.
        alias = ref Slot(ref tail);
        alias += 1;

        return head.Value + tail.Value + peek + local;
    }

    /// <summary>
    /// A <c>foreach</c> whose iteration variable is a ref local.
    /// </summary>
    /// <remarks>
    /// 13.6.2.4 meeting 13.9.5.2. `foreach (ref int cell in span)` declares an alias per
    /// iteration rather than a copy, so the loop body writes into the span. The iteration
    /// variable of a `foreach` is the one declaration in the language that is a ref local
    /// without a declarator of its own.
    /// </remarks>
    /// <param name="cells">Cells to double in place.</param>
    /// <returns>Their total after doubling.</returns>
    public static int DoubleInPlace(int[] cells)
    {
        int total = 0;

        foreach (ref int cell in cells.AsSpan())
        {
            cell *= 2;
            total += cell;
        }

        // 13.6.2.4 — and the readonly form of the same iteration variable.
        foreach (ref readonly int cell in cells.AsSpan())
        {
            total += cell;
        }

        return total;
    }
}
