// Clause 9.4 — definite assignment, and clause 9.4.4's precise rules statement by
// statement. Nothing an index holds changes with the analysis itself; what the clauses give
// this project is a statement form each, and every statement form that can declare a
// variable declares one here — often twice, with the same name in a sibling scope, because
// that is the shape an ordinal-numbered identity gets wrong.

namespace Surface.Types;

/// <summary>
/// 9.4.1 hazard — a variable with one declaration and several assignments, and an out
/// parameter that must be definitely assigned on every path out. The declaration is one
/// row; the writes to it are many.
/// </summary>
public static class VarDefiniteAssignment
{
    /// <summary>9.4.1 — one declaration, one write per branch, and a read after the join.</summary>
    public static string Branching(int seed)
    {
        string state;
        if (seed > 0)
        {
            state = "positive";
        }
        else if (seed < 0)
        {
            state = "negative";
        }
        else
        {
            state = "zero";
        }

        return state;
    }

    /// <summary>9.4.2 — initially assigned variables: a static variable, an instance
    /// variable, an array element, a value parameter, a ref parameter and an in parameter
    /// are all definitely assigned at the start of the body.</summary>
    public static int InitiallyAssigned(int byValue, ref int byReference, in int byInput) =>
        VarStaticsBase.Tally + byValue + byReference + byInput + VarArrayElements.Vector[0];

    /// <summary>9.4.3 — initially unassigned variables: a local without an initializer, and
    /// an out parameter, neither of which may be read before it is written.</summary>
    public static bool InitiallyUnassigned(out int result)
    {
        int local;
        result = 0;
        local = result + 1;
        return local > 0;
    }

    /// <summary>9.4.3 — a struct local counts as assigned once every field is, which is the
    /// one place the rule is per-field rather than per-variable.</summary>
    public static double FieldByField()
    {
        TyPoint2D point;
        point.X = 1;
        point.Y = 2;
        return point.X + point.Y;
    }
}

/// <summary>
/// 9.4.4.1 hazard — the statement forms, each with the variables it can declare. Names
/// repeat deliberately across sibling scopes throughout.
/// </summary>
public static class VarStatementForms
{
    /// <summary>9.4.4.3 — a block statement, and the checked and unchecked blocks that the
    /// same clause governs. Each is a scope of its own.</summary>
    public static int Blocks(int seed)
    {
        int total = 0;
        {
            int inner = seed;
            total += inner;
        }

        checked
        {
            int guarded = seed;
            total += guarded;
        }

        unchecked
        {
            int wrapped = seed * 2;
            total += wrapped;
        }

        return total;
    }

    /// <summary>9.4.4.4 — expression statements, which assign without declaring.</summary>
    public static int ExpressionStatements(int seed)
    {
        int total = seed;
        total++;
        total += 2;
        total = total * 3;
        VarStaticsBase.Tally = total;
        _ = total.ToString();
        return total;
    }

    /// <summary>
    /// 9.4.4.5 hazard — declaration statements. Two locals named <c>x</c> in sibling blocks
    /// of one method: two declarations, one name, one container, and no scope in common.
    /// </summary>
    public static int SiblingDeclarations()
    {
        int total = 0;
        {
            int x = 1;
            total += x;
        }

        {
            int x = 2;
            total += x;
        }

        return total;
    }

    /// <summary>9.4.4.6 — if statements, with a variable assigned in one branch only and so
    /// unreadable after the join unless the other branch returns.</summary>
    public static string IfStatements(int seed)
    {
        string label;
        if (seed == 0)
        {
            label = "zero";
        }
        else
        {
            return "other";
        }

        return label;
    }

    /// <summary>
    /// 9.4.4.7 hazard — switch statements. Each switch section is its own scope, so
    /// <c>value</c> is declared three times in this one switch, at three different types.
    /// </summary>
    public static string SwitchStatement(object subject)
    {
        switch (subject)
        {
            case int value:
                return $"int {value}";

            case string value:
                return $"string {value}";

            case TyPoint2D value:
                return $"point {value.X}";

            case null:
                return "null";

            default:
                return "other";
        }
    }

    /// <summary>9.4.4.7 — a switch statement over a constant governing type, with `goto case`
    /// (9.4.4.11) linking two sections.</summary>
    public static int SwitchWithGotoCase(TyPriority priority)
    {
        int weight = 0;
        switch (priority)
        {
            case TyPriority.High:
                weight += 10;
                goto case TyPriority.Normal;

            case TyPriority.Normal:
                weight += 5;
                break;

            case TyPriority.Low:
                weight += 1;
                break;

            default:
                weight = -1;
                break;
        }

        return weight;
    }

    /// <summary>9.4.4.7 — a switch expression, whose arms declare pattern variables of their
    /// own; the same name may appear in more than one arm.</summary>
    public static string SwitchExpression(object subject) => subject switch
    {
        int value when value > 0 => $"positive {value}",
        int value => $"int {value}",
        string value => $"string {value.Length}",
        _ => "other",
    };

    /// <summary>9.4.4.8 — a while statement, whose condition is checked before the body.</summary>
    public static int WhileStatement(int limit)
    {
        int total = 0;
        int index = 0;
        while (index < limit)
        {
            int step = index * 2;
            total += step;
            index++;
        }

        return total;
    }

    /// <summary>9.4.4.9 — a do statement, whose body runs before its condition.</summary>
    public static int DoStatement(int limit)
    {
        int total = 0;
        int index = 0;
        do
        {
            int step = index + 1;
            total += step;
            index++;
        }
        while (index < limit);

        return total;
    }

    /// <summary>
    /// 9.4.4.10 hazard — for statements. Two loops in one method each declare <c>index</c>
    /// in their own initializer, and the third declares two variables in one initializer.
    /// </summary>
    public static int ForStatements(int limit)
    {
        int total = 0;
        for (int index = 0; index < limit; index++)
        {
            total += index;
        }

        for (int index = limit; index > 0; index--)
        {
            total -= index;
        }

        for (int index = 0, mirror = limit; index < mirror; index++, mirror--)
        {
            total += index * mirror;
        }

        // 9.4.4.10 — a for statement with every clause empty, exited by break (9.4.4.11).
        for (; ; )
        {
            total++;
            break;
        }

        return total;
    }

    /// <summary>
    /// 9.4.4.11 hazard — break, continue and goto. The label <c>Finish</c> is a declaration
    /// in this method, and <see cref="GotoAgain"/> declares a label of exactly the same name.
    /// </summary>
    public static int BreakContinueGoto(int limit)
    {
        int total = 0;
        for (int index = 0; index < limit; index++)
        {
            if (index == 2)
            {
                continue;
            }

            if (index == 5)
            {
                break;
            }

            total += index;
        }

        if (total > 100)
        {
            goto Finish;
        }

        total += 1;

    Finish:
        return total;
    }

    /// <summary>9.4.4.11 hazard — a second label with the same simple name, in another
    /// method of the same type.</summary>
    public static int GotoAgain(int seed)
    {
        int total = seed;
        goto Finish;

    Finish:
        return total;
    }

    /// <summary>9.4.4.12 — a throw statement, which is an endpoint and so assigns nothing.</summary>
    public static int ThrowStatement(int seed)
    {
        if (seed < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(seed));
        }

        return seed;
    }

    /// <summary>9.4.4.13 — return statements, one per branch, each an endpoint.</summary>
    public static string ReturnStatements(bool flag)
    {
        if (flag)
        {
            return "true";
        }

        return "false";
    }

    /// <summary>
    /// 9.4.4.14 hazard — try-catch. Both catch clauses of this one try statement declare
    /// <c>error</c>: two declarations, one name, sibling scopes inside one statement.
    /// </summary>
    public static string TryCatch(string text)
    {
        try
        {
            return int.Parse(text).ToString();
        }
        catch (System.FormatException error)
        {
            return $"format: {error.Message.Length}";
        }
        catch (System.OverflowException error)
        {
            return $"overflow: {error.Message.Length}";
        }
    }

    /// <summary>9.4.4.14 — an exception filter, and a catch clause that declares nothing.</summary>
    public static string TryCatchFiltered(string text)
    {
        try
        {
            return int.Parse(text).ToString();
        }
        catch (System.FormatException error) when (error.Message.Length > 0)
        {
            return "filtered";
        }
        catch
        {
            return "bare";
        }
    }

    /// <summary>9.4.4.15 — try-finally, where the finally block runs on every path out.</summary>
    public static int TryFinally(int seed)
    {
        int total = 0;
        try
        {
            total = seed;
            return total;
        }
        finally
        {
            VarStaticsBase.Tally += total;
        }
    }

    /// <summary>9.4.4.16 — try-catch-finally, which is the two rules composed.</summary>
    public static string TryCatchFinally(string text)
    {
        string outcome;
        try
        {
            outcome = int.Parse(text).ToString();
        }
        catch (System.Exception error)
        {
            outcome = error.GetType().Name;
        }
        finally
        {
            VarStaticsBase.Tally++;
        }

        return outcome;
    }

    /// <summary>
    /// 9.4.4.17 hazard — foreach statements. Three loops in one method, each declaring
    /// <c>item</c>: over an array, over a list, and over a tuple sequence with a
    /// deconstructing iteration variable.
    /// </summary>
    public static int ForeachStatements()
    {
        int total = 0;
        foreach (int item in VarArrayElements.Vector)
        {
            total += item;
        }

        System.Collections.Generic.List<string> names = ["a", "bb"];
        foreach (string item in names)
        {
            total += item.Length;
        }

        foreach ((int row, int column) in TyTupleSpellings.Cells)
        {
            total += row + column;
        }

        // 9.4.4.17 — an implicitly typed iteration variable, and a discard-free nested loop.
        foreach (var item in VarArrayElements.Jagged)
        {
            foreach (int inner in item)
            {
                total += inner;
            }
        }

        return total;
    }

    /// <summary>9.4.4.17 — a foreach whose iteration variable is a `ref`, which requires the
    /// sequence to be a span and makes each element assignable.</summary>
    public static int ForeachByReference()
    {
        System.Span<int> window = stackalloc int[3];
        foreach (ref int slot in window)
        {
            slot = 1;
        }

        int total = 0;
        foreach (ref readonly int slot in window)
        {
            total += slot;
        }

        return total;
    }

    /// <summary>
    /// 9.4.4.18 hazard — using statements. <c>handle</c> is declared three times in this one
    /// method: twice by declaration in sibling blocks, once by a parenthesized using.
    /// </summary>
    public static int UsingStatements()
    {
        int total = 0;
        {
            using var handle = new VarDisposableHandle(1);
            total += handle.Tag;
        }

        {
            using var handle = new VarDisposableHandle(2);
            total += handle.Tag;
        }

        using (var handle = new VarDisposableHandle(3))
        {
            total += handle.Tag;
        }

        // 9.4.4.18 — one using statement with two declarators, and one over an expression
        // rather than a declaration.
        using (VarDisposableHandle first = new(4), second = new(5))
        {
            total += first.Tag + second.Tag;
        }

        using (new VarDisposableHandle(6))
        {
            total += 6;
        }

        return total;
    }

    /// <summary>9.4.4.18 — the asynchronous form, whose resource is awaited on disposal.</summary>
    public static async System.Threading.Tasks.Task<int> AwaitUsingStatement()
    {
        await using var handle = new VarAsyncHandle(7);
        return handle.Tag;
    }

    /// <summary>
    /// 9.4.4.19 hazard — lock statements. The first locks an object, which compiles to
    /// Monitor; the second locks a System.Threading.Lock, which compiles to EnterScope and
    /// so calls a different method for the same syntax.
    /// </summary>
    public static int LockStatements()
    {
        int total = 0;
        lock (VarSynchronization.Gate)
        {
            int inner = 1;
            total += inner;
        }

        lock (VarSynchronization.TypedGate)
        {
            int inner = 2;
            total += inner;
        }

        return total;
    }

    /// <summary>9.4.4.20 — yield statements, which make this method an iterator; the local
    /// below survives across the yield because the compiler moves it to a state machine.</summary>
    public static System.Collections.Generic.IEnumerable<int> YieldStatements(int limit)
    {
        int running = 0;
        for (int index = 0; index < limit; index++)
        {
            running += index;
            yield return running;
        }

        if (limit < 0)
        {
            yield break;
        }

        yield return -1;
    }

    /// <summary>9.4.4.21 — constant expressions. A constant condition makes the other branch
    /// unreachable, and the definite assignment rules skip it; the unreachable-code warning
    /// this raises is the clause's whole observable effect.</summary>
    public static int ConstantExpressions()
    {
        const bool never = false;
        const int scale = 3;

        int total = scale;
        if (never)
        {
            total = -1;
        }

        while (never)
        {
            total = -2;
        }

        return total;
    }
}

/// <summary>9.4.4.18 — a disposable resource for the using statements above.</summary>
public sealed class VarDisposableHandle : System.IDisposable
{
    public VarDisposableHandle(int tag) => Tag = tag;

    public int Tag { get; }

    public void Dispose() => VarStaticsBase.Tally += Tag;
}

/// <summary>9.4.4.18 — the asynchronous counterpart.</summary>
public sealed class VarAsyncHandle : System.IAsyncDisposable
{
    public VarAsyncHandle(int tag) => Tag = tag;

    public int Tag { get; }

    public System.Threading.Tasks.ValueTask DisposeAsync()
    {
        VarStaticsBase.Tally += Tag;
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}

/// <summary>9.4.4.19 / 9.6 — the two things a lock statement can lock, and the atomicity
/// clause's subject beside them.</summary>
public static class VarSynchronization
{
    /// <summary>9.4.4.19 — an object to lock, which is the pre-standard form.</summary>
    public static readonly object Gate = new();

    /// <summary>9.4.4.19 hazard — a System.Threading.Lock (post-standard), for which the
    /// same `lock` syntax emits an entirely different call.</summary>
    public static readonly System.Threading.Lock TypedGate = new();

    /// <summary>9.6 — a variable whose reads and writes are atomic by the clause's rule.</summary>
    public static int Counter;

    /// <summary>9.6 — and the interlocked form, which is atomic as a read-modify-write.</summary>
    public static int BumpAtomically() => System.Threading.Interlocked.Increment(ref Counter);
}
