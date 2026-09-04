namespace Surface.Quarantine.Arity;

/// <summary>
/// M1 — the merge repeats at every enclosing segment, not only the last.
/// </summary>
/// <remarks>
/// <para>
/// Clause 15.3.9.7 (nested types in a generic class) and 8.4.4: the containing type's
/// type parameters are part of a nested type's own parameter list, so
/// <c>ArityOuter&lt;T&gt;.ArityInner&lt;U&gt;</c> and
/// <c>ArityOuter.ArityInner&lt;U, V&gt;</c> agree in neither segment's arity.
/// </para>
/// <para>
/// The descriptor path is built by walking <c>ContainingSymbol</c> and appending
/// <c>Name(symbol) + '#'</c> at each step, so the loss is not one bad segment but one bad
/// segment per enclosing type: both nested types spell
/// <c>…/ArityOuter#ArityInner#</c>. That is why the assertion for this project has to
/// name the <i>first</i> path segment as well as the last — a fix that adds arity only to
/// the symbol being declared would still merge these two.
/// </para>
/// </remarks>
/// <typeparam name="T">The outer parameter, absent from the outer's descriptor segment.</typeparam>
public class ArityOuter<T>
{
    /// <summary>Nested in the arity-1 outer, itself arity-1 in its own right.</summary>
    /// <typeparam name="U">Spells <c>ArityOuter#ArityInner#[U]</c>.</typeparam>
    public class ArityInner<U>
    {
        /// <summary>A member so the nested type is not empty.</summary>
        public int Depth => 1;
    }
}

/// <summary>M1 — the arity-0 outer, holding an arity-2 nested type. Clause 15.3.9.7.</summary>
public class ArityOuter
{
    /// <summary>Nested in the arity-0 outer, arity-2, and one descriptor with its sibling.</summary>
    /// <typeparam name="U">Shares <c>ArityOuter#ArityInner#[U]</c> with the other nest.</typeparam>
    /// <typeparam name="V">Has no counterpart at all in the other nest.</typeparam>
    public class ArityInner<U, V>
    {
        /// <summary>A member so the nested type is not empty.</summary>
        public int Depth => 2;
    }
}
