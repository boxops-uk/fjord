// Clause 13.9.5 — the foreach statement: 13.9.5.1 (the three forms and how the collection,
// enumerator and iteration types are determined), 13.9.5.2 (the synchronous form) and
// 13.9.5.4 (the deconstructing form). The asynchronous form, 13.9.5.3, is in
// `AsyncForeachStatements.cs`. The collections themselves are in `ForeachCollections.cs`.
//
// `foreach` is the statement that references the most and writes the least. A loop over a
// collection reaches `GetEnumerator`, `Current`, `MoveNext` and sometimes `Dispose`, and the
// source contains none of those four identifiers. Which four it reaches depends on which of
// 13.9.5.1's three forms applies — the pattern, `IEnumerable<T>`, or `IEnumerable` with a
// conversion — and the three forms pick different members on different types.
//
// Two hazards live here:
//
//   13.9.5.1/13.9.5.2 — `element` is the iteration variable of seven consecutive loops in
//                       `EveryCollectionForm`, at five different types. Seven declarations,
//                       one identifier, one containing member, and no `foreach` has a
//                       declarator a query could key on other than its own span.
//   13.9.5.4          — `left` and `right` are declared by two deconstructing loops in
//                       `EveryDeconstructingForm`, and a deconstructing `foreach` declares
//                       *two* variables in one iteration variable position, which no other
//                       statement form does.

using System;
using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.9.5 — <c>foreach</c>, over everything it can walk.</summary>
public static class StmtForeach
{
    /// <summary>
    /// Seven loops, seven declarations of <c>element</c>, five collection shapes.
    /// </summary>
    /// <remarks>
    /// 13.9.5.1 and 13.9.5.2, with the hazard. Each `foreach` is its own scope, so the seven
    /// declarations are all legal; the iteration types are `int`, `char`, `string`, `string`,
    /// `int`, `string` and `int`, and the enumerator each loop binds is a different type
    /// found a different way.
    /// </remarks>
    /// <param name="cells">An array to walk.</param>
    /// <returns>A total that touches every loop.</returns>
    public static int EveryCollectionForm(int[] cells)
    {
        int total = 0;

        // 13.9.5.2, form 1 — an array. The compiler does not call `GetEnumerator` at all: an
        // array `foreach` is compiled to an index loop, so this is the one form whose
        // references exist in the language rule and not in the emitted code.
        foreach (int element in cells)
        {
            total += element;
        }

        // 13.9.5.2, form 1 again — a string, which is special-cased the same way.
        foreach (char element in "seed")
        {
            total += element;
        }

        // 13.9.5.2, form 2 — `List<T>`, which has a public struct `GetEnumerator` *and*
        // implements `IEnumerable<T>`. The pattern wins, so the enumerator is the struct and
        // no interface dispatch happens.
        List<string> names = ["first", "second"];
        foreach (string element in names)
        {
            total += element.Length;
        }

        // 13.9.5.2, form 2 — a collection of our own with a pattern `GetEnumerator` and no
        // interface at all. `StmtRosterCursor` has no `Dispose`, which the pattern allows.
        StmtRoster roster = new("alpha", "beta", "gamma");
        foreach (string element in roster)
        {
            total += element.Length;
        }

        // 13.9.5.2 — a `ref struct` enumerator, which cannot implement `IEnumerator<T>` and
        // so can only ever be found by pattern. Its `Dispose` is called by pattern too.
        StmtSpanRun run = new(cells);
        foreach (int element in run)
        {
            total += element;
        }

        // 13.9.5.2, form 3 — a collection that implements only the non-generic
        // `IEnumerable`. The iteration type is `object`, and the `string` written below makes
        // the compiler insert an explicit reference conversion on every iteration — a cast
        // that is in the semantics and not in the source.
        StmtLegacyBag bag = new("one", "two");
        foreach (string element in bag)
        {
            total += element.Length;
        }

        // 13.9.5.2, post-standard — an *extension* `GetEnumerator`. The collection is an
        // `int`, whose own type knows nothing about enumeration, so the binding target is a
        // method in a different type from the collection's.
        foreach (int element in 3)
        {
            total += element;
        }

        return total;
    }

    /// <summary>
    /// The iteration variable's own forms.
    /// </summary>
    /// <remarks>
    /// 13.9.5.1. The iteration variable may be explicitly typed, implicitly typed with `var`,
    /// or a `ref`/`ref readonly` alias over a collection that offers one (which is in
    /// `RefLocals.cs`). It is read-only inside the body — assigning to it is CS1656 — and it
    /// is a *fresh* variable per iteration, which is why a lambda capturing it captures a
    /// different variable each time round. That last fact is a declaration-level fact with no
    /// syntax to point at, and the closure list below is the only way to make it visible.
    /// </remarks>
    /// <param name="cells">An array to walk.</param>
    /// <returns>The values the captured closures see, in order.</returns>
    public static List<int> FreshEachIteration(int[] cells)
    {
        List<Func<int>> captured = [];

        // 13.9.5.1 — `var`, inferred from the iteration type.
        foreach (var value in cells)
        {
            captured.Add(() => value);
        }

        List<int> seen = [];

        foreach (Func<int> closure in captured)
        {
            seen.Add(closure());
        }

        return seen;
    }

    /// <summary>
    /// Deconstructing <c>foreach</c>, over a tuple sequence and over a type with a
    /// <c>Deconstruct</c>.
    /// </summary>
    /// <remarks>
    /// 13.9.5.4, and its hazard. The iteration variable position holds a deconstruction
    /// declaration, so one `foreach` declares two locals — and `left` and `right` are each
    /// declared twice in this member, once from a tuple's elements and once through
    /// `StmtPair.Deconstruct`, which the source does not name.
    /// </remarks>
    /// <param name="pairs">Tuples to walk.</param>
    /// <param name="book">Deconstructable pairs to walk.</param>
    /// <returns>A total that touches all four declarations.</returns>
    public static int EveryDeconstructingForm(List<(string Key, int Weight)> pairs, StmtPairBook book)
    {
        int total = 0;

        // 13.9.5.4 — deconstructing a tuple. The names come from the declaration here, not
        // from the tuple's element names, so `left` and `right` shadow `Key` and `Weight`.
        foreach (var (left, right) in pairs)
        {
            total += left.Length + right;
        }

        // 13.9.5.4 — deconstructing through a user-defined `Deconstruct`. Same two names, a
        // second time, in the same member.
        foreach (var (left, right) in book)
        {
            total += left.Length + right;
        }

        // 13.9.5.4 — the explicitly typed spelling, where each element of the deconstruction
        // carries its own type.
        foreach ((string key, int weight) in book)
        {
            total += key.Length + weight;
        }

        // 13.9.5.4 — a deconstruction with a discard, which declares one variable and
        // nothing for the other position.
        foreach (var (key, _) in pairs)
        {
            total += key.Length;
        }

        return total;
    }

    /// <summary>
    /// A <c>foreach</c> body containing every jump that can leave it.
    /// </summary>
    /// <remarks>
    /// 13.9.5.2 with 13.10. `continue` goes to the next `MoveNext`; `break` leaves the loop
    /// and disposes the enumerator; `return` leaves the method and disposes it too; and a
    /// `goto` out of the loop does the same. The disposal is the part with no syntax: leaving
    /// a `foreach` by any route calls the enumerator's `Dispose` if it has one, and none of
    /// the four jumps below says so.
    /// </remarks>
    /// <param name="names">Names to search.</param>
    /// <param name="needle">What to look for.</param>
    /// <returns>Where the needle was found, or -1.</returns>
    public static int FindWithEveryExit(StmtRoster names, string needle)
    {
        int index = -1;
        int seen = 0;

        foreach (string name in names)
        {
            if (name.Length == 0)
            {
                continue;
            }

            if (name == needle)
            {
                index = seen;
                break;
            }

            if (name == "poison")
            {
                goto Abandoned;
            }

            seen++;
        }

        return index;

        Abandoned:
        return -2;
    }
}
