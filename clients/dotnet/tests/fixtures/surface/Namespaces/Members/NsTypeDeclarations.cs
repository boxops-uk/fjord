// Clause 14.7 (type declarations): the namespace member that is a type. The clause enumerates
// the forms, and all of them are declared below as members of one namespace — a class, a
// struct, an interface, an enum and a delegate from the standard, plus the record class and
// record struct that later versions added to the same production.
//
// Generic types are here too, at one arity each. `NsCell<TValue>` has no non-generic
// namesake and `NsPairOf<TFirst, TSecond>` has no one-parameter namesake, deliberately: a
// type name declared at two arities is a known index-killer, so the corpus's coverage of
// arity lives in a quarantine project and this file stays on the safe side of it.
//
// The hazard is that these are the members whose declarations *do* have locations, sitting in
// a container whose declarations do not. Each type below can be pointed at; the namespace they
// are members of cannot.

namespace Surface.Namespaces.Members
{
    /// <summary>14.7: a class declaration as a namespace member.</summary>
    public class NsMemberClass
    {
        /// <summary>The clause this type was written for.</summary>
        public const string Clause = "14.7.class";

        /// <summary>Something for a derived class to override.</summary>
        public virtual string Describe() => Clause;
    }

    /// <summary>14.7: a class declaration that is abstract, and its sealed derivative.</summary>
    public abstract class NsMemberAbstractClass
    {
        /// <summary>What the derived class must say.</summary>
        public abstract string Describe();
    }

    /// <summary>14.7: the sealed leaf of the pair above.</summary>
    public sealed class NsMemberSealedClass : NsMemberAbstractClass
    {
        /// <inheritdoc/>
        public override string Describe() => "14.7.sealed";
    }

    /// <summary>14.7: a struct declaration as a namespace member.</summary>
    public readonly struct NsMemberStruct
    {
        /// <summary>The clause this type was written for.</summary>
        public const string Clause = "14.7.struct";

        /// <summary>How wide the cell is.</summary>
        public int Width { get; init; }
    }

    /// <summary>14.7: an interface declaration as a namespace member.</summary>
    public interface INsMemberInterface
    {
        /// <summary>What every implementation reports.</summary>
        string Describe();
    }

    /// <summary>14.7: an enum declaration as a namespace member.</summary>
    public enum NsMemberEnum
    {
        /// <summary>The lower of the two.</summary>
        Low = 1,

        /// <summary>The higher of the two.</summary>
        High = 2,
    }

    /// <summary>14.7: a delegate declaration as a namespace member.</summary>
    /// <param name="value">What the target is handed.</param>
    /// <returns>Whatever the target makes of it.</returns>
    public delegate string NsMemberDelegate(int value);

    /// <summary>14.7: a record class declaration as a namespace member.</summary>
    /// <param name="Label">What the record is called.</param>
    public record NsMemberRecordClass(string Label) : INsMemberInterface
    {
        /// <inheritdoc/>
        public string Describe() => Label;
    }

    /// <summary>14.7: a record struct declaration as a namespace member.</summary>
    /// <param name="Row">Which row this is.</param>
    public readonly record struct NsMemberRecordStruct(int Row);

    /// <summary>14.7: a generic class declaration, declared at one arity and only one.</summary>
    /// <typeparam name="TValue">What the cell holds.</typeparam>
    public sealed class NsCell<TValue>
        where TValue : notnull
    {
        /// <summary>The clause this type was written for.</summary>
        public const string Clause = "14.7.generic";

        /// <summary>Builds a cell around a value.</summary>
        /// <param name="value">What to hold.</param>
        public NsCell(TValue value) => Value = value;

        /// <summary>What the cell holds.</summary>
        public TValue Value { get; }

        /// <summary>14.5.4: a static member, so a using static directive has something to import.</summary>
        public static string CellLabel() => Clause;
    }

    /// <summary>14.7: a second generic class, at a different arity and a different name.</summary>
    /// <typeparam name="TFirst">The left half.</typeparam>
    /// <typeparam name="TSecond">The right half.</typeparam>
    public sealed class NsPairOf<TFirst, TSecond>
    {
        /// <summary>Builds a pair.</summary>
        /// <param name="first">The left half.</param>
        /// <param name="second">The right half.</param>
        public NsPairOf(TFirst first, TSecond second)
        {
            First = first;
            Second = second;
        }

        /// <summary>The left half.</summary>
        public TFirst First { get; }

        /// <summary>The right half.</summary>
        public TSecond Second { get; }
    }
}
