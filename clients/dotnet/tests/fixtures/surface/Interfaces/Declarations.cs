// Clause 19.2 — interface declarations — with 19.2.1's general form (attributes, modifiers,
// `interface`, identifier, an optional type parameter list, an optional base list, an optional
// constraint list, a body), 19.2.2's modifiers, and 19.3's body.
//
// One modifier of 19.2.2 is deliberately absent: `partial`. A partial type declaration is a
// known killer of an indexing run when both parts sit in one file, and splitting one across two
// files to dodge that is a bet on how the identity of a type declaration is keyed. The rest of
// the modifier set is here, and the prediction is recorded in README.md instead.

using System;

namespace Surface.Interfaces;

/// <summary>
/// 19.2.1 — an interface declaration may carry attributes, and this one does. The declaration
/// itself is the minimal form otherwise: one modifier, the keyword, an identifier, a body.
/// </summary>
[Obsolete("19.2.1 — an attribute on an interface declaration, so the attribute list is not empty.")]
public interface IfaceAttributed
{
    /// <summary>19.2.1 — a member, so the body is not the empty one.</summary>
    void Ping();
}

/// <summary>19.3 — an interface body may be empty, and this is the whole of one.</summary>
public interface IfaceEmpty
{
}

/// <summary>
/// 19.2.1 hazard — a declaration with every optional part filled in at once: a type parameter
/// list, a base list, a constraint clause and a body. Its identity has to carry the arity, and
/// no other declaration in this corpus is named <c>IfaceConstrained</c> at any arity.
/// </summary>
/// <typeparam name="TItem">19.2.1 — the one type parameter, constrained below.</typeparam>
public interface IfaceConstrained<TItem> : IfaceEmpty
    where TItem : class, IfaceContract, new()
{
    /// <summary>19.2.1 — a member whose signature mentions the type parameter.</summary>
    TItem Make();
}

/// <summary>
/// 19.2.2 — `internal`, which is also the default for a top-level interface. Stated explicitly
/// here so a query can see a modifier that changes nothing about the accessibility.
/// </summary>
internal interface IfaceInternal
{
    /// <summary>19.2.2 — a member of an internal interface is internal in effect, public in form.</summary>
    int Rank { get; }
}

/// <summary>
/// 19.2.2 — the declared accessibilities a nested interface may have. All six appear once,
/// which is the whole modifier set apart from `new` (below) and `partial` (absent, see the
/// file header).
/// </summary>
public class IfaceModifierHost
{
    /// <summary>19.2.2 — `public`.</summary>
    public interface IfacePublicNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — `internal`.</summary>
    internal interface IfaceInternalNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — `protected`, which only a nested declaration may be.</summary>
    protected interface IfaceProtectedNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — `protected internal`, two keywords for one accessibility.</summary>
    protected internal interface IfaceProtectedInternalNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — `private protected`.</summary>
    private protected interface IfacePrivateProtectedNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — `private`, reachable only from inside this class.</summary>
    private interface IfacePrivateNested
    {
        void Reach();
    }

    /// <summary>19.2.2 — an implementation of the private one, so its accessibility is used
    /// rather than merely declared.</summary>
    private sealed class IfacePrivateUse : IfacePrivateNested
    {
        public void Reach()
        {
        }
    }

    /// <summary>19.2.2 — the private interface reached through the private implementation.</summary>
    public static void UsePrivate() => new IfacePrivateUse().Reach();
}

/// <summary>19.2.2 — the base of the pair that exercises the `new` modifier.</summary>
public class IfaceHidingBase
{
    /// <summary>19.2.2 — a nested interface that a derived class hides.</summary>
    public interface IfaceHidden
    {
        /// <summary>19.2.2 — the hidden declaration's member, typed <see cref="int"/>.</summary>
        int Tag { get; }
    }
}

/// <summary>
/// 19.2.2 hazard — `new` on an interface declaration, hiding the inherited nested interface of
/// the same simple name. Two interface declarations spelled <c>IfaceHidden</c> exist, differing
/// only in their containing type, and they declare members of different types so a wrong merge
/// is observable rather than harmless.
/// </summary>
public class IfaceHidingDerived : IfaceHidingBase
{
    /// <summary>19.2.2 — the hiding declaration, whose member is a <see cref="string"/>.</summary>
    public new interface IfaceHidden
    {
        string Tag { get; }
    }

    /// <summary>19.2.2 — the hiding declaration named from inside the hiding class, which is
    /// where the `new` modifier changes what the simple name means.</summary>
    public static Type Hiding() => typeof(IfaceHidden);

    /// <summary>19.2.2 — the hidden declaration, reachable only through its container.</summary>
    public static Type Hidden() => typeof(IfaceHidingBase.IfaceHidden);
}
