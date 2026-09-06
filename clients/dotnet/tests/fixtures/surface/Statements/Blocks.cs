// Clause 13.3 — blocks — with 13.3.1 (the block, the scope of its locals, and the iterator
// block) and 13.3.2 (statement lists).
//
// 13.3.1 is flagged as a hazard, and the hazard is the *scope* half of it: the scope of a
// local is the block that declares it, and two sibling blocks are two scopes. So one member
// can declare the same name twice, legally, with two different types, and the only thing
// separating the two declarations is where they are written. `TwoSlots` is that shape written
// on purpose. `TwoSlots` is not an accident of naming — `slot` appears twice in one method
// body because a query has to be able to say which of the two it means.
//
// Nested blocks are the other half: a nested block may *not* redeclare a name from an
// enclosing block (CS0136), so the collision only exists between siblings, and every repeated
// local name in this project is written across siblings for that reason.

using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.3 — the block, its statement list, and the scope of its locals.</summary>
public sealed class StmtBlocks
{
    private int _depth;

    /// <summary>How many blocks have been entered.</summary>
    public int Depth => _depth;

    /// <summary>
    /// Declares <c>slot</c> twice, in two sibling blocks, at two different types.
    /// </summary>
    /// <remarks>
    /// 13.3.1. Neither declaration is visible to the other and neither is in error. An index
    /// that names a local by its containing member and its identifier mints one string for
    /// both of these.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total that touches both slots.</returns>
    public int TwoSlots(int seed)
    {
        int total = 0;

        // 13.3.1 — a block whose only purpose is to be a scope. `slot` is an int here.
        {
            int slot = seed + 1;
            total += slot;
            _depth++;
        }

        // 13.3.1 — the sibling block. `slot` again, and a string this time. The first `slot`
        // has gone out of scope, so this is a declaration and not an assignment.
        {
            string slot = seed.ToString();
            total += slot.Length;
            _depth++;
        }

        return total;
    }

    /// <summary>
    /// A nested block, which is the case the sibling rule is contrasted with.
    /// </summary>
    /// <remarks>
    /// 13.3.1. The inner block can *read* <c>outer</c> and could not declare a second one:
    /// <c>int outer = 2;</c> inside it is CS0136. So a name repeated down a nesting is not a
    /// shape any compiling C# can contain, and the corpus cannot hold it.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total from both levels.</returns>
    public int Nested(int seed)
    {
        int outer = seed;

        {
            int inner = outer * 2;

            {
                int deeper = inner + outer;
                outer = deeper;
            }
        }

        return outer;
    }

    /// <summary>
    /// A block with an empty statement list, and a block with a long one.
    /// </summary>
    /// <remarks>
    /// 13.3.2. The empty block has no <c>statement_list</c> at all — the production is
    /// optional — which makes it the smallest statement in the language that is not the empty
    /// statement of 13.4.
    /// </remarks>
    /// <param name="seed">A number to accumulate from.</param>
    /// <returns>The accumulated total.</returns>
    public int StatementList(int seed)
    {
        // 13.3.2 — a block with no statement list. It declares nothing and does nothing, and
        // it is a statement.
        {
        }

        int total = seed;

        // 13.3.2 — a statement list of eight statements, four of which are declarations, so
        // that "a block's statements in order" is a claim with something to be wrong about.
        {
            int first = 1;
            int second = first + 1;
            total += first;
            total += second;
            int third = second + 1;
            total += third;
            int fourth = third + 1;
            total += fourth;
        }

        return total;
    }

    /// <summary>
    /// An iterator block: a block containing <c>yield</c>, which 13.3.1 singles out because
    /// its locals outlive the call that declared them.
    /// </summary>
    /// <remarks>
    /// 13.3.1. The compiler turns this block into a state machine class nested in
    /// <see cref="StmtBlocks"/>, and every local below becomes a field of it. So the
    /// declarations here have two lives — a local in the source, a field in the emitted
    /// metadata — and an index built from source and one built from IL disagree about them by
    /// construction. The <c>yield</c> statements themselves are clause 13.15, in
    /// <c>YieldStatements.cs</c>.
    /// </remarks>
    /// <param name="count">How many numbers to produce.</param>
    /// <returns>A lazy sequence.</returns>
    public IEnumerable<int> IteratorBlock(int count)
    {
        int running = 0;

        for (int i = 0; i < count; i++)
        {
            running += i;
            yield return running;
        }
    }
}
