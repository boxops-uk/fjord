namespace Surface.Bindings.Interfaces;

/// <summary>
/// M24, the total case — a static abstract interface member, whose only legal call site in
/// C# is through a constrained type parameter, and which therefore has no reachable use at
/// all.
/// </summary>
/// <remarks>
/// <para>
/// A static abstract member (C# 11, ECMA-334 draft-v9 18.4.4 as amended) can only be
/// invoked on a type parameter that is constrained to the interface —
/// <c>TUnit.Weight()</c> in <see cref="BindConstrained.Of{TUnit}"/>. Measured, the bound
/// symbol there is <c>IBindUnit&lt;TUnit&gt;.Weight()</c>, whose original definition is
/// the interface's member: so the reference rows target the <i>abstract</i> declaration.
/// </para>
/// <para>
/// <b>And there is no other spelling.</b> <c>BindUnitValue.Weight()</c> is legal C# and is
/// deliberately not written here, because writing it would give the implementation a
/// reference and destroy the claim. As the fixture stands,
/// <see cref="BindUnitValue.Weight"/> is a declaration with a definition row, a search
/// entry, a symbol, and an empty <c>csharp.EntityRef</c> — reachable by no query that
/// starts from a use. Combined with M24's missing member-level <c>implements</c> edge,
/// there is no path from the interface member to it and no path from it to the interface
/// member: the implementation is an island, and the corpus asserts that as an anti-join.
/// </para>
/// <para>
/// <b>The type parameter is a reference too, and it fuses.</b> <c>TUnit</c> in
/// <c>TUnit.Weight()</c> is a name that binds to an <c>ITypeParameterSymbol</c>, and
/// <c>csharp.TypeParameter</c>'s key is
/// <c>{name, variance, hasNotNull, hasReferenceType, hasValueType}</c> with no declaring
/// scope — so it is the same row as any other invariant unconstrained <c>TUnit</c> in the
/// corpus. That fusion is a mechanism of its own and is stated here only so that a reader
/// of this file does not attribute it to M24.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The implementing type, by the curiously recurring pattern.</typeparam>
public interface IBindUnit<TSelf>
    where TSelf : IBindUnit<TSelf>
{
    /// <summary>
    /// The static abstract member. Every reference row in the corpus that mentions a
    /// <c>Weight</c> on a unit targets this declaration.
    /// </summary>
    static abstract int Weight();
}

/// <summary>M24 — the implementation, which nothing in the index reaches.</summary>
public sealed class BindUnitValue : IBindUnit<BindUnitValue>
{
    /// <summary>
    /// Implements <c>IBindUnit&lt;TSelf&gt;.Weight</c>. Not called anywhere by name, on
    /// purpose.
    /// </summary>
    public static int Weight() => 3;
}

/// <summary>M24 — the one call site the language allows.</summary>
public static class BindConstrained
{
    /// <summary>
    /// Invokes the static abstract member through the constrained type parameter.
    /// </summary>
    /// <typeparam name="TUnit">A unit.</typeparam>
    public static int Of<TUnit>()
        where TUnit : IBindUnit<TUnit> => TUnit.Weight();

    /// <summary>Instantiates it, so the constraint is satisfied by a real type.</summary>
    public static int Value() => Of<BindUnitValue>();
}
