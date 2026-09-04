// Clause 24.6.2 — pointer indirection, `*P`.
//
// The expression has no name token in it, so it mints nothing: `Indexer.IndexTree` writes a
// reference from a `SimpleNameSyntax`, an invocation, an object creation or a member access,
// and `*at` is none of those. What the index holds about this clause is the signatures
// around it — a `csharp.Parameter` typed `pointerType(int)` and a return type that is the
// pointee — which is why the census row is exercised by declarations rather than by spans.

namespace Surface.Unsafe;

/// <summary>Clause 24.6.2 — indirection in each position the language allows it.</summary>
public unsafe class UnsIndirection
{
    /// <summary>Clause 24.6.2 — indirection as a value.</summary>
    public static int Read(int* at) => *at;

    /// <summary>Clause 24.6.2 — indirection as the target of an assignment: the result is a variable.</summary>
    public static void Write(int* at, int value) => *at = value;

    /// <summary>Clause 24.6.2 — indirection through a pointer to a pointer.</summary>
    public static int ReadThrough(int** at) => **at;

    /// <summary>Clause 24.6.2 — indirection yielding a struct value, copied out.</summary>
    public static UnsPoint ReadStruct(UnsPoint* at) => *at;

    /// <summary>
    /// Clause 24.6.2 — <c>(*P).M</c>, the form clause 24.6.3's <c>-&gt;</c> is defined as
    /// shorthand for. This one is a member access on a parenthesized indirection, so the
    /// index holds a <c>csharp.MemberAccessLocation</c> at <c>X</c>.
    /// </summary>
    public static int FieldThroughIndirection(UnsPoint* at) => (*at).X;

    /// <summary>Clause 24.6.2 — indirection as an argument, and in a compound assignment.</summary>
    public static int Accumulate(int* at)
    {
        *at += Read(at);
        return *at;
    }

    /// <summary>Clause 24.6.2 — indirection through a pointer to a type parameter.</summary>
    public static T ReadGeneric<T>(T* at)
        where T : unmanaged
        => *at;

    // Clause 24.6.2, written nowhere because it does not compile: `*` applied to a `void*`
    // is CS0242 — the pointee is unknown, so there is no type for the result. The
    // restriction is the compiler's, and leaves no fact behind either way.
}
