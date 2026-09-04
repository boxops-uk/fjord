namespace Surface.Quarantine.Partial;

/// <summary>
/// M3 — the implementing half, in a second file, naming the same parameters differently.
/// </summary>
/// <remarks>
/// <para>
/// Clause 15.6.9. This is the half <c>ContainingType.GetMembers()</c> does <i>not</i>
/// return, so <c>Disambiguator</c>'s <c>FindIndex</c> answers −1 for it and the
/// <c>index &lt;= 0</c> guard spells that miss as the bare, ordinal-zero form. Here the
/// bare form happens to be right, because <c>Handle</c> has no sibling overload; the
/// <c>Ordinal</c> project is the same guard where the bare form belongs to another method.
/// </para>
/// <para>
/// <b>The edge residue.</b> <c>Declare</c> calls <c>Edges(symbol)</c> for each declaration
/// and <c>Edges</c> runs <c>Ordered(MethodParameter, self, method.Parameters)</c> over
/// <c>OriginalDefinition</c>, which for this half carries <i>its</i> parameter names.
/// <c>Entity(method)</c> gives both halves one <c>csharp.Method</c> key — the
/// documentation id is positional (<c>M:….Handle``1(``0)</c>) and so identical — and
/// <c>MethodParameter {method, index, parameter}</c> is all key with no value, so both
/// rows are accepted. The method then has parameter <c>item</c> at index 0 <i>and</i>
/// parameter <c>value</c> at index 0, and <c>MethodTypeParameter</c> likewise holds
/// <c>TItem</c> and <c>TValue</c> both at index 0. That is a wrong answer rather than a
/// refusal, so it survives the identity fix and needs its own count assertion.
/// </para>
/// </remarks>
public partial class PartialHandler
{
    /// <summary>
    /// The implementing half. This is the summary Roslyn resolves for both halves.
    /// </summary>
    /// <typeparam name="TValue">The other half calls this <c>TItem</c>.</typeparam>
    /// <param name="value">The other half calls this <c>item</c>.</param>
    public partial void Handle<TValue>(TValue value)
    {
        _ = value;
    }
}
