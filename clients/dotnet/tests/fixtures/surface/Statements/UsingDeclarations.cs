// Clause 13.14.2 — the using declaration.
//
// `using var h = e;` is a *declaration statement*, not a using statement: it has no
// parentheses, no embedded statement and no block of its own. Its resource is disposed at the
// end of the enclosing block, in reverse declaration order, which means the `finally` it
// generates wraps everything after it rather than something it contains. So a `using`
// declaration is the one statement in the language whose effect extends over statements that
// are its *siblings*.
//
// The hazard is `handle`, declared by four using declarations in `EveryForm` — two in sibling
// blocks and two more as two declarators of one statement. The same collision as 13.14.1's,
// with the statement form that has no block to scope it: here what limits the scope is the
// enclosing block, so the two sibling blocks are load-bearing rather than decorative.

using System;
using System.Threading.Tasks;

namespace Surface.Statements;

/// <summary>Clause 13.14.2 — the <c>using</c> declaration, and where its disposal lands.</summary>
public static class StmtUsingDeclarations
{
    /// <summary>
    /// Four using declarations of <c>handle</c>, in two sibling blocks.
    /// </summary>
    /// <remarks>
    /// 13.14.2, and its hazard. The first block declares `handle` and `spare` in one
    /// statement; the second declares `handle` again with `var`. Both blocks dispose what
    /// they declared when they end, in reverse order, and no `Dispose` is written.
    /// </remarks>
    /// <param name="name">What to call the handles.</param>
    /// <returns>A total over everything measured.</returns>
    public static int EveryForm(string name)
    {
        int total = 0;

        {
            // 13.14.2 — one using declaration with two declarators. `spare` is disposed
            // first and `handle` second, at the closing brace of this block.
            using StmtHandle handle = new(name), spare = new($"{name}-spare");

            total += handle.Measure() + spare.Measure();
        }

        {
            // 13.14.2 — `handle` again, in a sibling block, implicitly typed. A different
            // declaration with the same name and the same type.
            using var handle = new StmtHandle($"{name}-second");

            total += handle.Measure();

            // 13.14.2 — a statement after the declaration, inside the `finally` the
            // declaration generated. Written to make the scope's extent visible: this line is
            // protected by a `try` whose `try` keyword is nowhere in the file.
            total += handle.Closed ? 0 : 1;
        }

        return total;
    }

    /// <summary>
    /// A using declaration at method scope, disposed by the method's end.
    /// </summary>
    /// <remarks>
    /// 13.14.2. With no enclosing block but the body, the resource lives until the method
    /// returns — including through the `return` statement, whose value is computed before the
    /// disposal runs (13.10.1). `Closed` is therefore false in the returned string and true
    /// immediately afterwards, and nothing in the source marks the moment it changes.
    /// </remarks>
    /// <param name="name">What to call the resource.</param>
    /// <returns>A label including whether the handle was closed when it was read.</returns>
    public static string AtMethodScope(string name)
    {
        using StmtHandle handle = new(name);

        return $"{handle.Name}:{handle.Closed}";
    }

    /// <summary>
    /// A using declaration over a <c>ref struct</c>, bound by pattern.
    /// </summary>
    /// <remarks>
    /// 13.14.2, post-standard, with the same pattern rule as 13.14.1: a `ref struct` with a
    /// `Dispose` method needs no interface. A `using` declaration is also the only way to
    /// hold a `ref struct` resource without a nested block, which is why this form exists at
    /// all in most code that uses it.
    /// </remarks>
    /// <param name="weight">A number to carry.</param>
    /// <returns>What the scope measured.</returns>
    public static int OverARefStruct(int weight)
    {
        using StmtScopedHandle scope = new(weight);

        return scope.Measure();
    }

    /// <summary>
    /// <c>await using var</c>, the asynchronous using declaration.
    /// </summary>
    /// <remarks>
    /// 13.14.2, post-standard. The disposal at the end of the block is an `await`, so the
    /// method has a suspension point after its last written statement — the one place in the
    /// language where a method can suspend at a position with no expression in it.
    /// </remarks>
    /// <param name="name">What to call the handles.</param>
    /// <returns>A total over both handles.</returns>
    public static async Task<int> AsyncForm(string name)
    {
        await using StmtAsyncHandle handle = new(name);

        int total = handle.Measure();

        {
            await using var inner = new StmtAsyncHandle($"{name}-inner");

            total += inner.Measure();
        }

        await Task.Yield();

        return total;
    }

    /// <summary>
    /// What a using declaration may not do, recorded because the corpus cannot hold it: it
    /// may not appear directly in a <c>switch</c> section (CS8647), it may not be the
    /// embedded statement of an <c>if</c> or a loop — no declaration statement may (13.1) —
    /// and it may not be <c>ref</c>. The `switch` case below therefore has a block round it,
    /// which is the workaround the rule forces and the shape most real code has.
    /// </summary>
    /// <param name="gate">Which arm to take.</param>
    /// <param name="name">What to call the resource.</param>
    /// <returns>What was measured.</returns>
    public static int InASwitchSection(int gate, string name)
    {
        switch (gate)
        {
            case 0:
            {
                // 13.14.2 — legal here because the section's statement list has been wrapped
                // in a block. Without the braces this is CS8647.
                using StmtHandle handle = new(name);

                return handle.Measure();
            }

            default:
                return 0;
        }
    }
}
