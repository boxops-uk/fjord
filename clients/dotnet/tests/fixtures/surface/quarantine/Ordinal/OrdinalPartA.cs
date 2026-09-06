namespace Surface.Quarantine.Ordinal;

/// <summary>
/// M2, declaration face — the first part: an ordinary overload beside the defining half of
/// a partial one. The run-killer this project is quarantined for.
/// </summary>
/// <remarks>
/// <para>
/// Clauses 15.6.9 (partial methods), 7.3 (declarations) and 12.5.1: <c>Send(int)</c> and
/// <c>Send(string)</c> are two members of one overload set, and one of them happens to be
/// partial. Nothing about this is unusual C#.
/// </para>
/// <para>
/// <c>ScipSymbols.Disambiguator</c> takes <c>ContainingType.GetMembers()</c>, filters it
/// to methods of the same name, sorts by documentation id, <c>FindIndex</c>es the symbol
/// being described, and returns <c>index &lt;= 0 ? "" : "+N"</c>. Two facts about that:
/// </para>
/// <para>
/// 1. <c>GetMembers()</c> returns only the <i>defining</i> part of a partial method, so
/// <c>FindIndex</c> on the implementing part in <c>OrdinalPartB.cs</c> answers −1.
/// </para>
/// <para>
/// 2. The guard is <c>index &lt;= 0</c>, not <c>index &lt; 0 ? throw : …</c>, so it spells
/// that miss as <i>the first overload</i>.
/// </para>
/// <para>
/// The documentation ids sort <c>M:….Send(System.Int32)</c> before
/// <c>M:….Send(System.String)</c>, so ordinal zero belongs to <c>Send(int)</c>. The
/// implementing half of <c>Send(string)</c> takes the bare form too, and two unrelated
/// overloads become one symbol — while the defining half, which <i>is</i> in the member
/// list, correctly spells <c>Send(+1).</c>. So the two halves of one member disagree with
/// each other <i>and</i> one of them agrees with a method it has nothing to do with.
/// </para>
/// <para>
/// <b>Why the run dies.</b> <c>SymbolInfo</c> is keyed <c>{symbol}</c> alone and receives
/// <c>void OrdinalCourier.Send(int count)</c> from this file and
/// <c>void OrdinalCourier.Send(string text)</c> from the other: two values, one key, and
/// the write stream fails. The halves name their parameters identically on purpose — the
/// display strings must differ because the two <i>methods</i> differ, not because
/// clause 15.6.9 let the names drift, which is the <c>Partial</c> project's mechanism.
/// </para>
/// <para>
/// <b>And where it would otherwise survive.</b> If the identity collision merged instead
/// of refusing, <c>Send(1)</c> would bind to a symbol whose definition rows include
/// <c>Send(string)</c>'s body — go-to-definition on an <c>int</c> call landing in the
/// <c>string</c> overload.
/// </para>
/// </remarks>
public partial class OrdinalCourier
{
    /// <summary>The non-partial overload, which owns ordinal zero by documentation id.</summary>
    /// <param name="count">The count to send.</param>
    public void Send(int count)
    {
        this.Sent += count;
    }

    /// <summary>
    /// The defining half of the partial overload, which sorts second and so spells
    /// <c>Send(+1).</c> correctly.
    /// </summary>
    /// <param name="text">Named identically in both halves, deliberately.</param>
    public partial void Send(string text);

    /// <summary>How much has been sent, so the overload is not dead.</summary>
    public int Sent { get; private set; }
}

/// <summary>
/// M2, the docId-tie face — a generic type whose two overloads sort <i>equal</i> once its
/// containing type is constructed.
/// </summary>
/// <remarks>
/// <para>
/// Clause 8.4.4 and 12.6.4.4: <c>Take(T)</c> and <c>Take(int)</c> are a legal overload
/// pair, and a call on <c>OrdinalSub&lt;int&gt;</c> resolves to <c>Take(int)</c> because a
/// non-type-parameter parameter type is more specific than a type parameter.
/// </para>
/// <para>
/// The <i>declaration</i> ordinals here are stable: on the open type the documentation ids
/// are <c>M:….OrdinalSub`1.Take(`0)</c> and <c>M:….OrdinalSub`1.Take(System.Int32)</c>,
/// which differ. What is not stable is the ordinal a <i>use</i> through
/// <c>OrdinalSub&lt;int&gt;</c> gets: for the constructed type both members have the
/// byte-identical documentation id <c>M:….OrdinalSub{System.Int32}.Take(System.Int32)</c>,
/// the sort ties, and <c>OrderBy</c>'s documented stability hands the answer back to the
/// order in which the compilation received its trees. Stable for one build, and a property
/// of the <c>&lt;Compile&gt;</c> item order rather than of the code — which is why this
/// project is indexed twice with <c>OrdinalPartA.cs</c> and <c>OrdinalPartB.cs</c>
/// swapped.
/// </para>
/// <para>
/// <c>ScipSymbolsTests.A_partial_classs_overloads_do_not_depend_on_file_order</c> gates
/// only the non-generic case, where the ids differ and the sort cannot tie.
/// </para>
/// </remarks>
/// <typeparam name="T">Substituted with <c>int</c> at the use site, which is what ties the sort.</typeparam>
public partial class OrdinalSub<T>
{
    /// <summary>The type-parameter overload, declared in part A.</summary>
    /// <param name="item">The item to take.</param>
    public int Take(T item) => item is null ? 0 : 1;
}
