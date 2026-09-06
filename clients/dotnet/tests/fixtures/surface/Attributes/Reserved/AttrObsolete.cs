// Clause 23.5.4 (the Obsolete attribute).
//
// `Obsolete` has four constructor forms in the clause's sense — no arguments, a message, a
// message with `false`, and a message with `true` — and all four are applied below, so the
// four applications are four references to three different constructors (the two-argument
// form serves twice). That is 23.4.2's overload story again, on a framework class this
// time, where an index cannot see the constructor declarations in any source file it walks.
//
// The `error: true` form is the interesting one, because it changes what a *reference* to
// the marked member costs. A reference to `AttrRetired.Removed` is CS0619, an error, and no
// `#pragma warning disable` can suppress it — so the only call to it in this project sits
// inside an `#if SURFACE_AUDIT` region that is never compiled. A source index that walks
// disabled regions finds a call; the compiler's own model has no such call, and neither has
// the assembly. The two answers are both defensible and they are not the same number.
//
// The rest of the references are warnings, suppressed by pragma at the site. A suppression
// is not a reference and produces no symbol, but `SURFACE001` below is a diagnostic id
// invented by an attribute argument and then named by a pragma, which is a string in one
// place matching a string in another with no declaration between them.

using System;

namespace Surface.Attributes.Reserved;

/// <summary>23.5.4: the bare form, with no message.</summary>
[Obsolete]
public sealed class AttrBareObsolete
{
    /// <summary>Something to reference.</summary>
    public int Counted { get; set; }
}

/// <summary>23.5.4: every target Obsolete is worth applying to, at three severities.</summary>
[Obsolete("the whole type is retired; prefer AttrCurrent")]
public class AttrRetired
{
    /// <summary>23.5.4: the two-argument form with <c>false</c>, which warns.</summary>
    [Obsolete("this field warns", false)]
    public int Warned;

    /// <summary>
    /// 23.5.4: the two-argument form with <c>true</c>, which makes every reference an
    /// error. Referenced only from a region that is not compiled.
    /// </summary>
    [Obsolete("this member is gone; there is no replacement", true)]
    public int Removed;

    /// <summary>23.5.4: a message-only application on a method.</summary>
    [Obsolete("call Describe instead")]
    public string Render() => $"retired:{Warned}";

    /// <summary>
    /// 23.5.4: the named parameters .NET added — a diagnostic id, so the warning can be
    /// suppressed by a name of the library's own choosing, and a url format.
    /// </summary>
    [Obsolete(
        "identified by SURFACE001",
        false,
        DiagnosticId = "SURFACE001",
        UrlFormat = "https://example.invalid/surface/{0}")]
    public string Identified() => "identified";

    /// <summary>23.5.4: on a property, an event and a nested type.</summary>
    [Obsolete("a retired property")]
    public int Legacy { get; set; }

    /// <summary>An event nobody should subscribe to any more.</summary>
    [Obsolete("a retired event")]
    public event EventHandler? Retired;

    /// <summary>A nested type that went with it.</summary>
    [Obsolete("a retired nested type")]
    public sealed class Inner
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 1;
    }

    /// <summary>Raises the retired event, from inside the retired type.</summary>
    public void RaiseRetired() => Retired?.Invoke(this, EventArgs.Empty);

    /// <summary>The replacement for <c>Render</c>, which is not obsolete.</summary>
    public string Describe() => $"retired:{Warned}";
}

/// <summary>23.5.4: on an enum, its members, an interface and a delegate.</summary>
[Obsolete("a retired enum")]
public enum AttrRetiredMode
{
    /// <summary>Still meaningful.</summary>
    Kept = 0,

    /// <summary>23.5.4: an obsolete enum member, which the enum's own use references.</summary>
    [Obsolete("a retired enum member")]
    Dropped = 1,
}

/// <summary>23.5.4: an obsolete interface, whose implementation inherits no warning.</summary>
[Obsolete("a retired interface")]
public interface IAttrRetired
{
    /// <summary>What it used to do.</summary>
    int Weigh();
}

/// <summary>23.5.4: an obsolete delegate type.</summary>
[Obsolete("a retired delegate")]
public delegate void AttrRetiredHandler();

/// <summary>
/// 23.5.4: the references. Each one is a use of an obsolete declaration, suppressed at the
/// site so that the build stays clean enough to read.
/// </summary>
public sealed class AttrObsoleteReferences
{
#pragma warning disable CS0612 // 23.5.4: the bare form's diagnostic, for a type reference.
#pragma warning disable CS0618 // 23.5.4: the message form's diagnostic.
    /// <summary>A field whose type is obsolete.</summary>
    private readonly AttrBareObsolete _bare = new();

    /// <summary>A field whose type is obsolete for a different reason.</summary>
    private readonly AttrRetired _retired = new();

    /// <summary>Reads every obsolete member that a warning, not an error, protects.</summary>
    public string ReadsThemAll()
    {
        _retired.Warned = 1;
        _retired.Legacy = 2;
        _retired.RaiseRetired();

        AttrRetiredMode mode = AttrRetiredMode.Dropped;
        AttrRetiredHandler handler = () => { };
        handler();

        return string.Join(
            ',',
            _bare.Counted,
            _retired.Render(),
            AttrRetired.Inner.Tag,
            mode,
            typeof(IAttrRetired).Name);
    }
#pragma warning restore CS0618
#pragma warning restore CS0612

    /// <summary>
    /// 23.5.4: the diagnostic id form, suppressed by the id the attribute argument named
    /// rather than by a compiler error number.
    /// </summary>
#pragma warning disable SURFACE001
    public string ReadsTheIdentified() => new AttrRetired().Identified();
#pragma warning restore SURFACE001

#if SURFACE_AUDIT
    /// <summary>
    /// 23.5.4: the only reference to an <c>error: true</c> member in this project, in a
    /// region no compilation includes. Defining SURFACE_AUDIT makes this project fail to
    /// build, on purpose: that is what `error: true` means.
    /// </summary>
    public int ReadsTheRemoved() => new AttrRetired().Removed;
#endif
}
