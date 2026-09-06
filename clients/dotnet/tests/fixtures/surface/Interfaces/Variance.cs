// Clause 19.2.3 — variant type parameter lists. 19.2.3.1 gives the annotations, 19.2.3.2 the
// safety rule that decides where an annotated parameter may appear, and 19.2.3.3 the conversion
// the annotation buys.

using System.Collections.Generic;

namespace Surface.Interfaces;

/// <summary>
/// 19.2.3.1 — a covariant type parameter. <c>out</c> makes <c>TItem</c> usable in output
/// positions only, which is what lets <see cref="IfaceVarianceUse.Widen"/> convert.
/// </summary>
/// <typeparam name="TItem">19.2.3.1 — annotated <c>out</c>.</typeparam>
public interface IfaceSource<out TItem>
{
    /// <summary>19.2.3.2 — an output position: a property with only a getter.</summary>
    TItem Current { get; }

    /// <summary>19.2.3.2 — an output position: a return type.</summary>
    TItem Take();
}

/// <summary>
/// 19.2.3.1 — a contravariant type parameter. <c>in</c> makes <c>TItem</c> usable in input
/// positions only.
/// </summary>
/// <typeparam name="TItem">19.2.3.1 — annotated <c>in</c>.</typeparam>
public interface IfaceSink<in TItem>
{
    /// <summary>19.2.3.2 — an input position: a parameter type.</summary>
    void Give(TItem item);

    /// <summary>19.2.3.2 — an input position again, with a non-variant return.</summary>
    bool Accepts(TItem item);
}

/// <summary>
/// 19.2.3.1 hazard — one declaration whose type parameter list mixes all three variances:
/// contravariant, covariant and invariant. Every constructed form of this interface below is a
/// different type built from this one declaration, and the annotations are part of the
/// declaration rather than of any of them.
/// </summary>
/// <typeparam name="TIn">19.2.3.1 — contravariant.</typeparam>
/// <typeparam name="TOut">19.2.3.1 — covariant.</typeparam>
/// <typeparam name="TState">19.2.3.1 — invariant: no annotation, so it may appear anywhere.</typeparam>
public interface IfaceChannel<in TIn, out TOut, TState>
{
    /// <summary>19.2.3.2 — input in, output out: the safe arrangement.</summary>
    TOut Convert(TIn input);

    /// <summary>19.2.3.2 — an invariant parameter in both positions at once, which is safe only
    /// because it carries no annotation.</summary>
    TState State { get; set; }
}

/// <summary>
/// 19.2.3.2 — variance safety demonstrated as the positions that are safe rather than as the
/// error text for the ones that are not. A covariant parameter may appear as an input to a
/// contravariant interface, because two flips make an output position.
/// </summary>
/// <typeparam name="TItem">19.2.3.2 — covariant, and used below in three safe positions.</typeparam>
public interface IfaceSafeSource<out TItem>
{
    /// <summary>19.2.3.2 — output position, nested: <c>IfaceSource</c> is itself covariant.</summary>
    IfaceSource<TItem> Nested { get; }

    /// <summary>
    /// 19.2.3.2 — an input position holding a contravariant construction of the same parameter.
    /// A parameter position flips the variance and <c>IfaceSink&lt;in T&gt;</c> flips it back,
    /// so <c>TItem</c> is output-safe here.
    /// </summary>
    void Drain(IfaceSink<TItem> sink);

    /// <summary>19.2.3.2 — the framework's own covariant interface in an output position.</summary>
    IEnumerable<TItem> All();
}

/// <summary>19.2.3.3 — somewhere for the conversions below to start from.</summary>
public sealed class IfaceStringSource : IfaceSource<string>
{
    /// <summary>19.2.3.3 — the constructed interface this class implements is <c>IfaceSource&lt;string&gt;</c>.</summary>
    public string Current => "string";

    public string Take() => Current;
}

/// <summary>19.2.3.3 — and something to convert in the other direction.</summary>
public sealed class IfaceObjectSink : IfaceSink<object>
{
    /// <summary>19.2.3.3 — implements <c>IfaceSink&lt;object&gt;</c>.</summary>
    public void Give(object item)
    {
    }

    public bool Accepts(object item) => item is not null;
}

/// <summary>
/// 19.2.3.3 — the variance conversions themselves. Each is an implicit reference conversion
/// between two constructed types built from one generic interface declaration.
/// </summary>
public static class IfaceVarianceUse
{
    /// <summary>
    /// 19.2.3.3 hazard — <c>IfaceSource&lt;string&gt;</c> converts to
    /// <c>IfaceSource&lt;object&gt;</c> with no cast. Two constructed types, one declaration,
    /// and the conversion is written nowhere: it is the type of the return expression.
    /// </summary>
    public static IfaceSource<object> Widen(IfaceSource<string> source) => source;

    /// <summary>19.2.3.3 — the contravariant direction: a sink of <c>object</c> is a sink of
    /// <c>string</c>.</summary>
    public static IfaceSink<string> Narrow(IfaceSink<object> sink) => sink;

    /// <summary>19.2.3.3 — the two annotations converting at once on one type.</summary>
    public static IfaceChannel<string, object, int> Both(IfaceChannel<object, string, int> channel) =>
        channel;

    /// <summary>19.2.3.3 — the same conversion on a framework interface, so the corpus holds a
    /// variance conversion whose declaration is outside it.</summary>
    public static IEnumerable<object> WidenFramework(IEnumerable<string> items) => items;

    /// <summary>19.2.3.3 — a variance conversion through a concrete implementation, so the
    /// converted expression's static type and its run-time type differ.</summary>
    public static object CurrentOfWidened()
    {
        IfaceSource<object> widened = new IfaceStringSource();
        return widened.Current;
    }

    /// <summary>19.2.3.3 — a variance conversion in a type test rather than an assignment.</summary>
    public static bool SinkAcceptsStrings(IfaceObjectSink sink) => sink is IfaceSink<string>;
}
