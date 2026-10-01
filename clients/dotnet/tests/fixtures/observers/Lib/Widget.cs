namespace Lib;

public static class Widget
{
    /// <summary>Lives in source, and not in the assembly.</summary>
    public static int Measure(string name) => name.Length;
}
