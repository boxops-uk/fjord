// Clause 15.3.6 — access modifiers on class members. A class member may have any of the six
// declared accessibilities, and `private` is what it gets when nothing is written. Every one
// of the six appears below on a field, on a constant and on a nested type, because the
// declared accessibility of a nested type is 15.3.9.3's row and it is the same list.

namespace Surface.Classes;

/// <summary>
/// 15.3.6 hazard — one container declaring six fields, six constants and six nested types
/// that differ in nothing but the access modifier. A member identity that does not carry the
/// modifier still has to keep these apart by name, and an index that filters on
/// accessibility has to agree with the source on all six.
/// </summary>
public class ClsAccessDeclarations
{
    /// <summary>15.3.6 — `private`, written.</summary>
    private int _privateField;

    /// <summary>15.3.6 — `private`, implied: no modifier on a class member means private.</summary>
    int _impliedPrivateField;

    /// <summary>15.3.6 — `protected`: this class and its derived classes.</summary>
    protected int ProtectedField;

    /// <summary>15.3.6 — `internal`: this assembly.</summary>
    internal int InternalField;

    /// <summary>15.3.6 — `protected internal`, the union of the two.</summary>
    protected internal int ProtectedInternalField;

    /// <summary>15.3.6 — `private protected`, the intersection.</summary>
    private protected int PrivateProtectedField;

    /// <summary>15.3.6 — `public`.</summary>
    public int PublicField;

    /// <summary>15.4 — a private constant.</summary>
    private const int PrivateLimit = 1;

    /// <summary>15.4 — and a public one of the same shape.</summary>
    public const int PublicLimit = 2;

    /// <summary>15.3.9.3 — a private nested type, reachable from nowhere else.</summary>
    private sealed class PrivateNested
    {
        public int Value;
    }

    /// <summary>15.3.9.3 — a protected nested type, named by the derived class below.</summary>
    protected class ProtectedNested
    {
        public int Value;
    }

    /// <summary>15.3.9.3 — internal.</summary>
    internal class InternalNested
    {
        public int Value;
    }

    /// <summary>15.3.9.3 — protected internal.</summary>
    protected internal class ProtectedInternalNested
    {
        public int Value;
    }

    /// <summary>15.3.9.3 — private protected.</summary>
    private protected class PrivateProtectedNested
    {
        public int Value;
    }

    /// <summary>15.3.9.3 — public.</summary>
    public class PublicNested
    {
        public int Value;
    }

    /// <summary>15.3.9.6 — a use of the private nested type and the private constant from
    /// the containing type, which is the only place either is visible.</summary>
    private static readonly PrivateNested Hidden = new PrivateNested { Value = PrivateLimit };

    /// <summary>15.5.6.2 — the projection that makes the private members reachable as a
    /// fact rather than as dead code.</summary>
    public static readonly int HiddenValue = Hidden.Value;
}

/// <summary>
/// 15.3.6 — the accessibility domains, seen from a derived class in the same assembly: the
/// `protected`, `internal`, `protected internal` and `private protected` members are all
/// reachable here and the `private` ones are not.
/// </summary>
public class ClsAccessDerived : ClsAccessDeclarations
{
    /// <summary>15.3.9.3 — a field typed by the base's protected nested type.</summary>
    protected ProtectedNested? Reachable;

    /// <summary>15.3.9.3 — and by the private protected one, legal because this class is
    /// both derived from the container and in its assembly.</summary>
    private protected PrivateProtectedNested? AlsoReachable;

    /// <summary>
    /// 15.3.6 — reads the base's `protected`, `internal` and `private protected` instance
    /// fields, which is legal here and nowhere outside this hierarchy. The read is in a
    /// static field initializer rather than a method body because clause 15.6 is the sibling
    /// project's: every reference in this project is carried by a field initializer.
    /// </summary>
    public static readonly int ReadsProtected =
        new ClsAccessDerived().ProtectedField
        + new ClsAccessDerived().InternalField
        + new ClsAccessDerived().PrivateProtectedField;
}
