namespace Surface.Names.Keywords;

/// <summary>
/// M14 — <c>nameof</c>: an identifier in invocation position that binds to nothing, and
/// the one construct in this project that makes the walk's <c>Unresolved</c> counter move.
/// </summary>
/// <remarks>
/// <para>
/// <c>nameof(X)</c> parses as an <c>InvocationExpressionSyntax</c> whose <c>Expression</c>
/// is an ordinary <c>IdentifierNameSyntax</c> spelled <c>nameof</c>. The walk reaches it
/// through <c>case SimpleNameSyntax</c>, <c>GetSymbolInfo</c> answers no symbol and no
/// candidate, and <c>Reference</c>'s first branch increments <c>_unresolved</c> — because
/// <c>IsConstraintKeyword</c> excuses only <c>notnull</c> and <c>unmanaged</c>, and only
/// inside a <c>TypeConstraintSyntax</c>. There is nothing to resolve to: <c>nameof</c> is
/// an operator with no declaration in any assembly.
/// </para>
/// <para>
/// <b>So this project's <c>Unresolved</c> is non-zero on purpose</b>, where the
/// <c>ledger</c> fixture asserts a global zero. The number is a property of the source and
/// is stated in this project's README, which is what makes it an assertion rather than a
/// tolerance. Nothing here may be moved into <c>ledger</c>: its frozen counts are pinned
/// by <c>LedgerTests</c> and one <c>nameof</c> would move them.
/// </para>
/// <para>
/// <b>The argument does bind, and that is the asymmetry.</b> <c>nameof(Subject.Weight)</c>
/// writes an ordinary reference row for <c>Subject</c> and one for <c>Weight</c> — a
/// find-references on the property answers this span, correctly — so the construct is half
/// resolved and half not, and the half that is not is the token a reader would say is the
/// whole point of the expression. <c>Invoked</c> is reached too and writes nothing, since
/// the invocation's own symbol info is empty as well.
/// </para>
/// </remarks>
public static class NamesNameOf
{
    /// <summary>Something for the arguments below to name.</summary>
    public sealed class Subject
    {
        /// <summary>A property, named by two of the three <c>nameof</c>s here.</summary>
        public int Weight { get; init; }
    }

    /// <summary>The bare form: one unresolved <c>nameof</c>, one resolved argument.</summary>
    public static string OfAMember() => nameof(Subject.Weight);

    /// <summary>The type form: one unresolved <c>nameof</c>, one resolved argument.</summary>
    public static string OfAType() => nameof(Subject);

    /// <summary>
    /// The nested form, and the only place a second <c>nameof</c> would be counted twice.
    /// </summary>
    /// <remarks>
    /// One <c>nameof</c> token, so one increment. A reader counting "three unresolved names
    /// in this file" is counting tokens spelled <c>nameof</c>, which is the same number.
    /// </remarks>
    public static string OfALocal()
    {
        var held = new Subject { Weight = 1 };

        return nameof(held) + held.Weight;
    }
}
