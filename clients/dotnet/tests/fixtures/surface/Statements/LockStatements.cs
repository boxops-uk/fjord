// Clause 13.13 — the lock statement.
//
// A `lock` is a reference row and the reference is entirely invisible. `lock (x) { … }` names
// no member, and what it binds depends on the *type* of x: for anything but the dedicated lock
// type it is `Monitor.Enter`/`Monitor.Exit`, and for a `System.Threading.Lock` it is
// `EnterScope` and the `Dispose` of the `Lock.Scope` that comes back. So one statement form
// has two completely different sets of targets, chosen by overload-resolution-shaped rules on
// a type the source mentions once.
//
// The hazard is the pair in `TwiceOnOneMonitor`: two `lock` statements in one member, on one
// field, binding one member. Two occurrences, one (source, target) pair, and no identifier at
// either use site for a reference to be keyed by.

using System.Collections.Generic;
using System.Threading;

namespace Surface.Statements;

/// <summary>Clause 13.13 — <c>lock</c>, over both kinds of lock object.</summary>
public static class StmtLocks
{
    /// <summary>
    /// Two <c>lock</c> statements on one monitor object.
    /// </summary>
    /// <remarks>
    /// 13.13, and its hazard. Both statements lock <see cref="StmtGuardedCounter.Monitor"/>
    /// and both bind `Monitor.Enter` and `Monitor.Exit`, from two places in one member and
    /// with neither name written.
    /// </remarks>
    /// <param name="counter">The counter to guard.</param>
    /// <returns>The count afterwards.</returns>
    public static int TwiceOnOneMonitor(StmtGuardedCounter counter)
    {
        lock (counter.Monitor)
        {
            counter.Bump();
        }

        lock (counter.Monitor)
        {
            counter.Bump();
        }

        return counter.Count;
    }

    /// <summary>
    /// A <c>lock</c> over the dedicated lock type, which compiles into something else.
    /// </summary>
    /// <remarks>
    /// 13.13, post-standard. When the operand's type is <see cref="Lock"/> the statement is
    /// no longer `Monitor.Enter`/`Monitor.Exit` at all: it is `EnterScope()` and a `using` of
    /// the scope it returns. Same syntax, same clause, three different members bound, and the
    /// only thing that chose them is the static type of the expression in the parentheses.
    /// </remarks>
    /// <param name="counter">The counter to guard.</param>
    /// <returns>The count afterwards.</returns>
    public static int OnALock(StmtGuardedCounter counter)
    {
        lock (counter.Gate)
        {
            counter.Bump();
        }

        // 13.13 — and again on a `Lock` held in a local, so the same binding happens with a
        // different kind of operand expression.
        Lock gate = counter.Gate;

        lock (gate)
        {
            counter.Bump();
        }

        return counter.Count;
    }

    /// <summary>
    /// Every operand shape and body shape a <c>lock</c> admits.
    /// </summary>
    /// <remarks>
    /// 13.13. The operand may be any expression of reference type — a field, a local, a
    /// property, a `this` — and the body is an embedded statement, so a `lock` with no braces
    /// is legal (13.1). A `lock` may nest, and a jump out of a `lock` releases it on the way,
    /// exactly as a `finally` runs (13.10.1) — because the statement is *defined* as a
    /// `try`/`finally`, which is the equivalence 13.13 states and no syntax shows.
    /// </remarks>
    /// <param name="counter">The counter to guard.</param>
    /// <param name="limit">How many times to bump it.</param>
    /// <returns>A trail of what was locked.</returns>
    public static string EveryShape(StmtGuardedCounter counter, int limit)
    {
        List<string> trail = [];
        object local = new();

        // 13.13 — a local as the operand, and an unbraced embedded statement as the body.
        lock (local)
            trail.Add("local");

        // 13.13 — a property as the operand.
        lock (counter.Monitor)
        {
            trail.Add("property");

            // 13.13 — a nested `lock` on a different object. Two monitors held at once, and
            // the release order is the reverse of the acquisition order because the statement
            // is a `try`/`finally` pair.
            lock (local)
            {
                trail.Add("nested");
            }
        }

        // 13.13 — a `lock` whose body leaves the statement by a jump. The monitor is
        // released before the `break` reaches the loop, and nothing in the source says so.
        for (int i = 0; i < limit; i++)
        {
            lock (local)
            {
                trail.Add("in-loop");

                if (i > 0)
                {
                    break;
                }
            }
        }

        return string.Join(",", trail);
    }
}
