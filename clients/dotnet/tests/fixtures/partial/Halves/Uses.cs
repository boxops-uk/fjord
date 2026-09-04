namespace Halves;

/// <summary>
/// A use of each partial member, so every one of them has a reference that must be
/// byte-identical to the one string both of its halves mint.
/// </summary>
public static class Uses
{
    public static int Go(Across across, Together together)
    {
        across.Ping();
        across.Ping(2);
        together.Tick();

        return across.Count + across[0] + across["slot"] + together.Beats + across.Whole();
    }
}
