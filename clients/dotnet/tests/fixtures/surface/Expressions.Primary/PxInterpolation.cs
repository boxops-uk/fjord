using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.3 — an interpolated string handler that is only reached when a condition holds. The
/// constructor's shape is part of the pattern: the extra parameters come from
/// <c>InterpolatedStringHandlerArgument</c>, and the trailing <c>out bool</c> is what lets the
/// compiler skip the appends entirely.
/// </summary>
[InterpolatedStringHandler]
public struct PxConditionalHandler
{
    private readonly StringBuilder? _text;

    /// <summary>The conditional handler shape: two lengths, the borrowed argument, and an out flag.</summary>
    public PxConditionalHandler(int literalLength, int formattedCount, bool enabled, out bool shouldAppend)
    {
        _text = enabled ? new StringBuilder(literalLength) : null;
        shouldAppend = enabled;
    }

    /// <summary>Bound per literal run, and called only when the handler said so.</summary>
    public void AppendLiteral(string value) => _text?.Append(value);

    /// <summary>Bound per hole.</summary>
    /// <typeparam name="T">The hole's type.</typeparam>
    public void AppendFormatted<T>(T value) => _text?.Append(value);

    /// <inheritdoc />
    public override string ToString() => _text?.ToString() ?? string.Empty;
}

/// <summary>
/// 12.8.3 — interpolated string expressions. Every form here binds to members no use site
/// spells: a plain <c>$"..."</c> reaches <c>DefaultInterpolatedStringHandler</c> or
/// <c>string.Format</c>, and a handler-typed parameter reaches the handler's own
/// <c>AppendLiteral</c> and <c>AppendFormatted</c> overloads.
/// </summary>
public static class PxInterpolation
{
    /// <summary>A parameter of handler type, so an interpolated string argument is rewritten into appends.</summary>
    public static string Render(PxLogHandler handler) => handler.ToString();

    /// <summary>
    /// A conditional handler, with the condition borrowed from an earlier parameter — the
    /// appends are bound but may never run.
    /// </summary>
    public static string RenderIf(
        bool enabled,
        [InterpolatedStringHandlerArgument(nameof(enabled))] PxConditionalHandler handler)
        => handler.ToString();

    /// <summary>
    /// 12.8.3 — the plain forms: one hole, several holes, an alignment, a format specifier,
    /// both, an escaped brace, and a verbatim interpolated string in both spellings.
    /// </summary>
    public static string PlainForms()
    {
        var seed = 42;
        var point = new PxPoint(1, 2);

        var one = $"seed {seed}";
        var several = $"{seed} and {point.X} and {point.Y}";
        var aligned = $"{seed,8}";
        var formatted = $"{seed:D4}";
        var alignedAndFormatted = $"{seed,-8:X}";
        var escaped = $"{{{seed}}}";
        var verbatim = $@"path\{seed}";
        var verbatimOtherOrder = @$"path\{seed}";

        return string.Join(" ", one, several, aligned, formatted, alignedAndFormatted, escaped, verbatim, verbatimOtherOrder);
    }

    /// <summary>
    /// 12.8.3 — holes are expressions, so every other clause in this project can appear inside
    /// one: an invocation, an element access, a null-conditional, a nested interpolation, a
    /// conditional, and a <c>nameof</c>.
    /// </summary>
    public static string HolesAreExpressions()
    {
        var target = new PxTarget(3);
        PxMaybe? maybe = null;

        var invocation = $"measured {target.Measure()}";
        var element = $"slot {target[1]}";
        var conditional = $"maybe {maybe?.Seed}";
        var nested = $"outer {$"inner {target.Seed}"}";
        var ternary = $"{(target.Seed > 0 ? "positive" : "negative")}";
        var named = $"{nameof(PxTarget.Compute)}";
        var creation = $"{new PxPoint(4, 5)}";

        return string.Join(" ", invocation, element, conditional, nested, ternary, named, creation);
    }

    /// <summary>
    /// 12.8.3 — a raw interpolated string, where the number of <c>$</c> decides how many braces
    /// open a hole. The post-standard form of the same clause.
    /// </summary>
    public static string RawForms()
    {
        var seed = 7;

        var single = $"""
            seed is {seed}
            """;

        var doubled = $$"""
            a literal {brace} and a hole {{seed}}
            """;

        return single + doubled;
    }

    /// <summary>
    /// 12.8.3 — the conversion target decides what an interpolated string binds to: a
    /// <c>string</c>, a <c>FormattableString</c>, an <c>IFormattable</c>, or a custom handler.
    /// </summary>
    public static string ConversionTargets()
    {
        var seed = 3;

        string asString = $"seed {seed}";
        FormattableString asFormattable = $"seed {seed}";
        IFormattable asIFormattable = $"seed {seed}";
        var custom = Render($"seed {seed} and a string {asString}");
        var conditional = RenderIf(true, $"seed {seed}");
        var skipped = RenderIf(false, $"seed {seed}");
        var invariant = FormattableString.Invariant($"seed {seed}");
        var withCulture = asFormattable.ToString(CultureInfo.InvariantCulture);

        return string.Join(" ", asString, asFormattable.Format, asIFormattable.ToString(null, CultureInfo.InvariantCulture), custom, conditional, skipped, invariant, withCulture);
    }

    /// <summary>
    /// 12.8.3 — a constant interpolated string: when every hole is a constant, the expression is
    /// itself a constant, and no handler is reached at all.
    /// </summary>
    public const string Constant = $"a {nameof(PxInterpolation)} constant";

    /// <summary>Same-file use of the constant, so the fact has a reader.</summary>
    public static int ConstantLength() => Constant.Length;
}
