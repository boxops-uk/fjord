// Clause 17.2.3 — a single-dimensional array `T[]` implements `IList<T>` and
// `IReadOnlyList<T>` and their base interfaces, and the conversion follows the element
// type's own reference conversions in both directions.
//
// This is 17.2.3's own example with the two lines the standard marks as compile-time errors
// left out — a fixture has to build, so `IList<string> lst2 = oa1;` lives in this comment and
// nowhere else. The casts the standard marks as run-time exceptions are kept: they compile,
// and nothing here is executed.
//
// The indexable fact is a heritage edge nobody declared. `string[] : IList<string>` is a base
// interface of a type that has no declaration in source and no symbol of its own — an array
// type is not a `SymbolKind.NamedType` — so the implementation relation this clause states
// has no declaration site at either end that a walk over source could reach.

using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>
/// The conversions between array types and the generic collection interfaces (17.2.3).
/// </summary>
public static class ArrCollectionInterfaces
{
    /// <summary>
    /// 17.2.3's example, restricted to the assignments the standard says are legal.
    /// </summary>
    /// <returns>How many of them ended up non-empty, so none is dead.</returns>
    public static int Assignments()
    {
        string[] sa = new string[5];
        object[] oa1 = new object[5];
        object[] oa2 = sa;

        IList<string> lst1 = sa;                            // Ok
        IList<object> lst3 = sa;                            // Ok — element covariance
        IList<object> lst4 = oa1;                           // Ok
        IList<string> lst5 = (IList<string>)oa1;            // compiles; throws at run time
        IList<string> lst6 = (IList<string>)oa2;            // Ok — oa2 really is a string[]

        IReadOnlyList<string> lst7 = sa;                    // Ok
        IReadOnlyList<object> lst9 = sa;                    // Ok — element covariance
        IReadOnlyList<object> lst10 = oa1;                  // Ok
        IReadOnlyList<string> lst11 = (IReadOnlyList<string>)oa1;  // compiles; throws
        IReadOnlyList<string> lst12 = (IReadOnlyList<string>)oa2;  // Ok

        return lst1.Count
            + lst3.Count
            + lst4.Count
            + lst5.Count
            + lst6.Count
            + lst7.Count
            + lst9.Count
            + lst10.Count
            + lst11.Count
            + lst12.Count;
    }

    /// <summary>
    /// 17.2.3 — "and its base interfaces": every interface between <c>T[]</c> and
    /// <c>IEnumerable</c>, each named so each conversion is written.
    /// </summary>
    /// <param name="labels">A rank-one array.</param>
    /// <returns>What each interface answers about it.</returns>
    public static (int List, int Collection, int ReadOnly, bool Any) BaseInterfaces(string[] labels)
    {
        IList<string> list = labels;
        ICollection<string> collection = labels;
        IReadOnlyCollection<string> readOnly = labels;
        IEnumerable<string> enumerable = labels;

        return (list.Count, collection.Count, readOnly.Count, enumerable.GetEnumerator().MoveNext());
    }

    /// <summary>
    /// 17.2.3's closing sentence — wherever there is a conversion from <c>S[]</c> to
    /// <c>IList&lt;T&gt;</c> there is an explicit one back, so this is the direction that
    /// recovers an array type from an interface.
    /// </summary>
    /// <param name="list">A list that is really an array.</param>
    /// <returns>The array.</returns>
    public static string[] BackToArray(IList<string> list) => (string[])list;

    /// <summary>
    /// The same conversion at an argument position rather than an assignment, which is where
    /// it is invisible in the source: the array is spelled, the interface is not.
    /// </summary>
    /// <returns>How many labels the interface saw.</returns>
    public static int ThroughAnArgument() => Counted(ArrGeneral.Labels);

    /// <summary>
    /// 17.2.3 — an array reaches this through the implicit conversion, and a
    /// <c>List&lt;T&gt;</c> reaches it by declaring the interface. The parameter cannot tell
    /// which arrived.
    /// </summary>
    /// <param name="items">Anything that lists strings.</param>
    /// <returns>How many there are.</returns>
    public static int Counted(IReadOnlyList<string> items) => items.Count;

    /// <summary>
    /// 17.2.3's last paragraph: some members of the implemented interface may throw, because
    /// an array's length is fixed at creation (17.3). Written, not called.
    /// </summary>
    /// <param name="labels">A rank-one array.</param>
    /// <returns>The list view whose <c>Add</c> would throw.</returns>
    public static IList<string> Fixed(string[] labels)
    {
        IList<string> list = labels;

        return list.IsReadOnly ? new List<string>(labels) : list;
    }
}
