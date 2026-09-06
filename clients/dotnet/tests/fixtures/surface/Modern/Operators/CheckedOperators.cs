namespace Surface.Modern.Operators;

/// <summary>
/// C# 11 — checked user-defined operators. A type may declare two versions of the same
/// operator: the plain one and a <c>checked</c> one, chosen by whether the *call site* is in
/// a checked context. Both have the same name and the same signature — the only thing that
/// tells them apart is the <c>checked</c> keyword — so a reference from an expression to one
/// of them cannot be resolved from the operator's spelling alone.
/// </summary>
public readonly struct Counter
{
    /// <summary>Creates a counter.</summary>
    public Counter(int value) => Value = value;

    /// <summary>The count.</summary>
    public int Value { get; }

    /// <summary>C# 11 — the unchecked addition.</summary>
    public static Counter operator +(Counter left, Counter right) => new(unchecked(left.Value + right.Value));

    /// <summary>C# 11 — <c>operator checked +</c>, selected inside a checked context.</summary>
    public static Counter operator checked +(Counter left, Counter right) => new(checked(left.Value + right.Value));

    /// <summary>C# 11 — the unchecked binary subtraction.</summary>
    public static Counter operator -(Counter left, Counter right) => new(unchecked(left.Value - right.Value));

    /// <summary>C# 11 — <c>operator checked -</c>, binary.</summary>
    public static Counter operator checked -(Counter left, Counter right) => new(checked(left.Value - right.Value));

    /// <summary>C# 11 — the unchecked unary negation, a different operator with the same token.</summary>
    public static Counter operator -(Counter value) => new(unchecked(-value.Value));

    /// <summary>C# 11 — <c>operator checked -</c>, unary.</summary>
    public static Counter operator checked -(Counter value) => new(checked(-value.Value));

    /// <summary>C# 11 — the unchecked multiplication.</summary>
    public static Counter operator *(Counter left, Counter right) => new(unchecked(left.Value * right.Value));

    /// <summary>C# 11 — <c>operator checked *</c>.</summary>
    public static Counter operator checked *(Counter left, Counter right) => new(checked(left.Value * right.Value));

    /// <summary>C# 11 — the unchecked division.</summary>
    public static Counter operator /(Counter left, Counter right) => new(unchecked(left.Value / right.Value));

    /// <summary>C# 11 — <c>operator checked /</c>, which differs only at <c>int.MinValue / -1</c>.</summary>
    public static Counter operator checked /(Counter left, Counter right) => new(checked(left.Value / right.Value));

    /// <summary>C# 11 — the unchecked increment.</summary>
    public static Counter operator ++(Counter value) => new(unchecked(value.Value + 1));

    /// <summary>C# 11 — <c>operator checked ++</c>.</summary>
    public static Counter operator checked ++(Counter value) => new(checked(value.Value + 1));

    /// <summary>C# 11 — the unchecked decrement.</summary>
    public static Counter operator --(Counter value) => new(unchecked(value.Value - 1));

    /// <summary>C# 11 — <c>operator checked --</c>.</summary>
    public static Counter operator checked --(Counter value) => new(checked(value.Value - 1));

    /// <summary>C# 11 — the unchecked explicit conversion.</summary>
    public static explicit operator short(Counter value) => unchecked((short)value.Value);

    /// <summary>C# 11 — <c>operator checked explicit</c>, the conversion half of the feature.</summary>
    public static explicit operator checked short(Counter value) => checked((short)value.Value);

    /// <summary>An implicit conversion, which may *not* have a checked form — the contrast.</summary>
    public static implicit operator int(Counter value) => value.Value;
}

/// <summary>Calls each operator from both a checked and an unchecked context.</summary>
public static class CheckedOperators
{
    /// <summary>
    /// Every call here selects the <c>checked</c> operator; the same expressions in
    /// <see cref="Unchecked"/> select the plain one. The two methods have identical bodies.
    /// </summary>
    public static int Checked()
    {
        var left = new Counter(3);
        var right = new Counter(4);

        checked
        {
            var sum = left + right;
            var difference = left - right;
            var negated = -left;
            var product = left * right;
            var quotient = right / left;
            var incremented = left;
            incremented++;
            var decremented = right;
            decremented--;
            var narrowed = (short)sum;

            return sum.Value + difference.Value + negated.Value + product.Value
                + quotient.Value + incremented.Value + decremented.Value + narrowed;
        }
    }

    /// <summary>The same expressions in an unchecked context.</summary>
    public static int Unchecked()
    {
        var left = new Counter(3);
        var right = new Counter(4);

        unchecked
        {
            var sum = left + right;
            var difference = left - right;
            var negated = -left;
            var product = left * right;
            var quotient = right / left;
            var incremented = left;
            incremented++;
            var decremented = right;
            decremented--;
            var narrowed = (short)sum;

            return sum.Value + difference.Value + negated.Value + product.Value
                + quotient.Value + incremented.Value + decremented.Value + narrowed;
        }
    }

    /// <summary>Uses the implicit conversion, which has no checked counterpart.</summary>
    public static int Implicit() => new Counter(9);
}
