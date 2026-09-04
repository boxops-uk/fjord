namespace Surface.Synthesised;

/// <summary>
/// The other half of <see cref="SynSplitPart"/>, which adds an interface and a member.
/// </summary>
public partial class SynSplitPart : ISynMarker
{
    /// <summary>Declared in this half, and read from the other.</summary>
    public int FromB => 2;
}

/// <summary>A partial struct, also split across two files, with its other half here.</summary>
/// <remarks>
/// Both halves of <c>SynSplitStruct</c> are in this one file, which is exactly the shape
/// the corpus quarantines — so it is <em>not</em> written. What stands instead is a single
/// non-partial struct, and the note that the two-parts-one-file shape belongs to the
/// quarantine project.
/// </remarks>
public struct SynSplitStruct
{
    /// <summary>The one field.</summary>
    public int Weight;
}
