using System;

namespace Surface.SyntaxForms.Declarations;

// Every member-declaration kind that reaches the walk's switch through
// `BaseMethodDeclarationSyntax`, `BasePropertyDeclarationSyntax`, `EnumMemberDeclarationSyntax`
// or a field declarator — plus the two constructor-initializer kinds, which are references a
// constructor makes with no name node of their own.

/// <summary>The base half of the BaseConstructorInitializer row.</summary>
public abstract class SfMemberBase
{
    /// <summary>Seeds the running total.</summary>
    /// <param name="opening">The opening balance.</param>
    protected SfMemberBase(int opening) => Opening = opening;

    /// <summary>What the ledger opened at.</summary>
    public int Opening { get; }
}

/// <summary>
/// One type carrying every ordinary member form: field, event (both spellings), property,
/// indexer, method, constructor, static constructor, destructor, operator, and both
/// conversion operators.
/// </summary>
/// <remarks>
/// <b>One indexer, deliberately.</b> A second <c>this[…]</c> on this type would be the
/// two-indexers shape that the quarantine projects own; <see cref="SfAccessorHost"/> and
/// <see cref="Expressions.SfIndexTarget"/> each carry their own single indexer instead.
/// </remarks>
public sealed class SfMemberHost : SfMemberBase, SfLedgerContract
{
    // FieldDeclaration with two VariableDeclarators — one declaration, two field symbols,
    // which is the step the walk takes through `VariableDeclarator` rather than through the
    // declaration node.
    private int _debits, _credits;

    /// <summary>A read-only field with an initialiser, so a declarator carries a value too.</summary>
    public static readonly string Currency = "GBP";

    /// <summary>EventFieldDeclaration — the field-like spelling of an event.</summary>
    public event SfLedgerHandler? Posted;

    // ConstructorDeclaration, with a BaseConstructorInitializer.
    /// <summary>Opens a ledger.</summary>
    /// <param name="opening">The opening balance.</param>
    public SfMemberHost(int opening)
        : base(opening)
    {
    }

    // ConstructorDeclaration, with a ThisConstructorInitializer.
    /// <summary>Opens a ledger at zero.</summary>
    public SfMemberHost()
        : this(0)
    {
    }

    // ConstructorDeclaration in its static form — the same syntax kind, a different
    // `MethodKind`, and the row the sibling slice owns from the symbol side.
    static SfMemberHost() => Epoch = DateTime.UnixEpoch;

    // DestructorDeclaration.
    /// <summary>Nothing to release; the finalizer exists so the kind is present.</summary>
    ~SfMemberHost()
    {
    }

    /// <summary>When the type was first touched, set by the static constructor.</summary>
    public static DateTime Epoch { get; }

    /// <summary>PropertyDeclaration with a get accessor and a set accessor.</summary>
    public int Total
    {
        get => _debits - _credits;
        set => _debits = value + _credits;
    }

    /// <summary>PropertyDeclaration with a get accessor and an init accessor.</summary>
    public string Label { get; init; } = "unnamed";

    /// <summary>
    /// PropertyDeclaration whose body is an ArrowExpressionClause — the declaring syntax of
    /// a getter the compiler synthesises, which has no accessor node to hang off.
    /// </summary>
    public int Size => _debits + _credits;

    /// <summary>EventDeclaration — the property-like spelling, with add and remove accessors.</summary>
    public event SfLedgerHandler Cleared
    {
        add => Posted += value;
        remove => Posted -= value;
    }

    /// <summary>IndexerDeclaration — the only <c>this[…]</c> on this type.</summary>
    /// <param name="kind">Which side to read.</param>
    /// <returns>The total on that side.</returns>
    public int this[SfLedgerKind kind] => kind == SfLedgerKind.Debit ? _debits : _credits;

    /// <summary>MethodDeclaration in its ordinary form, with a block body.</summary>
    /// <returns>A rendering of the ledger.</returns>
    public string Describe()
    {
        return $"{Currency} {Total} of {Size}";
    }

    /// <summary>MethodDeclaration with an ArrowExpressionClause body.</summary>
    /// <param name="amount">How much to post.</param>
    public void Post(int amount) => _debits += amount;

    /// <summary>OperatorDeclaration — a user-defined binary operator.</summary>
    /// <param name="left">One ledger.</param>
    /// <param name="right">The other.</param>
    /// <returns>Their sum.</returns>
    public static SfMemberHost operator +(SfMemberHost left, SfMemberHost right) =>
        new(left.Opening + right.Opening);

    /// <summary>OperatorDeclaration in its unary form.</summary>
    /// <param name="ledger">The ledger to negate.</param>
    /// <returns>A ledger opened at the negation.</returns>
    public static SfMemberHost operator -(SfMemberHost ledger) => new(-ledger.Opening);

    /// <summary>ConversionOperatorDeclaration — the implicit half.</summary>
    /// <param name="ledger">The ledger to read.</param>
    public static implicit operator int(SfMemberHost ledger) => ledger.Total;

    /// <summary>ConversionOperatorDeclaration — the explicit half.</summary>
    /// <param name="ledger">The ledger to read.</param>
    public static explicit operator string(SfMemberHost ledger) => ledger.Describe();
}

/// <summary>
/// MethodDeclaration in its explicit-interface form, which is a different declared name for
/// the same syntax kind.
/// </summary>
/// <remarks>
/// A type of its own so the explicitly implemented <c>Describe</c> has no same-named
/// ordinary sibling: an implementing half and an overload sharing a name is the ordinal
/// shape a quarantine project owns, and while nothing here is partial, keeping the two
/// spellings apart costs nothing.
/// </remarks>
public sealed class SfExplicitImplementor : SfLedgerContract
{
    /// <summary>The interface's property, implemented explicitly.</summary>
    int SfLedgerContract.Size => 0;

    /// <summary>The interface's method, implemented explicitly.</summary>
    /// <returns>A fixed rendering.</returns>
    string SfLedgerContract.Describe() => "explicit";
}
