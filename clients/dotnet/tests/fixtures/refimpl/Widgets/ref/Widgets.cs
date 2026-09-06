// The API surface as a reference assembly states it: every member declared, no body, and
// no documentation comment anywhere. This is what `ref/System.Collections.cs` is.
namespace Widgets
{
    public partial class Gadget
    {
        public Gadget() { }

        public int Spin(int turns) { throw null; }

        public string Name { get { throw null; } set { } }
    }

    public partial struct Bolt
    {
        public int Size { get { throw null; } }
    }
}
