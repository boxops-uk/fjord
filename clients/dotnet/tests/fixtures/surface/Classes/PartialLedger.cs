// Clause 15.2.7 — partial type declarations, part one of two. The parts are in *two files*
// on purpose: two parts in one file is one of the five shapes that kills an indexing run, and
// it lives in a quarantine project. No member here is partial — a partial member's two halves
// are another of the five.
//
// The rules this file's half carries: the base class may be named by at most one part
// (15.2.7), each part may add to the interface list, and a generic partial type must repeat
// its type parameter list and its constraints identically in every part.

namespace Surface.Classes;

/// <summary>
/// 15.2.7 hazard — one class, two declarations, two files. Every part contributes members to
/// one type, so the members below and the members in PartialLedger.Part2.cs belong to the
/// same container: an index has two declaration sites for one type identity, and a query has
/// to see one type whose members came from both files rather than two types or one file's
/// worth of members.
/// </summary>
public partial class ClsPartialLedger : ClsBase, IClsTagged
{
    /// <summary>15.5.1 — a field declared in part one.</summary>
    public int EntryCount;

    /// <summary>15.3.9.1 — a nested type declared in part one, whose containing type is
    /// spread over two files.</summary>
    public sealed class PartOneNested
    {
        public int Depth;
    }
}

/// <summary>
/// 15.2.7 — a generic partial class. Both parts repeat `TItem` and repeat the constraint;
/// the type parameter has two declaration sites and one identity, which is the same hazard
/// as the type's, one level down.
/// </summary>
/// <typeparam name="TItem">Declared in both parts, identically, as the standard requires.</typeparam>
public partial class ClsPartialStore<TItem>
    where TItem : notnull
{
    /// <summary>15.5.1 — part one's field, typed by the type parameter.</summary>
    public TItem? Head;
}
