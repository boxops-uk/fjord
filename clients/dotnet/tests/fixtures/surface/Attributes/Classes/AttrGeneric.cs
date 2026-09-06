// Clause 23.2.1 (attribute classes), in the form the standard's text forbids and the
// language now allows: a *generic* attribute class, C# 11.
//
// ECMA-334 draft-v9 says an attribute class may not be generic. C# 11 lifted that, and the
// lifted form is the one worth indexing, because an application carries a type argument:
// `[AttrTyped<int>("...")]` is a reference to a constructor of a *constructed* type, and
// the constructed type is not a declaration anywhere. An index that names the target
// `AttrTypedAttribute<T>` loses which instantiation was applied; one that names it
// `AttrTypedAttribute<int>` names something no file declares.
//
// The class name is at one arity and one arity only. There is no non-generic
// `AttrTypedAttribute` in this corpus, deliberately: that pair is a refused write, and it
// belongs to the quarantine projects rather than here.

using System;

namespace Surface.Attributes.Classes;

/// <summary>
/// C# 11: a generic attribute class. The type parameter carries a constraint, so the
/// constraint is a fact about a type parameter of an attribute class.
/// </summary>
/// <typeparam name="T">What the mark is about.</typeparam>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class AttrTypedAttribute<T> : Attribute
    where T : notnull
{
    /// <summary>23.2.3: a positional parameter on a generic attribute class.</summary>
    public AttrTypedAttribute(string note) => Note = note;

    /// <summary>What the mark says.</summary>
    public string Note { get; }

    /// <summary>
    /// 23.2.4: a named parameter whose declared type is the type parameter. Its argument
    /// still has to be one of the permitted types, which is a per-instantiation rule.
    /// </summary>
    public T? Sample { get; set; }
}

/// <summary>
/// C# 11: three instantiations of one generic attribute class, applied to one type.
/// </summary>
[AttrTyped<int>("an int instantiation", Sample = 7)]
[AttrTyped<string>("a string instantiation", Sample = "seven")]
[AttrTyped<AttrSeverity>("an enum instantiation", Sample = AttrSeverity.Warn)]
public sealed class AttrGenericApplications
{
    /// <summary>
    /// C# 11: the same instantiation applied twice to one member, so the pair that
    /// collides is two references to one constructed type's constructor.
    /// </summary>
    [AttrTyped<int>("first")]
    [AttrTyped<int>("second")]
    public int Twice { get; set; }

    /// <summary>
    /// C# 11: a type argument that is itself a constructed type, which is a type reference
    /// nested two deep inside an attribute application.
    /// </summary>
    [AttrTyped<System.Collections.Generic.List<string>>("a constructed type argument")]
    public void Nested()
    {
    }

    /// <summary>
    /// The open form is the declaration; this method's signature is the only place in this
    /// project where <c>AttrTypedAttribute&lt;T&gt;</c> appears as an ordinary type.
    /// </summary>
    public static AttrTypedAttribute<int> AsAnOrdinaryType() => new("constructed by hand");
}
