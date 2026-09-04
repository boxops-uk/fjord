namespace Slots;

/// <summary>
/// A use of each indexer, so every declaration has a reference to be byte-identical to.
/// The ones through <c>Shelf&lt;int&gt;</c> are the arm that resolves a *constructed*
/// member.
/// </summary>
public static class Uses
{
    public static int ByPosition(Shelf<int> shelf) => shelf[0];

    public static int ByPage(Shelf<int> shelf) => shelf[1, 0];

    public static int ByName(Shelf<int> shelf) => shelf["slot"];

    public static int Lone(Single single) => single[0];

    public static int Interfaced(IShelf shelf) => shelf[0] + shelf["slot"];
}
