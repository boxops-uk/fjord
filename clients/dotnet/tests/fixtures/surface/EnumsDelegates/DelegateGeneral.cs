// Clause 21 — delegates — and clause 21.1's general characteristics: a delegate declaration
// defines a reference type that encapsulates a method with a particular signature and return
// type; an instance can hold static methods, instance methods, or several of either; and an
// instance's invocation list is immutable, so combining two delegates yields a third and
// changes neither. One `delegate` line therefore mints a class whose base is
// System.MulticastDelegate and whose members — a constructor, `Invoke`, `BeginInvoke` and
// `EndInvoke` — are written nowhere at all.

namespace Surface.EnumsDelegates;

/// <summary>
/// 21 / 21.1 — the headline delegate declaration: no parameters, no return value. The type
/// this mints has four members that appear in no source file.
/// </summary>
public delegate void EdSignal();

/// <summary>
/// 21.1 — the general characteristics of a delegate, exercised against static and instance
/// method targets and against the immutability of the invocation list.
/// </summary>
public sealed class EdSignalBoard
{
    private int _count;

    /// <summary>21.1 — an instance method, so a delegate over it captures this object.</summary>
    public void Bump() => _count++;

    /// <summary>21.1 — a static method, so a delegate over it captures nothing.</summary>
    public static void Log()
    {
    }

    /// <summary>21.1 — the count the instance target advances.</summary>
    public int Count => _count;

    /// <summary>
    /// 21.1 — one delegate type over two kinds of target. Both conversions produce values of
    /// <see cref="EdSignal"/>, and the combined value's invocation list holds both methods.
    /// </summary>
    public static EdSignal Both(EdSignalBoard board)
    {
        EdSignal fromStatic = Log;
        EdSignal fromInstance = board.Bump;
        return fromStatic + fromInstance;
    }

    /// <summary>
    /// 21.1 — the invocation list is immutable: combining leaves both operands with the
    /// lists they had, and produces a third instance.
    /// </summary>
    public static bool ListIsImmutable(EdSignal first, EdSignal second)
    {
        EdSignal combined = first + second;
        return !object.ReferenceEquals(combined, first)
            && first.GetInvocationList().Length == 1
            && combined.GetInvocationList().Length == 2;
    }

    /// <summary>
    /// 21.1 — a delegate is a reference type, so a variable of one can be null and a
    /// delegate value can be compared with null.
    /// </summary>
    public static bool Unassigned()
    {
        EdSignal? none = null;
        return none is null;
    }
}
