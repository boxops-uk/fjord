// Clause 20.3 — enum modifiers. The permitted modifiers are `new`, `public`, `protected`,
// `internal`, `private` and the two combinations of the last three; an enum declared with no
// modifier takes the default accessibility of its container. `new` is permitted only on a
// nested enum, and it exists to hide an inherited member of the same name.

namespace Surface.EnumsDelegates;

/// <summary>
/// 20.3 hazard — no modifier at all, so this enum is `internal` by a rule and not by a
/// token. The accessibility an index reports for it is written nowhere in the source.
/// </summary>
enum EdModImplicit
{
    Only,
}

/// <summary>20.3 — `internal`, written out where it would be implied.</summary>
internal enum EdModInternal
{
    Only,
}

/// <summary>20.3 — `public`, the only modifier that widens a top-level enum.</summary>
public enum EdModPublic
{
    Only,
}

/// <summary>20.3 — every accessibility a nested enum can be declared with.</summary>
public class EdModifierBase
{
    /// <summary>20.3 — `public` on a nested enum.</summary>
    public enum EdModNestedPublic
    {
        Only,
    }

    /// <summary>20.3 — `internal` on a nested enum.</summary>
    internal enum EdModNestedInternal
    {
        Only,
    }

    /// <summary>20.3 — `protected`, which needs a class to be nested in.</summary>
    protected enum EdModNestedProtected
    {
        Only,
    }

    /// <summary>20.3 — `private`, the default for a nested enum, written out.</summary>
    private enum EdModNestedPrivate
    {
        Only,
    }

    /// <summary>20.3 — `protected internal`, the union of the two.</summary>
    protected internal enum EdModNestedProtectedInternal
    {
        Only,
    }

    /// <summary>20.3 — `private protected`, the intersection of the two.</summary>
    private protected enum EdModNestedPrivateProtected
    {
        Only,
    }

    /// <summary>20.3 — the enum the derived class hides with `new`.</summary>
    public enum EdModHidden
    {
        FromBase,
    }

    /// <summary>20.3 — a use of the private nested enum, so it is not merely declared.</summary>
    public string Hidden() => EdModNestedPrivate.Only.ToString();
}

/// <summary>
/// 20.3 hazard — `new` on a nested enum, hiding <see cref="EdModifierBase.EdModHidden"/>.
/// Two enum declarations carry one simple name, in two types one of which inherits from the
/// other, and each declares a differently named member. The two must separate, and the
/// hiding relation between them is a fact only the modifier states.
/// </summary>
public class EdModifierDerived : EdModifierBase
{
    /// <summary>20.3 — the hiding declaration.</summary>
    public new enum EdModHidden
    {
        FromDerived,
    }

    /// <summary>20.3 — the hidden enum, still reachable through the base type's name.</summary>
    public static EdModifierBase.EdModHidden Base() => EdModifierBase.EdModHidden.FromBase;

    /// <summary>20.3 — the hiding enum, reached by the simple name inside the derived class.</summary>
    public static EdModHidden Derived() => EdModHidden.FromDerived;
}
