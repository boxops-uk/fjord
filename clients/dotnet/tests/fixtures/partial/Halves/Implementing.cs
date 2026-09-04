namespace Halves;

/// <summary>
/// The implementing halves. Every symbol here is a declaration whose own containing type
/// does not list it, which is the shape that killed the run.
/// </summary>
public partial class Across
{
    public partial void Ping()
    {
    }

    public partial void Ping(int times)
    {
    }

    public partial int Count => 2;

    public partial int this[int slot] => slot;

    public partial int this[string name] => name.Length;
}
