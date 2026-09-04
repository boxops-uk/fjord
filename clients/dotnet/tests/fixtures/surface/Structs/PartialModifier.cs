// Clause 16.2.4 — Partial modifier. A struct declaration with `partial` is one part of a
// type whose other parts appear elsewhere in the same program; the parts are combined at
// compile time into a single type, and the base interface list, the members and the
// attributes of every part are unioned.
//
// The parts here are in TWO FILES on purpose. A partial type whose parts sit in one file
// is one of the five shapes that refuse a write in this indexer, so it is not written
// anywhere in this project — see the README. Two files is the shape that is safe to index
// and is still the interesting question: one type, two declaration sites.
//
// Nothing in this project declares a partial MEMBER — no `partial void`, no partial
// property, indexer, constructor or event. Those belong to the quarantine projects.

namespace Surface.Structs;

/// <summary>
/// Clause 16.2.4 — the interface one part of the partial struct names. The other part
/// names none, and the combined type implements it.
/// </summary>
public interface IStPartialFace
{
    /// <summary>The running total.</summary>
    long Total { get; }
}

/// <summary>
/// Clause 16.2.4 — part one of two. This part carries the fields, the constructor and the
/// base interface list; <c>PartialModifierPart2.cs</c> carries a property, a method and an
/// attribute. Both parts must repeat the `partial` modifier and must agree on `struct`.
/// </summary>
[StSurveyed("16.2.4 part one")]
public partial struct StPartialLedger : IStPartialFace
{
    /// <summary>Clause 16.3.1 — a field declared in part one.</summary>
    public long Credits;

    /// <summary>Clause 16.3.1 — a second field declared in part one.</summary>
    public long Debits;

    /// <summary>
    /// Clause 16.4.9 — the constructor, declared in part one. Its declaration is in this
    /// file and the fields it assigns are too, but the property it satisfies is not.
    /// </summary>
    public StPartialLedger(long credits, long debits)
    {
        Credits = credits;
        Debits = debits;
    }

    /// <summary>
    /// Clause 16.2.4 — a method in part one that calls a method declared in part two, so
    /// the reference crosses the file boundary within one type.
    /// </summary>
    public string DescribeAcrossParts() => $"{Total} ({Kind()})";
}
