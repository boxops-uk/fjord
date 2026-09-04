// Clause 15.2.2 — class modifiers. 15.2.2.1 lists them (`new`, the four access modifiers,
// `abstract`, `sealed`, `static`, `unsafe`, `partial`); 15.2.2.2 is `abstract` and 15.2.2.3 is
// `sealed`. `static` is StaticClasses.cs, `partial` is PartialLedger.cs, and `new` on a class
// is only legal on a nested one, so it is NestedTypes.cs.

namespace Surface.Classes;

/// <summary>
/// 15.2.2.2 — an abstract class. It may not be instantiated, so nothing in this project
/// writes `new ClsAbstractRecord()`; the fields are the only facts it carries.
/// </summary>
public abstract class ClsAbstractRecord
{
    /// <summary>15.5.3.1 — a protected readonly field, never assigned (CS0649 is the point:
    /// an abstract class with no constructor of its own still declares storage).</summary>
    protected readonly int Version;

    /// <summary>15.3.6 — `protected internal`, the union accessibility.</summary>
    protected internal ClsGrade Grade;
}

/// <summary>15.2.2.2 — an abstract class derived from an abstract class, which is the only
/// way the modifier appears twice in one chain.</summary>
public abstract class ClsAbstractMiddle : ClsAbstractRecord
{
    /// <summary>15.5.1 — a field declared halfway down an abstract chain.</summary>
    protected int Depth;
}

/// <summary>
/// 15.2.2.3 hazard — a sealed class closing an abstract chain, whose `new` field takes the
/// name of the `Version` its base declares and gives it a different type (15.3.5). Two field
/// declarations want the name `Version`; a query has to see two rows in two containers, not
/// one row whose type depends on which declaration was walked last.
/// </summary>
public sealed class ClsSealedLeaf : ClsAbstractMiddle
{
    /// <summary>15.3.5 — hides <c>ClsAbstractRecord.Version</c>, which is an `int`.</summary>
    public new readonly string Version = "leaf";
}

/// <summary>15.2.2.3 — `sealed` with no inheritance in sight, so the modifier is the whole
/// fact about the declaration.</summary>
internal sealed class ClsSealedStandalone
{
    internal int Slot;
}

/// <summary>
/// 15.2.2.1 — the `unsafe` class modifier. The body declares no pointer type: the modifier
/// widens the context and clause 23 owns what may then be written in it.
/// </summary>
internal unsafe class ClsUnsafeMarked
{
    internal int Slot;
}

/// <summary>15.2.2.1 — `private` is legal on a class only where the class is a member, so the
/// modifier list is only fully reachable through a nesting.</summary>
public sealed class ClsModifierHost
{
    /// <summary>15.2.2.1 — a `private` class, which no top-level declaration can be.</summary>
    private sealed class ClsHostPrivate
    {
        public int Value;
    }

    /// <summary>15.5.6.2 — and a use of it, so the private nesting is reachable from a fact.</summary>
    private static readonly ClsHostPrivate Held = new ClsHostPrivate { Value = 1 };

    /// <summary>15.5.6.2 — the reachable projection of that private member.</summary>
    public static readonly int HeldValue = Held.Value;
}
