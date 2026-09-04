// Clause 16.4.2 — Value semantics — with 16.4.4 Assignment, 16.4.5 Default values and
// 16.4.6 Boxing and unboxing. A struct is a value type: a variable of struct type holds
// the data directly, assignment copies it, an argument passed by value is a copy, and two
// variables of struct type never refer to one object.
//
// None of that is a declaration, so every row this file answers is answered by a
// reference. The hazard is that a copy has no syntax: `b = a;` declares nothing, names no
// member, and produces a second value with the same type. Whatever the index records about
// the line, it records twice about one declaration.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.2 — the struct the copies are made of. Mutable on purpose: value semantics
/// are only observable when there is something to change.
/// </summary>
public struct StValuePair
{
    /// <summary>The first component.</summary>
    public int First;

    /// <summary>The second component.</summary>
    public int Second;

    /// <summary>Clause 16.4.9 — the declared constructor.</summary>
    public StValuePair(int first, int second)
    {
        First = first;
        Second = second;
    }

    /// <summary>Clause 16.4.12 — a mutating method, which mutates whichever copy it is called on.</summary>
    public void Bump() => First++;
}

/// <summary>
/// Clause 16.4.2 — the same fields as a class, for contrast. Assignment of one of these
/// copies a reference, and the mutation below is seen through both names.
/// </summary>
public sealed class StReferencePair
{
    /// <summary>The first component.</summary>
    public int First;

    /// <summary>The second component.</summary>
    public int Second;

    /// <summary>The constructor.</summary>
    public StReferencePair(int first, int second)
    {
        First = first;
        Second = second;
    }

    /// <summary>A mutating method, which mutates the one object every name refers to.</summary>
    public void Bump() => First++;
}

/// <summary>
/// Clause 16.4.2 / 16.4.4 / 16.4.5 / 16.4.6 — the references. Each method is one sentence
/// of the clause turned into code that could be run to check it.
/// </summary>
public static class StValueSemanticsUse
{
    /// <summary>
    /// Clause 16.4.4 — assignment of a struct copies the value. Returns 1, because the
    /// bump lands on the copy.
    /// </summary>
    public static int AssignmentCopies()
    {
        StValuePair source = new StValuePair(1, 2);
        StValuePair copy = source;
        copy.Bump();
        return source.First;
    }

    /// <summary>
    /// Clause 16.4.2 — the class beside it. Returns 2, from the same shape of code, which
    /// is the whole content of the clause.
    /// </summary>
    public static int ReferenceAssignmentShares()
    {
        StReferencePair source = new StReferencePair(1, 2);
        StReferencePair alias = source;
        alias.Bump();
        return source.First;
    }

    /// <summary>Clause 16.4.2 — an argument passed by value is a copy too.</summary>
    public static int ArgumentCopies(StValuePair pair)
    {
        pair.Bump();
        return pair.First;
    }

    /// <summary>
    /// Clause 16.4.4 — assignment through a `ref` local, which is the exception: the ref
    /// is a reference variable, so the copy does not happen.
    /// </summary>
    public static int AssignmentThroughRef()
    {
        StValuePair source = new StValuePair(1, 2);
        ref StValuePair alias = ref source;
        alias.Bump();
        return source.First;
    }

    /// <summary>
    /// Clause 16.4.5 — the three spellings of a struct's default value. All three produce
    /// the same all-zero value, and only one of them names a constructor.
    /// </summary>
    public static bool ThreeDefaultsAgree()
    {
        StValuePair viaDefaultOperator = default(StValuePair);
        StValuePair viaTargetTypedDefault = default;
        StValuePair viaNew = new StValuePair();
        return viaDefaultOperator.First == viaTargetTypedDefault.First
            && viaTargetTypedDefault.First == viaNew.First;
    }

    /// <summary>
    /// Clause 16.4.5 — a struct field of a class is at its default value before any
    /// constructor of the struct has run, and no line here says so.
    /// </summary>
    public sealed class StDefaultHolder
    {
        /// <summary>A struct field, never assigned, therefore zero.</summary>
        public StValuePair Pair;

        /// <summary>An array of structs, every element at its default.</summary>
        public StValuePair[] Row = new StValuePair[4];

        /// <summary>A nullable struct field, whose default is the null value, not the zero one.</summary>
        public StValuePair? Maybe;
    }

    /// <summary>Clause 16.4.6 — boxing to `object`, then unboxing.</summary>
    public static int BoxRoundTrip(StValuePair pair)
    {
        object boxed = pair;
        StValuePair unboxed = (StValuePair)boxed;
        return unboxed.Second;
    }

    /// <summary>
    /// Clause 16.4.6 — boxing to an interface, which is the other boxing destination, and
    /// unboxing with a pattern rather than a cast.
    /// </summary>
    public static double BoxToInterface(StSquare square)
    {
        IStArea area = square;
        return area is StSquare back ? back.Area : 0.0;
    }

    /// <summary>
    /// Clause 16.4.6 — a mutation on a boxed struct, which mutates the box and not the
    /// original. The unbox-then-cast makes the copy the language requires.
    /// </summary>
    public static int MutateThroughBox(StValuePair pair)
    {
        object boxed = pair;
        StValuePair copy = (StValuePair)boxed;
        copy.Bump();
        return pair.First;
    }

    /// <summary>
    /// Clause 16.4.4, post-standard — a `with` expression on a struct that is not a
    /// record. C# 10 allows it on any struct: the receiver is copied and the named
    /// members are assigned in the copy.
    /// </summary>
    public static StValuePair WithSecond(StValuePair pair, int second) => pair with { Second = second };

    /// <summary>Clause 16.4.4 — `with` naming both members, so nothing survives from the receiver.</summary>
    public static StValuePair WithBoth(StValuePair pair) => pair with { First = 0, Second = 0 };
}
