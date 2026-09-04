// Clause 15.3.4 — inheritance: a class inherits the members of its base class, transitively,
// and a reference through a derived type resolves to the base's declaration. Clause 15.3.5 —
// the `new` modifier, which is how a declaration says it means to hide an inherited member of
// the same name.

namespace Surface.Classes;

/// <summary>15.3.4 — the root of the hierarchy this file inherits down.</summary>
public class ClsRoot
{
    /// <summary>15.3.4 — inherited by every class below, and hidden by two of them.</summary>
    public int Depth;

    /// <summary>15.4 — an inherited constant, which `ClsHidesConst` hides.</summary>
    public const int RootCeiling = 10;

    /// <summary>15.3.6 — an internal field, hidden below *without* `new`.</summary>
    internal ClsGrade Grade;
}

/// <summary>15.3.4 — one level down: `Depth`, `RootCeiling` and `Grade` are members of this
/// class too, declared nowhere in it.</summary>
public class ClsInherits : ClsRoot
{
    public int Extra;
}

/// <summary>15.3.4 — two levels down, so the inherited set arrives transitively.</summary>
public sealed class ClsInheritsDeep : ClsInherits
{
    public int Deeper;
}

/// <summary>
/// 15.3.5 hazard — a `new` field taking an inherited field's name at the same type. Nothing
/// distinguishes the two declarations but their container, and a reference through this class
/// means this one while a reference through the base means the base's.
/// </summary>
public class ClsHidesField : ClsRoot
{
    /// <summary>15.3.5 — hides <c>ClsRoot.Depth</c>, same name, same type.</summary>
    public new int Depth;
}

/// <summary>15.3.5 hazard — a second class hiding the same inherited name, at a different
/// type. Three declarations now want `Depth`, in three containers.</summary>
public class ClsHidesFieldRetyped : ClsRoot
{
    /// <summary>15.3.5 — hides <c>ClsRoot.Depth</c>, which is an `int`.</summary>
    public new string Depth = "hidden";
}

/// <summary>15.3.5 — `new` on a constant, hiding an inherited constant.</summary>
public class ClsHidesConst : ClsRoot
{
    /// <summary>15.4 — hides <c>ClsRoot.RootCeiling</c>, with a different value.</summary>
    public new const int RootCeiling = 20;
}

/// <summary>
/// 15.3.5 hazard — hiding *without* `new`, which is legal and warns (CS0108). The pair is
/// identical in effect to <see cref="ClsHidesField"/>'s and differs in whether the source
/// says so: an index that records the modifier records nothing here and the hiding is still
/// in force.
/// </summary>
public class ClsHidesWithoutNew : ClsRoot
{
    /// <summary>15.3.5 — hides the inherited <c>Grade</c>, which is a `ClsGrade`.</summary>
    internal int Grade;
}

/// <summary>
/// 15.3.4 — the references. Each initializer reads a field through a *derived* type and the
/// declaration it resolves to is the base's, except the last two, which resolve to the
/// hiding declarations.
/// </summary>
public static class ClsInheritanceReads
{
    /// <summary>15.3.4 — read through the immediate derived type; declared in `ClsRoot`.</summary>
    public static readonly int DepthViaDerived = new ClsInherits().Depth;

    /// <summary>15.3.4 — read through two levels of derivation; still `ClsRoot.Depth`.</summary>
    public static readonly int DepthViaDeep = new ClsInheritsDeep().Depth;

    /// <summary>15.3.4 — an inherited constant read through the derived type.</summary>
    public static readonly int CeilingViaDerived = ClsInherits.RootCeiling;

    /// <summary>15.3.5 — read through the hiding type, so this one is `ClsHidesField.Depth`.</summary>
    public static readonly int DepthViaHider = new ClsHidesField().Depth;

    /// <summary>15.3.5 — and the hidden one, reached by converting to the base type.</summary>
    public static readonly int DepthViaBaseOfHider = ((ClsRoot)new ClsHidesField()).Depth;
}
