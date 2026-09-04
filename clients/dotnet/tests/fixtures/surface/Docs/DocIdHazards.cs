// Annex D.4.2 and D.4.3, written as the shapes that make two declarations want one string.
// Everything in this file compiles and every ID string in it is distinct — checked against
// the emitted `Docs.xml`, not recalled — but each shape is a place where something *smaller*
// than the ID string is the same for two members, so an identity built from anything less
// than the whole ID string merges them.
//
//   * `DocConvertible` — four conversion operators, in two pairs. `op_Implicit(DocConvertible)`
//     appears twice and `op_Explicit(DocConvertible)` twice; within each pair the containing
//     type, the member name and the entire argument list are identical, and the `~` return
//     type is the only difference. A conversion operator is the *only* member in C# that can
//     do this: CS0111 stops every other same-signature pair, and CS0557 stops only a pair
//     with the same source *and* target. So this is the shape a descriptor of
//     (type, name, argument types) cannot express, and the ordinal a doc-ID sort assigns is
//     the only thing left to separate them by.
//   * `DocSignal` — a field-like event, which declares two members from one line: the event
//     `E:…DocSignal.Fired` and a private backing field the compiler names `Fired` too. The
//     documentation file holds only the event, so the field is a declaration with no ID
//     string at all and the annex offers nothing to tell them apart with. Writing the field
//     by hand beside the event is CS0102, so this is the only way the pair exists.
//   * `DocIdOrdinals` — four overloads whose source order is the exact reverse of their
//     doc-ID order. If the ordinal is assigned by sorting ID strings, `Rank()` is ordinal 0
//     and it is written last.
//   * `DocIndexerNamed` — an indexer whose ID string is not `Item`, because
//     `IndexerNameAttribute` renamed it. An index that assumes `Item` looks up a string
//     nothing minted.

using System.Runtime.CompilerServices;

namespace Surface.Docs.Ids;

/// <summary>
/// D.4.2 hazard — the <c>~</c> return type, which exists in the format because conversion
/// operators need it and no other member kind does.
/// </summary>
public readonly struct DocConvertible
{
    /// <summary>D.4.2 — the amount being converted.</summary>
    public int Amount { get; }

    /// <summary>D.4.2 — fixes an amount.</summary>
    /// <param name="amount">The amount.</param>
    public DocConvertible(int amount) => Amount = amount;

    /// <summary>
    /// D.4.2 hazard — <c>M:Surface.Docs.Ids.DocConvertible.op_Implicit(Surface.Docs.Ids.DocConvertible)~System.Int32</c>.
    /// </summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The amount, as an <c>int</c>.</returns>
    public static implicit operator int(DocConvertible value) => value.Amount;

    /// <summary>
    /// D.4.2 hazard — <c>M:Surface.Docs.Ids.DocConvertible.op_Implicit(Surface.Docs.Ids.DocConvertible)~System.Int64</c>.
    /// The same member name and the same argument list as the one above; everything before
    /// the <c>~</c> is character-for-character identical.
    /// </summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The amount, as a <c>long</c>.</returns>
    public static implicit operator long(DocConvertible value) => value.Amount;

    /// <summary>
    /// D.4.2 hazard — <c>M:…DocConvertible.op_Explicit(Surface.Docs.Ids.DocConvertible)~System.Int16</c>,
    /// the first of the second pair.
    /// </summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The amount, narrowed.</returns>
    public static explicit operator short(DocConvertible value) => (short)value.Amount;

    /// <summary>
    /// D.4.2 hazard — <c>M:…DocConvertible.op_Explicit(Surface.Docs.Ids.DocConvertible)~System.Byte</c>.
    /// </summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The amount, narrowed further.</returns>
    public static explicit operator byte(DocConvertible value) => (byte)value.Amount;

    /// <summary>
    /// D.4.2 — the conversion in the other direction:
    /// <c>M:…DocConvertible.op_Implicit(System.Int32)~Surface.Docs.Ids.DocConvertible</c>.
    /// Same member name as the two implicit conversions above, and here the argument list is
    /// what separates it — which is the ordinary case, and the reason the pairs above are
    /// remarkable.
    /// </summary>
    /// <param name="amount">The amount to wrap.</param>
    /// <returns>That amount, wrapped.</returns>
    public static implicit operator DocConvertible(int amount) => new(amount);

    /// <summary>
    /// D.4.2 — a <c>checked</c> conversion, which is a *fifth* member and the only one whose
    /// emitted name differs: <c>op_CheckedExplicit</c>. Its ID string is
    /// <c>M:…DocConvertible.op_CheckedExplicit(Surface.Docs.Ids.DocConvertible)~System.Int16</c>,
    /// which differs from the <c>short</c> conversion above in the member name alone.
    /// </summary>
    /// <param name="value">What to convert.</param>
    /// <returns>The amount, narrowed, throwing on overflow.</returns>
    public static explicit operator checked short(DocConvertible value) => checked((short)value.Amount);
}

/// <summary>
/// D.4.2 hazard — a field-like event. One line of source declares an event and a field of
/// the same name, and only one of the two has an ID string.
/// </summary>
public sealed class DocSignal
{
    /// <summary>
    /// D.4.2 hazard — <c>E:Surface.Docs.Ids.DocSignal.Fired</c>. The compiler also mints a
    /// private field called <c>Fired</c> to hold the delegate; that field is a declaration
    /// with a name, a type and a location, and the documentation file has no entry for it.
    /// Declaring <c>private System.Action Fired;</c> here as well is CS0102.
    /// </summary>
    public event System.Action Fired;

    /// <summary>
    /// D.4.2 — an event with explicit accessors, which mints *no* field. Its ID string,
    /// <c>E:Surface.Docs.Ids.DocSignal.Watched</c>, is the same shape as
    /// <see cref="Fired"/>'s, so the two forms are indistinguishable in the file even though
    /// one of them brought a second declaration with it.
    /// </summary>
    public event System.Action Watched
    {
        add => Fired += value;
        remove => Fired -= value;
    }

    /// <summary>D.4.2 — raises the field-like event, so the backing field is read.</summary>
    /// <returns>Whether anything was listening.</returns>
    public bool Raise()
    {
        System.Action handler = Fired;
        handler?.Invoke();
        return handler is not null;
    }
}

/// <summary>
/// D.4.2 hazard — four overloads whose source order is the reverse of the order their ID
/// strings sort in. Written this way round on purpose: if the overload ordinal is assigned by
/// sorting documentation identifiers, then the ordinals here run 3, 2, 1, 0 down the file,
/// and an ordinal assigned by source position would run 0, 1, 2, 3 and be wrong about every
/// one of them.
/// </summary>
public sealed class DocIdOrdinals
{
    /// <summary>
    /// D.4.2 — <c>M:Surface.Docs.Ids.DocIdOrdinals.Rank(System.String)</c>, which sorts last
    /// of the four and is declared first.
    /// </summary>
    /// <param name="text">What to rank.</param>
    /// <returns>Its length.</returns>
    public int Rank(string text) => text.Length;

    /// <summary>
    /// D.4.2 — <c>M:Surface.Docs.Ids.DocIdOrdinals.Rank(System.Int32)</c>, third of four.
    /// </summary>
    /// <param name="count">What to rank.</param>
    /// <returns>The count.</returns>
    public int Rank(int count) => count;

    /// <summary>
    /// D.4.2 — <c>M:Surface.Docs.Ids.DocIdOrdinals.Rank(System.Boolean)</c>, second of four.
    /// </summary>
    /// <param name="flag">What to rank.</param>
    /// <returns>One or zero.</returns>
    public int Rank(bool flag) => flag ? 1 : 0;

    /// <summary>
    /// D.4.2 — <c>M:Surface.Docs.Ids.DocIdOrdinals.Rank</c>, with no argument list at all,
    /// which sorts first of the four because it is a prefix of the other three.
    /// </summary>
    /// <returns>Zero.</returns>
    public int Rank() => 0;
}

/// <summary>
/// D.4.2 hazard — an indexer's member name is whatever <c>IndexerNameAttribute</c> says, and
/// <c>Item</c> only when nothing says anything. This one is <c>Cell</c>, so its ID string is
/// <c>P:Surface.Docs.Ids.DocIndexerNamed.Cell(System.Int32)</c> and a lookup for
/// <c>…DocIndexerNamed.Item(System.Int32)</c> finds nothing.
/// </summary>
public sealed class DocIndexerNamed
{
    private readonly int[] _cells = new int[8];

    /// <summary>D.4.2 hazard — the renamed indexer.</summary>
    /// <param name="index">Which cell.</param>
    /// <returns>The cell's value.</returns>
    /// <value>D.3.19 — what the cell holds, which starts at zero.</value>
    [IndexerName("Cell")]
    public int this[int index]
    {
        get => _cells[index];
        set => _cells[index] = value;
    }

    /// <summary>
    /// D.4.2 — a property called <c>Item</c>, which is the name the indexer would have had.
    /// Its ID string is <c>P:…DocIndexerNamed.Item</c> — no argument list — so the two do not
    /// collide, and would have if the attribute were absent (CS0102).
    /// </summary>
    public int Item => _cells.Length;
}
