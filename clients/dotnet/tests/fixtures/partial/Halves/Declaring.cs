namespace Halves;

/// <summary>
/// The declaring halves of a member split <i>across two files</i>, which is what a partial
/// member is normally written for. The implementing halves are in
/// <c>Implementing.cs</c>.
/// </summary>
public partial class Across
{
    /// <summary>Only this half carries a documentation comment, which is the point.</summary>
    public partial void Ping();

    /// <summary>A second overload, so the sibling ordinal is counted and not just looked up.</summary>
    public partial void Ping(int times);

    /// <summary>A partial property — C# 13, and the arm round two widened to.</summary>
    public partial int Count { get; }

    /// <summary>A partial indexer: an escaped name with the ordinal inside it.</summary>
    public partial int this[int slot] { get; }

    /// <summary>Its overload, differing only in parameter type.</summary>
    public partial int this[string name] { get; }

    /// <summary>The control: an ordinary member of the same type, in the same file.</summary>
    public int Whole() => 1;
}
