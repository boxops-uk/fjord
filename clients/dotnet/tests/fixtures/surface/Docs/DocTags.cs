// Annex D.3.1 through D.3.19 — the recommended tags that carry no `cref` and name no
// declaration. Each of these is *content*: the compiler copies it into the documentation file
// without looking inside it, so the only fact an index holds about them is the documentation
// text attached to the symbol. That makes them the quiet half of the annex, and the half a
// hover has to render.
//
// D.3.1's own rule is that the list is a recommendation, not a closed set: an implementation
// must accept the tags the annex names *and* any others, so a tag nobody has heard of travels
// into the documentation file unchanged. `DocCustomTag` below is that case.
//
// The tags that do name a declaration — <see>, <seealso>, <exception>, <permission>, <param>,
// <paramref>, <typeparam> and <typeparamref> — are in DocCrefs.cs and DocParams.cs, because
// what an index holds about them is a reference edge rather than a string.

namespace Surface.Docs.Tags;

/// <summary>
/// D.3.16 — <c>&lt;summary&gt;</c>: a short description of a type or member, and the one tag
/// every declaration in this project carries. D.3.2 — <c>&lt;c&gt;</c> marks a fragment of
/// text as code, as <c>DocTagged</c> is marked here.
/// </summary>
/// <remarks>
/// <para>
/// D.3.12 — <c>&lt;remarks&gt;</c>: the long description, where the detail a summary cannot
/// hold goes. D.3.8 — <c>&lt;para&gt;</c> divides it into paragraphs, and this is the first.
/// </para>
/// <para>
/// D.3.8 — the second paragraph, so that the tag is exercised as a divider rather than as a
/// wrapper. A single <c>&lt;para&gt;</c> would be indistinguishable from none.
/// </para>
/// </remarks>
public sealed class DocTagged
{
    /// <summary>D.3.16 — a field's summary.</summary>
    private int _weight;

    /// <summary>
    /// D.3.19 — <c>&lt;value&gt;</c> describes what a property *holds*, which is a different
    /// question from what its accessors do; the annex gives it its own tag for that reason.
    /// </summary>
    /// <value>
    /// D.3.19 — the weight, in whole units. Never negative: the setter clamps at zero rather
    /// than rejecting, so the value is a fact about the property and not about the caller.
    /// </value>
    public int Weight
    {
        get => _weight;
        set => _weight = value < 0 ? 0 : value;
    }

    /// <summary>
    /// D.3.13 — <c>&lt;returns&gt;</c> describes the return value, and is the one tag whose
    /// absence on a non-void member is visible: there is nowhere else for that sentence.
    /// </summary>
    /// <returns>
    /// D.3.13 — the weight as a string, with no unit suffix.
    /// </returns>
    public string Describe() => _weight.ToString();

    /// <summary>
    /// D.3.3 — <c>&lt;code&gt;</c> marks multiple lines as code, where D.3.2's
    /// <c>&lt;c&gt;</c> marks a fragment inside a line. D.3.4 — <c>&lt;example&gt;</c> is the
    /// tag that says what the code is *for*.
    /// </summary>
    /// <example>
    /// D.3.4 — how to weigh something and read the answer back:
    /// <code>
    /// DocTagged tagged = new DocTagged();
    /// tagged.Weight = 12;
    /// string shown = tagged.Describe();   // "12"
    /// </code>
    /// The example is prose plus code; the two tags nest that way round on purpose, because
    /// <c>&lt;code&gt;</c> holds no explanation of its own.
    /// </example>
    /// <returns>D.3.13 — the weight the example set, so the example can be checked.</returns>
    public int Reweigh()
    {
        Weight = 12;
        return Weight;
    }

    /// <summary>D.3.7 — <c>&lt;list&gt;</c>, in the three forms the annex names.</summary>
    /// <remarks>
    /// <para>D.3.7 — the bulleted form, whose items have descriptions and no terms:</para>
    /// <list type="bullet">
    ///   <item><description>A summary is required of every public member here.</description></item>
    ///   <item><description>A remark is not.</description></item>
    /// </list>
    /// <para>D.3.7 — the numbered form, where the order is the content:</para>
    /// <list type="number">
    ///   <item><description>The compiler parses the comment.</description></item>
    ///   <item><description>It resolves each <c>cref</c>.</description></item>
    ///   <item><description>It writes an ID string and the comment's body to the file.</description></item>
    /// </list>
    /// <para>D.3.7 — the two-column table form, with a header row:</para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Prefix</term>
    ///     <description>What it introduces</description>
    ///   </listheader>
    ///   <item>
    ///     <term>T:</term>
    ///     <description>A type — class, interface, struct, enum or delegate.</description>
    ///   </item>
    ///   <item>
    ///     <term>M:</term>
    ///     <description>A method, including constructors, finalizers and operators.</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <returns>D.3.7 — the number of list forms above, which is three.</returns>
    public int ListForms() => 3;
}

/// <summary>
/// D.3.1 — the recommended tags are a recommendation. A tag the annex never names is accepted
/// and copied into the documentation file verbatim, so the documentation of this type contains
/// an element no consumer is required to understand.
/// </summary>
/// <threadsafety static="true" instance="false">
/// D.3.1 — <c>&lt;threadsafety&gt;</c> is not in the annex's list. The compiler neither
/// validates its attributes nor complains about it; an index either carries it through with
/// the rest of the documentation or drops it, and which one is a fact worth asking for.
/// </threadsafety>
/// <invariant>D.3.1 — and a second unknown tag, with no attributes at all.</invariant>
public static class DocCustomTag
{
    /// <summary>D.3.1 — a member whose documentation is entirely unrecognized tags.</summary>
    /// <complexity>Constant.</complexity>
    /// <returns>D.3.13 — zero, which is the only recognized tag on this member.</returns>
    public static int Nothing() => 0;
}
