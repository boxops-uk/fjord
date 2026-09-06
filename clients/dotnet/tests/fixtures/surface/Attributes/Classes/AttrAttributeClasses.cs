// Clause 23.2.1 (attribute classes, general), 23.2.2 (attribute usage) and 23.5.2 (the
// AttributeUsage attribute).
//
// An attribute class is a class deriving directly or indirectly from System.Attribute, and
// that is all it is — so every declaration here is an ordinary class declaration and an
// index that holds a fact about "is an attribute class" is holding a fact about the base
// type chain, not about a keyword. `AttrDerivedMarkAttribute` derives *indirectly*, which
// is the case a one-level check gets wrong.
//
// Two hazards live in this file.
//
// The first is the suffix rule. `AttrMarkAttribute` may be applied as `[AttrMark]` or as
// `[AttrMarkAttribute]`, and both spellings are used below on different targets: two
// reference texts of different lengths naming one declaration. An index keyed on the
// identifier as written mints two names for it; one keyed on the resolved symbol mints one
// and loses which spelling the source used.
//
// The second is `AttrTwin`/`AttrTwinAttribute`, declared side by side. Both are attribute
// classes and the short spelling `[AttrTwin]` is then CS1614 — ambiguous — so only the
// long spelling appears below, and `AttrTwin` is applied by *no* application at all. Two
// declarations whose names differ by a suffix an index is likely to strip is exactly the
// pair that mints one identity string for two classes.

using System;

namespace Surface.Attributes.Classes;

/// <summary>
/// 23.5.2: <c>AttributeUsage</c> with a positional <c>ValidOn</c> and both named
/// parameters. This one may be applied to anything, once per target, and is inherited.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
public sealed class AttrMarkAttribute : Attribute
{
    /// <summary>23.2.3: a positional parameter, which is a constructor parameter.</summary>
    public AttrMarkAttribute(string note) => Note = note;

    /// <summary>What the mark says.</summary>
    public string Note { get; }
}

/// <summary>
/// 23.2.2: <c>AllowMultiple</c> true, which is the only reason two applications of one
/// attribute class to one target are legal.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field
        | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Parameter
        | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter
        | AttributeTargets.Assembly | AttributeTargets.Module,
    AllowMultiple = true,
    Inherited = false)]
public sealed class AttrRepeatableAttribute : Attribute
{
    /// <summary>Which of the repeated applications this is.</summary>
    public AttrRepeatableAttribute(int ordinal) => Ordinal = ordinal;

    /// <summary>The application's ordinal, as written at the application site.</summary>
    public int Ordinal { get; }
}

/// <summary>
/// 23.2.1: an *abstract* attribute class, which may be declared and may never be applied.
/// It is the intermediate step that makes the next class an indirect derivation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public abstract class AttrShapeAttribute : Attribute
{
    /// <summary>A constructor an abstract class may still declare.</summary>
    protected AttrShapeAttribute(string kind) => Kind = kind;

    /// <summary>Which kind of shape.</summary>
    public string Kind { get; }
}

/// <summary>
/// 23.2.1: derives from <see cref="Attribute"/> only *indirectly*, through
/// <see cref="AttrShapeAttribute"/>, and is applicable because it is not abstract.
/// </summary>
public sealed class AttrDerivedMarkAttribute : AttrShapeAttribute
{
    /// <summary>Chains to the abstract base's constructor.</summary>
    public AttrDerivedMarkAttribute()
        : base("derived")
    {
    }
}

/// <summary>
/// 23.2.1: an attribute class whose name does not end in <c>Attribute</c>, so it has
/// exactly one spelling at an application site.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class AttrUnsuffixed : Attribute
{
    /// <summary>Nothing to say; the point is the name.</summary>
    public AttrUnsuffixed()
    {
    }
}

/// <summary>
/// 23.2.1: the first half of the ambiguous pair. Applied only as
/// <c>[AttrTwinAttribute]</c>, because <c>[AttrTwin]</c> would name both.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class AttrTwinAttribute : Attribute
{
    /// <summary>Marks the long spelling.</summary>
    public AttrTwinAttribute()
    {
    }
}

/// <summary>
/// 23.2.1: the second half. It is a perfectly good attribute class that no application in
/// this corpus can name, because every spelling that reaches it also reaches its twin.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class AttrTwin : Attribute
{
    /// <summary>Marks the short spelling, which is unusable while the twin exists.</summary>
    public AttrTwin()
    {
    }
}

/// <summary>
/// 23.2.1: an attribute class with no accessible public constructor, which is declarable
/// and, from outside, not applicable.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class AttrInternalOnlyAttribute : Attribute
{
    /// <summary>Internal, so only this assembly may apply it.</summary>
    internal AttrInternalOnlyAttribute(string reason) => Reason = reason;

    /// <summary>Why the method is marked.</summary>
    internal string Reason { get; }
}

/// <summary>
/// 23.2.2 and 23.5.2: the targets and the multiplicity, demonstrated. Both spellings of
/// <see cref="AttrMarkAttribute"/> appear, and <see cref="AttrRepeatableAttribute"/> is
/// applied twice to one member.
/// </summary>
[AttrMark("the short spelling, on the type")]
[AttrDerivedMark]
[AttrTwinAttribute]
public class AttrUsageDemonstration
{
    /// <summary>The long spelling of the very same attribute class.</summary>
    [AttrMarkAttribute("the long spelling, on a field")]
    public int Marked;

    /// <summary>
    /// 23.2.2: two applications of one class to one target, legal only because
    /// <c>AllowMultiple</c> is true. Same target, same class, same argument shape.
    /// </summary>
    [AttrRepeatable(1)]
    [AttrRepeatable(2)]
    [AttrInternalOnly("internal attributes reach internal members")]
    public void Repeated()
    {
    }

    /// <summary>23.2.2: two applications in *one* section, comma-separated.</summary>
    [AttrRepeatable(3), AttrRepeatable(4), AttrUnsuffixed]
    public int Sectioned { get; set; }
}

/// <summary>
/// 23.2.2: <c>Inherited</c> is what decides whether a derived type answers for its base's
/// attributes, and nothing in the source of this class says so.
/// </summary>
public sealed class AttrInheritsMarks : AttrUsageDemonstration
{
    /// <summary>Reads the base field, so the base type is referenced from here too.</summary>
    public int Read() => Marked + Sectioned;
}
