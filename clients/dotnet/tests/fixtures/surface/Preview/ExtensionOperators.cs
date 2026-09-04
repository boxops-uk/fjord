using System;

namespace Surface.Preview;

/// <summary>A tally an extension block declares operators for, and which declares none itself.</summary>
public readonly struct Tally
{
    /// <summary>How many.</summary>
    public int Count { get; init; }

    /// <inheritdoc/>
    public override string ToString() => Count.ToString();
}

/// <summary>
/// C# 14 — Extension operators: user-defined operators declared inside an
/// <c>extension</c> block. <see cref="Tally"/> declares no operator at all, so every operator
/// below belongs to a type that is not the operand's — which is a resolution nothing at the
/// use site can express. <c>a + b</c> in <see cref="ExtensionOperatorUses"/> is a reference
/// into a static class the expression never names.
/// </summary>
public static class TallyOperators
{
    /// <summary>C# 14 — a static extension block, which is where binary operators live.</summary>
    extension(Tally)
    {
        /// <summary>C# 14 — an extension <c>+</c>.</summary>
        public static Tally operator +(Tally left, Tally right) => new() { Count = left.Count + right.Count };

        /// <summary>C# 14 — an extension <c>-</c>, whose checked form is below.</summary>
        public static Tally operator -(Tally left, Tally right) => new() { Count = left.Count - right.Count };

        /// <summary>
        /// C# 14 and C# 11 at once — an extension operator in its <c>checked</c> form. It
        /// requires the unchecked one above to exist, as any checked operator does.
        /// </summary>
        public static Tally operator checked -(Tally left, Tally right) =>
            new() { Count = checked(left.Count - right.Count) };

        /// <summary>C# 14 — an extension comparison operator, which must come in a pair.</summary>
        public static bool operator >(Tally left, Tally right) => left.Count > right.Count;

        /// <summary>The other half of the pair.</summary>
        public static bool operator <(Tally left, Tally right) => left.Count < right.Count;
    }

    /// <summary>
    /// C# 14 — an extension block whose receiver is <c>ref</c>, which is what a user-defined
    /// compound assignment operator needs: it mutates the left operand in place.
    /// </summary>
    extension(ref Tally target)
    {
        /// <summary>C# 14 — an extension compound assignment operator.</summary>
        public void operator +=(int amount) => target = new Tally { Count = target.Count + amount };

        /// <summary>C# 14 — an extension increment operator, which is also void and in-place.</summary>
        public void operator ++() => target = new Tally { Count = target.Count + 1 };
    }
}

/// <summary>Applies every extension operator, so each declaration has a use.</summary>
public static class ExtensionOperatorUses
{
    /// <summary>The binary and comparison operators.</summary>
    public static string Binary()
    {
        var left = new Tally { Count = 5 };
        var right = new Tally { Count = 3 };

        var sum = left + right;
        var difference = checked(left - right);
        var uncheckedDifference = unchecked(right - left);

        return $"{sum}{difference}{uncheckedDifference}{left > right}{left < right}";
    }

    /// <summary>The in-place operators, which need a writable variable.</summary>
    public static string InPlace()
    {
        var tally = new Tally { Count = 1 };

        tally += 4;
        tally++;

        return tally.ToString();
    }
}
