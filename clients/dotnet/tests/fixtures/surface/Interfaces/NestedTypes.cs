// Clause 19.4.9 — interface nested types. An interface may declare types, and a nested type of
// an interface is a type whose containing declaration is not a class or struct: its identity is
// qualified by an interface, which is the shape this file is here for.

namespace Surface.Interfaces;

/// <summary>
/// 19.4.9 hazard — every type kind an interface may nest, in one interface. Each nested
/// declaration is a member of this interface and a type in its own right, so it wants both an
/// identity as a member and an identity as a type, and the two have to agree.
/// </summary>
public interface IfaceHost
{
    /// <summary>19.4.9 — a nested class.</summary>
    public class IfaceHostEntry
    {
        /// <summary>19.4.9 — an instance field, which an interface may not declare but a class
        /// nested in one may.</summary>
        public int Weight;

        /// <summary>19.4.9 — and a constructor, likewise.</summary>
        public IfaceHostEntry(int weight) => Weight = weight;
    }

    /// <summary>19.4.9 — a nested struct.</summary>
    public readonly struct IfaceHostKey
    {
        public IfaceHostKey(string text) => Text = text;

        public string Text { get; }
    }

    /// <summary>19.4.9 — a nested interface, whose container is an interface.</summary>
    public interface IfaceHostInner
    {
        /// <summary>19.4.9 — a member of the nested interface, not of the outer one.</summary>
        void Inner();
    }

    /// <summary>19.4.9 — a nested enum.</summary>
    public enum IfaceHostMode
    {
        /// <summary>19.4.9 — an enum member of a type nested in an interface.</summary>
        Idle = 0,

        Busy = 1,
    }

    /// <summary>19.4.9 — a nested delegate declaration.</summary>
    public delegate int IfaceHostPick(int left, int right);

    /// <summary>19.4.9 — a nested record, whose generated members are nested twice over.</summary>
    public record IfaceHostNote(string Text, int Weight);

    /// <summary>19.4.9 — an instance member whose type is a nested type of the same
    /// interface, so the reference and the declaration share a container.</summary>
    IfaceHostEntry Entry { get; }

    /// <summary>19.4.9 — a member whose signature names three of the nested types.</summary>
    IfaceHostNote Describe(IfaceHostKey key, IfaceHostMode mode);
}

/// <summary>
/// 19.4.9 hazard — a nested type of a generic interface. <c>IfaceHostOfCursor</c> declares no
/// type parameters of its own and is nonetheless a different type for every substitution of
/// <c>TItem</c>: its identity carries the enclosing interface's arguments.
/// </summary>
/// <typeparam name="TItem">19.4.9 — used by the nested class below.</typeparam>
public interface IfaceHostOf<TItem>
{
    /// <summary>19.4.9 — a non-generic class nested in a generic interface.</summary>
    public sealed class IfaceHostOfCursor
    {
        /// <summary>19.4.9 — a field whose type is the enclosing interface's type parameter.</summary>
        public TItem Current;
    }

    /// <summary>19.4.9 — a generic class nested in a generic interface, so two argument lists
    /// meet on one type.</summary>
    public sealed class IfaceHostOfPair<TOther>
    {
        public TItem First;

        public TOther Second;
    }

    /// <summary>19.4.9 — the cursor named from the interface that contains it.</summary>
    IfaceHostOfCursor Cursor { get; }
}

/// <summary>19.4.9 — an implementation, which names the nested types through the interface.</summary>
public sealed class IfaceHostBox : IfaceHost, IfaceHost.IfaceHostInner
{
    /// <summary>19.4.9 — a nested type of an interface used as a field type from outside it.</summary>
    private readonly IfaceHost.IfaceHostEntry _entry = new(2);

    /// <summary>19.4.9 — the interface property, whose type is a nested type of the interface
    /// that declares it.</summary>
    public IfaceHost.IfaceHostEntry Entry => _entry;

    /// <summary>19.4.9 — the nested interface's member, implemented by the same class that
    /// implements the container.</summary>
    public void Inner()
    {
    }

    /// <summary>19.4.9 — a nested enum member and a nested record constructor, both reached
    /// through the interface name.</summary>
    public IfaceHost.IfaceHostNote Describe(IfaceHost.IfaceHostKey key, IfaceHost.IfaceHostMode mode) =>
        new(key.Text, mode == IfaceHost.IfaceHostMode.Busy ? 1 : 0);

    /// <summary>19.4.9 — a nested delegate type of an interface, instantiated.</summary>
    public static IfaceHost.IfaceHostPick Larger => (left, right) => left >= right ? left : right;

    /// <summary>19.4.9 — two constructions of a nested type of a generic interface, which is
    /// one declaration and two types.</summary>
    public static string Cursors()
    {
        var text = new IfaceHostOf<string>.IfaceHostOfCursor { Current = "s" };
        var number = new IfaceHostOf<int>.IfaceHostOfCursor { Current = 1 };
        var pair = new IfaceHostOf<int>.IfaceHostOfPair<string> { First = 2, Second = "t" };

        return $"{text.Current}{number.Current}{pair.First}{pair.Second}";
    }
}
