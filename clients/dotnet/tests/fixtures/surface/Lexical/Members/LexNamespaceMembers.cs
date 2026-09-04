// Clause 7.4.2 (namespace members): a namespace's members are namespaces and types, and
// nothing else — no field, no method, no constant lives directly in one. Every kind of
// type is declared here so that the namespace's member list is complete.
//
// The hazard is the spelling: this file opens `Surface.Lexical.Members.Nest.Inner` with
// the dotted form and LexNamespaceMembersNested.cs opens the same namespace with two
// nested declarations. One declaration space, two syntaxes, four namespace declaration
// nodes between the two files.

namespace Surface.Lexical.Members.Nest.Inner;

/// <summary>7.4.2: a class as a namespace member.</summary>
public class LexNamespaceClass
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 1;
}

/// <summary>7.4.2: a struct as a namespace member.</summary>
public struct LexNamespaceStruct
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 2;
}

/// <summary>7.4.2: an interface as a namespace member.</summary>
public interface ILexNamespaceInterface
{
    /// <summary>A member so the interface is not empty.</summary>
    int Read();
}

/// <summary>7.4.2: an enum as a namespace member.</summary>
public enum LexNamespaceEnum
{
    /// <summary>The only member.</summary>
    Only = 3,
}

/// <summary>7.4.2: a delegate as a namespace member.</summary>
/// <param name="value">What is passed along.</param>
/// <returns>Whatever the target returns.</returns>
public delegate int LexNamespaceDelegate(int value);

/// <summary>7.4.2: a record, which is a class and so also a namespace member.</summary>
/// <param name="Tag">Something to reference.</param>
public record LexNamespaceRecord(int Tag);
