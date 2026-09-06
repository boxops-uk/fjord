namespace Surface.Synthesised;

/// <summary>
/// One half of a partial class (C# 2), whose other half is in <c>PartialSplitB.cs</c>.
/// </summary>
/// <remarks>
/// A partial type is one symbol with two declaring syntax references in two files. Split
/// across files, as here, it is the ordinary case and an index merges the halves. Both
/// halves in <em>one</em> file is the shape the corpus quarantines, because the two
/// declarations then agree on file as well as on name and arity.
///
/// Note what each half contributes: the base list is written once, the interface list
/// once, and the type parameter constraint once — the compiler unions them, so the symbol
/// this file declares has members, a base type and an interface that this file does not
/// mention.
/// </remarks>
public partial class SynSplitPart : SynClassKind
{
    /// <summary>Declared in this half.</summary>
    public int FromA => 1;

    /// <inheritdoc/>
    public override string Label() => $"{FromA}:{FromB}";
}
