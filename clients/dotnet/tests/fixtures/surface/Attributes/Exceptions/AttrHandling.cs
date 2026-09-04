// Clause 22.4 (how exceptions are handled).
//
// The clause is about the search for a handler, and every part of that search has a
// syntactic form here: a catch clause with a type and a variable, one with a type and no
// variable, a general catch with neither, an exception filter, a rethrow, and a finally
// block that runs on both paths.
//
// The hazard is the *variable*. Sibling catch clauses are disjoint declaration spaces, so
// `fault` below is declared four times inside one method — twice in one try statement and
// twice more in the next. An identity string for a local minted from (enclosing member,
// name) is one string for four declarations of four different types, and the merge is
// silent: nothing fails, the index simply says this method has one `fault` whose type is
// whichever declaration was walked last.
//
// The rethrow is the other thing to check. `throw;` names nothing — it is a statement with
// no operand and therefore no reference — while `throw fault;` two clauses down is a read
// of a local. A query that counts references to AttrLedgerFault must not find one in the
// bare rethrow.

using System;

namespace Surface.Attributes.Exceptions;

/// <summary>22.4: the handler search, one method per part of it.</summary>
public static class AttrHandling
{
    /// <summary>
    /// 22.4: four catch clauses over two try statements, all declaring <c>fault</c>, plus
    /// the filter that decides between two of them.
    /// </summary>
    public static string Handles(int mode)
    {
        try
        {
            AttrThrowSites.Raises(mode, "ledger");
        }
        catch (AttrPostingFault fault) when (fault.Line > 0)
        {
            // 22.4: a filter is evaluated before any handler runs, so this clause can
            // decline an exception the next one accepts.
            return $"posting {fault.Line}";
        }
        catch (AttrLedgerFault fault)
        {
            return $"ledger {fault.Ledger}";
        }

        try
        {
            AttrImplicitCauses.Divides(1, mode);
        }
        catch (DivideByZeroException fault) when (mode == 0)
        {
            return fault.Message;
        }
        catch (ArithmeticException fault)
        {
            // 22.4: the base class catches what the derived clause declined.
            return fault.GetType().Name;
        }

        return "handled nothing";
    }

    /// <summary>
    /// 22.4: the three catch clauses that declare no variable — a type with no name, and
    /// the general catch, which the clause makes equivalent to <c>catch (Exception)</c>.
    /// </summary>
    public static string CatchesWithoutNaming(Action work)
    {
        try
        {
            work();
        }
        catch (AttrLedgerFault)
        {
            // A type reference with no declaration beside it.
            return "a ledger fault, unnamed";
        }
        catch (Exception) when (AttrFilters.IsRecoverable())
        {
            return "recoverable";
        }
        catch
        {
            // 22.4: the general catch clause, which declares nothing and names nothing.
            return "something";
        }

        return "nothing thrown";
    }

    /// <summary>
    /// 22.4: rethrow versus re-throw. The first preserves the original throw point and
    /// carries no operand; the second is an ordinary throw whose operand reads a local.
    /// </summary>
    public static void Rethrows(int mode)
    {
        try
        {
            AttrThrowSites.Raises(mode, null);
        }
        catch (AttrPostingFault)
        {
            throw;
        }
        catch (AttrLedgerFault fault)
        {
            throw fault;
        }
        finally
        {
            // 22.4: the finally block runs whether or not a handler was found.
            AttrFilters.Record(nameof(Rethrows));
        }
    }

    /// <summary>
    /// 22.4: a try statement with a finally and no catch, nested inside one with both, so
    /// the search passes through a frame that cannot handle anything.
    /// </summary>
    public static int Unwinds(int[] cells, int index)
    {
        try
        {
            try
            {
                return cells[index];
            }
            finally
            {
                AttrFilters.Record(nameof(Unwinds));
            }
        }
        catch (IndexOutOfRangeException fault)
        {
            AttrFilters.Record(fault.Message);
            return -1;
        }
    }
}

/// <summary>Somewhere for the filters and the finally blocks above to call.</summary>
internal static class AttrFilters
{
    private static int _records;

    /// <summary>22.4: a filter's condition is an ordinary boolean expression.</summary>
    internal static bool IsRecoverable() => _records % 2 == 0;

    /// <summary>Counts what ran, so no finally block above is empty.</summary>
    internal static void Record(string what) => _records += what.Length;
}
