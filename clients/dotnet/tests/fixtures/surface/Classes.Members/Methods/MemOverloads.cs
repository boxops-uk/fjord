// Clause 15.6.1 (methods) read through clause 7.6 (signatures and overloading), which is what
// makes this file the densest identity shape in the project. This indexer separates two members
// of one type that share a name by an ordinal counted off a sorted member list, so a name plus
// an ordinal is the whole of a method's identity — and every group below is a set of members
// that agree on the name.
//
// Four shapes, in order of how little separates the members:
//
//   MemArityOverloads       — nothing but the number of type parameters.
//   MemTypeOverloads        — nothing but one parameter's type.
//   MemModifierOverloads    — nothing but `ref`-ness, across an inheritance boundary, which is
//                             where `ref` beside `out` compiles at all.
//   MemSubstitutionOverloads — distinct as declared, one member as instantiated.

namespace Surface.Classes.Members.Methods;

/// <summary>7.6 through 15.6.1: three members named <c>Take</c>, differing only in arity.</summary>
public sealed class MemArityOverloads
{
    /// <summary>No type parameters.</summary>
    public int Take() => 0;

    /// <summary>One type parameter, and still no parameters at all.</summary>
    public int Take<TFirst>() => 1;

    /// <summary>Two type parameters. The type argument list at the call site is the only thing
    /// that selects between the three.</summary>
    public int Take<TFirst, TSecond>() => 2;

    /// <summary>All three, each reached only by its type argument list.</summary>
    public int All() => Take() + Take<int>() + Take<int, string>();
}

/// <summary>7.6 through 15.6.1: members named <c>Read</c> differing in one parameter's type,
/// including pairs where the two types are the same underlying type spelled differently.</summary>
public sealed class MemTypeOverloads
{
    /// <summary>The value type.</summary>
    public int Read(int value) => value;

    /// <summary>Its nullable form, which is a different type.</summary>
    public int Read(int? value) => value ?? 0;

    /// <summary>An enum whose underlying type is <c>int</c> — a different type again.</summary>
    public int Read(MemUnit unit) => (int)unit;

    /// <summary>An unsigned type of the same width.</summary>
    public int Read(uint value) => (int)value;

    /// <summary>A rank-one array.</summary>
    public int Read(int[] values) => values.Length;

    /// <summary>A rank-two array, whose rank is part of the type.</summary>
    public int Read(int[,] values) => values.Length;

    /// <summary>A jagged array, which is the rank-one array of a rank-one array.</summary>
    public int Read(int[][] values) => values.Length;

    /// <summary>One instantiation of a generic type.</summary>
    public int Read(System.Collections.Generic.List<int> values) => values.Count;

    /// <summary>The other instantiation. Two closed types, one open one.</summary>
    public int Read(System.Collections.Generic.List<string> values) => values.Count;

    /// <summary>A pointer-free by-reference form of the first overload, legal because the other
    /// half of the pair is by value.</summary>
    public int Read(ref int value) => value += 1;

    /// <summary>Every overload above, called.</summary>
    public int All()
    {
        var slot = 1;
        return Read(slot) + Read((int?)slot) + Read(MemUnit.Pixel) + Read(1u) + Read(new int[1]) +
               Read(new int[1, 1]) + Read(new int[1][]) + Read(new System.Collections.Generic.List<int>()) +
               Read(new System.Collections.Generic.List<string>()) + Read(ref slot);
    }
}

/// <summary>15.6.2.3.1: the unit a <c>Read</c> overload distinguishes by enum type.</summary>
public enum MemUnit
{
    /// <summary>The default.</summary>
    Pixel = 0,

    /// <summary>Not the default.</summary>
    Point = 1,
}

/// <summary>15.6.2.3.3 beside 15.6.2.3.4: `ref` and `out` overloads of one name. CS0663 forbids
/// this within one type, so the pair is split across a base and a derived class — where it is
/// legal, and where the two together still form one overload set at a call site on the derived
/// type. Nothing but the modifier separates the two signatures.</summary>
public class MemModifierOverloadBase
{
    /// <summary>15.6.2.3.3: the `ref` half, declared on the base.</summary>
    public virtual int Blend(ref int slot) => slot += 1;

    /// <summary>15.6.2.3.2: an `in` half, which may sit beside the `ref` one only because the
    /// parameter type differs as well.</summary>
    public int Blend(in long slot) => (int)slot;
}

/// <summary>7.6: the `out` half — and the finding this file was written to record. For the
/// purpose of *hiding*, `ref` and `out` are the same parameter kind (which is why CS0663
/// exists), so `Blend(out int)` here hides `Blend(ref int)` on the base. It hides it silently:
/// no CS0108, no CS0114, even though the base member is virtual. So the pair that differs only
/// in `ref` versus `out` is not expressible as an overload *set* anywhere in C# — inside one
/// type it is CS0663, and across an inheritance boundary the derived declaration removes the
/// base one from the overload set. `Blend(ref slot)` on a `MemModifierOverloadDerived` receiver
/// is CS1620; reaching the base member needs `base.` or a base-typed receiver, and both
/// spellings are below.</summary>
public sealed class MemModifierOverloadDerived : MemModifierOverloadBase
{
    /// <summary>15.6.2.3.4: the `out` half.</summary>
    public int Blend(out int slot)
    {
        slot = 1;
        return slot;
    }

    /// <summary>7.6: all three. The `out` one binds to this type's declaration; the `ref` one is
    /// reachable only through `base.`, because the declaration above hid it; the `in` one is
    /// still inherited, and it needs a `long` variable, because an argument passed by reference
    /// admits no implicit conversion — `Blend(in slot)` with an `int` binds to the `out`
    /// overload instead and is CS1620.</summary>
    public int All()
    {
        var slot = 1;
        var wide = 1L;
        return Blend(out slot) + base.Blend(ref slot) + Blend(in wide);
    }

    /// <summary>7.6: the same hidden member through a base-typed receiver rather than through
    /// `base.`, so the reference exists at both spellings.</summary>
    public static int ThroughBase()
    {
        var slot = 1;
        MemModifierOverloadBase upcast = new MemModifierOverloadDerived();
        return upcast.Blend(ref slot);
    }
}

/// <summary>7.6: the pair that is two members as declared and one member as instantiated.
/// `Take(TItem)` and `Take(int)` have different signatures in the open type, so the declaration
/// is legal; at `MemSubstitutionOverloads&lt;int&gt;` they have the same one, and every call with
/// an `int` argument binds to the non-generic overload by the tie-break rule rather than by
/// there being only one candidate.</summary>
public sealed class MemSubstitutionOverloads<TItem>
{
    /// <summary>The overload whose parameter is the type parameter.</summary>
    public int Take(TItem value) => 1;

    /// <summary>The overload whose parameter is <c>int</c>. Identical to the above once
    /// <c>TItem</c> is <c>int</c>.</summary>
    public int Take(int value) => 2;

    /// <summary>Both, from inside the open type, where they are still two.</summary>
    public int Both(TItem item, int number) => Take(item) + Take(number);
}

/// <summary>7.6: the instantiation at which the pair collapses, and the call that shows which
/// half wins.</summary>
public static class MemSubstitutionUse
{
    /// <summary>Reads the closed type. `Take(1)` here is `Take(int)`, not `Take(TItem)`.</summary>
    public static int Collapsed()
    {
        var subject = new MemSubstitutionOverloads<int>();
        return subject.Take(1) + subject.Both(2, 3);
    }

    /// <summary>The same open type at an instantiation where the two stay distinct.</summary>
    public static int Distinct()
    {
        var subject = new MemSubstitutionOverloads<string>();
        return subject.Take("x") + subject.Take(1);
    }
}
