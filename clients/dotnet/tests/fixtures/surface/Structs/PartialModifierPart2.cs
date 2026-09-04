// Clause 16.2.4 — Partial modifier, part two of two. This file holds the rest of
// `StPartialLedger`, whose first part is in `PartialModifier.cs`. Read them together: the
// combined type has two fields, one constructor, one property, two methods and two
// attributes, and no single file shows more than half of that.
//
// The property here implements `IStPartialFace`, which only part one names. The attribute
// here is a second attribute on one type, unioned with part one's.

namespace Surface.Structs;

/// <summary>
/// Clause 16.2.4 — part two. The doc comment on a partial type is per part, so this
/// summary and part one's are two comments over one type, and only one of them can win.
/// </summary>
[StSurveyed("16.2.4 part two")]
public partial struct StPartialLedger
{
    /// <summary>
    /// Clause 16.2.4 / 16.4.11 — the property that satisfies the interface part one
    /// declared, reading the fields part one declared.
    /// </summary>
    public long Total => Credits - Debits;

    /// <summary>Clause 16.4.12 — a method in part two, called from part one.</summary>
    public string Kind() => Credits >= Debits ? "in credit" : "overdrawn";

    /// <summary>
    /// Clause 16.2.4 — a nested type in part two. The combined type's member list holds
    /// it, and the file that declares the constructor never sees it.
    /// </summary>
    public enum StLedgerSide
    {
        /// <summary>The credit side.</summary>
        Credit = 1,

        /// <summary>The debit side.</summary>
        Debit = 2,
    }
}

/// <summary>
/// Clause 16.2.4 — the use site, which sees one type. Nothing at a use site says which
/// part declared the member being reached, and the two reaches below land in two files.
/// </summary>
public static class StPartialUse
{
    /// <summary>Reaches the constructor from part one and the property from part two.</summary>
    public static long NetOf(long credits, long debits) =>
        new StPartialLedger(credits, debits).Total;

    /// <summary>Reaches the method in part one, which reaches the method in part two.</summary>
    public static string Describe() => new StPartialLedger(5, 2).DescribeAcrossParts();

    /// <summary>Reaches the nested enum, which only part two declares.</summary>
    public static StPartialLedger.StLedgerSide Side = StPartialLedger.StLedgerSide.Credit;

    /// <summary>Clause 16.2.4 — the combined type seen through the interface part one named.</summary>
    public static long ThroughInterface(long credits)
    {
        IStPartialFace face = new StPartialLedger(credits, 0);
        return face.Total;
    }
}
