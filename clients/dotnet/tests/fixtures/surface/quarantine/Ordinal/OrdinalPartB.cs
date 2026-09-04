namespace Surface.Quarantine.Ordinal;

/// <summary>
/// M2, declaration face — the second part: the implementing half that
/// <c>GetMembers()</c> does not return.
/// </summary>
/// <remarks>
/// Clause 15.6.9. This half is a <c>MethodDeclarationSyntax</c> the walk visits like any
/// other, so <c>Declare</c> runs for it and <c>Markup</c> writes its
/// <c>SymbolInfo</c> — under the symbol that <c>Send(int)</c> in
/// <c>OrdinalPartA.cs</c> already holds. It sits in a second file on purpose: the refusal
/// is on <c>SymbolInfo {symbol}</c>, which is keyed on the symbol alone, so it does not
/// need the two declarations to share a file. <c>Definition {symbol, file}</c> is
/// untouched here, which is what separates this mechanism from the <c>Partial</c>
/// project's.
/// </remarks>
public partial class OrdinalCourier
{
    /// <summary>
    /// The implementing half, whose <c>FindIndex</c> is −1 and whose descriptor is
    /// therefore the bare form that belongs to <c>Send(int)</c>.
    /// </summary>
    /// <param name="text">Named identically to the defining half's parameter.</param>
    public partial void Send(string text)
    {
        this.Length += text.Length;
    }

    /// <summary>How much text has been sent, so the overload is not dead.</summary>
    public int Length { get; private set; }
}

/// <summary>
/// M2, the docId-tie face — the second part, holding the overload the tie is against.
/// </summary>
/// <remarks>
/// Splitting the pair across the two stated <c>&lt;Compile&gt;</c> items is what makes the
/// tie-break observable: with the items in the order A, B the compilation sees
/// <c>Take(T)</c> first; reversed, it sees <c>Take(int)</c> first, and a stable sort over
/// two equal keys returns them in that order. The assertion is that the symbol a use of
/// <c>OrdinalSub&lt;int&gt;.Take</c> names is the same string under both orders.
/// </remarks>
/// <typeparam name="T">The unused parameter of this part's declaration.</typeparam>
public partial class OrdinalSub<T>
{
    /// <summary>The <c>int</c> overload, declared in part B.</summary>
    /// <param name="n">The count to take.</param>
    public int Take(int n) => n;
}
