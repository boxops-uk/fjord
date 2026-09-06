// Clause 7.8.1 (namespace and type names, general): the alias qualifier's reason to exist.
// A type declared here is named `System`, so inside this namespace the simple name
// `System` resolves to the type and every framework name below it is unreachable through
// the ordinary spelling. `global::` is the only qualifier that ignores it.
//
// This type has a file and a namespace of its own because a type named `System` shadows
// the namespace for *everything* declared beside it: with it in the same namespace as
// LexQualified.cs's six spellings of `System.String`, three of those six became CS0426.

namespace Surface.Lexical.Names.Shadow;

/// <summary>7.8.1: a type whose name is the first component of a namespace.</summary>
public sealed class System
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 1;

    /// <summary>A nested type, so <c>System.Something</c> resolves here and not out there.</summary>
    public sealed class String
    {
        /// <summary>Something to reference, spelled the way the framework's member is.</summary>
        public const int Empty = 0;
    }
}

/// <summary>
/// 7.8.1: references written from inside the shadow. Each pair is the same source
/// characters resolving to a different symbol depending on the qualifier.
/// </summary>
public static class LexShadowedNameUses
{
    /// <summary>Resolves <c>System</c> to the local type, because it is nearer.</summary>
    public static int LocalSystem() => System.Tag;

    /// <summary>Resolves <c>System.String</c> to the local nested type.</summary>
    public static int LocalString() => System.String.Empty;

    /// <summary>
    /// Resolves the same two identifiers to the framework's namespace and type, which
    /// only <c>global::</c> can reach from here.
    /// </summary>
    public static int FrameworkString() => global::System.String.Empty.Length;

    /// <summary>Reaches a framework type the shadow hides completely otherwise.</summary>
    public static int FrameworkMath() => (int)global::System.Math.Floor(1.5);
}
