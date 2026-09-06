// Clause 14.2 (compilation units): a compilation unit is
//
//     extern_alias_directives? using_directives? global_attributes? namespace_member_declarations?
//
// in that order and in no other. Every one of the four sections is present in this file, so
// the whole production can be read off one page, and a section moved out of order is a
// compile error rather than a matter of taste.
//
// The hazard is the global attributes. A global_attribute_target is `assembly` or `module`,
// and both are here: neither the assembly nor the module is declared by any file, so these are
// attribute usages whose container no compilation unit contains. The two assembly ones carry
// the same attribute type, which is legal only because AssemblyMetadataAttribute allows
// multiple, and they differ solely in their arguments — two usages, one attribute type, one
// target, and nothing but an argument list to tell them apart.

extern alias NsPing;

using System.Reflection;

using NsUnitTag = System.Int32;

[assembly: AssemblyMetadata("surface.clause", "14.2")]
[assembly: AssemblyMetadata("surface.project", "Namespaces")]
[module: System.Runtime.CompilerServices.SkipLocalsInit]

namespace Surface.Namespaces.Compilation
{
    /// <summary>
    /// 14.2: the namespace_member_declarations section of this compilation unit — the fourth
    /// and last of the four sections, and the only one that declares a type.
    /// </summary>
    public sealed class NsCompilationUnitReport
    {
        /// <summary>The clause this compilation unit was written for.</summary>
        public const string Clause = "14.2";

        /// <summary>14.2: uses the alias declared by the using_directives section.</summary>
        public NsUnitTag Tag { get; init; } = 142;

        /// <summary>
        /// 14.2: uses the alias declared by the extern_alias_directives section, so the first
        /// section of the compilation unit is a live reference and not decoration.
        /// </summary>
        public static string AliasedAssemblyType() =>
            typeof(NsPing::System.Net.NetworkInformation.Ping).Name;

        /// <summary>
        /// 14.2: reads back this compilation unit's own assembly-target global attributes,
        /// which is the only way a global_attributes section is observable from inside the
        /// program. The module-target one is read the same way through
        /// <see cref="System.Reflection.Module.GetCustomAttributes(bool)"/>.
        /// </summary>
        public static int GlobalAttributeCount()
        {
            var assembly = typeof(NsCompilationUnitReport).Assembly;
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
            return metadata.Length;
        }
    }
}
