namespace Surface.Bindings.Attributes;

/// <summary>
/// M8 — the application sites: four spellings, four references to a constructor, and no
/// reference to <c>BindMarkAttribute</c>.
/// </summary>
/// <remarks>
/// The four are deliberately different: the shortened name with a named argument, the
/// shortened name with a positional argument, the shortened name bare, and the full name.
/// All four bind to a <c>.ctor</c>, so the count of reference rows targeting the attribute
/// class is nought at four application sites rather than at one — a producer that special-
/// cased the bare form would still be wrong here.
/// </remarks>
[BindMark(Topic = "audit")]
public sealed class BindAnnotated
{
    /// <summary>Marked with the positional overload.</summary>
    [BindMark("because")]
    public int Weight { get; set; }

    /// <summary>Marked with the bare shortened name.</summary>
    [BindMark]
    public int Slot;

    /// <summary>Marked with the full name, which is the same constructor.</summary>
    /// <param name="held">A parameter, so the attribute reaches a parameter position too.</param>
    [BindMarkAttribute]
    public int Run(int held) => held + Weight + Slot;
}
