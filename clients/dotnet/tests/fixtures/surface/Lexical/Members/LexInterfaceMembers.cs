// Clause 7.4.6 (interface members). Two interfaces declare a member of the same name and
// one class implements both explicitly, so `LexTwoInterfaces` holds two members whose
// simple name is `Extent()` and whose metadata names are the interface-qualified ones.
// That is the shape every collection in the framework has — `IEnumerable.GetEnumerator`
// beside `IEnumerable<T>.GetEnumerator` — and the one an index keyed on container plus
// simple name plus parameters cannot separate.

namespace Surface.Lexical.Members;

/// <summary>7.4.6: an interface declaring one of each member kind it may declare.</summary>
public interface ILexInterfaceMembers
{
    /// <summary>A property.</summary>
    int Extent { get; }

    /// <summary>A settable property.</summary>
    int Held { get; set; }

    /// <summary>The one indexer an interface may declare without repeating itself.</summary>
    int this[int offset] { get; }

    /// <summary>An event.</summary>
    event System.Action Raised;

    /// <summary>A method.</summary>
    int Read();

    /// <summary>A method with a default implementation, so an interface has a body.</summary>
    int ReadTwice() => Read() * 2;

    /// <summary>A static member, which an interface may declare and no instance inherits.</summary>
    static int Shared => 1;
}

/// <summary>7.4.6: an interface inheriting another, so its member list is the union.</summary>
public interface ILexDerivedInterface : ILexInterfaceMembers
{
    /// <summary>A declaration of its own.</summary>
    int Own { get; }
}

/// <summary>7.4.6: the first of two interfaces declaring a method of one name.</summary>
public interface ILexFirstExtent
{
    /// <summary>Named the same as the member of the second interface.</summary>
    int Extent();
}

/// <summary>7.4.6: the second of two interfaces declaring a method of one name.</summary>
public interface ILexSecondExtent
{
    /// <summary>Named the same as the member of the first interface.</summary>
    int Extent();
}

/// <summary>
/// 7.4.6: two explicit interface implementations of one simple name in one type. Neither
/// is accessible except through its interface, and there is no third member named
/// <c>Extent</c> for them to be confused with.
/// </summary>
public sealed class LexTwoInterfaces : ILexFirstExtent, ILexSecondExtent
{
    /// <summary>Implements the first interface's member.</summary>
    int ILexFirstExtent.Extent() => 1;

    /// <summary>Implements the second interface's member, whose name is the same.</summary>
    int ILexSecondExtent.Extent() => 2;

    /// <summary>Reads both through their interfaces, which is the only way to reach them.</summary>
    public int Both() => ((ILexFirstExtent)this).Extent() + ((ILexSecondExtent)this).Extent();
}

/// <summary>7.4.6: an implicit implementation of the full interface, for the comparison.</summary>
public sealed class LexInterfaceImplementation : ILexDerivedInterface
{
    /// <summary>Implements the inherited property implicitly.</summary>
    public int Extent => 1;

    /// <summary>Implements the inherited settable property implicitly.</summary>
    public int Held { get; set; }

    /// <summary>Implements the derived interface's own property.</summary>
    public int Own => 2;

    /// <summary>Implements the inherited indexer.</summary>
    public int this[int offset] => offset + Extent;

    /// <summary>Implements the inherited event.</summary>
    public event System.Action? Raised;

    /// <summary>Implements the inherited method.</summary>
    public int Read() => Extent + Held + Own;

    /// <summary>Raises the event, so it is written as well as declared.</summary>
    public void Raise() => Raised?.Invoke();
}
