namespace Surface.Names.Spelling;

/// <summary>M27 — the same types written as their framework names instead.</summary>
/// <remarks>
/// <c>System.Int32</c> is a <c>QualifiedNameSyntax</c> over two <c>IdentifierNameSyntax</c>
/// nodes, and the walk reaches both: <c>System</c> binds to a namespace, which
/// <c>Reference</c> drops before its counter, and <c>Int32</c> binds to the type and
/// writes the row the keyword did not. So one type has three spellings in this project and
/// three different reference counts — nought for <c>int</c>, one for <c>System.Int32</c>,
/// one for <c>var</c> — and only the middle one names what a reader would call the type.
/// </remarks>
public static class NamesFrameworkSpelling
{
    /// <summary>The same body as <c>NamesPredefinedSpelling.Counted</c>.</summary>
    public static System.Int32 Counted()
    {
        System.Int32 total = 0;
        System.Int32 step = 3;

        total += step;

        return total;
    }

    /// <summary>The same body as <c>NamesPredefinedSpelling.Joined</c>.</summary>
    public static System.String Joined()
    {
        System.String first = "a";
        System.String second = "b";

        return first + second;
    }
}
