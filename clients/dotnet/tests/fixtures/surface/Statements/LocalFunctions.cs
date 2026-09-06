// Clause 13.6.4 — local function declarations.
//
// A local function is a member-shaped declaration in a statement position: a return type, a
// name, an optional type parameter list with constraints, a parameter list, and a body that
// may be a block, an expression, an iterator or async. It is scoped to the enclosing *block*,
// like a local, and it may be declared after the statements that call it, unlike one.
//
// The hazard is `Fold`, declared twice in two sibling blocks with two different signatures.
// Local functions in one scope cannot overload each other (CS0128), so this pair is not an
// overload set — but it looks like one to anything that keys a declaration by name and
// container, and the two signatures make the merge visible if it happens.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Clause 13.6.4 — the local function, in every body form it has.</summary>
public static class StmtLocalFunctions
{
    /// <summary>
    /// Declares <c>Fold</c> twice, in sibling blocks, with different signatures.
    /// </summary>
    /// <remarks>
    /// 13.6.4, and its hazard. Two declarations, one name, one containing member, and no
    /// overload relationship between them: the second is not a second entry in an overload
    /// set, it is a different declaration in a scope the first cannot see.
    /// </remarks>
    /// <param name="cells">Numbers to fold.</param>
    /// <returns>A total from both folds.</returns>
    public static int TwoFolds(int[] cells)
    {
        int total = 0;

        // 13.6.4 — `Fold` over ints, with a block body.
        {
            int Fold(int[] values)
            {
                int running = 0;

                foreach (int value in values)
                {
                    running += value;
                }

                return running;
            }

            total += Fold(cells);
        }

        // 13.6.4 — `Fold` again, in a sibling block, taking two parameters and returning a
        // string. Nothing about this declaration is related to the one above.
        {
            string Fold(int left, int right) => $"{left}:{right}";

            total += Fold(cells.Length, total).Length;
        }

        return total;
    }

    /// <summary>
    /// Every body and modifier form a local function has.
    /// </summary>
    /// <remarks>
    /// 13.6.4. A `static` local function captures nothing, which is a declaration-level fact
    /// the compiler enforces; a non-static one closes over `seed` and becomes a method on a
    /// display class. The generic one carries a constraint, which is a type reference in a
    /// declaration that has no other trace. The iterator and async forms make the compiler
    /// emit a state machine per local function, nested inside the *enclosing* type.
    /// </remarks>
    /// <param name="seed">A number to close over.</param>
    /// <returns>A total that calls every form.</returns>
    public static int EveryForm(int seed)
    {
        // 13.6.4 — declared before its first call, which is the ordinary way round.
        static int Twice(int value) => value * 2;

        // 13.6.4 — a non-static local function, closing over `seed`.
        int Shifted(int value) => value + seed;

        // 13.6.4 — a generic local function with a constraint and a type parameter used in
        // its parameter list.
        static int CountOf<T>(IEnumerable<T> items)
            where T : notnull
        {
            int running = 0;

            foreach (T item in items)
            {
                running += item.ToString()?.Length ?? 0;
            }

            return running;
        }

        // 13.6.4 — an iterator local function. `yield` inside a local function makes the
        // local function the iterator, not the enclosing method (13.15).
        static IEnumerable<int> Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return i;
            }
        }

        // 13.6.4 — an async local function, whose body may await.
        static async Task<int> SumAsync(int count)
        {
            await Task.Yield();

            int running = 0;

            foreach (int step in Steps(count))
            {
                running += step;
            }

            return running;
        }

        // 13.6.4 — parameter list forms: a default, `params`, `ref` and `out`.
        static int Combine(ref int accumulator, out string label, int scale = 2, params int[] extra)
        {
            label = $"scale {scale}";
            accumulator *= scale;

            foreach (int value in extra)
            {
                accumulator += value;
            }

            return accumulator;
        }

        int accumulator = seed;
        int combined = Combine(ref accumulator, out string label, 3, 1, 2, 3);

        // 13.6.4 — called after the declarations above, and one call to a function declared
        // *below* this statement, which a local variable could not be.
        int total = Twice(seed) + Shifted(seed) + CountOf(Steps(3)) + combined + label.Length;

        total += SumAsync(3).GetAwaiter().GetResult();

        total += Late(seed);

        // 13.6.4 — declared after every use of it. A local function's scope is the whole
        // block, so forward references are legal and this is the shape that proves it.
        static int Late(int value) => value - 1;

        return total;
    }

    /// <summary>
    /// A local function nested inside another local function, and one that recurses.
    /// </summary>
    /// <remarks>
    /// 13.6.4. A local function body is a block, so it can hold declaration statements,
    /// including further local functions — which gives a declaration whose container is
    /// another declaration that itself has no name in metadata.
    /// </remarks>
    /// <param name="depth">How deep to recurse.</param>
    /// <returns>The factorial-ish total.</returns>
    public static int Nested(int depth)
    {
        static int Outer(int value)
        {
            static int Inner(int inner) => inner <= 1 ? 1 : inner * Inner(inner - 1);

            return Inner(value);
        }

        return Outer(Math.Min(depth, 6));
    }
}
