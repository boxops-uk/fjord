namespace Surface.Bindings.Interfaces;

/// <summary>
/// M24 — one member, one name, and nothing in the index links either implementation of it
/// to the interface member they implement.
/// </summary>
/// <remarks>
/// <para>
/// <c>Indexer.Relate</c> writes four edge kinds. <c>contains</c> from
/// <c>ContainingSymbol as INamedTypeSymbol</c>; <c>extends</c> and <c>implements</c> only
/// under <c>if (symbol is INamedTypeSymbol type)</c>, so <c>implements</c> is type-to-type
/// and there is no member arm at all; and <c>overrides</c> only under
/// <c>if (symbol.IsOverride)</c>. An implementation of an interface member has
/// <c>IsOverride == false</c> — measured, for both the implicit and the explicit form — so
/// it reaches none of the four.
/// </para>
/// <para>
/// <b>The consequence is that a use through the interface reaches the interface and
/// stops.</b> <see cref="BindThroughInterface.Sum"/> is the only call site here, and every
/// row it writes targets <c>IBindSink.Accept</c>. <c>BindImplicitSink.Accept</c> and
/// <c>BindExplicitSink</c>'s explicit implementation both have a definition row and an
/// empty <c>csharp.EntityRef</c> fan-out: a reader standing on the code that runs cannot
/// find out what calls it, and a reader standing on the interface cannot find out what
/// runs.
/// </para>
/// <para>
/// <b><c>overrides</c> is the one member-level heritage edge that exists</b>, and
/// <see cref="BindDerived.Weight"/> is here to hold it — so the corpus can assert that
/// <c>Relation {kind = overrides}</c> is non-empty while
/// <c>Relation {kind = implements}</c> has no row whose <c>from</c> is a method. The two
/// live side by side in one file because the difference between them is not visible in
/// the source: both are a derived member standing in for a base one.
/// </para>
/// <para>
/// <b>The explicit implementation's <c>Name</c> is the qualified interface member's
/// name</b> — measured as <c>Surface.Bindings.Interfaces.IBindSink.Accept</c> — which
/// <c>ScipSymbols.Name</c> backtick-escapes because it is not a simple identifier. So the
/// two implementations do not collide with each other, and neither collides with the
/// interface member. This project declares <b>one member per name per interface</b> for
/// exactly that reason: two explicit implementations of two constructed forms of one
/// generic interface is the term-descriptor collision that kills a run, and it is
/// quarantined elsewhere.
/// </para>
/// </remarks>
public interface IBindSink
{
    /// <summary>The one member, implemented twice and called through here only.</summary>
    /// <param name="weight">What to accept.</param>
    void Accept(int weight);
}

/// <summary>M24 — the implicit implementation: no edge, and no reachable use.</summary>
public sealed class BindImplicitSink : IBindSink
{
    /// <summary>What has been accepted.</summary>
    public int Total { get; private set; }

    /// <summary>
    /// Implements <c>IBindSink.Accept</c>. <c>IsOverride</c> is false, so
    /// <c>Relate</c> writes nothing for it.
    /// </summary>
    /// <param name="weight">What to accept.</param>
    public void Accept(int weight) => Total += weight;
}

/// <summary>M24 — the explicit implementation: a qualified name, and no edge either.</summary>
public sealed class BindExplicitSink : IBindSink
{
    private int _total;

    /// <summary>What has been accepted.</summary>
    public int Total => _total;

    /// <summary>
    /// Implements <c>IBindSink.Accept</c> explicitly, so its <c>Name</c> carries the
    /// interface's full name and its descriptor is backtick-escaped.
    /// </summary>
    /// <param name="weight">What to accept.</param>
    void IBindSink.Accept(int weight) => _total += weight;
}

/// <summary>M24 — the contrast: a virtual member, and the one edge that is written.</summary>
public abstract class BindBase
{
    /// <summary>Overridden by <c>BindDerived.Weight</c>.</summary>
    public virtual int Weight() => 1;
}

/// <summary>M24 — <c>IsOverride</c> is true here, so this member does get an edge.</summary>
public sealed class BindDerived : BindBase
{
    /// <summary>
    /// The one member in this project that <c>Relate</c> writes a heritage edge for.
    /// </summary>
    public override int Weight() => 2;
}

/// <summary>M24 — every use, and every one of them stops at the interface.</summary>
public static class BindThroughInterface
{
    /// <summary>
    /// The only call site for <c>IBindSink.Accept</c>, and it names the interface
    /// member.
    /// </summary>
    /// <param name="sink">Either implementation, reached through the interface.</param>
    public static void Sum(IBindSink sink) => sink.Accept(1);

    /// <summary>
    /// Builds both implementations and hands them out as the interface, so both types are
    /// constructed and neither implementation is named.
    /// </summary>
    public static int Both()
    {
        IBindSink implicitly = new BindImplicitSink();
        IBindSink explicitly = new BindExplicitSink();

        Sum(implicitly);
        Sum(explicitly);

        return new BindDerived().Weight();
    }
}
