// Clause 19.4.7 — interface operators — and 19.4.8 — interface static constructors. An interface
// may declare a static operator with a body, and it may declare one `static abstract` or
// `static virtual`, which an implementing type then supplies. An operator's name is not an
// identifier: it is `operator +`, and its metadata name is `op_Addition`.
//
// One member per name per type is observed here in the one place it bites: `IfaceTally` declares
// its addition operator once, as an explicit implementation, and not also implicitly. A type
// holding both would hold two members whose metadata name is `op_Addition`.
//
// 19.4.7 as written forbids an interface from declaring a conversion, equality or inequality
// operator. The conversion operator below is declared `static abstract`, which the clause did
// not have and the compiler accepts, so the rule and its successor are both on record here.
// Equality and inequality operators are absent: a pair of them declared `static abstract` would
// belong to whichever project owns clause 15.10, and neither is needed to reach this clause.

using System;

namespace Surface.Interfaces;

/// <summary>
/// 19.4.7 hazard — static abstract and static virtual operators, in the shape the framework's
/// own numeric interfaces use: the type parameter is constrained to the interface itself, so an
/// implementation is a type that can be substituted for it.
/// </summary>
/// <typeparam name="TSelf">19.4.7 — the implementing type, constrained to this interface.</typeparam>
public interface IfaceAddable<TSelf>
    where TSelf : IfaceAddable<TSelf>
{
    /// <summary>19.4.7 — a static abstract operator. An implementation must declare it, and
    /// the declaration here has no body.</summary>
    static abstract TSelf operator +(TSelf left, TSelf right);

    /// <summary>19.4.7 — a static abstract unary operator, so both arities are present.</summary>
    static abstract TSelf operator -(TSelf value);

    /// <summary>19.4.7 — a static abstract property, which is the identity element the default
    /// implementations below need.</summary>
    static abstract TSelf Zero { get; }

    /// <summary>
    /// 19.4.7 — a static abstract explicit conversion operator, whose source type is the type
    /// parameter constrained to this interface. Draft-v9's 19.4.7 says it is a compile-time
    /// error for an interface to declare a conversion operator; the `static abstract` form
    /// post-dates that sentence and this compiles, so the construct is the clause's rule and
    /// the language's answer to it in one declaration.
    /// </summary>
    static abstract explicit operator int(TSelf value);

    /// <summary>19.4.7 hazard — a static virtual member with a body. It is virtual, so an
    /// implementing type may override it, and it has an implementation, so it is also the
    /// member that runs when none does.</summary>
    static virtual TSelf Twice(TSelf value) => value + value;

    /// <summary>19.4.7 — a static virtual property with a body, in terms of the abstract one.</summary>
    static virtual TSelf One => TSelf.Zero;
}

/// <summary>
/// 19.4.7 — a struct satisfying every static abstract member of
/// <see cref="IfaceAddable{TSelf}"/> implicitly, and inheriting both static virtual defaults.
/// </summary>
public readonly struct IfaceMoney : IfaceAddable<IfaceMoney>
{
    /// <summary>19.4.7 — the value the operators work on.</summary>
    public IfaceMoney(int pence) => Pence = pence;

    /// <summary>19.4.7 — read by the conversion operator below.</summary>
    public int Pence { get; }

    /// <summary>19.4.7 — the implicit implementation of the static abstract property.</summary>
    public static IfaceMoney Zero => new(0);

    /// <summary>19.4.7 — the implicit implementation of the binary operator, and the only
    /// member of this type whose metadata name is <c>op_Addition</c>.</summary>
    public static IfaceMoney operator +(IfaceMoney left, IfaceMoney right) =>
        new(left.Pence + right.Pence);

    /// <summary>19.4.7 — the unary one.</summary>
    public static IfaceMoney operator -(IfaceMoney value) => new(-value.Pence);

    /// <summary>19.4.7 — the conversion operator, satisfying the static abstract conversion.</summary>
    public static explicit operator int(IfaceMoney value) => value.Pence;
}

/// <summary>
/// 19.4.7 hazard — a struct satisfying the same interface by explicit implementation. Each
/// member's name is qualified by the constructed interface <c>IfaceAddable&lt;IfaceTally&gt;</c>,
/// so the operator's name carries a substitution as well as a symbol, and this type declares
/// each such member exactly once.
/// </summary>
public readonly struct IfaceTally : IfaceAddable<IfaceTally>
{
    /// <summary>19.4.7 — the count this type carries.</summary>
    public IfaceTally(int count) => Count = count;

    /// <summary>19.4.7 — read by the explicit conversion below.</summary>
    public int Count { get; }

    /// <summary>19.4.7 — an explicitly implemented static abstract property.</summary>
    static IfaceTally IfaceAddable<IfaceTally>.Zero => new(0);

    /// <summary>19.4.7 — an explicitly implemented static abstract operator. Its name is a
    /// qualified name whose qualifier is a constructed type.</summary>
    static IfaceTally IfaceAddable<IfaceTally>.operator +(IfaceTally left, IfaceTally right) =>
        new(left.Count + right.Count);

    /// <summary>19.4.7 — the unary operator, explicitly.</summary>
    static IfaceTally IfaceAddable<IfaceTally>.operator -(IfaceTally value) => new(-value.Count);

    /// <summary>19.4.7 — an explicitly implemented conversion operator.</summary>
    static explicit IfaceAddable<IfaceTally>.operator int(IfaceTally value) => value.Count;

    /// <summary>19.4.7 — the static virtual default overridden explicitly, so this type's
    /// <c>Twice</c> is found instead of the interface's.</summary>
    static IfaceTally IfaceAddable<IfaceTally>.Twice(IfaceTally value) => new(value.Count * 2);
}

/// <summary>
/// 19.4.7 — the static abstract and static virtual members reached through a type parameter,
/// which is the only way they can be reached. <c>T.Zero</c> and <c>T.Twice</c> are member
/// accesses whose qualifier is a type parameter.
/// </summary>
public static class IfaceAddableUse
{
    /// <summary>19.4.7 — the static abstract property and operator, through the constraint.</summary>
    public static T Sum<T>(T[] values)
        where T : IfaceAddable<T>
    {
        var total = T.Zero;

        foreach (var value in values)
        {
            total = total + value;
        }

        return total;
    }

    /// <summary>19.4.7 — the static virtual member: <see cref="IfaceMoney"/> inherits the
    /// interface's implementation and <see cref="IfaceTally"/> supplies its own, so one call
    /// site resolves to two different members.</summary>
    public static T Twice<T>(T value)
        where T : IfaceAddable<T>
        => T.Twice(value);

    /// <summary>19.4.7 — the static virtual property, likewise.</summary>
    public static T One<T>()
        where T : IfaceAddable<T>
        => T.One;

    /// <summary>19.4.7 — the unary operator and the conversion, through the constraint.</summary>
    public static int NegatedValue<T>(T value)
        where T : IfaceAddable<T>
        => (int)(-value);

    /// <summary>19.4.7 — the two implementations at one call site each.</summary>
    public static int Totals() =>
        (int)Sum([new IfaceMoney(2), new IfaceMoney(3)])
        + ((IfaceTally)Twice(new IfaceTally(4))).Count;
}

/// <summary>
/// 19.4.7 — a static operator with a body in an interface, which is not abstract and not
/// virtual: the interface itself is the operand type, so no implementing type supplies it.
/// </summary>
public interface IfaceOperand
{
    /// <summary>19.4.7 — the value the operator reads through the interface.</summary>
    int Value { get; }

    /// <summary>19.4.7 — a concrete static operator declared in an interface. Its operands are
    /// of the interface type, and it is found on the interface rather than on any
    /// implementation.</summary>
    static int operator +(IfaceOperand left, IfaceOperand right) => left.Value + right.Value;
}

/// <summary>19.4.7 — an implementation of the operand interface, which declares no operator.</summary>
public sealed class IfaceOperandBox : IfaceOperand
{
    /// <summary>19.4.7 — the one member this class declares.</summary>
    public int Value => 6;

    /// <summary>19.4.7 — the interface's operator applied to two implementations, which binds
    /// to the member declared in <see cref="IfaceOperand"/>.</summary>
    public static int Twice(IfaceOperandBox box)
    {
        IfaceOperand left = box;
        IfaceOperand right = box;
        return left + right;
    }
}

/// <summary>
/// 19.4.8 hazard — an interface static constructor. It has no name of its own, no parameters and
/// no accessibility, it runs once before any static member of the interface is touched, and it is
/// a member of the interface that no implementation inherits.
/// </summary>
public interface IfaceCounted
{
    /// <summary>19.4.8 — a static field the static constructor assigns.</summary>
    static readonly string Origin;

    /// <summary>19.4.8 — a static field with an initialiser as well, so the constructor and the
    /// initialisers both run and their order is observable.</summary>
    static int Ticks = 1;

    /// <summary>
    /// 19.4.8 — the static constructor. Its declaration is spelled with the interface's own
    /// name, which is also the name of a type: the one member of an interface whose identity
    /// cannot be built from its name alone.
    /// </summary>
    static IfaceCounted()
    {
        Origin = "19.4.8";
        Ticks += 1;
    }

    /// <summary>19.4.8 — the state the static constructor left, read through the interface.</summary>
    static string Describe() => $"{Origin}:{Ticks}";

    /// <summary>19.4.8 — an instance member, so the interface is worth implementing.</summary>
    string Label { get; }
}

/// <summary>19.4.8 — an implementation, which has a static constructor of its own.</summary>
public sealed class IfaceCountedBox : IfaceCounted
{
    /// <summary>19.4.8 — a class static constructor beside an interface one, so the two
    /// declarations of the same kind are in one compilation and in different types.</summary>
    static IfaceCountedBox() => Prefix = "box";

    /// <summary>19.4.8 — assigned by the class's static constructor.</summary>
    public static readonly string Prefix;

    /// <summary>19.4.8 — the instance member, which names the interface's static state.</summary>
    public string Label => $"{Prefix}:{IfaceCounted.Describe()}";
}
