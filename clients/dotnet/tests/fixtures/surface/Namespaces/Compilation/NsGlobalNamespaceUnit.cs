// Clause 14.2 (compilation units) and 14.6 (namespace member declarations): a compilation unit
// whose namespace_member_declarations are not wrapped in a namespace declaration at all. Its
// members are members of the *global namespace*, which is the one namespace in a program that
// no declaration declares — there is no `namespace global { }` to point at, and nothing here
// has a containing declaration for a reader or an index to name.
//
// This is the file that makes "a namespace declaration has no location of its own" checkable
// from the other side: the members below have a container, the container has no declaration,
// and so any fact an index holds about that container was synthesised rather than read.
//
// The names are prefixed because the global namespace is the one declaration space the whole
// corpus shares: `NsSurfaceGlobalUnit` is unique across all 22 projects by construction,
// where a plain `Unit` would collide with the first other agent to have the same idea.

/// <summary>14.2: a type whose containing namespace is the global namespace.</summary>
public sealed class NsSurfaceGlobalUnit
{
    /// <summary>The clause this type was written for.</summary>
    public const string Clause = "14.2";

    /// <summary>14.6: a global-namespace type reaching a namespaced one.</summary>
    public static string Describe() =>
        Surface.Namespaces.Compilation.NsCompilationUnitReport.Clause;

    /// <summary>14.6: and reaching the other member of its own compilation unit.</summary>
    public static NsSurfaceGlobalKind Kind() => NsSurfaceGlobalKind.Global;
}

/// <summary>
/// 14.6: a second global-namespace member declared by the same compilation unit, so the global
/// namespace's member list is contributed to twice from one file.
/// </summary>
public enum NsSurfaceGlobalKind
{
    /// <summary>Declared with no namespace declaration above it.</summary>
    Global = 0,

    /// <summary>Declared inside one.</summary>
    Named = 1,
}
