namespace Surface.Bindings.Roles;

/// <summary>Something with a property and a field to write.</summary>
public sealed class BindHolder
{
    /// <summary>Written seven ways in <c>BindWriteRole</c>.</summary>
    public int Value { get; set; }

    /// <summary>Written by reference, which the role never records.</summary>
    public int Slot;
}

/// <summary>
/// M9 — <c>CodeMarkup.Role</c> promotes a name to its enclosing expression only through a
/// <c>MemberAccessExpressionSyntax</c>, and only tests for a plain assignment, so four of
/// the seven writes below are keyed <c>read</c>.
/// </summary>
/// <remarks>
/// <para>
/// The rule is two lines:
/// <code>
/// var expression = name.Parent is MemberAccessExpressionSyntax access &amp;&amp; access.Name == name
///     ? (ExpressionSyntax)access
///     : name;
/// return expression.Parent is AssignmentExpressionSyntax assignment &amp;&amp; assignment.Left == expression
///     ? write : read;
/// </code>
/// Both halves are narrow, and each is narrow in a different way. The promotion misses a
/// <c>MemberBindingExpressionSyntax</c> — the <c>.Value</c> of <c>maybe?.Value</c>, which is
/// a different node kind — and the test misses every way of writing that is not spelled
/// with <c>=</c>: a postfix increment, a <c>ref</c> or <c>out</c> argument, a tuple
/// deconstruction whose left is a <c>TupleExpressionSyntax</c>.
/// </para>
/// <para>
/// <b>Measured, on these seven methods:</b> <see cref="Direct"/> write,
/// <see cref="Conditional"/> <b>read</b>, <see cref="Incremented"/> <b>read</b>,
/// <see cref="Compound"/> write, <see cref="Aliased"/> <b>read</b>,
/// <see cref="Deconstructed"/> <b>read</b>, <see cref="Initialised"/> write. The
/// object-initializer case being a <i>write</i> is the surprise: <c>{ Value = 1 }</c> is a
/// bare <c>IdentifierNameSyntax</c> whose parent really is an assignment, so it passes the
/// second test without needing the first.
/// </para>
/// <para>
/// <b><c>role</c> is a key field, so the row says the wrong thing rather than losing an
/// annotation.</b> <c>codemarkup.FileXRef {file, use, target, role}</c> — the role trails
/// the key, so a wrong role does not collide with anything and does not drop anything: it
/// files the reference under a value a consumer filters on. Asking for every write of
/// <see cref="BindHolder.Value"/> answers three of the six spans that write it, and the
/// three it omits are indistinguishable from reads at the same member. There is no count
/// anywhere that is off, which is why nothing detects this but a fixture that knows the
/// answer.
/// </para>
/// <para>
/// <b>Compound assignment is a read <i>and</i> a write and is keyed <c>write</c>.</b>
/// <c>holder.Value += 1</c> reads the property and writes it back;
/// <c>AssignmentExpressionSyntax</c> covers every compound form, so the row is
/// <c>write</c> and the read half is unrecorded. So the mapping is not merely too narrow —
/// it is a projection of a bitmask onto one value, which <c>codemarkup.sigla</c> says out
/// loud where it declares <c>Role</c>, and the corpus is where the projection's edges get
/// counted.
/// </para>
/// </remarks>
public static class BindWriteRole
{
    /// <summary>Somewhere for the <c>ref</c> case to write through.</summary>
    /// <param name="slot">The alias.</param>
    public static void Take(ref int slot) => slot = 3;

    /// <summary>A plain assignment through a member access: keyed <c>write</c>.</summary>
    /// <param name="holder">The target.</param>
    public static void Direct(BindHolder holder) => holder.Value = 1;

    /// <summary>
    /// A null-conditional assignment: the name's parent is a
    /// <c>MemberBindingExpressionSyntax</c>, the promotion does not fire, and the row is
    /// keyed <c>read</c>.
    /// </summary>
    /// <param name="maybe">The target, if there is one.</param>
    public static void Conditional(BindHolder? maybe) => maybe?.Value = 1;

    /// <summary>
    /// A postfix increment: the promotion fires, the assignment test does not, and the row
    /// is keyed <c>read</c>.
    /// </summary>
    /// <param name="holder">The target.</param>
    public static void Incremented(BindHolder holder) => holder.Value++;

    /// <summary>
    /// A compound assignment: keyed <c>write</c>, and the read half of it is unrecorded.
    /// </summary>
    /// <param name="holder">The target.</param>
    public static void Compound(BindHolder holder) => holder.Value += 1;

    /// <summary>
    /// A <c>ref</c> argument: the callee writes through it and the row is keyed
    /// <c>read</c>.
    /// </summary>
    /// <param name="holder">The target.</param>
    public static void Aliased(BindHolder holder) => Take(ref holder.Slot);

    /// <summary>
    /// A tuple deconstruction: the assignment's left is the tuple, not this member access,
    /// so <c>assignment.Left == expression</c> is false and the row is keyed <c>read</c>.
    /// </summary>
    /// <param name="holder">The target.</param>
    public static void Deconstructed(BindHolder holder) => (holder.Value, _) = (1, 2);

    /// <summary>
    /// An object initializer: a bare identifier whose parent <i>is</i> the assignment, so
    /// this one is keyed <c>write</c> without the promotion being involved at all.
    /// </summary>
    public static BindHolder Initialised() => new BindHolder { Value = 1 };

    /// <summary>A genuine read, for the role that is correct by construction.</summary>
    /// <param name="holder">The source.</param>
    public static int Plain(BindHolder holder) => holder.Value;
}
