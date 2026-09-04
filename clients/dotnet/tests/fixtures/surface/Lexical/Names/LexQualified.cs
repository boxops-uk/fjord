// Clause 7.8.1 (namespace and type names, general): the forms a namespace-or-type-name may
// take — a simple name, a qualified name, an alias-qualified name, `global::`, a
// constructed type, an array type and a nullable type.
//
// The hazard is that every one of the first six members below has the type
// `System.String`. Six spellings, one symbol: an index that records the reference's *text*
// answers "what types does this class mention" with six answers, and one that records the
// resolved symbol answers it with one.

using System;
using SysText = System.Text;
using StringAlias = System.String;
using IntList = System.Collections.Generic.List<int>;
using static System.Math;

namespace Surface.Lexical.Names;

/// <summary>7.8.1: one field per spelling of a namespace-or-type-name.</summary>
public sealed class LexQualifiedNames
{
    /// <summary>The predefined-type keyword, which is not an identifier at all.</summary>
    public string ByKeyword = string.Empty;

    /// <summary>The simple name, reachable because of the <c>using System;</c> above.</summary>
    public String BySimpleName = String.Empty;

    /// <summary>The qualified name.</summary>
    public System.String ByQualifiedName = System.String.Empty;

    /// <summary>The alias-qualified name, whose <c>::</c> skips every local declaration.</summary>
    public global::System.String ByGlobalQualifier = global::System.String.Empty;

    /// <summary>A <c>using</c> alias for the type itself.</summary>
    public StringAlias ByTypeAlias = StringAlias.Empty;

    /// <summary>The same type reached through a <c>using</c> alias for its *namespace*.</summary>
    public SysText::StringBuilder ByNamespaceAlias = new();

    /// <summary>A constructed type, whose type argument is a namespace-or-type-name too.</summary>
    public System.Collections.Generic.List<System.String> Constructed = [];

    /// <summary>An alias for a constructed type, which names an instantiation and not a type.</summary>
    public IntList AliasedInstantiation = [];

    /// <summary>An array type, whose element type is a namespace-or-type-name.</summary>
    public String[] ArrayOfThem = [];

    /// <summary>A nullable value type, which is <c>Nullable&lt;int&gt;</c> spelled shorter.</summary>
    public int? NullableValue;

    /// <summary>The same type spelled out, so both spellings are in the corpus.</summary>
    public System.Nullable<int> NullableSpeltOut;

    /// <summary>A nested type reached through its containing type's name.</summary>
    public System.Collections.Generic.List<int>.Enumerator NestedThroughContainer =
        new System.Collections.Generic.List<int>().GetEnumerator();

    /// <summary>A member reached through <c>using static</c>, which imports no type name.</summary>
    public double Rounded => Floor(1.5);

    /// <summary>Reads every field, so none of the spellings is unreferenced.</summary>
    public int Total() =>
        ByKeyword.Length + BySimpleName.Length + ByQualifiedName.Length
        + ByGlobalQualifier.Length + ByTypeAlias.Length + ByNamespaceAlias.Length
        + Constructed.Count + AliasedInstantiation.Count + ArrayOfThem.Length
        + (NullableValue ?? 0) + (NullableSpeltOut ?? 0)
        + (int)Rounded;
}
