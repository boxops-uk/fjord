namespace Ledger.App;

/// <summary>A generic helper, so the index holds a method type parameter.</summary>
public static class Names
{
    public static string Of<T>(T value)
        where T : notnull => value.ToString() ?? string.Empty;
}
