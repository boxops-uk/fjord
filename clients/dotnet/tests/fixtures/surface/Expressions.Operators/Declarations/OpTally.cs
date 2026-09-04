namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A running total that declares both halves of the compound-assignment story: the static
/// binary operator a compound assignment has always been rewritten into, and the C# 14
/// instance <c>operator +=</c> that a compound assignment now prefers.
/// </summary>
/// <remarks>
/// <para>Clause 12.23.5 says <c>x op= y</c> is evaluated as <c>x = x op y</c>. C# 14 adds a
/// user-defined compound assignment operator, declared instance and returning
/// <c>void</c>, which the compound assignment binds to in preference to the static one.</para>
/// <para>That leaves this type holding two declarations reached by the same three
/// characters, <c>+=</c>: <c>op_AdditionAssignment</c> (instance) is what a use selects,
/// and <c>op_Addition</c> (static) is what it would have selected without it. The same
/// doubling happens for increment: <c>operator ++()</c> emits
/// <c>op_IncrementAssignment</c> as an instance method and <c>operator ++(OpTally)</c>
/// emits <c>op_Increment</c> as a static one, so the type declares two members whose
/// source token is <c>++</c> and whose arities differ by the receiver.</para>
/// </remarks>
public sealed class OpTally
{
    /// <summary>Constructs a tally with a starting count.</summary>
    public OpTally(int count) => Count = count;

    /// <summary>The running count.</summary>
    public int Count { get; private set; }

    // 12.23.5 — the C# 14 instance compound assignment operators. `void`, and they mutate.
    public void operator +=(OpTally other) => Count += other.Count;

    public void operator -=(OpTally other) => Count -= other.Count;

    public void operator *=(int factor) => Count *= factor;

    // 12.9.7 / 12.8.16 — the C# 14 instance increment and decrement operators, which take
    // no operand at all because the receiver is the operand.
    public void operator ++() => Count++;

    public void operator --() => Count--;

    // 12.12.5 / 12.12.6 — the static operators the same tokens bound to before C# 14.
    public static OpTally operator +(OpTally left, OpTally right) => new(left.Count + right.Count);

    public static OpTally operator -(OpTally left, OpTally right) => new(left.Count - right.Count);

    // 12.9.7 — the static increment, which returns a new instance rather than mutating.
    public static OpTally operator ++(OpTally value) => new(value.Count + 1);

    /// <inheritdoc/>
    public override string ToString() => Count.ToString();
}
