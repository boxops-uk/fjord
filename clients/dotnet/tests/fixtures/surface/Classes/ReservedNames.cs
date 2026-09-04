// Clause 15.3.10 — reserved member names. 15.3.10.1 states the mechanism: certain member
// declarations reserve *signatures* in the containing type's declaration space, so that a
// property `P` makes `get_P`/`set_P` unavailable. The reservation is a consequence of the
// declaration that causes it, which is why this file can declare the reserved *shapes* and
// compile: nothing here declares the property, event, indexer, finalizer or operator that
// would reserve them.
//
// 15.3.10.2 (properties), .3 (events), .4 (indexers), .5 (finalizers) and .6 (operators) each
// need the member kind they name, and those are the sibling project's rows.

namespace Surface.Classes;

/// <summary>
/// 15.3.10.1 hazard — members named exactly as a property, an event, an indexer and a
/// finalizer would reserve. Each is legal here and would be an error in a class that
/// declared the corresponding member: an index cannot tell these apart from compiler-emitted
/// accessors by name alone, and a query for "the getter of `Total`" must not find this field.
/// </summary>
public class ClsReservedNameShapes
{
    /// <summary>15.3.10.2 shape — a field named as a property `Total`'s getter would be.
    /// Legal because no property `Total` is declared in this class.</summary>
    public int get_Total;

    /// <summary>15.3.10.2 shape — and as its setter.</summary>
    public int set_Total;

    /// <summary>15.3.10.3 shape — as an event `Changed`'s add accessor.</summary>
    public int add_Changed;

    /// <summary>15.3.10.4 shape — as an indexer's getter, whose reserved name is fixed.</summary>
    public int get_Item;

    /// <summary>15.3.10.5 shape — as a finalizer's, which is the one reserved name that is
    /// a plain identifier.</summary>
    public const string Finalize = "not a finalizer";

    /// <summary>15.3.10.6 shape — as a binary `+` operator's method name.</summary>
    public static readonly int op_Addition = 1;

    /// <summary>15.3.10.1 — a nested type in a reserved shape, which no clause reserves:
    /// the reservation is over signatures of methods, so a type of that name is unaffected.</summary>
    public sealed class get_Nested
    {
        public int Value;
    }
}
