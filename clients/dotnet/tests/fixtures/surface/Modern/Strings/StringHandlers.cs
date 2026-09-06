using System.Runtime.CompilerServices;
using System.Text;

namespace Surface.Modern.Strings;

/// <summary>
/// C# 10 — Interpolated string handlers. A type marked
/// <see cref="InterpolatedStringHandlerAttribute"/> with the constructor and <c>Append…</c>
/// members the pattern names; the compiler rewrites an interpolated string argument into
/// calls on it. Those members are found *by name and shape*, not through an interface, which
/// is what makes them worth indexing — nothing in the signature says they are overridden or
/// implemented, so a call to one has no declared contract to point at.
/// </summary>
[InterpolatedStringHandler]
public ref struct TraceHandler
{
    private readonly StringBuilder _builder;

    /// <summary>The constructor shape the pattern requires: literal length and hole count.</summary>
    public TraceHandler(int literalLength, int formattedCount)
    {
        _builder = new StringBuilder(literalLength + (formattedCount * 8));
        Holes = formattedCount;
    }

    /// <summary>
    /// The constructor overload the pattern uses when
    /// <see cref="InterpolatedStringHandlerArgumentAttribute"/> names another argument.
    /// </summary>
    public TraceHandler(int literalLength, int formattedCount, int width)
    {
        _builder = new StringBuilder(literalLength + (formattedCount * width));
        Holes = formattedCount;
    }

    /// <summary>How many holes the handler was told to expect.</summary>
    public int Holes { get; }

    /// <summary>Appends the literal text between two holes.</summary>
    public void AppendLiteral(string text) => _builder.Append(text);

    /// <summary>Appends a hole's value.</summary>
    /// <typeparam name="T">The hole's type, which the compiler supplies.</typeparam>
    public void AppendFormatted<T>(T value) => _builder.Append(value?.ToString());

    /// <summary>Appends a hole's value with an alignment and a format.</summary>
    /// <typeparam name="T">The hole's type.</typeparam>
    public void AppendFormatted<T>(T value, int alignment, string? format) =>
        _builder.Append(((value as IFormattable)?.ToString(format, null) ?? value?.ToString())?.PadLeft(alignment));

    /// <summary>The accumulated text.</summary>
    public override string ToString() => _builder.ToString();
}

/// <summary>Calls the handler, so the compiler's rewrite has a site.</summary>
public static class StringHandlers
{
    /// <summary>A method whose parameter is the handler type rather than <c>string</c>.</summary>
    public static string Trace(TraceHandler handler) => handler.ToString();

    /// <summary>
    /// C# 10 — <c>[InterpolatedStringHandlerArgument]</c>: the handler's constructor is
    /// handed another argument of the same call.
    /// </summary>
    public static string TraceWide(int width, [InterpolatedStringHandlerArgument(nameof(width))] TraceHandler handler) =>
        handler.ToString();

    /// <summary>
    /// The call sites. Neither interpolated string is ever a <c>string</c>: the compiler
    /// constructs a <see cref="TraceHandler"/> and calls its members instead.
    /// </summary>
    public static string Both(int count) =>
        Trace($"count = {count}") + TraceWide(6, $"count = {count,4:D}");
}
