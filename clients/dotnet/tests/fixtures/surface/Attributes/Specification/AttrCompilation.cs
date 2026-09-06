// Clause 23.4.2 (compilation of an attribute).
//
// The clause is the one that says what an application *is*: the attribute's positional
// arguments are the arguments of an instance constructor invocation, chosen by ordinary
// overload resolution, and its named arguments are assignments to fields and properties
// performed after the constructor returns. So an application is a reference to a
// constructor, and the class is reached only through it.
//
// That is the target this file exists to pin down. `AttrAuditAttribute` declares four
// constructors — no parameters, one int, one string, and a `params string[]` — and the
// applications below select all four. Every one of them is `.ctor` in metadata; they
// differ only by parameter list. An index that names the target of an application by the
// attribute *class* answers the same symbol for all four sites and cannot say which
// constructor ran; one that names it by member name alone mints one identity string for
// four declarations.
//
// Two argument spellings are worth separating, because they look alike and resolve to
// different declarations:
//
//   `[AttrAudit(note: "x")]`  — a *named constructor argument*: a reference to the
//                               constructor parameter `note`.
//   `[AttrAudit("x", Note = "y")]` — a *named parameter* in 23.2.3's sense: a reference to
//                               the property `Note`.
//
// And `[AttrAudit]` with no argument list at all is the clause's equivalence: it selects
// the parameterless constructor with no argument-list syntax to hang the reference on.
//
// What the built assembly does with those two spellings is the part worth knowing, and it
// was found by reading metadata rather than the clause: **a named constructor argument is
// stored positionally.** `[AttrAudit(note: "x")]` records one positional argument and no
// named arguments, exactly as `[AttrAudit("x")]` does — the parameter name `note` survives
// only in the source. `[AttrAudit("x", Note = "y")]` records one positional argument *and*
// a named argument, because that one really is a property assignment. So the two spellings
// that look alike are indistinguishable in the assembly and distinguishable in the syntax
// tree, and it is the syntactic index that has the reference to the parameter.

using System;

namespace Surface.Attributes.Specification;

/// <summary>
/// 23.4.2: four constructors, so an application has an overload to resolve rather than a
/// single candidate.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class AttrAuditAttribute : Attribute
{
    /// <summary>The parameterless form, selected by <c>[AttrAudit]</c>.</summary>
    public AttrAuditAttribute() => Note = "unnoted";

    /// <summary>The int form.</summary>
    /// <param name="ordinal">Which audit this is.</param>
    public AttrAuditAttribute(int ordinal)
    {
        Note = "ordinal";
        Ordinal = ordinal;
    }

    /// <summary>The string form, whose parameter shares a name with a property.</summary>
    /// <param name="note">What the audit says.</param>
    public AttrAuditAttribute(string note) => Note = note;

    /// <summary>
    /// 23.4.2: a <c>params</c> form. The application writes loose arguments and the
    /// compiler synthesises the array, so the array creation is in metadata and not in the
    /// source.
    /// </summary>
    /// <param name="first">The first note, which keeps this overload from swallowing the
    /// single-string form.</param>
    /// <param name="rest">The remaining notes.</param>
    public AttrAuditAttribute(string first, params string[] rest)
    {
        Note = first;
        Rest = rest;
    }

    /// <summary>A named parameter whose name differs from a constructor parameter's only
    /// in case.</summary>
    public string Note { get; set; }

    /// <summary>A named parameter, also reachable as a constructor parameter.</summary>
    public int Ordinal { get; set; }

    /// <summary>What the <c>params</c> overload captured.</summary>
    public string[]? Rest { get; }
}

/// <summary>
/// 23.4.2: one application per constructor, plus the two named-argument spellings that
/// resolve to different declarations.
/// </summary>
[AttrAudit]
[AttrAudit(7)]
[AttrAudit("the string overload")]
[AttrAudit("the params overload", "second", "third")]
public sealed class AttrCompiledApplications
{
    /// <summary>23.4.2: a named *constructor argument* — a reference to a parameter.</summary>
    [AttrAudit(note: "bound by parameter name")]
    public int ByParameterName;

    /// <summary>23.4.2: a named *parameter* — a reference to a property.</summary>
    [AttrAudit("positional", Note = "reassigned after construction")]
    public int ByPropertyName;

    /// <summary>
    /// 23.4.2: both at once. `ordinal:` is the parameter and `Ordinal =` is the property,
    /// and they are the same word at the same site.
    /// </summary>
    [AttrAudit(ordinal: 1, Ordinal = 2)]
    public int BothSpellings;

    /// <summary>
    /// 23.4.2: an argument that is a constant expression rather than a literal. The value
    /// in metadata is 12; the reference in the source is to two constants.
    /// </summary>
    [AttrAudit(TwiceSix)]
    public int Folded;

    /// <summary>Half of the folded argument above.</summary>
    public const int Six = 6;

    /// <summary>The folded argument, so a query can look for the constant it read.</summary>
    public const int TwiceSix = Six * 2;

    /// <summary>Reads the fields, so none of them is written and never used.</summary>
    public int Total() => ByParameterName + ByPropertyName + BothSpellings + Folded;
}
