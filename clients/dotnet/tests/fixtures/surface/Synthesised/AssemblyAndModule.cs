using System.Reflection;
using System.Runtime.CompilerServices;

// SymbolKind.Assembly and SymbolKind.NetModule (22.3): the two symbols every compilation
// has and no source declares. An attribute target is the only way C# names either of
// them, so these two lines are the whole of this project's evidence for both kinds — the
// assembly and the module are containers an index has to invent a row for, from the
// project rather than from a file, and the `[assembly:]`/`[module:]` targets are the only
// place a file mentions them at all.
[assembly: AssemblyMetadata("Surface.Slice", "synthesised members and compiler enumerations")]
[assembly: InternalsVisibleTo("Surface.Synthesised.NotBuilt")]
[module: SkipLocalsInit]

namespace Surface.Synthesised;

/// <summary>Somewhere for the file's attributes to sit above.</summary>
/// <remarks>
/// The assembly-level and module-level attribute sections above this namespace are
/// syntactically part of the same compilation unit as this type but belong to neither it
/// nor the namespace: their target is the enclosing assembly and module, which have no
/// declaration in any file. <c>SkipLocalsInit</c> also needs the project's
/// <c>AllowUnsafeBlocks</c>, so an attribute application here depends on a build property.
/// </remarks>
public sealed class SynAssemblyHome
{
    /// <summary>Reads back one of the attributes applied above, at runtime.</summary>
    /// <returns>The metadata value, if the attribute survived.</returns>
    public static string? Metadata()
    {
        foreach (var attribute in typeof(SynAssemblyHome).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key == "Surface.Slice")
            {
                return attribute.Value;
            }
        }

        return null;
    }
}
