// Clause 15.3.9 — nested types. 15.3.9.1 is what one is, 15.3.9.2 the fully qualified name,
// 15.3.9.3 the declared accessibility (MemberAccess.cs writes all six), 15.3.9.4 hiding,
// 15.3.9.5 `this` access, 15.3.9.6 access to the containing type's private and protected
// members, and 15.3.9.7 nested types in generic classes.
//
// The collision this file deliberately does *not* write: a namespace `Surface.Classes.X`
// containing a type `Y` beside a class `X` containing a nested type `Y`. The two have the
// same fully qualified name and an identity built from that name alone would take two
// different values for one key.

namespace Surface.Classes;

/// <summary>
/// 15.3.9.2 hazard — the container of a nesting four levels deep. The innermost type's fully
/// qualified name is `Surface.Classes.ClsOuterHost.Middle.Inner.Deepest`, in which only the
/// first two segments are namespaces: a query has to be able to say which segments are
/// containers of which kind.
/// </summary>
public class ClsOuterHost
{
    /// <summary>15.3.9.6 — a private static field of the containing type, read by a nested
    /// type below.</summary>
    private static readonly int OuterSecret = 7;

    /// <summary>15.3.9.6 — a protected static field, likewise.</summary>
    protected static readonly int OuterProtected = 8;

    /// <summary>15.5.1 — an instance field, so the container has state a nested type
    /// cannot reach without an instance (15.3.9.5).</summary>
    public int Instances;

    /// <summary>15.2.4.2 — a nested type used as a base class by <see cref="ClsFromNested"/>,
    /// so a nested declaration is on the other end of a base reference.</summary>
    public class Springboard
    {
        public int Slot;
    }

    /// <summary>15.3.9.1 — one level in.</summary>
    public class Middle
    {
        public int MiddleValue;

        /// <summary>15.3.9.2 — two levels in: `…ClsOuterHost.Middle.Inner`.</summary>
        public sealed class Inner
        {
            public int InnerValue;

            /// <summary>15.3.9.2 — three levels in, and the name is still one identifier.</summary>
            public sealed class Deepest
            {
                public int DeepValue;
            }
        }
    }

    /// <summary>
    /// 15.3.9.5 — a nested type has no implicit `this` for its containing type: the
    /// reference to the containing instance has to be declared, and this field is it. An
    /// index sees an ordinary field whose type happens to be the container.
    /// </summary>
    public sealed class Probe
    {
        /// <summary>15.3.9.5 — the explicit stand-in for the `this` a nested type does not get.</summary>
        public ClsOuterHost? Owner;
    }

    /// <summary>
    /// 15.3.9.6 hazard — a nested type reading the containing type's `private` and
    /// `protected` static members. Both reads are legal only because of where this
    /// declaration sits, so an index that answers "who reads OuterSecret" has to attribute
    /// them to a type whose name does not appear in the reference.
    /// </summary>
    public sealed class SecretReader
    {
        /// <summary>15.3.9.6 — reads the container's private static field.</summary>
        public static readonly int CopiedSecret = OuterSecret;

        /// <summary>15.3.9.6 — reads the container's protected static field.</summary>
        public static readonly int CopiedProtected = OuterProtected;
    }
}

/// <summary>15.3.9.4 — the base of the nested-type hiding pair.</summary>
public class ClsNestBase
{
    /// <summary>15.3.9.4 — the nested type the derived class hides.</summary>
    public class Marker
    {
        public int Value;
    }

    /// <summary>15.3.9.4 — a nested type the derived class inherits unchanged.</summary>
    public class Kept
    {
        public int Value;
    }
}

/// <summary>
/// 15.3.9.4 hazard — a nested type hiding an inherited nested type of the same name. Both
/// `Marker` declarations are types, both are members, and the derived one's `Value` has a
/// different type from the base one's: a merge of the two produces a type that exists nowhere.
/// </summary>
public class ClsNestDerived : ClsNestBase
{
    /// <summary>15.3.9.4 — `new` on a nested type declaration.</summary>
    public new class Marker
    {
        public string Value = "derived";
    }
}

/// <summary>
/// 15.3.9.7 hazard — nested types in a generic class. `Inner` declares no type parameter of
/// its own and is still a different type for every argument its container takes, so its
/// identity has to carry the container's arity; `InnerGeneric` adds an arity of its own on
/// top of it.
/// </summary>
/// <typeparam name="TPayload">In scope over every nested declaration below.</typeparam>
public class ClsGenericHost<TPayload>
{
    public TPayload? Payload;

    /// <summary>15.3.9.7 — non-generic, nested in a generic: arity 0 of its own, and
    /// `ClsGenericHost&lt;int&gt;.Inner` is not `ClsGenericHost&lt;string&gt;.Inner`.</summary>
    public class Inner
    {
        /// <summary>15.3.9.7 — a field typed by the *container's* type parameter.</summary>
        public TPayload? FromOuter;
    }

    /// <summary>15.3.9.7 — generic nested in generic, so two argument lists meet.</summary>
    /// <typeparam name="TExtra">The nested declaration's own parameter.</typeparam>
    public class InnerGeneric<TExtra>
    {
        public TPayload? FromOuter;

        public TExtra? Own;
    }

    /// <summary>15.3.9.7 — two levels of nesting under one generic container.</summary>
    public class InnerDeep
    {
        public sealed class Deeper
        {
            public TPayload? Still;
        }
    }
}

/// <summary>
/// 15.3.9.7 — the references. Each field's type names one nested declaration, and no two of
/// these fields have the same type.
/// </summary>
public static class ClsGenericHostUses
{
    /// <summary>15.3.9.7 — the nested type of one closed container.</summary>
    public static ClsGenericHost<int>.Inner? IntInner;

    /// <summary>15.3.9.7 — the same declaration under another argument.</summary>
    public static ClsGenericHost<string>.Inner? StringInner;

    /// <summary>15.3.9.7 — both arity lists supplied at once.</summary>
    public static ClsGenericHost<int>.InnerGeneric<string>? Mixed;

    /// <summary>15.3.9.7 — a two-deep nesting under a closed container.</summary>
    public static ClsGenericHost<int>.InnerDeep.Deeper? Deep;

    /// <summary>15.3.9.2 — the four-segment nested name, as a field type.</summary>
    public static ClsOuterHost.Middle.Inner.Deepest? Deepest;

    /// <summary>15.3.9.4 — the hiding nested type, named through the class that hides.</summary>
    public static ClsNestDerived.Marker? HidingMarker;

    /// <summary>15.3.9.4 — and the hidden one, named through the class that declares it.</summary>
    public static ClsNestBase.Marker? HiddenMarker;

    /// <summary>15.3.9.4 — the inherited nested type, named through the *derived* class,
    /// which is a reference to a declaration in the base.</summary>
    public static ClsNestDerived.Kept? KeptViaDerived;
}
