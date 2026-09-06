using Surface.Modern.MethodGroups;

namespace Surface.Modern.MethodGroups.Inner;

/// <summary>
/// An extension in the *inner* namespace scope, whose only candidate takes a
/// <see cref="string"/>.
/// </summary>
public static class InnerBeaconExtensions
{
    /// <summary>The inner scope's candidate.</summary>
    public static void Mark(this Beacon beacon, string text) => beacon.Trace = text;
}

/// <summary>
/// C# 13 — Method group natural type improvements, the scope-by-scope half. The use site
/// below sees two <c>Mark</c> extensions: one in this namespace and one in the enclosing
/// <c>Surface.Modern.MethodGroups</c>. C# 12 merged them into a group of two, which had no
/// natural type and was an error; C# 13 considers the innermost scope first, finds one
/// candidate, and stops — so the delegate points at
/// <see cref="InnerBeaconExtensions.Mark(Beacon, string)"/> and the outer candidate is never
/// weighed. The reference recorded here is decided entirely by which *namespace* declares it.
/// </summary>
public static class InnerScopePruning
{
    /// <summary>C# 13 — a method group of extensions, converted to <c>var</c>.</summary>
    public static string ByScope()
    {
        var beacon = new Beacon();

        var marker = beacon.Mark;
        marker("inner");

        return beacon.Trace;
    }

    /// <summary>The outer candidate, reached explicitly, so both declarations have a use.</summary>
    public static string OuterByCall()
    {
        var beacon = new Beacon();
        OuterBeaconExtensions.Mark(beacon, 4);

        return beacon.Trace;
    }
}
