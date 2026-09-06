namespace Surface.Expressions.Operators.Declarations;

/// <summary>A reading with no operators of its own.</summary>
/// <remarks>
/// Every operator that applies to this type is declared somewhere else, in
/// <see cref="OpReadingExtensions"/>. Clause 12.4.6 builds its candidate set from the
/// operand's type, and a C# 14 extension block widens what "provided by the type" means.
/// </remarks>
public readonly struct OpReading
{
    /// <summary>Constructs a reading.</summary>
    public OpReading(double value) => Value = value;

    /// <summary>The reading.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("0.##");
}

/// <summary>
/// Operators and members for <see cref="OpReading"/>, declared outside it in a C# 14
/// extension block.
/// </summary>
/// <remarks>
/// The declaration's containing type is this static class, the operand type is
/// <see cref="OpReading"/>, and the use site names neither: <c>a * b</c> on two readings
/// is a reference into a type the expression never mentions.
/// </remarks>
public static class OpReadingExtensions
{
    extension(OpReading reading)
    {
        /// <summary>Half the reading, as an extension property.</summary>
        public double Halved => reading.Value / 2;

        // 12.12.2 — a multiplication operator whose declaring type is not either operand's.
        public static OpReading operator *(OpReading left, OpReading right) =>
            new(left.Value * right.Value);

        // 12.12.5 — an addition operator in the same position.
        public static OpReading operator +(OpReading left, OpReading right) =>
            new(left.Value + right.Value);

        // 12.9.3 — a unary minus declared in an extension block.
        public static OpReading operator -(OpReading value) => new(-value.Value);
    }
}
