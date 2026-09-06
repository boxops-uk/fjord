namespace Surface.Names.Ambiguity;

/// <summary>
/// M35 — <c>G &lt; A , B &gt; (7)</c>: one run of tokens with two readings, and the parser
/// is what decides which reference the index holds.
/// </summary>
/// <remarks>
/// <para>
/// ECMA-334 draft-v9 6.2.5 resolves this with a lookahead rule rather than with types: a
/// <c>&lt;</c> begins a type argument list if the matching <c>&gt;</c> is followed by one
/// of <c>(</c> <c>)</c> <c>]</c> <c>}</c> <c>:</c> <c>;</c> <c>,</c> <c>.</c> <c>?</c>
/// <c>==</c> <c>!=</c>, and is a less-than operator otherwise. So the shape of the
/// *surrounding punctuation* decides whether the index sees one reference to a generic
/// method with two type arguments, or three references to three ordinary values.
/// </para>
/// <para>
/// <b>Roslyn's answer is the right one and this fixture pins it anyway.</b> Nothing in the
/// walk re-derives it — <c>GetSymbolInfo</c> is asked about whatever node the parser
/// built — so the value here is for any path that does not go through Roslyn: a SCIP
/// converter, a lexical fall-back, a regex-driven reindex of a changed line. Each of those
/// has to reproduce the lookahead or produce a different set of rows for the same bytes.
/// </para>
/// <para>
/// <b>And it pins the span.</b> <c>Reference</c> writes
/// <c>offsets.Span(name.Identifier.Span)</c>, and for a <c>GenericNameSyntax</c> the
/// identifier is the bare name — so the reference row over <c>Pick&lt;Left, Right&gt;</c>
/// covers <c>Pick</c> alone, the angle brackets are in no row, and <c>Left</c> and
/// <c>Right</c> get rows of their own. Three disjoint spans over one expression, none of
/// which contains a <c>&lt;</c>.
/// </para>
/// <para>
/// The two readings cannot share one set of names in one compilation — a name is either a
/// method or an <c>int</c> — so they are written as a matched pair with parallel
/// spellings, and the token *shapes* rather than the token texts are what agree.
/// </para>
/// </remarks>
public static class NamesGenericOrComparison
{
    /// <summary>The first type argument of the generic reading.</summary>
    public sealed class NamesAmbiguityLeft;

    /// <summary>The second type argument of the generic reading.</summary>
    public sealed class NamesAmbiguityRight;

    /// <summary>Takes one <c>int</c>: the generic reading's outer call.</summary>
    public static int Consume(int only) => only;

    /// <summary>Takes two <c>bool</c>s: the comparison reading's outer call.</summary>
    public static int Consume(bool first, bool second) => (first ? 1 : 0) + (second ? 2 : 0);

    /// <summary>Two type parameters and one value parameter.</summary>
    public static int Pick<TFirst, TSecond>(int seed) => seed;

    /// <summary>
    /// The generic reading: <c>&gt;</c> is followed by <c>(</c>, so this is one argument.
    /// </summary>
    /// <remarks>
    /// Reference rows: <c>Consume</c>, <c>Pick</c>, <c>NamesAmbiguityLeft</c>,
    /// <c>NamesAmbiguityRight</c> — four, and the overload chosen is
    /// <c>Consume(int)</c>.
    /// </remarks>
    public static int AsAGenericCall() =>
        Consume(Pick<NamesAmbiguityLeft, NamesAmbiguityRight>(7));

    /// <summary>
    /// The comparison reading: the same token shape with three <c>int</c>s in place of a
    /// method and two types, so <c>&gt;</c> is followed by <c>(</c> and the rule still says
    /// "type argument list" — which is why the operands are *parenthesised* here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Consume(pick &lt; left, right &gt; (7))</c> is the reading the lookahead rule
    /// forbids, so the comparison has to be written with the grouping a reader would infer:
    /// <c>Consume(pick &lt; left, (right &gt; 7))</c>. Two arguments, both <c>bool</c>, and
    /// the overload chosen is <c>Consume(bool, bool)</c>. Reference rows: <c>Consume</c>
    /// and the three locals, and the three locals are file-local so they are
    /// <c>codemarkup.FileLocalXRef</c> rather than <c>SymbolXRef</c>.
    /// </para>
    /// <para>
    /// That the disambiguation is decided by punctuation and not by what the names mean is
    /// the whole content of the clause: this method and <see cref="AsAGenericCall"/> differ
    /// by two parentheses and produce different predicates.
    /// </para>
    /// </remarks>
    public static int AsComparisons()
    {
        var pick = 1;
        var left = 2;
        var right = 8;

        return Consume(pick < left, (right > 7));
    }

    /// <summary>
    /// The third reading the same rule produces: <c>&gt;</c> followed by <c>;</c> is a type
    /// argument list, so this is a method group conversion and not a comparison chain.
    /// </summary>
    public static System.Func<int, int> AsAMethodGroup() =>
        Pick<NamesAmbiguityLeft, NamesAmbiguityRight>;
}
