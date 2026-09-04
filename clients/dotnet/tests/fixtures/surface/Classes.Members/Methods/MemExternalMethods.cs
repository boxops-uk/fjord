// Clause 15.6.8 (external methods). An `extern` method has no body: the modifier says the
// implementation is provided outside the C# source, and the attribute says where. So an index
// holds a declaration with a real signature and no statements under it at all, which is the
// only method shape in the language where "declared here, defined nowhere in the corpus" is
// correct rather than a resolution failure.
//
// The hazard is that the *other* spelling of this clause cannot be written here. The modern
// form, `[LibraryImport]`, requires `static partial` — a partial member, which is quarantine
// shape 3 — and refuses to compile without it (CS8795 and CS0751, plus SYSLIB1050 and
// SYSLIB1062). So the `extern` half of clause 15.6.8 lives in this project and the
// source-generated half belongs to the quarantine that owns partial members.

using System.Runtime.InteropServices;

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.8: three external methods, each a declaration with no body.</summary>
public static class MemExternalMethods
{
    /// <summary>15.6.8: the plain case — a static extern method bound by <c>DllImport</c> to a
    /// C library entry point whose name differs from the C# one.</summary>
    [DllImport("libc", EntryPoint = "abs")]
    public static extern int Magnitude(int value);

    /// <summary>15.6.8: an extern method with a by-reference parameter, so a marshalled
    /// signature carries a parameter modifier.</summary>
    [DllImport("libc", EntryPoint = "labs")]
    public static extern long Magnitude(ref long value);

    /// <summary>15.6.8: an extern method whose return and parameter marshalling is stated by
    /// attributes on the signature rather than by the types.</summary>
    [DllImport("libc", EntryPoint = "getenv", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.LPStr)]
    public static extern string? Environment([MarshalAs(UnmanagedType.LPStr)] string name);

    /// <summary>15.6.8: references to all three. The call sites resolve; the callees have no
    /// bodies to walk into.</summary>
    public static string UseAll()
    {
        var wide = -2L;
        return $"{Magnitude(-1)} {Magnitude(ref wide)} {Environment("PATH") is not null}";
    }
}
