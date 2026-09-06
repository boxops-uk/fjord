// Clause 14.4 (extern alias directives). An extern alias directive introduces an identifier
// that serves as an alias for a namespace hierarchy the compiler was handed under that name —
// not for a namespace declared in the program, and not for a type. It has no target inside the
// source at all: `extern alias NsPing;` is a declaration whose meaning is supplied by the
// build, and nothing in the file says what it means.
//
// The corpus may reference no package and no sibling project, so this alias is put on one
// assembly of the framework reference — see the target in Namespaces.csproj. Two consequences,
// and both are the point:
//
//   * `NsPing::System.Net.NetworkInformation.Ping` resolves, in this file and in the four
//     other compilation units that declare the alias.
//   * plain `System.Net.NetworkInformation.Ping` does *not* resolve anywhere in this project.
//     Giving a reference an alias removes it from the global alias, so the negative half of
//     14.4 is checkable: `NsExternAliasNegative` records the error, because the code that
//     would produce it cannot be written here.
//
// The hazard is the alias's declaration count. `NsPing` is declared six times in this project
// — at compilation-unit level in NsCompilationUnit.cs, NsUsingForms.cs,
// NsQualifiedAliasMember.cs, NsAliasUniqueness.cs and this file, and once more inside a
// namespace body in NsAliasUniqueness.cs — and every one of them means the same assembly. Six
// declarations, one meaning, and no location for the thing they name.

extern alias NsPing;

namespace Surface.Namespaces.Extern;

/// <summary>14.4: what the extern alias reaches.</summary>
public static class NsExternAliasHost
{
    /// <summary>14.4: a type from the aliased assembly, named through the alias.</summary>
    public static string AliasedType() => typeof(NsPing::System.Net.NetworkInformation.Ping).Name;

    /// <summary>14.4: a second type from the same assembly, so the alias is not one-shot.</summary>
    public static string AliasedSibling() =>
        typeof(NsPing::System.Net.NetworkInformation.PingReply).Name;

    /// <summary>
    /// 14.4: the aliased assembly's identity, read at run time. This is the only place in the
    /// project that names the assembly the alias stands for, and it names it as data.
    /// </summary>
    public static string AliasedAssemblyName() =>
        typeof(NsPing::System.Net.NetworkInformation.Ping).Assembly.GetName().Name ?? string.Empty;

    /// <summary>
    /// 14.4: an instance, so the aliased assembly is used and not merely mentioned.
    /// `PingOptions` needs no network to construct.
    /// </summary>
    public static int AliasedInstanceTtl() =>
        new NsPing::System.Net.NetworkInformation.PingOptions().Ttl;
}

/// <summary>
/// 14.4: the negative half, recorded rather than compiled. With `System.Net.Ping` aliased, the
/// unaliased spelling is CS1069 in every file of this project, and so no reference row in the
/// corpus resolves `System.Net.NetworkInformation.Ping` without an alias in front of it.
/// </summary>
public static class NsExternAliasNegative
{
    /// <summary>The error the unaliased spelling produces here.</summary>
    public const string ExpectedError =
        "CS1069: The type name 'Ping' could not be found in the namespace " +
        "'System.Net.NetworkInformation'. This type has been forwarded to assembly " +
        "'System.Net.Ping, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'";

    // Deliberately not written, and this line is the reason the class exists:
    //     public static string Unaliased() => typeof(System.Net.NetworkInformation.Ping).Name;
}
