// Clause 11 — patterns and pattern matching: the input side.
//
// Nothing in this file is a pattern. It is the vocabulary every pattern in this project is
// applied to — the *pattern input value* of 11.1 — kept apart so that the other files can
// be about the pattern forms of 11.2 and nothing else. Each member says which pattern form
// binds it, because that binding is the whole point: a positional pattern names no
// `Deconstruct`, a property pattern names a property with no receiver, and a list pattern
// names neither `Length` nor an indexer. The declarations are here; the uses are elsewhere
// and are silent.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Patterns;

/// <summary>
/// A shape: the type a declaration pattern (11.2.2) narrows an <c>object</c> to, and the
/// root of the closed hierarchy the exhaustiveness rules of 11.1 are demonstrated over.
/// </summary>
public abstract class PatShape
{
    /// <param name="label">What the constant patterns of 11.2.3 compare against.</param>
    protected PatShape(string label) => Label = label;

    /// <summary>
    /// Bound by name, with no name at the use site, by a property pattern (11.2.6).
    /// </summary>
    public string Label { get; }

    /// <summary>How much of the plane the shape covers.</summary>
    public abstract double Extent { get; }
}

/// <summary>
/// A circle, and the one type here with <b>two</b> <c>Deconstruct</c> methods — which is
/// how a positional pattern (11.2.5) picks a binding by subpattern count alone.
/// </summary>
public sealed class PatCircle : PatShape
{
    /// <param name="radius">The radius, which a property pattern matches relationally.</param>
    public PatCircle(double radius)
        : base("circle") => Radius = radius;

    /// <summary>Bound by <c>{ Radius: &gt; 1 }</c> in 11.2.6.</summary>
    public double Radius { get; }

    /// <inheritdoc/>
    public override double Extent => Math.PI * Radius * Radius;

    /// <summary>The one-subpattern deconstruction: <c>is PatCircle(var r)</c>.</summary>
    /// <param name="radius">The radius.</param>
    public void Deconstruct(out double radius) => radius = Radius;

    /// <summary>The two-subpattern deconstruction: <c>is PatCircle(var r, var name)</c>.</summary>
    /// <param name="radius">The radius.</param>
    /// <param name="label">The label, so the two overloads differ in more than arity.</param>
    public void Deconstruct(out double radius, out string label)
    {
        radius = Radius;
        label = Label;
    }
}

/// <summary>A square, with one deconstruction and a settable property.</summary>
public sealed class PatSquare : PatShape
{
    /// <param name="side">The side length.</param>
    public PatSquare(double side)
        : base("square") => Side = side;

    /// <summary>
    /// Bound by a property pattern, and by the second subpattern of a positional one.
    /// </summary>
    public double Side { get; }

    /// <summary>Which enum member a constant pattern (11.2.3) compares this against.</summary>
    public PatKind Kind { get; init; } = PatKind.Boxed;

    /// <inheritdoc/>
    public override double Extent => Side * Side;

    /// <summary>The only deconstruction: <c>is PatSquare(var side, var name)</c>.</summary>
    /// <param name="side">The side length.</param>
    /// <param name="label">The label.</param>
    public void Deconstruct(out double side, out string label)
    {
        side = Side;
        label = Label;
    }
}

/// <summary>
/// A shape with <b>no</b> <c>Deconstruct</c> of its own: its positional pattern binds an
/// extension method in another type entirely (11.2.5), and its <c>Tag</c> is a field
/// rather than a property, so a property pattern (11.2.6) is shown binding one of each.
/// </summary>
public sealed class PatBlob : PatShape
{
    /// <summary>A blob is labelled once and for all.</summary>
    public PatBlob()
        : base("blob")
    {
    }

    /// <summary>A <b>field</b> a property pattern binds — 11.2.6 admits fields too.</summary>
    public int Tag;

    /// <inheritdoc/>
    public override double Extent => Tag;
}

/// <summary>
/// The deconstruction of <see cref="PatBlob"/>, written outside it.
/// </summary>
/// <remarks>
/// A positional pattern will bind an <b>extension</b> <c>Deconstruct</c>, so the member a
/// pattern reaches need not live on the type the pattern names — and there is still no name
/// at the use site to record the reach.
/// </remarks>
public static class PatBlobDeconstruction
{
    /// <summary>Lets <c>blob is (var name, var tag)</c> bind (11.2.5).</summary>
    /// <param name="blob">The blob.</param>
    /// <param name="label">Its label.</param>
    /// <param name="tag">Its tag.</param>
    public static void Deconstruct(this PatBlob blob, out string label, out int tag)
    {
        label = blob.Label;
        tag = blob.Tag;
    }
}

/// <summary>
/// What kind of thing a shape is — the enum the constant patterns of 11.2.3 name.
/// </summary>
public enum PatKind
{
    /// <summary>Nothing known.</summary>
    Unknown = 0,

    /// <summary>A circle, or something like one.</summary>
    Round = 1,

    /// <summary>A square, or something like one.</summary>
    Boxed = 2,
}

/// <summary>The constants a constant pattern (11.2.3) compares against by name.</summary>
public static class PatConstants
{
    /// <summary>A constant pattern's target that is a <c>const</c> field.</summary>
    public const int Limit = 7;

    /// <summary>A constant pattern's target that is a <c>const string</c>.</summary>
    public const string RoundLabel = "circle";

    /// <summary>
    /// A <c>static readonly</c> field, which is <b>not</b> a constant and so cannot be a
    /// pattern.
    /// </summary>
    /// <remarks>
    /// Here to be pointed at: 11.2.3 requires a <i>constant expression</i>, so
    /// <c>v is NearLimit</c> does not compile and the field is only ever read normally.
    /// </remarks>
    public static readonly int NearLimit = 6;
}

/// <summary>A base with a <c>const</c> that a derived type hides (11.2.3).</summary>
public class PatThresholdBase
{
    /// <summary>The base threshold — one of two declarations with this simple name.</summary>
    public const int Threshold = 3;
}

/// <summary>
/// The derived type whose <c>new const</c> hides the base one, so that two declarations
/// answer to the simple name <c>Threshold</c> and a constant pattern's qualifier decides
/// which (11.2.3).
/// </summary>
public class PatThresholdDerived : PatThresholdBase
{
    /// <summary>The hiding threshold — the other declaration with this simple name.</summary>
    public new const int Threshold = 9;
}

/// <summary>A base with a property a derived type hides (11.2.6).</summary>
public class PatWeighedBase
{
    /// <summary>The base weight — one of two properties with this simple name.</summary>
    public int Weight { get; init; }

    /// <summary>
    /// A nullable value type, so a property pattern can match <c>{ Tag: 5 }</c> against one.
    /// </summary>
    public int? Tag { get; init; }
}

/// <summary>
/// The derived type whose <c>new</c> property hides the base one: a property pattern binds
/// whichever the <i>static</i> type of the input selects (11.2.6).
/// </summary>
public sealed class PatWeighedDerived : PatWeighedBase
{
    /// <summary>The hiding weight — the other property with this simple name.</summary>
    public new int Weight { get; init; }

    /// <summary>What a nested property pattern recurses into.</summary>
    public PatWeighedBase Inner { get; init; } = new();
}

/// <summary>Something a nested and an extended property pattern reach through (11.2.6).</summary>
public sealed class PatCrate
{
    /// <summary>The shape inside, or nothing.</summary>
    public PatShape? Held { get; init; }

    /// <summary>What kind the crate declares itself to be.</summary>
    public PatKind Kind { get; init; }

    /// <summary>A crate inside the crate, so a pattern can nest twice.</summary>
    public PatCrate? Nested { get; init; }
}

/// <summary>A note-bearing thing, so an interface-typed input can be property-matched.</summary>
public interface IPatNoted
{
    /// <summary>The note a property pattern binds on the <i>interface</i>.</summary>
    string Note { get; }
}

/// <summary>The one implementation of <see cref="IPatNoted"/>.</summary>
public sealed class PatNoted : IPatNoted
{
    /// <inheritdoc/>
    public string Note => "noted";
}

/// <summary>
/// A sequence a list pattern walks: <c>Length</c>, one indexer and <c>Slice</c> — the three
/// members a list pattern and a slice pattern bind, none of them named at the use site.
/// </summary>
public sealed class PatRun
{
    private readonly int[] _values;

    /// <param name="values">The values, in order.</param>
    public PatRun(params int[] values) => _values = values;

    /// <summary>What a list pattern's length check binds.</summary>
    public int Length => _values.Length;

    /// <summary>
    /// What a list pattern's element access binds. There is exactly one indexer here, on
    /// purpose.
    /// </summary>
    /// <param name="index">Which element.</param>
    public int this[int index] => _values[index];

    /// <summary>What a slice pattern's <c>..</c> binds.</summary>
    /// <param name="start">Where the slice starts.</param>
    /// <param name="length">How long it is.</param>
    public PatRun Slice(int start, int length) => new(_values[start..(start + length)]);
}

/// <summary>
/// A pair reached by a positional pattern through <see cref="ITuple"/> rather than through
/// a <c>Deconstruct</c> (11.2.5).
/// </summary>
/// <remarks>
/// The interface is implemented <b>explicitly</b>, so the indexer and the length a pattern
/// uses are not even members of the public surface — and the pattern still binds them.
/// </remarks>
public sealed class PatPair : ITuple
{
    /// <param name="first">The first element.</param>
    /// <param name="second">The second element.</param>
    public PatPair(int first, string second)
    {
        First = first;
        Second = second;
    }

    /// <summary>The first element, readable by name as well as positionally.</summary>
    public int First { get; }

    /// <summary>The second element.</summary>
    public string Second { get; }

    /// <inheritdoc/>
    int ITuple.Length => 2;

    /// <inheritdoc/>
    object? ITuple.this[int index] => index switch
    {
        0 => First,
        1 => Second,
        _ => throw new IndexOutOfRangeException(nameof(index)),
    };
}

/// <summary>
/// A record, whose <c>Deconstruct</c> the compiler writes: a positional pattern over it
/// binds a member that exists and was never written down (11.2.5).
/// </summary>
/// <param name="Start">Where the extent begins.</param>
/// <param name="Count">How long it is.</param>
public sealed record PatExtent(int Start, int Count);

/// <summary>
/// A box, so that a declaration pattern can name a <i>constructed generic</i> type and a
/// pattern can be written inside an object-creation argument list (11.2.1).
/// </summary>
/// <typeparam name="T">What is in the box.</typeparam>
public sealed class PatBox<T>
{
    /// <param name="held">What to put in the box.</param>
    public PatBox(T held) => Held = held;

    /// <summary>What is in the box.</summary>
    public T Held { get; }
}

/// <summary>
/// A witness for the evaluation order of 11.2.1: reading <see cref="Next"/> has an effect,
/// so which subpatterns ran is observable at run time — and is recorded nowhere.
/// </summary>
public sealed class PatCounter
{
    private int _reads;

    /// <summary>How many times <see cref="Next"/> has been read.</summary>
    public int Reads => _reads;

    /// <summary>A property whose read is a side effect.</summary>
    public int Next => ++_reads;

    /// <summary>A property whose read is not.</summary>
    public int Steady { get; init; }
}
