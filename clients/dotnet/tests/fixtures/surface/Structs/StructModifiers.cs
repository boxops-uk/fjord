// Clause 16.2.2 — Struct modifiers. The struct_modifier production is `new`, `public`,
// `protected`, `internal`, `private`, `readonly`, or (from the unsafe grammar) `unsafe`;
// `ref` and `partial` are separate parts of the declaration and have files of their own.
// `abstract`, `sealed` and `virtual` are not struct modifiers, because a struct is
// implicitly sealed — 16.4.3 — and there is nothing for them to say.
//
// A modifier changes no name, so every declaration in this file is a case where the index
// either records the modifier or loses it; nothing else about these types differs.

namespace Surface.Structs;

/// <summary>Clause 16.2.2 — `public`, the widest accessibility, at namespace level.</summary>
public struct StPublicWidget
{
    /// <summary>The only field.</summary>
    public int Serial;
}

/// <summary>Clause 16.2.2 — `internal`, which is also the default at namespace level.</summary>
internal struct StInternalWidget
{
    /// <summary>The only field.</summary>
    public int Serial;
}

/// <summary>
/// Clause 16.2.2 — a struct declared with no accessibility modifier at all. It is
/// internal, and the absence is what the index has to represent.
/// </summary>
struct StDefaultAccessWidget
{
    /// <summary>The only field.</summary>
    public int Serial;
}

/// <summary>
/// Clause 16.2.2 — `readonly`. Every instance member of a readonly struct is implicitly
/// a readonly member (16.3.2), and every instance field must be readonly.
/// </summary>
public readonly struct StReadOnlyWidget
{
    /// <summary>An instance field, which `readonly` on the type requires be readonly.</summary>
    public readonly int Serial;

    /// <summary>Clause 16.4.9 — a declared constructor, which is how the field is set.</summary>
    public StReadOnlyWidget(int serial) => Serial = serial;
}

/// <summary>
/// Clause 16.2.2 — `unsafe`. The modifier is legal on a struct declaration and changes
/// only the context its body is compiled in; pointer types are clause 23 and live in
/// another project, so this struct holds one and does nothing with it.
/// </summary>
public unsafe struct StUnsafeBlob
{
    /// <summary>A pointer field, legal only because of the modifier on the type.</summary>
    public int* Cursor;

    /// <summary>An ordinary field beside it.</summary>
    public int Length;
}

/// <summary>
/// Clause 16.2.2 — the nested accessibilities. `protected`, `protected internal` and
/// `private protected` are legal only on a member of a class, so the container is a class
/// and every struct in it is a nested type.
/// </summary>
public class StModifierHost
{
    /// <summary>Clause 16.2.2 — `private`, reachable only from this class.</summary>
    private struct StPrivateSlot
    {
        public int Depth;
    }

    /// <summary>Clause 16.2.2 — `protected`, visible to a derived class.</summary>
    protected struct StProtectedSlot
    {
        /// <summary>The depth of the slot.</summary>
        public int Depth;
    }

    /// <summary>Clause 16.2.2 — `protected internal`, the union of the two.</summary>
    protected internal struct StProtectedInternalSlot
    {
        /// <summary>The depth of the slot.</summary>
        public int Depth;
    }

    /// <summary>Clause 16.2.2 — `private protected`, the intersection of the two.</summary>
    private protected struct StPrivateProtectedSlot
    {
        /// <summary>The depth of the slot.</summary>
        public int Depth;
    }

    /// <summary>Clause 16.2.2 — a nested struct with no modifier, which is private.</summary>
    struct StDefaultNestedSlot
    {
        public int Depth;
    }

    /// <summary>The only reader of the private nested types, so nothing is unused.</summary>
    public int PrivateDepths()
    {
        StPrivateSlot one = default;
        StDefaultNestedSlot two = default;
        return one.Depth + two.Depth;
    }
}

/// <summary>
/// Clause 16.2.2 — the base of the `new` modifier hazard. A nested struct here is
/// inherited by every derived class.
/// </summary>
public class StHidingBase
{
    /// <summary>The nested struct that the derived class hides.</summary>
    public struct StNestedMarker
    {
        /// <summary>An integer tag.</summary>
        public int Tag;
    }
}

/// <summary>
/// Clause 16.2.2 hazard — `new`, the one struct modifier that exists to say a name is
/// already taken. <c>StHidingDerived.StNestedMarker</c> and
/// <c>StHidingBase.StNestedMarker</c> are two struct declarations with one simple name,
/// one arity and different containers, and the field inside them has one name and two
/// types. Same-arity homonyms are a merge, not a refusal: the containers differ, so the
/// qualified names differ, and the question is whether the index qualifies.
/// </summary>
public class StHidingDerived : StHidingBase
{
    /// <summary>Clause 16.2.2 — the hiding declaration.</summary>
    public new struct StNestedMarker
    {
        /// <summary>A string tag, where the hidden one holds an integer.</summary>
        public string Tag;
    }

    /// <summary>Reaches the hiding declaration, whose `Tag` is the string one.</summary>
    public string DerivedTag()
    {
        StNestedMarker marker = default;
        return marker.Tag ?? string.Empty;
    }

    /// <summary>Reaches the hidden declaration, whose `Tag` is the integer one.</summary>
    public int BaseTag()
    {
        StHidingBase.StNestedMarker marker = default;
        return marker.Tag;
    }
}
