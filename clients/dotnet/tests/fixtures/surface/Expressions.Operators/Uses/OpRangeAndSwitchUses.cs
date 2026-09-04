using System;
using Surface.Expressions.Operators.Declarations;

namespace Surface.Expressions.Operators.Uses;

/// <summary>
/// The range operator (12.10) and the switch expression (12.11).
/// </summary>
public static class OpRangeAndSwitchUses
{
    /// <summary>
    /// 12.10 — every form of the range operator: both endpoints, left only, right only,
    /// and neither. Each yields a <see cref="Range"/> and references its constructor or
    /// one of its factory properties without naming either.
    /// </summary>
    public static (Range Both, Range LeftOnly, Range RightOnly, Range Neither) RangeForms(int lo, int hi) =>
        (lo..hi, lo.., ..hi, ..);

    /// <summary>
    /// 12.10 — a range used to slice: an array, where the compiler emits a call to a
    /// runtime helper, and <see cref="OpSpan"/>, where it emits calls to <c>Length</c> and
    /// <c>Slice</c>.
    /// </summary>
    public static (int[] FromArray, OpSpan FromSpan, string FromString) Slicing(
        int[] items,
        OpSpan window,
        string text) =>
        (items[1..^1], window[1..^1], text[..3]);

    /// <summary>
    /// 12.10 — a range built from index-from-end expressions on both sides, so 12.9.6 and
    /// 12.10 appear in one expression.
    /// </summary>
    public static Range FromEndBothSides() => ^3..^1;

    /// <summary>
    /// 12.11 — the switch expression, whose arms are patterns and whose pattern
    /// designations are declarations inside an expression.
    /// </summary>
    public static string SwitchExpression(object candidate) => candidate switch
    {
        null => "none",
        string { Length: 0 } => "empty",
        string text => text,
        OpMoney money when money.Minor > 0 => money.ToString(),
        OpMoney => "non-positive money",
        int or long => "integral",
        int[] items => $"many:{items.Length}",
        _ => "other",
    };

    /// <summary>
    /// 12.11 — a switch expression whose arms are list patterns, each of which declares
    /// the variables its slice and element designations name.
    /// </summary>
    public static string SwitchOverList(int[] items) => items switch
    {
        [] => "empty",
        [var only] => $"one:{only}",
        [var head, .., var tail] => $"{head}..{tail}",
    };

    /// <summary>
    /// 12.11 — a switch expression over a tuple, with a relational pattern in each arm and
    /// a <c>throw</c> expression (12.18) in the default arm.
    /// </summary>
    public static string SwitchOverTuple(int left, int right) => (left, right) switch
    {
        ( < 0, _) => "left negative",
        (_, < 0) => "right negative",
        (0, 0) => "both zero",
        var (a, b) when a == b => "equal",
        _ => throw new ArgumentOutOfRangeException(nameof(left)),
    };

    /// <summary>
    /// 12.11 — a switch expression whose arms are operator expressions, so the arm
    /// selection guards two distinct user-defined operator references.
    /// </summary>
    public static OpMoney SwitchArmsAreOperators(OpMoney money, OpChannel channel) => channel switch
    {
        OpChannel.Left => -money,
        OpChannel.Right => money + money,
        OpChannel.Both => money * 2L,
        _ => default,
    };
}
