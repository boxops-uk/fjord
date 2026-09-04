// Clause 24.8 — fixed-size buffers: 24.8.1 (General), 24.8.2 (declarations), 24.8.3 (in
// expressions) and 24.8.4 (definite assignment checking).
//
// **A fixed-size buffer field is declared with an element type and indexed like an array,
// and the index holds a pointer.** Roslyn gives `fixed int Cells[8]` a field symbol whose
// `Type` is `int*`, so `CsharpEntities.Field` writes `type = pointerType(int)` — the `8` is
// in no field of `csharp.Field` and appears nowhere in the schema, and neither does the
// nested struct Roslyn synthesises to hold the storage (`<Cells>e__FixedBuffer`), because
// the walk reaches declarations through syntax and that type has no declaration to reach.
// So two buffers differing only in length are two `csharp.Field` rows that differ only in
// their names, and the length is lost silently.

namespace Surface.Unsafe;

/// <summary>
/// Clause 24.8.2 — fixed-size buffer declarations, one per element type the clause admits.
/// A buffer may only be declared in a struct, and only in an unsafe context.
/// </summary>
public unsafe struct UnsFixedBufferHost
{
    /// <summary>Clause 24.8.2 — a buffer of <c>int</c>, length 8.</summary>
    public fixed int Cells[8];

    /// <summary>Clause 24.8.2 — a buffer of <c>byte</c>, the shape an interop name uses.</summary>
    public fixed byte Name[16];

    /// <summary>Clause 24.8.2 — a buffer of <c>char</c>.</summary>
    public fixed char Label[4];

    /// <summary>Clause 24.8.2 — a buffer of <c>bool</c>.</summary>
    public fixed bool Flags[2];

    /// <summary>Clause 24.8.2 — a buffer of <c>double</c>.</summary>
    public fixed double Weights[3];

    /// <summary>Clause 24.8.2 — a buffer of length one, which is still a buffer and not a field.</summary>
    public fixed long Single[1];

    /// <summary>Clause 24.8.2 — an ordinary field beside the buffers, for contrast in the index.</summary>
    public int Count;

    /// <summary>
    /// Clause 24.8.3 — the buffer's name inside the declaring struct: it converts to a
    /// pointer to the first element, and needs a <c>fixed</c> statement because
    /// <c>this</c> is not known to be fixed.
    /// </summary>
    public int FirstCell()
    {
        fixed (int* at = Cells)
        {
            return at[0];
        }
    }

    /// <summary>Clause 24.8.3 — writing through a pinned buffer, and reading the count back.</summary>
    public int Fill(int value)
    {
        fixed (int* at = Cells)
        {
            for (int index = 0; index < 8; index++)
            {
                at[index] = value;
            }
        }

        Count = 8;
        return Count;
    }
}

/// <summary>Clause 24.8.2 — a struct whose buffer is reached through a nested struct, so the nesting is exercised.</summary>
public unsafe struct UnsBufferPair
{
    /// <summary>The left half.</summary>
    public UnsFixedBufferHost Left;

    /// <summary>The right half.</summary>
    public UnsFixedBufferHost Right;
}

/// <summary>Clause 24.8.3 — fixed-size buffers in expressions, from outside the declaring struct.</summary>
public unsafe class UnsFixedBufferUse
{
    /// <summary>A buffer-bearing struct on the heap, whose buffer is therefore moveable.</summary>
    private UnsFixedBufferHost _host;

    /// <summary>
    /// Clause 24.8.3 — through a pointer to the struct, where no <c>fixed</c> statement is
    /// needed: the receiver is already a pointer, so the buffer name is a pointer too.
    /// </summary>
    public static int FirstThroughPointer(UnsFixedBufferHost* host) => host->Cells[0];

    /// <summary>Clause 24.8.3 — assignment through a buffer element.</summary>
    public static void SetThroughPointer(UnsFixedBufferHost* host, int index, int value) =>
        host->Cells[index] = value;

    /// <summary>Clause 24.8.3 — a buffer of a different element type, read through the same receiver.</summary>
    public static byte NameByte(UnsFixedBufferHost* host, int index) => host->Name[index];

    /// <summary>
    /// Clause 24.8.3 — through a struct-typed <i>local</i>, which is a fixed variable, so the
    /// buffer may be indexed directly.
    /// </summary>
    public static int FirstOfLocal()
    {
        UnsFixedBufferHost host = default;
        host.Cells[0] = 41;
        return host.Cells[0];
    }

    /// <summary>Clause 24.8.3 — the buffer of a nested struct field, reached through two names.</summary>
    public static int FirstOfPair(UnsBufferPair* pair) => pair->Left.Cells[0];

    /// <summary>
    /// Clause 24.7 with 24.8.3 — a buffer as a fixed statement's initializer. The receiver
    /// is an instance field of a class, so it is moveable and the statement is required;
    /// over a struct-typed <i>local</i> the same statement is CS0213, because the buffer is
    /// already fixed and there is nothing left for the statement to do.
    /// </summary>
    public int PinnedFromField()
    {
        fixed (int* at = _host.Cells)
        {
            return at[1];
        }
    }

    /// <summary>
    /// Clause 24.8.4 — definite assignment: a fixed-size buffer is <i>not</i> subject to the
    /// checks an ordinary field is, so this local is read without ever being assigned and
    /// the compiler accepts it.
    /// </summary>
    public static int ReadUnassigned()
    {
        UnsFixedBufferHost host = default;
        return host.Cells[3] + host.Count;
    }

    // Clause 24.8.3, written nowhere because it does not compile: reaching a buffer through
    // a *moveable* receiver — a field of a class, or an array element — without pinning it
    // is CS1666. Clause 24.8.2's other refusals are the same: a buffer outside a struct is
    // CS1642, a buffer with a non-constant length is CS0150, and a buffer whose element type
    // is managed is CS1663.
}
