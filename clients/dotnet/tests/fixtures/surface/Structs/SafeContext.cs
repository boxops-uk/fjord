// Clause 16.4.15 — Safe context constraint — and its eight subclauses. The safe context of
// an expression is the widest scope its value may escape to; a ref struct is exactly the
// kind whose values carry one. 16.4.15.1 states the rule, and 16.4.15.2 through 16.4.15.8
// give it for parameters, locals, fields, operators, method and property invocations,
// stackalloc, and constructor invocations.
//
// A safe context is a property of an expression, not a name, so most of this clause is a
// set of references rather than declarations. What IS declared is the `scoped` modifier and
// the parameter modifiers, and none of them changes a name: `scoped ref int` and `ref int`
// are the same parameter as far as any identity built from the name goes. That is the
// hazard the subclauses share, and the reason the same method name is declared here with
// four different modifiers on four different types.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.15.2 — every parameter modifier the clause gives a rule for, on one type.
/// The names differ because C# forbids overloads that differ only by `ref`, `out` or `in`;
/// the modifiers are the content.
/// </summary>
public static class StParameterSafeContext
{
    /// <summary>Clause 16.4.15.2 — a by-value parameter, whose safe context is the widest.</summary>
    public static int ByValue(int slot) => slot;

    /// <summary>Clause 16.4.15.2 — a `ref` parameter, which may be returned by reference.</summary>
    public static ref int ByRef(ref int slot) => ref slot;

    /// <summary>Clause 16.4.15.2 — a `scoped ref` parameter, which may NOT be returned by reference.</summary>
    public static int ByScopedRef(scoped ref int slot)
    {
        slot += 1;
        return slot;
    }

    /// <summary>Clause 16.4.15.2 — an `in` parameter, a readonly reference.</summary>
    public static int ByIn(in StValuePair pair) => pair.First;

    /// <summary>Clause 16.4.15.2 — a `scoped in` parameter.</summary>
    public static int ByScopedIn(scoped in StValuePair pair) => pair.Second;

    /// <summary>Clause 16.4.15.2, post-standard — a `ref readonly` parameter, which C# 12 added.</summary>
    public static int ByRefReadonly(ref readonly int slot) => slot;

    /// <summary>Clause 16.4.15.2 — an `out` parameter, which is definitely assigned on return.</summary>
    public static bool ByOut(int seed, out int slot)
    {
        slot = seed * 2;
        return slot > 0;
    }

    /// <summary>Clause 16.4.15.2 — a by-value ref struct parameter, whose value may escape by return.</summary>
    public static Span<int> ByValueRefStruct(Span<int> values) => values;

    /// <summary>Clause 16.4.15.2 — the same, `scoped`, so the value may not escape.</summary>
    public static int ByScopedRefStruct(scoped Span<int> values) => values.Length;

    /// <summary>Clause 16.4.15.2 — a `ref` parameter of ref struct type, which may be written through.</summary>
    public static void ByRefRefStruct(ref StRefCursor cursor) => cursor.Advance(1);

    /// <summary>Clause 16.4.15.2 — a `scoped ref` parameter of ref struct type.</summary>
    public static int ByScopedRefRefStruct(scoped ref StRefCursor cursor) => cursor.Offset;
}

/// <summary>
/// Clause 16.4.15.2 hazard — the same method name as one above, in another type, with a
/// different modifier. `StParameterSafeContext.ByValue(int)` and
/// `StOtherParameterSafeContext.ByValue(ref int)` are one name and two signatures that an
/// identity erasing parameter modifiers cannot tell apart. They are in two types because
/// C# will not let them share one.
/// </summary>
public static class StOtherParameterSafeContext
{
    /// <summary>Clause 16.4.15.2 — the modifier-only difference, in a type of its own.</summary>
    public static int ByValue(ref int slot) => slot;

    /// <summary>Clause 16.4.15.2 — and again, `in`.</summary>
    public static int ByIn(in int slot) => slot;
}

/// <summary>
/// Clause 16.4.15.3 — local variable safe context. A local of ref struct type, a `ref`
/// local, a `scoped ref` local and a `scoped` local of ref struct type each get a
/// different rule, and all four are declarations with no modifier in their name.
/// </summary>
public static class StLocalSafeContext
{
    /// <summary>Clause 16.4.15.3 — a `ref` local, which aliases another variable.</summary>
    public static int RefLocal()
    {
        int slot = 1;
        ref int alias = ref slot;
        alias = 2;
        return slot;
    }

    /// <summary>Clause 16.4.15.3 — a `scoped ref` local, whose ref safe context is this block.</summary>
    public static int ScopedRefLocal()
    {
        int slot = 3;
        scoped ref int alias = ref slot;
        alias += 1;
        return slot;
    }

    /// <summary>Clause 16.4.15.3 — a `ref readonly` local.</summary>
    public static int RefReadonlyLocal()
    {
        int slot = 5;
        ref readonly int alias = ref slot;
        return alias;
    }

    /// <summary>
    /// Clause 16.4.15.3 / 16.4.15.7 — a local of ref struct type initialized from
    /// stackalloc, and a `scoped` local narrowed from it. Neither declaration names the
    /// stack.
    /// </summary>
    public static int ScopedRefStructLocal()
    {
        Span<int> scratch = stackalloc int[4];
        scoped Span<int> narrowed = scratch;
        narrowed[0] = 9;
        return scratch[0];
    }

    /// <summary>
    /// Clause 16.4.15.3 hazard — two locals with one name in two sibling blocks. Neither
    /// is in scope where the other is declared, so the source is legal and the two
    /// declarations have the same name, the same type and the same containing method.
    /// </summary>
    public static int SiblingScopes(bool first)
    {
        if (first)
        {
            ref int alias = ref StLocalSafeContextStore.Slot;
            return alias;
        }
        else
        {
            scoped ref int alias = ref StLocalSafeContextStore.Slot;
            return alias + 1;
        }
    }
}

/// <summary>
/// Clause 16.4.15.4 — field safe context. A field of a ref struct is reached through its
/// containing variable, so its ref safe context is that variable's; a static field's is
/// the widest there is, which is why the one below can be returned by reference.
/// </summary>
public static class StLocalSafeContextStore
{
    /// <summary>Clause 16.4.15.4 — a static field, whose ref safe context is the whole program.</summary>
    public static int Slot = 1;

    /// <summary>Clause 16.4.15.4 — returning a reference to a static field, which always escapes safely.</summary>
    public static ref int SlotRef() => ref Slot;

    /// <summary>
    /// Clause 16.4.15.4 — a reference to a field of a by-`ref` struct parameter, which
    /// escapes exactly as far as the parameter does.
    /// </summary>
    public static ref int FirstOf(ref StValuePair pair) => ref pair.First;

    /// <summary>
    /// Clause 16.4.15.4 — the same field of a by-value parameter, which may NOT be
    /// returned by reference, so this method returns its value instead.
    /// </summary>
    public static int FirstValueOf(StValuePair pair) => pair.First;
}

/// <summary>
/// Clause 16.4.15.5 hazard — operators. This struct declares `operator +` twice and
/// `operator -` twice: one unary and one binary each. In source that is one token at two
/// arities; in metadata it is <c>op_UnaryPlus</c> and <c>op_Addition</c>,
/// <c>op_UnaryNegation</c> and <c>op_Subtraction</c>, which are four distinct names. An
/// identity taken from the source token merges each pair; one taken from the metadata name
/// or from a member ordinal does not.
/// </summary>
public struct StVector2
{
    /// <summary>The horizontal component.</summary>
    public double X;

    /// <summary>The vertical component.</summary>
    public double Y;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StVector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Clause 16.4.15.5 — unary plus: `operator +`, one operand.</summary>
    public static StVector2 operator +(StVector2 value) => value;

    /// <summary>Clause 16.4.15.5 — binary plus: `operator +`, two operands, the same token.</summary>
    public static StVector2 operator +(StVector2 left, StVector2 right) =>
        new StVector2(left.X + right.X, left.Y + right.Y);

    /// <summary>Clause 16.4.15.5 — unary minus.</summary>
    public static StVector2 operator -(StVector2 value) => new StVector2(-value.X, -value.Y);

    /// <summary>Clause 16.4.15.5 — binary minus, the same token again.</summary>
    public static StVector2 operator -(StVector2 left, StVector2 right) =>
        new StVector2(left.X - right.X, left.Y - right.Y);

    /// <summary>Clause 16.4.15.5 — equality, which C# requires be declared in a pair.</summary>
    public static bool operator ==(StVector2 left, StVector2 right) =>
        left.X.Equals(right.X) && left.Y.Equals(right.Y);

    /// <summary>Clause 16.4.15.5 — and inequality, the other half of the pair.</summary>
    public static bool operator !=(StVector2 left, StVector2 right) => !(left == right);

    /// <summary>Clause 16.4.3 — the override the operator pair obliges.</summary>
    public override bool Equals(object? obj) => obj is StVector2 other && this == other;

    /// <summary>Clause 16.4.3 — and its hash code.</summary>
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

/// <summary>
/// Clause 16.4.15.5 — an operator on a ref struct, where the safe context rule bites: the
/// result's ref safe context is the narrowest of the operands', so the span the cursor
/// holds may escape only as far as the operand it came from.
/// </summary>
public ref struct StSpanCursor
{
    private readonly ReadOnlySpan<int> _values;
    private readonly int _at;

    /// <summary>Clause 16.4.15.8 — the public constructor.</summary>
    public StSpanCursor(ReadOnlySpan<int> values)
    {
        _values = values;
        _at = 0;
    }

    private StSpanCursor(ReadOnlySpan<int> values, int at)
    {
        _values = values;
        _at = at;
    }

    /// <summary>Clause 16.4.15.6 — a property on a ref struct.</summary>
    public int Current => _values[_at];

    /// <summary>Clause 16.4.15.6 — a method on a ref struct, returning a ref struct.</summary>
    public StSpanCursor Rewound() => new StSpanCursor(_values, 0);

    /// <summary>Clause 16.4.15.5 — an operator whose operand and result are both ref structs.</summary>
    public static StSpanCursor operator +(StSpanCursor cursor, int by) =>
        new StSpanCursor(cursor._values, cursor._at + by);
}

/// <summary>
/// Clause 16.4.15.6 hazard — method and property invocation. A member invoked on a struct
/// receiver may be invoked on a copy, on the variable itself, or on a temporary, and the
/// syntax is identical in all three cases. The `Reading` name below is a property here and
/// a method in the next type, so the two invocations are spelled almost alike and resolve
/// to different member kinds.
/// </summary>
public struct StInvocationTarget
{
    private int _reading;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StInvocationTarget(int reading) => _reading = reading;

    /// <summary>Clause 16.4.15.6 — a property, invoked without parentheses.</summary>
    public int Reading => _reading;

    /// <summary>Clause 16.4.15.6 — a mutating method, whose receiver must be a variable.</summary>
    public void Bump() => _reading++;
}

/// <summary>
/// Clause 16.4.15.6 — the same simple name as the property above, as a method, in another
/// type. One name, two member kinds, two declaring types.
/// </summary>
public struct StOtherInvocationTarget
{
    private readonly int _reading;

    /// <summary>Clause 16.4.9 — the constructor.</summary>
    public StOtherInvocationTarget(int reading) => _reading = reading;

    /// <summary>Clause 16.4.15.6 — a method, invoked with parentheses.</summary>
    public int Reading() => _reading;
}

/// <summary>
/// Clause 16.4.15.6, 16.4.15.7 and 16.4.15.8 — the invocations, the stackallocs and the
/// constructor invocations, each in the shape its subclause is about.
/// </summary>
public static class StSafeContextUse
{
    /// <summary>Clause 16.4.15.6 — a property invocation on a value, and a method invocation on a variable.</summary>
    public static int InvokeBoth()
    {
        StInvocationTarget target = new StInvocationTarget(1);
        target.Bump();
        return target.Reading + new StOtherInvocationTarget(2).Reading();
    }

    /// <summary>
    /// Clause 16.4.15.6 — an invocation on a temporary, which is a variable the source
    /// never declares and which is discarded before the mutation can be seen.
    /// </summary>
    public static int InvokeOnTemporary() => new StInvocationTarget(1).Reading;

    /// <summary>Clause 16.4.15.6 — an invocation through an `in` parameter, which may copy first.</summary>
    public static int InvokeThroughIn(in StInvocationTarget target) => target.Reading;

    /// <summary>Clause 16.4.15.6 — an invocation on a ref struct receiver.</summary>
    public static int InvokeOnRefStruct(ReadOnlySpan<int> values) =>
        new StSpanCursor(values).Rewound().Current;

    /// <summary>Clause 16.4.15.7 — `stackalloc` into a span, whose safe context is this method.</summary>
    public static int StackallocIntoSpan()
    {
        Span<int> scratch = stackalloc int[4];
        scratch[0] = 1;
        scratch[3] = 4;
        return scratch[0] + scratch[3];
    }

    /// <summary>Clause 16.4.15.7 — `stackalloc` with an initializer, and to a readonly span.</summary>
    public static int StackallocInitialized()
    {
        ReadOnlySpan<int> values = stackalloc int[] { 1, 2, 3 };
        return values.Length;
    }

    /// <summary>
    /// Clause 16.4.15.7 / 16.4.15.8 — a stackalloc used directly as a constructor
    /// argument, so the ref struct's safe context is the argument's and neither is named.
    /// </summary>
    public static int StackallocIntoConstructor()
    {
        StSpanCursor cursor = new StSpanCursor(stackalloc int[] { 7, 8 });
        return (cursor + 1).Current;
    }

    /// <summary>Clause 16.4.15.8 — a constructor invocation with a `ref` argument, which the ref field captures.</summary>
    public static int ConstructWithRefArgument()
    {
        int slot = 1;
        int origin = 0;
        StRefCursor cursor = new StRefCursor(ref slot, ref origin);
        cursor.Advance(2);
        return slot;
    }

    /// <summary>
    /// Clause 16.4.15.8 — a constructor invocation of a non-ref struct with an object
    /// initializer, whose safe context is unconstrained because the type is not a ref struct.
    /// </summary>
    public static StValuePair ConstructWithInitializer() => new StValuePair(1, 2) { Second = 3 };

    /// <summary>Clause 16.4.15.5 — the four operator declarations, all exercised.</summary>
    public static bool Operators()
    {
        StVector2 one = new StVector2(1.0, 2.0);
        StVector2 two = new StVector2(3.0, 4.0);
        StVector2 sum = one + two;
        StVector2 difference = two - one;
        StVector2 negated = -one;
        StVector2 unchanged = +one;
        return sum != difference && negated.X < 0.0 && unchanged == one;
    }

    /// <summary>Clause 16.4.15.2 — the parameter modifiers, called.</summary>
    public static int CallEveryModifier()
    {
        int slot = 4;
        StValuePair pair = new StValuePair(1, 2);
        Span<int> scratch = stackalloc int[2];
        int total = StParameterSafeContext.ByValue(slot);
        total += StParameterSafeContext.ByRef(ref slot);
        total += StParameterSafeContext.ByScopedRef(ref slot);
        total += StParameterSafeContext.ByIn(in pair);
        total += StParameterSafeContext.ByScopedIn(pair);
        total += StParameterSafeContext.ByRefReadonly(in slot);
        total += StParameterSafeContext.ByOut(slot, out int produced) ? produced : 0;
        total += StParameterSafeContext.ByValueRefStruct(scratch).Length;
        total += StParameterSafeContext.ByScopedRefStruct(scratch);
        total += StOtherParameterSafeContext.ByValue(ref slot);
        total += StOtherParameterSafeContext.ByIn(slot);
        return total;
    }

    /// <summary>Clause 16.4.15.2 — the ref struct parameter modifiers, called.</summary>
    public static int CallRefStructModifiers()
    {
        int slot = 0;
        int origin = 0;
        StRefCursor cursor = new StRefCursor(ref slot, ref origin);
        StParameterSafeContext.ByRefRefStruct(ref cursor);
        return StParameterSafeContext.ByScopedRefRefStruct(ref cursor);
    }
}
