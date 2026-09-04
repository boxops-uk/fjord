// Clause 7.5.2 (declared accessibility) and 7.5.4 (protected access).
//
// 7.5.2's six levels are a fact an index holds about every symbol, and the two-word ones
// are the pair worth checking: `protected internal` is a *union* and `private protected`
// an *intersection*, so a stored accessibility that keeps only the first word gets one of
// them exactly backwards.
//
// 7.5.4 is about the reference and not the declaration: a protected member is reachable
// through `this`, through `base`, and through an expression whose type is the accessing
// class or one derived from it — and *not* through one whose type is only the base. All
// four spellings are below, and the fourth is the one that does not compile, recorded in
// the README rather than written here.

namespace Surface.Lexical.Access;

/// <summary>7.5.2: one member at each of the six declared accessibilities.</summary>
public class LexAccessibilityLevels
{
    /// <summary>Reachable from anywhere the type is.</summary>
    public int Everywhere = 1;

    /// <summary>Reachable only from this type and its nested types.</summary>
    private int _thisTypeOnly = 2;

    /// <summary>Reachable from this assembly.</summary>
    internal int ThisAssembly = 3;

    /// <summary>Reachable from this type and from types derived from it.</summary>
    protected int ThisAndDerived = 4;

    /// <summary>The union: this assembly *or* any derived type, in any assembly.</summary>
    protected internal int ThisAssemblyOrDerived = 5;

    /// <summary>The intersection: derived types, and only those in this assembly.</summary>
    private protected int DerivedInThisAssembly = 6;

    /// <summary>A nested type at each accessibility a nested type may have.</summary>
    private sealed class Hidden
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 7;
    }

    /// <summary>Reads the private members, so nothing here is unreferenced.</summary>
    public int Total() => Everywhere + _thisTypeOnly + ThisAssembly + ThisAndDerived
        + ThisAssemblyOrDerived + DerivedInThisAssembly + Hidden.Tag;

    /// <summary>A protected method, so 7.5.4's rules apply to a call and not only a read.</summary>
    protected int ProtectedRead() => ThisAndDerived;
}

/// <summary>
/// 7.5.4: the three spellings of a protected access that are legal from a derived class.
/// </summary>
public sealed class LexProtectedAccess : LexAccessibilityLevels
{
    /// <summary>Through <c>this</c>, which is the derived type.</summary>
    public int ThroughThis() => this.ThisAndDerived;

    /// <summary>Through the bare name, which is the same access with the <c>this</c> left out.</summary>
    public int ThroughTheSimpleName() => ThisAndDerived;

    /// <summary>Through <c>base</c>, which reaches the same declaration.</summary>
    public int ThroughBase() => base.ThisAndDerived + base.ProtectedRead();

    /// <summary>
    /// Through another instance whose type is this derived class, which 7.5.4 allows
    /// because the qualifying type is the accessing class.
    /// </summary>
    public static int ThroughADerivedInstance(LexProtectedAccess other) => other.ThisAndDerived;

    /// <summary>
    /// Through an instance whose type is only the *base*, which 7.5.4 forbids — reading
    /// <c>other.ThisAndDerived</c> here would be CS1540. This method reads the public
    /// member instead, so the forbidden shape stays a note and not a build failure.
    /// </summary>
    public static int ThroughABaseInstance(LexAccessibilityLevels other) => other.Everywhere;

    /// <summary>Through the intersection accessibility, which this assembly satisfies.</summary>
    public int ThroughThePrivateProtected() => DerivedInThisAssembly;

    /// <summary>Through the union accessibility.</summary>
    public int ThroughTheProtectedInternal() => ThisAssemblyOrDerived;
}

/// <summary>
/// 7.5.2: a type that is not derived from <c>LexAccessibilityLevels</c>, so the only
/// members it may reach are the public and internal ones. It exists to make the accessible
/// set a fact about a *pair* of types and not about the declaration alone.
/// </summary>
public static class LexUnrelatedAccess
{
    /// <summary>Reaches the public member.</summary>
    public static int Public(LexAccessibilityLevels other) => other.Everywhere;

    /// <summary>Reaches the internal member, which this assembly may.</summary>
    public static int Internal(LexAccessibilityLevels other) => other.ThisAssembly;

    /// <summary>Reaches the union member, which this assembly may through its
    /// <c>internal</c> half rather than its <c>protected</c> one.</summary>
    public static int Union(LexAccessibilityLevels other) => other.ThisAssemblyOrDerived;
}
