namespace Surface.Names.VarTaken;

/// <summary>
/// M14 — a user type named <c>var</c>, which takes the token back from the language.
/// </summary>
/// <remarks>
/// <para>
/// A type named <c>var</c> is legal C# — the compiler says so out loud, with CS8981, which
/// this project leaves unsuppressed so the build log carries the evidence. What it changes
/// is the meaning of every <c>var</c> in scope: ECMA-334 draft-v9 13.6.2 makes an
/// implicitly typed local declaration conditional on no type named <c>var</c> being in
/// scope, so inside <b>this namespace</b> <c>var</c> is an ordinary type name and
/// inference is gone.
/// </para>
/// <para>
/// <b>Which makes the M14 claim measurable from both sides.</b> In
/// <c>Spelling/NamesInferredSpelling.cs</c> every <c>var</c> is a reference to the
/// inferred type, and the reference nobody wrote is the defect. Here every <c>var</c> is a
/// reference to <see cref="var"/> — the same syntax, the same dispatch, the same row shape
/// — and the row is *correct*. A query that can tell the two files apart is a query that
/// consulted something other than the token, and there is nothing else to consult: the
/// index holds a <c>typeRef</c> over three characters spelled <c>var</c> in both.
/// </para>
/// <para>
/// <b>The negative half is stated and not written</b>, because it does not compile.
/// <c>var n = 3;</c> in this namespace is
/// <c>error CS0029: Cannot implicitly convert type 'int' to
/// 'Surface.Names.VarTaken.var'</c> — the declaration bound <c>var</c> to the class and
/// then failed the conversion, which is the proof that the token was taken rather than
/// shared. It is deliberately absent from this file: a fixture has to compile to be a
/// gate, and the error code is the record.
/// </para>
/// <para>
/// <b>This namespace is a sibling of the inference files, not a parent.</b> A type named
/// <c>var</c> declared in <c>Surface.Names</c> would be in scope in every file of the
/// project and would silently rewrite <c>NamesInferredSpelling.cs</c> into a fifteen-error
/// build. The separation is the fixture's only structural requirement.
/// </para>
/// </remarks>
public sealed class var
{
    /// <summary>How many of these were made, so the type is not merely declared.</summary>
    public int Count { get; set; }

    /// <summary>The type's own name, as a string, for a test that renders it.</summary>
    public override string ToString() => "var";
}

/// <summary>M14 — <c>var</c> used as what it now is: a type name.</summary>
public static class NamesVarAsAType
{
    /// <summary>
    /// Three <c>var</c> tokens, all three references to <c>var</c> and none of them
    /// an inferred declaration.
    /// </summary>
    public static int Use()
    {
        var held = new var { Count = 2 };
        var again = held;

        return again.Count;
    }

    /// <summary>The token in every other type position a declaration offers.</summary>
    /// <param name="taken">A parameter whose type is spelled with the keyword.</param>
    public static var Roundtrip(var taken) => taken;
}
