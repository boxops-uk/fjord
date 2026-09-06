namespace Surface.SyntaxForms.Declarations;

// The six type-declaration kinds, the delegate kind, and the two kinds that hang off a
// type's own header. Every node in this file is a `BaseTypeDeclarationSyntax` or a
// `DelegateDeclarationSyntax` — two of the six cases the walk's declaration switch names —
// so these are the rows the indexer is most certain to answer, and they are gathered here
// so the certain ones can be read together.

/// <summary>ClassDeclaration — a class with a nested class, so a qualified name has a target.</summary>
public class SfLedgerClass
{
    /// <summary>A running total, so the class has state worth naming.</summary>
    public int Total { get; set; }

    /// <summary>A nested class, which is what makes <c>SfLedgerClass.SfNestedEntry</c> a QualifiedName.</summary>
    public sealed class SfNestedEntry
    {
        /// <summary>The amount this entry moves.</summary>
        public int Amount { get; init; }
    }
}

/// <summary>StructDeclaration — a value type, distinct from the record struct below.</summary>
public struct SfLedgerStruct
{
    /// <summary>The column this cell sits in.</summary>
    public int Column;

    /// <summary>The row this cell sits in.</summary>
    public int Row;
}

/// <summary>InterfaceDeclaration — the contract the member forms implement.</summary>
public interface SfLedgerContract
{
    /// <summary>How many entries the ledger holds.</summary>
    int Size { get; }

    /// <summary>Renders the ledger for a human.</summary>
    string Describe();
}

/// <summary>RecordDeclaration — the base half of the primary-constructor base row.</summary>
/// <param name="Seed">The opening balance.</param>
public record SfLedgerRecordBase(int Seed);

/// <summary>
/// RecordDeclaration, and PrimaryConstructorBaseType in its <c>: SfLedgerRecordBase(Seed)</c>
/// — the base clause of a primary-constructor type, which carries argument syntax and so
/// references a constructor rather than only a type.
/// </summary>
/// <param name="Seed">Passed straight through to the base record.</param>
/// <param name="Label">What this run of the ledger is called.</param>
public record SfLedgerRecord(int Seed, string Label) : SfLedgerRecordBase(Seed);

/// <summary>RecordStructDeclaration — the value-type spelling of a record.</summary>
/// <param name="Width">Across.</param>
/// <param name="Height">Down.</param>
public readonly record struct SfLedgerRecordStruct(int Width, int Height);

/// <summary>EnumDeclaration, and EnumMemberDeclaration for each of its three members.</summary>
public enum SfLedgerKind
{
    /// <summary>Nothing recorded.</summary>
    Empty = 0,

    /// <summary>Money out.</summary>
    Debit = 1,

    /// <summary>Money in.</summary>
    Credit = 2,
}

/// <summary>DelegateDeclaration — a delegate type, whose parameters are Parameter rows too.</summary>
/// <param name="entry">The entry that moved.</param>
/// <param name="kind">Which way it moved.</param>
/// <returns>The new total.</returns>
public delegate int SfLedgerHandler(SfLedgerClass.SfNestedEntry entry, SfLedgerKind kind);

/// <summary>
/// TypeParameter and TypeParameterConstraintClause — a generic type with a constrained
/// parameter, and a generic method with a second constraint clause of its own.
/// </summary>
/// <typeparam name="TItem">What the box holds.</typeparam>
public sealed class SfLedgerBox<TItem>
    where TItem : notnull
{
    private readonly TItem _held;

    /// <summary>Wraps one item.</summary>
    /// <param name="held">The item.</param>
    public SfLedgerBox(TItem held) => _held = held;

    /// <summary>What the box holds.</summary>
    public TItem Held => _held;

    /// <summary>
    /// A generic method, so a TypeParameter and a TypeParameterConstraintClause exist at
    /// member scope as well as at type scope — the two positions bind through different
    /// containers and only one of them is a type declaration.
    /// </summary>
    /// <typeparam name="TOther">The value type to pair with.</typeparam>
    /// <param name="other">The value to pair with.</param>
    /// <returns>The pair.</returns>
    public SfLedgerBox<TItem> PairWith<TOther>(TOther other)
        where TOther : struct, System.IComparable<TOther> => this;
}
