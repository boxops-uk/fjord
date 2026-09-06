namespace Widgets
{
    /// <summary>A use of each, so every declaration has a reference.</summary>
    public static class Uses
    {
        /// <summary>Uses them.</summary>
        public static int Go()
        {
            var gadget = new Gadget { Name = "one" };
            var bolt = new Bolt();
            return gadget.Spin(2) + bolt.Size + gadget.Name.Length;
        }
    }
}
