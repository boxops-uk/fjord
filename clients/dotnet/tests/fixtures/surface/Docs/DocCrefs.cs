// Annex D.3.5 (<exception>), D.3.11 (<permission>), D.3.14 (<see>) and D.3.15 (<seealso>) —
// the four recommended tags that carry a `cref`. A `cref` is the only thing in a
// documentation comment the compiler *resolves*: it binds the attribute's text to a symbol
// and rewrites it in the documentation file as that symbol's ID string, prefixed by its kind.
// So every one of these tags is a reference edge, from the documented declaration to another
// declaration, and the edge's target is spelled in the D.4.2 language rather than in C#.
//
// `DocCrefForms` below writes every form a `cref` can take, and the ID string each one
// produced is recorded beside it. Three of those forms are worth reading twice, because they
// are places where the annex's language and C#'s do not line up:
//
//   * **A constructed generic type cannot be named by a resolved `cref`.** `DocCache{string}`
//     and `DocCache<string>` are both CS1584 — after a cref name, braces are parsed as a
//     type-parameter *declaration* list, so only an identifier fits — and `DocCache{TItem}`
//     resolves to the *unbound* `T:…DocCache`1`. A constructed type reaches the documentation
//     file only as a method cref's parameter (where `<string>` does bind, and becomes
//     `{System.String}`) or as a verbatim prefixed cref nobody resolved.
//   * **An explicit interface implementation cannot be named by a resolved `cref` either.**
//     `DocExplicitSink.IDocSink.Accept(string)` is CS1574. Its ID string exists —
//     `M:….DocExplicitSink.Surface#Docs#Crefs#IDocSink#Accept(System.String)` — and the only
//     way to write it is verbatim, which means the compiler never checks it.
//   * **An unqualified cref to an overload set mints the ID string of the nullary overload.**
//     `<see cref="DocOverloadSet.Emit"/>` warns CS0419 and emits `M:….Emit`, which is
//     character-for-character the ID string `Emit()` declares for itself. That is the hazard
//     of this file, and it is not a name clash in C# at all: it is two different meanings
//     arriving at one string.
//
// A `cref` that resolves to nothing is not an error either — it becomes `!:` plus the
// unresolved text, which is D.4.2's error-string form and the only way to produce it.

using Surface.Docs.Ids;

namespace Surface.Docs.Crefs;

/// <summary>D.3.14 — an interface, so that an explicit implementation has something to name.</summary>
public interface IDocSink
{
    /// <summary>D.3.14 — the member `DocExplicitSink` implements explicitly.</summary>
    /// <param name="text">What to accept.</param>
    void Accept(string text);
}

/// <summary>D.3.14 — a second interface declaring the same member name as the first.</summary>
public interface IDocSecondSink
{
    /// <summary>D.3.14 — same simple name, different declaring interface.</summary>
    /// <param name="text">What to accept.</param>
    void Accept(string text);
}

/// <summary>
/// D.3.14 — a type implementing both interfaces explicitly and declaring an ordinary method
/// of the same name besides. Three members are spelled `Accept` here and their ID strings
/// are three different strings; only the two `#`-substituted ones say which interface they
/// came from.
/// </summary>
public sealed class DocExplicitSink : IDocSink, IDocSecondSink
{
    /// <summary>
    /// D.3.14 — the explicit implementation of <see cref="IDocSink.Accept(System.String)"/>.
    /// ID string: <c>M:Surface.Docs.Crefs.DocExplicitSink.Surface#Docs#Crefs#IDocSink#Accept(System.String)</c>
    /// — the member's name contains dots, and D.4.2 replaces each with <c>#</c>.
    /// </summary>
    /// <param name="text">What to accept.</param>
    void IDocSink.Accept(string text) => Last = "first:" + text;

    /// <summary>
    /// D.3.14 — the explicit implementation of
    /// <see cref="IDocSecondSink.Accept(System.String)"/>, whose ID string differs from the
    /// one above only in the interface name inside the <c>#</c>-separated run.
    /// </summary>
    /// <param name="text">What to accept.</param>
    void IDocSecondSink.Accept(string text) => Last = "second:" + text;

    /// <summary>
    /// D.3.14 — the type's own <c>Accept</c>, ID string <c>M:…DocExplicitSink.Accept(System.String)</c>.
    /// </summary>
    /// <param name="text">What to accept.</param>
    public void Accept(string text) => Last = "own:" + text;

    /// <summary>D.3.14 — which of the three ran last.</summary>
    public string Last { get; private set; } = string.Empty;
}

/// <summary>
/// D.3.14 — an overload set, so that a `cref` can be written both with a signature and
/// without one.
/// </summary>
public sealed class DocOverloadSet
{
    /// <summary>
    /// D.3.14 hazard — the nullary overload. Its own ID string is <c>M:…DocOverloadSet.Emit</c>,
    /// with no parentheses, which is the same string an unqualified cref to the whole set
    /// produces.
    /// </summary>
    /// <returns>Nothing to emit, so zero.</returns>
    public int Emit() => 0;

    /// <summary>D.3.14 — ID string <c>M:…DocOverloadSet.Emit(System.Int32)</c>.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The count back.</returns>
    public int Emit(int count) => count;

    /// <summary>D.3.14 — ID string <c>M:…DocOverloadSet.Emit(System.String)</c>.</summary>
    /// <param name="text">What to emit.</param>
    /// <returns>Its length.</returns>
    public int Emit(string text) => text.Length;

    /// <summary>
    /// D.3.14 — ID string <c>M:…DocOverloadSet.Emit(System.Int32@)</c>: <c>out</c>,
    /// <c>ref</c>, <c>in</c> and <c>ref readonly</c> all encode as a single <c>@</c>.
    /// </summary>
    /// <param name="count">Set to zero.</param>
    /// <returns>Always true.</returns>
    public bool Emit(out int count)
    {
        count = 0;
        return true;
    }
}

/// <summary>D.3.14 — a generic type, to be cref'd bound and unbound.</summary>
/// <typeparam name="TItem">What the cache holds.</typeparam>
public sealed class DocCache<TItem>
{
    /// <summary>D.3.14 — the held item.</summary>
    public TItem Held { get; set; }

    /// <summary>
    /// D.3.14 — a method whose parameter is a constructed generic type, which is the only
    /// way a resolved cref can carry one. ID string:
    /// <c>M:…DocCache`1.Fill(System.Collections.Generic.List{System.Int32})</c>.
    /// </summary>
    /// <param name="counts">The numbers to take a count from.</param>
    /// <returns>How many there were.</returns>
    public int Fill(System.Collections.Generic.List<int> counts) => counts.Count;
}

/// <summary>D.3.14 — a type with operators and a conversion, to be cref'd.</summary>
public readonly struct DocMoney
{
    /// <summary>D.3.14 — the amount in pence.</summary>
    public int Pence { get; }

    /// <summary>D.3.14 — fixes an amount.</summary>
    /// <param name="pence">The amount in pence.</param>
    public DocMoney(int pence) => Pence = pence;

    /// <summary>
    /// D.3.14 — an operator. ID string <c>M:…DocMoney.op_Addition(…DocMoney,…DocMoney)</c>:
    /// the ID string uses the *emitted* name, so `+` never appears in it.
    /// </summary>
    /// <param name="left">One amount.</param>
    /// <param name="right">The other.</param>
    /// <returns>Their sum.</returns>
    public static DocMoney operator +(DocMoney left, DocMoney right) => new(left.Pence + right.Pence);

    /// <summary>D.3.14 — a unary operator, <c>op_UnaryNegation</c>.</summary>
    /// <param name="value">The amount to negate.</param>
    /// <returns>The negated amount.</returns>
    public static DocMoney operator -(DocMoney value) => new(-value.Pence);

    /// <summary>
    /// D.3.14 — a conversion operator. ID string
    /// <c>M:…DocMoney.op_Explicit(…DocMoney)~System.Int32</c>: alone among members, a
    /// conversion's ID string carries its **return type**, after a <c>~</c>.
    /// </summary>
    /// <param name="value">The amount to reduce to a number.</param>
    /// <returns>The amount in pence.</returns>
    public static explicit operator int(DocMoney value) => value.Pence;

    /// <summary>
    /// D.3.14 — the conversion the other way, <c>op_Implicit(System.Int32)~…DocMoney</c>.
    /// Same emitted name family, and the parameter is what separates it.
    /// </summary>
    /// <param name="pence">The amount in pence.</param>
    /// <returns>That amount as money.</returns>
    public static implicit operator DocMoney(int pence) => new(pence);
}

/// <summary>D.3.14 — a type with an indexer, a field, a property and an event to cref.</summary>
public sealed class DocCatalog
{
    /// <summary>D.3.14 — a field, ID string <c>F:…DocCatalog.Slot</c>.</summary>
    public int Slot;

    /// <summary>D.3.14 — a constant, which is also an <c>F:</c>.</summary>
    public const int Limit = 16;

    /// <summary>D.3.14 — a property, ID string <c>P:…DocCatalog.Depth</c>.</summary>
    public int Depth => Slot + 1;

    /// <summary>D.3.14 — an event, ID string <c>E:…DocCatalog.Changed</c>.</summary>
    public event System.Action Changed;

    /// <summary>
    /// D.3.14 — an indexer. ID string <c>P:…DocCatalog.Item(System.Int32)</c>: an indexer is
    /// a property whose ID string carries a parameter list, and whose name is the one
    /// <c>IndexerNameAttribute</c> gives it or <c>Item</c> when it gives none.
    /// </summary>
    /// <param name="index">Which slot.</param>
    /// <returns>The slot's value.</returns>
    public int this[int index] => index + Slot;

    /// <summary>D.3.14 — raises <see cref="Changed"/>, so the event is not merely declared.</summary>
    public void Touch()
    {
        Slot++;
        Changed?.Invoke();
    }
}

/// <summary>
/// D.3.5, D.3.11, D.3.14 and D.3.15 — every `cref` form, on members that do nothing else.
/// The ID string each form produced is written beside it, checked against the <c>Docs.xml</c>
/// this project emits rather than recalled.
/// </summary>
/// <seealso cref="DocCatalog"/>
/// <seealso cref="N:Surface.Docs.Ids"/>
public static class DocCrefForms
{
    /// <summary>
    /// D.3.14 — a cref to a type, an unbound generic type, a nested type and a namespace.
    /// </summary>
    /// <remarks>
    /// <para>A type: <see cref="DocCatalog"/> becomes <c>T:Surface.Docs.Crefs.DocCatalog</c>.</para>
    /// <para>
    /// An unbound generic type: <see cref="DocCache{TItem}"/> becomes
    /// <c>T:Surface.Docs.Crefs.DocCache`1</c> — the braces hold a type-parameter
    /// *declaration*, and the arity is what survives.
    /// </para>
    /// <para>
    /// A framework type: <see cref="System.Collections.Generic.List{T}"/> becomes
    /// <c>T:System.Collections.Generic.List`1</c>, from another assembly.
    /// </para>
    /// <para>
    /// A namespace: <see cref="N:Surface.Docs.Tags"/> is D.4.2's <c>N:</c> prefix, and a cref
    /// is the *only* place it appears — a documentation comment on a namespace declaration is
    /// CS1587 and reaches the file as nothing at all.
    /// </para>
    /// <para>
    /// A namespace, resolved rather than asserted: <see cref="Surface.Docs.Crefs"/> becomes
    /// <c>N:Surface.Docs.Crefs</c> with no prefix written by hand.
    /// </para>
    /// </remarks>
    /// <returns>The number of cref forms in this remark, which is five.</returns>
    public static int Types() => 5;

    /// <summary>D.3.14 — a cref to a constructed generic type, in the three ways it can go.</summary>
    /// <remarks>
    /// <para>
    /// As a method cref's parameter, which binds:
    /// <see cref="DocCache{TItem}.Fill(System.Collections.Generic.List&lt;int&gt;)"/> becomes
    /// <c>M:…DocCache`1.Fill(System.Collections.Generic.List{System.Int32})</c>, and the
    /// constructed type is spelled with braces in the ID string.
    /// </para>
    /// <para>
    /// As a verbatim prefixed cref, which is copied through unresolved:
    /// <see cref="T:System.Collections.Generic.List{System.Int32}"/>. Well formed, never
    /// checked, and pointing at a type that has no declaration anywhere in this corpus.
    /// </para>
    /// <para>
    /// As a bare cref, which cannot be done: <c>DocCache{string}</c> is CS1584 and
    /// <c>DocCache&lt;string&gt;</c> is CS1584 as well. `DocCrefBroken.Unresolved` below
    /// carries the one this project writes on purpose.
    /// </para>
    /// </remarks>
    /// <returns>Two, being the number of forms above that reach the file as a real ID.</returns>
    public static int Constructed() => 2;

    /// <summary>D.3.14 — a cref to a method, with a signature and without one.</summary>
    /// <remarks>
    /// <para>
    /// With an explicit signature: <see cref="DocOverloadSet.Emit(System.Int32)"/> becomes
    /// <c>M:…DocOverloadSet.Emit(System.Int32)</c>, and
    /// <see cref="DocOverloadSet.Emit(System.String)"/> its sibling.
    /// </para>
    /// <para>
    /// With an empty signature: <see cref="DocOverloadSet.Emit()"/> becomes
    /// <c>M:…DocOverloadSet.Emit</c> — the parentheses are dropped, because D.4.2 omits the
    /// argument list when there are no arguments.
    /// </para>
    /// <para>
    /// With a by-reference parameter: <see cref="DocOverloadSet.Emit(out System.Int32)"/>
    /// becomes <c>M:…DocOverloadSet.Emit(System.Int32@)</c>.
    /// </para>
    /// <para>
    /// A generic method: <see cref="DocIdShapes.Map{TResult}(System.Int32)"/> becomes
    /// <c>M:…DocIdShapes.Map``1(System.Int32)</c>, with the double backtick D.4.2 reserves
    /// for a method's own type parameters.
    /// </para>
    /// </remarks>
    /// <returns>Five, the number of method crefs above.</returns>
    public static int Methods() => 5;

    /// <summary>
    /// D.3.14 hazard — a cref to an overload set with no signature at all. The compiler warns
    /// CS0419, picks one overload and writes *its* ID string, which for a set containing a
    /// nullary overload is the string that overload declares for itself.
    /// </summary>
    /// <remarks>
    /// <see cref="DocOverloadSet.Emit"/> — no signature, CS0419, and the emitted target is
    /// <c>M:Surface.Docs.Crefs.DocOverloadSet.Emit</c>. So is
    /// <see cref="DocOverloadSet.Emit()"/>, which names one overload and means something
    /// narrower. Two references, one string, two meanings.
    /// </remarks>
    /// <returns>The number of overloads in the set, which is four.</returns>
    public static int OverloadSet() => 4;

    /// <summary>D.3.14 — a cref to each operator form.</summary>
    /// <remarks>
    /// <para>
    /// By C# operator syntax: <see cref="DocMoney.operator +(DocMoney, DocMoney)"/> becomes
    /// <c>M:…DocMoney.op_Addition(…DocMoney,…DocMoney)</c>, and
    /// <see cref="DocMoney.operator -(DocMoney)"/> becomes <c>op_UnaryNegation</c>.
    /// </para>
    /// <para>
    /// By emitted name: <see cref="DocMoney.op_Addition"/> resolves too, and to exactly the
    /// same ID string as the operator-syntax form above. Two spellings, one edge.
    /// </para>
    /// <para>
    /// A conversion operator: <see cref="DocMoney.explicit operator int(DocMoney)"/> becomes
    /// <c>M:…DocMoney.op_Explicit(…DocMoney)~System.Int32</c>, and
    /// <see cref="DocMoney.implicit operator DocMoney(int)"/> becomes
    /// <c>op_Implicit(System.Int32)~…DocMoney</c>. The <c>~</c> and what follows it is the
    /// return type, which no other member kind's ID string mentions.
    /// </para>
    /// </remarks>
    /// <returns>Five, the number of operator crefs above.</returns>
    public static int Operators() => 5;

    /// <summary>D.3.14 — a cref to an indexer, a field, a constant, a property and an event.</summary>
    /// <remarks>
    /// <para>An indexer: <see cref="DocCatalog.this[System.Int32]"/> becomes
    /// <c>P:…DocCatalog.Item(System.Int32)</c>.</para>
    /// <para>A field: <see cref="DocCatalog.Slot"/> becomes <c>F:…DocCatalog.Slot</c>.</para>
    /// <para>A constant: <see cref="DocCatalog.Limit"/> is an <c>F:</c> as well — D.4.2 has
    /// no prefix for a constant.</para>
    /// <para>A property: <see cref="DocCatalog.Depth"/> becomes <c>P:…DocCatalog.Depth</c>.</para>
    /// <para>An event: <see cref="DocCatalog.Changed"/> becomes <c>E:…DocCatalog.Changed</c>.</para>
    /// <para>A constructor: <see cref="DocMoney.DocMoney(System.Int32)"/> becomes
    /// <c>M:…DocMoney.#ctor(System.Int32)</c>, whose member name is written with a character
    /// no C# identifier may contain.</para>
    /// </remarks>
    /// <returns>Six, the number of member crefs above.</returns>
    public static int Members() => 6;

    /// <summary>
    /// D.3.14 — a cref to an explicit interface implementation, which has no C# spelling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interface member is reachable:
    /// <see cref="IDocSink.Accept(System.String)"/> becomes
    /// <c>M:Surface.Docs.Crefs.IDocSink.Accept(System.String)</c>.
    /// </para>
    /// <para>
    /// The implementation is not. It has an ID string, and the only way to write it is
    /// verbatim, unresolved and unchecked:
    /// <see cref="M:Surface.Docs.Crefs.DocExplicitSink.Surface#Docs#Crefs#IDocSink#Accept(System.String)"/>.
    /// Written as C# — <c>DocExplicitSink.IDocSink.Accept(string)</c> — it is CS1574, so
    /// `DocCrefBroken` carries that case rather than this member.
    /// </para>
    /// <para>
    /// The type's own same-named method resolves normally:
    /// <see cref="DocExplicitSink.Accept(System.String)"/>.
    /// </para>
    /// </remarks>
    /// <returns>Three, the number of `Accept` members `DocExplicitSink` declares.</returns>
    public static int ExplicitImplementations() => 3;
}

/// <summary>
/// D.3.5 and D.3.11 — the two `cref`-carrying tags that are about *contract* rather than
/// about further reading, on a member that honours what they say.
/// </summary>
public sealed class DocContract
{
    private readonly int[] _slots = new int[4];

    /// <summary>
    /// D.3.5 — <c>&lt;exception&gt;</c> names an exception this member can throw, one tag per
    /// exception type, and its `cref` is resolved like any other. Nothing checks that the
    /// member can actually throw it.
    /// </summary>
    /// <param name="index">Which slot to read.</param>
    /// <returns>The slot's value.</returns>
    /// <exception cref="System.IndexOutOfRangeException">
    /// D.3.5 — thrown when <paramref name="index"/> is outside the array, which this member
    /// really does do.
    /// </exception>
    /// <exception cref="System.NotSupportedException">
    /// D.3.5 — documented and never thrown. The compiler does not mind; an index that
    /// records the edge records a reference to a type this member never mentions in its body.
    /// </exception>
    /// <exception cref="DocCatalog">
    /// D.3.5 — a `cref` to a type that is not an exception type at all. Still resolved, still
    /// an edge; D.3.5's rule about what belongs here is not one a compiler enforces.
    /// </exception>
    /// <permission cref="System.Security.SecurityException">
    /// D.3.11 — <c>&lt;permission&gt;</c> documents the access this member requires. Its
    /// `cref` resolves the same way <c>&lt;exception&gt;</c>'s does, so the two tags are
    /// distinguishable only by their element name.
    /// </permission>
    public int Read(int index) => _slots[index];

    /// <summary>
    /// D.3.5, D.3.11, D.3.14, D.3.15 hazard — one cref target, four tags, four source
    /// positions. Every one of these resolves to
    /// <c>T:System.ArgumentOutOfRangeException</c>, so a reference keyed by (documented
    /// member, target) has one row to hold four occurrences, and a reference keyed by source
    /// position has four.
    /// </summary>
    /// <param name="index">Which slot to write.</param>
    /// <param name="value">What to write there.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">D.3.5 — the first occurrence.</exception>
    /// <permission cref="System.ArgumentOutOfRangeException">D.3.11 — the second, in a tag
    /// that is not about throwing at all.</permission>
    /// <remarks>
    /// D.3.14 — the third: <see cref="System.ArgumentOutOfRangeException"/>, inline.
    /// </remarks>
    /// <seealso cref="System.ArgumentOutOfRangeException"/>
    public void Write(int index, int value)
    {
        if (index < 0 || index >= _slots.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index));
        }

        _slots[index] = value;
    }

    /// <summary>
    /// D.3.15 — <c>&lt;seealso&gt;</c> is <c>&lt;see&gt;</c> moved out of the prose: same
    /// `cref`, same resolution, and a position in the documentation that says "further
    /// reading" rather than "this word means that declaration".
    /// </summary>
    /// <returns>How many slots there are.</returns>
    /// <seealso cref="Read(System.Int32)"/>
    /// <seealso cref="Write(System.Int32,System.Int32)"/>
    /// <seealso cref="DocCatalog.this[System.Int32]"/>
    /// <seealso cref="N:Surface.Docs.Graphics"/>
    public int Count() => _slots.Length;
}

/// <summary>
/// D.4.2 — the error-string form. A `cref` that binds to nothing is not an error: the
/// compiler warns CS1574 (or CS1584 when the text is not even cref syntax) and writes
/// <c>!:</c> followed by the text it could not resolve. That is the only way to produce a
/// <c>!:</c> ID string, and every member here produces one.
/// </summary>
public static class DocCrefBroken
{
    /// <summary>
    /// D.4.2 — a name with no declaration. CS1574, and the target reaches the file as
    /// <c>!:DocNoSuchDeclaration</c>.
    /// </summary>
    /// <remarks>D.4.2 — <see cref="DocNoSuchDeclaration"/>, resolved to nothing.</remarks>
    /// <returns>Zero.</returns>
    public static int Unresolved() => 0;

    /// <summary>
    /// D.4.2 — a member that exists, named in a way no cref syntax admits. CS1574, and the
    /// target reaches the file as <c>!:DocExplicitSink.IDocSink.Accept(System.String)</c> —
    /// an unresolved reference to a declaration ten lines away.
    /// </summary>
    /// <remarks>D.4.2 — <see cref="DocExplicitSink.IDocSink.Accept(System.String)"/>.</remarks>
    /// <returns>Zero.</returns>
    public static int UnnameableMember() => 0;
}
