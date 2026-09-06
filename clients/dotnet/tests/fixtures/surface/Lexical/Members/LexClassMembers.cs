// Clause 7.4.5 (class members) and 7.4.1 (members, general). 7.4.5 lists every kind of
// member a class may declare; 7.4.1's rule is that a type's members include the ones it
// inherits, which is what `LexDerivedMembers` is for — `derived.Extent` is a reference
// whose declaration is in the base, so a reference recorded against the type it was
// *written* on names a type that declares nothing of that name.

namespace Surface.Lexical.Members;

/// <summary>7.4.5: every kind of member a class may declare.</summary>
public class LexClassMembers
{
    /// <summary>A constant.</summary>
    public const int Extent = 1;

    /// <summary>An instance field with an initialiser.</summary>
    public int Held = 2;

    /// <summary>A static field.</summary>
    public static int Shared = 3;

    /// <summary>A readonly field, which only a constructor may assign.</summary>
    public readonly int Fixed;

    /// <summary>A volatile field, whose reads and writes are ordered.</summary>
    public volatile int Watched;

    /// <summary>A static constructor.</summary>
    static LexClassMembers() => Shared = Extent;

    /// <summary>An instance constructor.</summary>
    public LexClassMembers() => Fixed = Extent;

    /// <summary>A second constructor, chaining to the first.</summary>
    public LexClassMembers(int held)
        : this() => Held = held;

    /// <summary>A finalizer, which compiles to <c>Finalize</c>.</summary>
    ~LexClassMembers()
    {
    }

    /// <summary>A property with both accessors.</summary>
    public int Computed { get; set; }

    /// <summary>A property with only a getter, written as an expression body.</summary>
    public int Doubled => Held * 2;

    /// <summary>A property whose accessors differ in accessibility.</summary>
    public int Guarded { get; protected set; }

    /// <summary>The one indexer this type declares.</summary>
    public int this[int offset] => Held + offset;

    /// <summary>A field-like event.</summary>
    public event System.Action? Raised;

    /// <summary>An event with explicit accessors.</summary>
    public event System.Action Watched2
    {
        add { }
        remove { }
    }

    /// <summary>A method.</summary>
    public virtual int Read() => Held + Extent + Shared + Fixed + Watched;

    /// <summary>A static method.</summary>
    public static int ReadShared() => Shared;

    /// <summary>An operator.</summary>
    public static LexClassMembers operator +(LexClassMembers left, LexClassMembers right) =>
        new(left.Held + right.Held);

    /// <summary>A nested class.</summary>
    public class NestedClass
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 4;
    }

    /// <summary>A nested struct.</summary>
    public struct NestedStruct
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 5;
    }

    /// <summary>A nested interface.</summary>
    public interface INestedInterface
    {
        /// <summary>A member so it is not empty.</summary>
        int Read();
    }

    /// <summary>A nested enum.</summary>
    public enum NestedEnum
    {
        /// <summary>The only member.</summary>
        Only = 6,
    }

    /// <summary>A nested delegate.</summary>
    /// <param name="value">What is passed along.</param>
    /// <returns>Whatever the target returns.</returns>
    public delegate int NestedDelegate(int value);

    /// <summary>Raises the field-like event, so it is written as well as declared.</summary>
    public void Raise() => Raised?.Invoke();
}

/// <summary>
/// 7.4.1: a derived class, whose member list is its own declarations plus the base's. It
/// declares nothing named <c>Extent</c>, <c>Held</c> or <c>Read</c>, and all three are
/// members of it.
/// </summary>
public class LexDerivedMembers : LexClassMembers
{
    /// <summary>A declaration of its own, so the type is not only its inheritance.</summary>
    public const int Own = 7;

    /// <summary>Overrides the base's method, which is one member and two declarations.</summary>
    public override int Read() => base.Read() + Own;
}

/// <summary>
/// 7.4.1: references to inherited members, written on the derived type. Each of these
/// resolves to a declaration in <c>LexClassMembers</c> and is spelled against
/// <c>LexDerivedMembers</c>.
/// </summary>
public static class LexInheritedMemberUses
{
    /// <summary>Reads the inherited constant through the derived type's name.</summary>
    public static int ThroughTheDerivedType() => LexDerivedMembers.Extent;

    /// <summary>Reads the inherited field through a derived instance.</summary>
    public static int ThroughADerivedInstance(LexDerivedMembers derived) => derived.Held;

    /// <summary>Reads the inherited indexer through a derived instance.</summary>
    public static int ThroughTheInheritedIndexer(LexDerivedMembers derived) => derived[1];

    /// <summary>Reads the same constant through the declaring type, for the comparison.</summary>
    public static int ThroughTheDeclaringType() => LexClassMembers.Extent;

    /// <summary>Reads the nested types, so each is reached.</summary>
    public static int NestedTags() =>
        LexClassMembers.NestedClass.Tag
        + LexClassMembers.NestedStruct.Tag
        + (int)LexClassMembers.NestedEnum.Only;
}
