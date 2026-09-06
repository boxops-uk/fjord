using System.Text;

namespace Surface.Synthesised;

/// <summary>
/// A positional record — the densest source of members no source declares.
/// </summary>
/// <remarks>
/// From this one declaration the compiler synthesises, with the record's identifier as
/// their only <c>Location</c> and with an empty <c>DeclaringSyntaxReferences</c>:
/// <list type="bullet">
///   <item>the primary constructor <c>.ctor(string, int)</c> — <c>MethodKind.Constructor</c></item>
///   <item>the copy constructor <c>.ctor(SynEntry)</c>, protected — also <c>MethodKind.Constructor</c></item>
///   <item><c>&lt;Clone&gt;$()</c>, whose name no C# identifier can spell</item>
///   <item><c>Deconstruct(out string, out int)</c> — <c>MethodKind.Ordinary</c></item>
///   <item><c>Equals(object?)</c> and <c>Equals(SynEntry?)</c> — two methods, one name, one span</item>
///   <item><c>GetHashCode()</c> and <c>ToString()</c>, both overrides</item>
///   <item><c>PrintMembers(StringBuilder)</c>, protected virtual</item>
///   <item><c>op_Equality</c> and <c>op_Inequality</c> — <c>MethodKind.UserDefinedOperator</c>
///         with no operator declaration anywhere in source</item>
///   <item>for each of <c>Key</c> and <c>Weight</c>: a property, its <c>get_</c> accessor
///         (<c>MethodKind.PropertyGet</c>), its <c>init_</c> accessor
///         (<c>MethodKind.PropertySet</c>) and a <c>&lt;Key&gt;k__BackingField</c></item>
/// </list>
/// Nineteen members from two identifiers and a pair of types. Nothing here is a
/// declaration in the syntax sense, and an indexer walking declaration nodes finds none
/// of it — which is the fact worth pinning.
/// </remarks>
public record SynEntry(string Key, int Weight);

/// <summary>A record deriving from a record: heritage changes what is synthesised.</summary>
/// <remarks>
/// <c>Key</c> and <c>Weight</c> are <em>not</em> re-synthesised here — they are inherited,
/// and the positional parameters of that name exist only to feed
/// <c>SynEntry(Key, Weight)</c>. <c>Stamp</c> is new, so it gets a property and a backing
/// field. Because the record is sealed, <c>PrintMembers</c> is synthesised as
/// <c>protected override</c> rather than <c>protected virtual</c>, and
/// <c>Equals(SynEntry?)</c> is synthesised as a <em>sealed override</em> beside the new
/// <c>Equals(SynStamped?)</c> — so a sealed derived record carries three <c>Equals</c>
/// methods at one span.
/// </remarks>
public sealed record SynStamped(string Key, int Weight, long Stamp) : SynEntry(Key, Weight);

/// <summary>A record struct: the same shape minus the reference-type half.</summary>
/// <remarks>
/// A record struct synthesises no <c>&lt;Clone&gt;$</c> and no copy constructor — a struct
/// is copied by assignment — and its <c>Equals(SynPoint3)</c> takes the type by value, not
/// as a nullable reference. Being <c>readonly</c>, its positional members are
/// <c>get</c>-only rather than <c>get</c>/<c>init</c>. A query that assumes "record" means
/// "has a Clone" is wrong here, which is why both spellings are in the corpus.
/// </remarks>
public readonly record struct SynPoint3(double X, double Y, double Z);

/// <summary>A non-readonly record struct, which does get <c>init</c> accessors.</summary>
public record struct SynCursor(int Row, int Column);

/// <summary>
/// A positional record that declares one of its positional members by hand.
/// </summary>
/// <remarks>
/// <c>Ordinal</c> is declared, so it is <em>not</em> synthesised: the property here is the
/// one the <c>Deconstruct</c> and <c>PrintMembers</c> the compiler still writes will read,
/// and the primary constructor no longer assigns it — the initializer does, which is why
/// the parameter is named in it. <c>Label</c> beside it is synthesised as usual. So one
/// record declaration holds one declared property and one undeclared property of the same
/// kind, and the parameter <c>Ordinal</c>, the property <c>Ordinal</c> and the
/// <c>Deconstruct</c> out-parameter <c>Ordinal</c> are three symbols spelled alike.
/// </remarks>
public record SynExplicit(int Ordinal, string Label)
{
    /// <summary>The declared half of the pair.</summary>
    public int Ordinal { get; init; } = Ordinal;
}

/// <summary>A record that writes every synthesisable member by hand.</summary>
/// <remarks>
/// The counterpart to <see cref="SynEntry"/>: here <c>ToString</c>, <c>PrintMembers</c>,
/// <c>Equals(SynHandWritten?)</c>, <c>GetHashCode</c> and <c>Deconstruct</c> all have real
/// declaration syntax, so an index built from declaration nodes holds all five. Only
/// <c>Equals(object?)</c>, <c>&lt;Clone&gt;$</c>, the copy constructor and the equality
/// operators remain synthesised. A query can subtract this type from
/// <see cref="SynEntry"/> to say exactly which members synthesis contributed.
/// </remarks>
public record SynHandWritten(int Left, int Right)
{
    /// <inheritdoc/>
    public override string ToString() => $"[{Left}:{Right}]";

    /// <summary>The declared strongly typed equality, replacing the synthesised one.</summary>
    public virtual bool Equals(SynHandWritten? other) =>
        other is not null && other.Left == Left && other.Right == Right;

    /// <inheritdoc/>
    public override int GetHashCode() => (Left * 397) ^ Right;

    /// <summary>The declared deconstructor, replacing the synthesised one.</summary>
    public void Deconstruct(out int left, out int right)
    {
        left = Left;
        right = Right;
    }

    /// <summary>The declared member printer, replacing the synthesised one.</summary>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append(Left).Append(", ").Append(Right);
        return true;
    }
}

/// <summary>Uses the members nothing declares, so that they are also referenced.</summary>
public static class SynRecordUse
{
    /// <summary>
    /// Every call below binds to a synthesised method: <c>with</c> invokes
    /// <c>&lt;Clone&gt;$</c> and then the <c>init</c> accessor, <c>==</c> invokes
    /// <c>op_Equality</c>, and the tuple assignment invokes <c>Deconstruct</c>. These are
    /// references whose target has no declaration — the asymmetric half of the index.
    /// </summary>
    public static string Exercise()
    {
        var first = new SynEntry("alpha", 1);
        var copied = first with { Weight = 2 };
        var same = first == copied;
        var (key, weight) = copied;

        var stamped = new SynStamped("beta", 3, 99L);
        var restamped = stamped with { Stamp = 100L };

        var point = new SynPoint3(1.0, 2.0, 3.0);
        var (x, y, z) = point;
        var cursor = new SynCursor(4, 5) { Column = 6 };

        var explicitly = new SynExplicit(7, "gamma") { Ordinal = 8 };
        var hand = new SynHandWritten(9, 10);
        var (left, right) = hand;

        return $"{key}{weight}{same}{restamped}{x}{y}{z}{cursor}{explicitly}{left}{right}{hand.GetHashCode()}";
    }
}
