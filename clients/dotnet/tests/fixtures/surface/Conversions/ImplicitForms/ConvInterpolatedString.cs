using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Surface.Conversions.ImplicitForms;

/// <summary>
/// An interpolated string handler — the type clause 10.2.5 lets an interpolated string
/// convert into directly, so that the pieces are appended instead of formatted into a
/// throwaway string. Its members are found by name and signature, not by an interface, so
/// every one of them is a declaration an index has to hold for the conversion to be
/// explicable.
/// </summary>
[InterpolatedStringHandler]
public struct ConvTraceHandler
{
    private readonly StringBuilder _builder;

    /// <summary>The shape the compiler looks for: literal length and hole count.</summary>
    /// <param name="literalLength">Total length of the literal pieces.</param>
    /// <param name="formattedCount">How many holes the string has.</param>
    public ConvTraceHandler(int literalLength, int formattedCount) =>
        _builder = new StringBuilder(literalLength + (formattedCount * 8));

    /// <summary>Appends one literal piece.</summary>
    public void AppendLiteral(string value) => _builder.Append(value);

    /// <summary>Appends one hole.</summary>
    public void AppendFormatted<T>(T value) => _builder.Append(value?.ToString());

    /// <summary>The assembled text.</summary>
    public override string ToString() => _builder.ToString();
}

/// <summary>
/// Clause 10.2.5 — the implicit interpolated string conversions. One interpolated string
/// expression has four possible targets, and which one is in scope decides what the compiler
/// emits: a concatenation, a <see cref="FormattableString"/>, or a handler.
/// </summary>
public static class ConvInterpolatedString
{
    /// <summary>Clause 10.2.5 — to <see cref="FormattableString"/>, keeping the holes apart.</summary>
    public static FormattableString ToFormattable(int count) => $"count is {count}";

    /// <summary>Clause 10.2.5 — to <see cref="IFormattable"/>, the interface form.</summary>
    public static IFormattable ToInterface(int count) => $"count is {count}";

    /// <summary>Clause 10.2.5 — to <c>string</c>, which is the conversion that emits code.</summary>
    public static string ToPlainString(int count) => $"count is {count}";

    /// <summary>Clause 10.2.5 — to a handler type, resolved by the handler's own members.</summary>
    public static string ThroughHandler(int count)
    {
        ConvTraceHandler handler = $"traced {count}";
        return handler.ToString();
    }

    /// <summary>Clause 10.2.5 — a string with a format specifier in the hole.</summary>
    public static FormattableString WithFormat(DateTime moment) => $"at {moment:o}";

    /// <summary>Clause 10.2.5 — a string with no holes, which still converts.</summary>
    public static FormattableString WithoutHoles() => $"constant";
}
