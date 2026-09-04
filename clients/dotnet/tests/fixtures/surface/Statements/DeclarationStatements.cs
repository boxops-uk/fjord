// Clause 13.6.1 — the declaration statement forms, in one member, all answering to one name.
//
// The clause's grammar admits a local_variable_declaration, a local_constant_declaration and
// a local_function_declaration, and the variable declaration has three spellings of its own
// (implicitly typed, explicitly typed, ref) that 13.6.2.2 to 13.6.2.4 give a subclause each.
// However the row's four is counted, all five spellings are declaration statements and all
// five are below.
//
// They are below *with one name*. `FivePayloads` declares `payload` five times in five
// sibling blocks of one method: a variable, an inferred variable, a ref alias, a constant and
// a function. Five declarations, five different kinds, one identifier, one containing member.
// That is the hazard the row is flagged for, and it is the widest version of it clause 13.6
// allows — the earlier files repeat a name across two declarations of the *same* kind, and
// this one repeats it across every kind there is.

namespace Surface.Statements;

/// <summary>Clause 13.6.1 — every declaration statement form, and one identifier.</summary>
public static class StmtDeclarationForms
{
    /// <summary>
    /// Declares <c>payload</c> five times, once in each declaration statement form.
    /// </summary>
    /// <remarks>
    /// 13.6.1. Each block is a scope (13.3.1), so no two of the five are ever in scope
    /// together and none of them is an error. What tells them apart is the form of the
    /// statement and the span it occupies, and nothing else.
    /// </remarks>
    /// <param name="seed">A number every form works from.</param>
    /// <returns>A total that touches all five.</returns>
    public static int FivePayloads(int seed)
    {
        int total = 0;

        // Form 1 — local_variable_declaration, explicitly typed (13.6.2.3).
        {
            int payload = seed;
            total += payload;
        }

        // Form 2 — local_variable_declaration, implicitly typed (13.6.2.2). The declared
        // type is `string`, and no token in the statement says so.
        {
            var payload = seed.ToString();
            total += payload.Length;
        }

        // Form 3 — local_variable_declaration, ref (13.6.2.4). `payload` is an alias for a
        // storage location that belongs to somebody else, so assigning to it writes through.
        {
            int[] cells = [seed, seed + 1];
            ref int payload = ref cells[1];
            payload += 1;
            total += cells[1];
        }

        // Form 4 — local_constant_declaration (13.6.3). Not a variable at all: `payload` here
        // has no storage, and every use of it is the literal 7.
        {
            const int payload = 7;
            total += payload;
        }

        // Form 5 — local_function_declaration (13.6.4). A *member-shaped* declaration inside
        // a statement list, with a return type, a parameter list and a body.
        {
            int payload(int scale) => (seed - 1) * scale;
            total += payload(2);
        }

        return total;
    }

    /// <summary>
    /// The declaration statement forms again, this time all in one scope so that they are
    /// five distinct names in one block.
    /// </summary>
    /// <remarks>
    /// 13.6.1. The same five forms with the collision removed, so that a query has a control
    /// case: whatever it answers about <see cref="FivePayloads"/>, it should answer the
    /// analogous thing here without any ambiguity to resolve.
    /// </remarks>
    /// <param name="seed">A number every form works from.</param>
    /// <returns>A total that touches all five.</returns>
    public static int FiveNames(int seed)
    {
        int plain = seed;
        var inferred = seed.ToString();
        int[] cells = [seed, seed + 1];
        ref int aliased = ref cells[0];
        const int fixedValue = 7;

        int scaled(int scale) => plain * scale;

        aliased += fixedValue;

        return cells[0] + inferred.Length + scaled(2);
    }
}
