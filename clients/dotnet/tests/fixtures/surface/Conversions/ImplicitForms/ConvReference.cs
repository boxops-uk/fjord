using System;
using System.Collections;
using System.Collections.Generic;

namespace Surface.Conversions.ImplicitForms;

/// <summary>Something that can say what it is. The interface an implicit reference conversion reaches.</summary>
public interface IConvLabelled
{
    /// <summary>What to call it.</summary>
    string Label { get; }
}

/// <summary>The base of the corpus's reference-conversion hierarchy.</summary>
public class ConvNode
{
    /// <summary>What to call it.</summary>
    public virtual string Label => "node";
}

/// <summary>A node with no children, and the derived end of every upcast here.</summary>
public class ConvLeaf : ConvNode, IConvLabelled
{
    /// <inheritdoc/>
    public override string Label => "leaf";
}

/// <summary>A covariant producer, so that clause 10.2.8's variance conversion has a subject.</summary>
/// <typeparam name="T">Produced, never consumed.</typeparam>
public interface IConvProducer<out T>
{
    /// <summary>Makes one.</summary>
    T Produce();
}

/// <summary>A contravariant consumer, the other half of clause 10.2.8's variance conversion.</summary>
/// <typeparam name="T">Consumed, never produced.</typeparam>
public interface IConvConsumer<in T>
{
    /// <summary>Takes one.</summary>
    void Consume(T value);
}

/// <summary>
/// Clause 10.2.8, deliberate hazard: one class implementing one interface at two type
/// arguments, with an explicit implementation for each. The two <c>Produce</c> members have
/// the same name and the same (empty) parameter list, and differ only in the interface each
/// names and in what it returns.
/// </summary>
public sealed class ConvDoubleProducer : IConvProducer<string>, IConvProducer<object>
{
    /// <summary>The <c>string</c> instantiation's member.</summary>
    string IConvProducer<string>.Produce() => "string";

    /// <summary>The <c>object</c> instantiation's member, same name, same parameter list.</summary>
    object IConvProducer<object>.Produce() => "object";
}

/// <summary>
/// Clause 10.2.8 — the implicit reference conversions. None of them is a declaration and
/// none emits code; every one of them is a place where the type of an expression changes
/// with nothing in the source to point at.
/// </summary>
public static class ConvReference
{
    /// <summary>Clause 10.2.8 — derived class to base class.</summary>
    public static ConvNode LeafToNode(ConvLeaf leaf) => leaf;

    /// <summary>Clause 10.2.8 — any reference type to <c>object</c>.</summary>
    public static object LeafToObject(ConvLeaf leaf) => leaf;

    /// <summary>Clause 10.2.8 — class to an interface it implements.</summary>
    public static IConvLabelled LeafToInterface(ConvLeaf leaf) => leaf;

    /// <summary>Clause 10.2.8 — array covariance, which is checked at run time.</summary>
    public static object[] StringsToObjects(string[] names) => names;

    /// <summary>Clause 10.2.8 — an array to <see cref="IList{T}"/> of its element type.</summary>
    public static IList<string> ArrayToList(string[] names) => names;

    /// <summary>Clause 10.2.8 — the variance conversion on a framework interface.</summary>
    public static IEnumerable<object> ToObjectSequence(IEnumerable<string> names) => names;

    /// <summary>Clause 10.2.8 — <c>out</c> variance on this project's own interface.</summary>
    public static IConvProducer<object> Widen(IConvProducer<string> producer) => producer;

    /// <summary>Clause 10.2.8 — <c>in</c> variance, which runs the other way.</summary>
    public static IConvConsumer<string> Narrow(IConvConsumer<object> consumer) => consumer;

    /// <summary>Clause 10.2.8 — any array type to <see cref="Array"/>.</summary>
    public static Array IntsToArray(int[] values) => values;

    /// <summary>Clause 10.2.8 — any array type to the non-generic <see cref="IEnumerable"/>.</summary>
    public static IEnumerable IntsToSequence(int[] values) => values;

    /// <summary>Clause 10.2.8 — any delegate type to <see cref="Delegate"/>.</summary>
    public static Delegate FuncToDelegate(Func<int> callback) => callback;

    /// <summary>Clause 10.2.8 — one interface to a base interface of it.</summary>
    public static IEnumerable ToBaseInterface(IEnumerable<int> values) => values;

    /// <summary>Clause 10.2.8 — the two <c>Produce</c> members, reached one at a time.</summary>
    public static string ProduceString(ConvDoubleProducer producer) =>
        ((IConvProducer<string>)producer).Produce();

    /// <summary>Clause 10.2.8 — and the other one, through the other instantiation.</summary>
    public static object ProduceObject(ConvDoubleProducer producer) =>
        ((IConvProducer<object>)producer).Produce();
}
