// Clause 15.6.10 (extension methods). An extension method is declared as a static method of a
// non-generic static class whose first parameter carries `this`, and is *referenced* as though
// it were an instance member of that parameter's type. The row is `both` for exactly that
// reason: the declaration lives in one type and every reference to it reads as a member of
// another.
//
// Two hazards, and both are written here.
//
//   1. An extension method never competes with an instance method: `MemBudget.Weight()` and
//      `MemBudgetExtensions.Weight(this MemBudget)` are two declarations of one name reachable
//      through one receiver, and `budget.Weight()` binds to the instance one, always. Only a
//      static call, `MemBudgetExtensions.Weight(budget)`, reaches the other. So an index that
//      resolves `budget.Weight()` to the extension has picked the member the compiler
//      discarded.
//   2. C# 14's extension blocks declare the same clause with a different syntax tree: the
//      receiver is on the `extension(...)` clause and not on the member, and the member may be
//      a property or an operator, which the `this`-parameter form cannot express. The two
//      spellings compile to members of one enclosing static class.

using System.Collections.Generic;

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.10: the receiver. Its own <c>Weight</c> is the member that wins.</summary>
public sealed class MemBudget
{
    /// <summary>What is left to spend.</summary>
    public int Remaining { get; init; }

    /// <summary>15.6.10: the instance method that shadows the extension of the same name.</summary>
    public int Weight() => Remaining;
}

/// <summary>15.6.10: the classic spelling — a static class of static methods whose first
/// parameter is marked <c>this</c>.</summary>
public static class MemBudgetExtensions
{
    /// <summary>15.6.10: the shadowed extension. Reachable only as a static call, because
    /// <see cref="MemBudget.Weight"/> is applicable to every receiver this is.</summary>
    public static int Weight(this MemBudget budget) => -budget.Remaining;

    /// <summary>15.6.10: an extension with no instance rival, so `budget.Halved()` is the only
    /// spelling anyone writes.</summary>
    public static int Halved(this MemBudget budget) => budget.Remaining / 2;

    /// <summary>15.6.10: an extension with further parameters after the receiver.</summary>
    public static int Scaled(this MemBudget budget, int factor, int offset = 0)
        => (budget.Remaining * factor) + offset;

    /// <summary>15.6.10: a generic extension method on a constructed generic type.</summary>
    public static TItem? Second<TItem>(this IReadOnlyList<TItem> items)
        => items.Count > 1 ? items[1] : default;

    /// <summary>15.6.10: an extension on an interface, so the receiver type is not a class.</summary>
    public static bool IsSpent(this IMemLimit limit) => limit.Limit == 0;

    /// <summary>15.6.10: an extension on a value type by readonly reference, which is the one
    /// place the receiver takes a modifier other than `this`.</summary>
    public static int Doubled(this in MemQuota quota) => quota.Cap * 2;

    /// <summary>15.6.10: both spellings of a reference to an extension, side by side. The first
    /// `Weight` is the instance member; the second is this class's.</summary>
    public static string UseAll()
    {
        var budget = new MemBudget { Remaining = 8 };
        IReadOnlyList<int> items = [1, 2, 3];
        var quota = new MemQuota(4);
        return $"{budget.Weight()} {Weight(budget)} {budget.Halved()} {budget.Scaled(2, 1)} " +
               $"{items.Second()} {new MemQuota(0).IsSpent()} {quota.Doubled()}";
    }
}

/// <summary>15.6.10: an interface used as an extension receiver.</summary>
public interface IMemLimit
{
    /// <summary>The ceiling.</summary>
    int Limit { get; }
}

/// <summary>15.6.10: a value-type receiver, so `this in` has something to bind to.</summary>
public readonly struct MemQuota(int cap) : IMemLimit
{
    /// <summary>The ceiling this quota carries.</summary>
    public int Cap { get; } = cap;

    /// <summary>15.7.1: the interface member, implemented implicitly.</summary>
    public int Limit => Cap;
}

/// <summary>15.6.10: the C# 14 spelling. One <c>extension</c> block per receiver, holding
/// members that the `this`-parameter form cannot declare at all — a property, a static member,
/// and an operator — all of which are still extension members of clause 15.6.10.</summary>
public static class MemQuotaExtensions
{
    /// <summary>Instance extension members for a quota.</summary>
    extension(MemQuota quota)
    {
        /// <summary>15.6.10 with 15.7.1: an extension property. There is no `this` parameter on
        /// the accessor — the receiver comes from the enclosing `extension` clause.</summary>
        public bool IsGenerous => quota.Cap > 100;

        /// <summary>15.6.10: an extension method inside a block, which is the same declaration
        /// as a `this`-parameter method and a different syntax tree.</summary>
        public MemQuota Grown(int by) => new MemQuota(quota.Cap + by);
    }

    /// <summary>Static extension members, whose receiver is the type and not a value.</summary>
    extension(MemQuota)
    {
        /// <summary>15.6.10: a static extension property, reached as `MemQuota.Unlimited`.</summary>
        public static MemQuota Unlimited => new MemQuota(int.MaxValue);

        /// <summary>15.6.10: a static extension method, reached as `MemQuota.Of(3)`.</summary>
        public static MemQuota Of(int cap) => new MemQuota(cap);
    }

    /// <summary>15.6.10: references to all four extension members. `MemQuota` declares none of
    /// these, and every one of them reads as though it did.</summary>
    public static string UseAll()
    {
        var quota = MemQuota.Of(7);
        return $"{quota.IsGenerous} {quota.Grown(1).Cap} {MemQuota.Unlimited.Cap}";
    }
}
