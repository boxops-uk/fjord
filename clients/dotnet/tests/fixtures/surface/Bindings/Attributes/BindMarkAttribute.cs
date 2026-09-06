using System;

namespace Surface.Bindings.Attributes;

/// <summary>
/// M8 — an attribute application's name binds to the chosen <i>constructor</i>, so the
/// attribute class is referenced by nothing anywhere in the corpus.
/// </summary>
/// <remarks>
/// <para>
/// <c>SemanticModel.GetSymbolInfo</c> on an <c>AttributeSyntax</c>'s name answers the
/// constructor overload the application selected — measured: <c>[BindMark]</c> and
/// <c>[BindMark(Topic = "audit")]</c> bind to <c>BindMarkAttribute()</c>,
/// <c>[BindMark("because")]</c> binds to <c>BindMarkAttribute(string)</c>, and the
/// fully-spelled <c>[BindMarkAttribute]</c> binds to <c>BindMarkAttribute()</c> as well.
/// So the walk writes a reference to a <c>.ctor</c>, never to the type, and three separate
/// things follow.
/// </para>
/// <para>
/// <b>The attribute class has no incoming reference.</b> <c>BindMarkAttribute</c> is
/// declared, has a <c>codemarkup.Definition</c>, and its <c>csharp.EntityRef</c> fan-out is
/// empty however many times it is applied. "Where is this attribute used?" is a question
/// the index cannot answer.
/// </para>
/// <para>
/// <b><c>codemarkup.Relation {kind = annotates}</c> is never written.</b> The discriminant
/// exists — <c>annotates = 8</c> in <c>codemarkup.sigla</c> — and <c>Indexer.Relate</c>
/// writes only <c>contains</c>, <c>extends</c>, <c>implements</c> and <c>overrides</c>. So
/// the count of <c>annotates</c> rows over the whole corpus is nought, which is one query
/// and the cheapest half of this mechanism to gate.
/// </para>
/// <para>
/// <b>The text does not match the target.</b> The span carries the four or five characters
/// <c>Mark</c> that a reader sees; the target's <c>codemarkup.Definition.name</c> is
/// <c>.ctor</c>, and the class it belongs to is named <c>BindMarkAttribute</c>. Neither
/// string is a prefix or suffix of the other, so a consumer that sanity-checks a reference
/// by comparing the use's text with the target's name rejects every attribute in the
/// corpus — and the C# attribute-name shortening rule (14.5.4: <c>Mark</c> may stand for
/// <c>MarkAttribute</c>) means the mismatch survives even if the target were the class.
/// </para>
/// <para>
/// <b>The named argument resolves correctly and is keyed wrongly.</b>
/// <c>Topic = "audit"</c> binds to <see cref="BindMarkAttribute.Topic"/> — a property being
/// written — and <c>CodeMarkup.Role</c> tests <c>AttributeSyntax</c> ancestry before it
/// tests for an assignment, so the row is keyed <c>decorator</c> rather than <c>write</c>.
/// Which means every <c>FileXRef {role = decorator}</c> row in the corpus targets a
/// constructor or a term, and none targets a type: the role a UI filters "show me
/// decorators" by cannot reach the decorator.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public sealed class BindMarkAttribute : Attribute
{
    /// <summary>The overload two of the four applications below select.</summary>
    public BindMarkAttribute()
    {
    }

    /// <summary>The overload the positional application selects.</summary>
    /// <param name="reason">Why the thing is marked.</param>
    public BindMarkAttribute(string reason) => Reason = reason;

    /// <summary>Why the thing is marked, when a reason was given.</summary>
    public string? Reason { get; }

    /// <summary>
    /// The named-argument target: a property, written, and keyed <c>decorator</c>.
    /// </summary>
    public string Topic { get; set; } = string.Empty;
}
