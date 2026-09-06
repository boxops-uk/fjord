// Clause 8.3.11 — tuple types. A tuple type is a value type with element names that live
// outside metadata: (int, int) and (int A, int B) are the same type, and both are
// System.ValueTuple<int, int>. 8.3.11.1 states the type, 8.3.11.2 the elision of
// intermediate tuple creation, and 8.3.11.3 the runtime representation.

namespace Surface.Types;

/// <summary>
/// 8.3.11.1 hazard — one type, three spellings. All three fields have exactly the same
/// type; only the element names differ, and element names are an attribute, not a type.
/// </summary>
public static class TyTupleSpellings
{
    /// <summary>8.3.11.1 — a tuple type with element names.</summary>
    public static (int Row, int Column) Named = (1, 2);

    /// <summary>8.3.11.1 hazard — the same type with its names elided.</summary>
    public static (int, int) Unnamed = (3, 4);

    /// <summary>8.3.11.3 hazard — and the same type spelled as the struct it is.</summary>
    public static System.ValueTuple<int, int> Spelled = new(5, 6);

    /// <summary>8.3.11.1 — assigning between the three, which needs no conversion.</summary>
    public static (int, int) Copy()
    {
        Unnamed = Named;
        Spelled = Unnamed;
        return Spelled;
    }

    /// <summary>8.3.11.1 — a tuple type in return position, with names the caller can use.</summary>
    public static (string Name, int Count) Summarize(string name) => (name, name.Length);

    /// <summary>8.3.11.1 — a tuple type as a parameter, and as a type argument.</summary>
    public static int Sum((int Left, int Right) pair) => pair.Left + pair.Right;

    public static System.Collections.Generic.List<(int Row, int Column)> Cells = [(0, 0), (1, 1)];

    /// <summary>8.3.11.1 — a nested tuple type, whose element is itself a tuple.</summary>
    public static ((int X, int Y) Start, (int X, int Y) End) Segment = ((0, 0), (1, 1));

    /// <summary>8.3.11.1 — element access by name, by position, and through the nesting.</summary>
    public static int Reach() => Named.Row + Unnamed.Item1 + Segment.End.Y;
}

/// <summary>
/// 8.3.11.2 hazard — the places where no tuple is actually constructed. Deconstruction,
/// tuple assignment and tuple equality all elide the intermediate ValueTuple, so a
/// constructor reference recorded for the literal would be a reference to a call that the
/// compiled code never makes.
/// </summary>
public static class TyTupleElision
{
    /// <summary>8.3.11.2 — a deconstructing declaration; the right-hand tuple is elided.</summary>
    public static int Deconstruct()
    {
        var (row, column) = TyTupleSpellings.Summarize("abc");
        return row.Length + column;
    }

    /// <summary>
    /// 8.3.11.2 / 9.4.4.25 — a swap through tuple assignment. Two locals, one statement,
    /// and no tuple in the emitted code.
    /// </summary>
    public static (int, int) Swap(int left, int right)
    {
        (left, right) = (right, left);
        return (left, right);
    }

    /// <summary>8.3.11.2 — tuple equality, which compares elementwise and builds nothing.</summary>
    public static bool Same((int, int) pair) => pair == (1, 2);

    /// <summary>8.3.11.2 — a deconstruction with a discard (9.2.9.2), which declares no variable.</summary>
    public static int RowOnly()
    {
        var (row, _) = TyTupleSpellings.Named;
        return row;
    }

    /// <summary>8.3.11.2 — deconstruction into existing variables, which declares nothing at all.</summary>
    public static int Reuse()
    {
        int row;
        int column;
        (row, column) = TyTupleSpellings.Unnamed;
        return row * column;
    }
}

/// <summary>
/// 8.3.11.3 hazard — the runtime representation. A tuple of eight elements is not
/// ValueTuple with eight type arguments: it is ValueTuple&lt;T1..T7, TRest&gt; whose eighth
/// argument is a one-element tuple. The two fields below have the same type written the two
/// ways, and the nesting is visible only in the second.
/// </summary>
public static class TyTupleRepresentation
{
    /// <summary>8.3.11.3 — eight elements in tuple syntax.</summary>
    public static (int A, int B, int C, int D, int E, int F, int G, int H) Wide =
        (1, 2, 3, 4, 5, 6, 7, 8);

    /// <summary>8.3.11.3 hazard — the same type spelled as the nested struct it really is.</summary>
    public static System.ValueTuple<int, int, int, int, int, int, int, System.ValueTuple<int>> Nested =
        new(1, 2, 3, 4, 5, 6, 7, new System.ValueTuple<int>(8));

    /// <summary>8.3.11.3 — the eighth element is reached as Rest.Item1 in the nested spelling.</summary>
    public static int EighthTwoWays() => Wide.H + Nested.Rest.Item1;

    /// <summary>8.3.11.3 — assignment between the two spellings needs no conversion, because
    /// they are one type.</summary>
    public static void Align() => Nested = Wide;

    /// <summary>8.3.11.3 — the seven-element boundary case, which is flat.</summary>
    public static (int, int, int, int, int, int, int) Flat = (1, 2, 3, 4, 5, 6, 7);
}
