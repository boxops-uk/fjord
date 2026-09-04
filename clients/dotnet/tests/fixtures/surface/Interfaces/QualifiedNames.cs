// Clause 19.5 — qualified interface member names. The name of an explicit interface member
// implementation is not an identifier: it is an interface type followed by a dot and a member
// name, and the interface type may be written in every form a type name may take. This file
// writes one member per spelling, so a query can ask whether the four spellings of one
// interface produce one qualifier or four.

using IfaceNamespaceAlias = Surface.Interfaces;
using IfaceTypeAlias = Surface.Interfaces.IfaceQualified;

namespace Surface.Interfaces;

/// <summary>
/// 19.5 — the interface whose members are implemented below under four spellings of its name.
/// One member per spelling, because two members of one interface implemented under two
/// spellings would be the same qualified name twice.
/// </summary>
public interface IfaceQualified
{
    /// <summary>19.5 — implemented under the simple name.</summary>
    string Simple();

    /// <summary>19.5 — implemented under the namespace-qualified name.</summary>
    string Namespaced();

    /// <summary>19.5 — implemented under a namespace alias.</summary>
    string Aliased();

    /// <summary>19.5 — implemented under a type alias.</summary>
    string TypeAliased();

    /// <summary>19.5 — implemented under an alias-qualified name.</summary>
    string Globalised();
}

/// <summary>
/// 19.5 hazard — one class, five explicit implementations, five spellings of one interface name.
/// Every one of these member names is a qualified name whose qualifier denotes
/// <see cref="IfaceQualified"/>, and an identity built from the written qualifier rather than
/// from the interface it binds to would produce five different owners for one interface.
/// </summary>
public sealed class IfaceQualifiedBox : IfaceQualified
{
    /// <summary>19.5 — <c>interface_type . identifier</c> with the interface named simply.</summary>
    string IfaceQualified.Simple() => "simple";

    /// <summary>19.5 — the qualifier is a namespace-qualified type name.</summary>
    string Surface.Interfaces.IfaceQualified.Namespaced() => "namespaced";

    /// <summary>19.5 — the qualifier begins with a using alias for the namespace.</summary>
    string IfaceNamespaceAlias.IfaceQualified.Aliased() => "aliased";

    /// <summary>19.5 — the qualifier is a using alias for the interface itself, so the
    /// qualifier's own name is nowhere in the interface's name.</summary>
    string IfaceTypeAlias.TypeAliased() => "type-aliased";

    /// <summary>19.5 — the qualifier is alias-qualified with <c>global::</c>.</summary>
    string global::Surface.Interfaces.IfaceQualified.Globalised() => "globalised";
}

/// <summary>
/// 19.5 hazard — a qualified interface member name whose qualifier is a constructed type. The
/// member name is <c>IfaceMap&lt;string, int&gt;.Get</c>: the substitution is part of the name,
/// and an identity that drops it cannot distinguish this member from the same member of any
/// other construction of <see cref="IfaceMap{TKey, TValue}"/>.
/// </summary>
public sealed class IfaceQualifiedMap : IfaceMap<string, int>
{
    /// <summary>19.5 — the qualifier is a constructed generic interface.</summary>
    int IfaceMap<string, int>.Get(string key) => key.Length;

    /// <summary>19.5 — a second member of the same constructed interface, whose qualifier is
    /// spelled identically and whose member name is not.</summary>
    void IfaceMap<string, int>.Put(string key, int value)
    {
    }

    /// <summary>19.5 — a qualified interface member name for an indexer, where the member name
    /// is the keyword <c>this</c> and not an identifier at all. This is the type's only
    /// indexer.</summary>
    int IfaceMap<string, int>.this[string key] => key.Length;

    /// <summary>19.5 — an implicit implementation beside the explicit ones, whose name is an
    /// identifier and whose mapping is the same kind of edge.</summary>
    public int Count => 0;
}

/// <summary>19.5 — the five spellings called, each through the interface.</summary>
public static class IfaceQualifiedUse
{
    /// <summary>19.5 — every explicitly implemented member is reachable only through the
    /// interface, whatever spelling its implementation used.</summary>
    public static string All(IfaceQualifiedBox box)
    {
        IfaceQualified qualified = box;
        return qualified.Simple()
            + qualified.Namespaced()
            + qualified.Aliased()
            + qualified.TypeAliased()
            + qualified.Globalised();
    }

    /// <summary>19.5 — the constructed interface's members, through the constructed
    /// interface.</summary>
    public static int MapValues(IfaceQualifiedMap map)
    {
        IfaceMap<string, int> constructed = map;
        constructed.Put("key", 1);
        return constructed.Get("key") + constructed["key"] + constructed.Count;
    }
}
