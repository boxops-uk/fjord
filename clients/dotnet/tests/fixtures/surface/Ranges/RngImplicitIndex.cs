// Clause 18.4.2 — implicit `Index` support. When a type is countable and has an accessible
// `int` indexer, `E[I]` where `I` is an `Index` is rewritten to use the countable property and
// that indexer, and step 4 of the clause says the rewrite uses "the get or set accessor",
// whichever the context calls for.
//
// The read/write split is the hazard, and the settable indexer below is what makes it visible.
// `s[^1]` compiles to `get_Item` and `s[^2] = v` to `set_Item` — two distinct `IMethodSymbol`s
// in `GetMembers()` — while `GetSymbolInfo` returns the *property* at both spans. A producer
// keyed on the property cannot tell the read from the write; one keyed on the accessor mints
// `get_Item().` and `set_Item().`, which no declaration walk ever emits, because `Declare`
// runs off `BasePropertyDeclarationSyntax` and mints only `this[]`. One declaration, two
// spellings, decided by which side of the index wrote the fact.
//
// The clause's own precondition — "`E[0]` is valid and uses the same indexer" — is what a
// second indexer would test, and a second indexer is a refused write. See `README.md`.

namespace Surface.Ranges;

/// <summary>
/// The countable-plus-indexer shape of 18.4.2, with a settable indexer so both accessors are
/// reached from a from-end index (18.4.2).
/// </summary>
public sealed class RngSequence
{
    private readonly int[] _items = new int[4];

    /// <summary>The countable property, spelled <c>Length</c>.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// The <c>int</c> indexer, with both accessors, which is what the clause's step 4 needs
    /// in order to mean anything.
    /// </summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The element there.</returns>
    public int this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }
}

/// <summary>
/// The same shape with <c>Count</c> instead of <c>Length</c> and a get-only indexer, so the
/// countable property's name and the accessor set are both varied across the corpus (18.4.2).
/// </summary>
public sealed class RngCountedSequence
{
    private readonly string[] _labels = { "north", "south", "east", "west" };

    /// <summary>The countable property, spelled <c>Count</c>.</summary>
    public int Count => _labels.Length;

    /// <summary>The <c>int</c> indexer, read-only.</summary>
    /// <param name="index">A from-start offset.</param>
    /// <returns>The label there.</returns>
    public string this[int index] => _labels[index];
}

/// <summary>
/// Every context a from-end index can appear in over a pattern-conforming type: a read, a
/// write, a compound assignment, an increment, and an argument (18.4.2).
/// </summary>
public static class RngIndexPatternUses
{
    /// <summary>The settable sequence.</summary>
    public static readonly RngSequence Sequence = new();

    /// <summary>The read-only sequence, countable through <c>Count</c>.</summary>
    public static readonly RngCountedSequence Labels = new();

    /// <summary>18.4.2 — a read through the get accessor.</summary>
    /// <returns>The last element.</returns>
    public static int Read() => Sequence[^1];

    /// <summary>
    /// 18.4.2 — a write through the set accessor, at the same kind of span as the read. This
    /// is the smallest C# where an element access is an assignment target rather than a value.
    /// </summary>
    /// <param name="value">What to write.</param>
    public static void Write(int value) => Sequence[^2] = value;

    /// <summary>
    /// 18.4.2 — a compound assignment and an increment, each of which uses *both* accessors
    /// at one span.
    /// </summary>
    public static void ReadAndWrite()
    {
        Sequence[^1] += 3;
        Sequence[^2]++;
    }

    /// <summary>18.4.2 — the from-end form over the <c>Count</c>-spelled type.</summary>
    /// <returns>The last label.</returns>
    public static string LastLabel() => Labels[^1];

    /// <summary>
    /// 18.4.2 — an <c>Index</c> arriving as a value, which is the form the clause is actually
    /// written about: the `^` is not part of the rewrite.
    /// </summary>
    /// <param name="index">Where to read.</param>
    /// <returns>The element there.</returns>
    public static int At(System.Index index) => Sequence[index];

    /// <summary>
    /// 18.4.2 — the ordinary from-start access, which the clause requires to be valid for the
    /// rewrite to be legal at all.
    /// </summary>
    /// <returns>The first element.</returns>
    public static int First() => Sequence[0];

    /// <summary>
    /// 18.4.2 — the countable property and the indexer named directly, so each has one
    /// reference in the corpus that a walk over names can find.
    /// </summary>
    /// <returns>The length, and the last element reached by arithmetic.</returns>
    public static (int Length, int Last) Named() =>
        (Sequence.Length, Sequence[Sequence.Length - 1]);
}
