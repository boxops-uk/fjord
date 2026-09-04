// Clause 7.4.3 (struct members): a struct's members are the ones it declares plus the ones
// it inherits from `System.ValueType`, and every kind it may declare is declared here. The
// name shared across this file and the class, interface, enum and delegate files is
// `Extent` — one simple name, five containers, so the container is the only thing an
// index has to tell them apart with.

namespace Surface.Lexical.Members;

/// <summary>7.4.3: every kind of member a struct may declare.</summary>
public struct LexStructMembers
{
    /// <summary>A constant, which is static whether or not it says so.</summary>
    public const int Extent = 1;

    /// <summary>An instance field, which a struct may not initialise inline.</summary>
    public int Held;

    /// <summary>A static field.</summary>
    public static int Shared;

    /// <summary>A static constructor, which runs before the first static access.</summary>
    static LexStructMembers() => Shared = Extent;

    /// <summary>An instance constructor with parameters.</summary>
    public LexStructMembers(int held) => Held = held;

    /// <summary>A property.</summary>
    public int Doubled => Held * 2;

    /// <summary>The one indexer this type declares — a second would be a second
    /// <c>this[...]</c>, which the corpus keeps in its own quarantine project.</summary>
    public int this[int offset] => Held + offset;

    /// <summary>An event, declared with explicit accessors because a struct's field-like
    /// event would need an initialiser.</summary>
    public event System.Action Raised
    {
        add { }
        remove { }
    }

    /// <summary>A method.</summary>
    public int Read() => Held + Extent + Shared;

    /// <summary>An operator.</summary>
    public static LexStructMembers operator +(LexStructMembers left, LexStructMembers right) =>
        new(left.Held + right.Held);

    /// <summary>A nested type, which is a member of the struct and not of the namespace.</summary>
    public struct Nested
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 2;
    }
}

/// <summary>
/// 7.4.3: a <c>readonly struct</c>, whose every instance member is implicitly readonly, and
/// which declares an explicit parameterless constructor.
/// </summary>
public readonly struct LexReadonlyStructMembers
{
    /// <summary>The same simple name as the struct above, in a different container.</summary>
    public const int Extent = 3;

    /// <summary>An explicit parameterless constructor, which a struct may declare.</summary>
    public LexReadonlyStructMembers() => Held = Extent;

    /// <summary>A parameterised constructor beside it.</summary>
    public LexReadonlyStructMembers(int held) => Held = held;

    /// <summary>A readonly instance field.</summary>
    public int Held { get; }
}

/// <summary>7.4.3: a <c>record struct</c>, whose positional parameter is a member.</summary>
/// <param name="Extent">The same simple name again, as a positional member this time.</param>
public readonly record struct LexRecordStructMembers(int Extent);
