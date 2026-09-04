namespace Surface.Quarantine.Terms;

/// <summary>
/// M4 — an interface that overloads its indexer by a type parameter. Clause 18.4.6.
/// </summary>
/// <remarks>
/// While the interface is open, <c>this[K]</c> and <c>this[int]</c> have different
/// parameter types and the declaration is legal; once <c>K</c> is <c>string</c> the two
/// remain distinct members of the constructed interface, and an implementing class must
/// implement both. Under a descriptor with no disambiguator they already collide here,
/// before any implementation exists.
/// </remarks>
/// <typeparam name="K">The key type, which the first indexer's parameter list names.</typeparam>
/// <typeparam name="V">The value type, which both indexers return.</typeparam>
public interface ITermsMap<K, V>
{
    /// <summary>Indexed by the key type parameter.</summary>
    /// <param name="key">The key.</param>
    V this[K key] { get; }

    /// <summary>Indexed by <c>int</c> — a positional lookup beside the keyed one.</summary>
    /// <param name="i">The position.</param>
    V this[int i] { get; }
}

/// <summary>
/// M4 — two explicit implementations of two overloaded members of one <i>constructed</i>
/// generic interface, which reach the same nameless arm.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 18.6.2 (explicit interface member implementations) and 15.7.1: an explicit
/// implementation names the interface it implements, and for an explicit implementation
/// Roslyn's <c>ISymbol.Name</c> is the qualifying interface's <i>display string</i> joined
/// to the member's metadata name —
/// <c>Surface.Quarantine.Terms.ITermsMap&lt;System.String,System.Int32&gt;.this[]</c>.
/// </para>
/// <para>
/// <c>Descriptor</c> passes that whole string through <c>Name</c>/<c>Escaped</c> and
/// appends only <c>.</c>, so both implementations below spell one descriptor:
/// <c>…/TermsLookup#`Surface.Quarantine.Terms.ITermsMap&lt;System.String,System.Int32&gt;.this[]`.</c>
/// Two declarations, two spans, one symbol, in one file — the same refusal as
/// <see cref="TermsIndexed"/>, reached by a different route, and the reason the fix has to
/// be a disambiguator on the term arm rather than a special case for element access.
/// </para>
/// <para>
/// The constructed interface is also what puts a substituted type spelling
/// (<c>System.String</c>) inside a descriptor segment, so this declaration's identity
/// depends on how <c>ToDisplayString</c> renders type arguments.
/// </para>
/// </remarks>
public class TermsLookup : ITermsMap<string, int>
{
    /// <summary>The keyed implementation.</summary>
    int ITermsMap<string, int>.this[string key] => key.Length;

    /// <summary>The positional implementation, one descriptor with the keyed one.</summary>
    int ITermsMap<string, int>.this[int i] => i;
}
