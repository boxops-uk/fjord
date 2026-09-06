namespace Surface.Bindings.Constructors;

/// <summary>The base whose constructor <c>: base(...)</c> selects.</summary>
public class BindConstructedBase
{
    /// <summary>Selected by <c>: base(weight)</c>, and referenced by nothing.</summary>
    /// <param name="weight">The weight.</param>
    public BindConstructedBase(int weight) => Weight = weight;

    /// <summary>The weight.</summary>
    public int Weight { get; }
}

/// <summary>
/// M30 — <c>: base(...)</c> and <c>: this(...)</c> are <c>ConstructorInitializerSyntax</c>
/// with no name node, and <c>new Derived(2)</c>'s only name node is the type — so a
/// constructor has no reference row from any spelling C# offers.
/// </summary>
/// <remarks>
/// <para>
/// A constructor initializer has its own <c>GetSymbolInfo</c> overload — one that takes a
/// <c>ConstructorInitializerSyntax</c>, because there is no expression and no name to ask
/// about. Measured: <c>: base(weight)</c> contains one <c>SimpleNameSyntax</c> and it is
/// the argument <c>weight</c>; <c>: this(1)</c> contains none at all. So the walk sees a
/// node kind it does not dispatch, resolves nothing, and writes nothing — not a reference
/// row, not a <c>csharp.MethodInvocationLocation</c> (the node is not an
/// <c>InvocationExpressionSyntax</c>), and not an <c>ObjectCreationLocation</c> (it is not
/// a <c>BaseObjectCreationExpressionSyntax</c> either).
/// </para>
/// <para>
/// <b>And a <c>new</c> expression names the type, not the constructor.</b> Measured:
/// <c>new BindConstructed(2)</c> has one child <c>SimpleNameSyntax</c> — the type name —
/// which binds to the class and is keyed <c>new_</c> because <c>CodeMarkup.Role</c> tests
/// <c>BaseObjectCreationExpressionSyntax</c> ancestry. The chosen constructor is recovered
/// separately, by <c>Indexer.Created</c>, which writes a <c>csharp.ObjectCreationLocation
/// {type, constructor, location}</c> and nothing on the <c>codemarkup</c> surface.
/// </para>
/// <para>
/// <b>So the two layers disagree about whether constructors are used.</b>
/// <c>csharp.ObjectCreationLocation</c> is non-empty and names the constructor;
/// <c>codemarkup.FileXRef</c> holds no row anywhere in the corpus whose target's descriptor
/// carries <c>`.ctor`()</c>. One anti-join is the gate, and it has to be stated as a
/// property of the <c>codemarkup</c> layer alone, because the <c>csharp</c> layer answers
/// correctly and a query over both would look complete.
/// </para>
/// <para>
/// <b>A target-typed <c>new()</c> has no name node at all</b> — measured, zero — so
/// <see cref="BindConstruction.TargetTyped"/> writes no reference row even for the type.
/// <c>Created</c> falls back to <c>creation.NewKeyword.Span</c>, so the
/// <c>ObjectCreationLocation</c> row exists and is drawn over the three characters
/// <c>new</c>. Which means the corpus contains a construction whose type is nowhere in the
/// reference index, and the row that does exist points at a keyword.
/// </para>
/// </remarks>
public sealed class BindConstructed : BindConstructedBase
{
    /// <summary>
    /// Chains to the base with <c>: base(weight)</c>, which is a use of
    /// <c>BindConstructedBase(int)</c> that no row records.
    /// </summary>
    /// <param name="weight">The weight.</param>
    public BindConstructed(int weight)
        : base(weight)
    {
    }

    /// <summary>
    /// Chains to a sibling with <c>: this(1)</c>, which contains no name node whatsoever.
    /// </summary>
    public BindConstructed()
        : this(1)
    {
    }
}

/// <summary>M30 — the three ways to reach a constructor from an expression.</summary>
public static class BindConstruction
{
    /// <summary>
    /// Named type, explicit argument: one reference row, for the <i>type</i>, keyed
    /// <c>new_</c>.
    /// </summary>
    public static int Made() => new BindConstructed(2).Weight;

    /// <summary>
    /// Named type, no arguments, reaching the constructor that chains with <c>: this</c>.
    /// </summary>
    public static int Defaulted() => new BindConstructed().Weight;

    /// <summary>Target-typed: no name node, so no reference row at all.</summary>
    public static int TargetTyped()
    {
        BindConstructed made = new(3);

        return made.Weight;
    }
}
