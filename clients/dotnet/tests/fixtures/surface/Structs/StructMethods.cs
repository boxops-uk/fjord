// Clause 16.4.12 — Methods. A method of a struct follows the rules for a method of a
// class, minus the ones that need inheritance: no `abstract`, no `virtual`, and `override`
// only for a member inherited from System.ValueType or object. What is left is the whole
// of overloading, generics, ref returns, and the parameter modifiers.
//
// The hazard is the overload set. Overloads share a name, a declaring type and, in the
// generic-arity case, a parameter list; they are told apart by a signature or by an
// ordinal, and an ordinal counted off a member list that also holds the unwritten
// parameterless constructor (16.4.9) is the shape that drifts. `StMethodSet` has five
// members called `Combine` and one unwritten constructor sorted among them.

using System;
using System.Collections.Generic;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.12 — one overload set of five, plus every other method form a struct may
/// declare. There is no indexer in this type: two indexers in one type refuse a write, and
/// the indexers have a file of their own.
/// </summary>
public struct StMethodSet
{
    /// <summary>The state the methods work on.</summary>
    public int Total;

    /// <summary>Clause 16.4.9 — the declared constructor. The unwritten one sorts before it.</summary>
    public StMethodSet(int total) => Total = total;

    /// <summary>Clause 16.4.12 — overload one: one parameter.</summary>
    public int Combine(int value) => Total + value;

    /// <summary>Clause 16.4.12 — overload two: two parameters, so the arity differs.</summary>
    public int Combine(int first, int second) => Total + first + second;

    /// <summary>Clause 16.4.12 — overload three: one parameter of a different type.</summary>
    public int Combine(double value) => Total + (int)value;

    /// <summary>
    /// Clause 16.4.12 — overload four: generic, one type parameter, one value parameter.
    /// It differs from overload one only by arity of type parameters.
    /// </summary>
    public int Combine<T>(T value)
        where T : struct => Total + value.GetHashCode();

    /// <summary>Clause 16.4.12 — overload five: two type parameters.</summary>
    public int Combine<T1, T2>(T1 first, T2 second)
        where T1 : struct
        where T2 : struct => Total + first.GetHashCode() + second.GetHashCode();

    /// <summary>Clause 16.4.12 — a method with an optional parameter, which is one declaration and two callable shapes.</summary>
    public int Scaled(int factor = 2) => Total * factor;

    /// <summary>Clause 16.4.12 — a `params` array parameter.</summary>
    public int SumAll(params int[] values)
    {
        int total = Total;
        foreach (int value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Clause 16.4.12, post-standard — `params` over a span, which C# 13 added.</summary>
    public int SumSpan(params ReadOnlySpan<int> values)
    {
        int total = Total;
        foreach (int value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>
    /// Clause 16.4.12 / 16.4.15.4 — a ref return over a struct's field. It has to be
    /// static and take the receiver by `ref`: inside an instance member `this` is
    /// implicitly `scoped ref`, so `ref this.Total` is CS8170 — which is what 16.4.7 and
    /// 16.4.15.4 add up to, and the reason this signature looks the way it does.
    /// </summary>
    public static ref int TotalRef(ref StMethodSet set) => ref set.Total;

    /// <summary>Clause 16.4.12 — a `ref readonly` return, over a static field so the ref safe context is wide.</summary>
    public static ref readonly int CeilingRef() => ref _ceiling;

    private static readonly int _ceiling = 100;

    /// <summary>Clause 16.4.12 — a static method on a struct.</summary>
    public static StMethodSet Of(int total) => new StMethodSet(total);

    /// <summary>Clause 16.4.12 — an iterator method in a struct, whose state machine is a class the source never declares.</summary>
    public IEnumerable<int> Steps()
    {
        for (int i = 0; i < Total; i++)
        {
            yield return i;
        }
    }

    /// <summary>Clause 16.4.12 — an async method in a struct, which is another unwritten state machine.</summary>
    public async System.Threading.Tasks.Task<int> DelayedTotalAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        return Total;
    }

    /// <summary>Clause 16.4.12 — a method with a local function, which declares a method nobody can call from outside.</summary>
    public int WithLocalFunction(int bound)
    {
        return Fold(Total);

        int Fold(int seed) => seed > bound ? bound : seed;
    }

    /// <summary>Clause 16.4.12 — the one `override` a struct method may be, on a readonly member.</summary>
    public readonly override string ToString() => $"total {Total}";
}

/// <summary>
/// Clause 16.4.12 — a second type with a method called `Combine`, whose signature matches
/// one of the five above exactly. The declaring type is the only thing that tells them
/// apart, which is the merge this pair is here to test.
/// </summary>
public struct StOtherMethodSet
{
    /// <summary>The state.</summary>
    public int Total;

    /// <summary>Clause 16.4.12 — the same name and the same signature, in a different type.</summary>
    public int Combine(int value) => Total - value;
}

/// <summary>
/// Clause 16.4.12 — the calls. Overload resolution picks one of five `Combine`s at each
/// site below, and two of the sites pick a generic one by inference, so the method the
/// call names is not the method the source spells.
/// </summary>
public static class StMethodUse
{
    /// <summary>Picks overload one.</summary>
    public static int One(StMethodSet set) => set.Combine(1);

    /// <summary>Picks overload two, by argument count.</summary>
    public static int Two(StMethodSet set) => set.Combine(1, 2);

    /// <summary>Picks overload three, by argument type.</summary>
    public static int Three(StMethodSet set) => set.Combine(1.5);

    /// <summary>Picks overload four, by inference from a type argument the site does not write.</summary>
    public static int Four(StMethodSet set) => set.Combine(DateTime.MinValue);

    /// <summary>Picks overload four again, with the type argument written out.</summary>
    public static int FourExplicit(StMethodSet set) => set.Combine<long>(1L);

    /// <summary>Picks overload five.</summary>
    public static int Five(StMethodSet set) => set.Combine(1L, 2.5);

    /// <summary>Clause 16.4.12 — the same simple name in the other type.</summary>
    public static int Other(StOtherMethodSet set) => set.Combine(1);

    /// <summary>Clause 16.4.12 — the optional parameter, omitted and supplied.</summary>
    public static int Optional(StMethodSet set) => set.Scaled() + set.Scaled(3);

    /// <summary>Clause 16.4.12 — a named argument, which reorders nothing and renames the parameter at the site.</summary>
    public static int Named(StMethodSet set) => set.Scaled(factor: 4);

    /// <summary>Clause 16.4.12 — both `params` forms, one of which allocates and one of which does not.</summary>
    public static int Params(StMethodSet set) => set.SumAll(1, 2, 3) + set.SumSpan(1, 2, 3);

    /// <summary>Clause 16.4.12 — a ref return, assigned through.</summary>
    public static int ThroughRefReturn()
    {
        StMethodSet set = new StMethodSet(1);
        ref int total = ref StMethodSet.TotalRef(ref set);
        total = 9;
        return set.Total;
    }

    /// <summary>Clause 16.4.12 — the iterator, consumed.</summary>
    public static int StepCount(StMethodSet set)
    {
        int count = 0;
        foreach (int step in set.Steps())
        {
            count += step;
        }

        return count;
    }

    /// <summary>Clause 16.4.12 — a method group converted to a delegate, which names no call.</summary>
    public static Func<int, int> AsDelegate(StMethodSet set) => set.Combine;
}
