using System;

namespace Surface.Conversions.ImplicitForms;

/// <summary>What a decision came to. The common target of a switch expression's arms.</summary>
public interface IConvOutcome
{
    /// <summary>Says what happened.</summary>
    string Describe();
}

/// <summary>An accepted outcome. Unrelated to <see cref="ConvRejected"/> on purpose.</summary>
public sealed class ConvAccepted : IConvOutcome
{
    /// <inheritdoc/>
    public string Describe() => "accepted";
}

/// <summary>A rejected outcome, sharing no base class with <see cref="ConvAccepted"/>.</summary>
public sealed class ConvRejected : IConvOutcome
{
    /// <inheritdoc/>
    public string Describe() => "rejected";
}

/// <summary>
/// Clauses 10.2.17 and 10.2.18 — the two conversions whose source expression has no type of
/// its own. A <c>throw</c> expression converts to every type, and a switch expression whose
/// arms have no common type converts to a target every arm can reach.
/// </summary>
public static class ConvThrowAndSwitch
{
    /// <summary>Clause 10.2.17 — a <c>throw</c> as the right operand of <c>??</c>.</summary>
    public static int Required(int? value) =>
        value ?? throw new ArgumentNullException(nameof(value));

    /// <summary>Clause 10.2.17 — a <c>throw</c> as one arm of a conditional.</summary>
    public static string Chosen(bool flag) =>
        flag ? "yes" : throw new InvalidOperationException("no");

    /// <summary>Clause 10.2.17 — a <c>throw</c> converted to a reference type.</summary>
    public static ConvLeaf Demanded(ConvNode node) =>
        node as ConvLeaf ?? throw new InvalidCastException("not a leaf");

    /// <summary>Clause 10.2.17 — a <c>throw</c> as a switch expression arm.</summary>
    public static long Widened(int code) => code switch
    {
        0 => 0,
        1 => 1L,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    /// <summary>
    /// Clause 10.2.18 — a switch expression with no natural type. <see cref="ConvAccepted"/>
    /// and <see cref="ConvRejected"/> have no common type, so the conversion is applied to
    /// each arm separately against the return type.
    /// </summary>
    public static IConvOutcome Decide(bool flag) => flag switch
    {
        true => new ConvAccepted(),
        false => new ConvRejected(),
    };

    /// <summary>Clause 10.2.18 — the same, with the target being a nullable value type.</summary>
    public static double? Rate(ConvStroke stroke) => stroke switch
    {
        ConvStroke.None => null,
        ConvStroke.Thin => 1,
        _ => 2.5,
    };

    /// <summary>Clause 10.2.18 — a switch expression whose arms are lambdas.</summary>
    public static Func<int, int> Adjuster(bool doubled) => doubled switch
    {
        true => value => value * 2,
        false => value => value,
    };
}
