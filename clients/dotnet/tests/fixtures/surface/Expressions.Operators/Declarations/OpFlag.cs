namespace Surface.Expressions.Operators.Declarations;

/// <summary>
/// A three-valued flag: the corpus's carrier for <c>operator true</c> and
/// <c>operator false</c>, and therefore for the user-defined conditional logical
/// operators of 12.16.3 and the boolean expressions of 12.26.
/// </summary>
/// <remarks>
/// <para>Clause 12.16.3 says <c>x &amp;&amp; y</c> is permitted on a user-defined type when
/// the type declares <c>operator &amp;</c> returning the type itself and both
/// <c>operator true</c> and <c>operator false</c>. The evaluation then calls
/// <c>op_False</c> and, if it answers false, <c>op_BitwiseAnd</c> — two member references
/// for which the source contains only the two characters <c>&amp;&amp;</c>.</para>
/// <para>Clause 12.26 makes the same pair the reason a value of this type may stand as the
/// condition of <c>if</c>, <c>while</c>, <c>do</c>, <c>for</c> and <c>?:</c>.</para>
/// </remarks>
public readonly struct OpFlag
{
    private readonly sbyte _state;

    private OpFlag(sbyte state) => _state = state;

    /// <summary>The definitely-true flag.</summary>
    public static OpFlag Yes => new(1);

    /// <summary>The definitely-false flag.</summary>
    public static OpFlag No => new(-1);

    /// <summary>The flag that is neither, so that both accessors below can answer false.</summary>
    public static OpFlag Unknown => new(0);

    // 12.26 / 12.16.3 — `op_True` and `op_False`, which must be declared as a pair.
    public static bool operator true(OpFlag value) => value._state > 0;

    public static bool operator false(OpFlag value) => value._state < 0;

    // 12.9.4 — logical negation, in its user-defined form.
    public static OpFlag operator !(OpFlag value) => new((sbyte)-value._state);

    // 12.16.3 — the two operators `&&` and `||` are defined in terms of.
    public static OpFlag operator &(OpFlag left, OpFlag right) =>
        new((sbyte)(left._state < right._state ? left._state : right._state));

    public static OpFlag operator |(OpFlag left, OpFlag right) =>
        new((sbyte)(left._state > right._state ? left._state : right._state));

    // 12.15.4's user-defined analogue, kept so the family is complete.
    public static OpFlag operator ^(OpFlag left, OpFlag right) =>
        new((sbyte)(left._state == right._state ? -1 : 1));

    /// <inheritdoc/>
    public override string ToString() => _state switch
    {
        > 0 => "yes",
        < 0 => "no",
        _ => "unknown",
    };
}
