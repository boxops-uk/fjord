namespace Surface.Quarantine.Arity;

/// <summary>
/// M1 — one type name at three arities. The run-killer this project is quarantined for.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 14.7 (type declarations), 8.4.1 and 8.4.4 (constructed types and type
/// arguments), 15.2.2.4.2 (type parameters of a class): the language says
/// <c>ArityStore</c>, <c>ArityStore&lt;T&gt;</c> and <c>ArityStore&lt;T, U&gt;</c> are
/// three unrelated types that happen to share a spelling, and a declaration space holds
/// all three because arity is part of the name.
/// </para>
/// <para>
/// <c>ScipSymbols.Descriptor</c>'s <c>NamedType</c> arm is
/// <c>text.Append(Name(symbol)).Append('#')</c>, and <c>Name</c> is <c>ISymbol.Name</c>,
/// which carries no arity — so all three mint one descriptor. Everything hanging off them
/// inherits the merge: <c>Put</c> in all three types, its parameter, and the <c>T</c> of
/// <c>ArityStore&lt;T&gt;</c> beside the <c>T</c> of <c>ArityStore&lt;T, U&gt;</c>.
/// </para>
/// <para>
/// <b>Why the run dies rather than answering wrongly.</b> <c>Definition</c> is keyed
/// <c>{symbol, file}</c> with the span and <c>qualified</c> on the value side, and
/// <c>qualified</c> is <c>ToDisplayString()</c>; <c>SymbolInfo</c> is keyed on the symbol
/// alone with a <c>Hover</c> signature that sets <c>IncludeTypeParameters</c>. So
/// <c>Surface.Quarantine.Arity.ArityStore</c> and
/// <c>Surface.Quarantine.Arity.ArityStore&lt;T&gt;</c> are two values under one key. The
/// three declarations sit in one file here, so both keys collide; splitting them across
/// files would still collide on <c>SymbolInfo</c>.
/// </para>
/// <para>
/// The fix is per-segment arity from <c>INamedTypeSymbol.MetadataName</c> or
/// <c>Arity</c> — never from counting written type arguments, which
/// <see cref="ArityUse"/> is here to trap.
/// </para>
/// </remarks>
public class ArityStore
{
    /// <summary>The arity-0 <c>Put</c>, whose parameter is an ordinary <c>int</c>.</summary>
    public void Put(int v)
    {
        _ = v;
    }
}

/// <summary>M1 — the arity-1 member of the same name. Clause 8.4.1.</summary>
/// <typeparam name="T">The stored type; its own descriptor segment is <c>ArityStore#[T]</c>.</typeparam>
public class ArityStore<T>
{
    /// <summary>The arity-1 <c>Put</c>, whose parameter type is a type parameter.</summary>
    public void Put(T v)
    {
        _ = v;
    }
}

/// <summary>M1 — the arity-2 member of the same name. Clause 8.4.1.</summary>
/// <typeparam name="T">Shares a descriptor segment with the <c>T</c> of the arity-1 form.</typeparam>
/// <typeparam name="U">The second parameter, which no descriptor segment records.</typeparam>
public class ArityStore<T, U>
{
    /// <summary>The arity-2 <c>Put</c>. Three unrelated methods, one descriptor.</summary>
    public void Put(T v)
    {
        _ = v;
    }
}
