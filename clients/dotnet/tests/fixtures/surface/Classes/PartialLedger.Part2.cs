// Clause 15.2.7 — partial type declarations, part two of two. This half names no base class
// (only one part may) and adds a second interface to the list; the interface set of the
// finished type is the union of both parts' lists.

namespace Surface.Classes;

/// <summary>
/// 15.2.7 hazard — the second declaration of <c>ClsPartialLedger</c>. It adds `IClsNamed` to
/// the interface list the other part started, and it declares members of two more kinds.
/// </summary>
public partial class ClsPartialLedger : IClsNamed
{
    /// <summary>15.4 — a constant contributed by part two.</summary>
    public const string LedgerKind = "partial";

    /// <summary>15.5.3.1 — a readonly field contributed by part two, so the two parts'
    /// members differ in modifier as well as in name.</summary>
    private readonly int _checksum;

    /// <summary>15.5.6.2 — a static field whose initializer reads part two's own constant,
    /// so there is a reference inside a partial part as well as a declaration.</summary>
    public static readonly string KindCopy = LedgerKind;

    /// <summary>15.5.6.2 — and one that reads part *one*'s member through the finished type,
    /// which is the reference a per-file walk cannot resolve without the other file.</summary>
    public static readonly int ChecksumWidth = sizeof(int);
}

/// <summary>15.2.7 — the second part of the generic partial class, with the type parameter
/// list and the constraint repeated verbatim.</summary>
/// <typeparam name="TItem">The same parameter as part one declares.</typeparam>
public partial class ClsPartialStore<TItem>
    where TItem : notnull
{
    /// <summary>15.5.1 — part two's field, typed by the parameter part one also declared.</summary>
    public TItem? Tail;
}
