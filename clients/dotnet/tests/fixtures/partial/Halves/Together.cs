namespace Halves;

/// <summary>
/// Both halves of one member <i>in one file</i>, which C# permits — and two parts of one
/// type in one file beside them, which is the same defect from a different direction.
/// </summary>
public partial class Together
{
    /// <summary>One member, two declarations, one file, one <c>{symbol, file}</c> key.</summary>
    public partial void Tick();

    public partial void Tick()
    {
    }

    /// <summary>The property form of it.</summary>
    public partial int Beats { get; }

    public partial int Beats => 3;
}

/// <summary>
/// A second part of <c>Together</c> in the file the first part is in: one type, two
/// declarations, one <c>{symbol, file}</c> key — a dead run before this fixture existed,
/// and nothing to do with a partial <i>member</i>.
/// </summary>
public partial class Together
{
    public int Extra => 4;
}
