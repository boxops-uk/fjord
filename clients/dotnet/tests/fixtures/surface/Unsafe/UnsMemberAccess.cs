// Clause 24.6.3 — pointer member access, `P->M`.
//
// **This is the arm of clause 24 that looks exactly like safe code to the index.** Roslyn
// parses `at->X` as a `MemberAccessExpressionSyntax` — kind `PointerMemberAccessExpression`,
// the same node type as `a.b` — so it falls into `Indexer.IndexTree`'s member-access arm
// and mints a `csharp.MemberAccessLocation` over the span of the name alone. A query
// counting member accesses gets the `->` ones mixed in with the `.` ones, and nothing in the
// fact says which operator was written.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.3 — a struct of pointers, so that an access can be chained through two of them.</summary>
public unsafe struct UnsSegment
{
    /// <summary>The first point of the segment.</summary>
    public UnsPoint* Head;

    /// <summary>The last point of the segment.</summary>
    public UnsPoint* Tail;
}

/// <summary>Clause 24.6.3 — every kind of member reached through <c>-&gt;</c>.</summary>
public unsafe class UnsPointerMemberAccess
{
    /// <summary>Clause 24.6.3 — a field read through <c>-&gt;</c>.</summary>
    public static int ReadField(UnsPoint* at) => at->X;

    /// <summary>Clause 24.6.3 — a field written through <c>-&gt;</c>, which is a variable.</summary>
    public static void WriteField(UnsPoint* at, int x) => at->X = x;

    /// <summary>Clause 24.6.3 — a method invoked through <c>-&gt;</c>.</summary>
    public static int CallMethod(UnsPoint* at) => at->Sum();

    /// <summary>Clause 24.6.3 — two arrows in one expression, through a pointer-typed field.</summary>
    public static int Chained(UnsSegment* at) => at->Head->Y;

    /// <summary>Clause 24.6.3 — an arrow on the result of pointer arithmetic.</summary>
    public static int Offset(UnsPoint* at, int index) => (at + index)->X;

    /// <summary>Clause 24.6.3 — a compound assignment through an arrow.</summary>
    public static int Bump(UnsPoint* at)
    {
        at->Y += 1;
        return at->Y;
    }
}
