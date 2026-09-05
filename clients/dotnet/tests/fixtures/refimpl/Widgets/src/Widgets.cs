namespace Widgets
{
    /// <summary>A gadget that spins.</summary>
    public class Gadget
    {
        /// <summary>Makes a gadget.</summary>
        public Gadget() { }

        /// <summary>Spins it <paramref name="turns"/> times.</summary>
        public int Spin(int turns) => turns * 2;

        /// <summary>What it is called.</summary>
        public string Name { get; set; } = "gadget";
    }

    /// <summary>A bolt, which has a size.</summary>
    public struct Bolt
    {
        /// <summary>How big it is.</summary>
        public int Size => 4;
    }
}
