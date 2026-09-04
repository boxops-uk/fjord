// M17 — the entity layer's missing assembly axis.
//
// This file and `Assemblies.Right/PairedType.cs` declare THE SAME namespace-qualified type
// names. That is the mechanism, not an accident: `CsharpEntities.FullName` is
// `{Name(symbol.Name), Namespace(symbol.ContainingNamespace)}` with no assembly field, and
// `csharp.Class` keys on that plus base type, containing type, accessibility and the boolean
// modifiers — every one of which the two copies agree about. So the two declarations intern
// ONE `csharp.Class` row with two `csharp.DefinitionLocation` spans, one `csharp.SymbolOf`
// mapping to two symbol strings, and a `csharp.EntityXRef` that cannot tell
// `AsmLeftUses`'s use of `AsmPairSignal` from `AsmRightUses`'s. The SCIP layer is expected
// to get this right, because `Package` writes the containing assembly's own identity into
// the symbol string.
//
// **The two copies must stay declaration-identical, including these doc comments.** Only the
// one body line marked below differs. `codemarkup.SymbolInfo` is keyed on the symbol alone
// and carries `{signature, doc, modifiers}`: if the assembly coordinate ever left the symbol
// string, identical values would still dedupe silently, whereas a diverging signature or doc
// would be a refused write that kills the whole corpus run. A silent merge is what this
// project is for; a dead run is what it must not be.
namespace Surface.Assemblies.Shared;

/// <summary>
/// A signal a producer raises and a consumer counts. Declared, identically, in two
/// assemblies that cannot see each other (15.2.1, 14.7 — a fully qualified name is unique
/// within a program, and two programs are two of them).
/// </summary>
public class AsmPairSignal
{
    /// <summary>How many times this signal has been raised.</summary>
    public int Count { get; private set; }

    /// <summary>Raises the signal, and answers with the stamp of the assembly that holds it.</summary>
    public string Raise()
    {
        Count++;
        return global::Surface.Assemblies.Left.AsmLeftBackend.Stamp; // the one line that differs between the two copies
    }
}

/// <summary>
/// A one-item slot: the pair's generic half, so one unconstrained type parameter named
/// <c>T</c> is declared in each of two assemblies (8.5, 15.2.3). <c>csharp.TypeParameter</c>
/// is keyed on name, variance and three constraint flags with no declaring scope, so both
/// <c>T</c>s are one fact — M10, at its widest.
/// </summary>
public class AsmPairSlot<T>
{
    private T? _held;

    /// <summary>Whether the slot currently holds an item.</summary>
    public bool IsFull { get; private set; }

    /// <summary>Puts an item in the slot, replacing whatever was there.</summary>
    public void Put(T item)
    {
        _held = item;
        IsFull = true;
    }

    /// <summary>Takes the held item, leaving the slot empty.</summary>
    public T? Take()
    {
        T? held = _held;
        _held = default;
        IsFull = false;
        return held;
    }
}
