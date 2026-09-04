namespace Surface.Names.Spelling;

/// <summary>
/// M27 — the keyword half of a matched pair: every type here is written as a
/// <c>PredefinedTypeSyntax</c>, and not one of them is a reference the index holds.
/// </summary>
/// <remarks>
/// <para>
/// <c>Indexer.IndexTree</c>'s reference arm is <c>case SimpleNameSyntax name</c>.
/// <c>int</c>, <c>string</c>, <c>bool</c>, <c>double</c>, <c>object</c> and <c>void</c>
/// parse as <c>PredefinedTypeSyntax</c>, which is a <c>TypeSyntax</c> and holds a
/// <i>token</i> rather than a name node — so the dispatch never reaches them, no
/// <c>codemarkup.FileXRef</c> is written over the keyword, and no
/// <c>csharp.TypeLocation</c> either. <c>System.Int32</c> is referenced by this file at
/// every position a reader would point at, and by the index at none.
/// </para>
/// <para>
/// <b>This file is the baseline of a two-file experiment.</b>
/// <c>NamesInferredSpelling.cs</c> declares the same members with the same bodies and
/// spells every one of these types <c>var</c>, which is an <c>IdentifierNameSyntax</c>
/// and does reach the dispatch. The reference count over the two files is therefore
/// different, and the *inferred* file — the one that names no type at all — is the larger
/// of the two. Nothing else differs between them, which is what makes the difference a
/// measurement rather than an observation.
/// </para>
/// </remarks>
public static class NamesPredefinedSpelling
{
    /// <summary>A local typed by the keyword. Reference rows over the type: none.</summary>
    public static int Counted()
    {
        int total = 0;
        int step = 3;

        total += step;

        return total;
    }

    /// <summary>The same for a reference type.</summary>
    public static string Joined()
    {
        string first = "a";
        string second = "b";

        return first + second;
    }

    /// <summary>And for a field, a property, a parameter and a return type.</summary>
    public static bool Wider(double weight, object tag)
    {
        bool held = weight > 0.0 && tag is not null;

        return held;
    }
}
