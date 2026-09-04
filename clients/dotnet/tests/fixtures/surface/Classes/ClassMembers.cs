// Clause 15.3 — class members. 15.3.1 lists the kinds; 15.3.2 is the instance type; 15.3.3 is
// what a member of a constructed type is; 15.3.7 is the constituent types of a declaration;
// 15.3.8 is the static/instance division. The function member kinds (15.6–15.13) are the
// sibling project's rows: what this file declares of every kind it can is constants, fields
// and nested types.

namespace Surface.Classes;

/// <summary>
/// 15.3.1 hazard — one class declaring a member of every non-function kind at once: a
/// constant, a static field, an instance field, and nested class, struct, interface, enum and
/// delegate declarations. The nested type names are unqualified and repeat names other
/// containers in this project also use, which is the whole point: a member identity has to
/// carry its container.
/// </summary>
public class ClsMemberKinds
{
    /// <summary>15.4 — a constant member.</summary>
    public const int Kinds = 8;

    /// <summary>15.5.2 — a static field member.</summary>
    public static int Instances;

    /// <summary>15.5.2 — an instance field member.</summary>
    public int Ordinal;

    /// <summary>15.3.9.1 — a nested class.</summary>
    public sealed class NestedClass
    {
        public int Value;
    }

    /// <summary>15.3.9.1 — a nested struct, which is a type member of a class.</summary>
    public struct NestedStruct
    {
        public int Value;
    }

    /// <summary>15.3.9.1 — a nested interface.</summary>
    public interface INestedMarker
    {
    }

    /// <summary>15.3.9.1 — a nested enumeration.</summary>
    public enum NestedEnum
    {
        First = 0,
        Second = 1,
    }

    /// <summary>15.3.9.1 — a nested delegate declaration, the last of the type kinds a class
    /// may contain. Clause 20 owns what a delegate *is*; here it is a member.</summary>
    /// <param name="value">The value handed to the handler.</param>
    public delegate void NestedHandler(int value);

    /// <summary>15.5.6.3 — a field whose type is one of this class's own nested types, so
    /// the container references its own member.</summary>
    public NestedStruct Own;
}

/// <summary>
/// 15.3.2 hazard — the instance type. Inside this declaration `ClsInstanceTyped&lt;TItem&gt;`
/// is the instance type and is spelled exactly as the declaration's own header is: the field
/// `Next` has the type of the thing that declares it, and the nested `Cursor` names it again.
/// </summary>
/// <typeparam name="TItem">Carried by the instance type.</typeparam>
public class ClsInstanceTyped<TItem>
{
    /// <summary>15.5.2 — one static field per closed constructed type (15.3.3).</summary>
    public static TItem? Seed;

    /// <summary>15.3.2 — a field of the instance type.</summary>
    public ClsInstanceTyped<TItem>? Next;

    /// <summary>15.5.1 — a field of the type parameter's type.</summary>
    public TItem? Item;

    /// <summary>15.3.9.7 — a nested type that names the instance type of its container.</summary>
    public sealed class Cursor
    {
        public ClsInstanceTyped<TItem>? Owner;
    }
}

/// <summary>
/// 15.3.3 hazard — members of constructed types. `Seed` is one field declaration and two
/// storage locations, one per closed type; `Cursor` is one nested type declaration and two
/// constructed types. Every reference below has to resolve back to the single declaration in
/// <see cref="ClsInstanceTyped{TItem}"/> while staying distinguishable by type argument.
/// </summary>
public static class ClsConstructedMembers
{
    /// <summary>15.3.3 — the static field of `ClsInstanceTyped&lt;int&gt;`.</summary>
    public static readonly int SeedOfInt = ClsInstanceTyped<int>.Seed;

    /// <summary>15.3.3 — the same declaration's static field at another type argument.</summary>
    public static readonly string? SeedOfString = ClsInstanceTyped<string>.Seed;

    /// <summary>15.3.3 — the nested type of a closed constructed type.</summary>
    public static ClsInstanceTyped<int>.Cursor? IntCursor;

    /// <summary>15.3.3 — and at the other argument, which is a different type.</summary>
    public static ClsInstanceTyped<string>.Cursor? StringCursor;
}

/// <summary>
/// 15.3.7 hazard — constituent types. Everything this declaration mentions is a constituent:
/// the base class, the implemented interface, the constraint, and every field's type. The
/// list a query has to produce is longer than the list of members.
/// </summary>
/// <typeparam name="TItem">Constrained, which makes the constraint a constituent too.</typeparam>
public class ClsConstituents<TItem> : ClsGenericBase<TItem>, IClsSink<TItem>
    where TItem : IClsTagged
{
    /// <summary>15.3.7 — an enumeration type as a constituent.</summary>
    public ClsGrade Grade;

    /// <summary>15.3.7 — a class type in this project.</summary>
    public ClsPlain? Plain;

    /// <summary>15.3.7 — an array type, whose element type is a constituent in turn.</summary>
    public int[] Counts = new int[2];

    /// <summary>15.3.7 — a nested type of another declaration.</summary>
    public ClsMemberKinds.NestedStruct Nested;

    /// <summary>15.3.7 — a constructed type built from a framework declaration.</summary>
    public System.Collections.Generic.List<string>? Names;

    /// <summary>15.3.7 — the type parameter itself.</summary>
    public TItem? Item;
}

/// <summary>
/// 15.3.8 — the static and instance division. `SharedTotal` exists once for the class and
/// `OwnTotal` once per instance; the declarations differ by one keyword.
/// </summary>
public class ClsStaticAndInstance
{
    /// <summary>15.3.8 — a static field.</summary>
    public static int SharedTotal;

    /// <summary>15.3.8 — an instance field of the same shape.</summary>
    public int OwnTotal;

    /// <summary>15.3.8 — a constant, which is static without the keyword.</summary>
    public const int Fixed = 1;

    /// <summary>15.3.8 — a static readonly field, static by keyword and by initializer.</summary>
    public static readonly int SharedFixed = Fixed;
}

/// <summary>
/// 15.3.8 hazard — a *static* member hiding an inherited *instance* member of the same name.
/// `ClsStaticAndInstance.OwnTotal` is per-instance and this `OwnTotal` is per-class: one
/// name, two declarations, and they are not even the same category of storage.
/// </summary>
public class ClsStaticHidesInstance : ClsStaticAndInstance
{
    /// <summary>15.3.5 — `new` is required, because the hidden member is inherited.</summary>
    public new static int OwnTotal;
}
