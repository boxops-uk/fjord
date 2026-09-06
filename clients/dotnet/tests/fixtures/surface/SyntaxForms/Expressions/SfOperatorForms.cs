using System;

namespace Surface.SyntaxForms.Expressions;

// The operator expression kinds, as four census groups: 22 binary, 13 assignment, 9
// prefix-unary, 3 postfix-unary. Plus `ParenthesizedExpression`, `RangeExpression`,
// `IndexExpression` and `AwaitExpression`, which are single rows.
//
// **None of these has a name node, and every one of them is a reference.** `a + b` over two
// `int`s binds to a `MethodKind.BuiltinOperator`; over `SfOperatorOverloads` it binds to a
// user-declared `op_Addition` that is a real method with a real location. A reference
// dispatch keyed on `SimpleNameSyntax` sees neither: there is no identifier in `a + b` other
// than the operands. The same holds for every compound assignment, every increment, `a..b`
// (which constructs a `System.Range`), `^1` (a `System.Index`), and `await e` (which calls
// `GetAwaiter`, `IsCompleted`, `OnCompleted` and `GetResult`). Where the operator is
// user-declared the missing edge is a call to source the index does hold a definition for,
// which is the case worth gating.

/// <summary>A type declaring the operators the overload section below uses.</summary>
public readonly struct SfOperatorOverloads
{
    /// <summary>The wrapped total.</summary>
    public int Value { get; }

    /// <summary>Wraps a total.</summary>
    /// <param name="value">The total.</param>
    public SfOperatorOverloads(int value) => Value = value;

    /// <summary>User-defined addition, so <c>AddExpression</c> can bind to source.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Their sum.</returns>
    public static SfOperatorOverloads operator +(SfOperatorOverloads left, SfOperatorOverloads right) =>
        new(left.Value + right.Value);

    /// <summary>User-defined negation, so <c>UnaryMinusExpression</c> can bind to source.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>Its negation.</returns>
    public static SfOperatorOverloads operator -(SfOperatorOverloads operand) => new(-operand.Value);

    /// <summary>User-defined equality, which C# requires in pairs.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they hold the same total.</returns>
    public static bool operator ==(SfOperatorOverloads left, SfOperatorOverloads right) =>
        left.Value == right.Value;

    /// <summary>The other half of the equality pair.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they hold different totals.</returns>
    public static bool operator !=(SfOperatorOverloads left, SfOperatorOverloads right) =>
        left.Value != right.Value;

    /// <inheritdoc/>
    public override bool Equals(object? other) => other is SfOperatorOverloads o && o == this;

    /// <inheritdoc/>
    public override int GetHashCode() => Value;
}

/// <summary>Every operator expression kind, written once each.</summary>
public static class SfOperatorForms
{
    /// <summary>
    /// The 22 binary kinds. Ten arithmetic and shift, six relational, three logical and
    /// bitwise pairs, and the three that are binary in the grammar but not arithmetic at
    /// all: <c>is</c>, <c>as</c> and <c>??</c>.
    /// </summary>
    /// <param name="left">The left operand throughout.</param>
    /// <param name="right">The right operand throughout.</param>
    /// <returns>A fold of every result, so nothing is dead.</returns>
    public static int Binary(int left, int right)
    {
        var add = left + right;                       // AddExpression
        var subtract = left - right;                  // SubtractExpression
        var multiply = left * right;                  // MultiplyExpression
        var divide = right == 0 ? 0 : left / right;   // DivideExpression
        var modulo = right == 0 ? 0 : left % right;   // ModuloExpression
        var leftShift = left << 1;                    // LeftShiftExpression
        var rightShift = left >> 1;                   // RightShiftExpression
        var unsignedShift = left >>> 1;               // UnsignedRightShiftExpression
        var bitwiseAnd = left & right;                // BitwiseAndExpression
        var bitwiseOr = left | right;                 // BitwiseOrExpression
        var exclusiveOr = left ^ right;               // ExclusiveOrExpression

        var equals = left == right;                   // EqualsExpression
        var notEquals = left != right;                // NotEqualsExpression
        var lessThan = left < right;                  // LessThanExpression
        var lessOrEqual = left <= right;              // LessThanOrEqualExpression
        var greaterThan = left > right;               // GreaterThanExpression
        var greaterOrEqual = left >= right;           // GreaterThanOrEqualExpression

        var logicalAnd = equals && lessThan;          // LogicalAndExpression
        var logicalOr = notEquals || greaterThan;     // LogicalOrExpression

        object boxed = add;
        var isType = boxed is string;                 // IsExpression
        var asType = boxed as string;                 // AsExpression
        var coalesce = asType ?? "none";              // CoalesceExpression

        var overloaded = new SfOperatorOverloads(left) + new SfOperatorOverloads(right);

        return add + subtract + multiply + divide + modulo
            + leftShift + rightShift + unsignedShift
            + bitwiseAnd + bitwiseOr + exclusiveOr
            + (lessOrEqual ? 1 : 0) + (greaterOrEqual ? 1 : 0)
            + (logicalAnd ? 1 : 0) + (logicalOr ? 1 : 0)
            + (isType ? 1 : 0) + coalesce.Length + overloaded.Value;
    }

    /// <summary>
    /// The 13 assignment kinds: simple assignment, the eleven compound forms, and
    /// <c>??=</c>.
    /// </summary>
    /// <param name="seed">Where to start.</param>
    /// <returns>What the chain arrives at.</returns>
    public static int Assignment(int seed)
    {
        int slot;
        slot = seed;        // SimpleAssignmentExpression
        slot += 3;          // AddAssignmentExpression
        slot -= 1;          // SubtractAssignmentExpression
        slot *= 2;          // MultiplyAssignmentExpression
        slot /= 2;          // DivideAssignmentExpression
        slot %= 97;         // ModuloAssignmentExpression
        slot &= 0xFF;       // AndAssignmentExpression
        slot |= 0x10;       // OrAssignmentExpression
        slot ^= 0x0F;       // ExclusiveOrAssignmentExpression
        slot <<= 1;         // LeftShiftAssignmentExpression
        slot >>= 1;         // RightShiftAssignmentExpression
        slot >>>= 1;        // UnsignedRightShiftAssignmentExpression

        string? name = null;
        name ??= "assigned"; // CoalesceAssignmentExpression

        return slot + name.Length;
    }

    /// <summary>
    /// The 9 prefix-unary kinds and the 3 postfix-unary kinds. Two of the prefix forms
    /// exist only over pointers, and one of them — <c>^1</c> — is spelled the same as the
    /// <c>IndexExpression</c> row.
    /// </summary>
    /// <param name="seed">Where to start.</param>
    /// <returns>A fold of every result.</returns>
    public static unsafe int Unary(int seed)
    {
        var slot = seed;

        var plus = +slot;             // UnaryPlusExpression
        var minus = -slot;            // UnaryMinusExpression
        var not = ~slot;              // BitwiseNotExpression
        var negated = !(slot > 0);    // LogicalNotExpression
        var preIncrement = ++slot;    // PreIncrementExpression
        var preDecrement = --slot;    // PreDecrementExpression
        var overloaded = -new SfOperatorOverloads(seed);

        int* cell = &slot;            // AddressOfExpression
        var pointed = *cell;          // PointerIndirectionExpression
        var fromEnd = ^1;             // IndexExpression, which is prefix-unary over `^`

        var postIncrement = slot++;   // PostIncrementExpression
        var postDecrement = slot--;   // PostDecrementExpression
        string? maybe = "present";
        var suppressed = maybe!;      // SuppressNullableWarningExpression

        return plus + minus + not + (negated ? 1 : 0)
            + preIncrement + preDecrement + pointed + fromEnd.Value
            + postIncrement + postDecrement + suppressed.Length + overloaded.Value;
    }

    /// <summary>
    /// ParenthesizedExpression, RangeExpression, IndexExpression and AwaitExpression — four
    /// single rows, in the smallest expressions that need them.
    /// </summary>
    /// <param name="left">One operand.</param>
    /// <param name="right">The other.</param>
    /// <returns>A fold of every result.</returns>
    public static int Every(int left, int right)
    {
        // ParenthesizedExpression, which changes what the tree says and not what the
        // program means.
        var grouped = (left + right) * (left - right);

        var window = new[] { 1, 2, 3, 4, 5 };

        // IndexExpression as an argument, and RangeExpression both bounded and open.
        var last = window[^1];
        var middle = window[1..3];
        var tail = window[2..];
        var head = window[..2];
        var whole = window[..];

        // AwaitExpression, reached synchronously so the fixture stays a pure computation.
        var awaited = Delayed(left).GetAwaiter().GetResult();

        return grouped + last + middle.Length + tail.Length + head.Length + whole.Length
            + awaited + Binary(left, right) + Assignment(left) + Unary(right);
    }

    /// <summary>An async method, so <c>await</c> has something to await.</summary>
    /// <param name="value">What to return.</param>
    /// <returns>The value, after an awaited completion.</returns>
    public static async System.Threading.Tasks.Task<int> Delayed(int value)
    {
        // AwaitExpression over a task, and over a value task, which bind through different
        // awaiter types and neither through a name in this expression.
        await System.Threading.Tasks.Task.CompletedTask;
        var ready = await new System.Threading.Tasks.ValueTask<int>(value);
        return ready;
    }
}
