// Clause 13.14.1 — the using statement, and its resource_acquisition.
//
// The row is marked `both` and it earns it twice over. A `using (T r = e)` *declares* `r`,
// read-only, scoped to the statement — one of only two declaration forms in the language that
// cannot be assigned after initialization (the other is a foreach's iteration variable). And
// it *references* `Dispose` without writing it, through one of three routes: `IDisposable` for
// a class, a pattern `Dispose` for a `ref struct`, and `IAsyncDisposable` (or a pattern
// `DisposeAsync`) for the post-standard `await using`.
//
// The hazard is `handle`, declared four times in `EveryAcquisitionForm`: twice by two
// declarators of one resource_acquisition, and twice more by two `using` statements in sibling
// blocks. One resource_acquisition with two declarators is one syntax node and two
// declarations, which is the same shape as 13.6.2.1's declarator list with a `finally` wrapped
// round it.
//
// The `using` *declaration* — `using var h = e;` with no parentheses and no block — is
// clause 13.14.2 and is in `UsingDeclarations.cs`.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Clause 13.14.1 — <c>using</c> as a statement, over every kind of resource.</summary>
public static class StmtUsing
{
    /// <summary>
    /// Every form a <c>resource_acquisition</c> takes, declaring <c>handle</c> four times.
    /// </summary>
    /// <remarks>
    /// 13.14.1, and its hazard. Two declarators in one acquisition, then two more
    /// declarations of the same name in sibling blocks. Every one of the four is scoped to
    /// its own `using` statement, so none of them is an error and none of them can be told
    /// from the others by name and container.
    /// </remarks>
    /// <param name="name">What to call the handles.</param>
    /// <returns>A total over everything measured.</returns>
    public static int EveryAcquisitionForm(string name)
    {
        int total = 0;

        // 13.14.1 — one resource_acquisition with two declarators. Both are disposed, in
        // reverse order, by nested `finally` blocks the source does not contain.
        using (StmtHandle handle = new(name), spare = new($"{name}-spare"))
        {
            total += handle.Measure() + spare.Measure();
        }

        // 13.14.1 — `handle` again, in a sibling block. Same name, same type, and a
        // different declaration.
        {
            using (StmtHandle handle = new($"{name}-second"))
            {
                total += handle.Measure();
            }
        }

        // 13.14.1 — and once more, with `var`. The declared type is still `StmtHandle` and no
        // token in the statement says so.
        {
            using (var handle = new StmtHandle($"{name}-third"))
            {
                total += handle.Measure();
            }
        }

        return total;
    }

    /// <summary>
    /// A <c>using</c> whose resource is an expression rather than a declaration.
    /// </summary>
    /// <remarks>
    /// 13.14.1. The other half of the `resource_acquisition` production: an *expression*, so
    /// this `using` declares nothing at all and the resource has no name. It is still
    /// disposed, through a temporary the compiler introduces and the source cannot refer to.
    /// A null resource is legal too — the generated `finally` is null-checked — which makes
    /// the second `using` below a statement that binds `Dispose` and never calls it.
    /// </remarks>
    /// <param name="name">What to call the resource.</param>
    /// <returns>A total from both statements.</returns>
    public static int WithoutADeclaration(string name)
    {
        int total = 0;

        // 13.14.1 — an expression resource. `Acquire()` is called once and its result is
        // held in a temporary with no name.
        using (Acquire(name))
        {
            total += name.Length;
        }

        // 13.14.1 — a null resource, which is legal and disposes nothing.
        StmtHandle? absent = null;

        using (absent)
        {
            total += 1;
        }

        return total;
    }

    /// <summary>
    /// A <c>using</c> over a <c>ref struct</c>, whose <c>Dispose</c> is found by pattern.
    /// </summary>
    /// <remarks>
    /// 13.14.1, post-standard. <see cref="StmtScopedHandle"/> implements no interface: a `ref
    /// struct` cannot implement <see cref="IDisposable"/> usefully, so the language looks for
    /// a `Dispose` method by shape instead. This is the only route by which a `using`
    /// statement's target can be a method that is not an interface implementation, and the
    /// binding is structural in exactly the way `foreach`'s is (13.9.5.2).
    /// </remarks>
    /// <param name="weight">A number to carry.</param>
    /// <returns>What the scope measured.</returns>
    public static int OverARefStruct(int weight)
    {
        int total = 0;

        using (StmtScopedHandle scope = new(weight))
        {
            total += scope.Measure();
        }

        // 13.14.1 — nested, so that two pattern `Dispose` calls are generated one inside the
        // other.
        using (StmtScopedHandle outer = new(weight))
        {
            using (StmtScopedHandle inner = new(weight + 1))
            {
                total += outer.Measure() + inner.Measure();
            }
        }

        return total;
    }

    /// <summary>
    /// <c>await using</c>, the asynchronous form.
    /// </summary>
    /// <remarks>
    /// 13.14.1, post-standard. The resource is an <see cref="IAsyncDisposable"/> and the
    /// generated `finally` *awaits* `DisposeAsync` — so the statement introduces an await in
    /// a position the source does not contain one, which is the same trick 13.9.5.3 plays
    /// with `MoveNextAsync`. Both the parenthesised statement form and the two-declarator
    /// form work here.
    /// </remarks>
    /// <param name="name">What to call the handles.</param>
    /// <returns>A total over everything measured.</returns>
    public static async Task<int> EveryAsyncForm(string name)
    {
        int total = 0;

        // 13.14.1 — `await using` as a statement, with a declaration.
        await using (StmtAsyncHandle handle = new(name))
        {
            total += handle.Measure();
        }

        // 13.14.1 — `await using` with two declarators, disposed in reverse order, each
        // await hidden in a generated `finally`.
        await using (StmtAsyncHandle first = new($"{name}-1"), second = new($"{name}-2"))
        {
            total += first.Measure() + second.Measure();
        }

        // 13.14.1 — `await using` over an expression, so the resource has no name.
        await using (AcquireAsync(name))
        {
            total += name.Length;
        }

        return total;
    }

    /// <summary>
    /// A <c>using</c> whose body leaves by every route, and the disposal that happens anyway.
    /// </summary>
    /// <remarks>
    /// 13.14.1 with 13.10.1. The statement is defined as a `try`/`finally`, so a `return`, a
    /// `break` and a `throw` out of the body all dispose the resource on the way — the trail
    /// below is the evidence, and `TryStatements.FinallyOnly` is the same shape written by
    /// hand for comparison.
    /// </remarks>
    /// <param name="name">What to call the resource.</param>
    /// <param name="which">Which exit to take.</param>
    /// <returns>A trail of what ran.</returns>
    public static string EveryExit(string name, int which)
    {
        List<string> trail = [];

        for (int i = 0; i < 2; i++)
        {
            using (StmtHandle handle = new(name))
            {
                trail.Add($"body {handle.Name}");

                if (which == 0)
                {
                    // 13.14.1 — disposed, then returned.
                    return string.Join(",", trail) + "|returned";
                }

                if (which == 1)
                {
                    // 13.14.1 — disposed, then the loop is left.
                    break;
                }

                if (which == 2)
                {
                    // 13.14.1 — disposed, then the next iteration starts.
                    continue;
                }
            }

            trail.Add($"after {i}");
        }

        return string.Join(",", trail);
    }

    private static StmtHandle Acquire(string name) => new($"{name}-acquired");

    private static StmtAsyncHandle AcquireAsync(string name) => new($"{name}-acquired-async");
}
