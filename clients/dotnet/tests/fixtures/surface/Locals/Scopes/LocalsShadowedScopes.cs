namespace Surface.Locals.Scopes;

/// <summary>
/// M16 — <c>csharp.Local</c>'s key has no scope segment, and the predicate is empty, so
/// this file pins the count the key <i>would</i> collapse rather than a count it does.
/// </summary>
/// <remarks>
/// <para>
/// The declared key is <c>{name, type, containingMethod, refKind, isConst}</c>. A method
/// body can declare any number of locals of one name in disjoint scopes, and every field
/// of that key agrees across all of them — so a filled predicate would answer "one local"
/// where the source declares four, and <c>csharp.MemberAccessExpression</c>'s
/// <c>local</c> alternative would point at it.
/// </para>
/// <para>
/// <b>It is latent, not live.</b> <c>CsharpEntities.Build</c> has no <c>ILocalSymbol</c>
/// arm, so <c>Entity</c> answers null for a local, <c>Definition</c> answers null, and
/// nothing is written: <c>PredicateCensusTests</c> records <c>Local</c> as
/// <c>Fill.Excused</c>. A measured run over this project writes <b>zero</b>
/// <c>csharp.Local</c> rows. The corpus's job is to make filling the predicate a decision:
/// the numbers below are what a correct producer must answer and what a producer keyed as
/// declared would answer, and they differ.
/// </para>
/// <para>
/// <b>The span-keyed layer beside it is right, on the same locals.</b> Every use writes a
/// <c>codemarkup.FileLocalXRef {file, use, declaration}</c> whose declaration span comes
/// from <c>Indexer.Declared</c>, which walks <c>symbol.Locations</c> — per symbol, not per
/// key. So in <see cref="Disjoint"/> the four uses of <c>step</c> point at four different
/// declaration spans, correctly, in the same run in which the entity layer would hold one
/// row for all four. One shape, two layers, and only the keyed one is wrong: that is the
/// comparison this file exists to make.
/// </para>
/// <para>
/// <b>The counts.</b> <see cref="Disjoint"/> declares 5 locals reaching 2 keys.
/// <see cref="Kinds"/> declares 4 locals reaching 4 keys — it changes one key field per
/// local, so it is the control. <see cref="Retyped"/> declares 2 locals of one name
/// reaching 2 keys, because <c>type</c> is in the key. Eleven locals, eight keys, and a
/// producer that reports eleven has a scope segment.
/// </para>
/// </remarks>
public static class LocalsShadowedScopes
{
    /// <summary>
    /// Five locals, two keys: <c>total</c>, and <c>step</c> declared four times in four
    /// disjoint scopes with the same name, type, containing method, ref kind and constness.
    /// </summary>
    public static int Disjoint()
    {
        var total = 0;

        {
            int step = 1;

            total += step;
        }

        {
            int step = 2;

            total += step;
        }

        for (int step = 3; step < 4; step++)
        {
            total += step;
        }

        foreach (int step in new[] { 5 })
        {
            total += step;
        }

        return total;
    }

    /// <summary>
    /// The control: four locals, four keys, one key field different in each.
    /// </summary>
    /// <param name="seed">Something to alias, so the <c>ref</c> local has a referent.</param>
    public static long Kinds(int seed)
    {
        int slot = seed;
        const int step = 1;
        ref int held = ref slot;
        long slotWide = held + step;

        held = slot + 1;

        return slotWide + held;
    }

    /// <summary>
    /// Two locals of one name in two disjoint scopes, separated by <c>type</c> alone.
    /// </summary>
    public static string Retyped()
    {
        string joined;

        {
            string step = "a";

            joined = step;
        }

        {
            int step = 2;

            joined += step;
        }

        return joined;
    }

    /// <summary>Calls all three, so nothing here is dead code.</summary>
    public static string Use() => Disjoint() + Kinds(1) + Retyped();
}
