// Annex D.3.9 (<param>), D.3.10 (<paramref>), D.3.17 (<typeparam>) and D.3.18
// (<typeparamref>) — the four tags that reference a declaration *by name* rather than by
// `cref`. They are the only references in the annex the ID string cannot express: a
// parameter and a type parameter have no ID string of their own, so the target of a
// `<param name="…">` is (this member, this name) and the target of a `<typeparamref>` is
// (this member or type, this name). Both halves come from outside the tag.
//
// That is what makes them the annex's naming hazard:
//
//   * A parameter name is unique only within its member. `DocParamTags` declares three
//     overloads of `Store`, each with a parameter named `value`, and each documents it with
//     `<param name="value">`. Three tags, one name, three targets — and the only thing
//     separating them is the member each tag is attached to, which is separated in turn by
//     the ordinal D.4.2's ID string assigns.
//   * A type parameter name is unique only within its declaration, and a method's type
//     parameter may shadow its type's. `DocParamShadow<T>.Where<T>` documents a
//     `<typeparam name="T">` that is not the `T` in the type's own `<typeparam name="T">`,
//     and a `<typeparamref name="T"/>` written inside that method names the inner one. The
//     ID strings do separate them — `` `0 `` for the type's, ``` ``0 ``` for the method's —
//     but the display string "T" does not, and neither does the tag's `name` attribute.
//
// The mistakes are here too, because every one of them is a warning rather than an error and
// so travels into the documentation file intact: a name that is not a parameter (CS1572), a
// name documented twice (CS1571), a parameter left undocumented while its siblings are
// documented (CS1573), and a `<paramref>`/`<typeparamref>` naming nothing (CS1734, CS1735).
// Each is a reference an index can hold and cannot resolve.

namespace Surface.Docs.Params;

/// <summary>
/// D.3.9 and D.3.10 — <c>&lt;param&gt;</c> documents a parameter where it is declared;
/// <c>&lt;paramref&gt;</c> refers to one from inside the prose. The two are the declaration
/// side and the use side of the same edge.
/// </summary>
public sealed class DocParamTags
{
    private int _stored;

    /// <summary>
    /// D.3.9 hazard — the first of three <c>Store</c> overloads, each declaring a parameter
    /// named <c>value</c>. ID string <c>M:…DocParamTags.Store(System.Int32)</c>.
    /// </summary>
    /// <param name="value">
    /// D.3.9 — the number to store. This tag's target is the <c>value</c> of *this* overload;
    /// nothing in the tag says which overload that is.
    /// </param>
    /// <returns>D.3.10 — what <paramref name="value"/> was, before it was stored.</returns>
    public int Store(int value)
    {
        int previous = _stored;
        _stored = value;
        return previous;
    }

    /// <summary>
    /// D.3.9 hazard — the second overload. ID string
    /// <c>M:…DocParamTags.Store(System.String)</c>, and its <c>value</c> is a different
    /// declaration from the one above.
    /// </summary>
    /// <param name="value">D.3.9 — the text whose length to store.</param>
    /// <returns>D.3.10 — the length <paramref name="value"/> contributed.</returns>
    public int Store(string value)
    {
        _stored = value.Length;
        return _stored;
    }

    /// <summary>
    /// D.3.9 hazard — the third overload, whose <c>value</c> is by reference. ID string
    /// <c>M:…DocParamTags.Store(System.Int32@)</c>: the <c>@</c> is all that separates it
    /// from the first overload's, and <c>ref</c>, <c>out</c> and <c>in</c> all produce it.
    /// </summary>
    /// <param name="value">D.3.9 — read, then replaced with what was stored before.</param>
    /// <returns>Always true.</returns>
    public bool Store(ref int value)
    {
        (value, _stored) = (_stored, value);
        return true;
    }

    /// <summary>
    /// D.3.10 — <c>&lt;paramref&gt;</c> used more than once for one parameter, and for two
    /// parameters in one sentence, so that the tag is exercised as a reference rather than as
    /// a label.
    /// </summary>
    /// <param name="first">D.3.9 — the left operand.</param>
    /// <param name="second">D.3.9 — the right operand.</param>
    /// <returns>
    /// D.3.10 — <paramref name="first"/> plus <paramref name="second"/>, unless
    /// <paramref name="second"/> is zero, in which case <paramref name="first"/> alone.
    /// </returns>
    public static int Add(int first, int second) => second == 0 ? first : first + second;

    /// <summary>
    /// D.3.9 — an indexer's parameters are documented with <c>&lt;param&gt;</c> like a
    /// method's, which is the one place a property takes the tag.
    /// </summary>
    /// <param name="index">D.3.9 — which slot.</param>
    /// <returns>D.3.10 — <paramref name="index"/>, stored and returned.</returns>
    /// <value>D.3.19 — the slot's value, which is the index it was asked for.</value>
    public int this[int index] => index + _stored;
}

/// <summary>
/// D.3.17 and D.3.18 — <c>&lt;typeparam&gt;</c> documents a type parameter where it is
/// declared and <c>&lt;typeparamref&gt;</c> refers to one from the prose.
/// </summary>
/// <typeparam name="TItem">
/// D.3.17 — what the box holds. The tag belongs to the type declaration, and the type
/// parameter it names is <c>`0</c> in every ID string this type's members have.
/// </typeparam>
public sealed class DocTypeParamTags<TItem>
{
    /// <summary>D.3.18 — the held <typeparamref name="TItem"/>.</summary>
    public TItem Held { get; set; }

    /// <summary>
    /// D.3.17 — a generic method's own type parameter, documented on the method. ID string
    /// <c>M:…DocTypeParamTags`1.Convert``1(System.Func{`0,``0})</c>: the parameter type
    /// mentions both, and the number of backticks is the only thing that says which is which.
    /// </summary>
    /// <typeparam name="TResult">D.3.17 — what the conversion produces.</typeparam>
    /// <param name="convert">D.3.9 — turns a <typeparamref name="TItem"/> into a
    /// <typeparamref name="TResult"/>.</param>
    /// <returns>
    /// D.3.18 — the held <typeparamref name="TItem"/> as a <typeparamref name="TResult"/>,
    /// or the default when nothing is held.
    /// </returns>
    public TResult Convert<TResult>(System.Func<TItem, TResult> convert) =>
        Held is null ? default : convert(Held);
}

/// <summary>
/// D.3.17 and D.3.18 hazard — a method type parameter that shadows its type's. CS0693 says
/// so and compiles it anyway, and after that the name <c>T</c> means two declarations in one
/// type.
/// </summary>
/// <typeparam name="T">
/// D.3.17 — the type's own <c>T</c>, which is <c>`0</c> in an ID string.
/// </typeparam>
public sealed class DocParamShadow<T>
{
    /// <summary>
    /// D.3.17 — a member using the type's <c>T</c>. ID string
    /// <c>M:…DocParamShadow`1.Keep(`0)</c>.
    /// </summary>
    /// <param name="value">D.3.9 — a <typeparamref name="T"/>, meaning the type's.</param>
    /// <returns>What it was given.</returns>
    public T Keep(T value) => value;

    /// <summary>
    /// D.3.17 hazard — a method whose own type parameter is also called <c>T</c>. ID string
    /// <c>M:…DocParamShadow`1.Where``1(``0)</c>, and the display string of this member and
    /// of <c>Keep</c> are both "T" in the parameter position.
    /// </summary>
    /// <typeparam name="T">
    /// D.3.17 — the *method's* <c>T</c>, which is <c>``0</c>. The <c>name</c> attribute is
    /// the same word as the type's, and only the declaration this tag hangs from separates
    /// the two.
    /// </typeparam>
    /// <param name="value">
    /// D.3.18 — a <typeparamref name="T"/>. Inside this member the name resolves to the
    /// method's type parameter, which shadows the type's; an index that resolves the name
    /// against the containing type instead reaches a different declaration and says nothing
    /// about it.
    /// </param>
    /// <returns>What it was given, unchanged.</returns>
    public T Where<T>(T value) => value;
}

/// <summary>
/// D.3.9, D.3.10, D.3.17 and D.3.18 — the malformed cases. Every one of these is a warning,
/// so every one of them reaches <c>Docs.xml</c> exactly as written and an index has to decide
/// what to do with a reference that binds to nothing.
/// </summary>
public sealed class DocParamMistakes
{
    /// <summary>
    /// D.3.9 — a <c>&lt;param&gt;</c> naming something that is not a parameter (CS1572), one
    /// naming the same parameter twice (CS1571), and a parameter with no tag while its
    /// sibling has one (CS1573). All three at once, and all three copied through.
    /// </summary>
    /// <param name="absent">D.3.9 — there is no parameter called this. CS1572.</param>
    /// <param name="first">D.3.9 — the first tag for this parameter.</param>
    /// <param name="first">D.3.9 — and the second. CS1571.</param>
    /// <returns>The sum, so the member does something.</returns>
    public int Wrong(int first, int second) => first + second;

    /// <summary>
    /// D.3.10 and D.3.18 — a <c>&lt;paramref&gt;</c> and a <c>&lt;typeparamref&gt;</c> that
    /// name nothing at all (CS1734, CS1735). The two ghost references sit in the file beside
    /// the resolved one.
    /// </summary>
    /// <param name="real">D.3.9 — a parameter that exists.</param>
    /// <returns>
    /// D.3.10 — <paramref name="real"/> resolves; <paramref name="ghost"/> does not, and
    /// neither does <typeparamref name="TGhost"/>.
    /// </returns>
    public int Ghosts(int real) => real;
}
